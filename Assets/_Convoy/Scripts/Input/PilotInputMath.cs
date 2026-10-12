using UnityEngine;

namespace Convoy.Input
{
    /// <summary>
    /// Pure Pilot input maths extracted from PilotInputSource so the future per-seat router (F-03b) feels identical.
    /// Static and allocation-free, so it is safe to call every frame.
    /// </summary>
    public static class PilotInputMath
    {
        /// <summary>accelerate - brake, clamped to [-1, 1] (both pedals/keys held at once cancel out).</summary>
        public static float Throttle(float accelerate, float brake)
        {
            return Mathf.Clamp(accelerate - brake, -1f, 1f);
        }

        /// <summary>Values whose magnitude is below the deadzone become 0; others pass through unchanged (no rescaling).</summary>
        public static float ApplyDeadzone(float value, float deadzone)
        {
            return Mathf.Abs(value) < deadzone ? 0f : value;
        }

        /// <summary>Gamepad stick and shaped keyboard steering at once: whichever has the larger magnitude wins (ties go to the keyboard).</summary>
        public static float CombineSteer(float stick, float keyboard)
        {
            return Mathf.Abs(stick) > Mathf.Abs(keyboard) ? stick : keyboard;
        }
    }
}
