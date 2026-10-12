using System.Collections.Generic;
using Convoy.Level;
using NUnit.Framework;
using UnityEngine;

namespace Convoy.AI.Tests
{
    /// <summary>
    /// TR-01c spec AC 9 (export half): every default Páramo rival route, exported exactly as the editor setup does
    /// (<see cref="RivalRouteExport"/>, 12 m target spacing, stop at the Monolith plateau edge), is a valid
    /// <see cref="PolylinePath"/> the AI can drive: length within 1 % of the finely sampled road, curvature
    /// ≤ 1/25 m⁻¹ everywhere, waypoints 10–15 m apart. Fast tests use the real layout on a coarse 257² heightmap
    /// (heights only shift y); the Slow test uses the real 1025² map.
    /// </summary>
    public class ParamoRivalRouteTests
    {
        // Same values the editor setup uses (TR01_ParamoRuntimeSetup.RivalWaypointSpacing).
        private const float WaypointSpacing = 12f;
        private const float MinGap = 10f, MaxGap = 15f;
        private const float MaxCurvature = 1f / 25f;
        private const float CurvatureSpan = 20f;
        private const float FineSpacing = 2f;

        private static HeightField _coarse;
        private static TerrainLayoutData _coarseLayout;

        private static TerrainLayoutData CoarseLayout
        {
            get
            {
                if (_coarseLayout != null) return _coarseLayout;
                _coarseLayout = ParamoLayoutDefaults.Create();
                _coarseLayout.heightmapResolution = 257;
                return _coarseLayout;
            }
        }

        private static HeightField Coarse => _coarse ?? (_coarse = TerrainSynthesis.Build(CoarseLayout));

        [Test]
        public void Defaults_HaveThreeRivalRoutes_FromTheMapEdgesToTheMonolith()
        {
            TerrainLayoutData l = CoarseLayout;
            int rivals = 0;
            foreach (RouteDef r in l.routes)
            {
                if (r.kind != RouteKind.Rival) continue;
                rivals++;
                Vector2 end = r.nodes[r.nodes.Length - 1];
                Assert.That((end - l.crater.centre).magnitude, Is.LessThanOrEqualTo(l.crater.plateauRadius), r.id);
                Assert.That(System.Array.Exists(l.spawns, s => s.rival && (s.position - r.nodes[0]).magnitude <= s.padRadius),
                    r.id + " does not start on a rival spawn pad");
            }
            Assert.AreEqual(3, rivals);
        }

        [Test]
        public void RivalExport_IsADrivablePolylinePath() => AssertRivalRoutes(Coarse, CoarseLayout);

        [Test, Category("Slow")]
        public void Slow_RivalExport_OnTheRealMap()
        {
            TerrainLayoutData l = ParamoLayoutDefaults.Create();
            AssertRivalRoutes(TerrainSynthesis.Build(l), l);
        }

        private static void AssertRivalRoutes(HeightField h, TerrainLayoutData l)
        {
            var waypoints = new List<Vector3>();
            var fine = new List<Vector3>();
            foreach (RouteDef r in l.routes)
            {
                if (r.kind != RouteKind.Rival) continue;
                int n = RivalRouteExport.Waypoints(h, r, WaypointSpacing, l.crater.centre, l.crater.plateauRadius, waypoints);
                Assert.That(n, Is.GreaterThan(10), r.id);

                // Waypoints 10-15 m apart (3D: what the RacingLine children are).
                for (int i = 1; i < n; i++)
                    Assert.That(Vector3.Distance(waypoints[i - 1], waypoints[i]), Is.InRange(MinGap, MaxGap), r.id + " gap at " + i);

                // Stops on the plateau edge, not in the Monolith.
                Vector3 last = waypoints[n - 1];
                float endR = new Vector2(last.x - l.crater.centre.x, last.z - l.crater.centre.y).magnitude;
                Assert.That(endR, Is.LessThanOrEqualTo(l.crater.plateauRadius), r.id + " stops short of the plateau");
                Assert.That(endR, Is.GreaterThan(l.crater.plateauRadius - MaxGap), r.id + " drives into the Monolith");

                var path = new PolylinePath(waypoints, false);

                // Length vs the same stretch of road sampled finely (up to the export's last point).
                TerrainSynthesis.SampleRoute(h, r, FineSpacing, fine);
                float fineLength = 0f;
                for (int i = 1; i < fine.Count; i++)
                {
                    Vector3 a = fine[i - 1], b = fine[i];
                    float rb = new Vector2(b.x - l.crater.centre.x, b.z - l.crater.centre.y).magnitude;
                    if (rb < endR - 1e-3f) break;
                    fineLength += Vector3.Distance(a, b);
                }
                Assert.That(path.Length, Is.EqualTo(fineLength).Within(0.01f * fineLength), r.id + " length");

                for (float d = 0f; d <= path.Length; d += 1f)
                    Assert.That(path.CurvatureAt(d, CurvatureSpan), Is.LessThanOrEqualTo(MaxCurvature), r.id + " curvature at " + d + " m");
            }
        }
    }
}
