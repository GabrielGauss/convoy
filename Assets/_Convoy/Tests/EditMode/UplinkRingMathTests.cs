using Convoy.Race;
using NUnit.Framework;

namespace Convoy.Tests
{
    public class UplinkRingMathTests
    {
        private const int Segments = 24;

        [TestCase(0f, 0)]
        [TestCase(0.01f, 1)]      // first physics step of a hold already lights a segment
        [TestCase(0.5f, 12)]
        [TestCase(1f, 24)]
        [TestCase(1.5f, 24)]      // > 1 clamps
        [TestCase(-0.2f, 0)]
        public void LitSegments_MatchesSpec(float progress, int expected)
        {
            Assert.That(UplinkRingMath.LitSegments(progress, Segments), Is.EqualTo(expected));
        }

        [Test]
        public void LitSegments_NaN_IsZero()
        {
            Assert.That(UplinkRingMath.LitSegments(float.NaN, Segments), Is.EqualTo(0));
        }

        [Test]
        public void LitSegments_NoSegments_IsZero()
        {
            Assert.That(UplinkRingMath.LitSegments(0.5f, 0), Is.EqualTo(0));
            Assert.That(UplinkRingMath.LitSegments(0.5f, -3), Is.EqualTo(0));
        }
    }
}
