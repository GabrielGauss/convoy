using System;
using System.Collections.Generic;
using UnityEngine;

namespace Convoy.Level
{
    /// <summary>
    /// Pure, deterministic terrain synthesis for open maps (TR-01a). No Terrain/TerrainData/scene API here: the editor
    /// generator (TR-01b) copies <see cref="HeightField.Heights"/> and the splat weights into Unity. Editor-time only
    /// (never at runtime in M1), so it may allocate a few full-grid scratch arrays per Build; nothing here runs per frame.
    /// </summary>
    public static class TerrainSynthesis
    {
        // Corner arcs are drawn as chords turning at most this much each. Why: a point beside the road projects onto
        // a slightly different arclength on each side of a chord joint; at 2 degrees the jump is < 3.5 % of the
        // lateral distance, so the bed stays smooth without exact arc maths.
        private const float ArcStepRad = 2f * Mathf.Deg2Rad;

        // Each noise layer is shifted by up to +/- half this many metres so layers (and seeds) sample unrelated
        // patches of noise. Kept moderate so the highest octave still has full float precision.
        private const float NoiseOffsetRange = 4096f;

        private const float Sqrt2 = 1.41421356f;

        // Blend weight of a bed at its own flat edge (1 on the centreline). Non-zero only so a lone bed's weight
        // never divides by zero; small so another bed overlapping there dominates.
        private const float MinBedWeight = 1e-3f;

        /// <summary>
        /// Builds the height field. Order: noise -> edge berm -> crater -> river -> routes (grade-limited profile,
        /// bed + shoulder blend) -> chasm cut -> spawn pads -> clamp [0, heightScale]. Same data = bit-identical output.
        /// </summary>
        public static HeightField Build(TerrainLayoutData layout)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            var h = new HeightField(layout.heightmapResolution, layout.sizeMetres, layout.heightScale);
            ApplyNoise(h, layout);
            ApplyEdgeBerm(h, layout.edgeBand, layout.edgeRise);
            ApplyCrater(h, layout.crater, layout.baseHeight);
            ApplyRiver(h, layout.river);
            // Pad heights are fixed before routes so routes leaving a pad start exactly at its level.
            float[] padHeights = PadHeights(h, layout.spawns);
            ApplyRoutes(h, layout, padHeights);
            ApplyChasm(h, layout);
            ApplySpawnPads(h, layout, padHeights);
            Clamp(h);
            return h;
        }

        /// <summary>
        /// Splat weights for <paramref name="alphaRes"/>² texels x 4 layers (0 sand, 1 basalt, 2 rust, 3 toxic), row-major
        /// <c>[(z * alphaRes + x) * 4 + layer]</c> = Unity's alphamap [z, x, layer] order. Each texel is sampled at its centre
        /// and its weights sum to 1. Toxic = river bed; basalt = steep ground and the volcanic crater; rust = canyon
        /// (ridged-noise) country; sand = everything else.
        /// </summary>
        public static void ComputeSplat(HeightField h, TerrainLayoutData layout, int alphaRes, float[] into)
        {
            if (h == null) throw new ArgumentNullException(nameof(h));
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (alphaRes < 1) throw new ArgumentOutOfRangeException(nameof(alphaRes));
            if (into == null || into.Length < alphaRes * alphaRes * 4)
                throw new ArgumentException("Needs alphaRes * alphaRes * 4 floats.", nameof(into));

            float texel = h.SizeMetres / alphaRes;
            float origin = h.Origin;
            CraterDef crater = layout.crater;
            RiverDef river = layout.river;
            bool hasRiver = river.nodes != null && river.nodes.Length >= 2 && river.width > 0f;
            NoiseRegion[] regions = layout.noise;
            float slopeMin = layout.basaltSlopeMinDeg;
            float slopeMax = Mathf.Max(layout.basaltSlopeMaxDeg, slopeMin + 0.001f);

            for (int z = 0; z < alphaRes; z++)
            {
                float wz = origin + (z + 0.5f) * texel;
                for (int x = 0; x < alphaRes; x++)
                {
                    float wx = origin + (x + 0.5f) * texel;

                    float rock = Smooth01((h.SlopeDeg(wx, wz) - slopeMin) / (slopeMax - slopeMin));
                    float basalt = Mathf.Max(rock, CraterBlend(crater, wx, wz));

                    float ridge = 0f;
                    if (regions != null)
                    {
                        for (int r = 0; r < regions.Length; r++)
                            if (regions[r].ridged) ridge = Mathf.Max(ridge, RegionWeight(regions[r], wx, wz));
                    }

                    float toxic = 0f;
                    if (hasRiver)
                    {
                        float d = PolylineDistance(river.nodes, new Vector2(wx, wz));
                        toxic = ChannelWeight(d, river.width * 0.5f, river.shoulder);
                    }

                    float rest = 1f - toxic;
                    int o = (z * alphaRes + x) * 4;
                    into[o] = rest * (1f - basalt) * (1f - ridge);
                    into[o + 1] = rest * basalt;
                    into[o + 2] = rest * (1f - basalt) * ridge;
                    into[o + 3] = toxic;
                }
            }
        }

