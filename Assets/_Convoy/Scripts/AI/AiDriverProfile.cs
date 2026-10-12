using UnityEngine;

namespace Convoy.AI
{
    /// <summary>
    /// All tunable numbers for one AI driver personality (D-019, ADR-004). Create via
    /// Assets ▸ Create ▸ Convoy ▸ AI Driver Profile. Vehicle limits (top speed, max steer)
    /// are deliberately NOT here: the brain reads them from the vehicle's <c>VehicleConfig</c>
    /// so AI and physics can never disagree.
    /// <c>cornerSpeedScale</c> is the main competence knob (0.85 steady … 0.98 aggressive).
    /// There is no rubber-banding field on purpose: no AI code reads the player's position.
    /// </summary>
    /// <remarks>
    /// Fields are public (same convention as <c>VehicleConfig</c>) because this is a plain data
    /// container edited in the Inspector; behaviour code only reads it. Values can be changed
    /// in Play mode and take effect on the next physics step.
    /// </remarks>
    [CreateAssetMenu(fileName = "AiDriverProfile", menuName = "Convoy/AI Driver Profile")]
    public sealed class AiDriverProfile : ScriptableObject
    {
        [Header("Steering")]
        [Tooltip("Lookahead distance at standstill, metres. Target point = progress + lookahead.")]
        [Min(0f)] public float lookaheadBase = 6f;
        [Tooltip("Extra lookahead per m/s of speed, seconds (0.5 → +20 m at 40 m/s). Longer = smoother but cuts corners.")]
        [Min(0f)] public float lookaheadPerSpeed = 0.5f;
        [Tooltip("Steer = angle to target / current max steer angle × this, clamped to ±1.")]
        [Min(0f)] public float steerGain = 1f;

        [Header("Speed")]
        [Tooltip("Sideways acceleration the AI trusts the tyres with in corners, m/s².")]
        [Min(0.1f)] public float maxLateralAccel = 14f;
        [Tooltip("Competence knob: fraction of the theoretical corner speed the AI uses.")]
        [Range(0.5f, 1.2f)] public float cornerSpeedScale = 0.92f;
        [Tooltip("Deceleration the AI plans with when braking for a corner, m/s².")]
        [Min(0.1f)] public float brakingDecel = 12f;
        [Tooltip("How far ahead corners are considered, metres.")]
        [Min(1f)] public float planningHorizon = 120f;
        [Tooltip("Spacing of the curvature samples inside the horizon, metres.")]
        [Min(0.5f)] public float planningStep = 5f;
        [Tooltip("Path length over which curvature is measured, metres. Smooths out single sharp waypoints.")]
        [Min(0.5f)] public float curvatureSpan = 8f;
        [Tooltip("Throttle per m/s of speed error.")]
        [Min(0f)] public float throttleGain = 0.5f;
        [Tooltip("Speed error (m/s) ignored before throttle/brake reacts. Stops pedal chatter around the target speed.")]
        [Min(0f)] public float coastBand = 1f;
        [Tooltip("Use nitro on straights.")]
        public bool useBoost = true;
        [Tooltip("Boost only if no corner within this distance is slower than the vehicle's top speed, metres.")]
        [Min(0f)] public float boostClearDistance = 80f;

        [Header("Lanes")]
        [Tooltip("Sideways distance between avoidance lanes, metres. Lanes are -1, 0, +1 × this, clamped to the route half-width.")]
        [Min(0f)] public float laneWidth = 2.5f;
        [Tooltip("How fast the lane offset moves toward the chosen lane, m/s (no snapping).")]
        [Min(0.1f)] public float laneChangeRate = 3f;
        [Tooltip("Obstacles further than this don't matter; a lane is 'clear' beyond it, metres.")]
        [Min(1f)] public float obstacleCheckDistance = 25f;
        [Tooltip("Sphere-cast radius used by AiPilot (A-01b), metres.")]
        [Min(0.1f)] public float obstacleRadius = 1f;
        [Tooltip("Sphere-cast height above the car's origin (A-01b), metres.")]
        [Min(0f)] public float obstacleCastHeight = 0.8f;
        [Tooltip("Hits whose surface normal · up exceeds this are drivable (ramps), not obstacles (A-01b).")]
        [Range(0f, 1f)] public float walkableNormalUp = 0.5f;

        [Header("Recovery")]
        [Tooltip("Below this speed (m/s) while asking for throttle counts as stuck.")]
        [Min(0f)] public float stuckSpeed = 1f;
        [Tooltip("Seconds of being stuck before reversing. Also the seconds of healthy driving that clear the reverse-attempt counter.")]
        [Min(0f)] public float stuckTime = 2.5f;
        [Tooltip("Seconds to reverse (steer mirrored) per attempt.")]
        [Min(0f)] public float reverseTime = 1.5f;
        [Tooltip("Reverse attempts before asking the vehicle for a reset (V-02).")]
        [Min(0)] public int maxReverseAttempts = 2;
        [Tooltip("Further than this from the path (metres) → reset immediately.")]
        [Min(1f)] public float offPathResetDistance = 25f;
        [Tooltip("Seconds of continuous negative progress (driving the wrong way) before a reset. 0 = off.")]
        [Min(0f)] public float wrongWayTime = 4f;
        [Tooltip("Recovery pose is this far behind current progress, metres (A-01b). Never ahead, so a reset can't skip track.")]
        [Min(0f)] public float recoveryBackoff = 10f;
        [Tooltip("Segments searched either side of last tick's segment when tracking progress. Keeps the search O(window).")]
        [Min(1)] public int projectionWindow = 8;
        [Tooltip("Open paths: the end counts as reached within this distance, metres.")]
        [Min(0f)] public float arriveDistance = 5f;

        [Header("Start (temporary until R-01)")]
        [Tooltip("AiPilot (A-01b) holds until the watched vehicle is faster than this, m/s.")]
        [Min(0f)] public float releaseSpeed = 1.5f;
    }
}
