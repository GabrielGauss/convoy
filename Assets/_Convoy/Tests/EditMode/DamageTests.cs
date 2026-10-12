using Convoy.Core;
using Convoy.Vehicle;
using NUnit.Framework;
using UnityEngine;

namespace Convoy.Tests
{
    public class DamageTests
    {
        private DamageConfig _config;

        [SetUp]
        public void SetUp() => _config = ScriptableObject.CreateInstance<DamageConfig>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_config);

        private static DamageEvent Hit(float amount, DamageKind kind = DamageKind.Projectile)
        {
            return new DamageEvent { Amount = amount, Kind = kind };
        }

        // ---------- VehicleId ----------

        [Test]
        public void VehicleId_NoneIsInvalidAndEqualsDefault()
        {
            Assert.That(VehicleId.None.IsValid, Is.False);
            Assert.That(VehicleId.None == default(VehicleId), Is.True);
        }

        [Test]
        public void VehicleId_EqualityByValue()
        {
            var a = new VehicleId { Value = 3 };
            var b = new VehicleId { Value = 3 };
            var c = new VehicleId { Value = 4 };
            Assert.That(a.IsValid, Is.True);
            Assert.That(a == b, Is.True);
            Assert.That(a != c, Is.True);
            Assert.That(a.Equals(b), Is.True);
            Assert.That(a.Equals((object)b), Is.True);
            Assert.That(a.Equals((object)"3"), Is.False);
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }

        // ---------- ImpactDamage ----------

        [Test]
        public void Impact_BelowThreshold_IsZero()
        {
            Assert.That(ImpactDamage.Compute(7.9f, 0f, _config), Is.EqualTo(0f));
            Assert.That(ImpactDamage.Compute(8f, 0f, _config), Is.EqualTo(0f));
        }

        [Test]
        public void Impact_AboveThreshold_ScalesLinearly()
        {
            // (12 - 8) * 2.5 = 10
            Assert.That(ImpactDamage.Compute(12f, 0f, _config), Is.EqualTo(10f).Within(1e-4f));
        }

        [Test]
        public void Impact_LandingContact_UsesHigherThreshold()
        {
            // dv 12 is damaging against a wall but free when landing (threshold 14).
            Assert.That(ImpactDamage.Compute(12f, 0.9f, _config), Is.EqualTo(0f));
            // (16 - 14) * 2.5 = 5
            Assert.That(ImpactDamage.Compute(16f, 0.9f, _config), Is.EqualTo(5f).Within(1e-4f));
        }

        [Test]
        public void Impact_NormalDotExactlyAtLandingDot_IsNotLanding()
        {
            // Strictly greater than landingNormalDot is required for the landing threshold.
            Assert.That(ImpactDamage.Compute(12f, _config.landingNormalDot, _config), Is.EqualTo(10f).Within(1e-4f));
        }

        [Test]
        public void Impact_IsCapped()
        {
            // 100 km/h wall: (28 - 8) * 2.5 = 50 -> capped at 35
            Assert.That(ImpactDamage.Compute(28f, 0f, _config), Is.EqualTo(_config.maxImpactDamage));
            Assert.That(ImpactDamage.Compute(1000f, 0f, _config), Is.EqualTo(_config.maxImpactDamage));
        }

        [Test]
        public void Impact_NaNOrNegativeDeltaV_IsZero()
        {
            Assert.That(ImpactDamage.Compute(float.NaN, 0f, _config), Is.EqualTo(0f));
            Assert.That(ImpactDamage.Compute(-20f, 0f, _config), Is.EqualTo(0f));
            Assert.That(ImpactDamage.Compute(0f, 0f, _config), Is.EqualTo(0f));
        }

        // ---------- ImpactWindow ----------

        [Test]
        public void Window_FirstCall_ReturnsRaw()
        {
            var w = new ImpactWindow(_config);
            Assert.That(w.Filter(10f, 5.0), Is.EqualTo(10f));
        }

