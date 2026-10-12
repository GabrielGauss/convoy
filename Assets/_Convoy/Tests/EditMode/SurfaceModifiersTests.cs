using Convoy.Vehicle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
// Unity's Is extends NUnit's Is with Is.Not.AllocatingGCMemory(); all the usual NUnit constraints still work.
// (GC.GetAllocatedBytesForCurrentThread returns 0 on Unity's Mono, so it cannot prove zero-alloc.)
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Convoy.Tests
{
    public class SurfaceModifiersTests
    {
        private SurfaceProfile _profile;
        private SurfaceProfile _otherProfile;
        private WheelSurfaceCache _cache;

        // Fields (not lambda locals) receive results inside the GC-measured lambdas, so the lambdas capture only
        // `this` and the closure is created before measurement starts.
        private SurfaceModifiers _modsSink;
        private SurfaceProfile _profileSink;
        private bool _hitSink;
        private byte[] _allocSink;

        [SetUp]
        public void SetUp()
        {
            _profile = ScriptableObject.CreateInstance<SurfaceProfile>();
            _otherProfile = ScriptableObject.CreateInstance<SurfaceProfile>();
            _cache = new WheelSurfaceCache(4);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_profile);
            Object.DestroyImmediate(_otherProfile);
        }

        private static void AssertAllExactlyOne(SurfaceModifiers m)
        {
            // Exact comparison on purpose: off-surface handling must be bit-identical (D-027).
            Assert.AreEqual(1f, m.Friction);
            Assert.AreEqual(1f, m.LateralGrip);
            Assert.AreEqual(1f, m.DriveForce);
            Assert.AreEqual(1f, m.TopSpeed);
            Assert.AreEqual(1f, m.RollingResistance);
        }

        // ---------- SurfaceModifiers ----------

        [Test]
        public void None_IsAllExactlyOne_AndDefault()
        {
            SurfaceModifiers none = SurfaceModifiers.None;
            AssertAllExactlyOne(none);
            Assert.AreEqual(SurfaceKind.Default, none.Kind);
            Assert.IsTrue(none.IsDefault);
        }

        [Test]
        public void None_TimesAnyValue_IsBitIdentical()
        {
            SurfaceModifiers none = SurfaceModifiers.None;
            float[] samples = { 0.1f, 1f / 3f, 12345.678f, 2.2e-38f, 3.4e38f };
            foreach (float x in samples)
            {
                Assert.AreEqual(x, x * none.Friction);
                Assert.AreEqual(x, x * none.TopSpeed);
            }
        }

        [Test]
        public void From_Null_EqualsNone()
        {
            SurfaceModifiers m = SurfaceModifiers.From(null);
            AssertAllExactlyOne(m);
            Assert.IsTrue(m.IsDefault);
        }

        [Test]
        public void From_DestroyedProfile_EqualsNone()
        {
            SurfaceProfile destroyed = ScriptableObject.CreateInstance<SurfaceProfile>();
            Object.DestroyImmediate(destroyed);
            AssertAllExactlyOne(SurfaceModifiers.From(destroyed));
        }

        [Test]
        public void From_DefaultMudProfile_CopiesFieldsAndKind()
        {
            SurfaceModifiers m = SurfaceModifiers.From(_profile);
            Assert.AreEqual(0.6f, m.Friction);
            Assert.AreEqual(0.6f, m.LateralGrip);
            Assert.AreEqual(0.6f, m.DriveForce);
            Assert.AreEqual(0.6f, m.TopSpeed);
            Assert.AreEqual(3f, m.RollingResistance);
            Assert.AreEqual(SurfaceKind.ToxicMud, m.Kind);
            Assert.IsFalse(m.IsDefault);
        }

        [Test]
        public void From_AllOneProfile_IsExactlyOne_ButKeepsKind()
        {
            _profile.frictionMultiplier = 1f;
            _profile.lateralGripMultiplier = 1f;
            _profile.driveForceMultiplier = 1f;
            _profile.topSpeedMultiplier = 1f;
            _profile.rollingResistanceMultiplier = 1f;
            SurfaceModifiers m = SurfaceModifiers.From(_profile);
            AssertAllExactlyOne(m);
            Assert.AreEqual(SurfaceKind.ToxicMud, m.Kind);
        }

        [Test]
        public void From_DefaultKindProfile_IsDefault()
        {
            _profile.kind = SurfaceKind.Default;
            Assert.IsTrue(SurfaceModifiers.From(_profile).IsDefault);
        }

        [Test]
        public void From_NaNField_FallsBackToOne_ForThatFieldOnly()
        {
            _profile.frictionMultiplier = float.NaN;
            _profile.rollingResistanceMultiplier = float.NaN;
            SurfaceModifiers m = SurfaceModifiers.From(_profile);
            Assert.AreEqual(1f, m.Friction);
            Assert.AreEqual(1f, m.RollingResistance);
            Assert.AreEqual(0.6f, m.LateralGrip);
            Assert.AreEqual(0.6f, m.DriveForce);
            Assert.AreEqual(0.6f, m.TopSpeed);
        }

        [Test]
        public void From_NegativeField_FallsBackToOne()
        {
            _profile.lateralGripMultiplier = -0.5f;
            _profile.topSpeedMultiplier = float.NegativeInfinity;
            SurfaceModifiers m = SurfaceModifiers.From(_profile);
            Assert.AreEqual(1f, m.LateralGrip);
            Assert.AreEqual(1f, m.TopSpeed);
            Assert.AreEqual(0.6f, m.Friction);
        }

        [Test]
        public void From_ZeroField_IsKept()
        {
            // 0 is a legal (if extreme) designer value — only NaN/negative are rejected.
            _profile.driveForceMultiplier = 0f;
            Assert.AreEqual(0f, SurfaceModifiers.From(_profile).DriveForce);
        }

        [Test]
        public void From_ReadsLiveValues()
        {
            // Profile numbers are read each call so Inspector edits apply in Play mode.
            _profile.frictionMultiplier = 0.3f;
            Assert.AreEqual(0.3f, SurfaceModifiers.From(_profile).Friction);
            _profile.frictionMultiplier = 0.9f;
            Assert.AreEqual(0.9f, SurfaceModifiers.From(_profile).Friction);
        }

        // ---------- WheelSurfaceCache ----------

        [Test]
        public void Cache_EmptyWheel_Misses()
        {
            Assert.IsFalse(_cache.TryGet(0, 123, out SurfaceProfile p));
            Assert.IsNull(p);
        }

        [Test]
        public void Cache_SameCollider_Hits_WithStoredProfile()
        {
            _cache.Store(1, 42, _profile);
            Assert.IsTrue(_cache.TryGet(1, 42, out SurfaceProfile p));
            Assert.AreSame(_profile, p);
        }

        [Test]
        public void Cache_NullProfile_IsCachedAsHit()
        {
            // Untagged ground is the common case: it must hit too, or TryGetComponent would run every step.
            _cache.Store(0, 7, null);
            Assert.IsTrue(_cache.TryGet(0, 7, out SurfaceProfile p));
            Assert.IsNull(p);
        }

        [Test]
        public void Cache_DifferentCollider_Misses()
        {
            _cache.Store(0, 42, _profile);
            Assert.IsFalse(_cache.TryGet(0, 43, out SurfaceProfile p));
            Assert.IsNull(p);
        }

        [Test]
        public void Cache_ZeroAndNegativeIds_AreValidKeys()
        {
            Assert.IsFalse(_cache.TryGet(2, 0, out _), "fresh slot must miss even for id 0");
            _cache.Store(2, 0, _profile);
            Assert.IsTrue(_cache.TryGet(2, 0, out _));
            _cache.Store(3, -99, _otherProfile);
            Assert.IsTrue(_cache.TryGet(3, -99, out SurfaceProfile p));
            Assert.AreSame(_otherProfile, p);
        }

        [Test]
        public void Cache_IsPerWheel()
        {
            _cache.Store(0, 42, _profile);
            _cache.Store(1, 42, _otherProfile);
            Assert.IsFalse(_cache.TryGet(2, 42, out _));
            _cache.TryGet(0, 42, out SurfaceProfile p0);
            _cache.TryGet(1, 42, out SurfaceProfile p1);
            Assert.AreSame(_profile, p0);
            Assert.AreSame(_otherProfile, p1);
        }

        [Test]
        public void Cache_Store_OverwritesPrevious()
        {
            _cache.Store(0, 42, _profile);
            _cache.Store(0, 50, _otherProfile);
            Assert.IsFalse(_cache.TryGet(0, 42, out _));
            Assert.IsTrue(_cache.TryGet(0, 50, out SurfaceProfile p));
            Assert.AreSame(_otherProfile, p);
        }

        [Test]
        public void Cache_Clear_MissesUntilStoredAgain_OnlyThatWheel()
        {
            _cache.Store(0, 42, _profile);
            _cache.Store(1, 42, _profile);
            _cache.Clear(0);
            Assert.IsFalse(_cache.TryGet(0, 42, out SurfaceProfile p));
            Assert.IsNull(p);
            Assert.IsTrue(_cache.TryGet(1, 42, out _), "clearing wheel 0 must not affect wheel 1");
            _cache.Store(0, 42, _profile);
            Assert.IsTrue(_cache.TryGet(0, 42, out _));
        }

        [Test]
        public void Cache_NegativeWheelCount_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new WheelSurfaceCache(-1));
        }

        // ---------- Zero-alloc (per-wheel hot path, LV-02 perf constraint) ----------

        [Test]
        public void AllocationConstraint_PositiveControl_DetectsAllocation()
        {
            // Proves the constraint actually measures allocations in this environment; otherwise the
            // zero-alloc tests below would pass vacuously.
            Assert.That(() => { _allocSink = new byte[1024]; }, Is.AllocatingGCMemory());
        }

        [Test]
        public void From_DoesNotAllocate()
        {
            SurfaceModifiers.From(_profile); // warm-up (JIT, static init) outside measurement
            Assert.That(() => { _modsSink = SurfaceModifiers.From(_profile); }, Is.Not.AllocatingGCMemory());
            Assert.That(() => { _modsSink = SurfaceModifiers.From(null); }, Is.Not.AllocatingGCMemory());
            Assert.That(() => { _modsSink = SurfaceModifiers.None; }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void CacheCalls_DoNotAllocate()
        {
            // Warm-up outside measurement.
            _cache.Store(0, 42, _profile);
            _cache.TryGet(0, 42, out _);
            _cache.TryGet(0, 43, out _);
            _cache.Clear(1);

            Assert.That(() => { _hitSink = _cache.TryGet(0, 42, out _profileSink); }, Is.Not.AllocatingGCMemory());
            Assert.That(() => { _hitSink = _cache.TryGet(0, 43, out _profileSink); }, Is.Not.AllocatingGCMemory());
            Assert.That(() => _cache.Store(1, 77, _otherProfile), Is.Not.AllocatingGCMemory());
            Assert.That(() => _cache.Store(2, 78, null), Is.Not.AllocatingGCMemory());
            Assert.That(() => _cache.Clear(1), Is.Not.AllocatingGCMemory());
        }
    }
}
