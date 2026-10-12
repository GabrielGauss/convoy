using UnityEngine;

namespace Convoy.Level
{
    /// <summary>Ballistics for the chasm shortcut, so the gap can be tuned against the Jackal's speed without playtesting each change.</summary>
    public static class ChasmJump
    {
        /// <summary>
        /// Minimum take-off speed, m/s, to clear a <paramref name="gap"/> (m) leaving a ramp of <paramref name="rampAngleDeg"/>
        /// and landing <paramref name="landingDrop"/> m lower. Point mass, no drag:
        /// v² = g·d² / (2·cos²θ·(d·tanθ + drop)). Returns +Infinity when no speed can clear it.
        /// </summary>
        public static float MinClearSpeed(float gap, float rampAngleDeg, float landingDrop, float gravity = 9.81f)
        {
            if (gap <= 0f) return 0f;
            float theta = rampAngleDeg * Mathf.Deg2Rad;
            float cos = Mathf.Cos(theta);
            float denom = 2f * cos * cos * (gap * Mathf.Tan(theta) + landingDrop);
            if (denom <= 0f || cos <= 0f) return float.PositiveInfinity;
            return Mathf.Sqrt(gravity * gap * gap / denom);
        }
    }
}