        /// <summary>
        /// Samples the route centreline (corners rounded exactly as Build carves them) every <paramref name="spacing"/>
        /// metres of arclength, y = carved ground height. First/last points are the route ends; only the final interval
        /// may be shorter than <paramref name="spacing"/>. Clears <paramref name="into"/>; returns the point count.
        /// </summary>
        public static int SampleRoute(HeightField h, RouteDef route, float spacing, List<Vector3> into)
        {
            if (h == null) throw new ArgumentNullException(nameof(h));
            if (into == null) throw new ArgumentNullException(nameof(into));
            if (spacing <= 0f) throw new ArgumentOutOfRangeException(nameof(spacing));
            into.Clear();
            if (route.nodes == null || route.nodes.Length < 2) return 0;

            RoutePath path = RoutePath.Build(route.nodes, CornerRadius(route));
            float length = path.Length;
            // Integer stepping (not s += spacing) so float error never accumulates along a 1 km route.
            for (int i = 0; ; i++)
            {
                float s = i * spacing;
                if (s >= length - 1e-3f) break;
                Vector2 p = path.PointAt(s);
                into.Add(new Vector3(p.x, h.Sample(p.x, p.y), p.y));
            }
            Vector2 end = path.PointAt(length);
            into.Add(new Vector3(end.x, h.Sample(end.x, end.y), end.y));
            return into.Count;
        }

        // ---------------------------------------------------------------- stages

        private static void ApplyNoise(HeightField h, TerrainLayoutData layout)
        {
            float[] heights = h.Heights;
            for (int i = 0; i < heights.Length; i++) heights[i] = layout.baseHeight;

            NoiseRegion[] regions = layout.noise;
            if (regions == null) return;
            int res = h.Resolution;
            var rng = new DeterministicRng(layout.seed);
            for (int r = 0; r < regions.Length; r++)
            {
                NoiseRegion region = regions[r];
                // Draw before any skip so one disabled layer never reshuffles the others.
                float offX = (rng.NextFloat01() - 0.5f) * NoiseOffsetRange;
                float offZ = (rng.NextFloat01() - 0.5f) * NoiseOffsetRange;
                uint salt = rng.NextUInt();
                if (region.wavelength <= 0f || region.amplitude == 0f || region.octaves < 1) continue;

                int x0 = 0, x1 = res - 1, z0 = 0, z1 = res - 1;
                if (region.radius > 0f)
                {
                    float reach = region.radius + Mathf.Max(region.falloff, 0f);
                    CellRange(h, region.centre.x - reach, region.centre.x + reach, out x0, out x1);
                    CellRange(h, region.centre.y - reach, region.centre.y + reach, out z0, out z1);
                }
                for (int z = z0; z <= z1; z++)
                {
                    float wz = h.CellToWorld(z);
                    for (int x = x0; x <= x1; x++)
                    {
                        float wx = h.CellToWorld(x);
                        float w = RegionWeight(region, wx, wz);
                        if (w <= 0f) continue;
                        float n = Fbm(wx + offX, wz + offZ, region.wavelength, region.octaves, region.ridged, salt);
                        heights[z * res + x] += w * region.amplitude * n;
                    }
                }
            }
        }

        private static void ApplyEdgeBerm(HeightField h, float band, float rise)
        {
            if (band <= 0f || rise == 0f) return;
            int res = h.Resolution;
            float[] heights = h.Heights;
            for (int z = 0; z < res; z++)
            {
                float dz = Mathf.Min(z, res - 1 - z) * h.Spacing;
                for (int x = 0; x < res; x++)
                {
                    float d = Mathf.Min(dz, Mathf.Min(x, res - 1 - x) * h.Spacing);
                    if (d >= band) continue;
                    // Smoothstep: flat where it meets the playfield, steepest (1.5 x rise / band) mid-band, flat on top.
                    heights[z * res + x] += rise * Smooth01(1f - d / band);
                }
            }
        }

        private static void ApplyCrater(HeightField h, CraterDef c, float baseHeight)
        {
            if (c.rimRadius <= 0f) return;
            float outer = Mathf.Max(c.outerRadius, c.rimRadius);
            int res = h.Resolution;
            float[] heights = h.Heights;
            CellRange(h, c.centre.x - outer, c.centre.x + outer, out int x0, out int x1);
            CellRange(h, c.centre.y - outer, c.centre.y + outer, out int z0, out int z1);
            for (int z = z0; z <= z1; z++)
            {
                float dz = h.CellToWorld(z) - c.centre.y;
                for (int x = x0; x <= x1; x++)
                {
                    float dx = h.CellToWorld(x) - c.centre.x;
                    float r = Mathf.Sqrt(dx * dx + dz * dz);
                    if (r >= outer) continue;
                    // Inside the rim the crater is absolute (noise-free) so the Monolith plateau and floor are exact;
                    // outside it the crater shape hands back to the noise terrain by outerRadius.
                    float w = CraterBlendAt(c, r);
                    int i = z * res + x;
                    heights[i] = Mathf.Lerp(heights[i], baseHeight + CraterProfile(c, r), w);
                }
            }
        }

        private static void ApplyRiver(HeightField h, RiverDef river)
        {
            if (river.nodes == null || river.nodes.Length < 2 || river.depth <= 0f || river.width <= 0f) return;
            float half = river.width * 0.5f;
            float reach = half + Mathf.Max(river.shoulder, 0f);
            int res = h.Resolution;
            float[] heights = h.Heights;
            PolylineBounds(river.nodes, reach, out Vector2 min, out Vector2 max);
            CellRange(h, min.x, max.x, out int x0, out int x1);
            CellRange(h, min.y, max.y, out int z0, out int z1);
            for (int z = z0; z <= z1; z++)
            {
                float wz = h.CellToWorld(z);
                for (int x = x0; x <= x1; x++)
                {
                    float d = PolylineDistance(river.nodes, new Vector2(h.CellToWorld(x), wz));
                    if (d >= reach) continue;
                    // Subtractive: the bed is exactly `depth` below whatever ground was there, so it follows the land.
                    heights[z * res + x] -= river.depth * ChannelWeight(d, half, river.shoulder);
                }
            }
        }

