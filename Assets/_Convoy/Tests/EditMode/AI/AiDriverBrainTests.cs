using Convoy.Core;
using Convoy.Vehicle;
using NUnit.Framework;
using UnityEngine;

namespace Convoy.AI.Tests
{
    public class AiDriverBrainTests
    {
        private const float Dt = 0.02f;
        private const float HalfWidth = 4f;
        private const float Clear = float.PositiveInfinity;

        private AiDriverProfile _profile;
        private VehicleConfig _vehicle;

        [SetUp]
        public void SetUp()
        {
            _profile = ScriptableObject.CreateInstance<AiDriverProfile>();
            _vehicle = ScriptableObject.CreateInstance<VehicleConfig>(); // topSpeed 40, resetCooldown 3, reverseThreshold 0.6
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_profile);
            Object.DestroyImmediate(_vehicle);
        }

        private AiDriverBrain Brain(PolylinePath path)
        {
            return new AiDriverBrain(path, _profile, _vehicle, HalfWidth);
        }

        private static AiSensorFrame Frame(Vector3 position, Vector3 forward, float forwardSpeed)
        {
            return new AiSensorFrame
            {
                DeltaTime = Dt,
                Position = position,
                Forward = forward,
                Velocity = forward * forwardSpeed,
                ForwardSpeed = forwardSpeed,
                GroundedWheels = 4,
                ObstacleLeft = Clear,
                ObstacleCentre = Clear,
                ObstacleRight = Clear,
            };
        }

