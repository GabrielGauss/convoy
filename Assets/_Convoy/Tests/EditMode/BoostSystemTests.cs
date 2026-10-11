using Convoy.Core;
using Convoy.Vehicle;
using NUnit.Framework;
using UnityEngine;

namespace Convoy.Tests
{
    public class BoostSystemTests
    {
        private const float Dt = 0.02f;

        private VehicleConfig _config;
        private BoostSystem _boost;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<VehicleConfig>();
            _boost = new BoostSystem(_config);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
        }

        private void Run(float seconds, bool requested)
        {
            int steps = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < steps; i++) _boost.Step(Dt, requested);
        }

        [Test]
        public void StartsFull()
        {
            Assert.That(_boost.Fraction, Is.EqualTo(1f));
            Assert.That(_boost.Active, Is.False);
        }

        [Test]
        public void Boosting_DrainsTank()
        {
            Run(1f, true);
            Assert.That(_boost.Active, Is.True);
            Assert.That(_boost.Fraction, Is.EqualTo(1f - _config.boostDrainPerSecond).Within(0.02f));
        }

        [Test]
        public void EmptyTank_StopsBoost()
        {
            Run(1f / _config.boostDrainPerSecond + 0.5f, true);
            Assert.That(_boost.Fraction, Is.EqualTo(0f));
            Assert.That(_boost.Active, Is.False);
        }

        [Test]
        public void EmptyTank_NeedsMinimumBeforeRestart()
        {
            Run(1f / _config.boostDrainPerSecond + 0.5f, true);   // empty
            Run(_config.boostRegenDelay + 0.2f, false);           // a little refill, below the minimum
            Assert.That(_boost.Fraction, Is.LessThan(_config.boostMinToStart));
            Assert.That(_boost.Step(Dt, true), Is.False);
        }

        [Test]
        public void Regen_WaitsForDelay()
        {
            Run(1f, true);
            float afterBoost = _boost.Fraction;
            Run(_config.boostRegenDelay * 0.5f, false);
            Assert.That(_boost.Fraction, Is.EqualTo(afterBoost).Within(1e-5f));
        }

        [Test]
        public void Regen_RefillsToFullAndStops()
        {
            Run(1f, true);
            Run(_config.boostRegenDelay + 1f / _config.boostRegenPerSecond + 1f, false);
            Assert.That(_boost.Fraction, Is.EqualTo(1f));
        }

        [Test]
        public void PilotCommand_ClampedKeepsBoost()
        {
            var cmd = new PilotCommand { Throttle = 5f, Boost = true }.Clamped();
            Assert.That(cmd.Boost, Is.True);
            Assert.That(cmd.Throttle, Is.EqualTo(1f));
        }
    }
}
