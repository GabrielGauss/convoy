using Convoy.Core;
using NUnit.Framework;

namespace Convoy.Tests
{
    public class SeatCommandsTests
    {
        [Test]
        public void DefaultCommands_AreNeutral()
        {
            GunnerCommand g = default;
            Assert.IsFalse(g.HasAim);
            Assert.IsFalse(g.Fire);
            Assert.AreEqual(0f, g.AimYawDegrees);
            Assert.AreEqual(0f, g.AimPitchDegrees);
            Assert.AreEqual(default(GunnerCommand), GunnerCommand.Neutral);

            EngineerCommand e = default;
            Assert.AreEqual(PowerFocus.Keep, e.Focus);
            Assert.IsFalse(e.Repair);
            Assert.AreEqual(default(EngineerCommand), EngineerCommand.Neutral);
        }

        [Test]
        public void PowerFocus_KeepIsZero()
        {
            Assert.AreEqual(0, (int)PowerFocus.Keep);
        }

        [Test]
        public void SeatsCount_MatchesSeatIdEnum()
        {
            Assert.AreEqual(3, Seats.Count);
            Assert.AreEqual(Seats.Count - 1, (int)SeatId.Engineer);
        }

        [TestCase(370f, 10f)]
        [TestCase(360f, 0f)]
        [TestCase(-10f, 350f)]
        [TestCase(-370f, 350f)]
        [TestCase(725f, 5f)]
        [TestCase(0f, 0f)]
        [TestCase(359.5f, 359.5f)]
        public void Clamped_WrapsYawIntoZeroTo360(float input, float expected)
        {
            var c = new GunnerCommand { AimYawDegrees = input }.Clamped();
            Assert.AreEqual(expected, c.AimYawDegrees, 1e-3f);
            Assert.GreaterOrEqual(c.AimYawDegrees, 0f);
            Assert.Less(c.AimYawDegrees, 360f);
        }

        [Test]
        public void Clamped_TinyNegativeYaw_StaysBelow360()
        {
            // -1e-9 + 360 rounds to exactly 360 in float; the result must still be inside [0, 360).
            var c = new GunnerCommand { AimYawDegrees = -1e-9f }.Clamped();
            Assert.Less(c.AimYawDegrees, 360f);
            Assert.GreaterOrEqual(c.AimYawDegrees, 0f);
        }

        [TestCase(120f, 89f)]
        [TestCase(-120f, -89f)]
        [TestCase(45f, 45f)]
        [TestCase(89f, 89f)]
        public void Clamped_ClampsPitch(float input, float expected)
        {
            var c = new GunnerCommand { AimPitchDegrees = input }.Clamped();
            Assert.AreEqual(expected, c.AimPitchDegrees, 1e-5f);
        }

        [Test]
        public void Clamped_KeepsHasAimAndFire()
        {
            var c = new GunnerCommand { HasAim = true, Fire = true, AimYawDegrees = 400f, AimPitchDegrees = 5f }.Clamped();
            Assert.IsTrue(c.HasAim);
            Assert.IsTrue(c.Fire);
        }

        [Test]
        public void SeatActivity_DefaultIsZero()
        {
            SeatActivity a = default;
            Assert.AreEqual(0, a.OccupiedSteps);
            Assert.AreEqual(0, a.ActiveSteps);
        }
    }
}
