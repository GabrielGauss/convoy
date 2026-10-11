using Convoy.Vehicle;
using NUnit.Framework;
using UnityEngine;

namespace Convoy.Tests
{
    public class VehicleRecoveryTests
    {
        private const float Dt = 0.02f;
        private const float Eps = 1e-3f;

        private VehicleConfig _config;
        private VehicleRecovery _recovery;

        private static readonly Quaternion Upright = Quaternion.identity;
        private static readonly Quaternion OnRoof = Quaternion.Euler(0f, 0f, 180f);

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<VehicleConfig>();
            _recovery = new VehicleRecovery(_config);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
        }

        private static RecoveryInput Input(Quaternion rotation, float speed = 0f, int grounded = 4,
            bool reset = false, Vector3 position = default)
        {
            return new RecoveryInput
            {
                DeltaTime = Dt,
                Position = position,
                Rotation = rotation,
                Speed = speed,
                GroundedWheels = grounded,
                TotalWheels = 4,
                ResetRequested = reset,
            };
        }

        private RecoveryAction Run(float seconds, RecoveryInput input)
        {
            RecoveryAction last = RecoveryAction.None;
            int steps = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < steps; i++)
            {
                last = _recovery.Step(input);
                if (last != RecoveryAction.None) return last;
            }
            return last;
        }

        // --- Upside-down detection -------------------------------------

        [Test]
        public void IsUpsideDown_FalseWhenUpright()
        {
            Assert.That(_recovery.IsUpsideDown(Upright), Is.False);
        }

        [Test]
        public void IsUpsideDown_TrueOnRoof()
        {
            Assert.That(_recovery.IsUpsideDown(OnRoof), Is.True);
        }

        [Test]
        public void IsUpsideDown_TrueOnSide()
        {
            Assert.That(_recovery.IsUpsideDown(Quaternion.Euler(0f, 0f, 90f)), Is.True);
        }

        [Test]
        public void IsUpsideDown_FalseOnSteepRamp()
        {
            Assert.That(_recovery.IsUpsideDown(Quaternion.Euler(-40f, 0f, 0f)), Is.False);
        }

        // --- Auto-right --------------------------------------------------

        [Test]
        public void AutoRight_DoesNotTriggerBeforeDelay()
        {
            Assert.That(Run(_config.autoRightDelay - 0.1f, Input(OnRoof, grounded: 0)), Is.EqualTo(RecoveryAction.None));
        }

        [Test]
        public void AutoRight_TriggersAfterDelay()
        {
            Assert.That(Run(_config.autoRightDelay + 0.1f, Input(OnRoof, grounded: 0)), Is.EqualTo(RecoveryAction.AutoRight));
        }

        [Test]
        public void AutoRight_IgnoresFastTumbles()
        {
            var tumbling = Input(OnRoof, speed: _config.autoRightMaxSpeed + 5f, grounded: 0);
            Assert.That(Run(_config.autoRightDelay * 3f, tumbling), Is.EqualTo(RecoveryAction.None));
        }

        [Test]
        public void AutoRight_TimerResetsWhenVehicleRightsItself()
        {
            Run(_config.autoRightDelay - 0.2f, Input(OnRoof, grounded: 0));
            _recovery.Step(Input(Upright));
            Assert.That(_recovery.UpsideDownTime, Is.EqualTo(0f));
        }

        [Test]
        public void AutoRightPose_KeepsHeadingAndRemovesRoll()
        {
            Quaternion rolledEast = Quaternion.Euler(0f, 90f, 180f);
            Pose pose = _recovery.AutoRightPose(new Vector3(5f, 0f, 5f), rolledEast);

            Assert.That(Vector3.Dot(pose.rotation * Vector3.up, Vector3.up), Is.EqualTo(1f).Within(Eps));
            Vector3 heading = pose.rotation * Vector3.forward;
            Assert.That(Mathf.Abs(Vector3.Dot(heading, Vector3.right)), Is.EqualTo(1f).Within(Eps));
            Assert.That(pose.position.y, Is.EqualTo(_config.recoveryLift).Within(Eps));
        }

        [Test]
        public void UprightYaw_HandlesNoseStraightDown()
        {
            Quaternion noseDown = Quaternion.Euler(90f, 0f, 0f);
            Quaternion result = VehicleRecovery.UprightYaw(noseDown);
            Assert.That(Vector3.Dot(result * Vector3.up, Vector3.up), Is.EqualTo(1f).Within(Eps));
        }

        // --- Manual reset & cooldown ------------------------------------

        [Test]
        public void ManualReset_TriggersImmediately()
        {
            Assert.That(_recovery.Step(Input(Upright, reset: true)), Is.EqualTo(RecoveryAction.Reset));
        }

        [Test]
        public void ManualReset_IsBlockedDuringCooldown()
        {
            _recovery.Step(Input(Upright, reset: true));
            Assert.That(_recovery.Step(Input(Upright, reset: true)), Is.EqualTo(RecoveryAction.None));
        }

        [Test]
        public void ManualReset_WorksAgainAfterCooldown()
        {
            _recovery.Step(Input(Upright, reset: true));
            Run(_config.resetCooldown + 0.1f, Input(Upright));
            Assert.That(_recovery.Step(Input(Upright, reset: true)), Is.EqualTo(RecoveryAction.Reset));
        }

        [Test]
        public void AutoRight_AlsoStartsCooldown()
        {
            Run(_config.autoRightDelay + 0.1f, Input(OnRoof, grounded: 0));
            Assert.That(_recovery.Step(Input(Upright, reset: true)), Is.EqualTo(RecoveryAction.None));
        }

        // --- Safe pose ---------------------------------------------------

        [Test]
        public void SafePose_NotRecordedWhileAirborne()
        {
            Run(1f, Input(Upright, grounded: 2));
            Assert.That(_recovery.HasSafePose, Is.False);
        }

        [Test]
        public void SafePose_RecordedWhenAllWheelsDown()
        {
            _recovery.Step(Input(Upright, position: new Vector3(1f, 0f, 2f)));
            Assert.That(_recovery.HasSafePose, Is.True);
            Assert.That(_recovery.SafePose.position, Is.EqualTo(new Vector3(1f, 0f, 2f)));
        }

        [Test]
        public void SafePose_PrefersOlderSampleOverCrashSpot()
        {
            _recovery.Step(Input(Upright, position: new Vector3(0f, 0f, 0f)));
            Run(_config.safePoseInterval + Dt, Input(Upright, position: new Vector3(0f, 0f, 30f)));
            Assert.That(_recovery.SafePose.position.z, Is.EqualTo(0f).Within(Eps));
        }
    }
}
