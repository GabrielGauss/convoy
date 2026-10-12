using System;
using Convoy.Core;

namespace Convoy.Crew
{
    /// <summary>
    /// Pure rules deciding whether a seat's command counts as "the player is doing something".
    /// Telemetry (Q-01b) uses this to tell an occupied-but-idle seat from an engaged one (D-002 revisit).
    /// </summary>
    public static class SeatActivityRules
    {
        /// <summary>Any throttle/steer beyond the deadzone, or any button held.</summary>
        public static bool IsActive(in PilotCommand command, float axisDeadzone)
        {
            return Math.Abs(command.Throttle) > axisDeadzone
                || Math.Abs(command.Steer) > axisDeadzone
                || command.Handbrake
                || command.Boost
                || command.Reset;
        }

        /// <summary>
        /// Firing, or the aim moved by more than <paramref name="aimDeltaDegrees"/> since <paramref name="previous"/>.
        /// Aim movement needs two aimed commands: if either has HasAim == false there is nothing to compare,
        /// so it does not count as movement (the first aim after entering the seat is not "activity").
        /// </summary>
        public static bool IsActive(in GunnerCommand command, in GunnerCommand previous, float aimDeltaDegrees)
        {
            if (command.Fire) return true;
            if (!command.HasAim || !previous.HasAim) return false;

            float yawMoved = Math.Abs(ShortestYawDelta(previous.AimYawDegrees, command.AimYawDegrees));
            float pitchMoved = Math.Abs(command.AimPitchDegrees - previous.AimPitchDegrees);
            return yawMoved > aimDeltaDegrees || pitchMoved > aimDeltaDegrees;
        }

        /// <summary>Repairing, or asking for a power focus that differs from the previous one.</summary>
        public static bool IsActive(in EngineerCommand command, in EngineerCommand previous)
        {
            if (command.Repair) return true;
            // Engineer commands are target states re-sent every frame, so only a CHANGE of focus is activity.
            return command.Focus != PowerFocus.Keep && command.Focus != previous.Focus;
        }

        // Signed shortest angular difference in degrees, in [-180, 180]. Yaw wraps at 360, so 359 -> 1 is +2, not -358.
        private static float ShortestYawDelta(float from, float to)
        {
            float d = (to - from) % 360f;
            if (d > 180f) d -= 360f;
            else if (d < -180f) d += 360f;
            return d;
        }
    }
}
