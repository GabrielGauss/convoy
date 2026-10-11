using Convoy.Presentation;
using NUnit.Framework;

namespace Convoy.Tests
{
    public class GaugeMathTests
    {
        [Test]
        public void NeedleAngle_MapsZeroAndMaxToEnds()
        {
            Assert.That(GaugeMath.NeedleAngle(0f, 200f, -135f, 270f), Is.EqualTo(-135f).Within(1e-4f));
            Assert.That(GaugeMath.NeedleAngle(200f, 200f, -135f, 270f), Is.EqualTo(135f).Within(1e-4f));
        }

        [Test]
        public void NeedleAngle_HalfwayPointsStraightUp()
        {
            Assert.That(GaugeMath.NeedleAngle(100f, 200f, -135f, 270f), Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void NeedleAngle_ClampsOverspeedAndNegatives()
        {
            Assert.That(GaugeMath.NeedleAngle(500f, 200f, -135f, 270f), Is.EqualTo(135f).Within(1e-4f));
            Assert.That(GaugeMath.NeedleAngle(-10f, 200f, -135f, 270f), Is.EqualTo(-135f).Within(1e-4f));
            Assert.That(GaugeMath.NeedleAngle(10f, 0f, -135f, 270f), Is.EqualTo(-135f));
        }

        [Test]
        public void DialMax_RoundsUpToStep()
        {
            Assert.That(GaugeMath.DialMax(187.2f, 20f), Is.EqualTo(200f));
            Assert.That(GaugeMath.DialMax(200f, 20f), Is.EqualTo(200f));
            Assert.That(GaugeMath.DialMax(0f, 20f), Is.EqualTo(20f));
        }

        [Test]
        public void DisplaySpeed_RoundsAndIsNeverNegative()
        {
            Assert.That(GaugeMath.DisplaySpeed(72.5f), Is.EqualTo(73));
            Assert.That(GaugeMath.DisplaySpeed(-30.2f), Is.EqualTo(30));
        }
    }
}
