using System;

namespace Convoy.Vehicle
{
    /// <summary>
    /// Remembers, per wheel, the last ground collider (by instance ID) and the SurfaceProfile found on it, so
    /// VehicleController only calls TryGetComponent when a wheel moves onto a different collider (LV-02 perf
    /// constraint: no per-step GetComponent). Stores the profile reference, not its numbers, so Inspector edits
    /// apply live. Plain C# class with fixed-size arrays allocated once in the constructor: lookups are 0 B GC.
    /// Collider instance IDs (int) are used instead of Collider references so this stays testable without scenes.
    /// </summary>
    public sealed class WheelSurfaceCache
    {
        private readonly int[] _colliderIds;
        private readonly SurfaceProfile[] _profiles;
        // Separate validity flag instead of a sentinel ID: Unity instance IDs can be any int (negative or positive).
        private readonly bool[] _valid;

        public WheelSurfaceCache(int wheelCount)
        {
            if (wheelCount < 0) throw new ArgumentOutOfRangeException(nameof(wheelCount));
            _colliderIds = new int[wheelCount];
            _profiles = new SurfaceProfile[wheelCount];
            _valid = new bool[wheelCount];
        }

        /// <summary>Hit when this wheel's last stored collider is the same one; profile may be null (= default ground).</summary>
        public bool TryGet(int wheel, int colliderInstanceId, out SurfaceProfile profile)
        {
            if (_valid[wheel] && _colliderIds[wheel] == colliderInstanceId)
            {
                profile = _profiles[wheel];
                return true;
            }

            profile = null;
            return false;
        }

        /// <summary>Records what TryGetComponent found on a miss. A null profile is cached too: untagged ground is the common case.</summary>
        public void Store(int wheel, int colliderInstanceId, SurfaceProfile profile)
        {
            _colliderIds[wheel] = colliderInstanceId;
            _profiles[wheel] = profile;
            _valid[wheel] = true;
        }

        /// <summary>Wheel airborne: forget the collider so the next landing always re-resolves.</summary>
        public void Clear(int wheel)
        {
            _valid[wheel] = false;
            // Drop the reference so a cleared slot never keeps an asset alive or returns stale data.
            _profiles[wheel] = null;
        }
    }
}