        /// <summary>
        /// Routes are profiled in priority order (Main, Shortcut, Rival). Each gets a 1-D bed profile along its
        /// centreline (corners rounded): ground minus carve depth, held near earlier routes (junction band) and pinned
        /// on hubs (spawn pads, Monolith plateau), grade-limited by a lower envelope (cut) and a fill envelope from the
        /// holds, then crest-smoothed. All beds are then written at once: flat across the bed, smoothstep shoulder back
        /// to the ground, overlapping corridors blended so shared stretches and junctions stay one level road.
        /// </summary>
        private static void ApplyRoutes(HeightField h, TerrainLayoutData layout, float[] padHeights)
        {
            RouteDef[] routes = layout.routes;
            if (routes == null || routes.Length == 0) return;

            float grade = Mathf.Tan(Mathf.Clamp(layout.maxRouteSlopeDeg, 0.1f, 89f) * Mathf.Deg2Rad);
            float junction = Mathf.Tan(Mathf.Clamp(layout.maxJunctionSlopeDeg, 0f, 89f) * Mathf.Deg2Rad);
            // Why the margin: Unity (and HeightField.Sample) interpolates bilinearly, so a point on the bed edge reads
            // grid corners up to one cell diagonal away. Keeping those corners flat too keeps the driven width flat.
            float bilinearMargin = h.Spacing * Sqrt2;
            // Half a cell: the profile is finer than the grid, so interpolating it never blurs the grade limit.
            float maxStep = h.Spacing * 0.5f;

            // Hubs = spawn pads + the Monolith plateau: every route touching one is pinned to its level, so the roads
            // that fan out of a pad or converge on the Monolith meet on one flat surface instead of ramping past each other.
            var hubs = new List<Hub>();
            if (layout.spawns != null)
            {
                for (int i = 0; i < layout.spawns.Length; i++)
                    if (layout.spawns[i].padRadius > 0f) hubs.Add(new Hub(layout.spawns[i].position, layout.spawns[i].padRadius, padHeights[i]));
            }
            CraterDef crater = layout.crater;
            if (crater.rimRadius > 0f && crater.plateauRadius > 0f)
                hubs.Add(new Hub(crater.centre, crater.plateauRadius, h.Sample(crater.centre.x, crater.centre.y)));

            var carved = new List<CarvedRoute>(routes.Length);
            for (int pass = 0; pass < 3; pass++)
            {
                var kind = (RouteKind)pass;
                for (int r = 0; r < routes.Length; r++)
                {
                    RouteDef def = routes[r];
                    if (def.kind != kind || def.nodes == null || def.nodes.Length < 2 || def.width <= 0f) continue;
                    RoutePath path = RoutePath.Build(def.nodes, CornerRadius(def));
                    if (path.Length <= 0f) continue;
                    carved.Add(Profile(h, def, path, carved, hubs, grade, junction, bilinearMargin, maxStep));
                }
            }
            if (carved.Count == 0) return;
            WriteBeds(h, carved);
        }

        private static CarvedRoute Profile(HeightField h, RouteDef def, RoutePath path, List<CarvedRoute> earlier,
            List<Hub> hubs, float grade, float junction, float bilinearMargin, float maxStep)
        {
            float length = path.Length;
            int n = Mathf.CeilToInt(length / maxStep) + 1;
            float step = length / (n - 1);
            float flatHalf = def.width * 0.5f + bilinearMargin;
            float shoulder = Mathf.Max(def.shoulder, 0f);
            // Carve fades in/out over the distance the grade needs to descend it, so route ends (spawn pads, the
            // Monolith plateau) sit on the original ground instead of at the bottom of a trench.
            float taper = def.carveDepth > 0f ? def.carveDepth / grade : 0f;
            int smoothHalf = Mathf.CeilToInt(2f * h.Spacing / step);

            var target = new float[n];
            var lower = new float[n];
            var upper = new float[n];
            for (int i = 0; i < n; i++)
            {
                float s = i * step;
                Vector2 c = path.PointAt(s);
                float carve = def.carveDepth;
                if (taper > 0f) carve *= Mathf.Min(1f, Mathf.Min(s, length - s) / taper);
                float t = h.Sample(c.x, c.y) - carve;

                // Junction band: where an earlier bed touches this one, the height difference must look like at most
                // `junction` per metre sideways and `grade` per metre lengthways from BOTH roads' point of view, so a
                // branch leaves (or a merge joins) a road level with it instead of with a side step on either bed.
                Vector2 tangent = path.TangentAt(s);
                float lo = float.NegativeInfinity, hi = float.PositiveInfinity;
                for (int e = 0; e < earlier.Count; e++)
                {
                    CarvedRoute other = earlier[e];
                    float d = other.Path.Nearest(c, out float se, out Vector2 q);
                    // Only where one bed reaches the other's corridor (bed + shoulder), i.e. where WriteBeds blends them.
                    if (d >= other.FlatHalf + flatHalf + Mathf.Max(other.Shoulder, shoulder)) continue;
                    Vector2 delta = q - c;
                    float band = Mathf.Min(Band(delta, tangent, junction, grade), Band(delta, other.Path.TangentAt(se), junction, grade));
                    float pe = other.ProfileAt(se);
                    lo = Mathf.Max(lo, pe - band);
                    hi = Mathf.Min(hi, pe + band);
                }
                if (hi < lo) lo = hi = 0.5f * (lo + hi); // two earlier routes disagree here: split the difference
                // Pinned past the hub edge by one step plus the smoothing reach, so the bed leaves the hub exactly at
                // its level (the pad stage's grade cone then never has to lift it, smoothing never dents it).
                for (int k = 0; k < hubs.Count; k++)
                {
                    float pinRadius = hubs[k].Radius + step + 2 * smoothHalf * step;
                    if ((c - hubs[k].Centre).sqrMagnitude <= pinRadius * pinRadius) lo = hi = hubs[k].Height;
                }
                target[i] = Mathf.Min(t, hi);
                lower[i] = lo;
                upper[i] = hi;
            }

            float rise = grade * step;
            CutEnvelope(target, rise);
            FillEnvelope(lower, rise);
            for (int i = 0; i < n; i++) target[i] = Mathf.Min(Mathf.Max(target[i], lower[i]), upper[i]);

            // Round off crests and dips (the envelopes leave sharp ^ / v kinks). Why: bilinear terrain sampling across
            // a kink that is not grid-aligned tilts the road sideways. Averaging keeps the grade limit (an average of
            // grade-limited samples is grade-limited); two box passes = a triangle kernel spanning +/- 4 cells.
            BoxSmooth(target, smoothHalf, lower); // `lower` is no longer needed: reused as scratch
            BoxSmooth(target, smoothHalf, lower);

            return new CarvedRoute(path, target, step, flatHalf, shoulder);
        }

