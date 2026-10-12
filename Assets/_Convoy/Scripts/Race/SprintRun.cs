using System;

namespace Convoy.Race
{
    public enum SprintPhase
    {
        /// <summary>Waiting at the start line; the clock starts when the vehicle moves.</summary>
        Ready,
        Running,
        /// <summary>Reached the finish (the monolith). Clock stopped.</summary>
        Finished,
    }

    /// <summary>
    /// Pure point-to-point sprint rules (S-01, D-014): clock, checkpoints in order, finish.
    /// Checkpoints cannot be skipped, so a reset (V-02) always returns to the last one passed.
    /// No Unity calls, so it is unit-tested.
    /// </summary>
    public sealed class SprintRun
    {
        public SprintRun(int checkpointCount, float bestTime = 0f)
        {
            if (checkpointCount < 0) throw new ArgumentOutOfRangeException(nameof(checkpointCount));
            CheckpointCount = checkpointCount;
            BestTime = bestTime;
            LastCheckpoint = -1;
        }

        public SprintPhase Phase { get; private set; }
        public int CheckpointCount { get; }

        /// <summary>Index of the last checkpoint passed, −1 = none (recover to the start line).</summary>
        public int LastCheckpoint { get; private set; }

        public float Elapsed { get; private set; }

        /// <summary>Best finish time in seconds, 0 = no time yet.</summary>
        public float BestTime { get; private set; }

        public bool IsNewBest { get; private set; }

        public bool AllCheckpointsPassed => LastCheckpoint == CheckpointCount - 1;

        /// <summary>Advance the clock. Starts the run the first time the vehicle moves.</summary>
        public void Tick(float deltaTime, bool vehicleMoving)
        {
            if (Phase == SprintPhase.Ready && vehicleMoving) Phase = SprintPhase.Running;
            if (Phase == SprintPhase.Running) Elapsed += Math.Max(0f, deltaTime);
        }

        /// <summary>Register a checkpoint. Only the next one in order counts; returns true if accepted.</summary>
        public bool PassCheckpoint(int index)
        {
            if (Phase != SprintPhase.Running) return false;
            if (index != LastCheckpoint + 1) return false;
            LastCheckpoint = index;
            return true;
        }

        /// <summary>Cross the finish. Only counts after every checkpoint; returns true when the run ends.</summary>
        public bool TryFinish()
        {
            if (Phase != SprintPhase.Running || !AllCheckpointsPassed) return false;
            Phase = SprintPhase.Finished;
            IsNewBest = BestTime <= 0f || Elapsed < BestTime;
            if (IsNewBest) BestTime = Elapsed;
            return true;
        }

        /// <summary>mm:ss.ff, e.g. 01:07.42.</summary>
        public static string FormatTime(float seconds)
        {
            if (seconds < 0f) seconds = 0f;
            int totalHundredths = (int)Math.Round(seconds * 100.0, MidpointRounding.AwayFromZero);
            int minutes = totalHundredths / 6000;
            int secs = totalHundredths / 100 % 60;
            int hundredths = totalHundredths % 100;
            return $"{minutes:00}:{secs:00}.{hundredths:00}";
        }
    }
}
