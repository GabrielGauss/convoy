using System;

namespace Convoy.Core
{
    // Core is engine-free (noEngineReferences in the asmdef): only System types here, no UnityEngine.

    /// <summary>Stable id of a vehicle in a race (assigned by race setup; None = unknown/environment).</summary>
    [Serializable]
    public struct VehicleId : IEquatable<VehicleId>
    {
        /// <summary>0 = None. Positive values identify a vehicle.</summary>
        public int Value;

        public static VehicleId None => default;

        public bool IsValid => Value > 0;

        public bool Equals(VehicleId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is VehicleId other && Equals(other);

        public override int GetHashCode() => Value;

        public static bool operator ==(VehicleId a, VehicleId b) => a.Value == b.Value;

        public static bool operator !=(VehicleId a, VehicleId b) => a.Value != b.Value;

        public override string ToString() => IsValid ? "Vehicle#" + Value : "None";
    }

    public enum DamageKind : byte
    {
        Impact = 0,
        Projectile = 1,
        Explosion = 2,
        Hazard = 3,
    }

    /// <summary>One packet of damage. Plain data so it can be queued/serialized (netcode, M2).</summary>
    [Serializable]
    public struct DamageEvent
    {
        /// <summary>Health points, expected &gt; 0 (non-positive/NaN amounts are ignored by receivers).</summary>
        public float Amount;
        public DamageKind Kind;
        /// <summary>Who caused it (None = environment/unknown).</summary>
        public VehicleId Source;
    }

    /// <summary>Anything that can take damage. The ONLY way damage enters the simulation (single choke point, ADR-003).</summary>
    public interface IDamageable
    {
        /// <returns>Health actually removed (0 if ignored, e.g. already wrecked).</returns>
        float ApplyDamage(in DamageEvent damage);
    }
}
