using System.Runtime.CompilerServices;
using Convoy.Core;
using UnityEngine;

// Lets the EditMode tests call Step()/ResolveReceivers()/Configure() directly. EditMode tests cannot rely on
// Awake/FixedUpdate: Unity only calls those message methods in Play mode (or on [ExecuteAlways] scripts).
[assembly: InternalsVisibleTo("Convoy.Crew.Tests.EditMode")]

namespace Convoy.Crew
{
    /// <summary>
    /// Single entry point for HUMAN (and later AI-assist) seat commands on a crew vehicle, and the source of
    /// seat status (ADR-002 section 4). Sits on the vehicle root.
    ///
    /// - Each seat has exactly one command source: a submission is ignored unless its source equals the seat's
    ///   current occupant. That is what stops two inputs (e.g. a human and an AI assist) fighting over a seat.
    /// - Commands are forwarded straight to the seat's receiver (VehicleController for Pilot; turret/engineer
    ///   stations later). Receivers store them and act in their own FixedUpdate, latching one-shot fields.
    /// - Missing receivers are fine (no turret until G-01): commands are dropped but activity is still counted,
    ///   so telemetry can measure Gunner/Engineer seats from day one.
    ///
    /// [DefaultExecutionOrder(-40)] makes this FixedUpdate run before the vehicle's, so activity is counted for the
    /// same command the vehicle simulates in that physics step.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-40)]
    public sealed class VehicleSeats : MonoBehaviour, ISeatStatus
    {
        [Tooltip("Activity thresholds (Crew_Config asset).")]
        [SerializeField] private CrewConfig _config;

        // All per-seat state lives in fixed arrays indexed by (int)SeatId, so nothing allocates after Awake.
        private readonly SeatOccupant[] _occupants = new SeatOccupant[Seats.Count];
        private readonly SeatActivity[] _activity = new SeatActivity[Seats.Count];

        private PilotCommand _pilot;
        private GunnerCommand _gunner;
        private GunnerCommand _gunnerPrevStep;     // what the Gunner sent at the previous physics step (aim-moved check)
        private EngineerCommand _engineer;
        private EngineerCommand _engineerPrevStep; // previous step's focus (only a CHANGE of focus is activity)

        private IPilotCommandReceiver _pilotReceiver;
        private IGunnerCommandReceiver _gunnerReceiver;
        private IEngineerCommandReceiver _engineerReceiver;

        private void Awake()
        {
            ResolveReceivers();
            if (_config == null)
                Debug.LogError($"{name}: VehicleSeats has no CrewConfig; activity uses zero thresholds.", this);
        }

        private void FixedUpdate()
        {
            Step();
        }

        /// <summary>
        /// Changes who controls a seat. On a real change the seat's stored command is reset and its Neutral
        /// command is forwarded to the receiver ONCE, so the car coasts / the turret holds instead of repeating
        /// the last input of whoever just left.
        /// </summary>
        public void SetOccupant(SeatId seat, SeatOccupant occupant)
        {
            if (!IsValid(seat) || _occupants[(int)seat] == occupant) return;
            _occupants[(int)seat] = occupant;

            switch (seat)
            {
                case SeatId.Pilot:
                    _pilot = PilotCommand.Neutral;
                    _pilotReceiver?.SetPilotCommand(PilotCommand.Neutral);
                    break;
                case SeatId.Gunner:
                    _gunner = GunnerCommand.Neutral;
                    _gunnerPrevStep = GunnerCommand.Neutral;
                    _gunnerReceiver?.SetGunnerCommand(GunnerCommand.Neutral);
                    break;
                case SeatId.Engineer:
                    _engineer = EngineerCommand.Neutral;
                    _engineerPrevStep = EngineerCommand.Neutral;
                    _engineerReceiver?.SetEngineerCommand(EngineerCommand.Neutral);
                    break;
            }
        }

        /// <summary>Ignored unless <paramref name="source"/> is the Pilot seat's current (non-empty) occupant.</summary>
        public void SubmitPilot(PilotCommand command, SeatOccupant source)
        {
            if (!Accepts(SeatId.Pilot, source)) return;
            _pilot = command;
            _pilotReceiver?.SetPilotCommand(command);
        }

        /// <summary>Ignored unless <paramref name="source"/> is the Gunner seat's current (non-empty) occupant.</summary>
        public void SubmitGunner(GunnerCommand command, SeatOccupant source)
        {
            if (!Accepts(SeatId.Gunner, source)) return;
            _gunner = command;
            _gunnerReceiver?.SetGunnerCommand(command);
        }

        /// <summary>Ignored unless <paramref name="source"/> is the Engineer seat's current (non-empty) occupant.</summary>
        public void SubmitEngineer(EngineerCommand command, SeatOccupant source)
        {
            if (!Accepts(SeatId.Engineer, source)) return;
            _engineer = command;
            _engineerReceiver?.SetEngineerCommand(command);
        }

        public SeatOccupant GetOccupant(SeatId seat)
        {
            return IsValid(seat) ? _occupants[(int)seat] : SeatOccupant.Empty;
        }

        public SeatActivity GetActivity(SeatId seat)
        {
            return IsValid(seat) ? _activity[(int)seat] : default;
        }

        /// <summary>
        /// Finds the station receivers once. GetComponentInChildren works with interfaces and also checks this
        /// GameObject; it is too slow to call per frame, hence the cache.
        /// </summary>
        internal void ResolveReceivers()
        {
            _pilotReceiver = GetComponentInChildren<IPilotCommandReceiver>();
            _gunnerReceiver = GetComponentInChildren<IGunnerCommandReceiver>();
            _engineerReceiver = GetComponentInChildren<IEngineerCommandReceiver>();
        }

        /// <summary>Test hook: sets the config that a scene would normally serialize into the field.</summary>
        internal void Configure(CrewConfig config)
        {
            _config = config;
        }

        /// <summary>One physics step of activity bookkeeping (called from FixedUpdate; tests call it directly).</summary>
        internal void Step()
        {
            // Without a config every non-neutral input counts (zero thresholds); Awake already logged the error.
            float axisDeadzone = _config != null ? _config.activityAxisDeadzone : 0f;
            float aimDegrees = _config != null ? _config.activityAimDegrees : 0f;

            if (_occupants[(int)SeatId.Pilot] != SeatOccupant.Empty)
                Count(SeatId.Pilot, SeatActivityRules.IsActive(in _pilot, axisDeadzone));

            if (_occupants[(int)SeatId.Gunner] != SeatOccupant.Empty)
                Count(SeatId.Gunner, SeatActivityRules.IsActive(in _gunner, in _gunnerPrevStep, aimDegrees));
            _gunnerPrevStep = _gunner;

            if (_occupants[(int)SeatId.Engineer] != SeatOccupant.Empty)
                Count(SeatId.Engineer, SeatActivityRules.IsActive(in _engineer, in _engineerPrevStep));
            _engineerPrevStep = _engineer;
        }

        private void Count(SeatId seat, bool active)
        {
            ref SeatActivity a = ref _activity[(int)seat];
            a.OccupiedSteps++;
            if (active) a.ActiveSteps++;
        }

        private bool Accepts(SeatId seat, SeatOccupant source)
        {
            return source != SeatOccupant.Empty && _occupants[(int)seat] == source;
        }

        private static bool IsValid(SeatId seat)
        {
            return (int)seat >= 0 && (int)seat < Seats.Count;
        }
    }
}
