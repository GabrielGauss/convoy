using System;

namespace Convoy.Presentation
{
    /// <summary>
    /// Pure maths for vehicle audio (AUD-01): fake gearbox RPM, engine pitch/volume, tyre squeal.
    /// No Unity calls, so it is unit-tested. The MonoBehaviour <see cref="VehicleAudio"/> only applies the numbers.
    /// </summary>
    public static class VehicleAudioMath
    {
        /// <summary>
        /// Engine "RPM" 0..1 from speed as a fraction of top speed, with a fake gearbox: RPM climbs
        /// through each gear and drops when the next gear engages, which is what makes a car sound fast.
        /// </summary>
        public static float EngineRpm01(float speedFraction, int gears, float idle = 0.15f)
        {
            if (gears < 1) gears = 1;
            float sf = Math.Clamp(speedFraction, 0f, 1f);
            if (sf >= 1f) return 1f;
            float scaled = sf * gears;
            int gear = (int)scaled;                       // 0-based gear
            float t = scaled - gear;                      // progress through this gear
            float floor = gear == 0 ? idle : 0.45f;       // higher gears re-engage at mid revs
            return floor + (1f - floor) * t;
        }

        /// <summary>AudioSource.pitch for the engine loop: low at idle, high near redline, a little extra under load.</summary>
        public static float EnginePitch(float rpm01, float throttle, float minPitch = 0.6f, float maxPitch = 2.1f)
        {
            float rpm = Math.Clamp(rpm01, 0f, 1f);
            return minPitch + (maxPitch - minPitch) * rpm + 0.08f * Math.Max(0f, throttle);
        }

        /// <summary>Engine loop volume: quieter coasting, louder on the throttle.</summary>
        public static float EngineVolume(float throttle, float coast = 0.35f, float full = 0.8f)
        {
            return coast + (full - coast) * Math.Clamp(Math.Abs(throttle), 0f, 1f);
        }

        /// <summary>
        /// Tyre squeal 0..1. Sliding sideways (slip angle) and hard braking both squeal; nothing in the air
        /// or at walking pace.
        /// </summary>
        public static float SkidIntensity(float slipAngleDeg, float forwardSpeed, float throttle, bool handbrake,
            int groundedWheels)
        {
            float speed = Math.Abs(forwardSpeed);
            if (groundedWheels == 0 || speed < 3f) return 0f;

            float slip = Math.Clamp((Math.Abs(slipAngleDeg) - 6f) / 20f, 0f, 1f);
            bool braking = throttle < -0.1f && forwardSpeed > 2f;
            float brake = braking ? 0.35f + 0.35f * Math.Clamp(speed / 30f, 0f, 1f) : 0f;
            float hand = handbrake && speed > 5f ? 0.6f : 0f;
            return Math.Max(slip, Math.Max(brake, hand));
        }

        /// <summary>Frame-rate independent move towards a target (for fading volumes).</summary>
        public static float Approach(float current, float target, float ratePerSecond, float deltaTime)
        {
            float step = ratePerSecond * Math.Max(0f, deltaTime);
            if (current < target) return Math.Min(target, current + step);
            return Math.Max(target, current - step);
        }
    }
}