        private static void WriteBeds(HeightField h, List<CarvedRoute> carved)
        {
            int res = h.Resolution;
            int cells = res * res;
            float[] heights = h.Heights;
            // Full-grid scratch: editor-time only (~7 floats per cell). Per-route nearest distance must be the min over
            // that route's segments before routes are blended, hence best* and the accumulators are kept separately.
            var bestD2 = new float[cells];
            var bestS = new float[cells];
            // Metres beyond a route end along its end direction (0 = alongside the route). Ends are square, not
            // round: a branch starts on the road it leaves, so a round cap would lay a second bed over it.
            var beyondEnd = new float[cells];
            // Bed cells (inside some flat band) and shoulder-only cells are blended separately: a bed takes the
            // weighted road levels of every corridor over it (never the raw ground a shoulder fades to), shoulders
            // fade to the ground. Junction bands in Profile keep overlapping levels close, so the fade is gentle.
            var inBed = new bool[cells];
            var bedSumA = new float[cells];
            var bedSumAP = new float[cells];
            var sumW = new float[cells];
            var sumWP = new float[cells];
            var maxW = new float[cells];

            for (int r = 0; r < carved.Count; r++)
            {
                CarvedRoute route = carved[r];
                float reach = route.FlatHalf + route.Shoulder;
                float reach2 = reach * reach;
                RoutePath path = route.Path;
                PolylineBounds(path.Points, reach, out Vector2 min, out Vector2 max);
                CellRange(h, min.x, max.x, out int bx0, out int bx1);
                CellRange(h, min.y, max.y, out int bz0, out int bz1);
                for (int z = bz0; z <= bz1; z++)
                for (int x = bx0; x <= bx1; x++)
                    bestD2[z * res + x] = float.PositiveInfinity;

                int lastSegment = path.Points.Length - 2;
                for (int k = 0; k <= lastSegment; k++)
                {
                    Vector2 a = path.Points[k];
                    Vector2 b = path.Points[k + 1];
                    Vector2 ab = b - a;
                    float len2 = ab.sqrMagnitude;
                    float s0 = path.S[k], s1 = path.S[k + 1];
                    CellRange(h, Mathf.Min(a.x, b.x) - reach, Mathf.Max(a.x, b.x) + reach, out int x0, out int x1);
                    CellRange(h, Mathf.Min(a.y, b.y) - reach, Mathf.Max(a.y, b.y) + reach, out int z0, out int z1);
                    for (int z = z0; z <= z1; z++)
                    {
                        float wz = h.CellToWorld(z);
                        for (int x = x0; x <= x1; x++)
                        {
                            var q = new Vector2(h.CellToWorld(x), wz);
                            float raw = len2 > 0f ? Vector2.Dot(q - a, ab) / len2 : 0f;
                            float t = Mathf.Clamp01(raw);
                            float d2 = (q - (a + ab * t)).sqrMagnitude;
                            int i = z * res + x;
                            if (d2 >= reach2 || d2 >= bestD2[i]) continue;
                            bestD2[i] = d2;
                            bestS[i] = s0 + (s1 - s0) * t;
                            float segLen = Mathf.Sqrt(len2);
                            beyondEnd[i] = k == 0 && raw < 0f ? -raw * segLen : (k == lastSegment && raw > 1f ? (raw - 1f) * segLen : 0f);
                        }
                    }
                }

                for (int z = bz0; z <= bz1; z++)
                {
                    for (int x = bx0; x <= bx1; x++)
                    {
                        int i = z * res + x;
                        if (bestD2[i] >= reach2) continue;
                        float d = Mathf.Sqrt(bestD2[i]);
                        float p = route.ProfileAt(bestS[i]);
                        if (beyondEnd[i] > 0f)
                        {
                            // Past an end the road keeps its cross-section but fades out over one shoulder length (never
                            // a bed), so a dead end eases into the ground and a branch start never re-levels the road it leaves.
                            float beyond = beyondEnd[i];
                            float lateral = Mathf.Sqrt(Mathf.Max(0f, d * d - beyond * beyond));
                            float wEnd = ChannelWeight(lateral, route.FlatHalf, route.Shoulder) * ChannelWeight(beyond, 0f, route.Shoulder);
                            if (wEnd <= 0f) continue;
                            sumW[i] += wEnd;
                            sumWP[i] += wEnd * p;
                            if (wEnd > maxW[i]) maxW[i] = wEnd;
                            continue;
                        }
                        // Bed surface: every route whose corridor covers the cell contributes its (flat-across) level,
                        // weighted 1 on its centreline down to ~0 at the corridor edge and faded in over one bed
                        // half-width from each route end. Where corridors overlap, one road surface then fades into the
                        // other across the whole corridor (no step), and a branch has no seam along its start line.
                        float fromEnd = Mathf.Min(bestS[i], path.Length - bestS[i]);
                        float a = (1f - Smooth01(d / reach)) * Smooth01(fromEnd / route.FlatHalf);
                        a = MinBedWeight + (1f - MinBedWeight) * a;
                        bedSumA[i] += a;
                        bedSumAP[i] += a * p;
                        if (d <= route.FlatHalf)
                        {
                            inBed[i] = true;
                            maxW[i] = 1f;
                            continue;
                        }
                        float w = ChannelWeight(d, route.FlatHalf, route.Shoulder);
                        if (w <= 0f) continue;
                        sumW[i] += w;
                        sumWP[i] += w * p;
                        if (w > maxW[i]) maxW[i] = w;
                    }
                }
            }

            for (int i = 0; i < cells; i++)
            {
                if (inBed[i]) heights[i] = bedSumAP[i] / bedSumA[i];
                else if (maxW[i] > 0f) heights[i] = Mathf.Lerp(heights[i], sumWP[i] / sumW[i], maxW[i]);
            }
        }

