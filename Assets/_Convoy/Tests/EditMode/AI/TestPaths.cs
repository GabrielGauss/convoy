using UnityEngine;

namespace Convoy.AI.Tests
{
    /// <summary>Code-built paths shared by the AI tests (no scenes, no assets).</summary>
    internal static class TestPaths
    {
        /// <summary>Open straight along +Z from the origin, one waypoint every <paramref name="spacing"/> metres.</summary>
        public static PolylinePath Straight(float length, float spacing = 10f)
        {
            int count = Mathf.RoundToInt(length / spacing) + 1;
            var points = new Vector3[count];
            for (int i = 0; i < count; i++) points[i] = new Vector3(0f, 0f, i * spacing);
            return new PolylinePath(points, false);
        }

        /// <summary>Closed circle around the origin in the XZ plane, driven clockwise seen from above (right turns).</summary>
        public static PolylinePath Circle(float radius, int pointCount)
        {
            var points = new Vector3[pointCount];
            for (int i = 0; i < pointCount; i++)
            {
                float angle = i * 2f * Mathf.PI / pointCount;
                // Start at (0,0,-r) heading −X; south → west → north → east is clockwise from above.
                points[i] = new Vector3(-radius * Mathf.Sin(angle), 0f, -radius * Mathf.Cos(angle));
            }
            return new PolylinePath(points, true);
        }

        /// <summary>
        /// Open path: straight along +Z for <paramref name="straight"/> metres, then a right-hand 90° arc of
        /// <paramref name="radius"/> (1° waypoints), then 100 m straight along +X.
        /// </summary>
        public static PolylinePath StraightThenRightTurn(float straight, float radius)
        {
            const int arcSteps = 90;
            var points = new Vector3[2 + arcSteps + 1];
            int n = 0;
            points[n++] = Vector3.zero;
            for (int i = 0; i <= arcSteps; i++)
            {
                float a = i * Mathf.Deg2Rad; // turning right around the centre (radius, 0, straight)
                points[n++] = new Vector3(radius - radius * Mathf.Cos(a), 0f, straight + radius * Mathf.Sin(a));
            }
            points[n] = new Vector3(radius + 100f, 0f, straight + radius);
            return new PolylinePath(points, false);
        }
    }
}
