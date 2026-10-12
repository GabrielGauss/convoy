using System;

namespace Convoy.Race
{
    /// <summary>
    /// Pure maths for the uplink progress ring (UP-01). Lives in Race (not Presentation) so the
    /// EditMode tests and any future HUD (UI Toolkit, world-space ring) share one rounding rule.
    /// </summary>
    public static class UplinkRingMath
    {
        /// <summary>
        /// Number of lit ring segments: ceil(progress01 x segmentCount), clamped to [0, segmentCount].
        /// Ceil, not round, so the very first physics step of a hold lights a segment — the player
        /// gets instant feedback that the uplink started. NaN and segmentCount &lt;= 0 give 0.
        /// </summary>
        public static int LitSegments(float progress01, int segmentCount)
        {
            if (segmentCount <= 0) return 0;
            // NaN fails both comparisons, so it is caught here too.
            if (!(progress01 > 0f)) return 0;
            if (progress01 >= 1f) return segmentCount;
            int lit = (int)Math.Ceiling(progress01 * segmentCount);
            return lit > segmentCount ? segmentCount : lit;
        }
    }
}
