using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Convoy.Level.Tests
{
    /// <summary>TR-01c AC 2: the runner applies the rolled world to the scene objects; a fixed seed replays it exactly.</summary>
    public class WorldVariationRunnerTests
    {
        private readonly List<Object> _created = new List<Object>();
        private WorldVariationConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<WorldVariationConfig>();
            _config.fixedSeed = 4242;
            _created.Add(_config);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _created)
                if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        private GameObject Make(string name, Vector3 position = default)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            go.SetActive(false); // generated inactive, like the real blockers and mine slots
            _created.Add(go);
            return go;
        }

        private WorldVariationRunner MakeRunner(out GameObject[] blockers, out GameObject[] mines)
        {
            blockers = new GameObject[_config.shortcutIds.Length];
            for (int i = 0; i < blockers.Length; i++) blockers[i] = Make("Blocker_" + _config.shortcutIds[i]);
            var slots = new List<Vector2>();
            WorldVariationRules.MineSlots(1, _config, slots);
            mines = new GameObject[slots.Count];
            for (int i = 0; i < mines.Length; i++) mines[i] = Make("MineSlot_" + i, new Vector3(slots[i].x, 0f, slots[i].y));

            // Host stays inactive: Awake must not run before Configure (it would log "no config" and fail the test).
            GameObject host = Make("World");
            var runner = host.AddComponent<WorldVariationRunner>();
            runner.Configure(_config, blockers, mines, Vector2.up);
            return runner;
        }

        [Test]
        public void EnsureApplied_ClosedShortcutsShowTheirRockfall_AndExactlyTheLiveMinesShow()
        {
            WorldVariationRunner runner = MakeRunner(out GameObject[] blockers, out GameObject[] mines);
            runner.EnsureApplied();

            Assert.That(runner.IsApplied);
            Assert.AreEqual(_config.fixedSeed, runner.Current.Seed);
            for (int i = 0; i < blockers.Length; i++)
                Assert.AreEqual(!runner.Current.IsShortcutOpen(i), blockers[i].activeSelf, _config.shortcutIds[i]);
            int live = 0;
            foreach (GameObject m in mines) if (m.activeSelf) live++;
            Assert.AreEqual(_config.activeMines, live);
        }

        [Test]
        public void FixedSeed_ReproducesTheSameWorld()
        {
            WorldVariationRunner a = MakeRunner(out GameObject[] blockersA, out GameObject[] minesA);
            WorldVariationRunner b = MakeRunner(out GameObject[] blockersB, out GameObject[] minesB);
            a.EnsureApplied();
            b.EnsureApplied();

            Assert.AreEqual(a.Current.DustStorm, b.Current.DustStorm);
            Assert.AreEqual(a.Current.OpenShortcutMask, b.Current.OpenShortcutMask);
            Assert.AreEqual(a.Current.SafeLane, b.Current.SafeLane);
            for (int i = 0; i < blockersA.Length; i++) Assert.AreEqual(blockersA[i].activeSelf, blockersB[i].activeSelf);
            for (int i = 0; i < minesA.Length; i++) Assert.AreEqual(minesA[i].activeSelf, minesB[i].activeSelf, "mine " + i);
        }

        [Test]
        public void EnsureApplied_IsIdempotent()
        {
            WorldVariationRunner runner = MakeRunner(out GameObject[] blockers, out _);
            runner.EnsureApplied();
            WorldVariation first = runner.Current;
            _config.fixedSeed = 99; // a second call must not re-roll
            runner.EnsureApplied();
            Assert.AreEqual(first.Seed, runner.Current.Seed);
        }

        [Test]
        public void DefaultConfig_NeverClosesTheCave()
        {
            // The cave is on the only river route (TR-01c review): it must not be a variable shortcut.
            Assert.That(System.Array.IndexOf(_config.shortcutIds, "cave"), Is.EqualTo(-1));
        }
    }
}
