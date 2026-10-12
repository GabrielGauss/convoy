using System;
using System.Collections.Generic;
using UnityEngine;

namespace Convoy.Level
{
    /// <summary>One debris chunk, in the wall's local space (wall centred at origin).</summary>
    public struct ChunkSpec
    {
        public Vector3 LocalCenter;
        public Vector3 Size;
        public Vector3 LocalEuler;
    }

    /// <summary>
    /// Splits a wall box into box chunks with irregular seams, using only simple cubes (no RayFire / paid assets, D-021).
    /// Pure and deterministic so the editor prefab generator (LV-01c) and tests get identical layouts per seed.
    /// </summary>
    public static class WallFractureLayout
    {
        private const float MaxTiltDegrees = 4f;
        // Offsets are fractions of a cell; below 0.5 a seam can never cross its neighbour, so chunks stay positive-sized.
        private const float MaxJitter = 0.45f;

        /// <summary>
        /// Wall box is local and centred: size.x = width, size.y = height, size.z = thickness.
        /// Column boundaries are shared by the whole wall; each column jitters its own row boundaries, so seams look
        /// shattered yet the pre-rotation chunks still tile the wall exactly (no gaps, no overlaps).
        /// Each chunk then gets a random tilt of at most 4 degrees per axis (visual only). Clears <paramref name="into"/>
        /// first; returns the chunk count (columns x rows). Allocates only at build time, never per frame.
        /// </summary>
        public static int Generate(Vector3 wallSize, int columns, int rows, float jitter, int seed, List<ChunkSpec> into)
        {
            if (into == null) throw new ArgumentNullException(nameof(into));
            into.Clear();
            if (columns < 1) columns = 1;
            if (rows < 1) rows = 1;
            jitter = Mathf.Clamp(jitter, 0f, MaxJitter);

            var rng = new System.Random(seed);
            float cellW = wallSize.x / columns;
            float cellH = wallSize.y / rows;

            // Fixed draw order (column edges, then per-column row edges and tilts) keeps output stable per seed.
            var xEdges = new float[columns + 1];
            for (int c = 0; c <= columns; c++)
            {
                float edge = -wallSize.x * 0.5f + c * cellW;
                if (c > 0 && c < columns) edge += Symmetric(rng) * jitter * cellW; // outer edges stay on the wall bounds
                xEdges[c] = edge;
            }
            xEdges[0] = -wallSize.x * 0.5f;
            xEdges[columns] = wallSize.x * 0.5f; // pin outer edges exactly: no float drift

            var yEdges = new float[rows + 1];
            for (int c = 0; c < columns; c++)
            {
                for (int r = 0; r <= rows; r++)
                {
                    float edge = -wallSize.y * 0.5f + r * cellH;
                    if (r > 0 && r < rows) edge += Symmetric(rng) * jitter * cellH;
                    yEdges[r] = edge;
                }
                yEdges[0] = -wallSize.y * 0.5f;
                yEdges[rows] = wallSize.y * 0.5f;

                for (int r = 0; r < rows; r++)
                {
                    float w = xEdges[c + 1] - xEdges[c];
                    float h = yEdges[r + 1] - yEdges[r];
                    into.Add(new ChunkSpec
                    {
                        LocalCenter = new Vector3(xEdges[c] + w * 0.5f, yEdges[r] + h * 0.5f, 0f),
                        Size = new Vector3(w, h, wallSize.z),
                        LocalEuler = new Vector3(
                            Symmetric(rng) * MaxTiltDegrees,
                            Symmetric(rng) * MaxTiltDegrees,
                            Symmetric(rng) * MaxTiltDegrees),
                    });
                }
            }
            return into.Count;
        }

        private static float Symmetric(System.Random rng) => (float)(rng.NextDouble() * 2.0 - 1.0);
    }
}
