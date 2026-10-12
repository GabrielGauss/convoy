using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Convoy.Core;
using Convoy.Crew;
using UnityEngine;
using UnityEngine.InputSystem;

// The EditMode tests check that every action path the router looks up exists in ConvoyControls.inputactions.
[assembly: InternalsVisibleTo("Convoy.Input.Tests.EditMode")]

namespace Convoy.Input
{
    /// <summary>Who sits where, for the debug label. Built on join/leave/cycle/lost, never per frame.</summary>
    public struct SeatBinding
    {
        public int PlayerIndex;
        public SeatId Seat;
        public bool HasSeat;
        /// <summary>e.g. "Keyboard&amp;Mouse", "Gamepad (XInputControllerWindows)"; cached at join.</summary>
        public string DeviceLabel;
        public bool DeviceLost;
    }

    /// <summary>View-only input for Presentation (C-01 chase camera). Never fed to the simulation.</summary>
    public struct SeatViewInput
    {
        public bool LookBack;
    }

    /// <summary>
    /// Scene object next to a PlayerInputManager (F-03, ADR-002 section 3). Turns each joined player's device
    /// input into the command struct of the seat they occupy, and submits it to <see cref="VehicleSeats"/>.
    ///
    /// Unity background: PlayerInputManager spawns one PlayerInput per joining player and pairs devices to it.
    /// Each PlayerInput owns a private COPY of the actions asset, so player 0's "Pilot" map and player 1's
    /// "Gunner" map are independent; we enable only the map of the player's seat. Actions are looked up once
    /// on join and polled in Update (ReadValue / IsPressed), which allocates nothing per frame.
    ///
    /// Commands are sent from Update (per rendered frame); receivers keep the latest one and simulate it in
    /// FixedUpdate, latching one-shot fields such as Pilot Reset (ADR-002 "Frame vs physics timing").
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SeatInputRouter : MonoBehaviour
    {
        // Scheme names must match the control schemes in ConvoyControls.inputactions.
        internal const string KeyboardMouseScheme = "KeyboardMouse";
        internal const string KeyboardMouseLabel = "Keyboard&Mouse";

        // Action-map name per seat, indexed by (int)SeatId. SeatId.ToString() would allocate a string on every call.
        internal static readonly string[] SeatMapNames = { "Pilot", "Gunner", "Engineer" };
        internal const string DebugMapName = "Debug";

        internal static readonly string[] RequiredActionPaths =
        {
            "Pilot/Accelerate", "Pilot/Brake", "Pilot/SteerKeys", "Pilot/SteerStick", "Pilot/Handbrake",
            "Pilot/Boost", "Pilot/Reset", "Pilot/LookBack",
            "Gunner/AimMouse", "Gunner/AimStick", "Gunner/Fire",
            "Engineer/FocusBalanced", "Engineer/FocusEngine", "Engineer/FocusWeapons", "Engineer/FocusCooling",
            "Engineer/Repair",
            "Debug/CycleSeat",
        };

        private const int MaxPlayers = Seats.Count;

        [Tooltip("The scene's PlayerInputManager (spawns one PlayerSlot per joining device set).")]
        [SerializeField] private PlayerInputManager _playerInputManager;
        [Tooltip("The crew vehicle's VehicleSeats.")]
        [SerializeField] private VehicleSeats _vehicle;
        [SerializeField] private InputSeatConfig _config;

        // Capacity preset so adding players never reallocates; Update only indexes these lists.
        private readonly List<PlayerState> _players = new List<PlayerState>(MaxPlayers);
        private readonly List<SeatBinding> _bindings = new List<SeatBinding>(MaxPlayers);
        private SeatAssignment _assignment;
        private bool _subscribed;
        private bool _cursorReleasedByEscape;
        private bool _cursorLockedByUs;

        public VehicleSeats Vehicle => _vehicle;
        public IReadOnlyList<SeatBinding> Bindings => _bindings;

        /// <summary>Increments on any join/leave/cycle/lost/regained, so the debug label rebuilds its text only then.</summary>
        public int BindingsVersion { get; private set; }

        private void Awake()
        {
            if (_playerInputManager == null || _vehicle == null || _config == null)
            {
                Debug.LogError($"{name}: SeatInputRouter needs a PlayerInputManager, a VehicleSeats and an InputSeatConfig.", this);
                enabled = false;
                return;
            }
            _assignment = new SeatAssignment(_config.joinOrder);
        }

        private void Start()
        {
            if (!enabled || _assignment == null) return;

            _playerInputManager.onPlayerJoined += HandlePlayerJoined;
            _playerInputManager.onPlayerLeft += HandlePlayerLeft;
            _subscribed = true;

            // Join keyboard+mouse explicitly (instead of waiting for a key press) so the scene drives with zero
            // clicks, exactly as with PilotInputSource. A device that is already paired is never joined again by
            // PlayerInputManager, so the keyboard cannot end up as two players.
            Keyboard keyboard = Keyboard.current;
            if (_config.autoJoinKeyboard && keyboard != null && !IsKeyboardPaired())
            {
                Mouse mouse = Mouse.current;
                // Passing a null Mouse inside the params array would try to pair "null"; build the array explicitly.
                InputDevice[] devices = mouse != null ? new InputDevice[] { keyboard, mouse } : new InputDevice[] { keyboard };
                _playerInputManager.JoinPlayer(-1, -1, KeyboardMouseScheme, devices);
            }
        }

        private void OnDestroy()
        {
            if (_subscribed && _playerInputManager != null)
            {
                _playerInputManager.onPlayerJoined -= HandlePlayerJoined;
                _playerInputManager.onPlayerLeft -= HandlePlayerLeft;
            }
            for (int i = 0; i < _players.Count; i++) Unsubscribe(_players[i].Player);
            ReleaseCursor();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < _players.Count; i++)
            {
                PlayerState p = _players[i];
                if (!p.HasSeat || p.DeviceLost) continue;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                // Hot-seat (F2): the map switch happens here, between this frame's reads, and we skip submitting
                // for this player this frame so no action of the old map is read after it was disabled.
                if (p.CycleSeat != null && p.CycleSeat.WasPressedThisFrame() && TryCycle(p)) continue;
#endif
                switch (p.Seat)
                {
                    case SeatId.Pilot: SubmitPilot(p, dt); break;
                    case SeatId.Gunner: SubmitGunner(p, dt); break;
                    case SeatId.Engineer: SubmitEngineer(p); break;
                }
            }
            UpdateCursor();
        }