        [Test]
        public void Window_TwoContactsInsideWindow_CountOnce()
        {
            var w = new ImpactWindow(_config);
            float total = w.Filter(10f, 1.00) + w.Filter(10f, 1.02);
            Assert.That(total, Is.EqualTo(10f).Within(1e-5f));
        }

        [Test]
        public void Window_LargerLaterContact_AddsOnlyTheExcess()
        {
            var w = new ImpactWindow(_config);
            float total = w.Filter(6f, 1.00) + w.Filter(10f, 1.05) + w.Filter(8f, 1.10);
            Assert.That(total, Is.EqualTo(10f).Within(1e-5f));
        }

        [Test]
        public void Window_SmallerLaterContact_AddsNothing()
        {
            var w = new ImpactWindow(_config);
            w.Filter(10f, 1.0);
            Assert.That(w.Filter(4f, 1.1), Is.EqualTo(0f));
        }

        [Test]
        public void Window_AfterTimeout_StartsNewWindow()
        {
            var w = new ImpactWindow(_config);
            w.Filter(10f, 1.0);
            Assert.That(w.Filter(10f, 1.0 + _config.impactWindowSeconds + 0.01), Is.EqualTo(10f).Within(1e-5f));
        }

        [Test]
        public void Window_ExactlyAtWindowEdge_IsStillSameWindow()
        {
            // Rule is "time - start > window" for a new window, so equality stays inside.
            // Times chosen as exact binary fractions so the subtraction is exact.
            _config.impactWindowSeconds = 0.25f;
            var w = new ImpactWindow(_config);
            w.Filter(10f, 1.0);
            Assert.That(w.Filter(10f, 1.25), Is.EqualTo(0f));
        }

        [Test]
        public void Window_NaNAndNegative_AreZero()
        {
            var w = new ImpactWindow(_config);
            Assert.That(w.Filter(float.NaN, 1.0), Is.EqualTo(0f));
            Assert.That(w.Filter(-5f, 5.0), Is.EqualTo(0f));
        }

        // ---------- VehicleHealth ----------

        [Test]
        public void Health_StartsFull()
        {
            var h = new VehicleHealth(_config);
            Assert.That(h.State.Health, Is.EqualTo(100f));
            Assert.That(h.State.MaxHealth, Is.EqualTo(100f));
            Assert.That(h.State.Health01, Is.EqualTo(1f));
            Assert.That(h.State.Wrecked, Is.False);
            Assert.That(h.State.HitCount, Is.EqualTo(0));
            Assert.That(h.State.TotalDamageTaken, Is.EqualTo(0f));
        }

        [Test]
        public void Health_Apply_ReducesAndCounts()
        {
            var h = new VehicleHealth(_config);
            float applied = h.Apply(Hit(30f));
            Assert.That(applied, Is.EqualTo(30f));
            Assert.That(h.State.Health, Is.EqualTo(70f));
            Assert.That(h.State.Health01, Is.EqualTo(0.7f).Within(1e-6f));
            Assert.That(h.State.HitCount, Is.EqualTo(1));
            Assert.That(h.State.TotalDamageTaken, Is.EqualTo(30f));
        }

        [Test]
        public void Health_Apply_ClampsAtZeroAndWrecks()
        {
            var h = new VehicleHealth(_config);
            float applied = h.Apply(Hit(250f));
            Assert.That(applied, Is.EqualTo(100f), "returns health actually removed");
            Assert.That(h.State.Health, Is.EqualTo(0f));
            Assert.That(h.State.Wrecked, Is.True);
            Assert.That(h.State.TotalDamageTaken, Is.EqualTo(100f));
        }

        [Test]
        public void Health_Apply_WhenWrecked_IsIgnored()
        {
            var h = new VehicleHealth(_config);
            h.Apply(Hit(100f));
            Assert.That(h.Apply(Hit(10f)), Is.EqualTo(0f));
            Assert.That(h.State.HitCount, Is.EqualTo(1));
            Assert.That(h.State.TotalDamageTaken, Is.EqualTo(100f));
        }

