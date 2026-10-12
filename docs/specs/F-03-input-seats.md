# Spec F-03 — Input + seat assignment

- **Human owner/reviewer:** Lead Dev (D-010) · **Builder agent:** ui-input-engineer · **Architecture:** ADR-001, ADR-002 (D-016, D-017)
- **Split (each a separate PR, < 20 min review):** **F-03a** pure logic + Core contracts · **F-03b** runtime input (asset, router, seats, label) · **F-03c** integration script (scenes/prefabs/assets — executed by the orchestrator)

## Objective
Replace the temporary, prefab-bound `PilotInputSource` with a per-seat input system: one Input Actions asset with `Pilot`/`Gunner`/`Engineer`/`Debug`/`UI` maps, `PlayerInputManager` joining one device set per player (keyboard+mouse or a gamepad), runtime `SeatId` assignment, and input converted into per-seat command structs that flow through `Crew.VehicleSeats` to the vehicle's station receivers. Driving with the keyboard in `Sandbox` and `Sprint_01` must feel identical to today, and a single developer must be able to test every seat (hot-seat).

## Scope
- Core: `GunnerCommand`, `EngineerCommand`, `PowerFocus`, `IGunnerCommandReceiver`, `IEngineerCommandReceiver`, `Seats.Count`, `SeatOccupant`, `SeatActivity`, `ISeatStatus` (exact shapes in ADR-002 §2).
- Crew: `SeatAssignment` (pure), `SeatActivityRules` (pure), `CrewConfig` (SO), `VehicleSeats` (MonoBehaviour).
- Input: pure `PilotInputMath`, `KeyboardSteerShaper`, `GunnerAimMath`; `InputSeatConfig` (SO); `ConvoyControls.inputactions`; `SeatInputRouter`.
- Presentation: `SeatDebugLabel` (device → seat, self-bootstrapping like `SpeedometerHud`).
- Integration (F-03c): editor script wiring Sandbox + Sprint_01 + Jackal prefab.