        private static void ApplyChasm(HeightField h, TerrainLayoutData layout)
        {
            ChasmDef c = layout.chasm;
            if (c.gapWidth <= 0f || c.length <= 0f || c.depth <= 0f) return;
            float yaw = c.yawDeg * Mathf.Deg2Rad;
            var fwd = new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw));
            var right = new Vector2(fwd.y, -fwd.x);
            float halfGap = c.gapWidth * 0.5f;
            float halfLen = c.length * 0.5f;

            // Lip = the approach bed one sample before the edge; everything is measured from it so the jump is the
            // same whatever the noise does.
            Vector2 nearLip = c.centre - fwd * (halfGap + h.Spacing);
            float lip = h.Sample(nearLip.x, nearLip.y);
            float floorY = lip - c.depth;
            float landing = lip - c.landingDrop;
            float grade = Mathf.Tan(Mathf.Clamp(layout.maxRouteSlopeDeg, 0.1f, 89f) * Mathf.Deg2Rad);
            // Far side: cut down to the landing height, then back up to the bed at the route grade.
            float runout = Mathf.Max(0f, (h.HeightScale - landing) / grade);

            float reachAlong = halfGap + runout;
            float rx = Mathf.Abs(fwd.x) * reachAlong + Mathf.Abs(right.x) * halfLen;
            float rz = Mathf.Abs(fwd.y) * reachAlong + Mathf.Abs(right.y) * halfLen;
            CellRange(h, c.centre.x - rx, c.centre.x + rx, out int x0, out int x1);
            CellRange(h, c.centre.y - rz, c.centre.y + rz, out int z0, out int z1);
            int res = h.Resolution;
            float[] heights = h.Heights;
            for (int z = z0; z <= z1; z++)
            {
                float wz = h.CellToWorld(z) - c.centre.y;
                for (int x = x0; x <= x1; x++)
                {
                    var rel = new Vector2(h.CellToWorld(x) - c.centre.x, wz);
                    float across = Vector2.Dot(rel, right);
                    if (across < -halfLen || across > halfLen) continue;
                    float along = Vector2.Dot(rel, fwd);
                    if (along < -halfGap) continue;
                    int i = z * res + x;
                    float cap = along <= halfGap ? floorY : landing + grade * (along - halfGap);
                    if (heights[i] > cap) heights[i] = cap;
                }
            }
        }

        private static float[] PadHeights(HeightField h, SpawnDef[] spawns)
        {
            if (spawns == null) return null;
            var heights = new float[spawns.Length];
            for (int i = 0; i < spawns.Length; i++) heights[i] = h.Sample(spawns[i].position.x, spawns[i].position.y);
            return heights;
        }

        private static void ApplySpawnPads(HeightField h, TerrainLayoutData layout, float[] padHeights)
        {
            SpawnDef[] spawns = layout.spawns;
            if (spawns == null) return;
            float grade = Mathf.Tan(Mathf.Clamp(layout.maxRouteSlopeDeg, 0.1f, 89f) * Mathf.Deg2Rad);
            int res = h.Resolution;
            float[] heights = h.Heights;
            for (int s = 0; s < spawns.Length; s++)
            {
                SpawnDef sp = spawns[s];
                float pad = sp.padRadius;
                if (pad <= 0f) continue;
                float padHeight = padHeights[s];
                // Flat inside padRadius. Over the next padRadius the ground is eased toward a cone rising/falling at the
                // route grade from the pad edge: routes (already within that cone) are untouched, rough ground is
                // softened, and nothing beyond 2 x padRadius changes (the world-edge berm stays steep).
                float outer = 2f * pad;
                CellRange(h, sp.position.x - outer, sp.position.x + outer, out int x0, out int x1);
                CellRange(h, sp.position.y - outer, sp.position.y + outer, out int z0, out int z1);
                for (int z = z0; z <= z1; z++)
                {
                    float dz = h.CellToWorld(z) - sp.position.y;
                    for (int x = x0; x <= x1; x++)
                    {
                        float dx = h.CellToWorld(x) - sp.position.x;
                        float r = Mathf.Sqrt(dx * dx + dz * dz);
                        if (r >= outer) continue;
                        int i = z * res + x;
                        if (r <= pad)
                        {
                            heights[i] = padHeight;
                            continue;
                        }
                        float cone = grade * (r - pad);
                        float target = Mathf.Clamp(heights[i], padHeight - cone, padHeight + cone);
                        heights[i] = Mathf.Lerp(heights[i], target, ChannelWeight(r, pad, pad));
                    }
                }
            }
        }

        private static void Clamp(HeightField h)
        {
            float[] heights = h.Heights;
            float top = h.HeightScale;
            for (int i = 0; i < heights.Length; i++) heights[i] = Mathf.Clamp(heights[i], 0f, top);
        }

        // ---------------------------------------------------------------- shapes

        private static float CraterProfile(CraterDef c, float r)
        {
            float floorY = -c.floorDepth;
            if (r <= c.rimRadius)
            {
                if (r <= c.floorRadius)
                {
                    float plateauY = floorY + c.plateauHeight;
                    if (r <= c.plateauRadius) return plateauY;
                    // Plateau edge is a 1:1 bank (as wide as it is tall): a crisp pedestal that routes ramp through.
                    float bank = Mathf.Abs(c.plateauHeight);
                    if (bank > 0f && r < c.plateauRadius + bank)
                        return Mathf.Lerp(plateauY, floorY, Smooth01((r - c.plateauRadius) / bank));
                    return floorY;
                }
                float wall = c.rimRadius - c.floorRadius;
                return wall > 0f ? Mathf.Lerp(floorY, c.rimHeight, Smooth01((r - c.floorRadius) / wall)) : c.rimHeight;
            }
            float slope = c.outerRadius - c.rimRadius;
            return slope > 0f ? c.rimHeight * (1f - Smooth01((r - c.rimRadius) / slope)) : 0f;
        }

        private static float CraterBlendAt(CraterDef c, float r)
        {
            if (c.rimRadius <= 0f) return 0f;
            if (r <= c.rimRadius) return 1f;
            float slope = c.outerRadius - c.rimRadius;
            return slope > 0f ? 1f - Smooth01((r - c.rimRadius) / slope) : 0f;
        }

        private static float CraterBlend(CraterDef c, float x, float z)
        {
            float dx = x - c.centre.x, dz = z - c.centre.y;
            return CraterBlendAt(c, Mathf.Sqrt(dx * dx + dz * dz));
        }

        /// <summary>1 inside <paramref name="half"/>, smoothstep down to 0 over <paramref name="shoulder"/>.</summary>
        private static float ChannelWeight(float d, float half, float shoulder)
        {
            if (d <= half) return 1f;
            if (shoulder <= 0f) return 0f;
            return 1f - Smooth01((d - half) / shoulder);
        }

        private static float RegionWeight(NoiseRegion region, float x, float z)
        {
            if (region.radius <= 0f) return 1f;
            float dx = x - region.centre.x, dz = z - region.centre.y;
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            if (d <= region.radius) return 1f;
            if (region.falloff <= 0f) return 0f;
            return 1f - Smooth01((d - region.radius) / region.falloff);
        }

        /// <summary>Allowed height difference to a point offset by <paramref name="delta"/> from a road heading <paramref name="dir"/>.</summary>
        private static float Band(Vector2 delta, Vector2 dir, float across, float along)
        {
            return across * Mathf.Abs(delta.x * dir.y - delta.y * dir.x) + along * Mathf.Abs(Vector2.Dot(delta, dir));
        }

        /// <summary>Corner fillet radius: the whole carved corridor (bed + both shoulders) turns as one smooth band.</summary>
        private static float CornerRadius(RouteDef route) => route.width + 2f * Mathf.Max(route.shoulder, 0f);

        private static float Smooth01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        // ---------------------------------------------------------------- envelopes

        /// <summary>Lower envelope: largest profile &lt;= values whose step-to-step change is at most <paramref name="rise"/> (cut only).</summary>
        private static void CutEnvelope(float[] v, float rise)
        {
            for (int i = 1; i < v.Length; i++) v[i] = Mathf.Min(v[i], v[i - 1] + rise);
            for (int i = v.Length - 2; i >= 0; i--) v[i] = Mathf.Min(v[i], v[i + 1] + rise);
        }

        /// <summary>Centred moving average over +/- <paramref name="half"/> samples, ends clamped. <paramref name="scratch"/> = same length.</summary>
        private static void BoxSmooth(float[] v, int half, float[] scratch)
        {
            int n = v.Length;
            if (half <= 0 || n < 3) return;
            Array.Copy(v, scratch, n);
            float sum = 0f;
            for (int j = -half; j <= half; j++) sum += scratch[Mathf.Clamp(j, 0, n - 1)];
            float inv = 1f / (2 * half + 1);
            for (int i = 0; i < n; i++)
            {
                v[i] = sum * inv;
                sum += scratch[Mathf.Min(i + half + 1, n - 1)] - scratch[Mathf.Max(i - half, 0)];
            }
        }

        /// <summary>Upper envelope: smallest profile &gt;= values with the same rise limit (fill toward pinned junctions).</summary>
        private static void FillEnvelope(float[] v, float rise)
        {
            for (int i = 1; i < v.Length; i++) v[i] = Mathf.Max(v[i], v[i - 1] - rise);
            for (int i = v.Length - 2; i >= 0; i--) v[i] = Mathf.Max(v[i], v[i + 1] - rise);
        }

        // ---------------------------------------------------------------- noise

        private static float Fbm(float x, float z, float wavelength, int octaves, bool ridged, uint salt)
        {
            // Standard fBm: each octave doubles frequency and halves amplitude; normalised so the result stays in
            // [-1, 1] (smooth) or [0, 1] (ridged) and `amplitude` in the data means metres.
            float freq = 1f / wavelength, amp = 1f, sum = 0f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                float n = Gradient(x * freq, z * freq, unchecked(salt + (uint)o * 0x9E3779B9u));
                if (ridged)
                {
                    n = 1f - Mathf.Abs(n);
                    n *= n;
                }
                sum += amp * n;
                norm += amp;
                freq *= 2f;
                amp *= 0.5f;
            }
            return sum / norm;
        }

        /// <summary>2-D gradient (Perlin) noise with our own integer hash, so it is identical on every Unity version (Mathf.PerlinNoise is not seedable).</summary>
        private static float Gradient(float x, float z, uint salt)
        {
            int ix = Mathf.FloorToInt(x), iz = Mathf.FloorToInt(z);
            float fx = x - ix, fz = z - iz;
            float u = Fade(fx), v = Fade(fz);
            float n00 = GradDot(Hash(ix, iz, salt), fx, fz);
            float n10 = GradDot(Hash(ix + 1, iz, salt), fx - 1f, fz);
            float n01 = GradDot(Hash(ix, iz + 1, salt), fx, fz - 1f);
            float n11 = GradDot(Hash(ix + 1, iz + 1, salt), fx - 1f, fz - 1f);
            float a = n00 + u * (n10 - n00);
            float b = n01 + u * (n11 - n01);
            // 2-D Perlin with unit gradients peaks at +/- 1/sqrt(2); rescale to +/- 1.
            return Mathf.Clamp((a + v * (b - a)) * Sqrt2, -1f, 1f);
        }

        private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        private static float GradDot(uint hash, float x, float z)
        {
            const float d = 0.70710678f;
            switch (hash >> 29)
            {
                case 0: return x;
                case 1: return -x;
                case 2: return z;
                case 3: return -z;
                case 4: return d * (x + z);
                case 5: return d * (x - z);
                case 6: return d * (-x + z);
                default: return d * (-x - z);
            }
        }

        private static uint Hash(int x, int z, uint salt)
        {
            unchecked
            {
                uint h = ((uint)x * 0x8DA6B343u) ^ ((uint)z * 0xD8163841u) ^ salt;
                h ^= h >> 15;
                h *= 0x2C1B3C6Du;
                h ^= h >> 12;
                h *= 0x297A2D39u;
                h ^= h >> 15;
                return h;
            }
        }

        // ---------------------------------------------------------------- geometry helpers

        private static void CellRange(HeightField h, float worldMin, float worldMax, out int i0, out int i1)
        {
            int last = h.Resolution - 1;
            i0 = Mathf.Clamp(Mathf.FloorToInt((worldMin - h.Origin) / h.Spacing), 0, last);
            i1 = Mathf.Clamp(Mathf.CeilToInt((worldMax - h.Origin) / h.Spacing), 0, last);
        }

        private static void PolylineBounds(Vector2[] pts, float pad, out Vector2 min, out Vector2 max)
        {
            min = pts[0];
            max = pts[0];
            for (int i = 1; i < pts.Length; i++)
            {
                min = Vector2.Min(min, pts[i]);
                max = Vector2.Max(max, pts[i]);
            }
            min -= new Vector2(pad, pad);
            max += new Vector2(pad, pad);
        }

        private static float PolylineDistance(Vector2[] pts, Vector2 q)
        {
            float best = float.PositiveInfinity;
            for (int k = 0; k < pts.Length - 1; k++)
            {
                Vector2 a = pts[k];
                Vector2 ab = pts[k + 1] - a;
                float len2 = ab.sqrMagnitude;
                float t = len2 > 0f ? Mathf.Clamp01(Vector2.Dot(q - a, ab) / len2) : 0f;
                float d2 = (q - (a + ab * t)).sqrMagnitude;
                if (d2 < best) best = d2;
            }
            return Mathf.Sqrt(best);
        }

        // ---------------------------------------------------------------- route internals

        /// <summary>A route centreline with rounded corners, as a dense polyline with cumulative arclength.</summary>
        private sealed class RoutePath
        {
            public Vector2[] Points;
            public float[] S;
            public float Length;

            public static RoutePath Build(Vector2[] nodes, float cornerRadius)
            {
                var pts = new List<Vector2>(nodes.Length * 8) { nodes[0] };
                for (int i = 1; i < nodes.Length - 1; i++)
                {
                    Vector2 a = nodes[i - 1], b = nodes[i], c = nodes[i + 1];
                    Vector2 d1 = b - a, d2 = c - b;
                    float l1 = d1.magnitude, l2 = d2.magnitude;
                    if (l1 < 1e-4f || l2 < 1e-4f || cornerRadius <= 0f)
                    {
                        pts.Add(b);
                        continue;
                    }
                    d1 /= l1;
                    d2 /= l2;
                    float cross = d1.x * d2.y - d1.y * d2.x;
                    float turn = Mathf.Atan2(Mathf.Abs(cross), Vector2.Dot(d1, d2));
                    if (turn < ArcStepRad * 0.5f)
                    {
                        pts.Add(b);
                        continue;
                    }
                    float tanHalf = Mathf.Tan(turn * 0.5f);
                    // Never let a fillet eat more than half of either leg, so consecutive corners cannot overlap.
                    float tangent = Mathf.Min(cornerRadius * tanHalf, 0.5f * Mathf.Min(l1, l2));
                    float radius = tangent / tanHalf;
                    float side = cross > 0f ? 1f : -1f;
                    Vector2 p1 = b - d1 * tangent;
                    Vector2 centre = p1 + new Vector2(-d1.y, d1.x) * (side * radius);
                    Vector2 v0 = p1 - centre;
                    int steps = Mathf.Max(1, Mathf.CeilToInt(turn / ArcStepRad));
                    for (int k = 0; k <= steps; k++)
                    {
                        float ang = side * turn * k / steps;
                        float cs = Mathf.Cos(ang), sn = Mathf.Sin(ang);
                        pts.Add(centre + new Vector2(v0.x * cs - v0.y * sn, v0.x * sn + v0.y * cs));
                    }
                }
                pts.Add(nodes[nodes.Length - 1]);

                // Drop zero-length pieces so every segment has a direction.
                var clean = new List<Vector2>(pts.Count) { pts[0] };
                for (int i = 1; i < pts.Count; i++)
                    if ((pts[i] - clean[clean.Count - 1]).sqrMagnitude > 1e-8f) clean.Add(pts[i]);
                if (clean.Count == 1) clean.Add(clean[0]);

                var path = new RoutePath { Points = clean.ToArray(), S = new float[clean.Count] };
                for (int i = 1; i < clean.Count; i++) path.S[i] = path.S[i - 1] + (clean[i] - clean[i - 1]).magnitude;
                path.Length = path.S[clean.Count - 1];
                return path;
            }

            public Vector2 PointAt(float s)
            {
                if (s <= 0f) return Points[0];
                int last = Points.Length - 1;
                if (s >= Length) return Points[last];
                int lo = 0, hi = last;
                while (hi - lo > 1)
                {
                    int mid = (lo + hi) >> 1;
                    if (S[mid] <= s) lo = mid;
                    else hi = mid;
                }
                float span = S[hi] - S[lo];
                float t = span > 0f ? (s - S[lo]) / span : 0f;
                return Points[lo] + (Points[hi] - Points[lo]) * t;
            }

            /// <summary>Unit direction of travel at arclength <paramref name="s"/>.</summary>
            public Vector2 TangentAt(float s)
            {
                int last = Points.Length - 1;
                int lo = 0, hi = last;
                while (hi - lo > 1)
                {
                    int mid = (lo + hi) >> 1;
                    if (S[mid] <= s) lo = mid;
                    else hi = mid;
                }
                Vector2 d = Points[hi] - Points[lo];
                float m = d.magnitude;
                return m > 0f ? d / m : Vector2.up;
            }

            /// <summary>Distance from <paramref name="q"/> to the centreline; <paramref name="s"/> = arclength and
            /// <paramref name="point"/> = position of the nearest centreline point.</summary>
            public float Nearest(Vector2 q, out float s, out Vector2 point)
            {
                float best = float.PositiveInfinity;
                s = 0f;
                point = Points[0];
                for (int k = 0; k < Points.Length - 1; k++)
                {
                    Vector2 a = Points[k];
                    Vector2 ab = Points[k + 1] - a;
                    float len2 = ab.sqrMagnitude;
                    float t = len2 > 0f ? Mathf.Clamp01(Vector2.Dot(q - a, ab) / len2) : 0f;
                    float d2 = (q - (a + ab * t)).sqrMagnitude;
                    if (d2 < best)
                    {
                        best = d2;
                        s = S[k] + (S[k + 1] - S[k]) * t;
                        point = a + ab * t;
                    }
                }
                return Mathf.Sqrt(best);
            }
        }

        private readonly struct Hub
        {
            public readonly Vector2 Centre;
            public readonly float Radius;
            public readonly float Height;

            public Hub(Vector2 centre, float radius, float height)
            {
                Centre = centre;
                Radius = radius;
                Height = height;
            }
        }

        private sealed class CarvedRoute
        {
            public readonly RoutePath Path;
            public readonly float FlatHalf;
            public readonly float Shoulder;
            private readonly float[] _profile;
            private readonly float _step;

            public CarvedRoute(RoutePath path, float[] profile, float step, float flatHalf, float shoulder)
            {
                Path = path;
                _profile = profile;
                _step = step;
                FlatHalf = flatHalf;
                Shoulder = shoulder;
            }

            public float ProfileAt(float s)
            {
                int last = _profile.Length - 1;
                if (last <= 0 || _step <= 0f) return _profile[0];
                float f = s / _step;
                if (f <= 0f) return _profile[0];
                if (f >= last) return _profile[last];
                int i = (int)f;
                return _profile[i] + (_profile[i + 1] - _profile[i]) * (f - i);
            }
        }
    }
}
