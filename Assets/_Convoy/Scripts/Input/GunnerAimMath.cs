using UnityEngine;

namespace Convoy.Input
{
    /// <summary>World-space turret aim in degrees (yaw clockwise from world +Z, pitch above the horizon).</summary>
    public struct GunnerAim
    {
        public float YawDegrees;
        public float PitchDegrees;
    }

    /// <summary>Pure aim accumulation for the Gunner seat. The caller converts mouse pixels or stick * dt into degrees first.</summary>
    public static class GunnerAimMath
    {
        /// <summary>Adds the deltas; wraps yaw into [0, 360) and clamps pitch to [minPitch, maxPitch].</summary>
        public static GunnerAim Integrate(GunnerAim aim, float deltaYaw, float deltaPitch, float minPitch, float maxPitch)
        {
            return new GunnerAim
            {
                YawDegrees = WrapYaw(aim.YawDegrees + deltaYaw),
                PitchDegrees = Mathf.Clamp(aim.PitchDegrees + deltaPitch, minPitch, maxPitch),
            };
        }

        /// <summary>Starting aim when a player sits down: looking along the vehicle's heading, level with the horizon.</summary>
        public static GunnerAim FromHeading(float vehicleYawDegrees)
        {
            return new GunnerAim { YawDegrees = WrapYaw(vehicleYawDegrees), PitchDegrees = 0f };
        }

        private static float WrapYaw(float yaw)
        {
            // Mathf.Repeat can return exactly 360 through float rounding for tiny negative inputs, hence the extra check.
            float wrapped = Mathf.Repeat(yaw, 360f);
            return wrapped >= 360f ? 0f : wrapped;
        }
    }
}
