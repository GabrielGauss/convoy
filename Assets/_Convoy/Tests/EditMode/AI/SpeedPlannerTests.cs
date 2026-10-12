using NUnit.Framework;
using UnityEngine;

namespace Convoy.AI.Tests
{
    public class SpeedPlannerTests
    {
        private const float TopSpeed = 40f;

        private AiDriverProfile _profile;

        [SetUp]
        public void SetUp()
        {
            _profile = ScriptableObject.CreateInstance<AiDriverProfile>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_profile);
        }

        private float CornerSpeed(float radius)
        {
            return Mathf.Sqrt(_profile.maxLateralAccel * _profile.cornerSpeedScale * radius);
        }

        [Test]
        public void TargetSpeed_OnAStraight_IsTopSpeed()
        {
            PolylinePath path = TestPaths.Straight(500f);
            Assert.AreEqual(TopSpeed, SpeedPlanner.TargetSpeed(path, 100f, TopSpeed, _profile), 1e-3f);
        }

        [Test]
        public void TargetSpeed_CornerAt50mAhead_RequiresBraking()
        {
            // The 20 m-radius arc starts half a curvature span before the 50 m sample, so the sample at
            // exactly 50 m ahead already reads the full corner curvature.
            float arcStart = 50f - _profile.curvatureSpan * 0.5f;
            PolylinePath path = TestPaths.StraightThenRightTurn(arcStart, 20f);
            float cornerSpeed = CornerSpeed(20f); // ≈ 16 m/s with defaults

            float target = SpeedPlanner.TargetSpeed(path, 0f, TopSpeed, _profile);

            float brakingBound = Mathf.Sqrt(cornerSpeed * cornerSpeed + 2f * _profile.brakingDecel * 50f); // ≈ 38 m/s
            Assert.Less(brakingBound, TopSpeed, "test setup: this corner must matter from 50 m out");
            Assert.LessOrEqual(target, brakingBound + 1e-3f);
            Assert.Greater(target, cornerSpeed, "no need to be at corner speed already, 50 m early");
        }

        [Test]
        public void TargetSpeed_InsideTheCorner_IsAboutCornerSpeed()
        {
            PolylinePath path = TestPaths.StraightThenRightTurn(50f, 20f);
            float midArc = 50f + Mathf.PI * 20f * 0.25f;
            float cornerSpeed = CornerSpeed(20f);
            Assert.AreEqual(cornerSpeed, SpeedPlanner.TargetSpeed(path, midArc, TopSpeed, _profile), cornerSpeed * 0.1f);
        }

        [Test]
        public void TargetSpeed_LowerCornerSpeedScale_IsSlower()
        {
            PolylinePath path = TestPaths.StraightThenRightTurn(50f, 20f);
            _profile.cornerSpeedScale = 0.98f;
            float aggressive = SpeedPlanner.TargetSpeed(path, 20f, TopSpeed, _profile);
            _profile.cornerSpeedScale = 0.85f;
            float steady = SpeedPlanner.TargetSpeed(path, 20f, TopSpeed, _profile);
            Assert.Less(steady, aggressive);
        }

        [Test]
        public void ClearForBoost_TrueOnStraight_FalseBeforeCorner_FalseWhenDisabled()
        {
            Assert.IsTrue(SpeedPlanner.ClearForBoost(TestPaths.Straight(500f), 0f, TopSpeed, _profile));
            Assert.IsFalse(SpeedPlanner.ClearForBoost(TestPaths.StraightThenRightTurn(50f, 20f), 0f, TopSpeed, _profile));

            _profile.useBoost = false;
            Assert.IsFalse(SpeedPlanner.ClearForBoost(TestPaths.Straight(500f), 0f, TopSpeed, _profile));
        }
    }
}