        [Test]
        public void Health_Apply_IgnoresZeroNegativeAndNaN()
        {
            var h = new VehicleHealth(_config);
            Assert.That(h.Apply(Hit(0f)), Is.EqualTo(0f));
            Assert.That(h.Apply(Hit(-5f)), Is.EqualTo(0f));
            Assert.That(h.Apply(Hit(float.NaN)), Is.EqualTo(0f));
            Assert.That(h.State.Health, Is.EqualTo(100f));
            Assert.That(h.State.HitCount, Is.EqualTo(0));
        }

        [Test]
        public void Health_TotalDamageTaken_NeverDecreases()
        {
            var h = new VehicleHealth(_config);
            float last = 0f;
            for (int i = 0; i < 5; i++)
            {
                h.Apply(Hit(7f));
                h.Repair(3f);
                Assert.That(h.State.TotalDamageTaken, Is.GreaterThanOrEqualTo(last));
                last = h.State.TotalDamageTaken;
            }
            h.Restore();
            Assert.That(h.State.TotalDamageTaken, Is.EqualTo(last));
        }

        [Test]
        public void Health_Repair_ClampsAtMax()
        {
            var h = new VehicleHealth(_config);
            h.Apply(Hit(20f));
            Assert.That(h.Repair(50f), Is.EqualTo(20f));
            Assert.That(h.State.Health, Is.EqualTo(100f));
            Assert.That(h.Repair(5f), Is.EqualTo(0f));
        }

        [Test]
        public void Health_Repair_IgnoresNonPositiveAndNaN()
        {
            var h = new VehicleHealth(_config);
            h.Apply(Hit(20f));
            Assert.That(h.Repair(-1f), Is.EqualTo(0f));
            Assert.That(h.Repair(float.NaN), Is.EqualTo(0f));
            Assert.That(h.State.Health, Is.EqualTo(80f));
        }

        [Test]
        public void Health_Repair_WhenWrecked_IsNoOp()
        {
            var h = new VehicleHealth(_config);
            h.Apply(Hit(100f));
            Assert.That(h.Repair(50f), Is.EqualTo(0f));
            Assert.That(h.State.Health, Is.EqualTo(0f));
            Assert.That(h.State.Wrecked, Is.True);
        }

        [Test]
        public void Health_Restore_RevivesAndKeepsTelemetry()
        {
            var h = new VehicleHealth(_config);
            h.Apply(Hit(100f));
            h.Restore();
            Assert.That(h.State.Health, Is.EqualTo(100f));
            Assert.That(h.State.Wrecked, Is.False);
            Assert.That(h.State.TotalDamageTaken, Is.EqualTo(100f));
            Assert.That(h.State.HitCount, Is.EqualTo(1));
        }

        [Test]
        public void Health_TwoBigCrashes_LandInDegradeZone()
        {
            // Pieces together: two head-on 100 km/h crashes (35 HP each) leave 30 HP, inside the degrade zone.
            var h = new VehicleHealth(_config);
            var w = new ImpactWindow(_config);
            for (int i = 0; i < 2; i++)
            {
                float raw = ImpactDamage.Compute(28f, 0f, _config);
                h.Apply(new DamageEvent { Amount = w.Filter(raw, 10.0 * (i + 1)), Kind = DamageKind.Impact });
            }
            Assert.That(h.State.Health, Is.EqualTo(30f).Within(1e-4f));
            Assert.That(h.State.Health01, Is.LessThan(_config.degradeBelowHealth01));
            Assert.That(HandlingModifiers.FromHealth(h.State.Health01, _config).TopSpeed, Is.LessThan(1f));
        }

        [Test]
        public void Health_FitsDamageableContract()
        {
            // IDamageable is the only damage entry point; a trivial adapter over VehicleHealth must satisfy it
            // (VehicleDamage in D-01b is the real one).
            IDamageable d = new Adapter(new VehicleHealth(_config));
            Assert.That(d.ApplyDamage(Hit(12f)), Is.EqualTo(12f));
        }

        private sealed class Adapter : IDamageable
        {
            private readonly VehicleHealth _health;
            public Adapter(VehicleHealth health) { _health = health; }
            public float ApplyDamage(in DamageEvent damage) => _health.Apply(damage);
        }
    }
}
