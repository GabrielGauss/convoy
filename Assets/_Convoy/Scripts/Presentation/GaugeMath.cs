using System;

namespace Convoy.Presentation
{
    /// <summary>Pure maths for the speedometer HUD (U-01). Unit-tested.</summary>
    public static class GaugeMath
    {
        /// <summary>
        /// Needle angle in degrees, clockwise from straight up, for a dial that starts at
        /// <paramref name="startDeg"/> and sweeps <paramref name="sweepDeg"/> clockwise.
        /// </summary>
        public static float NeedleAngle(float value, float max, float startDeg, float sweepDeg)
        {
            if (max <= 0f) return startDeg;
            float t = Math.Clamp(value / max, 0f, 1f);
            return startDeg + t * sweepDeg;
        }

        /// <summary>Dial maximum: the vehicle's boosted top speed rounded up to a clean step (e.g. 20 km/h).</summary>
        public static float DialMax(float topSpeedKmh, float step)
        {
            if (step <= 0f) return Math.Max(topSpeedKmh, 1f);
            return Math.Max(step, (float)Math.Ceiling(topSpeedKmh / step) * step);
        }

        /// <summary>Whole km/h shown on the digital readout (never negative; reversing shows speed too).</summary>
        public static int DisplaySpeed(float speedKmh)
        {
            return (int)Math.Round(Math.Abs(speedKmh), MidpointRounding.AwayFromZero);
        }
    }
}