        /// <summary>View-only input of whoever sits in <paramref name="seat"/> (default when empty or device lost).</summary>
        public SeatViewInput GetViewInput(SeatId seat)
        {
            if (seat != SeatId.Pilot) return default;   // only the Pilot map has LookBack (C-01)
            PlayerState p = FindBySeat(seat);
            if (p == null || p.DeviceLost) return default;
            return new SeatViewInput { LookBack = p.LookBack.IsPressed() };
        }

        // ---------------------------------------------------------------- per-seat command building

        private void SubmitPilot(PlayerState p, float dt)
        {
            // Same maths and order as PilotInputSource.Update, via the F-03a helpers (parity covered by tests).
            float throttle = PilotInputMath.Throttle(p.Accelerate.ReadValue<float>(), p.Brake.ReadValue<float>());
            float keyboardSteer = p.SteerShaper.Step(p.SteerKeys.ReadValue<float>(), dt);
            float stick = PilotInputMath.ApplyDeadzone(p.SteerStick.ReadValue<float>(), _config.stickDeadzone);

            _vehicle.SubmitPilot(new PilotCommand
            {
                Throttle = throttle,
                Steer = PilotInputMath.CombineSteer(stick, keyboardSteer),
                Handbrake = p.Handbrake.IsPressed(),
                Boost = p.Boost.IsPressed(),
                Reset = p.Reset.WasPressedThisFrame(),   // one-shot; VehicleController latches it for FixedUpdate
            }, SeatOccupant.Human);
        }

