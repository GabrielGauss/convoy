using System;

namespace Convoy.Core
{
    /// <summary>
    /// Everything the Pilot seat can ask the vehicle to do in one simulation step.
    /// Produced by human input or AI; consumed by the vehicle simulation (D-007).
    /// Plain serializable data so it can later be sent over the network unchanged.
    /// </summary>
    [Serializable]
    public struct PilotCommand
    {
        /// <summary>-1 = full brake/reverse, +1 = full throttle.</summary>
        public float Throttle;

        /// <summary>-1 = full left, +1 = full right.</summary>
        public float Steer;

        public bool Handbrake;

        /// <summary>Hold to use the nitro tank.</summary>
        public bool Boost;

        /// <summary>Request a reset to the last checkpoint (handled by V-02).</summary>
        public bool Reset;

        public static PilotCommand Neutral => default;

        public PilotCommand Clamped()
        {
            return new PilotCommand
            {
                Throttle = Math.Clamp(Throttle, -1f, 1f),
                Steer = Math.Clamp(Steer, -1f, 1f),
                Handbrake = Handbrake,
                Boost = Boost,
                Reset = Reset,
            };
        }
    }

    /// <summary>Anything that accepts Pilot commands (the vehicle). Lets input and AI stay decoupled from Vehicle code.</summary>
    public interface IPilotCommandReceiver
    {
        void SetPilotCommand(PilotCommand command);
    }
}
