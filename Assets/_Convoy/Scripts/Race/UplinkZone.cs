using System;
using System.Collections.Generic;
using Convoy.Vehicle;
using UnityEngine;

namespace Convoy.Race
{
    /// <summary>
    /// The Monolith uplink volume (UP-01, D-025): an oriented box (this transform's position/rotation,
    /// local half extents). Geometric test instead of a trigger: a stopped Rigidbody falls asleep and stops
    /// sending OnTriggerStay — exactly the "hold still" case (ADR-006 § 5).
    ///
    /// D-031: winning the Páramo = first vehicle to complete the uplink, player or rival. So the zone is
    /// vehicle-agnostic and owns the single "who won" answer (<see cref="Winner"/> / <see cref="Completed"/>):
    /// - The player's hold is ticked by <see cref="SprintController"/> (it gates on its run: started, all
    ///   checkpoints passed) and reported here via <see cref="ReportCompleted"/>.
    /// - Any other vehicle (future AI rivals, A-01/R-01) is registered with <see cref="Track"/>; the zone
    ///   ticks those holds itself with <see cref="_trackedConfig"/>.
    /// Never Track a vehicle a SprintController already owns, or its hold would be ticked twice.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UplinkZone : MonoBehaviour
    {
        // Small pre-sized list: Track is the only place that can allocate; FixedUpdate never does.
        private const int InitialTrackedCapacity = 8;

        [Tooltip("Half size of the uplink box in this object's local axes, metres. Keep the transform scale at 1.")]
        [SerializeField] private Vector3 _halfExtents = new Vector3(8f, 6f, 8f);
        [Tooltip("Hold rules for vehicles registered with Track (AI rivals). The player's rules come from the SprintController's config.")]
        [SerializeField] private UplinkConfig _trackedConfig;

        private sealed class TrackedVehicle
        {
            public VehicleController Vehicle;
            public UplinkHold Hold;
        }

        private readonly List<TrackedVehicle> _tracked = new List<TrackedVehicle>(InitialTrackedCapacity);

        public Vector3 HalfExtents => _halfExtents;

        /// <summary>First vehicle that completed the uplink, or null while nobody has.</summary>
        public VehicleController Winner { get; private set; }
        public bool HasWinner => Winner != null;

        /// <summary>Raised once, with the winning vehicle, when the first uplink completes (D-031).</summary>
        public event Action<VehicleController> Completed;

        private void Awake()
        {
            // Contains() works in unscaled local space so the Inspector half extents are true metres.
            // A scaled transform would silently shrink/grow the zone, so flag it.
            if ((transform.lossyScale - Vector3.one).sqrMagnitude > 1e-6f)
                Debug.LogWarning($"{name}: UplinkZone ignores transform scale; set scale to 1 and use Half Extents.", this);
        }

        /// <summary>True if <paramref name="worldPoint"/> is inside the oriented box (faces inclusive).</summary>
        public bool Contains(Vector3 worldPoint)
        {
            Vector3 p = transform.InverseTransformPoint(worldPoint);
            return Mathf.Abs(p.x) <= _halfExtents.x
                && Mathf.Abs(p.y) <= _halfExtents.y
                && Mathf.Abs(p.z) <= _halfExtents.z;
        }

        /// <summary>
        /// Record a completed uplink. The first call wins: sets <see cref="Winner"/> and raises
        /// <see cref="Completed"/>. Returns true only for that winning call.
        /// </summary>
        public bool ReportCompleted(VehicleController vehicle)
        {
            if (vehicle == null || Winner != null) return false;
            Winner = vehicle;
            Completed?.Invoke(vehicle);
            return true;
        }

        /// <summary>
        /// Let the zone run the uplink hold for a vehicle that has no SprintController (AI rivals).
        /// Returns the hold so the owner/HUD can read progress, or null if no tracked config is assigned.
        /// Idempotent: tracking the same vehicle twice returns the existing hold.
        /// </summary>
        public UplinkHold Track(VehicleController vehicle)
        {
            if (vehicle == null) return null;
            UplinkHold existing = GetTrackedHold(vehicle);
            if (existing != null) return existing;
            if (_trackedConfig == null)
            {
                Debug.LogError($"{name}: UplinkZone has no Tracked Config; cannot track {vehicle.name}.", this);
                return null;
            }
            var entry = new TrackedVehicle { Vehicle = vehicle, Hold = new UplinkHold(_trackedConfig) };
            _tracked.Add(entry);
            return entry.Hold;
        }

        public void Untrack(VehicleController vehicle)
        {
            for (int i = _tracked.Count - 1; i >= 0; i--)
                if (_tracked[i].Vehicle == vehicle) _tracked.RemoveAt(i);
        }

        /// <summary>The zone-ticked hold for a tracked vehicle, or null.</summary>
        public UplinkHold GetTrackedHold(VehicleController vehicle)
        {
            for (int i = 0; i < _tracked.Count; i++)
                if (_tracked[i].Vehicle == vehicle) return _tracked[i].Hold;
            return null;
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            for (int i = 0; i < _tracked.Count; i++)
            {
                TrackedVehicle t = _tracked[i];
                // Unity's == null is also true for a destroyed vehicle (e.g. a wrecked rival removed from the scene).
                if (t.Vehicle == null) continue;
                VehicleState state = t.Vehicle.State;
                bool inside = Contains(state.Position);
                if (t.Hold.Tick(dt, inside, state.Velocity.magnitude)) ReportCompleted(t.Vehicle);
            }
        }

        private void OnDrawGizmos()
        {
            // Draw in the zone's own space so the wire box follows its rotation, like Contains().
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(Vector3.zero, _halfExtents * 2f);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
