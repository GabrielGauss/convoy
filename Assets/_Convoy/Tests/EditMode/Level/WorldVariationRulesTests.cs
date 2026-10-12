using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Convoy.Level.Tests
{
    /// <summary>TR-01a acceptance criterion 7: per-run world variation.</summary>
    public class WorldVariationRulesTests
    {
        private const int LayoutSeed = 1;
        private static readonly Vector2 LaneDir = new Vector2(-0.1f, 1f); // desert route heads roughly north through the field

        private WorldVariationConfig _config;
        private List<Vector2> _slots;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<WorldVariationConfig>();
            _slots = new List<Vector2>(_config.mineSlotCount);
            WorldVariationRules.MineSlots(LayoutSeed, _config, _slots);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_config);

        // ---------------------------------------------------------------- MineSlots

        [Test]
        public void MineSlots_CountAndInsideTheField()
        {
            Assert.AreEqual(_config.mineSlotCount, _slots.Count);
            foreach (Vector2 s in _slots)
                Assert.That((s - _config.minefieldCentre).magnitude, Is.LessThanOrEqualTo(_config.minefieldRadius + 1e-3f));
        }

        [Test]
        public void MineSlots_SameLayoutSeed_Identical()
        {
            var again = new List<Vector2>();
            WorldVariationRules.MineSlots(LayoutSeed, _config, again);
            Assert.AreEqual(_slots.Count, again.Count);
            for (int i = 0; i < _slots.Count; i++) Assert.AreEqual(_slots[i], again[i]);
        }

        [Test]
        public void MineSlots_DifferentLayoutSeed_Differs()
        {
            var other = new List<Vector2>();
            WorldVariationRules.MineSlots(LayoutSeed + 1, _config, other);
            bool anyDifferent = false;
            for (int i = 0; i < _slots.Count; i++) anyDifferent |= _slots[i] != other[i];
            Assert.That(anyDifferent, Is.True);
        }

        [Test]
        public void MineSlots_AreDistinct()
        {
            for (int i = 0; i < _slots.Count; i++)
            for (int j = i + 1; j < _slots.Count; j++)
                Assert.That((_slots[i] - _slots[j]).magnitude, Is.GreaterThan(1f), "slots " + i + " and " + j + " overlap");
        }

        // ---------------------------------------------------------------- Select

        [Test]
        public void Select_SameSeed_IdenticalResultAndActiveSlots()
        {
            var a = new List<int>(_config.activeMines);
            var b = new List<int>(_config.activeMines);
            for (int seed = 1; seed <= 50; seed++)
            {
                WorldVariation va = WorldVariationRules.Select(seed, _config, _slots, LaneDir, a);
                WorldVariation vb = WorldVariationRules.Select(seed, _config, _slots, LaneDir, b);
                Assert.AreEqual(va.Seed, vb.Seed);
                Assert.AreEqual(va.DustStorm, vb.DustStorm);
                Assert.AreEqual(va.OpenShortcutMask, vb.OpenShortcutMask);
                Assert.AreEqual(va.SafeLane, vb.SafeLane);
                Assert.AreEqual(a.Count, b.Count);
                for (int i = 0; i < a.Count; i++) Assert.AreEqual(a[i], b[i]);
            }
        }

        [Test]
        public void Select_StormRate_MatchesChance()
        {
            var active = new List<int>(_config.activeMines);
            int storms = 0;
            for (int seed = 1; seed <= 1000; seed++)
                if (WorldVariationRules.Select(seed, _config, _slots, LaneDir, active).DustStorm) storms++;
            Assert.That(storms / 1000f, Is.EqualTo(_config.dustStormChance).Within(0.05f));
        }

        [Test]
        public void Select_OpenShortcuts_WithinBoundsAndEveryShortcutGetsUsed()
        {
            var active = new List<int>(_config.activeMines);
            int n = _config.shortcutIds.Length;
            var openedCount = new int[n];
            for (int seed = 1; seed <= 1000; seed++)
            {
                WorldVariation v = WorldVariationRules.Select(seed, _config, _slots, LaneDir, active);
                Assert.AreEqual(0u, v.OpenShortcutMask >> n, "bit set beyond shortcutIds");
                int open = 0;
                for (int i = 0; i < n; i++)
                {
                    if (!v.IsShortcutOpen(i)) continue;
                    open++;
                    openedCount[i]++;
                }
                Assert.That(open, Is.InRange(_config.minOpenShortcuts, _config.maxOpenShortcuts), "seed " + seed);
            }
            for (int i = 0; i < n; i++) Assert.That(openedCount[i], Is.GreaterThan(0), _config.shortcutIds[i] + " never opens");
        }

        [Test]
        public void Select_ExactlyActiveMines_NoneOnTheSafeLane()
        {
            var active = new List<int>(_config.activeMines);
            Vector2 dir = LaneDir.normalized;
            var right = new Vector2(dir.y, -dir.x);
            for (int seed = 1; seed <= 300; seed++)
            {
                WorldVariation v = WorldVariationRules.Select(seed, _config, _slots, LaneDir, active);
                Assert.AreEqual(_config.activeMines, active.Count, "seed " + seed);
                Assert.That(v.SafeLane, Is.InRange(0, _config.safeLaneOffsets.Length - 1));
                float offset = _config.safeLaneOffsets[v.SafeLane];
                var seen = new HashSet<int>();
                foreach (int i in active)
                {
                    Assert.That(seen.Add(i), Is.True, "duplicate slot " + i);
                    Assert.That(i, Is.InRange(0, _slots.Count - 1));
                    float lateral = Vector2.Dot(_slots[i] - _config.minefieldCentre, right) - offset;
                    Assert.That(Mathf.Abs(lateral), Is.GreaterThan(_config.safeLaneHalfWidth), "mine on the safe lane, seed " + seed);
                }
            }
        }

        [Test]
        public void Select_TooFewSlotsOffTheLane_ActivatesAllOfThem()
        {
            _config.safeLaneHalfWidth = 85f; // lane band covers almost the whole field
            _config.safeLaneOffsets = new[] { 0f };
            var active = new List<int>();
            WorldVariationRules.Select(5, _config, _slots, LaneDir, active);
            Vector2 dir = LaneDir.normalized;
            var right = new Vector2(dir.y, -dir.x);
            int eligible = 0;
            foreach (Vector2 s in _slots)
                if (Mathf.Abs(Vector2.Dot(s - _config.minefieldCentre, right)) > _config.safeLaneHalfWidth) eligible++;
            Assert.That(eligible, Is.LessThan(_config.activeMines), "test setup: expected a shortage");
            Assert.AreEqual(eligible, active.Count);
        }

        [Test]
        public void Select_DoesNotAllocate()
        {
            var active = new List<int>(_config.activeMines);
            IReadOnlyList<Vector2> slots = _slots;
            WorldVariationRules.Select(1, _config, slots, LaneDir, active); // warm-up: JIT and static setup
            int seed = 2;
            Assert.That(() =>
            {
                WorldVariationRules.Select(seed++, _config, slots, LaneDir, active);
            }, Is.Not.AllocatingGCMemory());
        }
    }
}
