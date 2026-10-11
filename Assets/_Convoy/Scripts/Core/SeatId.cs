namespace Convoy.Core
{
    /// <summary>Crew stations on a vehicle (D-002). Seats are addressed by id, never by a "local player" singleton (D-007).</summary>
    public enum SeatId
    {
        Pilot = 0,
        Gunner = 1,
        Engineer = 2,
    }
}
