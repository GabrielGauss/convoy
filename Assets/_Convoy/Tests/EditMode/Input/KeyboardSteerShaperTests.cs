using NUnit.Framework;
using UnityEngine;

namespace Convoy.Input.Tests
{
    /// <summary>
    /// Parity tests: KeyboardSteerShaper + PilotInputMath must reproduce PilotInputSource.Update exactly,
    /// so driving the keyboard in Sandbox/Sprint_01 feels identical after F-03c (ADR-002 section 6).
    /// </summary>
    public class KeyboardSteerShaperTests
    {
        private const float Dt = 0.02f;
        private const float RampUp = 4f;
        private const float Return = 6f;
        private const float Deadzone = 0.12f;
        private const float Tolerance = 1e-5f;

        /// <summary>The steering part of PilotInputSource.Update, copied verbatim (only field names adapted).</summary>
        private sealed class ReferenceSteer
        {
            private readonly float _keyboardSteerSpeed = RampUp;
            private readonly float _keyboardSteerReturn = Return;
            private readonly float _stickDeadzone = Deadzone;
            private float _keyboardSteer;

            public float Update(float keys, float stick, float deltaTime)
            {
                float rate = Mathf.Approximately(keys, 0f) || Mathf.Sign(keys) != Mathf.Sign(_keyboardSteer)
                    ? _keyboardSteerReturn
                    : _keyboardSteerSpeed;
                _keyboardSteer = Mathf.MoveTowards(_keyboardSteer, keys, rate * deltaTime);

                if (Mathf.Abs(stick) < _stickDeadzone) stick = 0f;

                return Mathf.Abs(stick) > Mathf.Abs(_keyboardSteer) ? stick : _keyboardSteer;
            }
        }

        private struct Frame
        {
            public float Keys;
            public float Stick;
            public Frame(float keys, float stick) { Keys = keys; Stick = stick; }
        }

        private static void AddFrames(System.Collections.Generic.List<Frame> list, int count, float keys, float stick)
        {
            for (int i = 0; i < count; i++) list.Add(new Frame(keys, stick));
        }

        private static void AssertParity(System.Collections.Generic.List<Frame> frames)
        {
            var reference = new ReferenceSteer();
            var shaper = new KeyboardSteerShaper(RampUp, Return);

            for (int i = 0; i < frames.Count; i++)
            {
                float expected = reference.Update(frames[i].Keys, frames[i].Stick, Dt);

                // Same pipeline the router will run: shape keys, deadzone the stick, combine.
                float keyboard = shaper.Step(frames[i].Keys, Dt);
                float stick = PilotInputMath.ApplyDeadzone(frames[i].Stick, Deadzone);
                float actual = PilotInputMath.CombineSteer(stick, keyboard);

                Assert.AreEqual(expected, actual, Tolerance, $"frame {i}");
            }
        }

        [Test]
        public void HoldRight_HalfSecond_ThenRelease_MatchesReference()
        {
            var f = new System.Collections.Generic.List<Frame>();
            AddFrames(f, 25, 1f, 0f);    // 0.5 s hold right
            AddFrames(f, 40, 0f, 0f);    // release, wheel returns to centre
            AssertParity(f);
        }

        [Test]
        public void TapLeft_MatchesReference()
        {
            var f = new System.Collections.Generic.List<Frame>();
            AddFrames(f, 5, -1f, 0f);    // 0.1 s tap
            AddFrames(f, 30, 0f, 0f);
            AssertParity(f);
        }

        [Test]
        public void ReverseDirection_MatchesReference()
        {
            var f = new System.Collections.Generic.List<Frame>();
            AddFrames(f, 20, 1f, 0f);
            AddFrames(f, 20, -1f, 0f);   // direct reversal uses the fast return rate
            AddFrames(f, 20, 1f, 0f);
            AssertParity(f);
        }

        [Test]
        public void StickInsideDeadzone_MatchesReference_AndIsZero()
        {
            var f = new System.Collections.Generic.List<Frame>();
            AddFrames(f, 10, 0f, 0.1f);
            AssertParity(f);

            var shaper = new KeyboardSteerShaper(RampUp, Return);
            float steer = PilotInputMath.CombineSteer(PilotInputMath.ApplyDeadzone(0.1f, Deadzone), shaper.Step(0f, Dt));
            Assert.AreEqual(0f, steer, Tolerance);
        }

        [Test]
        public void StickVersusKeys_LargerMagnitudeWins_MatchesReference()
        {
            var f = new System.Collections.Generic.List<Frame>();
            AddFrames(f, 10, 1f, 0.8f);     // keys still ramping up: stick wins at first
            AddFrames(f, 40, 1f, 0.8f);     // keys overtake 0.8
            AddFrames(f, 10, 1f, -0.8f);    // opposing stick
            AddFrames(f, 10, 0f, -0.8f);
            AssertParity(f);
        }

        [Test]
        public void RampUp_ReachesFullLockInQuarterSecond()
        {
            var shaper = new KeyboardSteerShaper(RampUp, Return);
            for (int i = 0; i < 12; i++) shaper.Step(1f, Dt);   // 0.24 s at 4/s -> 0.96
            Assert.AreEqual(0.96f, shaper.Value, 1e-4f);
            shaper.Step(1f, Dt);
            shaper.Step(1f, Dt);
            Assert.AreEqual(1f, shaper.Value, Tolerance, "MoveTowards never overshoots the target");
        }

        [Test]
        public void Reset_ReturnsToCentre()
        {
            var shaper = new KeyboardSteerShaper(RampUp, Return);
            shaper.Step(1f, 0.1f);
            Assert.AreNotEqual(0f, shaper.Value);
            shaper.Reset();
            Assert.AreEqual(0f, shaper.Value);
        }
    }
}
