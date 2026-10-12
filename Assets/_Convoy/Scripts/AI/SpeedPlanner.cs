using System;
using UnityEngine;

namespace Convoy.AI
{
    /// <summary>
    /// Curvature-based speed planning (ADR-004). Samples the path ahead every <c>planningStep</c> up to
    /// <c>planningHorizon</c>; for each sample i at distance dᵢ ahead:
    /// <code>
    /// κᵢ        = CurvatureAt(progress + dᵢ, curvatureSpan)                      (1/m)
    /// v_cornerᵢ = sqrt(maxLateralAccel × cornerSpeedScale / max(κᵢ, 1e-4))
    /// v_allowed = min over i of sqrt(v_cornerᵢ² + 2 × brakingDecel × dᵢ), then min(topSpeed)
    /// </code>
    /// i.e. "the fastest speed from which I can still brake down to every corner's speed in time".
    /// Pure and allocation-free; called every physics step by <see cref="AiDriverBrain"/>.
    /// </summary>
    public static class SpeedPlanner
    {
        // Curvature floor so a dead-straight sample gives a large-but-finite corner speed (no divide by zero).
        private const float MinCurvature = 1e-4f;

        /// <summary>Max speed allowed now so every corner within the horizon can be taken (ADR-004 formula), m/s.</summary>
        public static float TargetSpeed(PolylinePath path, float distance, float vehicleTopSpeed, AiDriverProfile profile)
        {
            float lateralAccel = profile.maxLateralAccel * profile.cornerSpeedScale;
            float allowed = vehicleTopSpeed;
            int samples = SampleCount(path, distance, profile.planningHorizon, profile.planningStep);

            for (int i = 0; i < samples; i++)
            {
                float ahead = i * profile.planningStep;
                float cornerSpeedSq = CornerSpeedSq(path, distance + ahead, lateralAccel, profile.curvatureSpan);
                float reachable = Mathf.Sqrt(cornerSpeedSq + 2f * profile.brakingDecel * ahead);
                if (reachable < allowed) allowed = reachable;
            }
            return Math.Max(0f, allowed);
        }

        /// <summary>
        /// True when boost is enabled in the profile and no corner within <c>boostClearDistance</c> has a corner
        /// speed below the vehicle's top speed — i.e. we're on a straight. Braking for corners further away is
        /// still handled by <see cref="TargetSpeed"/> every step.
        /// </summary>
        public static bool ClearForBoost(PolylinePath path, float distance, float vehicleTopSpeed, AiDriverProfile profile)
        {
            if (!profile.useBoost) return false;

            float lateralAccel = profile.maxLateralAccel * profile.cornerSpeedScale;
            float topSpeedSq = vehicleTopSpeed * vehicleTopSpeed;
            int samples = SampleCount(path, distance, profile.boostClearDistance, profile.planningStep);

            for (int i = 0; i < samples; i++)
            {
                float ahead = i * profile.planningStep;
                if (CornerSpeedSq(path, distance + ahead, lateralAccel, profile.curvatureSpan) < topSpeedSq) return false;
            }
            return true;
        }

        private static float CornerSpeedSq(PolylinePath path, float distance, float lateralAccel, float span)
        {
            float curvature = Math.Max(path.CurvatureAt(distance, span), MinCurvature);
            return lateralAccel / curvature; // v² = a / κ  (a = v²/r, κ = 1/r)
        }

        // Samples at 0, step, 2·step … ≤ horizon. On open paths, samples past the end are skipped
        // (PositionAt would clamp them onto the last point and repeat the same reading).
        private static int SampleCount(PolylinePath path, float distance, float horizon, float step)
        {
            if (step <= 0f) return 1;
            float usable = path.Closed ? horizon : Math.Min(horizon, path.Length - distance);
            if (usable < 0f) return 1;
            return (int)(usable / step) + 1;
        }
    }
}
