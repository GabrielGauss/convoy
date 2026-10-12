using System;
using Convoy.Race;
using NUnit.Framework;
using UnityEngine;

namespace Convoy.Tests
{
    public class UplinkHoldTests
    {
        private const float Dt = 0.02f;
        private UplinkConfig _cfg;

        [SetUp]
        public void SetUp() => _cfg = ScriptableObject.CreateInstance<UplinkConfig>();

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(_cfg);

        [Test]
        public void CompletesOnExactly200thStep()
        {
            var hold = new UplinkHold(_cfg); // 4 s default
            for (int i = 1; i <= 199; i++)
                Assert.That(hold.Tick(Dt, true, 0f), Is.False, "step " + i);
            Assert.That(hold.Complete, Is.False);
            Assert.That(hold.Tick(Dt, true, 0f), Is.True, "step 200");
            Assert.That(hold.Complete, Is.True);
            Assert.That(hold.HeldSeconds, Is.EqualTo(4f));
            Assert.That(hold.Progress01, Is.EqualTo(1f));
        }

        [Test]
        public void CompleteIsStickyAndTickReturnsFalseAfterwards()
        {
            var hold = new UplinkHold(_cfg);
            for (int i = 0; i < 200; i++) hold.Tick(Dt, true, 0f);
            Assert.That(hold.Tick(Dt, true, 0f), Is.False);
            Assert.That(hold.Tick(Dt, false, 0f), Is.False);
            Assert.That(hold.Complete, Is.True);
            Assert.That(hold.HeldSeconds, Is.EqualTo(4f));
        }

        [Test]
        public void ResetModeDropsToZeroOnLeaving()
        {
            var hold = new UplinkHold(_cfg);
            for (int i = 0; i < 150; i++) hold.Tick(Dt, true, 0f);
            Assert.That(hold.HeldSeconds, Is.GreaterThan(2.9f));
            hold.Tick(Dt, false, 0f);
            Assert.That(hold.HeldSeconds, Is.EqualTo(0f));
            Assert.That(hold.Inside, Is.False);
        }

        [Test]
        public void DecayModeDrainsAtConfiguredRate()
        {
            _cfg.exitBehaviour = UplinkExitBehaviour.Decay;
            _cfg.decayPerSecond = 2f;
            var hold = new UplinkHold(_cfg);
            for (int i = 0; i < 150; i++) hold.Tick(Dt, true, 0f);
            for (int i = 0; i < 25; i++) hold.Tick(Dt, false, 0f);
            Assert.That(hold.HeldSeconds, Is.EqualTo(2f).Within(0.001f));
        }

        [Test]
        public void DecayNeverGoesNegative()
        {
            _cfg.exitBehaviour = UplinkExitBehaviour.Decay;
            var hold = new UplinkHold(_cfg);
            hold.Tick(Dt, true, 0f);
            for (int i = 0; i < 100; i++) hold.Tick(Dt, false, 0f);
            Assert.That(hold.HeldSeconds, Is.EqualTo(0f));
        }

        [Test]
        public void SpeedLimitStopsHoldAdvancing()
        {
            _cfg.maxHoldSpeedKmh = 30f;
            var hold = new UplinkHold(_cfg);
            hold.Tick(Dt, true, 10f); // 36 km/h > 30
            Assert.That(hold.HeldSeconds, Is.EqualTo(0f));
            Assert.That(hold.Inside, Is.False);
            hold.Tick(Dt, true, 8f);  // 28.8 km/h
            Assert.That(hold.HeldSeconds, Is.EqualTo(Dt).Within(1e-6f));
            Assert.That(hold.Inside, Is.True);
        }

        [Test]
        public void SpeedLimitZeroMeansUnlimited()
        {
            var hold = new UplinkHold(_cfg);
            hold.Tick(Dt, true, 200f);
            Assert.That(hold.HeldSeconds, Is.EqualTo(Dt).Within(1e-6f));
        }

        [Test]
        public void NegativeAndNaNDeltaTimeAdvanceNothing()
        {
            var hold = new UplinkHold(_cfg);
            hold.Tick(Dt, true, 0f);
            hold.Tick(-1f, true, 0f);
            hold.Tick(float.NaN, true, 0f);
            Assert.That(hold.HeldSeconds, Is.EqualTo(Dt).Within(1e-6f));
            Assert.That(float.IsNaN(hold.HeldSeconds), Is.False);
        }

        [Test]
        public void NullConfigThrows()
        {
            Assert.Throws<ArgumentNullException>(() => new UplinkHold(null));
        }

        [Test]
        public void ResetClearsEverythingIncludingComplete()
        {
            var hold = new UplinkHold(_cfg);
            for (int i = 0; i < 200; i++) hold.Tick(Dt, true, 0f);
            hold.Reset();
            Assert.That(hold.Complete, Is.False);
            Assert.That(hold.HeldSeconds, Is.EqualTo(0f));
            Assert.That(hold.Progress01, Is.EqualTo(0f));
            Assert.That(hold.Tick(Dt, true, 0f), Is.False);
            Assert.That(hold.HeldSeconds, Is.EqualTo(Dt).Within(1e-6f));
        }

        [Test]
        public void Progress01TracksHeldFraction()
        {
            var hold = new UplinkHold(_cfg);
            for (int i = 0; i < 100; i++) hold.Tick(Dt, true, 0f);
            Assert.That(hold.Progress01, Is.EqualTo(0.5f).Within(0.001f));
        }

        [Test]
        public void TickDoesNotAllocate()
        {
            var hold = new UplinkHold(_cfg);
            Assert.That(() =>
            {
                hold.Tick(Dt, true, 3f);
                hold.Tick(Dt, false, 3f);
            }, Is.Not.AllocatingGCMemory());
        }
    }
}
