using UnityEngine;

namespace Convoy.Vehicle
{
    /// <summary>
    /// Multipliers VehicleController applies to its tuned values. A struct (no heap, no GC) so it can be
    /// cached by the adapter and read in FixedUpdate.
    /// </summary>
    public struct HandlingModifiers
    {
        public float TopSpeed;
        public float EngineForce;
        public float Grip;
        public float Steer;

        /// <summary>All multipliers exactly 1f: the controller behaves exactly as before damage existed.</summary>
        public static HandlingModifiers None => new HandlingModifiers
        {
            TopSpeed = 1f,
            EngineForce = 1f,
            Grip = 1f,
            Steer = 1f,
        };

        /// <summary>
        /// Linear degradation with a dead zone: m(h) = h &gt;= degradeBelow ? 1 : lerp(min, 1, h / degradeBelow).
        /// At/above the threshold the literal 1f is returned (not a lerp, which could yield 0.99999) so
        /// x * 1f == x bit-for-bit and the tuned Jackal is untouched. h is clamped to [0,1]; NaN is treated as full health.
        /// </summary>
        public static HandlingModifiers FromHealth(float health01, DamageConfig config)
        {
            float h = float.IsNaN(health01) ? 1f : Mathf.Clamp01(health01);
            float below = config.degradeBelowHealth01;
            if (h >= below) return None;

            // below > h >= 0 here, so the division is safe.
            float t = h / below;
            return new HandlingModifiers
            {
                TopSpeed = Mathf.Lerp(config.minTopSpeedMultiplier, 1f, t),
                EngineForce = Mathf.Lerp(config.minEngineForceMultiplier, 1f, t),
                Grip = Mathf.Lerp(config.minGripMultiplier, 1f, t),
                Steer = Mathf.Lerp(config.minSteerMultiplier, 1f, t),
            };
        }
    }
}
