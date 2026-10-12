using UnityEngine;

namespace Convoy.Vehicle
{
    /// <summary>Raw damage from one collision contact (pure maths, no engine state).</summary>
    public static class ImpactDamage
    {
        /// <summary>
        /// threshold = normalUpDot &gt; landingNormalDot ? landingSpeedThreshold : impactSpeedThreshold;
        /// damage = min(max(0, deltaV - threshold) * impactDamagePerMps, maxImpactDamage).
        /// NaN or non-positive deltaV gives 0. A NaN normal falls back to the (lower) impact threshold.
        /// </summary>
        public static float Compute(float deltaV, float normalUpDot, DamageConfig config)
        {
            if (!(deltaV > 0f)) return 0f; // also rejects NaN (all comparisons with NaN are false)

            float threshold = normalUpDot > config.landingNormalDot
                ? config.landingSpeedThreshold
                : config.impactSpeedThreshold;

            float damage = (deltaV - threshold) * config.impactDamagePerMps;
            if (!(damage > 0f)) return 0f;
            return Mathf.Min(damage, config.maxImpactDamage);
        }
    }

    /// <summary>
    /// Counts multi-contact bounces once. PhysX reports several contacts for one crash; inside a window
    /// only the excess over the window's largest raw damage is new, so the total equals the largest hit.
    /// Allocation-free after construction.
    /// </summary>
    public sealed class ImpactWindow
    {
        private readonly DamageConfig _config;
        private double _windowStart = double.NegativeInfinity; // forces a new window on first call
        private float _max;

        public ImpactWindow(DamageConfig config)
        {
            _config = config;
        }

        /// <param name="rawDamage">Output of <see cref="ImpactDamage.Compute"/>.</param>
        /// <param name="time">Monotonic time in seconds (the adapter passes Time.fixedTimeAsDouble).</param>
        /// <returns>The damage that is new, i.e. should actually be applied.</returns>
        public float Filter(float rawDamage, double time)
        {
            float raw = rawDamage > 0f ? rawDamage : 0f; // NaN and negatives become 0

            if (time - _windowStart > _config.impactWindowSeconds)
            {
                _windowStart = time;
                _max = raw;
                return raw;
            }

            float fresh = raw - _max;
            if (raw > _max) _max = raw;
            return fresh > 0f ? fresh : 0f;
        }
    }
}
