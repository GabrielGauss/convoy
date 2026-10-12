using System;
using System.Collections.Generic;
using UnityEngine;

namespace Convoy.Level
{
    /// <summary>
    /// Rolls and applies this run's world variation (TR-01c, D-031) once, at scene start: which shortcuts are closed by
    /// their rockfall and which mine slots are live. Weather is only <i>decided</i> here (<see cref="Current"/>); the
    /// presentation layer (<c>ParamoAtmosphere</c>) reads it and sets the fog, so Level never references Presentation.
    /// The pure rules are <see cref="WorldVariationRules"/>; this is the thin scene adapter.
    /// </summary>
    /// <remarks>
    /// <c>[DefaultExecutionOrder(-200)]</c> + work in <c>Awake</c>: Unity runs this before every default-order
    /// Awake and long before the first <c>FixedUpdate</c>, so no vehicle ever drives through a world that changes under
    /// it. Readers that might wake earlier call <see cref="EnsureApplied"/> (idempotent) instead of relying on order.
    /// Blockers and mine slots are generated inactive by the terrain generator and only toggled here; SetActive works
    /// on static-batched objects.
    /// </remarks>
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class WorldVariationRunner : MonoBehaviour
    {
        [Tooltip("Variation settings (Paramo_WorldVariation). fixedSeed 0 = a new world every run.")]
        [SerializeField] private WorldVariationConfig _config;
        [Tooltip("Rockfall per shortcut, same order as the config's shortcutIds. Active = that shortcut is closed this run. " +
                 "Empty entries are allowed (that shortcut simply has no blocker).")]
        [SerializeField] private GameObject[] _shortcutBlockers = Array.Empty<GameObject>();
        [Tooltip("Every possible mine position (generated MineSlot_xx objects). Only the live ones are activated.")]
        [SerializeField] private GameObject[] _mineSlots = Array.Empty<GameObject>();
        [Tooltip("Driving direction of the route through the minefield (XZ); safe lanes run along it.")]
        [SerializeField] private Vector2 _laneDir = Vector2.up;

        private List<Vector2> _slotPositions;
        private List<int> _activeSlots;

        /// <summary>This run's world. Valid once <see cref="IsApplied"/> is true.</summary>
        public WorldVariation Current { get; private set; }

        public bool IsApplied { get; private set; }

        public WorldVariationConfig Config => _config;

        /// <summary>Editor/test wiring (scenes use the serialized fields). Does not apply anything by itself.</summary>
        public void Configure(WorldVariationConfig config, GameObject[] shortcutBlockers, GameObject[] mineSlots, Vector2 laneDir)
        {
            _config = config;
            _shortcutBlockers = shortcutBlockers ?? Array.Empty<GameObject>();
            _mineSlots = mineSlots ?? Array.Empty<GameObject>();
            _laneDir = laneDir;
        }

        private void Awake()
        {
            EnsureApplied();
        }

        /// <summary>Applies the variation for this run if that has not happened yet. Safe to call any number of times.</summary>
        public void EnsureApplied()
        {
            if (IsApplied) return;
            if (_config == null)
            {
                Debug.LogError($"{name}: WorldVariationRunner has no WorldVariationConfig; the world stays as generated.", this);
                return;
            }
            Apply(_config.fixedSeed != 0 ? _config.fixedSeed : FreshSeed());
        }

        /// <summary>
        /// Rolls the world for <paramref name="seed"/> and applies it (replaces any earlier run). Public so a debug tool
        /// or a future restart flow can replay a reported seed without reloading the scene. Setup-time only.
        /// </summary>
        public void Apply(int seed)
        {
            if (_config == null) return;
            int slotCount = _mineSlots != null ? _mineSlots.Length : 0;
            // Sized once to the slot count, so Select itself never grows them (it is 0 B GC with pre-sized lists).
            if (_slotPositions == null) _slotPositions = new List<Vector2>(slotCount);
            if (_activeSlots == null) _activeSlots = new List<int>(Mathf.Max(slotCount, _config.activeMines));
            _slotPositions.Clear();
            for (int i = 0; i < slotCount; i++)
            {
                // A missing slot keeps its index (so indices still match the objects) but sits far away, where no
                // safe-lane rule cares about it; it is simply never shown.
                Vector3 p = _mineSlots[i] != null ? _mineSlots[i].transform.position : new Vector3(float.MaxValue, 0f, float.MaxValue);
                _slotPositions.Add(new Vector2(p.x, p.z));
            }

            WorldVariation v = WorldVariationRules.Select(seed, _config, _slotPositions, _laneDir, _activeSlots);

            int shortcutCount = _shortcutBlockers != null ? _shortcutBlockers.Length : 0;
            for (int i = 0; i < shortcutCount; i++)
                if (_shortcutBlockers[i] != null) _shortcutBlockers[i].SetActive(!v.IsShortcutOpen(i));

            for (int i = 0; i < slotCount; i++)
                if (_mineSlots[i] != null) _mineSlots[i].SetActive(false);
            for (int k = 0; k < _activeSlots.Count; k++)
            {
                GameObject slot = _mineSlots[_activeSlots[k]];
                if (slot != null) slot.SetActive(true);
            }

            Current = v;
            IsApplied = true;
            // One line per run so a playtester can report the seed and the orchestrator can replay it (fixedSeed).
            Debug.Log($"[World] seed {v.Seed}: storm {(v.DustStorm ? "on" : "off")}, open shortcuts {Describe(v)}, " +
                      $"{_activeSlots.Count} live mines, safe lane {v.SafeLane}.", this);
        }

        private string Describe(WorldVariation v)
        {
            string[] ids = _config.shortcutIds;
            if (ids == null || ids.Length == 0) return "none";
            var text = new System.Text.StringBuilder();
            for (int i = 0; i < ids.Length && i < 32; i++)
            {
                if (!v.IsShortcutOpen(i)) continue;
                if (text.Length > 0) text.Append(", ");
                text.Append(ids[i]);
            }
            return text.Length > 0 ? text.ToString() : "none";
        }

        private static int FreshSeed()
        {
            // Any non-zero int: 0 means "random" in the config, so it must never be a real seed.
            int seed = unchecked((int)DateTime.UtcNow.Ticks ^ (Environment.TickCount * 397));
            return seed != 0 ? seed : 1;
        }
    }
}
