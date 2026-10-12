using UnityEngine;

namespace Convoy.Level
{
    /// <summary>
    /// Square height grid in metres, row-major <c>[z * Resolution + x]</c> (the same [z, x] order Unity's
    /// TerrainData.SetHeights expects, so TR-01b copies it without transposing). Cell (0, 0) sits at world
    /// (-SizeMetres/2, -SizeMetres/2): the grid is centred on world (0, 0) like the layout data.
    /// Plain C# (no TerrainData) so synthesis and its checks run in EditMode tests.
    /// </summary>
    public sealed class HeightField
    {
        public HeightField(int resolution, float sizeMetres, float heightScale)
        {
            if (resolution < 2) resolution = 2;
            Resolution = resolution;
            SizeMetres = sizeMetres;
            HeightScale = heightScale;
            Spacing = sizeMetres / (resolution - 1);
            Origin = -0.5f * sizeMetres;
            Heights = new float[resolution * resolution];
        }

        public int Resolution { get; }
        public float SizeMetres { get; }
        /// <summary>Vertical range of the Unity terrain, m; heights are clamped to [0, HeightScale].</summary>
        public float HeightScale { get; }
        /// <summary>Distance between neighbouring samples, m.</summary>
        public float Spacing { get; }
        /// <summary>World X (and Z) of cell 0.</summary>
        public float Origin { get; }
        public float[] Heights { get; }

        /// <summary>World coordinate of grid column/row <paramref name="index"/>.</summary>
        public float CellToWorld(int index) => Origin + index * Spacing;

        /// <summary>Bilinear height at a world position, clamped to the grid bounds.</summary>
        public float Sample(float worldX, float worldZ)
        {
            int last = Resolution - 1;
            float fx = Mathf.Clamp((worldX - Origin) / Spacing, 0f, last);
            float fz = Mathf.Clamp((worldZ - Origin) / Spacing, 0f, last);
            int ix = Mathf.Min((int)fx, last - 1);
            int iz = Mathf.Min((int)fz, last - 1);
            float tx = fx - ix;
            float tz = fz - iz;
            int i = iz * Resolution + ix;
            float h00 = Heights[i], h10 = Heights[i + 1];
            float h01 = Heights[i + Resolution], h11 = Heights[i + Resolution + 1];
            float a = h00 + (h10 - h00) * tx;
            float b = h01 + (h11 - h01) * tx;
            return a + (b - a) * tz;
        }

        /// <summary>Ground slope in degrees from central differences one sample apart.</summary>
        public float SlopeDeg(float worldX, float worldZ)
        {
            float d = Spacing;
            float gx = (Sample(worldX + d, worldZ) - Sample(worldX - d, worldZ)) / (2f * d);
            float gz = (Sample(worldX, worldZ + d) - Sample(worldX, worldZ - d)) / (2f * d);
            return Mathf.Atan(Mathf.Sqrt(gx * gx + gz * gz)) * Mathf.Rad2Deg;
        }
    }
}
