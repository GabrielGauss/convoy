using Convoy.Vehicle;
using NUnit.Framework;
using UnityEngine;

namespace Convoy.Tests
{
    public class HandlingModifiersTests
    {
        private DamageConfig _config;

        [SetUp]
        public void SetUp() => _config = ScriptableObject.CreateInstance<DamageConfig>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_config);

        private static void AssertAllExactlyOne(HandlingModifiers m)
        {
            // Exact comparison on purpose: bit-identical handling at full health (D-018).
            Assert.AreEqual(1f, m.TopSpeed);
            Assert.AreEqual(1f, m.EngineForce);
            Assert.AreEqual(1f, m.Grip);
            Assert.AreEqual(1f, m.Steer);
        }

        [Test]
        public void None_IsAllExactlyOne() => AssertAllExactlyOne(HandlingModifiers.None);

        [TestCase(1f)]
        [TestCase(0.9f)]
        [TestCase(0.75f)]
        [TestCase(0.6f)]
        public void FromHealth_AtOrAboveThreshold_IsExactlyOne(float h)
        {
            AssertAllExactlyOne(HandlingModifiers.FromHealth(h, _config));
        }

        [Test]
        public void FromHealth_AboveOne_IsClampedToOne()
        {
            AssertAllExactlyOne(HandlingModifiers.FromHealth(5f, _config));
        }

        [Test]
        public void FromHealth_NaN_IsTreatedAsFullHealth()
        {
            AssertAllExactlyOne(HandlingModifiers.FromHealth(float.NaN, _config));
        }

        [Test]
        public void FromHealth_JustBelowThreshold_IsBelowOne()
        {
            HandlingModifiers m = HandlingModifiers.FromHealth(0.59f, _config);
            Assert.Less(m.TopSpeed, 1f);
            Assert.Less(m.EngineForce, 1f);
            Assert.Less(m.Grip, 1f);
            Assert.Less(m.Steer, 1f);
        }

        [Test]
        public void FromHealth_AtZero_IsConfiguredMinimums()
        {
            HandlingModifiers m = HandlingModifiers.FromHealth(0f, _config);
            Assert.AreEqual(_config.minTopSpeedMultiplier, m.TopSpeed, 1e-6f);
            Assert.AreEqual(_config.minEngineForceMultiplier, m.EngineForce, 1e-6f);
            Assert.AreEqual(_config.minGripMultiplier, m.Grip, 1e-6f);
            Assert.AreEqual(_config.minSteerMultiplier, m.Steer, 1e-6f);
        }

        [Test]
        public void FromHealth_NegativeHealth_ClampsToMinimums()
        {
            HandlingModifiers m = HandlingModifiers.FromHealth(-3f, _config);
            Assert.AreEqual(_config.minTopSpeedMultiplier, m.TopSpeed, 1e-6f);
        }

        [Test]
        public void FromHealth_Midpoint_IsLinear()
        {
            // h = 0.3 is half of the 0.6 threshold: halfway between min and 1.
            HandlingModifiers m = HandlingModifiers.FromHealth(0.3f, _config);
            Assert.AreEqual((_config.minTopSpeedMultiplier + 1f) * 0.5f, m.TopSpeed, 1e-5f);
            Assert.AreEqual((_config.minEngineForceMultiplier + 1f) * 0.5f, m.EngineForce, 1e-5f);
            Assert.AreEqual((_config.minGripMultiplier + 1f) * 0.5f, m.Grip, 1e-5f);
            Assert.AreEqual((_config.minSteerMultiplier + 1f) * 0.5f, m.Steer, 1e-5f);
        }

        [Test]
        public void FromHealth_IsMonotonicNonDecreasingWithHealth()
        {
            float prev = HandlingModifiers.FromHealth(0f, _config).TopSpeed;
            for (int i = 1; i <= 100; i++)
            {
                float cur = HandlingModifiers.FromHealth(i / 100f, _config).TopSpeed;
                Assert.GreaterOrEqual(cur, prev);
                prev = cur;
            }
        }

        [Test]
        public void FromHealth_ZeroThreshold_NeverDegrades()
        {
            _config.degradeBelowHealth01 = 0f;
            AssertAllExactlyOne(HandlingModifiers.FromHealth(0f, _config));
        }
    }
}