        private void SubmitGunner(PlayerState p, float dt)
        {
            // Mouse delta is already "pixels this frame" (frame-rate independent); the stick is a rate, so it
            // needs * deltaTime. Mixing those up is a classic bug (ADR-002 consequences).
            Vector2 mouse = p.AimMouse.ReadValue<Vector2>() * _config.mouseAimDegreesPerPixel;
            Vector2 stick = p.AimStick.ReadValue<Vector2>() * (_config.stickAimDegreesPerSecond * dt);
            // +x = turn right (yaw grows clockwise from above); +y = look up (pitch above the horizon).
            p.Aim = GunnerAimMath.Integrate(p.Aim, mouse.x + stick.x, mouse.y + stick.y,
                _config.minAimPitch, _config.maxAimPitch);

            _vehicle.SubmitGunner(new GunnerCommand
            {
                HasAim = true,
                AimYawDegrees = p.Aim.YawDegrees,
                AimPitchDegrees = p.Aim.PitchDegrees,
                Fire = p.Fire.IsPressed(),
            }.Clamped(), SeatOccupant.Human);
        }

        private void SubmitEngineer(PlayerState p)
        {
            // Engineer commands are target states: remember the last focus pressed and re-send it every frame
            // (Keep until the first press), so a dropped/duplicated command never changes the outcome.
            if (p.FocusBalanced.WasPressedThisFrame()) p.Focus = PowerFocus.Balanced;
            if (p.FocusEngine.WasPressedThisFrame()) p.Focus = PowerFocus.Engine;
            if (p.FocusWeapons.WasPressedThisFrame()) p.Focus = PowerFocus.Weapons;
            if (p.FocusCooling.WasPressedThisFrame()) p.Focus = PowerFocus.Cooling;

            _vehicle.SubmitEngineer(new EngineerCommand
            {
                Focus = p.Focus,
                Repair = p.Repair.IsPressed(),
            }, SeatOccupant.Human);
        }

        private void SubmitNeutral(SeatId seat)
        {
            switch (seat)
            {
                case SeatId.Pilot: _vehicle.SubmitPilot(PilotCommand.Neutral, SeatOccupant.Human); break;
                case SeatId.Gunner: _vehicle.SubmitGunner(GunnerCommand.Neutral, SeatOccupant.Human); break;
                case SeatId.Engineer: _vehicle.SubmitEngineer(EngineerCommand.Neutral, SeatOccupant.Human); break;
            }
        }

        // ---------------------------------------------------------------- join / leave / devices

        private void HandlePlayerJoined(PlayerInput input)
        {
            if (FindByInput(input) != null) return;

            var p = new PlayerState(input, _config);
            _players.Add(p);
            input.onDeviceLost += HandleDeviceLost;
            input.onDeviceRegained += HandleDeviceRegained;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // The Debug map (F2 hot-seat) stays enabled independently of the seat map, keyboard player only.
            if (p.IsKeyboard)
            {
                InputActionMap debugMap = input.actions.FindActionMap(DebugMapName, throwIfNotFound: true);
                debugMap.Enable();
                p.CycleSeat = debugMap.FindAction("CycleSeat", throwIfNotFound: true);
            }
#endif
            if (_assignment.TryJoin(input.playerIndex, out SeatId seat))
            {
                EnterSeat(p, seat);
                _vehicle.SetOccupant(seat, SeatOccupant.Human);
            }
            // else: no free seat -> the player stays seatless ("spectating" on the label) with no seat map enabled.

            RebuildBindings();
        }

        private void HandlePlayerLeft(PlayerInput input)
        {
            PlayerState p = FindByInput(input);
            if (p == null) return;

            Unsubscribe(input);
            if (p.HasSeat) _vehicle.SetOccupant(p.Seat, SeatOccupant.Empty);   // sends Neutral once
            _assignment.Leave(input.playerIndex);
            _players.Remove(p);
            RebuildBindings();
        }

