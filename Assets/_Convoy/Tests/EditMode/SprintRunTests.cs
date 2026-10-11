using Convoy.Race;
using NUnit.Framework;

namespace Convoy.Tests
{
    public class SprintRunTests
    {
        private static SprintRun Running(int checkpoints = 3, float best = 0f)
        {
            var run = new SprintRun(checkpoints, best);
            run.Tick(0.02f, true);
            return run;
        }

        [Test]
        public void ClockWaitsUntilVehicleMoves()
        {
            var run = new SprintRun(3);
            run.Tick(1f, false);
            Assert.That(run.Phase, Is.EqualTo(SprintPhase.Ready));
            Assert.That(run.Elapsed, Is.EqualTo(0f));
            run.Tick(0.5f, true);
            Assert.That(run.Phase, Is.EqualTo(SprintPhase.Running));
            Assert.That(run.Elapsed, Is.EqualTo(0.5f).Within(1e-5f));
        }

        [Test]
        public void CheckpointsMustBeInOrder()
        {
            var run = Running();
            Assert.That(run.PassCheckpoint(1), Is.False, "skipping checkpoint 0");
            Assert.That(run.PassCheckpoint(0), Is.True);
            Assert.That(run.PassCheckpoint(0), Is.False, "same checkpoint twice");
            Assert.That(run.PassCheckpoint(1), Is.True);
            Assert.That(run.LastCheckpoint, Is.EqualTo(1));
        }

        [Test]
        public void CheckpointsIgnoredBeforeStart()
        {
            var run = new SprintRun(3);
            Assert.That(run.PassCheckpoint(0), Is.False);
        }

        [Test]
        public void FinishRequiresAllCheckpoints()
        {
            var run = Running();
            run.PassCheckpoint(0);
            run.PassCheckpoint(1);
            Assert.That(run.TryFinish(), Is.False);
            run.PassCheckpoint(2);
            Assert.That(run.TryFinish(), Is.True);
            Assert.That(run.Phase, Is.EqualTo(SprintPhase.Finished));
        }

        [Test]
        public void FinishWithNoCheckpointsWorks()
        {
            var run = Running(0);
            Assert.That(run.TryFinish(), Is.True);
        }

        [Test]
        public void ClockStopsAtFinish()
        {
            var run = Running(0);
            run.TryFinish();
            float t = run.Elapsed;
            run.Tick(5f, true);
            Assert.That(run.Elapsed, Is.EqualTo(t));
        }

        [Test]
        public void BestTime_SetOnFirstFinishAndOnlyImproves()
        {
            var first = Running(0);
            first.Tick(60f, true);
            first.TryFinish();
            Assert.That(first.IsNewBest, Is.True);

            var slower = Running(0, first.BestTime);
            slower.Tick(70f, true);
            slower.TryFinish();
            Assert.That(slower.IsNewBest, Is.False);
            Assert.That(slower.BestTime, Is.EqualTo(first.BestTime));

            var faster = Running(0, first.BestTime);
            faster.Tick(50f, true);
            faster.TryFinish();
            Assert.That(faster.IsNewBest, Is.True);
            Assert.That(faster.BestTime, Is.LessThan(first.BestTime));
        }

        [Test]
        public void FormatTime_IsMinutesSecondsHundredths()
        {
            Assert.That(SprintRun.FormatTime(67.42f), Is.EqualTo("01:07.42"));
            Assert.That(SprintRun.FormatTime(5.005f), Is.EqualTo("00:05.01").Or.EqualTo("00:05.00"));
            Assert.That(SprintRun.FormatTime(-3f), Is.EqualTo("00:00.00"));
            Assert.That(SprintRun.FormatTime(600f), Is.EqualTo("10:00.00"));
        }
    }
}
