using Convoy.Core;
using Convoy.Vehicle;
using NUnit.Framework;

namespace Convoy.Tests
{
    public class VehicleMathTests
    {
        private const float Eps = 1e-4f;

        // --- Suspension -------------------------------------------------

        [Test]
        public void Compression_IsZero_WhenWheelHangsFree()
        {
            Assert.That(VehicleMath.Compression(0.45f, 0.45f + 0.38f, 0.38f), Is.EqualTo(0f).Within(Eps));
        }

        [Test]
        public void Compression_GrowsAsGroundGetsCloser()
        {
            Assert.That(VehicleMath.Compression(0.45f, 0.38f + 0.30f, 0.38f), Is.EqualTo(0.15f).Within(Eps));
        }

        [Test]
        public void Compression_IsClampedToRestLength()
        {
            Assert.That(VehicleMath.Compression(0.45f, 0.1f, 0.38f), Is.EqualTo(0.45f).Within(Eps));
        }

        [Test]
        public void SuspensionForce_IsSpringPlusDamper()
        {
            Assert.That(VehicleMath.SuspensionForce(0.1f, 0.5f, 35000f, 3500f), Is.EqualTo(3500f + 1750f).Within(0.01f));
        }

        [Test]
        public void SuspensionForce_NeverPullsDown()
        {
            Assert.That(VehicleMath.SuspensionForce(0.01f, -5f, 35000f, 3500f), Is.EqualTo(0f));
        }

        [Test]
        public void DefaultConfig_StaticLoad_SitsInsideSuspensionTravel()
        {
            // 1200 kg on 4 springs at 35 kN/m should compress ~8 cm: well inside 45 cm travel.
            float perWheelLoad = 1200f * 9.81f / 4f;
            float compression = perWheelLoad / 35000f;
            Assert.That(compression, Is.GreaterThan(0.03f).And.LessThan(0.45f * 0.5f));
        }

        // --- Throttle / brake / reverse ---------------------------------

        [Test]
        public void Throttle_FromRest_Drives()
        {
            VehicleMath.ResolveLongitudinal(1f, 0f, 0.6f, out float drive, out float brake);
            Assert.That(drive, Is.EqualTo(1f));
            Assert.That(brake, Is.EqualTo(0f));
        }

        [Test]
        public void Reverse_WhileMovingForward_Brakes()
        {
            VehicleMath.ResolveLongitudinal(-1f, 10f, 0.6f, out float drive, out float brake);
            Assert.That(drive, Is.EqualTo(0f));
            Assert.That(brake, Is.EqualTo(1f));
        }

        [Test]
        public void Reverse_WhenStopped_DrivesBackwards()
        {
            VehicleMath.ResolveLongitudinal(-0.5f, 0.2f, 0.6f, out float drive, out float brake);
            Assert.That(drive, Is.EqualTo(-0.5f));
            Assert.That(brake, Is.EqualTo(0f));
        }

        [Test]
        public void Throttle_WhileRollingBackwards_Brakes()
        {
            VehicleMath.ResolveLongitudinal(0.8f, -5f, 0.6f, out float drive, out float brake);
            Assert.That(drive, Is.EqualTo(0f));
            Assert.That(brake, Is.EqualTo(0.8f));
        }

        [Test]
        public void DriveFactor_IsFullAtRest_AndZeroAtTopSpeed()
        {
            Assert.That(VehicleMath.DriveFactor(0f, 40f), Is.EqualTo(1f).Within(Eps));
            Assert.That(VehicleMath.DriveFactor(40f, 40f), Is.EqualTo(0f).Within(Eps));
            Assert.That(VehicleMath.DriveFactor(80f, 40f), Is.EqualTo(0f).Within(Eps));
        }

        [Test]
        public void BrakeForce_OpposesMotion()
        {
            Assert.That(VehicleMath.BrakeForce(10f, 1f, 3000f, 300f, 0.02f), Is.EqualTo(-3000f).Within(Eps));
            Assert.That(VehicleMath.BrakeForce(-10f, 1f, 3000f, 300f, 0.02f), Is.EqualTo(3000f).Within(Eps));
        }

        [Test]
        public void BrakeForce_NeverOvershootsIntoReverse()
        {
            // 0.05 m/s * 300 kg / 0.02 s = 750 N is enough to stop; must not apply the full 3000 N.
            Assert.That(VehicleMath.BrakeForce(0.05f, 1f, 3000f, 300f, 0.02f), Is.EqualTo(-750f).Within(0.01f));
            Assert.That(VehicleMath.BrakeForce(0f, 1f, 3000f, 300f, 0.02f), Is.EqualTo(0f).Within(Eps));
        }

        // --- Tyres ------------------------------------------------------

        [Test]
        public void LateralGrip_OpposesSideways_Sliding()
        {
            Assert.That(VehicleMath.LateralGripForce(2f, 0.5f, 300f, 0.02f), Is.EqualTo(-15000f).Within(0.01f));
            Assert.That(VehicleMath.LateralGripForce(-2f, 0.5f, 300f, 0.02f), Is.EqualTo(15000f).Within(0.01f));
        }

        [Test]
        public void FrictionCircle_LeavesSmallForcesAlone()
        {
            float fwd = 300f, lat = 400f;
            bool sliding = VehicleMath.ClampToFrictionCircle(ref fwd, ref lat, 1000f);
            Assert.That(sliding, Is.False);
            Assert.That(fwd, Is.EqualTo(300f));
            Assert.That(lat, Is.EqualTo(400f));
        }

        [Test]
        public void FrictionCircle_ScalesExcessForce_KeepingDirection()
        {
            float fwd = 3000f, lat = 4000f; // magnitude 5000
            bool sliding = VehicleMath.ClampToFrictionCircle(ref fwd, ref lat, 1000f);
            Assert.That(sliding, Is.True);
            Assert.That(fwd, Is.EqualTo(600f).Within(0.01f));
            Assert.That(lat, Is.EqualTo(800f).Within(0.01f));
        }

        [Test]
        public void FrictionCircle_NoLoad_NoGrip()
        {
            float fwd = 100f, lat = 100f;
            VehicleMath.ClampToFrictionCircle(ref fwd, ref lat, 0f);
            Assert.That(fwd, Is.EqualTo(0f));
            Assert.That(lat, Is.EqualTo(0f));
        }

        // --- Steering ---------------------------------------------------

        [Test]
        public void Steering_IsFullAtRest()
        {
            Assert.That(VehicleMath.SteerAngle(1f, 32f, 0f, 40f, 0.35f), Is.EqualTo(32f).Within(Eps));
        }

        [Test]
        public void Steering_ShrinksAtTopSpeed()
        {
            Assert.That(VehicleMath.SteerAngle(-1f, 32f, 40f, 40f, 0.35f), Is.EqualTo(-32f * 0.35f).Within(Eps));
        }

        [Test]
        public void Steering_InputIsClamped()
        {
            Assert.That(VehicleMath.SteerAngle(3f, 32f, 0f, 40f, 0.35f), Is.EqualTo(32f).Within(Eps));
        }

        // --- Commands ---------------------------------------------------

        [Test]
        public void PilotCommand_Clamped_LimitsAxes()
        {
            var c = new PilotCommand { Throttle = 2f, Steer = -5f, Handbrake = true }.Clamped();
            Assert.That(c.Throttle, Is.EqualTo(1f));
            Assert.That(c.Steer, Is.EqualTo(-1f));
            Assert.That(c.Handbrake, Is.True);
        }
    }
}
