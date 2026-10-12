using UnityEngine;

namespace Convoy.Level
{
    /// <summary>
    /// Pure maths for the Rammer gate, kept out of the MonoBehaviour (LV-01b) so it is testable in EditMode
    /// without a physics scene. Static + value types only: no allocations in the physics-callback hot path.
    /// </summary>
    public static class RammerGateRules
    {
        private const float MpsToKmh = 3.6f;

        // Why a tolerance: the spec requires 22.22 m/s to break an 80 km/h gate, but 80 / 3.6 = 22.2222...,
        // so a strict compare would reject the rounded value people type in. 0.01 km/h (~0.003 m/s) is far
        // below anything perceptible, while 22.2 m/s (79.92 km/h) still correctly fails.
        private const float ThresholdToleranceKmh = 0.01f;

        /// <summary>
        /// Speed toward the wall along its normal, m/s, never negative. The normal is the wall's forward axis
        /// flipped to point from the body to the wall centre, so hits on the back face behave identically and
        /// glancing hits only count their normal component (a 45 degree hit at 25 m/s = 17.68).
        /// </summary>
        /// <param name="bodyToWallCentre">wallCentre - bodyPosition (world space).</param>
        public static float ClosingSpeed(Vector3 velocity, Vector3 wallForward, Vector3 bodyToWallCentre)
        {
            // dot == 0 (body exactly in the wall plane) picks +forward; any consistent choice is fine there.
            float side = Vector3.Dot(bodyToWallCentre, wallForward) < 0f ? -1f : 1f;
            float closing = Vector3.Dot(velocity, wallForward) * side;
            return closing > 0f ? closing : 0f;
        }

        /// <summary>True when the hit is fast enough (inclusive) and the body heavy enough. NaN input never breaks.</summary>
        public static bool ShouldBreak(float closingSpeedMps, float massKg, RammerGateConfig config)
        {
            if (config == null || float.IsNaN(closingSpeedMps) || float.IsNaN(massKg)) return false;
            if (massKg < config.minBreakerMassKg) return false;
            return closingSpeedMps * MpsToKmh >= config.breakSpeedKmh - ThresholdToleranceKmh;
        }

        /// <summary>Scales horizontal (x, z) velocity by <paramref name="retention"/>; keeps y so gravity and jumps are untouched.</summary>
        public static Vector3 RetainedVelocity(Vector3 velocity, float retention)
        {
            return new Vector3(velocity.x * retention, velocity.y, velocity.z * retention);
        }

        /// <summary>
        /// Initial velocity of one debris chunk: a fraction of the breaker's velocity plus a deterministic random
        /// spread (upward-biased so chunks tumble rather than skid). Deterministic per (seed, chunkIndex) so
        /// replays and tests match. Allocates one System.Random, acceptable because it runs once per chunk at
        /// break time only, never per frame.
        /// </summary>
        public static Vector3 DebrisVelocity(Vector3 breakerVelocity, float factor, float spread, int chunkIndex, int seed)
        {
            var rng = new System.Random(seed ^ chunkIndex);
            float sx = (float)(rng.NextDouble() * 2.0 - 1.0);
            float sy = (float)rng.NextDouble();
            float sz = (float)(rng.NextDouble() * 2.0 - 1.0);
            return breakerVelocity * factor + new Vector3(sx, sy, sz) * spread;
        }
    }
}
