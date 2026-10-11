using System;
using Convoy.Core;
using UnityEngine;

namespace Convoy.Vehicle
{
    /// <summary>
    /// Arcade raycast vehicle (D-004): one Rigidbody, a spring/damper ray per wheel,
    /// and an authored tyre model with a friction circle. All numbers come from <see cref="VehicleConfig"/>.
    /// Receives <see cref="PilotCommand"/>s from input or AI; exposes <see cref="State"/> for presentation.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public sealed class VehicleController : MonoBehaviour, IPilotCommandReceiver
    {
        [Serializable]
        public sealed class Wheel
        {
            [Tooltip("Empty child at the top of this wheel's suspension. The ray is cast straight down from here.")]
            public Transform anchor;
            [Tooltip("Optional mesh moved to the contact point and spun (visual only).")]
            public Transform visual;
            public bool steers;
            public bool driven = true;
            [Tooltip("Rear wheels: affected by the handbrake and use rear grip.")]
            public bool rear;
        }

        [SerializeField] private VehicleConfig _config;
        [SerializeField] private Wheel[] _wheels = Array.Empty<Wheel>();
        [Tooltip("Layers the suspension can stand on. The vehicle's own colliders are ignored automatically.")]
        [SerializeField] private LayerMask _groundMask = ~0;

        private const int MaxRayHits = 8;

        private Rigidbody _body;
        private PilotCommand _command;
        private float[] _lastCompression = Array.Empty<float>();
        private float[] _wheelSpin = Array.Empty<float>();
        private readonly RaycastHit[] _hits = new RaycastHit[MaxRayHits];
        private VehicleState _state;

        public VehicleConfig Config => _config;
        public VehicleState State => _state;
        public PilotCommand CurrentCommand => _command;

        public void SetPilotCommand(PilotCommand command)
        {
            _command = command.Clamped();
        }

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _lastCompression = new float[_wheels.Length];
            _wheelSpin = new float[_wheels.Length];
            ApplyBodySettings();

            if (_config == null)
                Debug.LogError($"{name}: VehicleController has no VehicleConfig assigned.", this);
            ValidateWheels();
        }

        /// <summary>Fails loudly on wiring mistakes that would otherwise leave the car silently immobile.</summary>
        private void ValidateWheels()
        {
            if (_wheels.Length == 0)
            {
                Debug.LogError($"{name}: VehicleController has no wheels. Set Wheels size to 4 and assign anchors.", this);
                return;
            }

            for (int i = 0; i < _wheels.Length; i++)
                if (_wheels[i] == null || _wheels[i].anchor == null)
                    Debug.LogError($"{name}: Wheel element {i} has no Anchor assigned.", this);

            if (CountDriven() == 0)
                Debug.LogError($"{name}: No wheel is marked Driven, so the engine cannot move the car.", this);

            bool anySteers = false;
            foreach (Wheel w in _wheels)
                if (w != null && w.steers && w.anchor != null) anySteers = true;
            if (!anySteers)
                Debug.LogWarning($"{name}: No wheel is marked Steers, so the car cannot turn.", this);
        }

