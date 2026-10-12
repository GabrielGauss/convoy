using System;
using System.Collections.Generic;
using Convoy.Core;

namespace Convoy.Crew
{
    /// <summary>
    /// Pure logic: which player sits in which seat. No Unity calls, so it is fully unit-testable.
    /// A "player" is an int index handed out by the input layer (one per device set); this class never
    /// knows about devices (ADR-001: Input asks Crew, Crew never knows devices).
    /// Uses fixed arrays so joining, leaving and cycling never allocate after construction.
    /// </summary>
    public sealed class SeatAssignment
    {
        private const int NoPlayer = -1;

        private readonly SeatId[] _joinOrder;
        private readonly int[] _playerBySeat = new int[Seats.Count];   // indexed by (int)SeatId
        private int _playerCount;

        /// <exception cref="ArgumentException">joinOrder is null, empty, has duplicates or a SeatId outside 0..Seats.Count-1.</exception>
        public SeatAssignment(IReadOnlyList<SeatId> joinOrder)
        {
            if (joinOrder == null || joinOrder.Count == 0)
                throw new ArgumentException("joinOrder must contain at least one seat.", nameof(joinOrder));

            _joinOrder = new SeatId[joinOrder.Count];
            for (int i = 0; i < joinOrder.Count; i++)
            {
                SeatId seat = joinOrder[i];
                if ((int)seat < 0 || (int)seat >= Seats.Count)
                    throw new ArgumentException($"Invalid seat {seat} in joinOrder.", nameof(joinOrder));
                for (int j = 0; j < i; j++)
                    if (_joinOrder[j] == seat)
                        throw new ArgumentException($"Duplicate seat {seat} in joinOrder.", nameof(joinOrder));
                _joinOrder[i] = seat;
            }

            for (int s = 0; s < _playerBySeat.Length; s++) _playerBySeat[s] = NoPlayer;
        }

        public int PlayerCount => _playerCount;

        /// <summary>
        /// Seats the player in the first free seat of the join order.
        /// Returns false (and seat = default) if the player already has a seat, the index is negative, or no seat is free.
        /// </summary>
        public bool TryJoin(int playerIndex, out SeatId seat)
        {
            seat = default;
            if (playerIndex < 0 || FindSeatOf(playerIndex, out _)) return false;

            for (int i = 0; i < _joinOrder.Length; i++)
            {
                SeatId candidate = _joinOrder[i];
                if (_playerBySeat[(int)candidate] != NoPlayer) continue;
                _playerBySeat[(int)candidate] = playerIndex;
                _playerCount++;
                seat = candidate;
                return true;
            }
            return false;
        }

        /// <summary>Frees the player's seat. No-op for unknown players.</summary>
        public void Leave(int playerIndex)
        {
            if (!FindSeatOf(playerIndex, out SeatId seat)) return;
            _playerBySeat[(int)seat] = NoPlayer;
            _playerCount--;
        }

        public bool TryGetSeat(int playerIndex, out SeatId seat)
        {
            return FindSeatOf(playerIndex, out seat);
        }

        public bool TryGetPlayer(SeatId seat, out int playerIndex)
        {
            playerIndex = NoPlayer;
            if ((int)seat < 0 || (int)seat >= Seats.Count) return false;
            int p = _playerBySeat[(int)seat];
            if (p == NoPlayer) return false;
            playerIndex = p;
            return true;
        }

        public bool IsOccupied(SeatId seat)
        {
            return (int)seat >= 0 && (int)seat < Seats.Count && _playerBySeat[(int)seat] != NoPlayer;
        }

        /// <summary>
        /// Moves the player to the next FREE seat after their current one, in SeatId order, wrapping around.
        /// Only seats that are part of the join order are candidates. Returns false if the player has no seat
        /// or no other seat is free (the player then stays where they are).
        /// </summary>
        public bool TryCycleSeat(int playerIndex, out SeatId newSeat)
        {
            newSeat = default;
            if (!FindSeatOf(playerIndex, out SeatId current)) return false;

            for (int step = 1; step < Seats.Count; step++)
            {
                int candidate = ((int)current + step) % Seats.Count;
                if (_playerBySeat[candidate] != NoPlayer || !InJoinOrder(candidate)) continue;
                _playerBySeat[(int)current] = NoPlayer;
                _playerBySeat[candidate] = playerIndex;
                newSeat = (SeatId)candidate;
                return true;
            }
            return false;
        }

        private bool FindSeatOf(int playerIndex, out SeatId seat)
        {
            seat = default;
            if (playerIndex < 0) return false;
            for (int s = 0; s < _playerBySeat.Length; s++)
            {
                if (_playerBySeat[s] != playerIndex) continue;
                seat = (SeatId)s;
                return true;
            }
            return false;
        }

        private bool InJoinOrder(int seat)
        {
            for (int i = 0; i < _joinOrder.Length; i++)
                if ((int)_joinOrder[i] == seat) return true;
            return false;
        }
    }
}
