using Convoy.Core;
using NUnit.Framework;

namespace Convoy.Crew.Tests
{
    public class SeatActivityRulesTests
    {
        private const float Dz = 0.05f;
        private const float AimThreshold = 0.5f;

        // ---- Pilot ----

        [Test]
        public void Pilot_Neutral_IsInactive()
        {
            Assert.IsFalse(SeatActivityRules.IsActive(PilotCommand.Neutral, Dz));
        }

        [Test]
        public void Pilot_AxesWithinDeadzone_AreInactive()
        {
            var c = new PilotCommand { Throttle = 0.05f, Steer = -0.04f };
            Assert.IsFalse(SeatActivityRules.IsActive(c, Dz));
        }

        [Test]
        public void Pilot_ThrottleOrSteerAboveDeadzone_IsActive()
        {
            Assert.IsTrue(SeatActivityRules.IsActive(new PilotCommand { Throttle = 0.2f }, Dz));
            Assert.IsTrue(SeatActivityRules.IsActive(new PilotCommand { Throttle = -0.2f }, Dz));
            Assert.IsTrue(SeatActivityRules.IsActive(new PilotCommand { Steer = 0.3f }, Dz));
            Assert.IsTrue(SeatActivityRules.IsActive(new PilotCommand { Steer = -0.3f }, Dz));
        }

        [Test]
        public void Pilot_AnyButton_IsActive()
        {
            Assert.IsTrue(SeatActivityRules.IsActive(new PilotCommand { Handbrake = true }, Dz));
            Assert.IsTrue(SeatActivityRules.IsActive(new PilotCommand { Boost = true }, Dz));
            Assert.IsTrue(SeatActivityRules.IsActive(new PilotCommand { Reset = true }, Dz));
        }

        // ---- Gunner ----

        [Test]
        public void Gunner_Neutral_IsInactive()
        {
            Assert.IsFalse(SeatActivityRules.IsActive(GunnerCommand.Neutral, GunnerCommand.Neutral, AimThreshold));
        }

        [Test]
        public void Gunner_Fire_IsActive()
        {
            var c = new GunnerCommand { Fire = true };
            Assert.IsTrue(SeatActivityRules.IsActive(c, GunnerCommand.Neutral, AimThreshold));
        }

        [Test]
        public void Gunner_AimHeldStill_IsInactive()
        {
            var prev = new GunnerCommand { HasAim = true, AimYawDegrees = 90f, AimPitchDegrees = 10f };
            var cur = prev;
            Assert.IsFalse(SeatActivityRules.IsActive(cur, prev, AimThreshold));
        }

        [Test]
        public void Gunner_YawOrPitchMovedBeyondThreshold_IsActive()
        {
            var prev = new GunnerCommand { HasAim = true, AimYawDegrees = 90f, AimPitchDegrees = 10f };
            var yawMoved = new GunnerCommand { HasAim = true, AimYawDegrees = 91f, AimPitchDegrees = 10f };
            var pitchMoved = new GunnerCommand { HasAim = true, AimYawDegrees = 90f, AimPitchDegrees = 9f };
            Assert.IsTrue(SeatActivityRules.IsActive(yawMoved, prev, AimThreshold));
            Assert.IsTrue(SeatActivityRules.IsActive(pitchMoved, prev, AimThreshold));
        }

        [Test]
        public void Gunner_MovementBelowThreshold_IsInactive()
        {
            var prev = new GunnerCommand { HasAim = true, AimYawDegrees = 90f };
            var cur = new GunnerCommand { HasAim = true, AimYawDegrees = 90.3f };
            Assert.IsFalse(SeatActivityRules.IsActive(cur, prev, AimThreshold));
        }

        [Test]
        public void Gunner_YawWrap_359To1_CountsAsTwoDegrees()
        {
            var prev = new GunnerCommand { HasAim = true, AimYawDegrees = 359f };
            var cur = new GunnerCommand { HasAim = true, AimYawDegrees = 1f };
            Assert.IsTrue(SeatActivityRules.IsActive(cur, prev, 1.5f), "2 degrees > 1.5");
            Assert.IsFalse(SeatActivityRules.IsActive(cur, prev, 2.5f), "2 degrees < 2.5, not 358");
        }

        [Test]
        public void Gunner_YawWrap_1To359_IsSymmetric()
        {
            var prev = new GunnerCommand { HasAim = true, AimYawDegrees = 1f };
            var cur = new GunnerCommand { HasAim = true, AimYawDegrees = 359f };
            Assert.IsFalse(SeatActivityRules.IsActive(cur, prev, 2.5f));
        }

        [Test]
        public void Gunner_NoAimOnEitherSide_IsNotMovement()
        {
            var aimed = new GunnerCommand { HasAim = true, AimYawDegrees = 180f };
            Assert.IsFalse(SeatActivityRules.IsActive(aimed, GunnerCommand.Neutral, AimThreshold));
            Assert.IsFalse(SeatActivityRules.IsActive(GunnerCommand.Neutral, aimed, AimThreshold));
        }

        // ---- Engineer ----

        [Test]
        public void Engineer_Neutral_IsInactive()
        {
            Assert.IsFalse(SeatActivityRules.IsActive(EngineerCommand.Neutral, EngineerCommand.Neutral));
        }

        [Test]
        public void Engineer_Repair_IsActive()
        {
            var c = new EngineerCommand { Repair = true };
            Assert.IsTrue(SeatActivityRules.IsActive(c, EngineerCommand.Neutral));
        }

        [Test]
        public void Engineer_FocusChange_IsActive()
        {
            var prev = new EngineerCommand { Focus = PowerFocus.Balanced };
            var cur = new EngineerCommand { Focus = PowerFocus.Weapons };
            Assert.IsTrue(SeatActivityRules.IsActive(cur, prev));
        }

        [Test]
        public void Engineer_FocusResentUnchanged_IsInactive()
        {
            // The router re-sends the target focus every frame; that must not look like constant activity.
            var prev = new EngineerCommand { Focus = PowerFocus.Weapons };
            var cur = new EngineerCommand { Focus = PowerFocus.Weapons };
            Assert.IsFalse(SeatActivityRules.IsActive(cur, prev));
        }

        [Test]
        public void Engineer_KeepFocus_IsInactive()
        {
            var prev = new EngineerCommand { Focus = PowerFocus.Engine };
            var cur = new EngineerCommand { Focus = PowerFocus.Keep };
            Assert.IsFalse(SeatActivityRules.IsActive(cur, prev));
        }
    }
}
