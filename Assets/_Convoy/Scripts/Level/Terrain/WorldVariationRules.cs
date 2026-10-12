using System;
using System.Collections.Generic;
using UnityEngine;

namespace Convoy.Level
{
    /// <summary>
    /// Pure per-run world variation (D-031). Deterministic from a seed so a run can be replayed (fixedSeed) and tested;
    /// <see cref="Select"/> allocates nothing so it is safe to call from scene setup or a restart without GC spikes.
    /// </summary>
    public static class WorldVariationRules
    {
        /// <summary>
        /// The minefield's fixed mine positions for a layout: a jittered grid clipped to the minefield circle, shuffled
        /// and trimmed to <c>mineSlotCount</c>. Depends only on <paramref name="layoutSeed"/> and the config, so the
        /// generated slot objects never move between runs; <see cref="Select"/> only picks which are live.
        /// Clears <paramref name="into"/>; returns the slot count. Setup-time (allocates only if the list must grow).
        /// </summary>
        public static int MineSlots(int layoutSeed, WorldVariationConfig c, List<Vector2> into)
        {
            if (c == null) throw new ArgumentNullException(nameof(c));
            if (into == null) throw new ArgumentNullException(nameof(into));
            into.Clear();
            int count = Mathf.Max(0, c.mineSlotCount);
            float radius = Mathf.Max(0f, c.minefieldRadius);
            if (count == 0) return 0;

            // Smallest grid whose cell centres inside the circle can hold every slot (circle ~ pi/4 of the square).
            int n = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(count * 4f / Mathf.PI)));
            while (InsideCount(n) < count) n++;

            float unitCell = 2f / n;
            float jitter = Mathf.Clamp(c.mineSlotJitter, 0f, 0.5f) * unitCell * radius;
            var rng = new DeterministicRng(layoutSeed);
            for (int z = 0; z < n; z++)
            {
                for (int x = 0; x < n; x++)
                {
                    // Same unit-circle test as InsideCount, so the grid always yields at least `count` slots.
                    float px = -1f + (x + 0.5f) * unitCell, pz = -1f + (z + 0.5f) * unitCell;
                    if (px * px + pz * pz > 1f) continue;
                    var local = new Vector2(px * radius, pz * radius);
                    local.x += (rng.NextFloat01() * 2f - 1f) * jitter;
                    local.y += (rng.NextFloat01() * 2f - 1f) * jitter;
                    float m = local.magnitude;
                    if (m > radius) local *= radius / m; // jitter must not push a mine out of the field
                    into.Add(c.minefieldCentre + local);
                }
            }

            // Shuffle (Fisher-Yates) so trimming to `count` does not always drop the same edge of the grid.
            for (int i = into.Count - 1; i > 0; i--)
            {
                int j = rng.NextInt(i + 1);
                Vector2 tmp = into[i];
                into[i] = into[j];
                into[j] = tmp;
            }
            if (into.Count > count) into.RemoveRange(count, into.Count - count);
            return into.Count;
        }

        /// <summary>
        /// Rolls this run's world. Draw order (fixed, version-stable): storm, open-shortcut count, each open shortcut,
        /// safe lane, live mines. Live mines are chosen uniformly among slots farther than <c>safeLaneHalfWidth</c> from
        /// the safe lane (a line through the minefield centre along <paramref name="laneDir"/>, offset sideways by the
        /// chosen <c>safeLaneOffsets</c> entry; + = right of travel). If too few slots qualify, all qualifying ones are
        /// live. Clears <paramref name="activeSlotsInto"/> and fills it with slot indices; 0 B GC when its capacity
        /// &gt;= <c>activeMines</c>.
        /// </summary>
        public static WorldVariation Select(int runSeed, WorldVariationConfig c, IReadOnlyList<Vector2> slots, Vector2 laneDir,
            List<int> activeSlotsInto)
        {
            if (c == null) throw new ArgumentNullException(nameof(c));
            if (activeSlotsInto == null) throw new ArgumentNullException(nameof(activeSlotsInto));
            activeSlotsInto.Clear();

            var rng = new DeterministicRng(runSeed);
            bool storm = rng.NextFloat01() < c.dustStormChance;

            int shortcutCount = c.shortcutIds == null ? 0 : Mathf.Min(c.shortcutIds.Length, 32);
            int lo = Mathf.Clamp(Mathf.Min(c.minOpenShortcuts, c.maxOpenShortcuts), 0, shortcutCount);
            int hi = Mathf.Clamp(Mathf.Max(c.minOpenShortcuts, c.maxOpenShortcuts), 0, shortcutCount);
            int open = lo + rng.NextInt(hi - lo + 1);
            uint mask = 0u;
            for (int k = 0; k < open; k++)
            {
                // Pick the pick-th still-closed shortcut: a partial shuffle with no array.
                int pick = rng.NextInt(shortcutCount - k);
                for (int i = 0; i < shortcutCount; i++)
                {
                    if ((mask & (1u << i)) != 0) continue;
                    if (pick-- == 0)
                    {
                        mask |= 1u << i;
                        break;
                    }
                }
            }

            int laneCount = c.safeLaneOffsets == null ? 0 : c.safeLaneOffsets.Length;
            int lane = laneCount > 0 ? rng.NextInt(laneCount) : -1;

            if (slots != null && slots.Count > 0)
            {
                Vector2 dir = laneDir.sqrMagnitude > 1e-12f ? laneDir.normalized : Vector2.up;
                var right = new Vector2(dir.y, -dir.x);
                float offset = lane >= 0 ? c.safeLaneOffsets[lane] : 0f;
                float halfWidth = lane >= 0 ? Mathf.Max(0f, c.safeLaneHalfWidth) : -1f;

                int eligible = 0;
                for (int i = 0; i < slots.Count; i++)
                    if (OffLane(slots[i], c.minefieldCentre, right, offset, halfWidth)) eligible++;

                // Selection sampling (Knuth, Algorithm S): exactly `need` uniform picks in one pass, no scratch array.
                int need = Mathf.Min(Mathf.Max(0, c.activeMines), eligible);
                int remaining = eligible;
                for (int i = 0; i < slots.Count && need > 0; i++)
                {
                    if (!OffLane(slots[i], c.minefieldCentre, right, offset, halfWidth)) continue;
                    if (rng.NextInt(remaining) < need)
                    {
                        activeSlotsInto.Add(i);
                        need--;
                    }
                    remaining--;
                }
            }

            return new WorldVariation { Seed = runSeed, DustStorm = storm, OpenShortcutMask = mask, SafeLane = lane };
        }

        private static bool OffLane(Vector2 slot, Vector2 centre, Vector2 right, float offset, float halfWidth)
        {
            float lateral = Vector2.Dot(slot - centre, right) - offset;
            return Mathf.Abs(lateral) > halfWidth;
        }

        private static int InsideCount(int n)
        {
            // Cell centres of an n x n grid over [-1, 1]² that lie in the unit circle.
            int inside = 0;
            float cell = 2f / n;
            for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                float px = -1f + (x + 0.5f) * cell, pz = -1f + (z + 0.5f) * cell;
                if (px * px + pz * pz <= 1f) inside++;
            }
            return inside;
        }
    }
}
