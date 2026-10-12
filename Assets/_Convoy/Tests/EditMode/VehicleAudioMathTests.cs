using Convoy.Presentation;
using NUnit.Framework;

namespace Convoy.Tests
{
    public class VehicleAudioMathTests
    {
        [Test]
        public void Rpm_StartsAtIdleAndReachesRedline()
        {
            Assert.That(VehicleAudioMath.EngineRpm01(0f, 5), Is.EqualTo(0.15f).Within(1e-4f));
            Assert.That(VehicleAudioMath.EngineRpm01(1f, 5), Is.EqualTo(1f));
        }

        [Test]
        public void Rpm_DropsWhenTheNextGearEngages()
        {
            float endOfFirst = VehicleAudioMath.EngineRpm01(0.199f, 5);
            float startOfSecond = VehicleAudioMath.EngineRpm01(0.201f, 5);
            Assert.That(endOfFirst, Is.GreaterThan(0.95f));
            Assert.That(startOfSecond, Is.LessThan(0.5f));
        }

        [Test]
        public void Pitch_RisesWithRpmAndThrottle()
        {
            Assert.That(VehicleAudioMath.EnginePitch(1f, 0f), Is.GreaterThan(VehicleAudioMath.EnginePitch(0f, 0f)));
            Assert.That(VehicleAudioMath.EnginePitch(0.5f, 1f), Is.GreaterThan(VehicleAudioMath.EnginePitch(0.5f, 0f)));
        }

        [Test]
        public void Volume_LouderOnThrottleOrBrake()
        {
            Assert.That(VehicleAudioMath.EngineVolume(1f), Is.GreaterThan(VehicleAudioMath.EngineVolume(0f)));
            Assert.That(VehicleAudioMath.EngineVolume(-1f), Is.EqualTo(VehicleAudioMath.EngineVolume(1f)));
        }

        [Test]
        public void Skid_SilentWhenAirborneOrSlow()
        {
            Assert.That(VehicleAudioMath.SkidIntensity(30f, 20f, 0f, true, 0), Is.EqualTo(0f));
            Assert.That(VehicleAudioMath.SkidIntensity(30f, 1f, -1f, true, 4), Is.EqualTo(0f));
        }

        [Test]
        public void Skid_FromDriftBrakeAndHandbrake()
        {
            Assert.That(VehicleAudioMath.SkidIntensity(2f, 20f, 1f, false, 4), Is.EqualTo(0f), "straight line, no squeal");
            Assert.That(VehicleAudioMath.SkidIntensity(26f, 20f, 1f, false, 4), Is.EqualTo(1f).Within(1e-4f), "big drift");
            Assert.That(VehicleAudioMath.SkidIntensity(0f, 20f, -1f, false, 4), Is.GreaterThan(0.3f), "hard braking");
            Assert.That(VehicleAudioMath.SkidIntensity(0f, 20f, 0f, true, 4), Is.EqualTo(0.6f).Within(1e-4f), "handbrake");
        }

        [Test]
        public void Approach_MovesTowardsTargetWithoutOvershoot()
        {
            Assert.That(VehicleAudioMath.Approach(0f, 1f, 3f, 0.1f), Is.EqualTo(0.3f).Within(1e-5f));
            Assert.That(VehicleAudioMath.Approach(0.9f, 1f, 3f, 0.1f), Is.EqualTo(1f));
            Assert.That(VehicleAudioMath.Approach(1f, 0f, 3f, 0.1f), Is.EqualTo(0.7f).Within(1e-5f));
        }
    }
}