## Out of scope
Station switching with transition (K-01), split-screen / per-seat cameras (C-01, G-01), turret and engineer receivers (G-01, M-01), AI seat assist (K-02, O-001), rebinding UI, gamepad aim assist, menus. Deleting `PilotInputSource` (needs Gabriel's approval later).

## Public interfaces

### F-03a — Core (new file `Scripts/Core/SeatCommands.cs`, `Scripts/Core/SeatStatus.cs`)
Exactly as ADR-002 §2. Plus:
```csharp
public struct GunnerCommand { /* … */ public GunnerCommand Clamped(); }  // yaw wrapped to [0,360), pitch clamped [-89,89]
```

### F-03a — Crew (`Scripts/Crew/`, new asmdef `Convoy.Crew` per ADR-001)
```csharp
namespace Convoy.Crew
{
    /// <summary>Pure: which player sits in which seat. No Unity calls.</summary>
    public sealed class SeatAssignment
    {
        public SeatAssignment(IReadOnlyList<SeatId> joinOrder);       // throws on duplicates/empty
        public bool TryJoin(int playerIndex, out SeatId seat);        // first free seat in joinOrder; false if full or already joined
        public void Leave(int playerIndex);                            // frees the seat (no-op if unknown)
        public bool TryGetSeat(int playerIndex, out SeatId seat);
        public bool TryGetPlayer(SeatId seat, out int playerIndex);
        public bool IsOccupied(SeatId seat);
        public bool TryCycleSeat(int playerIndex, out SeatId newSeat); // next FREE seat in SeatId order (wraps); false if none free
        public int PlayerCount { get; }
    }

    public static class SeatActivityRules
    {
        public static bool IsActive(in PilotCommand command, float axisDeadzone);   // |Throttle|>dz || |Steer|>dz || any bool
        public static bool IsActive(in GunnerCommand command, in GunnerCommand previous, float aimDeltaDegrees); // Fire || aim moved > threshold (yaw wrap-aware)
        public static bool IsActive(in EngineerCommand command, in EngineerCommand previous); // Repair || Focus != Keep && Focus != previous.Focus
    }

    [CreateAssetMenu(fileName = "CrewConfig", menuName = "Convoy/Crew Config")]
    public sealed class CrewConfig : ScriptableObject
    {
        [Range(0f, 0.5f)] public float activityAxisDeadzone = 0.05f;
        [Min(0f)] public float activityAimDegrees = 0.5f;
        // K-01 adds: switchSeconds = 1f
    }
}
```

### F-03a — Input pure helpers (`Scripts/Input/`)
```csharp
namespace Convoy.Input
{
    public static class PilotInputMath
    {
        public static float Throttle(float accelerate, float brake);       // accelerate − brake, clamped ±1
        public static float ApplyDeadzone(float value, float deadzone);     // |v| < dz → 0, else v (same as PilotInputSource)
        public static float CombineSteer(float stick, float keyboard);      // larger magnitude wins (same as PilotInputSource)
    }

    /// <summary>Keyboard steer ramp extracted from PilotInputSource (rampUp 4/s, return 6/s).</summary>
    public sealed class KeyboardSteerShaper
    {
        public KeyboardSteerShaper(float rampUpPerSecond, float returnPerSecond);
        public float Value { get; }
        public float Step(float keys, float deltaTime);   // identical maths to PilotInputSource.Update
        public void Reset();
    }

    public struct GunnerAim { public float YawDegrees; public float PitchDegrees; }

    public static class GunnerAimMath
    {
        /// <summary>Adds yaw/pitch deltas (degrees); wraps yaw to [0,360), clamps pitch to [minPitch,maxPitch].</summary>
        public static GunnerAim Integrate(GunnerAim aim, float deltaYaw, float deltaPitch, float minPitch, float maxPitch);
        public static GunnerAim FromHeading(float vehicleYawDegrees);  // pitch 0
    }
}
```

### F-03b — runtime
```csharp
namespace Convoy.Input
{
    [CreateAssetMenu(fileName = "InputSeatConfig", menuName = "Convoy/Input Seat Config")]
    public sealed class InputSeatConfig : ScriptableObject
    {
        public bool autoJoinKeyboard = true;
        public SeatId[] joinOrder = { SeatId.Pilot, SeatId.Gunner, SeatId.Engineer };
        [Min(0.1f)] public float keyboardSteerRampUp = 4f;     // = PilotInputSource._keyboardSteerSpeed
        [Min(0.1f)] public float keyboardSteerReturn = 6f;     // = PilotInputSource._keyboardSteerReturn
        [Range(0f, 0.5f)] public float stickDeadzone = 0.12f;  // = PilotInputSource._stickDeadzone
        [Min(0f)] public float mouseAimDegreesPerPixel = 0.12f;
        [Min(0f)] public float stickAimDegreesPerSecond = 180f;
        [Range(-89f, 0f)] public float minAimPitch = -30f;
        [Range(0f, 89f)] public float maxAimPitch = 60f;
    }

    public struct SeatBinding            // for the debug label; built on join/leave/lost, not per frame
    {
        public int PlayerIndex;
        public SeatId Seat;
        public bool HasSeat;
        public string DeviceLabel;       // e.g. "Keyboard&Mouse", "Gamepad (XInputControllerWindows)"; cached at join
        public bool DeviceLost;
    }

    public struct SeatViewInput { public bool LookBack; }   // view-only input for Presentation (C-01); never simulation

    /// <summary>Scene object next to a PlayerInputManager. Reads each joined player's current seat map in Update
    /// and submits that seat's command to VehicleSeats. Polls cached InputActions (no per-frame allocation).</summary>
    [DisallowMultipleComponent]
    public sealed class SeatInputRouter : MonoBehaviour
    {
        [SerializeField] private PlayerInputManager _playerInputManager;
        [SerializeField] private VehicleSeats _vehicle;          // the crew vehicle
        [SerializeField] private InputSeatConfig _config;

        public VehicleSeats Vehicle { get; }
        public IReadOnlyList<SeatBinding> Bindings { get; }
        public int BindingsVersion { get; }                      // increments on any change (label caches its string)
        public SeatViewInput GetViewInput(SeatId seat);
    }
}

namespace Convoy.Crew
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-40)]
    public sealed class VehicleSeats : MonoBehaviour, ISeatStatus   // see ADR-002 §4 for semantics
    {
        [SerializeField] private CrewConfig _config;
        public void SetOccupant(SeatId seat, SeatOccupant occupant);
        public void SubmitPilot(PilotCommand command, SeatOccupant source);
        public void SubmitGunner(GunnerCommand command, SeatOccupant source);
        public void SubmitEngineer(EngineerCommand command, SeatOccupant source);
        public SeatOccupant GetOccupant(SeatId seat);
        public SeatActivity GetActivity(SeatId seat);
        // FixedUpdate: for each occupied seat, OccupiedSteps++; ActiveSteps++ if SeatActivityRules says the last command is active.
        // Receivers resolved once in Awake via GetComponentInChildren<I…Receiver>(); missing receivers allowed.
    }
}

namespace Convoy.Presentation
{
    /// <summary>Editor/development builds only (#if UNITY_EDITOR || DEVELOPMENT_BUILD). IMGUI, top-right,
    /// one line per player: "P0 Keyboard&Mouse → Pilot", plus "Engineer: empty". Rebuilds its string only when BindingsVersion changes.</summary>
    public sealed class SeatDebugLabel : MonoBehaviour { }
}
```

### Input Actions asset `Assets/_Convoy/Config/Input/ConvoyControls.inputactions` (JSON, authored as text)
Control schemes: `KeyboardMouse` (Keyboard required, Mouse optional), `Gamepad` (Gamepad required). Do **not** enable "Generate C# Class" (that setting lives in the `.meta`, which builders don't author); the router looks actions up once with `FindAction("Map/Action", throwIfNotFound: true)` and caches them.

| Map | Action (type) | Keyboard & mouse | Gamepad |
|---|---|---|---|
| Pilot | Accelerate (Value, Axis) | W, ↑ | rightTrigger |
| Pilot | Brake (Value, Axis) | S, ↓ | leftTrigger |
| Pilot | SteerKeys (Value, Axis, 1DAxis composites) | A/D, ←/→ | — |
| Pilot | SteerStick (Value, Axis) | — | leftStick/x |
| Pilot | Handbrake (Button) | Space | buttonSouth |
| Pilot | Boost (Button) | leftShift, rightShift | rightShoulder |
| Pilot | Reset (Button) | R | buttonNorth |
| Pilot | LookBack (Button) | C | rightStickPress |
| Gunner | AimMouse (PassThrough, Vector2) | Mouse/delta | — |
| Gunner | AimStick (Value, Vector2) | — | rightStick |
| Gunner | Fire (Button) | Mouse/leftButton | rightTrigger |
| Gunner | SwitchStation (Button) | Tab | buttonNorth |
| Engineer | FocusBalanced / FocusEngine / FocusWeapons / FocusCooling (Button) | 1 / 2 / 3 / 4 | dpad/up / dpad/left / dpad/right / dpad/down |
| Engineer | Repair (Button, held) | E | buttonWest |
| Engineer | SwitchStation (Button) | Tab | buttonNorth |
| Debug | CycleSeat (Button) | F2 | — |
| UI | Restart (Button) | Enter | start |

The Pilot bindings are **exactly** today's `PilotInputSource` bindings (+ LookBack). The `UI` map is defined for later; `SprintHud` keeps reading Enter/Start directly in this task.

### Router behaviour (F-03b)
- `Start`: subscribe `PlayerInputManager.onPlayerJoined/onPlayerLeft`; if `autoJoinKeyboard && Keyboard.current != null` → `JoinPlayer(-1, -1, "KeyboardMouse", Keyboard.current, Mouse.current)` (Mouse may be null).
- On join: `SeatAssignment.TryJoin`; if a seat is free → `PlayerInput.SwitchCurrentActionMap(seat name)`, enable `Debug` map for the keyboard player, `VehicleSeats.SetOccupant(seat, Human)`; if no seat → player stays seatless (label says "spectating"). Subscribe `onDeviceLost/onDeviceRegained`.
- `Update` per seated, non-lost player: build the seat's command from cached actions and `Submit…(cmd, SeatOccupant.Human)`. Pilot: same maths as `PilotInputSource` via `PilotInputMath`/`KeyboardSteerShaper`; `Reset = WasPressedThisFrame()`. Gunner: integrate aim (`mouseDelta × degreesPerPixel`, `stick × degreesPerSecond × Time.deltaTime`), initialised with `GunnerAimMath.FromHeading(vehicle yaw)` on entering the seat; `HasAim = true`. Engineer: the router remembers the last pressed focus per player (target state) and sends it every frame (`Keep` until the first press).
- Device lost → submit the seat's `Neutral` once, mark binding lost; regained → resume.
- F2 (`Debug/CycleSeat`, keyboard player) → `SeatAssignment.TryCycleSeat` → old seat `SetOccupant(Empty)`, new seat `SetOccupant(Human)`, switch action map.
- Cursor locked + hidden while the keyboard player is Gunner; released otherwise and on Escape.
- No allocations in `Update` (no LINQ, no string building, no `new` of reference types; `List` capacity preset to 3).

## Data assets (created by the F-03c script, never hand-edited)
| Asset | Path | Values |
|---|---|---|
| `InputSeatConfig` | `Assets/_Convoy/Config/Input/InputSeatConfig.asset` | defaults above |
| `CrewConfig` | `Assets/_Convoy/Config/Crew_Config.asset` | defaults above |
| `PlayerSlot.prefab` | `Assets/_Convoy/Prefabs/Input/PlayerSlot.prefab` | `PlayerInput` (actions = ConvoyControls, behaviour = Invoke C# Events, no default map, no camera) |

## Dependencies
- Blocked by: — (F-03a), F-03a (F-03b), F-03b (F-03c).
- Blocks: K-01, G-01, M-01 (receivers), Q-01b (seat activity), A-01c (prefab no longer reads the keyboard), C-01a (`LookBack`).
- ADRs: ADR-001 (module refs: `Convoy.Input → Convoy.Crew`; `Convoy.Presentation → Convoy.Input, Convoy.Crew`), ADR-002.

## Allowed files
- **F-03a:** `Assets/_Convoy/Scripts/Core/SeatCommands.cs`, `Assets/_Convoy/Scripts/Core/SeatStatus.cs` (new); `Assets/_Convoy/Scripts/Crew/**` (new: asmdef, `SeatAssignment.cs`, `SeatActivityRules.cs`, `CrewConfig.cs`); `Assets/_Convoy/Scripts/Input/PilotInputMath.cs`, `KeyboardSteerShaper.cs`, `GunnerAimMath.cs` (new); `Assets/_Convoy/Tests/EditMode/Crew/**`, `Assets/_Convoy/Tests/EditMode/Input/**` (new test asmdefs + tests); `Assets/_Convoy/Tests/EditMode/SeatCommandsTests.cs` (new, Core tests in the existing assembly).
- **F-03b:** `Assets/_Convoy/Config/Input/ConvoyControls.inputactions` (new); `Assets/_Convoy/Scripts/Input/InputSeatConfig.cs`, `SeatInputRouter.cs` (new); `Assets/_Convoy/Scripts/Input/Convoy.Input.asmdef` (add `Convoy.Crew`); `Assets/_Convoy/Scripts/Input/PilotInputSource.cs` (doc comment only); `Assets/_Convoy/Scripts/Crew/VehicleSeats.cs` (new); `Assets/_Convoy/Scripts/Presentation/SeatDebugLabel.cs` (new); `Assets/_Convoy/Scripts/Presentation/Convoy.Presentation.asmdef` (add `Convoy.Crew`, `Convoy.Input`); tests under `Tests/EditMode/Crew/`, `Tests/EditMode/Input/`.
- **F-03c:** `Assets/_Convoy/Editor/Convoy.Editor.asmdef` (new, per ADR-001), `Assets/_Convoy/Editor/Integration/F03_InputSeatsSetup.cs` (new). The **orchestrator** commits the resulting changes to `Jackal.prefab`, `Sandbox.unity`, `Sprint_01.unity`, the three assets above and all generated `.meta` files.

## F-03c integration script — `Convoy/Integration/F-03 Input Seats Setup`
*(ADR-006, D-030)* Expose `public static void ApplyToScene(string scenePath)`; the menu item calls it for the scenes listed below. LV-03c reuses it for `Sprint_02`.
Idempotent, never deletes, logs each change:
1. Create `InputSeatConfig.asset`, `Crew_Config.asset` if missing.
2. Create `PlayerSlot.prefab` if missing (GameObject + `PlayerInput`, actions = `ConvoyControls`, notification behaviour = Invoke C# Events).
3. `Jackal.prefab`: add `VehicleSeats` (config = `Crew_Config`) if missing; set `PilotInputSource.enabled = false`.
4. For `Sandbox.unity` and `Sprint_01.unity`: find the scene's Jackal instance (the one `VehicleController` in the scene); if no `CrewInput` object exists, create it with `PlayerInputManager` (playerPrefab = `PlayerSlot`, joinBehavior = JoinPlayersWhenButtonIsPressed, maxPlayerCount = 3, splitScreen = off, notificationBehavior = Invoke C# Events) and `SeatInputRouter` (manager, vehicle = Jackal's `VehicleSeats`, config). Save scene.
5. Print a summary.

## Acceptance criteria
1. (F-03a) All new EditMode tests pass; existing 21+ tests still pass.
2. (F-03a) `KeyboardSteerShaper` + `PilotInputMath` reproduce `PilotInputSource` outputs: a test feeds the same key/stick sequences (hold right 0.5 s, release, tap left, stick 0.1 → 0, stick 0.8 vs keys) and asserts equality within 1e-5 against a reference implementation copied verbatim from `PilotInputSource.Update` into the test.
3. (F-03a) `SeatAssignment`: 3 joins fill Pilot/Gunner/Engineer in order; 4th join fails; leave + rejoin reuses the freed seat; `TryCycleSeat` skips occupied seats and fails when none free.
4. (F-03b) Project compiles with 0 errors and 0 new warnings; no allocation in `SeatInputRouter.Update` or `VehicleSeats.FixedUpdate` (Profiler "GC Alloc" column = 0 B in Play mode with 3 players).
5. (F-03c, manual checklist below) 1 keyboard+mouse and 2 gamepads map to three seats; the debug label shows device → seat; unplugging a pad does not crash and its seat goes neutral; replugging restores it.
6. (F-03c) Keyboard driving in `Sprint_01` is indistinguishable from before: Gabriel completes a run; 0–100 km/h in the Sandbox measured 3.4 s ± 0.2 (as in PT-01).
7. (F-03c) Only one input source drives the crew vehicle (no double input): with the disabled `PilotInputSource`, pressing W produces exactly one `SetPilotCommand` per frame (verified with a log breakpoint or a temporary counter in Play mode by the orchestrator).

## Test plan
- **EditMode (mandatory):** `SeatCommandsTests` (Clamped wrap/clamp, default = neutral), `SeatAssignmentTests`, `SeatActivityRulesTests` (yaw wrap 359°→1° counts as 2°), `KeyboardSteerShaperTests` (parity), `PilotInputMathTests`, `GunnerAimMathTests` (wrap, pitch clamp, FromHeading), `VehicleSeatsTests` (EditMode with `new GameObject().AddComponent<VehicleSeats>()` + fake receivers: source mismatch ignored; vacate sends neutral once; activity counters via a public `TickForTests()` or by invoking the internal step method — expose `internal void Step()` + `InternalsVisibleTo("Convoy.Crew.Tests.EditMode")`).
- **Manual checklist (Gabriel / Lead Dev, after F-03c):**
  1. Open `Sprint_01`, press Play, drive with WASD immediately (no join press needed). Label: "P0 Keyboard&Mouse → Pilot".
  2. Press A on pad 1 → label "P1 Gamepad → Gunner"; move right stick → no error. Pad 2 → Engineer.
  3. Unplug pad 1 → label "lost", no exception in Console; replug → resumes.
  4. Press F2 → keyboard moves to the next free seat; car coasts (Pilot empty); F2 again cycles.
  5. Complete a full Sprint_01 run with the keyboard; feel identical (Gabriel's call).
- **Perf:** Profiler, 3 players, `SeatInputRouter.Update` < 0.05 ms, 0 B GC.

## Risks
- `PlayerInputManager` auto-joining on *any* button may join the keyboard twice if pairing is wrong → join keyboard explicitly in `Start`; devices already paired are not re-joined.
- Mouse delta read via a PassThrough action can be lost if the map is switched mid-frame → switch maps only between frames (in the join/cycle handlers).
- Gamepad `buttonNorth` is Reset for Pilot and SwitchStation for Gunner/Engineer: fine because maps are per seat, but document it in the label/help.
- Human-owned area (Lead Dev): keep the PR small; the reviewer must check router ↔ assignment logic carefully (most bug-prone part).