        private void ApplyBodySettings()
        {
            if (_config == null) return;
            _body.mass = _config.mass;
            _body.centerOfMass = _config.centerOfMassOffset;
            _body.angularDamping = _config.angularDamping;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        private void FixedUpdate()
        {
            if (_config == null || _wheels.Length == 0) return;

#if UNITY_EDITOR
            // Live tuning: pick up Inspector changes made during Play mode.
            ApplyBodySettings();
#endif

            float dt = Time.fixedDeltaTime;
            Vector3 up = transform.up;
            Vector3 velocity = _body.linearVelocity;
            float forwardSpeed = Vector3.Dot(velocity, transform.forward);
            float massPerWheel = _config.mass / _wheels.Length;

            VehicleMath.ResolveLongitudinal(_command.Throttle, forwardSpeed, _config.reverseThreshold,
                out float drive, out float brake);

            float driveTopSpeed = drive >= 0f ? _config.topSpeed : _config.reverseTopSpeed;
            float driveFactor = VehicleMath.DriveFactor(forwardSpeed, driveTopSpeed);
            int drivenCount = CountDriven();
            float steerAngle = VehicleMath.SteerAngle(_command.Steer, _config.maxSteerAngle, forwardSpeed,
                _config.topSpeed, _config.highSpeedSteerFactor);

            int grounded = 0;
            bool sliding = false;

            for (int i = 0; i < _wheels.Length; i++)
            {
                Wheel wheel = _wheels[i];
                if (wheel.anchor == null) continue;

                Vector3 origin = wheel.anchor.position;
                float rayLength = _config.suspensionRestLength + _config.wheelRadius;

                if (!TryGroundHit(origin, -up, rayLength, out RaycastHit hit))
                {
                    _lastCompression[i] = 0f;
                    UpdateWheelVisual(i, wheel, origin - up * _config.suspensionRestLength, forwardSpeed, steerAngle);
                    continue;
                }

                grounded++;

                // --- Suspension ---
                float compression = VehicleMath.Compression(_config.suspensionRestLength, hit.distance, _config.wheelRadius);
                float compressionVelocity = (compression - _lastCompression[i]) / dt;
                _lastCompression[i] = compression;
                float normalForce = VehicleMath.SuspensionForce(compression, compressionVelocity,
                    _config.springStiffness, _config.springDamping);
                _body.AddForceAtPosition(up * normalForce, origin, ForceMode.Force);

                // --- Tyre frame on the ground plane ---
                Quaternion steerRotation = wheel.steers ? Quaternion.AngleAxis(steerAngle, up) : Quaternion.identity;
                Vector3 wheelForward = Vector3.ProjectOnPlane(steerRotation * transform.forward, hit.normal).normalized;
                Vector3 wheelRight = Vector3.Cross(hit.normal, wheelForward).normalized;

                Vector3 contact = hit.point;
                Vector3 pointVelocity = _body.GetPointVelocity(contact);
                float wheelForwardSpeed = Vector3.Dot(pointVelocity, wheelForward);
                float wheelLateralSpeed = Vector3.Dot(pointVelocity, wheelRight);

                // --- Longitudinal: drive, brake, rolling resistance ---
                float longitudinal = 0f;
                if (wheel.driven && drivenCount > 0)
                    longitudinal += drive * _config.engineForce * driveFactor / drivenCount;

                longitudinal += VehicleMath.BrakeForce(wheelForwardSpeed, brake,
                    _config.brakeForce / _wheels.Length, massPerWheel, dt);

                if (wheel.rear && _command.Handbrake)
                    longitudinal += VehicleMath.BrakeForce(wheelForwardSpeed, 1f,
                        _config.handbrakeBrakeForce, massPerWheel, dt);

                longitudinal -= wheelForwardSpeed * _config.rollingResistance * massPerWheel;

                // --- Lateral grip ---
                float grip = wheel.rear ? _config.rearLateralGrip : _config.frontLateralGrip;
                if (wheel.rear && _command.Handbrake) grip *= _config.handbrakeRearGripMultiplier;
                float lateral = VehicleMath.LateralGripForce(wheelLateralSpeed, grip, massPerWheel, dt);

                // --- Friction circle: tyres can only transmit mu × load ---
                sliding |= VehicleMath.ClampToFrictionCircle(ref longitudinal, ref lateral,
                    _config.tireFriction * normalForce);

                _body.AddForceAtPosition(wheelForward * longitudinal + wheelRight * lateral, contact, ForceMode.Force);

                UpdateWheelVisual(i, wheel, origin - up * (hit.distance - _config.wheelRadius), wheelForwardSpeed, steerAngle);
            }

            // --- Downforce (only while touching the ground, so jumps still fly) ---
            if (grounded > 0)
                _body.AddForce(-up * _config.downforce * velocity.sqrMagnitude, ForceMode.Force);
            else
                ApplyAirControl();

            WriteState(grounded, sliding);
        }

        private bool TryGroundHit(Vector3 origin, Vector3 direction, float length, out RaycastHit closest)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, _hits, length, _groundMask, QueryTriggerInteraction.Ignore);
            closest = default;
            float best = float.MaxValue;
            bool found = false;
            for (int h = 0; h < count; h++)
            {
                // Ignore the vehicle's own colliders.
                if (_hits[h].rigidbody == _body) continue;
                if (_hits[h].distance < best)
                {
                    best = _hits[h].distance;
                    closest = _hits[h];
                    found = true;
                }
            }
            return found;
        }

        private void ApplyAirControl()
        {
            // Throttle pitches, steer rolls+yaws, so a mistimed jump can be saved.
            Vector3 torque = transform.right * _command.Throttle * _config.airControl
                           + transform.up * _command.Steer * _config.airControl * 0.5f
                           - transform.forward * _command.Steer * _config.airControl * 0.5f;
            _body.AddTorque(torque, ForceMode.Acceleration);
        }

        private int CountDriven()
        {
            int n = 0;
            foreach (Wheel w in _wheels)
                if (w != null && w.driven && w.anchor != null) n++;
            return n;
        }

        private void UpdateWheelVisual(int index, Wheel wheel, Vector3 centre, float forwardSpeed, float steerAngle)
        {
            if (wheel.visual == null) return;
            _wheelSpin[index] += forwardSpeed / Mathf.Max(0.01f, _config.wheelRadius) * Mathf.Rad2Deg * Time.fixedDeltaTime;
            _wheelSpin[index] %= 360f;
            wheel.visual.position = centre;
            float yaw = wheel.steers ? steerAngle : 0f;
            wheel.visual.rotation = transform.rotation * Quaternion.Euler(_wheelSpin[index], yaw, 0f);
        }

        private void WriteState(int grounded, bool sliding)
        {
            Vector3 velocity = _body.linearVelocity;
            float forward = Vector3.Dot(velocity, transform.forward);
            float lateral = Vector3.Dot(velocity, transform.right);
            float slip = velocity.sqrMagnitude > 1f
                ? Vector3.SignedAngle(transform.forward, Vector3.ProjectOnPlane(velocity, transform.up), transform.up)
                : 0f;

            _state = new VehicleState
            {
                Position = _body.position,
                Rotation = _body.rotation,
                Velocity = velocity,
                AngularVelocity = _body.angularVelocity,
                ForwardSpeed = forward,
                LateralSpeed = lateral,
                SlipAngle = slip,
                GroundedWheels = grounded,
                TotalWheels = _wheels.Length,
                Sliding = sliding,
            };
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_config == null || _wheels == null) return;
            Gizmos.color = Color.cyan;
            foreach (Wheel w in _wheels)
            {
                if (w?.anchor == null) continue;
                Vector3 o = w.anchor.position;
                Vector3 end = o - transform.up * (_config.suspensionRestLength + _config.wheelRadius);
                Gizmos.DrawLine(o, end);
                Gizmos.DrawWireSphere(o - transform.up * _config.suspensionRestLength, _config.wheelRadius);
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(transform.TransformPoint(_config.centerOfMassOffset), 0.08f);
        }
#endif
    }
}
