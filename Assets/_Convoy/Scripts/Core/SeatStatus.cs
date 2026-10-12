using System;

namespace Convoy.Core
{
    /// <summary>Number of seats on a vehicle. SeatId values are 0..Count-1, so they can index arrays.</summary>
    public static class Seats
    {
        public const int Count = 3;
    }

    /// <summary>Who controls a seat right now.</summary>
    public enum SeatOccupant : byte
    {
        Empty = 0,
        Human = 1,
        Ai = 2,
    }

    /// <summary>
    /// Cumulative counters since the vehicle spawned, counted once per physics step.
    /// Consumers (telemetry) take differences between two reads.
    /// </summary>
    [Serializable]
    public struct SeatActivity
    {
        /// <summary>Steps with a Human or Ai occupant.</summary>
        public int OccupiedSteps;

        /// <summary>Occupied steps where the command was "active" (see Crew.SeatActivityRules).</summary>
        public int ActiveSteps;
    }

    /// <summary>Read-only seat status of one vehicle. Implemented by Crew.VehicleSeats (F-03b).</summary>
    public interface ISeatStatus
    {
        SeatOccupant GetOccupant(SeatId seat);
        SeatActivity GetActivity(SeatId seat);
    }
}
