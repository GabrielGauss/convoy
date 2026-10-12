using NUnit.Framework;
using UnityEngine;

namespace Convoy.AI.Tests
{
    public class LaneSelectorTests
    {
        private const float Dt = 0.02f;
        private const float Clear = float.PositiveInfinity;
        private const float Eps = 1e-3f;

        private AiDriverProfile _profile;

        [SetUp]
        public void SetUp()
        {
            _profile = ScriptableObject.CreateInstance<AiDriverProfile>(); // laneWidth 2.5, rate 3 m/s, check 25 m
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_profile);
        }

        private static void Run(LaneSelector lanes, float seconds, float left, float centre, float right)
        {
            int steps = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < steps; i++) lanes.Step(left, centre, right, Dt);
        }

        [Test]
        public void AllClear_StaysOnCentre()
        {
            var lanes = new LaneSelector(_profile, 4f);
            Run(lanes, 1f, Clear, Clear, Clear);
            Assert.AreEqual(0, lanes.Lane);
            Assert.AreEqual(0f, lanes.Offset, Eps);
        }

        [Test]
        public void BlockedCentre_PicksTheClearerSide()
        {
            var lanes = new LaneSelector(_profile, 4f);
            lanes.Step(8f, 10f, 20f, Dt);
            Assert.AreEqual(1, lanes.Lane, "right has more room");

            lanes = new LaneSelector(_profile, 4f);
            lanes.Step(Clear, 10f, 15f, Dt);
            Assert.AreEqual(-1, lanes.Lane, "left is clear");
        }

        [Test]
        public void Tie_PrefersCentre_ThenKeepsCurrentLaneWhileItIsClear()
        {
            var lanes = new LaneSelector(_profile, 4f);
            lanes.Step(10f, 10f, 10f, Dt);
            Assert.AreEqual(0, lanes.Lane, "equal distances → centre");

            lanes.Step(Clear, 5f, Clear, Dt);
            int dodged = lanes.Lane;
            Assert.AreNotEqual(0, dodged);

            Run(lanes, 1f, Clear, Clear, Clear);
            Assert.AreEqual(dodged, lanes.Lane, "a clear lane is kept (no chatter back and forth)");
        }

        [Test]
        public void Offset_MovesTowardTheLane_AtLaneChangeRate()
        {
            var lanes = new LaneSelector(_profile, 4f);
            lanes.Step(Clear, 5f, 0f, Dt); // → left lane, −2.5 m
            Assert.AreEqual(-1, lanes.Lane);
            Assert.AreEqual(-_profile.laneChangeRate * Dt, lanes.Offset, Eps, "one step of movement, no snap");

            Run(lanes, 0.5f, Clear, 5f, 0f);
            Assert.AreEqual(-_profile.laneChangeRate * 0.52f, lanes.Offset, 0.01f);

            Run(lanes, 2f, Clear, 5f, 0f);
            Assert.AreEqual(-_profile.laneWidth, lanes.Offset, Eps, "settles on the lane, no overshoot");
        }

        [Test]
        public void Offset_IsClampedToHalfWidth()
        {
            var lanes = new LaneSelector(_profile, 1.5f); // narrower than one lane width
            Run(lanes, 3f, 0f, 5f, Clear);
            Assert.AreEqual(1, lanes.Lane);
            Assert.AreEqual(1.5f, lanes.Offset, Eps);
        }
    }
}
