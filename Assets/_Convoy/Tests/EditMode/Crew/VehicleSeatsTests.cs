using Convoy.Core;
using NUnit.Framework;
using UnityEngine;

namespace Convoy.Crew.Tests
{
    // Fake station receivers. EditMode never calls Awake/FixedUpdate, so the tests call the internal
    // ResolveReceivers()/Step() hooks of VehicleSeats directly (InternalsVisibleTo in VehicleSeats.cs).
    public sealed class FakePilotReceiver : MonoBehaviour, IPilotCommandReceiver
    {
        public int Calls;
        public PilotCommand Last;
        public void SetPilotCommand(PilotCommand command) { Calls++; Last = command; }
    }

    public sealed class FakeGunnerReceiver : MonoBehaviour, IGunnerCommandReceiver
    {
        public int Calls;
        public GunnerCommand Last;
        public void SetGunnerCommand(GunnerCommand command) { Calls++; Last = command; }
    }

    public sealed class FakeEngineerReceiver : MonoBehaviour, IEngineerCommandReceiver
    {
        public int Calls;
        public EngineerCommand Last;
        public void SetEngineerCommand(EngineerCommand command) { Calls++; Last = command; }
    }

    public class VehicleSeatsTests
    {
        private GameObject _root;
        private CrewConfig _config;
        private VehicleSeats _seats;
        private FakePilotReceiver _pilot;
        private FakeGunnerReceiver _gunner;
        private FakeEngineerReceiver _engineer;

        private static readonly PilotCommand Throttling = new PilotCommand { Throttle = 1f };

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("VehicleSeatsTest");
            _pilot = _root.AddComponent<FakePilotReceiver>();
            // Station receivers may live on children (ADR-002 section 4).
            var child = new GameObject("Stations");
            child.transform.SetParent(_root.transform);
            _gunner = child.AddComponent<FakeGunnerReceiver>();
            _engineer = child.AddComponent<FakeEngineerReceiver>();

            _config = ScriptableObject.CreateInstance<CrewConfig>();
            _seats = _root.AddComponent<VehicleSeats>();
            _seats.Configure(_config);
            _seats.ResolveReceivers();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void AllSeats_StartEmpty_WithZeroActivity()
        {
            for (int s = 0; s < Seats.Count; s++)
            {
                Assert.AreEqual(SeatOccupant.Empty, _seats.GetOccupant((SeatId)s));
                Assert.AreEqual(0, _seats.GetActivity((SeatId)s).OccupiedSteps);
            }
        }

        [Test]
        public void Submit_FromOccupant_IsForwardedToReceiver()
        {
            _seats.SetOccupant(SeatId.Pilot, SeatOccupant.Human);
            int before = _pilot.Calls;
            _seats.SubmitPilot(Throttling, SeatOccupant.Human);
            Assert.AreEqual(before + 1, _pilot.Calls);
            Assert.AreEqual(1f, _pilot.Last.Throttle);
        }

        [Test]
        public void Submit_SourceMismatch_IsIgnored()
        {
            _seats.SetOccupant(SeatId.Pilot, SeatOccupant.Human);
            int before = _pilot.Calls;

            _seats.SubmitPilot(Throttling, SeatOccupant.Ai);
            _seats.SubmitPilot(Throttling, SeatOccupant.Empty);

            Assert.AreEqual(before, _pilot.Calls, "AI/Empty must not drive a Human-occupied seat");
            Assert.AreEqual(0f, _pilot.Last.Throttle);
        }

        [Test]
        public void Submit_ToEmptySeat_IsIgnored_EvenWithSourceEmpty()
        {
            _seats.SubmitGunner(new GunnerCommand { Fire = true }, SeatOccupant.Empty);
            _seats.SubmitEngineer(new EngineerCommand { Repair = true }, SeatOccupant.Human);
            Assert.AreEqual(0, _gunner.Calls);
            Assert.AreEqual(0, _engineer.Calls);
        }

        [Test]
        public void Vacate_SendsNeutralOnce()
        {
            _seats.SetOccupant(SeatId.Pilot, SeatOccupant.Human);
            _seats.SubmitPilot(Throttling, SeatOccupant.Human);
            int before = _pilot.Calls;

            _seats.SetOccupant(SeatId.Pilot, SeatOccupant.Empty);
            _seats.SetOccupant(SeatId.Pilot, SeatOccupant.Empty);   // no change -> nothing sent
            _seats.SubmitPilot(Throttling, SeatOccupant.Human);     // former occupant -> ignored

            Assert.AreEqual(before + 1, _pilot.Calls);
            Assert.AreEqual(PilotCommand.Neutral.Throttle, _pilot.Last.Throttle);
            Assert.IsFalse(_pilot.Last.Boost || _pilot.Last.Handbrake || _pilot.Last.Reset);
        }

        [Test]
        public void Vacate_Gunner_And_Engineer_SendNeutralOnce()
        {
            _seats.SetOccupant(SeatId.Gunner, SeatOccupant.Human);
            _seats.SetOccupant(SeatId.Engineer, SeatOccupant.Human);
            _seats.SubmitGunner(new GunnerCommand { HasAim = true, AimYawDegrees = 30f, Fire = true }, SeatOccupant.Human);
            _seats.SubmitEngineer(new EngineerCommand { Focus = PowerFocus.Engine, Repair = true }, SeatOccupant.Human);
            int g = _gunner.Calls, e = _engineer.Calls;

            _seats.SetOccupant(SeatId.Gunner, SeatOccupant.Empty);
            _seats.SetOccupant(SeatId.Engineer, SeatOccupant.Empty);

            Assert.AreEqual(g + 1, _gunner.Calls);
            Assert.AreEqual(e + 1, _engineer.Calls);
            Assert.IsFalse(_gunner.Last.Fire);
            Assert.IsFalse(_gunner.Last.HasAim, "neutral gunner = hold current turret orientation");
            Assert.AreEqual(PowerFocus.Keep, _engineer.Last.Focus);
            Assert.IsFalse(_engineer.Last.Repair);
        }

