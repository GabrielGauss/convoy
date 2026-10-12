using NUnit.Framework;

namespace Convoy.Input.Tests
{
    public class GunnerAimMathTests
    {
        private const float MinPitch = -30f;
        private const float MaxPitch = 60f;

        [Test]
        public void Integrate_AddsDeltas()
        {
            var aim = new GunnerAim { YawDegrees = 10f, PitchDegrees = 5f };
            var r = GunnerAimMath.Integrate(aim, 15f, 10f, MinPitch, MaxPitch);
            Assert.AreEqual(25f, r.YawDegrees, 1e-4f);
            Assert.AreEqual(15f, r.PitchDegrees, 1e-4f);
        }

        [Test]
        public void Integrate_WrapsYawPastNorth()
        {
            var aim = new GunnerAim { YawDegrees = 350f };
            var r = GunnerAimMath.Integrate(aim, 20f, 0f, MinPitch, MaxPitch);
            Assert.AreEqual(10f, r.YawDegrees, 1e-3f);
        }

        [Test]
        public void Integrate_WrapsNegativeYaw()
        {
            var aim = new GunnerAim { YawDegrees = 5f };
            var r = GunnerAimMath.Integrate(aim, -15f, 0f, MinPitch, MaxPitch);
            Assert.AreEqual(350f, r.YawDegrees, 1e-3f);
        }

        [Test]
        public void Integrate_YawAlwaysInZeroTo360()
        {
            var aim = new GunnerAim { YawDegrees = 0f };
            foreach (float delta in new[] { -720f, -360f, -1e-9f, 360f, 1000f })
            {
                var r = GunnerAimMath.Integrate(aim, delta, 0f, MinPitch, MaxPitch);
                Assert.GreaterOrEqual(r.YawDegrees, 0f, $"delta {delta}");
                Assert.Less(r.YawDegrees, 360f, $"delta {delta}");
            }
        }

        [Test]
        public void Integrate_ClampsPitchToLimits()
        {
            var aim = new GunnerAim { PitchDegrees = 50f };
            Assert.AreEqual(MaxPitch, GunnerAimMath.Integrate(aim, 0f, 100f, MinPitch, MaxPitch).PitchDegrees, 1e-5f);
            Assert.AreEqual(MinPitch, GunnerAimMath.Integrate(aim, 0f, -200f, MinPitch, MaxPitch).PitchDegrees, 1e-5f);
        }

        [Test]
        public void Integrate_DoesNotMutateInput()
        {
            var aim = new GunnerAim { YawDegrees = 1f, PitchDegrees = 2f };
            GunnerAimMath.Integrate(aim, 10f, 10f, MinPitch, MaxPitch);
            Assert.AreEqual(1f, aim.YawDegrees);
            Assert.AreEqual(2f, aim.PitchDegrees);
        }

        [Test]
        public void FromHeading_SetsYawAndLevelPitch()
        {
            var r = GunnerAimMath.FromHeading(135f);
            Assert.AreEqual(135f, r.YawDegrees, 1e-4f);
            Assert.AreEqual(0f, r.PitchDegrees);
        }

        [TestCase(-90f, 270f)]
        [TestCase(360f, 0f)]
        [TestCase(450f, 90f)]
        public void FromHeading_WrapsYaw(float heading, float expected)
        {
            Assert.AreEqual(expected, GunnerAimMath.FromHeading(heading).YawDegrees, 1e-3f);
        }
    }
}
