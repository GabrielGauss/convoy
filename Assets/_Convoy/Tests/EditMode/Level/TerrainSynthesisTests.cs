using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Convoy.Level.Tests
{
    /// <summary>
    /// TR-01a acceptance criteria 1-6. Fast tests use a synthetic 500 m / 257² layout (same 1.95 m spacing as the real
    /// map); [Category("Slow")] tests build ParamoLayoutDefaults at 1025² once and share it.
    /// </summary>
    public class TerrainSynthesisTests
    {
        private const float SlopeToleranceDeg = 0.5f;
        private const float MaxCrossSlopeDeg = 6f;

        // ---------------------------------------------------------------- synthetic layout

        private static readonly Vector2 SynthStart = new Vector2(0f, -200f);
        private static readonly Vector2 SynthCrater = new Vector2(0f, 40f);

        private static TerrainLayoutData Synthetic(int seed = 3, float riverDepth = 4f)
        {
            var corner = new Vector2(120f, -150f);
            var branch = new Vector2(114f, -124f); // on the east route's second leg
            var chasmEnd = new Vector2(150f, -50f);
            Vector2 chasmDir = (chasmEnd - branch).normalized;
            return new TerrainLayoutData
            {
                seed = seed,
                sizeMetres = 500f,
                heightScale = 150f,
                baseHeight = 60f,
                heightmapResolution = 257,
                alphamapResolution = 128,
                noise = new[]
                {
                    new NoiseRegion { wavelength = 150f, amplitude = 3f, octaves = 4 },
                    new NoiseRegion { centre = new Vector2(150f, -100f), radius = 80f, falloff = 40f, wavelength = 60f, amplitude = 10f, octaves = 3, ridged = true },
                },
                edgeBand = 30f,
                edgeRise = 30f,
                crater = new CraterDef
                {
                    centre = SynthCrater, rimRadius = 85f, rimHeight = 15f, floorRadius = 50f, floorDepth = 10f,
                    outerRadius = 140f, plateauRadius = 20f, plateauHeight = 2f,
                },
                river = new RiverDef
                {
                    nodes = new[] { new Vector2(-250f, -60f), new Vector2(-80f, -90f), new Vector2(100f, -120f) },
                    width = 16f, depth = riverDepth, shoulder = 8f,
                },
                chasm = new ChasmDef
                {
                    centre = (branch + chasmEnd) * 0.5f, yawDeg = Mathf.Atan2(chasmDir.x, chasmDir.y) * Mathf.Rad2Deg,
                    gapWidth = 12f, length = 14f, depth = 15f, rampAngleDeg = 12f, landingDrop = 1.5f,
                },
                routes = new[]
                {
                    new RouteDef
                    {
                        id = "west", kind = RouteKind.Main, width = 12f, shoulder = 10f,
                        nodes = new[] { SynthStart, new Vector2(-40f, -120f), new Vector2(-20f, -40f), SynthCrater },
                    },
                    new RouteDef
                    {
                        id = "east", kind = RouteKind.Main, width = 12f, shoulder = 10f, carveDepth = 4f,
                        nodes = new[] { SynthStart, corner, new Vector2(90f, -20f), SynthCrater },
                    },
                    new RouteDef
                    {
                        id = "chasm", kind = RouteKind.Shortcut, shortcutId = "chasm", width = 12f, shoulder = 10f,
                        nodes = new[] { branch, chasmEnd },
                    },
                    new RouteDef
                    {
                        id = "rival", kind = RouteKind.Rival, width = 10f, shoulder = 10f,
                        nodes = new[] { new Vector2(-200f, 150f), new Vector2(-60f, 80f), SynthCrater },
                    },
                },
                spawns = new[]
                {
                    new SpawnDef { id = "player", position = SynthStart, padRadius = 12f },
                    new SpawnDef { id = "rival", position = new Vector2(-200f, 150f), yawDeg = 90f, rival = true, padRadius = 12f },
                },
                maxRouteSlopeDeg = 18f,
                routeSampleSpacing = 2f,
                maxJunctionSlopeDeg = 2f,
            };
        }

        private static HeightField _synthetic;
        private static HeightField SyntheticBuild => _synthetic ?? (_synthetic = TerrainSynthesis.Build(Synthetic()));

        private static HeightField _defaults;
        private static TerrainLayoutData _defaultsLayout;
        private static TerrainLayoutData DefaultsLayout => _defaultsLayout ?? (_defaultsLayout = ParamoLayoutDefaults.Create());
        private static HeightField DefaultsBuild => _defaults ?? (_defaults = TerrainSynthesis.Build(DefaultsLayout));

        // ---------------------------------------------------------------- AC 1: determinism

        [Test]
        public void Rng_FirstValues_ArePinned()
        {
            // Pinned so a "harmless" refactor of the generator can never silently reshape every saved seed.
            var rng = new DeterministicRng(7);
            Assert.AreEqual(PinnedRng7[0], rng.NextUInt());
            Assert.AreEqual(PinnedRng7[1], rng.NextUInt());
            Assert.AreEqual(PinnedRng7[2], rng.NextUInt());
        }

        // Reference SplitMix32 (seed 7), computed independently of this C# implementation.
        private static readonly uint[] PinnedRng7 = { 588686121u, 1937383562u, 4286812467u };

        [Test]
        public void Rng_Float01_StaysInUnitInterval()
        {
            var rng = new DeterministicRng(123);
            for (int i = 0; i < 10000; i++)
            {
                float f = rng.NextFloat01();
                Assert.That(f >= 0f && f < 1f, "NextFloat01 out of [0,1): " + f);
                int n = rng.NextInt(5);
                Assert.That(n >= 0 && n < 5, "NextInt out of range: " + n);
            }
        }

        [Test]
        public void Build_SameData_IsBitIdentical()
        {
            float[] a = TerrainSynthesis.Build(Synthetic()).Heights;
            float[] b = TerrainSynthesis.Build(Synthetic()).Heights;
            Assert.AreEqual(a.Length, b.Length);
            for (int i = 0; i < a.Length; i++)
                if (BitConverter.SingleToInt32Bits(a[i]) != BitConverter.SingleToInt32Bits(b[i]))
                    Assert.Fail("Cell " + i + " differs: " + a[i] + " vs " + b[i]);
        }

        [Test]
        public void Build_SeedPlusOne_ChangesAtLeastFivePercentOfCells()
        {
            float[] a = TerrainSynthesis.Build(Synthetic(seed: 3)).Heights;
            float[] b = TerrainSynthesis.Build(Synthetic(seed: 4)).Heights;
            int changed = 0;
            for (int i = 0; i < a.Length; i++)
                if (Mathf.Abs(a[i] - b[i]) > 0.01f) changed++;
            Assert.That(changed / (float)a.Length, Is.GreaterThanOrEqualTo(0.05f));
        }

        // ---------------------------------------------------------------- AC 2: crater

        [Test]
        public void Crater_Centre_IsOnThePlateau() => AssertCraterCentre(SyntheticBuild, Synthetic());

        [Test]
        public void Crater_RimIsTheHighestPointOnEveryClearRay() => AssertCraterRim(SyntheticBuild, Synthetic());

        [Test]
        public void Crater_Floor_IsFlat() => AssertCraterFloor(SyntheticBuild, Synthetic());

        // ---------------------------------------------------------------- AC 3: routes

        [Test]
        public void Routes_AreDrivable() => AssertRoutesDrivable(SyntheticBuild, Synthetic());

        // ---------------------------------------------------------------- AC 4: chasm

        [Test]
        public void Chasm_GapSpanAndDepth_MatchData() => AssertChasm(SyntheticBuild, Synthetic(), "chasm");

        [Test]
        public void ChasmJump_KnownValue()
        {
            Assert.That(ChasmJump.MinClearSpeed(24f, 12f, 2f), Is.EqualTo(20.4f).Within(0.1f));
        }

        [Test]
        public void ChasmJump_HigherLanding_NeedsMoreSpeed()
        {
            Assert.That(ChasmJump.MinClearSpeed(24f, 12f, 0f), Is.GreaterThan(ChasmJump.MinClearSpeed(24f, 12f, 2f)));
            Assert.That(float.IsPositiveInfinity(ChasmJump.MinClearSpeed(24f, 0f, 0f)), "Flat take-off onto a level far side can never clear");
        }

        [Test]
        public void ChasmJump_Defaults_AreClearableByTheJackal()
        {
            // Jackal top speed 40 m/s: needing more than 80 % of it leaves no margin; under 15 m/s it is not a jump.
            ChasmDef c = DefaultsLayout.chasm;
            float v = ChasmJump.MinClearSpeed(c.gapWidth, c.rampAngleDeg, c.landingDrop);
            Assert.That(v, Is.InRange(15f, 32f));
        }

        // ---------------------------------------------------------------- AC 5: river, edge, pads

        [Test]
        public void River_Bed_IsDepthBelowTheBank()
        {
            TerrainLayoutData layout = Synthetic();
            HeightField without = TerrainSynthesis.Build(Synthetic(riverDepth: 0f));
            AssertRiverDepth(SyntheticBuild, without, layout);
        }

        [Test]
        public void EdgeBand_IsTooSteepToDrive() => AssertEdgeBand(SyntheticBuild, Synthetic());

        [Test]
        public void SpawnPads_AreFlat() => AssertSpawnPads(SyntheticBuild, Synthetic());

        // ---------------------------------------------------------------- AC 6: splat, sampler

        [Test]
        public void Splat_SumsToOne_AndToxicDominatesTheRiverBed() => AssertSplat(SyntheticBuild, Synthetic(), 128);

        [Test]
        public void Splat_TooSmallBuffer_Throws()
        {
            Assert.Throws<ArgumentException>(() => TerrainSynthesis.ComputeSplat(SyntheticBuild, Synthetic(), 8, new float[10]));
        }

        [Test]
        public void SampleRoute_SpacingEndsAndHeights() => AssertSampleRoute(SyntheticBuild, Synthetic());

        [Test]
        public void HeightField_Sample_IsBilinearAndClamped()
        {
            var h = new HeightField(3, 10f, 100f); // cells at -5, 0, 5
            h.Heights[0] = 0f;
            h.Heights[1] = 10f;
            h.Heights[3] = 20f;
            h.Heights[4] = 30f;
            Assert.That(h.Spacing, Is.EqualTo(5f).Within(1e-6f));
            Assert.That(h.Sample(-2.5f, -2.5f), Is.EqualTo(15f).Within(1e-4f));
            Assert.That(h.Sample(-50f, -50f), Is.EqualTo(0f).Within(1e-4f));
        }

        // ---------------------------------------------------------------- defaults data

        [Test]
        public void Defaults_MainRoutes_RunFromThePlayerPadToTheMonolith()
        {
            TerrainLayoutData d = DefaultsLayout;
            SpawnDef player = Array.Find(d.spawns, s => !s.rival);
            int mains = 0;
            foreach (RouteDef r in d.routes)
            {
                if (r.kind != RouteKind.Main) continue;
                mains++;
                Assert.That((r.nodes[0] - player.position).magnitude, Is.LessThanOrEqualTo(player.padRadius), r.id);
                Assert.That((r.nodes[r.nodes.Length - 1] - d.crater.centre).magnitude, Is.LessThanOrEqualTo(d.crater.plateauRadius), r.id);
            }
            Assert.AreEqual(3, mains);
            Assert.That(Array.Exists(d.routes, r => r.kind == RouteKind.Shortcut && r.shortcutId == "chasm"));
            Assert.That(Array.Exists(d.routes, r => r.kind == RouteKind.Shortcut && r.shortcutId == "rammer"));
            Assert.That(Array.Exists(d.routes, r => r.shortcutId == "cave" && r.coveredRange.y > r.coveredRange.x));
            Assert.AreEqual(3, Array.FindAll(d.routes, r => r.kind == RouteKind.Rival).Length);
        }

        // ---------------------------------------------------------------- slow: the real 2 km map

        [Test, Category("Slow")]
        public void Slow_Defaults_Deterministic()
        {
            float[] a = DefaultsBuild.Heights;
            float[] b = TerrainSynthesis.Build(ParamoLayoutDefaults.Create()).Heights;
            for (int i = 0; i < a.Length; i++)
                if (BitConverter.SingleToInt32Bits(a[i]) != BitConverter.SingleToInt32Bits(b[i]))
                    Assert.Fail("Cell " + i + " differs");
        }

        [Test, Category("Slow")]
        public void Slow_Defaults_Crater()
        {
            AssertCraterCentre(DefaultsBuild, DefaultsLayout);
            AssertCraterRim(DefaultsBuild, DefaultsLayout);
            AssertCraterFloor(DefaultsBuild, DefaultsLayout);
        }

        [Test, Category("Slow")]
        public void Slow_Defaults_RoutesAreDrivable() => AssertRoutesDrivable(DefaultsBuild, DefaultsLayout);

        [Test, Category("Slow")]
        public void Slow_Defaults_Chasm() => AssertChasm(DefaultsBuild, DefaultsLayout, ParamoLayoutDefaults.ShortcutChasm);

        [Test, Category("Slow")]
        public void Slow_Defaults_RiverEdgeAndPads()
        {
            TerrainLayoutData noRiver = ParamoLayoutDefaults.Create();
            noRiver.river.depth = 0f;
            AssertRiverDepth(DefaultsBuild, TerrainSynthesis.Build(noRiver), DefaultsLayout);
            AssertEdgeBand(DefaultsBuild, DefaultsLayout);
            AssertSpawnPads(DefaultsBuild, DefaultsLayout);
        }

        [Test, Category("Slow")]
        public void Slow_Defaults_SplatAndSampler()
        {
            AssertSplat(DefaultsBuild, DefaultsLayout, DefaultsLayout.alphamapResolution);
            AssertSampleRoute(DefaultsBuild, DefaultsLayout);
        }

        // ================================================================ shared checks

        private static void AssertCraterCentre(HeightField h, TerrainLayoutData l)
        {
            CraterDef c = l.crater;
            float expected = l.baseHeight - c.floorDepth + c.plateauHeight;
            Assert.That(h.Sample(c.centre.x, c.centre.y), Is.EqualTo(expected).Within(0.5f));
        }

        private static void AssertCraterRim(HeightField h, TerrainLayoutData l)
        {
            CraterDef c = l.crater;
            float step = h.Spacing * 0.5f;
            // Searched out to the middle of the outer apron: beyond it the crater has mostly handed back to the noise
            // terrain, and a canyon ridge there may legitimately stand taller than the rim.
            float searchRadius = 0.5f * (c.rimRadius + c.outerRadius);
            int tested = 0;
            for (int k = 0; k < 32; k++)
            {
                Vector2 dir = Dir(k * 360f / 32f);
                if (RayNearRoute(l, c.centre, dir, c.floorRadius, searchRadius, h.Spacing)) continue;
                tested++;
                float bestR = 0f, bestH = float.NegativeInfinity;
                for (float r = 0f; r <= searchRadius; r += step)
                {
                    Vector2 p = c.centre + dir * r;
                    float y = h.Sample(p.x, p.y);
                    if (y > bestH)
                    {
                        bestH = y;
                        bestR = r;
                    }
                }
                Assert.That(bestR, Is.EqualTo(c.rimRadius).Within(2f * h.Spacing), "ray " + k + " max position");
                Assert.That(bestH, Is.GreaterThanOrEqualTo(l.baseHeight + c.rimHeight - 2f), "ray " + k + " rim height");
            }
            Assert.That(tested, Is.GreaterThanOrEqualTo(8), "too few rays clear of routes to judge the rim");
        }

        private static void AssertCraterFloor(HeightField h, TerrainLayoutData l)
        {
            CraterDef c = l.crater;
            float r0 = c.plateauRadius + 5f, r1 = 0.8f * c.floorRadius;
            int tested = 0;
            for (int k = 0; k < 32; k++)
            {
                Vector2 dir = Dir(k * 360f / 32f);
                if (RayNearRoute(l, c.centre, dir, r0, r1, h.Spacing)) continue;
                tested++;
                for (float r = r0 + 0.01f; r < r1; r += h.Spacing * 0.5f)
                {
                    Vector2 p = c.centre + dir * r;
                    Assert.That(h.SlopeDeg(p.x, p.y), Is.LessThanOrEqualTo(3f), "floor ray " + k + " r " + r);
                }
            }
            Assert.That(tested, Is.GreaterThanOrEqualTo(4), "too few rays clear of routes to judge the floor");
        }

        private static void AssertRoutesDrivable(HeightField h, TerrainLayoutData l)
        {
            var pts = new List<Vector3>();
            float maxSlope = l.maxRouteSlopeDeg + SlopeToleranceDeg;
            foreach (RouteDef r in l.routes)
            {
                TerrainSynthesis.SampleRoute(h, r, l.routeSampleSpacing, pts);
                Assert.That(pts.Count, Is.GreaterThan(2), r.id);
                float worstLong = 0f, worstCross = 0f;
                string whereLong = "", whereCross = "";
                float lateral = r.width * 0.5f - 1f;
                for (int i = 0; i < pts.Count; i++)
                {
                    Vector3 p = pts[i];
                    if (InChasm(l, h, p)) continue;
                    bool openEnd = NearOpenEnd(l, r, new Vector2(p.x, p.z), h.Spacing * 1.5f);
                    if (i > 0 && !InChasm(l, h, pts[i - 1]))
                    {
                        Vector3 q = pts[i - 1];
                        float run = new Vector2(p.x - q.x, p.z - q.z).magnitude;
                        float slope = Mathf.Atan(Mathf.Abs(p.y - q.y) / run) * Mathf.Rad2Deg;
                        if (slope > worstLong)
                        {
                            worstLong = slope;
                            whereLong = p.ToString();
                        }
                    }
                    // A route end that is not on a pad or the plateau is square (a branch start or a dead end): the
                    // bilinear footprint of the last cross-section reaches past it, off the road, so it is not judged.
                    if (openEnd) continue;
                    Vector3 a = pts[Mathf.Max(0, i - 1)], b = pts[Mathf.Min(pts.Count - 1, i + 1)];
                    Vector2 t = new Vector2(b.x - a.x, b.z - a.z).normalized;
                    var n = new Vector2(t.y, -t.x);
                    float prev = h.Sample(p.x - n.x * lateral, p.z - n.y * lateral);
                    for (float o = -lateral + 1f; o <= lateral + 1e-3f; o += 1f)
                    {
                        float y = h.Sample(p.x + n.x * o, p.z + n.y * o);
                        float cross = Mathf.Atan(Mathf.Abs(y - prev)) * Mathf.Rad2Deg;
                        prev = y;
                        if (cross > worstCross)
                        {
                            worstCross = cross;
                            whereCross = p.ToString();
                        }
                    }
                }
                Assert.That(worstLong, Is.LessThanOrEqualTo(maxSlope), r.id + " longitudinal slope at " + whereLong);
                Assert.That(worstCross, Is.LessThanOrEqualTo(MaxCrossSlopeDeg), r.id + " cross slope at " + whereCross);

                if (r.kind != RouteKind.Main) continue;
                bool fromPad = Array.Exists(l.spawns, s => (r.nodes[0] - s.position).magnitude <= s.padRadius);
                Assert.That(fromPad, r.id + " does not start on a spawn pad");
                Vector3 end = pts[pts.Count - 1];
                Assert.That((new Vector2(end.x, end.z) - l.crater.centre).magnitude, Is.LessThanOrEqualTo(l.crater.plateauRadius), r.id + " does not reach the plateau");
                Assert.That(end.y, Is.EqualTo(l.baseHeight - l.crater.floorDepth + l.crater.plateauHeight).Within(0.5f), r.id + " ends off the plateau");
            }
        }

        private static void AssertChasm(HeightField h, TerrainLayoutData l, string shortcutId)
        {
            ChasmDef c = l.chasm;
            RouteDef route = Array.Find(l.routes, r => r.kind == RouteKind.Shortcut && r.shortcutId == shortcutId);
            Assert.That(route.nodes, Is.Not.Null, "no shortcut " + shortcutId);
            var pts = new List<Vector3>();
            const float fine = 0.25f;
            TerrainSynthesis.SampleRoute(h, route, fine, pts);

            Vector2 fwd = Dir(c.yawDeg);
            Vector2 lipPoint = c.centre - fwd * (c.gapWidth * 0.5f + 4f);
            float lip = h.Sample(lipPoint.x, lipPoint.y);
            float threshold = lip - c.depth * 0.5f;
            int first = -1, last = -1;
            float lowest = float.PositiveInfinity;
            for (int i = 0; i < pts.Count; i++)
            {
                // Only around the chasm: elsewhere the shortcut may legitimately dip that low (river, valleys).
                if (!InChasmBox(l, new Vector2(pts[i].x, pts[i].z), 10f)) continue;
                if (pts[i].y >= threshold) continue;
                if (first < 0) first = i;
                last = i;
                lowest = Mathf.Min(lowest, pts[i].y);
            }
            Assert.That(first, Is.GreaterThanOrEqualTo(0), "no gap found along " + shortcutId);
            for (int i = first; i <= last; i++)
                Assert.That(pts[i].y, Is.LessThan(threshold), "gap is not one continuous span at " + pts[i]);
            float span = (last - first + 1) * fine;
            Assert.That(span, Is.EqualTo(c.gapWidth).Within(2f), "gap length");
            Assert.That(lip - lowest, Is.GreaterThanOrEqualTo(c.depth - 1f), "gap depth");
        }

        private static void AssertRiverDepth(HeightField with, HeightField without, TerrainLayoutData l)
        {
            RiverDef river = l.river;
            float grade = Mathf.Tan(l.maxRouteSlopeDeg * Mathf.Deg2Rad);
            int tested = 0;
            for (int k = 0; k < river.nodes.Length - 1; k++)
            {
                Vector2 a = river.nodes[k], b = river.nodes[k + 1];
                float len = (b - a).magnitude;
                for (float s = 0f; s <= len; s += 4f)
                {
                    Vector2 p = a + (b - a) * (s / len);
                    // Away from fords (where a route bed replaces the channel) and from map features that also reshape it.
                    if (NearAnyRoute(l, p, river.depth / grade + 2f * with.Spacing)) continue;
                    if ((p - l.crater.centre).magnitude < l.crater.outerRadius) continue;
                    if (Mathf.Abs(p.x) > 0.5f * l.sizeMetres - with.Spacing || Mathf.Abs(p.y) > 0.5f * l.sizeMetres - with.Spacing) continue;
                    if (InChasmBox(l, p, 10f)) continue;
                    tested++;
                    float bank = without.Sample(p.x, p.y);
                    float bed = with.Sample(p.x, p.y);
                    Assert.That(bank - bed, Is.EqualTo(river.depth).Within(0.5f), "river at " + p);
                }
            }
            Assert.That(tested, Is.GreaterThan(10));
        }

        private static void AssertEdgeBand(HeightField h, TerrainLayoutData l)
        {
            float half = 0.5f * l.sizeMetres;
            int tested = 0;
            for (int side = 0; side < 4; side++)
            {
                for (float u = -half + l.edgeBand; u <= half - l.edgeBand; u += 10f)
                {
                    // Ray from the border inward across the band.
                    Vector2 edge, inward;
                    switch (side)
                    {
                        case 0: edge = new Vector2(u, -half); inward = Vector2.up; break;
                        case 1: edge = new Vector2(u, half); inward = Vector2.down; break;
                        case 2: edge = new Vector2(-half, u); inward = Vector2.right; break;
                        default: edge = new Vector2(half, u); inward = Vector2.left; break;
                    }
                    if (RayNearSpawn(l, edge, inward, l.edgeBand)) continue;
                    tested++;
                    float max = 0f;
                    for (float d = h.Spacing; d < l.edgeBand; d += h.Spacing * 0.5f)
                    {
                        Vector2 p = edge + inward * d;
                        max = Mathf.Max(max, h.SlopeDeg(p.x, p.y));
                    }
                    Assert.That(max, Is.GreaterThanOrEqualTo(30f), "edge band at " + edge);
                }
            }
            Assert.That(tested, Is.GreaterThan(0));
        }

        private static void AssertSpawnPads(HeightField h, TerrainLayoutData l)
        {
            foreach (SpawnDef s in l.spawns)
            {
                float r = s.padRadius - h.Spacing;
                for (float x = -r; x <= r; x += h.Spacing * 0.5f)
                for (float z = -r; z <= r; z += h.Spacing * 0.5f)
                {
                    if (x * x + z * z > r * r) continue;
                    Assert.That(h.SlopeDeg(s.position.x + x, s.position.y + z), Is.LessThanOrEqualTo(3f), s.id + " pad");
                }
            }
        }

        private static void AssertSplat(HeightField h, TerrainLayoutData l, int res)
        {
            var w = new float[res * res * 4];
            TerrainSynthesis.ComputeSplat(h, l, res, w);
            float texel = l.sizeMetres / res;
            int riverTexels = 0;
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    int o = (z * res + x) * 4;
                    float sum = w[o] + w[o + 1] + w[o + 2] + w[o + 3];
                    Assert.That(sum, Is.EqualTo(1f).Within(1e-4f), "texel " + x + "," + z);
                    for (int k = 0; k < 4; k++) Assert.That(w[o + k], Is.GreaterThanOrEqualTo(0f));

                    var p = new Vector2(-0.5f * l.sizeMetres + (x + 0.5f) * texel, -0.5f * l.sizeMetres + (z + 0.5f) * texel);
                    if (DistanceToPolyline(l.river.nodes, p) > l.river.width * 0.5f - texel) continue;
                    riverTexels++;
                    Assert.That(w[o + 3], Is.GreaterThan(Mathf.Max(w[o], Mathf.Max(w[o + 1], w[o + 2]))), "toxic not dominant at " + p);
                }
            }
            Assert.That(riverTexels, Is.GreaterThan(0));
        }

        private static void AssertSampleRoute(HeightField h, TerrainLayoutData l)
        {
            var pts = new List<Vector3>();
            float spacing = l.routeSampleSpacing;
            foreach (RouteDef r in l.routes)
            {
                int n = TerrainSynthesis.SampleRoute(h, r, spacing, pts);
                Assert.AreEqual(pts.Count, n);
                Vector2 first = new Vector2(pts[0].x, pts[0].z), last = new Vector2(pts[n - 1].x, pts[n - 1].z);
                Assert.That((first - r.nodes[0]).magnitude, Is.LessThan(1e-3f), r.id + " first");
                Assert.That((last - r.nodes[r.nodes.Length - 1]).magnitude, Is.LessThan(1e-3f), r.id + " last");
                for (int i = 1; i < n; i++)
                {
                    float d = new Vector2(pts[i].x - pts[i - 1].x, pts[i].z - pts[i - 1].z).magnitude;
                    if (i < n - 1) Assert.That(d, Is.EqualTo(spacing).Within(0.01f), r.id + " spacing at " + i);
                    else Assert.That(d, Is.LessThanOrEqualTo(spacing + 0.01f), r.id + " last interval");
                    Assert.That(pts[i].y, Is.EqualTo(h.Sample(pts[i].x, pts[i].z)).Within(0.05f));
                }
            }
        }

        // ---------------------------------------------------------------- geometry helpers

        private static Vector2 Dir(float yawDeg)
        {
            float a = yawDeg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(a), Mathf.Cos(a));
        }

        private static float RouteClearance(RouteDef r, float spacing) => r.width * 0.5f + r.shoulder + 3f * spacing;

        private static bool NearAnyRoute(TerrainLayoutData l, Vector2 p, float extra)
        {
            foreach (RouteDef r in l.routes)
                if (DistanceToPolyline(r.nodes, p) < r.width * 0.5f + r.shoulder + extra) return true;
            return false;
        }

        private static bool RayNearRoute(TerrainLayoutData l, Vector2 origin, Vector2 dir, float r0, float r1, float spacing)
        {
            for (float r = r0; r <= r1; r += 2f)
            {
                Vector2 p = origin + dir * r;
                foreach (RouteDef route in l.routes)
                    if (DistanceToPolyline(route.nodes, p) < RouteClearance(route, spacing)) return true;
            }
            return false;
        }

        private static bool RayNearSpawn(TerrainLayoutData l, Vector2 origin, Vector2 dir, float length)
        {
            for (float d = 0f; d <= length; d += 2f)
            {
                Vector2 p = origin + dir * d;
                foreach (SpawnDef s in l.spawns)
                    if ((p - s.position).magnitude < s.padRadius) return true;
            }
            return false;
        }

        private static bool NearOpenEnd(TerrainLayoutData l, RouteDef r, Vector2 p, float within)
        {
            Vector2 first = r.nodes[0], last = r.nodes[r.nodes.Length - 1];
            return ((p - first).magnitude <= within && !OnHub(l, first)) || ((p - last).magnitude <= within && !OnHub(l, last));
        }

        private static bool OnHub(TerrainLayoutData l, Vector2 p)
        {
            if ((p - l.crater.centre).magnitude <= l.crater.plateauRadius) return true;
            return Array.Exists(l.spawns, s => (p - s.position).magnitude <= s.padRadius);
        }

        private static bool InChasm(TerrainLayoutData l, HeightField h, Vector3 p) => InChasmBox(l, new Vector2(p.x, p.z), 2f * h.Spacing);

        private static bool InChasmBox(TerrainLayoutData l, Vector2 p, float margin)
        {
            ChasmDef c = l.chasm;
            if (c.gapWidth <= 0f) return false;
            Vector2 fwd = Dir(c.yawDeg);
            var right = new Vector2(fwd.y, -fwd.x);
            Vector2 rel = p - c.centre;
            return Mathf.Abs(Vector2.Dot(rel, fwd)) <= c.gapWidth * 0.5f + margin
                && Mathf.Abs(Vector2.Dot(rel, right)) <= c.length * 0.5f + margin;
        }

        private static float DistanceToPolyline(Vector2[] pts, Vector2 q)
        {
            float best = float.PositiveInfinity;
            for (int k = 0; k < pts.Length - 1; k++)
            {
                Vector2 a = pts[k], ab = pts[k + 1] - pts[k];
                float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, (q - (a + ab * t)).magnitude);
            }
            return best;
        }
    }
}
