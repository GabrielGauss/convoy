using System.Runtime.CompilerServices;
using Convoy.Core;
using Convoy.Vehicle;
using Unity.Profiling;
using UnityEngine;

// Lets the PlayMode perf test call Tick() directly to measure exactly this component's cost and GC.
[assembly: InternalsVisibleTo("Convoy.Tests.PlayMode")]

namespace Convoy.AI
{
    /// <summary>
    /// Runtime adapter for one AI rival (D-019, ADR-004). Every physics step it <b>senses</b> (the vehicle's
    /// <see cref="VehicleState"/> + three sphere-casts), asks the pure <see cref="AiDriverBrain"/> for a
    /// <see cref="PilotCommand"/>, and sends it through <see cref="IPilotCommandReceiver.SetPilotCommand"/> —
    /// the same Pilot-seat path a human's input takes. No physics shortcuts; it never reads the player's position
    /// (except the temporary start hold below), so there is no rubber-banding.
    /// <para>Also the vehicle's <see cref="IRecoveryPointProvider"/>: a V-02 reset puts the rival back on its
    /// racing line, behind where it was, facing along the line.</para>
    /// <para>Wiring: Inspector (scenes/editor scripts) or <see cref="Configure"/> (code). With
    /// <see cref="Configure"/>, call it before the GameObject becomes active (or right after AddComponent).</para>
    /// </summary>
    /// <remarks>
    /// <c>[DefaultExecutionOrder(-50)]</c>: Unity runs this FixedUpdate before the vehicle's (order 0) in the
    /// same physics step, so the vehicle simulates the command decided this step, not last step's.
    /// </remarks>
    [RequireComponent(typeof(VehicleController))]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public sealed class AiPilot : MonoBehaviour, IRecoveryPointProvider
    {
        // Enough for the car's own colliders plus a few obstacles; extra hits are dropped, which only
        // matters if > 8 colliders sit inside one 25 m cast.
        private const int MaxHits = 8;
        private const int LaneCount = 3; // −1, 0, +1 (ADR-004)

        // Shows up in the Profiler as its own row: AC 6 (3 rivals < 0.1 ms, 0 B GC) is read from it.
        private static readonly ProfilerMarker StepMarker = new ProfilerMarker("Convoy.AiPilot.FixedUpdate");

        [Tooltip("The route this rival drives.")]
        [SerializeField] private RacingLine _line;
        [Tooltip("Driver personality (AI_Steady / AI_Balanced / AI_Aggressive).")]
        [SerializeField] private AiDriverProfile _profile;
        [Tooltip("Layers the obstacle sphere-casts can hit. Ground hits are filtered by slope, so Default is fine.")]
        [SerializeField] private LayerMask _obstacleMask = ~0;
        [Tooltip("Temporary until R-01: hold until this vehicle starts moving.")]
        [SerializeField] private VehicleController _waitForVehicle;

        private readonly RaycastHit[] _hits = new RaycastHit[MaxHits];
        private readonly float[] _laneFree = new float[LaneCount];

        private VehicleController _vehicle;
        private Rigidbody _body;
        private PolylinePath _path;
        private AiDriverBrain _brain;
        private bool _awake;

        /// <summary>The decision logic; null until wired. Read-only use for HUD/debug/race code.</summary>
        public AiDriverBrain Brain => _brain;

        public RacingLine Line => _line;

        /// <summary>
        /// Wires the rival from code (tests, runtime spawners). Editor scripts should set the serialized fields
        /// instead so the scene keeps them. Rebuilds the brain (allocates: setup time only).
        /// </summary>
        public void Configure(RacingLine line, AiDriverProfile profile, VehicleController waitForVehicle = null)
        {
            _line = line;
            _profile = profile;
            _waitForVehicle = waitForVehicle;
            if (!_awake) return; // Awake will build it
            BuildBrain();
            if (_brain != null) enabled = true; // Start may have disabled us for missing wiring
        }

        /// <summary>Starts driving now. R-01 calls this when its countdown ends.</summary>
        public void Release()
        {
            _waitForVehicle = null;
            if (_brain != null) _brain.Held = false;
        }

        /// <summary>Path pose <c>recoveryBackoff</c> metres behind current progress, facing along the path. Never ahead.</summary>
        public bool TryGetRecoveryPose(out Pose pose)
        {
            if (_brain == null)
            {
                pose = default;
                return false;
            }

            float distance = _brain.Progress - _profile.recoveryBackoff;
            // Open line: clamp at the start. (Closed lines wrap; PositionAt handles that.)
            if (!_path.Closed && distance < 0f) distance = 0f;

            Vector3 tangent = _path.TangentAt(distance);
            pose = new Pose(_path.PositionAt(distance), Quaternion.LookRotation(tangent, Vector3.up));
            return true;
        }

        private void Awake()
        {
            _vehicle = GetComponent<VehicleController>();
            _body = GetComponent<Rigidbody>();
            _awake = true;
            BuildBrain();
        }

        private void Start()
        {
            // Reported in Start, not Awake, so Configure() right after AddComponent doesn't log a false error.
            if (_brain != null) return;
            Debug.LogError($"{name}: AiPilot needs a RacingLine with a valid path, an AiDriverProfile and a " +
                           "VehicleConfig on the VehicleController. The rival will not drive.", this);
            enabled = false;
        }

        private void OnDestroy()
        {
            // AI removed while the car lives on (e.g. a human takes over): stop handing out AI path poses, so the
            // vehicle falls back to its own safe pose. R-01 re-registers its provider when it owns recovery.
            if (_vehicle != null && _brain != null) _vehicle.SetRecoveryPointProvider(null);
        }

        private void BuildBrain()
        {
            _brain = null;
            _path = _line != null ? _line.Path : null;
            if (_path == null || _profile == null || _vehicle == null || _vehicle.Config == null) return;

            _brain = new AiDriverBrain(_path, _profile, _vehicle.Config, _line.HalfWidth)
            {
                Held = _waitForVehicle != null,
            };
            _vehicle.SetRecoveryPointProvider(this);
        }

        private void FixedUpdate()
        {
            Tick();
        }

        /// <summary>One sense → decide → act step. Called from FixedUpdate; the perf test calls it directly. 0 B GC.</summary>
        internal void Tick()
        {
            if (_brain == null) return;
            using (StepMarker.Auto())
            {
                VehicleState state = _vehicle.State;

                // The vehicle writes State at the end of its own FixedUpdate; before its first step the state is all
                // zeros (position = world origin), which would make the brain lock onto the wrong part of the path.
                if (state.TotalWheels == 0)
                {
                    _vehicle.SetPilotCommand(PilotCommand.Neutral);
                    return;
                }

                UpdateStartHold();

                Vector3 forward = state.Rotation * Vector3.forward;
                SenseObstacles(state.Position);

                var frame = new AiSensorFrame
                {
                    DeltaTime = Time.fixedDeltaTime,
                    Position = state.Position,
                    Forward = forward,
                    Velocity = state.Velocity,
                    ForwardSpeed = state.ForwardSpeed,
                    GroundedWheels = state.GroundedWheels,
                    ObstacleLeft = _laneFree[0],
                    ObstacleCentre = _laneFree[1],
                    ObstacleRight = _laneFree[2],
                    RecoveryCooldown = state.RecoveryCooldown,
                };

                _vehicle.SetPilotCommand(_brain.Step(frame));
            }
        }

        // Temporary until R-01 (ADR-004 "Start of race"): same rule SprintController uses to start the clock.
        private void UpdateStartHold()
        {
            if (_waitForVehicle == null) return;
            Vector3 v = _waitForVehicle.State.Velocity;
            v.y = 0f;
            float release = _profile.releaseSpeed;
            if (v.sqrMagnitude > release * release) Release();
        }

        // Three sphere-casts along the path direction, one per lane (left, centre, right of the PATH, not of the car),
        // so LaneSelector compares the same corridors it steers into. Result: metres to the first obstacle or +∞.
        private void SenseObstacles(Vector3 position)
        {
            float progress = _brain.Progress;
            Vector3 direction = _path.TangentAt(progress);
            Vector3 right = PolylinePath.RightOf(direction);

            // The car sits roughly at its current lane offset; shift each cast origin from there to its lane.
            float carOffset = _brain.LaneOffset;
            float halfWidth = _line.HalfWidth;
            Vector3 origin = position + Vector3.up * _profile.obstacleCastHeight;

            for (int lane = 0; lane < LaneCount; lane++)
            {
                // Same lane geometry as LaneSelector: (lane − 1) × laneWidth, clamped to the route half-width.
                float laneOffset = Mathf.Clamp((lane - 1) * _profile.laneWidth, -halfWidth, halfWidth);
                Vector3 laneOrigin = origin + right * (laneOffset - carOffset);
                _laneFree[lane] = CastLane(laneOrigin, direction);
            }
        }

        private float CastLane(Vector3 origin, Vector3 direction)
        {
            // NonAlloc + a preallocated buffer: Physics.SphereCastAll would allocate a new array every call.
            int count = Physics.SphereCastNonAlloc(origin, _profile.obstacleRadius, direction, _hits,
                _profile.obstacleCheckDistance, _obstacleMask, QueryTriggerInteraction.Ignore);

            float nearest = float.PositiveInfinity;
            float walkable = _profile.walkableNormalUp;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];
                if (hit.rigidbody == _body) continue; // our own colliders

                // Unity reports colliders the sphere already overlaps at the start with distance 0 and a
                // meaningless normal. Static ones are usually the ground on rough terrain → ignore; a
                // dynamic one (another car right beside us in that lane) is a real obstacle.
                if (hit.distance <= 0f)
                {
                    if (hit.rigidbody == null) continue;
                }
                else if (Vector3.Dot(hit.normal, Vector3.up) > walkable)
                {
                    continue; // ramps and mounds face up: drivable, not obstacles (ADR-004)
                }

                if (hit.distance < nearest) nearest = hit.distance;
            }
            return nearest;
        }
    }
}
