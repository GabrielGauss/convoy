using System;

namespace Convoy.Vehicle
{
    /// <summary>
    /// Pure, engine-free math for the arcade vehicle model (D-004).
    /// Every function here is deterministic and covered by EditMode tests,
    /// so tuning bugs can be found without entering Play mode.
    /// </summary>
    public static class VehicleMath
    {
        /// <summary>How far the spring is compressed, in metres, clamped to [0, restLength].</summary>
        /// <param name="restLength">Suspension travel when fully extended.</param>
        /// <param name="hitDistance">Raycast distance from wheel anchor to ground.</param>
        /// <param name="wheelRadius">Wheel radius.</param>
        public static float Compression(float restLength, float hitDistance, float wheelRadius)
        {
            float springLength = hitDistance - wheelRadius;
            return Math.Clamp(restLength - springLength, 0f, restLength);
        }

        /// <summary>Spring + damper force along the suspension axis. Never pulls the car down (≥ 0).</summary>
        /// <param name="compressionVelocity">Positive when compressing.</param>
        public static float SuspensionForce(float compression, float compressionVelocity, float stiffness, float damping)
        {
            return Math.Max(0f, compression * stiffness + compressionVelocity * damping);
        }

        /// <summary>
        /// Splits a signed throttle input into drive and brake, given the current forward speed.
        /// Pressing against the direction of travel brakes; once (almost) stopped it drives the other way.
        /// </summary>
        /// <param name="throttle">-1..1 from the pilot.</param>
        /// <param name="forwardSpeed">Signed speed along the vehicle's forward axis, m/s.</param>
        /// <param name="reverseThreshold">Speed below which braking turns into driving in the opposite direction.</param>
        /// <param name="drive">-1..1 drive request (negative = reverse).</param>
        /// <param name="brake">0..1 brake request.</param>
        public static void ResolveLongitudinal(float throttle, float forwardSpeed, float reverseThreshold,
            out float drive, out float brake)
        {
            drive = 0f;
            brake = 0f;
            if (throttle > 0f)
            {
                if (forwardSpeed < -reverseThreshold) brake = throttle;
                else drive = throttle;
            }
            else if (throttle < 0f)
            {
                if (forwardSpeed > reverseThreshold) brake = -throttle;
                else drive = throttle;
            }
        }

        /// <summary>Engine force multiplier that fades to 0 as speed approaches the top speed (soft cap).</summary>
        public static float DriveFactor(float speed, float topSpeed)
        {
            if (topSpeed <= 0f) return 0f;
            float ratio = Math.Clamp(Math.Abs(speed) / topSpeed, 0f, 1f);
            return 1f - ratio * ratio;
        }

        /// <summary>Brake force for one wheel, limited so it can stop the wheel's share of mass but never push it backwards.</summary>
        /// <param name="forwardSpeed">Signed forward speed at the wheel, m/s.</param>
        /// <returns>Signed force along the wheel's forward axis.</returns>
        public static float BrakeForce(float forwardSpeed, float brakeInput, float maxBrakeForce, float massPerWheel, float deltaTime)
        {
            if (brakeInput <= 0f || deltaTime <= 0f) return 0f;
            float stopForce = Math.Abs(forwardSpeed) * massPerWheel / deltaTime;
            float force = Math.Min(brakeInput * maxBrakeForce, stopForce);
            return -Math.Sign(forwardSpeed) * force;
        }

        /// <summary>Lateral force that cancels a fraction of sideways sliding this step (before friction limits).</summary>
        /// <param name="lateralSpeed">Signed sideways speed at the wheel, m/s.</param>
        /// <param name="grip">0..1 fraction of sideways velocity removed per step.</param>
        public static float LateralGripForce(float lateralSpeed, float grip, float massPerWheel, float deltaTime)
        {
            if (deltaTime <= 0f) return 0f;
            return -lateralSpeed * Math.Clamp(grip, 0f, 1f) * massPerWheel / deltaTime;
        }

        /// <summary>
        /// Friction circle: scales the combined tyre force down to the maximum the tyre can transmit
        /// (mu * normal force). This is what makes the car slide when it is pushed too hard.
        /// </summary>
        /// <returns>True when the tyre is saturated (sliding).</returns>
        public static bool ClampToFrictionCircle(ref float forwardForce, ref float lateralForce, float maxForce)
        {
            if (maxForce <= 0f)
            {
                forwardForce = 0f;
                lateralForce = 0f;
                return true;
            }

            float magnitude = (float)Math.Sqrt(forwardForce * forwardForce + lateralForce * lateralForce);
            if (magnitude <= maxForce) return false;

            float scale = maxForce / magnitude;
            forwardForce *= scale;
            lateralForce *= scale;
            return true;
        }

        /// <summary>Steering angle in degrees, reduced at high speed for stability.</summary>
        /// <param name="highSpeedFactor">Fraction of max steering left at top speed (0..1).</param>
        public static float SteerAngle(float steerInput, float maxAngle, float speed, float topSpeed, float highSpeedFactor)
        {
            float ratio = topSpeed > 0f ? Math.Clamp(Math.Abs(speed) / topSpeed, 0f, 1f) : 0f;
            float factor = 1f + (Math.Clamp(highSpeedFactor, 0f, 1f) - 1f) * ratio;
            return Math.Clamp(steerInput, -1f, 1f) * maxAngle * factor;
        }
    }
}
