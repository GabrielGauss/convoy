using System.IO;
using Convoy.Level;
using UnityEditor;
using UnityEngine;

namespace Convoy.Editor
{
    /// <summary>
    /// Creates the four greybox terrain layers for Páramo (TR-01b): a 256² PNG per layer (flat colour + seeded grain)
    /// and a <see cref="TerrainLayer"/> asset pointing at it. Free, no asset purchases (D-021).
    /// Why generated: final terrain art is out of M1 scope; flat colours make slope/biome splat blends easy to read
    /// while tuning. Created once: if a PNG or .terrainlayer already exists it is left alone, so an artist can repaint
    /// or retune it without the generator overwriting their work. Layer order matches
    /// <see cref="TerrainSynthesis.ComputeSplat"/>: 0 sand, 1 basalt, 2 rust, 3 toxic.
    /// </summary>
    public static class TerrainTextureFactory
    {
        private const int TextureSize = 256;

        // Low-frequency blotches tile seamlessly because the value-noise lattice wraps at this many cells.
        private const int BlotchCells = 8;

        private struct LayerSpec
        {
            public string Name;
            public Color Colour;
            public float Grain;      // per-pixel brightness jitter, fraction of the colour
            public float Blotch;     // low-frequency brightness variation, fraction of the colour
            public float TileMetres; // world size one texture repeat covers
        }

        // Placeholder art values (not gameplay numbers): colours picked to match the Sprint_01 palette.
        private static readonly LayerSpec[] Specs =
        {
            new LayerSpec { Name = "Sand",   Colour = new Color(0.76f, 0.60f, 0.42f), Grain = 0.10f, Blotch = 0.10f, TileMetres = 12f },
            new LayerSpec { Name = "Basalt", Colour = new Color(0.22f, 0.19f, 0.18f), Grain = 0.18f, Blotch = 0.15f, TileMetres = 10f },
            new LayerSpec { Name = "Rust",   Colour = new Color(0.55f, 0.28f, 0.15f), Grain = 0.12f, Blotch = 0.18f, TileMetres = 14f },
            new LayerSpec { Name = "Toxic",  Colour = new Color(0.36f, 0.58f, 0.12f), Grain = 0.08f, Blotch = 0.20f, TileMetres = 8f },
        };

        /// <summary>Number of layers <see cref="EnsureLayers"/> returns (= splat channels of the synthesis).</summary>
        public static int LayerCount => Specs.Length;

        /// <summary>Loads or creates the layers under <paramref name="folder"/> (an "Assets/..." path that must exist).</summary>
        public static TerrainLayer[] EnsureLayers(string folder, int seed)
        {
            var layers = new TerrainLayer[Specs.Length];
            for (int i = 0; i < Specs.Length; i++)
            {
                LayerSpec spec = Specs[i];
                string texturePath = $"{folder}/{spec.Name}.png";
                string layerPath = $"{folder}/{spec.Name}.terrainlayer";

                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                if (texture == null)
                {
                    // Seed per layer so layers do not share the same grain pattern.
                    WritePng(texturePath, spec, seed * 977 + i * 131 + 17);
                    AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
                    texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                }

                TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
                if (layer == null)
                {
                    layer = new TerrainLayer
                    {
                        diffuseTexture = texture,
                        tileSize = new Vector2(spec.TileMetres, spec.TileMetres),
                    };
                    AssetDatabase.CreateAsset(layer, layerPath);
                }
                else if (layer.diffuseTexture == null)
                {
                    layer.diffuseTexture = texture;
                    EditorUtility.SetDirty(layer);
                }
                layers[i] = layer;
            }
            return layers;
        }

        private static void WritePng(string assetPath, LayerSpec spec, int seed)
        {
            var rng = new DeterministicRng(seed);
            var lattice = new float[BlotchCells * BlotchCells];
            for (int i = 0; i < lattice.Length; i++) lattice[i] = rng.NextFloat01() * 2f - 1f;

            var pixels = new Color32[TextureSize * TextureSize];
            float cell = (float)TextureSize / BlotchCells;
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float blotch = WrappedValueNoise(lattice, x / cell, y / cell);
                    float grain = rng.NextFloat01() * 2f - 1f;
                    float k = 1f + blotch * spec.Blotch + grain * spec.Grain;
                    Color c = spec.Colour * k;
                    c.a = 1f;
                    pixels[y * TextureSize + x] = c;
                }
            }

            // Texture2D is a temporary only used to encode the PNG; destroyed right after so no editor leak.
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels32(pixels);
                texture.Apply(false);
                byte[] png = texture.EncodeToPNG();
                File.WriteAllBytes(ToFullPath(assetPath), png);
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        /// <summary>Bilinear value noise on a lattice that wraps, so the texture tiles without seams.</summary>
        private static float WrappedValueNoise(float[] lattice, float u, float v)
        {
            int x0 = Mathf.FloorToInt(u), y0 = Mathf.FloorToInt(v);
            float tx = u - x0, ty = v - y0;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            float a = Lattice(lattice, x0, y0), b = Lattice(lattice, x0 + 1, y0);
            float c = Lattice(lattice, x0, y0 + 1), d = Lattice(lattice, x0 + 1, y0 + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        private static float Lattice(float[] lattice, int x, int y)
        {
            x = ((x % BlotchCells) + BlotchCells) % BlotchCells;
            y = ((y % BlotchCells) + BlotchCells) % BlotchCells;
            return lattice[y * BlotchCells + x];
        }

        /// <summary>"Assets/a/b.png" → absolute disk path (File IO needs it; AssetDatabase uses the project path).</summary>
        internal static string ToFullPath(string assetPath)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(projectRoot ?? string.Empty, assetPath);
        }
    }
}
