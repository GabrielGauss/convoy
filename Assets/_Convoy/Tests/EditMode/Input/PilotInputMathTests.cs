using NUnit.Framework;

namespace Convoy.Input.Tests
{
    public class PilotInputMathTests
    {
        [TestCase(1f, 0f, 1f)]
        [TestCase(0f, 1f, -1f)]
        [TestCase(0.75f, 0.25f, 0.5f)]
        [TestCase(1f, 1f, 0f)]
        [TestCase(2f, 0f, 1f)]    // W and right trigger together: clamped
        [TestCase(0f, 2f, -1f)]
        public void Throttle_IsAccelerateMinusBrake_Clamped(float accelerate, float brake, float expected)
        {
            Assert.AreEqual(expected, PilotInputMath.Throttle(accelerate, brake), 1e-6f);
        }

        [TestCase(0.1f, 0.12f, 0f)]
        [TestCase(-0.1f, 0.12f, 0f)]
        [TestCase(0.12f, 0.12f, 0.12f)]   // exactly at the deadzone passes (same as PilotInputSource: '<' not '<=')
        [TestCase(0.5f, 0.12f, 0.5f)]     // no rescaling
        [TestCase(-0.8f, 0.12f, -0.8f)]
        [TestCase(0f, 0f, 0f)]
        public void ApplyDeadzone_ZeroesSmallValuesOnly(float value, float deadzone, float expected)
        {
            Assert.AreEqual(expected, PilotInputMath.ApplyDeadzone(value, deadzone), 1e-6f);
        }

        [TestCase(0.8f, 0.5f, 0.8f)]
        [TestCase(0.3f, 0.5f, 0.5f)]
        [TestCase(-0.9f, 0.4f, -0.9f)]
        [TestCase(0.4f, -0.9f, -0.9f)]
        [TestCase(0f, 0f, 0f)]
        public void CombineSteer_LargerMagnitudeWins(float stick, float keyboard, float expected)
        {
            Assert.AreEqual(expected, PilotInputMath.CombineSteer(stick, keyboard), 1e-6f);
        }

        [Test]
        public void CombineSteer_Tie_GoesToKeyboard()
        {
            Assert.AreEqual(-0.5f, PilotInputMath.CombineSteer(0.5f, -0.5f), 1e-6f);
        }
    }
}
