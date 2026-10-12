using NUnit.Framework;
using UnityEngine;

namespace Convoy.Level.Tests
{
    public class RammerGateRulesTests
    {
        private RammerGateConfig _config;

        [SetUp]
        public void SetUp() => _config = ScriptableObject.CreateInstance<RammerGateConfig>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_config);

        // Wall at the origin, forward = +Z.
        private static readonly Vector3 Fwd = Vector3.forward;
        // Body at z = -10: the wall centre is 10 m away along forward.
        private static readonly Vector3 BodyAtMinusZ = new Vector3(0f, 0f, 10f);
        // Body at z = +10 (the other face): the wall centre is 10 m away against forward.
        private static readonly Vector3 BodyAtPlusZ = new Vector3(0f, 0f, -10f);

        // ---------- ClosingSpeed ----------

        [Test]
        public void ClosingSpeed_HeadOn_EqualsSpeed()
        {
            float s = RammerGateRules.ClosingSpeed(new Vector3(0f, 0f, 25f), Fwd, BodyAtMinusZ);
            Assert.That(s, Is.EqualTo(25f).Within(0.001f));
        }

        [Test]
        public void ClosingSpeed_FortyFiveDegrees_UsesNormalComponent()
        {
            var v = new Vector3(25f * 0.70710678f, 0f, 25f * 0.70710678f);
            Assert.That(RammerGateRules.ClosingSpeed(v, Fwd, BodyAtMinusZ), Is.EqualTo(17.68f).Within(0.01f));
        }

        [Test]
        public void ClosingSpeed_MovingAway_IsZero()
        {
            Assert.That(RammerGateRules.ClosingSpeed(new Vector3(0f, 0f, -25f), Fwd, BodyAtMinusZ), Is.EqualTo(0f));
        }

        [Test]
        public void ClosingSpeed_FromBackFace_IsPositiveTowardWall()
        {
            // Body on the +Z side driving toward -Z, i.e. toward the wall: same result as from the other face.
            float s = RammerGateRules.ClosingSpeed(new Vector3(0f, 0f, -25f), Fwd, BodyAtPlusZ);
            Assert.That(s, Is.EqualTo(25f).Within(0.001f));
        }

        [Test]
        public void ClosingSpeed_FromBackFace_MovingAway_IsZero()
        {
            Assert.That(RammerGateRules.ClosingSpeed(new Vector3(0f, 0f, 25f), Fwd, BodyAtPlusZ), Is.EqualTo(0f));
        }

        [Test]
        public void ClosingSpeed_Sideways_IsZero()
        {
            Assert.That(RammerGateRules.ClosingSpeed(new Vector3(25f, 0f, 0f), Fwd, BodyAtMinusZ), Is.EqualTo(0f).Within(1e-5f));
        }

        // ---------- ShouldBreak ----------

        [Test]
        public void ShouldBreak_AtThreshold80Kmh_Jackal_True()
        {
            Assert.That(RammerGateRules.ShouldBreak(22.22f, 1200f, _config), Is.True);
        }

        [Test]
        public void ShouldBreak_ExactThreshold_IsInclusive()
        {
            Assert.That(RammerGateRules.ShouldBreak(80f / 3.6f, 1200f, _config), Is.True);
        }

        [Test]
        public void ShouldBreak_JustBelowThreshold_False()
        {
            Assert.That(RammerGateRules.ShouldBreak(22.2f, 1200f, _config), Is.False);
        }

        [Test]
        public void ShouldBreak_FastButLight_False()
        {
            Assert.That(RammerGateRules.ShouldBreak(30f, 40f, _config), Is.False);
        }

        [Test]
        public void ShouldBreak_MassExactlyMinimum_IsInclusive()
        {
            Assert.That(RammerGateRules.ShouldBreak(30f, _config.minBreakerMassKg, _config), Is.True);
        }

        [Test]
        public void ShouldBreak_NaN_False()
        {
            Assert.That(RammerGateRules.ShouldBreak(float.NaN, 1200f, _config), Is.False);
            Assert.That(RammerGateRules.ShouldBreak(30f, float.NaN, _config), Is.False);
        }

        [Test]
        public void ShouldBreak_NullConfig_False()
        {
            Assert.That(RammerGateRules.ShouldBreak(30f, 1200f, null), Is.False);
        }

        [Test]
        public void ShouldBreak_RespectsConfiguredThreshold()
        {
            _config.breakSpeedKmh = 100f;
            Assert.That(RammerGateRules.ShouldBreak(25f, 1200f, _config), Is.False); // 90 km/h
            Assert.That(RammerGateRules.ShouldBreak(28f, 1200f, _config), Is.True);  // 100.8 km/h
        }

        // ---------- RetainedVelocity ----------

        [Test]
        public void RetainedVelocity_ScalesHorizontalKeepsVertical()
        {
            Vector3 r = RammerGateRules.RetainedVelocity(new Vector3(10f, -3f, 20f), 0.85f);
            Assert.That(r.x, Is.EqualTo(8.5f).Within(1e-4f));
            Assert.That(r.y, Is.EqualTo(-3f));
            Assert.That(r.z, Is.EqualTo(17f).Within(1e-4f));
        }

        // ---------- DebrisVelocity ----------

        [Test]
        public void DebrisVelocity_SameInputs_Identical()
        {
            var v = new Vector3(0f, 0f, 25f);
            Assert.That(RammerGateRules.DebrisVelocity(v, 0.6f, 3f, 5, 42),
                Is.EqualTo(RammerGateRules.DebrisVelocity(v, 0.6f, 3f, 5, 42)));
        }

        [Test]
        public void DebrisVelocity_DifferentChunks_Differ()
        {
            var v = new Vector3(0f, 0f, 25f);
            Assert.That(RammerGateRules.DebrisVelocity(v, 0.6f, 3f, 1, 42),
                Is.Not.EqualTo(RammerGateRules.DebrisVelocity(v, 0.6f, 3f, 2, 42)));
        }

        [Test]
        public void DebrisVelocity_ZeroSpread_IsScaledBreakerVelocity()
        {
            Vector3 d = RammerGateRules.DebrisVelocity(new Vector3(0f, 0f, 25f), 0.6f, 0f, 3, 7);
            Assert.That(d.z, Is.EqualTo(15f).Within(1e-4f));
            Assert.That(d.x, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(d.y, Is.EqualTo(0f).Within(1e-6f));
        }

        [Test]
        public void DebrisVelocity_SpreadIsBounded_AndNeverDownward()
        {
            for (int i = 0; i < 50; i++)
            {
                Vector3 d = RammerGateRules.DebrisVelocity(Vector3.zero, 0.6f, 3f, i, 99);
                Assert.That(Mathf.Abs(d.x), Is.LessThanOrEqualTo(3f + 1e-4f));
                Assert.That(Mathf.Abs(d.z), Is.LessThanOrEqualTo(3f + 1e-4f));
                Assert.That(d.y, Is.InRange(0f, 3f + 1e-4f));
            }
        }
    }
}
