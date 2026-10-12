using System;
using Convoy.Core;
using NUnit.Framework;

namespace Convoy.Crew.Tests
{
    public class SeatAssignmentTests
    {
        private static readonly SeatId[] DefaultOrder = { SeatId.Pilot, SeatId.Gunner, SeatId.Engineer };

        private static SeatAssignment Make()
        {
            return new SeatAssignment(DefaultOrder);
        }

        [Test]
        public void Constructor_RejectsEmptyNullAndDuplicates()
        {
            Assert.Throws<ArgumentException>(() => new SeatAssignment(new SeatId[0]));
            Assert.Throws<ArgumentException>(() => new SeatAssignment(null));
            Assert.Throws<ArgumentException>(() => new SeatAssignment(new[] { SeatId.Pilot, SeatId.Pilot }));
            Assert.Throws<ArgumentException>(() => new SeatAssignment(new[] { (SeatId)7 }));
        }

        [Test]
        public void ThreeJoins_FillSeatsInOrder()
        {
            var a = Make();
            Assert.IsTrue(a.TryJoin(0, out SeatId s0));
            Assert.IsTrue(a.TryJoin(1, out SeatId s1));
            Assert.IsTrue(a.TryJoin(2, out SeatId s2));
            Assert.AreEqual(SeatId.Pilot, s0);
            Assert.AreEqual(SeatId.Gunner, s1);
            Assert.AreEqual(SeatId.Engineer, s2);
            Assert.AreEqual(3, a.PlayerCount);
        }

        [Test]
        public void FourthJoin_Fails()
        {
            var a = Make();
            a.TryJoin(0, out _);
            a.TryJoin(1, out _);
            a.TryJoin(2, out _);
            Assert.IsFalse(a.TryJoin(3, out _));
            Assert.AreEqual(3, a.PlayerCount);
        }

        [Test]
        public void CustomJoinOrder_IsRespected()
        {
            var a = new SeatAssignment(new[] { SeatId.Gunner, SeatId.Pilot });
            a.TryJoin(5, out SeatId first);
            a.TryJoin(6, out SeatId second);
            Assert.AreEqual(SeatId.Gunner, first);
            Assert.AreEqual(SeatId.Pilot, second);
            Assert.IsFalse(a.TryJoin(7, out _), "Engineer is not in the join order");
        }

        [Test]
        public void JoinTwice_Fails_AndKeepsOriginalSeat()
        {
            var a = Make();
            Assert.IsTrue(a.TryJoin(4, out _));
            Assert.IsFalse(a.TryJoin(4, out _));
            Assert.IsTrue(a.TryGetSeat(4, out SeatId seat));
            Assert.AreEqual(SeatId.Pilot, seat);
            Assert.AreEqual(1, a.PlayerCount);
        }

        [Test]
        public void NegativePlayerIndex_CannotJoin()
        {
            Assert.IsFalse(Make().TryJoin(-1, out _));
        }

        [Test]
        public void LeaveAndRejoin_ReusesFreedSeat()
        {
            var a = Make();
            a.TryJoin(0, out _);
            a.TryJoin(1, out _);
            a.TryJoin(2, out _);

            a.Leave(1);   // frees Gunner
            Assert.IsFalse(a.IsOccupied(SeatId.Gunner));
            Assert.AreEqual(2, a.PlayerCount);

            Assert.IsTrue(a.TryJoin(9, out SeatId seat));
            Assert.AreEqual(SeatId.Gunner, seat);
            Assert.AreEqual(3, a.PlayerCount);
        }

        [Test]
        public void Leave_UnknownPlayer_IsNoOp()
        {
            var a = Make();
            a.TryJoin(0, out _);
            a.Leave(42);
            Assert.AreEqual(1, a.PlayerCount);
            Assert.IsTrue(a.IsOccupied(SeatId.Pilot));
        }

        [Test]
        public void Lookups_ReportSeatAndPlayer()
        {
            var a = Make();
            a.TryJoin(7, out _);   // Pilot
            a.TryJoin(3, out _);   // Gunner

            Assert.IsTrue(a.TryGetPlayer(SeatId.Gunner, out int p));
            Assert.AreEqual(3, p);
            Assert.IsFalse(a.TryGetPlayer(SeatId.Engineer, out _));
            Assert.IsFalse(a.TryGetSeat(99, out _));
            Assert.IsTrue(a.IsOccupied(SeatId.Pilot));
            Assert.IsFalse(a.IsOccupied(SeatId.Engineer));
        }

        [Test]
        public void CycleSeat_SkipsOccupiedSeats()
        {
            var a = Make();
            a.TryJoin(0, out _);   // Pilot
            a.TryJoin(1, out _);   // Gunner  (occupied, must be skipped)

            Assert.IsTrue(a.TryCycleSeat(0, out SeatId newSeat));
            Assert.AreEqual(SeatId.Engineer, newSeat);
            Assert.IsFalse(a.IsOccupied(SeatId.Pilot), "old seat is freed");
            Assert.IsTrue(a.TryGetPlayer(SeatId.Engineer, out int p));
            Assert.AreEqual(0, p);
        }

        [Test]
        public void CycleSeat_WrapsAround()
        {
            var a = Make();
            a.TryJoin(0, out _);
            a.TryJoin(1, out _);
            a.TryJoin(2, out _);
            a.Leave(0);   // Pilot free; player 2 sits in Engineer

            Assert.IsTrue(a.TryCycleSeat(2, out SeatId newSeat));
            Assert.AreEqual(SeatId.Pilot, newSeat);
        }

        [Test]
        public void CycleSeat_Alone_VisitsAllSeatsInOrder()
        {
            var a = Make();
            a.TryJoin(0, out _);
            a.TryCycleSeat(0, out SeatId s1);
            a.TryCycleSeat(0, out SeatId s2);
            a.TryCycleSeat(0, out SeatId s3);
            Assert.AreEqual(SeatId.Gunner, s1);
            Assert.AreEqual(SeatId.Engineer, s2);
            Assert.AreEqual(SeatId.Pilot, s3);
        }

        [Test]
        public void CycleSeat_FailsWhenNoSeatFree_AndKeepsSeat()
        {
            var a = Make();
            a.TryJoin(0, out _);
            a.TryJoin(1, out _);
            a.TryJoin(2, out _);

            Assert.IsFalse(a.TryCycleSeat(0, out _));
            Assert.IsTrue(a.TryGetSeat(0, out SeatId seat));
            Assert.AreEqual(SeatId.Pilot, seat);
        }

        [Test]
        public void CycleSeat_UnknownPlayer_Fails()
        {
            Assert.IsFalse(Make().TryCycleSeat(5, out _));
        }
    }
}