        [Test]
        public void HandOver_HumanToAi_AiNowAccepted_HumanIgnored()
        {
            _seats.SetOccupant(SeatId.Gunner, SeatOccupant.Human);
            _seats.SetOccupant(SeatId.Gunner, SeatOccupant.Ai);
            int before = _gunner.Calls;

            _seats.SubmitGunner(new GunnerCommand { Fire = true }, SeatOccupant.Human);
            Assert.AreEqual(before, _gunner.Calls);
            _seats.SubmitGunner(new GunnerCommand { Fire = true }, SeatOccupant.Ai);
            Assert.AreEqual(before + 1, _gunner.Calls);
        }

        [Test]
        public void MissingReceivers_AreFine_AndActivityStillCounts()
        {
            var bare = new GameObject("Bare");
            try
            {
                var seats = bare.AddComponent<VehicleSeats>();
                seats.Configure(_config);
                seats.ResolveReceivers();
                seats.SetOccupant(SeatId.Gunner, SeatOccupant.Human);
                seats.SubmitGunner(new GunnerCommand { Fire = true }, SeatOccupant.Human);
                seats.Step();
                Assert.AreEqual(1, seats.GetActivity(SeatId.Gunner).OccupiedSteps);
                Assert.AreEqual(1, seats.GetActivity(SeatId.Gunner).ActiveSteps);
            }
            finally
            {
                Object.DestroyImmediate(bare);
            }
        }

        [Test]
        public void Activity_EmptySeats_AreNotCounted()
        {
            _seats.Step();
            _seats.Step();
            for (int s = 0; s < Seats.Count; s++)
                Assert.AreEqual(0, _seats.GetActivity((SeatId)s).OccupiedSteps);
        }

        [Test]
        public void Activity_Pilot_CountsOccupiedAndActiveSteps()
        {
            _seats.SetOccupant(SeatId.Pilot, SeatOccupant.Human);
            _seats.Step();                                               // neutral: occupied, idle
            _seats.SubmitPilot(Throttling, SeatOccupant.Human);
            _seats.Step();                                               // active
            _seats.Step();                                               // still the latest command -> active
            _seats.SubmitPilot(new PilotCommand { Steer = _config.activityAxisDeadzone * 0.5f }, SeatOccupant.Human);
            _seats.Step();                                               // below deadzone -> idle

            SeatActivity a = _seats.GetActivity(SeatId.Pilot);
            Assert.AreEqual(4, a.OccupiedSteps);
            Assert.AreEqual(2, a.ActiveSteps);
        }

        [Test]
        public void Activity_Gunner_AimMovement_ComparedToPreviousStep()
        {
            _seats.SetOccupant(SeatId.Gunner, SeatOccupant.Human);
            var aim = new GunnerCommand { HasAim = true, AimYawDegrees = 10f };
            _seats.SubmitGunner(aim, SeatOccupant.Human);
            _seats.Step();   // first aim after sitting down: nothing to compare -> idle

            aim.AimYawDegrees = 10f + _config.activityAimDegrees * 4f;
            _seats.SubmitGunner(aim, SeatOccupant.Human);
            _seats.Step();   // moved -> active
            _seats.Step();   // same aim as previous step -> idle

            aim.Fire = true;
            _seats.SubmitGunner(aim, SeatOccupant.Human);
            _seats.Step();   // firing -> active

            SeatActivity a = _seats.GetActivity(SeatId.Gunner);
            Assert.AreEqual(4, a.OccupiedSteps);
            Assert.AreEqual(2, a.ActiveSteps);
        }

        [Test]
        public void Activity_Engineer_OnlyFocusChangeOrRepairCounts()
        {
            _seats.SetOccupant(SeatId.Engineer, SeatOccupant.Human);
            _seats.SubmitEngineer(new EngineerCommand { Focus = PowerFocus.Weapons }, SeatOccupant.Human);
            _seats.Step();   // Keep -> Weapons: active
            _seats.Step();   // Weapons re-sent: idle
            _seats.SubmitEngineer(new EngineerCommand { Focus = PowerFocus.Weapons, Repair = true }, SeatOccupant.Human);
            _seats.Step();   // repairing: active

            SeatActivity a = _seats.GetActivity(SeatId.Engineer);
            Assert.AreEqual(3, a.OccupiedSteps);
            Assert.AreEqual(2, a.ActiveSteps);
        }

        [Test]
        public void Activity_IsCumulative_AcrossVacateAndReoccupy()
        {
            _seats.SetOccupant(SeatId.Pilot, SeatOccupant.Human);
            _seats.Step();
            _seats.SetOccupant(SeatId.Pilot, SeatOccupant.Empty);
            _seats.Step();
            _seats.SetOccupant(SeatId.Pilot, SeatOccupant.Ai);
            _seats.Step();
            Assert.AreEqual(2, _seats.GetActivity(SeatId.Pilot).OccupiedSteps);
        }

        [Test]
        public void ImplementsISeatStatus()
        {
            ISeatStatus status = _seats;
            _seats.SetOccupant(SeatId.Engineer, SeatOccupant.Ai);
            Assert.AreEqual(SeatOccupant.Ai, status.GetOccupant(SeatId.Engineer));
        }
    }
}
