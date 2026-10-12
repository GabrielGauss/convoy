using System;
using NUnit.Framework;
using UnityEngine;

namespace Convoy.AI.Tests
{
    public class PolylinePathTests
    {
        private const float Eps = 1e-3f;

        // 3-4-5 triangle: open length 7, closed length 12.
        private static readonly Vector3[] Triangle = { Vector3.zero, new Vector3(3f, 0f, 0f), new Vector3(3f, 0f, 4f) };

        private static void AssertVector(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, Eps, "x");
            Assert.AreEqual(expected.y, actual.y, Eps, "y");
            Assert.AreEqual(expected.z, actual.z, Eps, "z");
        }

        // --- Construction ------------------------------------------------

        [Test]
        public void Constructor_Throws_WithFewerThanTwoDistinctPoints()
        {
            Assert.Throws<ArgumentNullException>(() => new PolylinePath(null, false));
            Assert.Throws<ArgumentException>(() => new PolylinePath(new[] { Vector3.one }, false));
            Assert.Throws<ArgumentException>(() => new PolylinePath(new[] { Vector3.one, Vector3.one }, false));
        }

        [Test]
        public void Length_IsSumOfSegments_PlusClosingSegmentWhenClosed()
        {
            Assert.AreEqual(7f, new PolylinePath(Triangle, false).Length, Eps);
            Assert.AreEqual(12f, new PolylinePath(Triangle, true).Length, Eps);
            Assert.AreEqual(3, new PolylinePath(Triangle, true).PointCount);
        }

        // --- PositionAt / TangentAt ---------------------------------------

        [Test]
        public void PositionAt_InterpolatesAlongSegments()
        {
            var path = new PolylinePath(Triangle, false);
            AssertVector(new Vector3(1.5f, 0f, 0f), path.PositionAt(1.5f));
            AssertVector(new Vector3(3f, 0f, 2f), path.PositionAt(5f));
        }

        [Test]
        public void PositionAt_ClampsOnOpenPath()
        {
            var path = new PolylinePath(Triangle, false);
            AssertVector(Vector3.zero, path.PositionAt(-5f));
            AssertVector(new Vector3(3f, 0f, 4f), path.PositionAt(100f));
        }

        [Test]
        public void PositionAt_WrapsOnClosedPath()
        {
            var path = new PolylinePath(Triangle, true);
            AssertVector(new Vector3(1.5f, 0f, 0f), path.PositionAt(12f + 1.5f));
            // −1 m wraps to 11 m: 4/5 of the way along the closing segment (3,0,4) → (0,0,0).
            AssertVector(new Vector3(0.6f, 0f, 0.8f), path.PositionAt(-1f));
        }

        [Test]
        public void TangentAt_IsUnitAndHorizontal_OnASlope()
        {
            var path = new PolylinePath(new[] { Vector3.zero, new Vector3(0f, 10f, 10f) }, false);
            AssertVector(Vector3.forward, path.TangentAt(5f));
        }

        // --- Projection ---------------------------------------------------

        [Test]
        public void Project_OnSegment_GivesDistanceAndSignedLateralOffset()
        {
            PolylinePath path = TestPaths.Straight(100f);

            PathProjection right = path.Project(new Vector3(2f, 0f, 15f), 0, 4);
            Assert.AreEqual(1, right.Segment);
            Assert.AreEqual(15f, right.Distance, Eps);
            Assert.AreEqual(2f, right.LateralOffset, Eps, "+ = right of the path");
            AssertVector(new Vector3(0f, 0f, 15f), right.Point);

            PathProjection left = path.Project(new Vector3(-3f, 0f, 25f), 2, 4);
            Assert.AreEqual(-3f, left.LateralOffset, Eps);
        }

        [Test]
        public void Project_Window_AgreesWithGlobalSearch_AroundALoop()
        {
            PolylinePath path = TestPaths.Circle(40f, 72);
            for (int k = 0; k < 72; k++)
            {
                float angle = (k + 0.3f) * 2f * Mathf.PI / 72f;
                var position = new Vector3(-42f * Mathf.Sin(angle), 0f, -42f * Mathf.Cos(angle));

                PathProjection global = path.ProjectGlobal(position);
                // Hint slightly off (as last tick's segment would be), including across the 71 → 0 wrap.
                PathProjection windowed = path.Project(position, global.Segment + 2, 4);

                Assert.AreEqual(global.Segment, windowed.Segment, $"sample {k}");
                Assert.AreEqual(global.Distance, windowed.Distance, Eps, $"sample {k}");
                Assert.AreEqual(global.LateralOffset, windowed.LateralOffset, Eps, $"sample {k}");
            }
        }

        [Test]
        public void Project_Window_DoesNotJumpToAFarPartOfThePath()
        {
            // Hairpin: out along +Z, back along −Z 6 m to the right. A car on the outbound leg near the
            // return leg must stay on the outbound leg when the window is small.
            var path = new PolylinePath(new[]
            {
                Vector3.zero, new Vector3(0f, 0f, 50f), new Vector3(0f, 0f, 100f),
                new Vector3(6f, 0f, 100f), new Vector3(6f, 0f, 50f), new Vector3(6f, 0f, 0f),
            }, false);

            PathProjection p = path.Project(new Vector3(3.5f, 0f, 25f), 0, 1);
            Assert.AreEqual(0, p.Segment);
            Assert.AreEqual(25f, p.Distance, Eps);
        }

        // --- Curvature ----------------------------------------------------

        [Test]
        public void CurvatureAt_IsZero_OnAStraight()
        {
            PolylinePath path = TestPaths.Straight(200f);
            for (float d = 0f; d <= 200f; d += 7f)
                Assert.AreEqual(0f, path.CurvatureAt(d, 8f), 1e-4f, $"d = {d}");
        }

        [Test]
        public void CurvatureAt_Of40mCircle_IsAbout1Over40()
        {
            PolylinePath path = TestPaths.Circle(40f, 72); // ~3.5 m between waypoints
            for (float d = 0f; d < path.Length; d += 3.3f)
                Assert.AreEqual(1f / 40f, path.CurvatureAt(d, 8f), 0.1f / 40f, $"d = {d}");
        }

        [Test]
        public void SignedAngleXZ_IsPositiveTurningRight()
        {
            Assert.Greater(PolylinePath.SignedAngleXZ(Vector3.forward, Vector3.right), 0f);
            Assert.Less(PolylinePath.SignedAngleXZ(Vector3.forward, Vector3.left), 0f);
            Assert.AreEqual(0f, PolylinePath.SignedAngleXZ(Vector3.zero, Vector3.right), Eps);
        }
    }
}