        private void HandleDeviceLost(PlayerInput input)
        {
            PlayerState p = FindByInput(input);
            if (p == null || p.DeviceLost) return;
            // Seat stays reserved for this player (no reshuffle); the seat goes neutral once: car coasts, turret holds.
            p.DeviceLost = true;
            if (p.HasSeat) SubmitNeutral(p.Seat);
            RebuildBindings();
        }

        private void HandleDeviceRegained(PlayerInput input)
        {
            PlayerState p = FindByInput(input);
            if (p == null || !p.DeviceLost) return;
            p.DeviceLost = false;
            p.SteerShaper.Reset();   // don't resume with a half-turned keyboard wheel from before the loss
            RebuildBindings();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool TryCycle(PlayerState p)
        {
            SeatId oldSeat = p.Seat;
            if (!_assignment.TryCycleSeat(p.Player.playerIndex, out SeatId newSeat)) return false;

            _vehicle.SetOccupant(oldSeat, SeatOccupant.Empty);
            EnterSeat(p, newSeat);
            _vehicle.SetOccupant(newSeat, SeatOccupant.Human);
            RebuildBindings();
            return true;
        }
#endif

        /// <summary>Switches the player's action map to the seat and resets per-seat input state.</summary>
        private void EnterSeat(PlayerState p, SeatId seat)
        {
            p.Seat = seat;
            p.HasSeat = true;
            p.SteerShaper.Reset();
            p.Focus = PowerFocus.Keep;
            // Start aiming along the vehicle's heading so the turret doesn't snap to world +Z on sitting down.
            p.Aim = GunnerAimMath.FromHeading(_vehicle.transform.eulerAngles.y);
            // SwitchCurrentActionMap disables the previous seat map and enables the new one; the Debug map is
            // not the "current" map, so it stays enabled.
            p.Player.SwitchCurrentActionMap(SeatMapNames[(int)seat]);
            if (seat == SeatId.Gunner && p.IsKeyboard) _cursorReleasedByEscape = false;
        }

        // ---------------------------------------------------------------- cursor

        private void UpdateCursor()
        {
            PlayerState gunner = FindBySeat(SeatId.Gunner);
            bool keyboardGunner = gunner != null && gunner.IsKeyboard && !gunner.DeviceLost;

            if (keyboardGunner)
            {
                Keyboard keyboard = Keyboard.current;
                if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) _cursorReleasedByEscape = true;
                // Clicking back into the game re-captures the mouse after Escape.
                else if (_cursorReleasedByEscape && gunner.Fire.WasPressedThisFrame()) _cursorReleasedByEscape = false;
            }

            bool wantLocked = keyboardGunner && !_cursorReleasedByEscape;
            if (wantLocked && !_cursorLockedByUs)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                _cursorLockedByUs = true;
            }
            else if (!wantLocked && _cursorLockedByUs)
            {
                ReleaseCursor();
            }
        }

        private void ReleaseCursor()
        {
            // Only undo a lock we made, so we never fight another system that manages the cursor.
            if (!_cursorLockedByUs) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _cursorLockedByUs = false;
        }

        // ---------------------------------------------------------------- bookkeeping

        private void RebuildBindings()
        {
            _bindings.Clear();
            for (int i = 0; i < _players.Count; i++)
            {
                PlayerState p = _players[i];
                _bindings.Add(new SeatBinding
                {
                    PlayerIndex = p.Player.playerIndex,
                    Seat = p.Seat,
                    HasSeat = p.HasSeat,
                    DeviceLabel = p.DeviceLabel,
                    DeviceLost = p.DeviceLost,
                });
            }
            BindingsVersion++;
        }

        private PlayerState FindByInput(PlayerInput input)
        {
            for (int i = 0; i < _players.Count; i++)
                if (_players[i].Player == input) return _players[i];
            return null;
        }

        private PlayerState FindBySeat(SeatId seat)
        {
            for (int i = 0; i < _players.Count; i++)
                if (_players[i].HasSeat && _players[i].Seat == seat) return _players[i];
            return null;
        }

        private void Unsubscribe(PlayerInput input)
        {
            if (input == null) return;
            input.onDeviceLost -= HandleDeviceLost;
            input.onDeviceRegained -= HandleDeviceRegained;
        }

        private static bool IsKeyboardPaired()
        {
            // PlayerInput.all is a ReadOnlyArray (struct); indexing it does not allocate. Called once in Start.
            var all = PlayerInput.all;
            for (int i = 0; i < all.Count; i++)
            {
                var devices = all[i].devices;
                for (int d = 0; d < devices.Count; d++)
                    if (devices[d] is Keyboard) return true;
            }
            return false;
        }

        /// <summary>
        /// Per-player runtime state. A class (not a struct) so Update can mutate it through the list without copies.
        /// Created on join only; actions are cached from THIS player's copy of the asset.
        /// </summary>
        private sealed class PlayerState
        {
            public readonly PlayerInput Player;
            public readonly bool IsKeyboard;
            public readonly string DeviceLabel;
            public readonly KeyboardSteerShaper SteerShaper;

            public readonly InputAction Accelerate, Brake, SteerKeys, SteerStick, Handbrake, Boost, Reset, LookBack;
            public readonly InputAction AimMouse, AimStick, Fire;
            public readonly InputAction FocusBalanced, FocusEngine, FocusWeapons, FocusCooling, Repair;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            public InputAction CycleSeat;   // keyboard player only; compiled out of release builds (no F2 hot-seat)
#endif

            public bool HasSeat;
            public SeatId Seat;
            public bool DeviceLost;
            public GunnerAim Aim;
            public PowerFocus Focus;

            public PlayerState(PlayerInput input, InputSeatConfig config)
            {
                Player = input;
                SteerShaper = new KeyboardSteerShaper(config.keyboardSteerRampUp, config.keyboardSteerReturn);

                InputActionAsset a = input.actions;
                Accelerate = a.FindAction("Pilot/Accelerate", throwIfNotFound: true);
                Brake = a.FindAction("Pilot/Brake", throwIfNotFound: true);
                SteerKeys = a.FindAction("Pilot/SteerKeys", throwIfNotFound: true);
                SteerStick = a.FindAction("Pilot/SteerStick", throwIfNotFound: true);
                Handbrake = a.FindAction("Pilot/Handbrake", throwIfNotFound: true);
                Boost = a.FindAction("Pilot/Boost", throwIfNotFound: true);
                Reset = a.FindAction("Pilot/Reset", throwIfNotFound: true);
                LookBack = a.FindAction("Pilot/LookBack", throwIfNotFound: true);
                AimMouse = a.FindAction("Gunner/AimMouse", throwIfNotFound: true);
                AimStick = a.FindAction("Gunner/AimStick", throwIfNotFound: true);
                Fire = a.FindAction("Gunner/Fire", throwIfNotFound: true);
                FocusBalanced = a.FindAction("Engineer/FocusBalanced", throwIfNotFound: true);
                FocusEngine = a.FindAction("Engineer/FocusEngine", throwIfNotFound: true);
                FocusWeapons = a.FindAction("Engineer/FocusWeapons", throwIfNotFound: true);
                FocusCooling = a.FindAction("Engineer/FocusCooling", throwIfNotFound: true);
                Repair = a.FindAction("Engineer/Repair", throwIfNotFound: true);

                IsKeyboard = false;
                string label = null;
                var devices = input.devices;
                for (int d = 0; d < devices.Count; d++)
                {
                    if (devices[d] is Keyboard) { IsKeyboard = true; break; }
                    if (label == null && devices[d] is Gamepad) label = "Gamepad (" + devices[d].layout + ")";
                }
                DeviceLabel = IsKeyboard ? KeyboardMouseLabel : (label ?? input.currentControlScheme ?? "Unknown device");
            }
        }
    }
}
