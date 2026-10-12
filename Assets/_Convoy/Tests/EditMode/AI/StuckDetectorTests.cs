using NUnit.Framework;
using UnityEngine;

namespace Convoy.AI.Tests
{
    public class StuckDetectorTests
    {
        private const float Dt = 0.02f;

        private AiDriverProfile _profile;
        private StuckDetector _detector;

        [SetUp]
        public void SetUp()
        {
            // Defaults: stuckSpeed 1, stuckTime 2.5, reverseTime 1.5, maxReverseAttempts 2, offPath 25, wrongWay 4.
            _profile = ScriptableObject.CreateInstance<AiDriverProfile>();
            _detector = new StuckDetector(_profile);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_profile);
        }

        /// <summary>Steps until the mode changes or <paramref name="maxSeconds"/> pass; returns seconds elapsed.</summary>
        private float RunUntilModeChanges(float maxSeconds, float speed, float throttle, float lateral = 0f, float progress = 0f)
        {
            AiDriveMode start = _detector.Mode;
            float t = 0f;
            while (t < maxSeconds)
            {
                _detector.Step(speed, throttle, lateral, progress, Dt);
                t += Dt;
                if (_detector.Mode != start) break;
            }
            return t;
        }

        [Test]
        public void StartsDriving()
        {
            Assert.AreEqual(AiDriveMode.Driving, _detector.Mode);
        }

        [Test]
        public void Stuck_Reverses_ThenRequestsResetAfterMaxAttempts()
        {
            for (int attempt = 1; attempt <= _profile.maxReverseAttempts; attempt++)
            {
                float stuckFor = RunUntilModeChanges(10f, speed: 0f, throttle: 1f);
                Assert.AreEqual(AiDriveMode.Reversing, _detector.Mode, $"attempt {attempt}");
                Assert.AreEqual(_profile.stuckTime, stuckFor, 2f * Dt, $"attempt {attempt}");
                Assert.AreEqual(attempt, _detector.ReverseAttempts);

                float reversedFor = RunUntilModeChanges(10f, speed: -0.5f, throttle: 1f, progress: -0.01f);
                Assert.AreEqual(AiDriveMode.Driving, _detector.Mode);
                Assert.AreEqual(_profile.reverseTime, reversedFor, 2f * Dt);
            }

            RunUntilModeChanges(10f, speed: 0f, throttle: 1f);
            Assert.AreEqual(AiDriveMode.RequestReset, _detector.Mode);
        }

        [Test]
        public void SlowWhileNotAskingForThrottle_IsNotStuck()
        {
            RunUntilModeChanges(10f, speed: 0f, throttle: 0.2f); // e.g. braking hard for a corner
            Assert.AreEqual(AiDriveMode.Driving, _detector.Mode);
        }

        [Test]
        public void OffPath_RequestsResetImmediately()
        {
            _detector.Step(10f, 1f, _profile.offPathResetDistance + 1f, 0.2f, Dt);
            Assert.AreEqual(AiDriveMode.RequestReset, _detector.Mode);
        }

        [Test]
        public void WrongWay_RequestsResetAfterWrongWayTime()
        {
            float t = RunUntilModeChanges(10f, speed: 10f, throttle: 1f, progress: -0.2f);
            Assert.AreEqual(AiDriveMode.RequestReset, _detector.Mode);
            Assert.AreEqual(_profile.wrongWayTime, t, 2f * Dt);
        }

        [Test]
        public void RequestReset_IsSticky_UntilNotified()
        {
            _detector.Step(10f, 1f, 100f, 0f, Dt);
            RunUntilModeChanges(1f, speed: 10f, throttle: 1f, progress: 0.2f);
            Assert.AreEqual(AiDriveMode.RequestReset, _detector.Mode);

            _detector.NotifyResetIssued();
            Assert.AreEqual(AiDriveMode.Driving, _detector.Mode);
            Assert.AreEqual(0, _detector.ReverseAttempts);
        }

        [Test]
        public void HealthyDriving_ClearsReverseAttempts()
        {
            RunUntilModeChanges(10f, speed: 0f, throttle: 1f);
            RunUntilModeChanges(10f, speed: -0.5f, throttle: 1f);
            Assert.AreEqual(1, _detector.ReverseAttempts);

            RunUntilModeChanges(_profile.stuckTime + 0.1f, speed: 10f, throttle: 1f, progress: 0.2f);
            Assert.AreEqual(0, _detector.ReverseAttempts);
        }

        [Test]
        public void ZeroReverseAttempts_GoesStraightToReset()
        {
            _profile.maxReverseAttempts = 0;
            RunUntilModeChanges(10f, speed: 0f, throttle: 1f);
            Assert.AreEqual(AiDriveMode.RequestReset, _detector.Mode);
        }
    }
}