        private static int CountResets(AiDriverBrain brain, AiSensorFrame frame, float seconds)
        {
            int resets = 0;
            int steps = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < steps; i++)
                if (brain.Step(frame).Reset) resets++;
            return resets;
        }

        // --- Steering & speed ---------------------------------------------

        [Test]
        public void CarLeftOfPath_SteersRight_AndRightOfPath_SteersLeft()
        {
            AiDriverBrain brain = Brain(TestPaths.Straight(300f));
            Assert.Greater(brain.Step(Frame(new Vector3(-3f, 0f, 10f), Vector3.forward, 10f)).Steer, 0f);

            brain = Brain(TestPaths.Straight(300f));
            Assert.Less(brain.Step(Frame(new Vector3(3f, 0f, 10f), Vector3.forward, 10f)).Steer, 0f);
        }

        [Test]
        public void OnPathOnAStraight_DrivesStraight_FullThrottle_WithBoost()
        {
            AiDriverBrain brain = Brain(TestPaths.Straight(300f));
            PilotCommand cmd = brain.Step(Frame(new Vector3(0f, 0f, 10f), Vector3.forward, 0f));

            Assert.AreEqual(0f, cmd.Steer, 1e-4f);
            Assert.AreEqual(1f, cmd.Throttle, 1e-4f);
            Assert.IsTrue(cmd.Boost);
            Assert.IsFalse(cmd.Reset);
            Assert.AreEqual(10f, brain.Progress, 1e-3f);
            Assert.AreEqual(AiDriveMode.Driving, brain.Mode);
        }

        [Test]
        public void FastBeforeACorner_Brakes_AndDoesNotBoost()
        {
            AiDriverBrain brain = Brain(TestPaths.StraightThenRightTurn(50f, 20f));
            PilotCommand cmd = brain.Step(Frame(new Vector3(0f, 0f, 20f), Vector3.forward, 40f));

            Assert.Less(cmd.Throttle, 0f);
            Assert.IsFalse(cmd.Boost);
        }

        [Test]
        public void ReachedEnd_BrakesToAStop_ButNeverReverses()
        {
            AiDriverBrain brain = Brain(TestPaths.Straight(100f));
            PilotCommand moving = brain.Step(Frame(new Vector3(0f, 0f, 97f), Vector3.forward, 10f));
            Assert.IsTrue(brain.ReachedEnd);
            Assert.AreEqual(-1f, moving.Throttle);

            PilotCommand stopped = brain.Step(Frame(new Vector3(0f, 0f, 97f), Vector3.forward, 0.1f));
            Assert.AreEqual(0f, stopped.Throttle);
        }

        // --- Hold & recovery ------------------------------------------------

        [Test]
        public void Held_ReturnsNeutral_AndNeverRequestsReset()
        {
            AiDriverBrain brain = Brain(TestPaths.Straight(300f));
            brain.Held = true;

            PilotCommand cmd = brain.Step(Frame(new Vector3(-3f, 0f, 10f), Vector3.forward, 0f));
            Assert.AreEqual(0f, cmd.Throttle);
            Assert.AreEqual(0f, cmd.Steer);
            Assert.IsFalse(cmd.Boost);
            Assert.IsFalse(cmd.Handbrake);
            Assert.IsFalse(cmd.Reset);
            Assert.AreEqual(AiDriveMode.Holding, brain.Mode);

            // Standing still for a long time while held is not "stuck".
            Assert.AreEqual(0, CountResets(brain, Frame(new Vector3(0f, 0f, 10f), Vector3.forward, 0f), 20f));

            brain.Held = false;
            Assert.AreEqual(1f, brain.Step(Frame(new Vector3(0f, 0f, 10f), Vector3.forward, 0f)).Throttle, 1e-4f);
        }

        [Test]
        public void StuckCar_ReversesFirst_ThenEmitsResetExactlyOnce()
        {
            AiDriverBrain brain = Brain(TestPaths.Straight(300f));
            AiSensorFrame wedged = Frame(new Vector3(0f, 0f, 50f), Vector3.forward, 0f);

            bool reversed = false;
            int resets = 0;
            // 2 × (stuck 2.5 s + reverse 1.5 s) + stuck 2.5 s = 10.5 s to the reset; the next one can't come before 21 s.
            int steps = Mathf.RoundToInt(15f / Dt);
            for (int i = 0; i < steps; i++)
            {
                PilotCommand cmd = brain.Step(wedged);
                if (brain.Mode == AiDriveMode.Reversing && cmd.Throttle < 0f) reversed = true;
                if (cmd.Reset)
                {
                    resets++;
                    Assert.IsTrue(reversed, "reverse attempts come before a reset");
                }
            }

            Assert.AreEqual(1, resets);
            Assert.AreEqual(1, brain.ResetRequests);
        }

        [Test]
        public void OffPath_EmitsResetOnce_ThenWaitsForTheVehicleCooldown()
        {
            AiDriverBrain brain = Brain(TestPaths.Straight(300f));
            AiSensorFrame lost = Frame(new Vector3(40f, 0f, 50f), Vector3.forward, 0f); // 40 m > offPathResetDistance

            Assert.IsTrue(brain.Step(lost).Reset, "first step");
            Assert.AreEqual(0, CountResets(brain, lost, _vehicle.resetCooldown - 0.2f), "no spam during cooldown");
            Assert.AreEqual(1, CountResets(brain, lost, 0.5f), "still lost after the cooldown → one more request");
            Assert.AreEqual(2, brain.ResetRequests);
        }

        // --- Performance -----------------------------------------------------

        [Test]
        public void Step_DoesNotAllocate()
        {
            // Closed loop so the run crosses the start line (progress wrap) and many segments;
            // obstacles toggle so the lane selector changes lanes.
            PolylinePath path = TestPaths.Circle(60f, 90);
            AiDriverBrain brain = Brain(path);
            float s = 0f;

            Drive(brain, path, ref s, 500); // warm-up: JIT compilation and first-call static setup allocate once

            long before = System.GC.GetAllocatedBytesForCurrentThread();
            Drive(brain, path, ref s, 3000);
            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocated, "AiDriverBrain.Step must not allocate (runs every physics step for every rival)");
        }

        // Kinematic drive along the path at 20 m/s (no physics): only the brain's own work is measured.
        private static void Drive(AiDriverBrain brain, PolylinePath path, ref float s, int steps)
        {
            for (int i = 0; i < steps; i++)
            {
                s += 20f * Dt;
                AiSensorFrame frame = Frame(path.PositionAt(s), path.TangentAt(s), 20f);
                if ((i / 100) % 2 == 1) frame.ObstacleCentre = 10f;
                brain.Step(frame);
            }
        }
    }
}
