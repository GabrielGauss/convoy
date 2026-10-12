using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Convoy.Level.Tests
{
    public class WallFractureLayoutTests
    {
        // Same dimensions as the LV-01c prefab: 8 x 5 x 1.5 m.
        private static readonly Vector3 Wall = new Vector3(8f, 5f, 1.5f);

        private static List<ChunkSpec> Make(int cols, int rows, float jitter, int seed)
        {
            var list = new List<ChunkSpec>();
            WallFractureLayout.Generate(Wall, cols, rows, jitter, seed, list);
            return list;
        }

        [Test]
        public void Count_IsColumnsTimesRows()
        {
            var list = new List<ChunkSpec>();
            int n = WallFractureLayout.Generate(Wall, 4, 3, 0.12f, 1, list);
            Assert.That(n, Is.EqualTo(12));
            Assert.That(list.Count, Is.EqualTo(12));
        }

        [Test]
        public void Generate_ClearsPreviousContents()
        {
            var list = Make(4, 3, 0.12f, 1);
            WallFractureLayout.Generate(Wall, 2, 2, 0.1f, 1, list);
            Assert.That(list.Count, Is.EqualTo(4));
        }

        [TestCase(4, 3, 0.12f, 1)]
        [TestCase(6, 5, 0.3f, 77)]
        [TestCase(1, 1, 0.2f, 3)]
        public void Chunks_TileWallVolumeExactly(int cols, int rows, float jitter, int seed)
        {
            float total = 0f;
            foreach (var c in Make(cols, rows, jitter, seed)) total += c.Size.x * c.Size.y * c.Size.z;
            float wall = Wall.x * Wall.y * Wall.z;
            Assert.That(total, Is.EqualTo(wall).Within(wall * 0.001f));
        }

        [Test]
        public void Chunks_PreRotationBoundsInsideWall()
        {
            const float eps = 1e-4f;
            foreach (var c in Make(5, 4, 0.3f, 9))
            {
                Assert.That(c.Size.x, Is.GreaterThan(0f));
                Assert.That(c.Size.y, Is.GreaterThan(0f));
                Assert.That(c.LocalCenter.x - c.Size.x * 0.5f, Is.GreaterThanOrEqualTo(-Wall.x * 0.5f - eps));
                Assert.That(c.LocalCenter.x + c.Size.x * 0.5f, Is.LessThanOrEqualTo(Wall.x * 0.5f + eps));
                Assert.That(c.LocalCenter.y - c.Size.y * 0.5f, Is.GreaterThanOrEqualTo(-Wall.y * 0.5f - eps));
                Assert.That(c.LocalCenter.y + c.Size.y * 0.5f, Is.LessThanOrEqualTo(Wall.y * 0.5f + eps));
                Assert.That(c.Size.z, Is.EqualTo(Wall.z));
            }
        }

        [Test]
        public void Chunks_DoNotOverlap_PreRotation()
        {
            var list = Make(4, 3, 0.3f, 5);
            for (int i = 0; i < list.Count; i++)
            {
                for (int j = i + 1; j < list.Count; j++)
                {
                    float ox = Overlap(list[i].LocalCenter.x, list[i].Size.x, list[j].LocalCenter.x, list[j].Size.x);
                    float oy = Overlap(list[i].LocalCenter.y, list[i].Size.y, list[j].LocalCenter.y, list[j].Size.y);
                    Assert.That(ox > 1e-4f && oy > 1e-4f, Is.False, $"chunks {i} and {j} overlap");
                }
            }
        }

        [Test]
        public void SameSeed_IdenticalOutput()
        {
            var a = Make(4, 3, 0.12f, 123);
            var b = Make(4, 3, 0.12f, 123);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.That(a[i].LocalCenter, Is.EqualTo(b[i].LocalCenter));
                Assert.That(a[i].Size, Is.EqualTo(b[i].Size));
                Assert.That(a[i].LocalEuler, Is.EqualTo(b[i].LocalEuler));
            }
        }

        [Test]
        public void DifferentSeed_DifferentSeams()
        {
            var a = Make(4, 3, 0.12f, 1);
            var b = Make(4, 3, 0.12f, 2);
            bool anyDiff = false;
            for (int i = 0; i < a.Count; i++)
                if (a[i].LocalCenter != b[i].LocalCenter || a[i].Size != b[i].Size) anyDiff = true;
            Assert.That(anyDiff, Is.True);
        }

        [Test]
        public void ZeroJitter_RegularGrid()
        {
            var list = Make(4, 3, 0f, 1);
            foreach (var c in list)
            {
                Assert.That(c.Size.x, Is.EqualTo(Wall.x / 4f).Within(1e-4f));
                Assert.That(c.Size.y, Is.EqualTo(Wall.y / 3f).Within(1e-4f));
            }
            // Column-major order: first chunk is the bottom-left cell.
            Assert.That(list[0].LocalCenter.x, Is.EqualTo(-Wall.x * 0.5f + Wall.x / 8f).Within(1e-4f));
            Assert.That(list[0].LocalCenter.y, Is.EqualTo(-Wall.y * 0.5f + Wall.y / 6f).Within(1e-4f));
        }

        [Test]
        public void Tilt_IsAtMostFourDegreesPerAxis()
        {
            foreach (var c in Make(6, 5, 0.3f, 11))
            {
                Assert.That(Mathf.Abs(c.LocalEuler.x), Is.LessThanOrEqualTo(4f + 1e-4f));
                Assert.That(Mathf.Abs(c.LocalEuler.y), Is.LessThanOrEqualTo(4f + 1e-4f));
                Assert.That(Mathf.Abs(c.LocalEuler.z), Is.LessThanOrEqualTo(4f + 1e-4f));
            }
        }

        [Test]
        public void InvalidCounts_ClampToOne()
        {
            var list = new List<ChunkSpec>();
            Assert.That(WallFractureLayout.Generate(Wall, 0, -2, 0.1f, 1, list), Is.EqualTo(1));
        }

        [Test]
        public void NullList_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => WallFractureLayout.Generate(Wall, 2, 2, 0.1f, 1, null));
        }

        private static float Overlap(float c1, float s1, float c2, float s2)
        {
            return Mathf.Min(c1 + s1 * 0.5f, c2 + s2 * 0.5f) - Mathf.Max(c1 - s1 * 0.5f, c2 - s2 * 0.5f);
        }
    }
}
