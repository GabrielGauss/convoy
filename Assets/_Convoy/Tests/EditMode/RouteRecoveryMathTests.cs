using Convoy.Race;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Convoy.Tests
{
    /// <summary>TR-01c spec AC 9 (recovery half): nearest route point + unit tangent, route boundaries respected, 0 B GC.</summary>
    public class RouteRecoveryMathTests
    {
        // Route 0: due north along x = 0, z = 0..40 (5 points, 10 m apart, climbing).
        // Route 1: due east along z = 100, x = 0..30 (4 points).
        private static readonly Vector3[] Points =
        {
            new Vector3(0f, 0f, 0f), new Vector3(0f, 1f, 10f), new Vector3(0f, 2f, 20f), new Vector3(0f, 3f, 30f), new Vector3(0f, 4f, 40f),
            new Vector3(0f, 5f, 100f), new Vector3(10f, 5f, 100f), new Vector3(20f, 5f, 100f), new Vector3(30f, 5f, 100f),
        };
        private static readonly int[] Starts = { 0, 5 };

        [Test]
        public void Nearest_IgnoresHeight_AndReturnsUnitTangent()
        {
            // 40 m below the road (fell into a chasm): still the point straight above-ish, not something "closer" in 3D.
            int i = RouteRecoveryMath.Nearest(new Vector3(1f, -40f, 21f), Points, Starts, out Vector3 tangent);
            Assert.AreEqual(2, i);
            Assert.That(tangent.magnitude, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(tangent.y, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(tangent.z, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void Nearest_OnSecondRoute_TangentFollowsThatRoute()
        {
            int i = RouteRecoveryMath.Nearest(new Vector3(19f, 0f, 104f), Points, Starts, out Vector3 tangent);
            Assert.AreEqual(7, i);
            Assert.That(tangent.x, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void Tangent_AtRouteEnds_NeverCrossesARouteBoundary()
        {
            // Last point of route 0 and first of route 1 are neighbours in the array, but not on the road.
            Assert.That(RouteRecoveryMath.TangentAt(4, Points, Starts).z, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(RouteRecoveryMath.TangentAt(5, Points, Starts).x, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void BackOff_WalksTowardTheRouteStart_AndStopsAtIt()
        {
            Assert.AreEqual(2, RouteRecoveryMath.BackOff(4, Points, Starts, 15f)); // 2 x 10 m >= 15 m
            Assert.AreEqual(4, RouteRecoveryMath.BackOff(4, Points, Starts, 0f));
            Assert.AreEqual(5, RouteRecoveryMath.BackOff(7, Points, Starts, 500f)); // clamped to route 1's first point
        }

        [Test]
        public void RouteBounds_PackedLayout()
        {
            Assert.AreEqual(0, RouteRecoveryMath.RouteOf(4, Starts));
            Assert.AreEqual(1, RouteRecoveryMath.RouteOf(5, Starts));
            Assert.AreEqual(5, RouteRecoveryMath.RouteFirst(8, Starts));
            Assert.AreEqual(4, RouteRecoveryMath.RouteLast(0, Starts, Points.Length));
            Assert.AreEqual(8, RouteRecoveryMath.RouteLast(6, Starts, Points.Length));
        }

        [Test]
        public void Nearest_NoPoints_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, RouteRecoveryMath.Nearest(Vector3.zero, new Vector3[0], new int[0], out Vector3 tangent));
            Assert.That(tangent.z, Is.EqualTo(1f).Within(1e-6f));
        }

        [Test]
        public void Queries_DoNotAllocate()
        {
            var p = new Vector3(3f, 0f, 37f);
            Assert.That(() =>
            {
                int i = RouteRecoveryMath.Nearest(p, Points, Starts, out _);
                int j = RouteRecoveryMath.BackOff(i, Points, Starts, 10f);
                RouteRecoveryMath.TangentAt(j, Points, Starts);
            }, Is.Not.AllocatingGCMemory());
        }
    }
}
