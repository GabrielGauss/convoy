# ADR-002 — Seats, seat commands, devices and stations

- **Status:** Proposed (architect, ARCH-001, 2026-10-11) · **Human owner/reviewer:** Lead Dev (owns F-03 and K-01 per D-010)
- **Builds on:** D-002 (3 stations), D-003 (per-seat cameras), D-005 (Input System, one device per player), D-007 (commands, `SeatId`). **Proposes:** D-017. **Recommendation for:** O-001.

## Context
- `Convoy.Core` already has `SeatId { Pilot, Gunner, Engineer }`, `PilotCommand` and `IPilotCommandReceiver` (implemented by `VehicleController`).
- Input today is `PilotInputSource` (Input module): keyboard + *any* gamepad drive one vehicle, bindings in code. It sits **on `Jackal.prefab`**, so every Jackal instance (including future AI rivals) would read the keyboard. It must leave the prefab before AI rivals exist (A-01c).
- Local multiplayer = 2–3 people on one PC (M1 goal). Online play is M2 (ADR-005).

## Decision

### 1. Vocabulary
| Term | Meaning |
|---|---|
| **Seat** (`SeatId`) | A station slot on one vehicle: Pilot, Gunner, Engineer. Addressed by id, never by "local player". |
| **Player** (`int playerIndex`) | One joined human = one device set (keyboard+mouse, or one gamepad). Index assigned by `PlayerInputManager` (0..2). |
| **Occupant** (`SeatOccupant`) | Who controls a seat now: `Empty`, `Human`, `Ai`. |
| **Command source** | Anything that produces seat commands: `SeatInputRouter` (humans), `AiPilot` (AI), later `Convoy.Net` (remote humans). |
| **Station / receiver** | The simulation component that consumes one seat's command (table in §4). |

### 2. Seat command structs (all in `Convoy.Core`, engine-free, `[Serializable]`, value types)
Commands are **target-state snapshots, not deltas** wherever possible: re-sending the same command is harmless (idempotent), dropping one is harmless — both matter for M2 netcode. `default(T)` is always the safe "nobody is here" command.

```csharp
namespace Convoy.Core
{
    // Exists (unchanged): PilotCommand { float Throttle; float Steer; bool Handbrake; bool Boost; bool Reset; }
    //                     interface IPilotCommandReceiver { void SetPilotCommand(PilotCommand command); }

    /// <summary>Gunner seat request for one step. Aim is in WORLD space so it stays on target while the
    /// vehicle turns (roll-stabilised, D-003). HasAim = false means "hold current turret orientation".</summary>
    [Serializable]
    public struct GunnerCommand
    {
        public bool HasAim;
        /// <summary>World yaw, degrees, [0, 360). 0 = world +Z, 90 = world +X (Unity's clockwise-from-above convention).</summary>
        public float AimYawDegrees;
        /// <summary>Degrees above the horizon, clamped to [-89, 89] by Clamped(). The turret applies its own tighter limits.</summary>
        public float AimPitchDegrees;
        public bool Fire;

        public static GunnerCommand Neutral => default;   // no aim change, not firing
        public GunnerCommand Clamped();                    // wraps yaw into [0,360), clamps pitch to [-89,89]
    }

    /// <summary>Engineer power focus (M-01). Keep = 0 so default(EngineerCommand) never changes the power split.</summary>
    public enum PowerFocus : byte { Keep = 0, Balanced = 1, Engine = 2, Weapons = 3, Cooling = 4 }

    /// <summary>Engineer seat request for one step. N-01 will add ping fields; adding fields is non-breaking.</summary>
    [Serializable]
    public struct EngineerCommand
    {
        /// <summary>Target power focus; Keep = leave as is.</summary>
        public PowerFocus Focus;
        /// <summary>Held = repairing (M-02 decides rate and what it blocks).</summary>
        public bool Repair;

        public static EngineerCommand Neutral => default;
    }

    public interface IGunnerCommandReceiver   { void SetGunnerCommand(GunnerCommand command); }
    public interface IEngineerCommandReceiver { void SetEngineerCommand(EngineerCommand command); }

    public static class Seats { public const int Count = 3; }   // SeatId values are 0..Count-1

    public enum SeatOccupant : byte { Empty = 0, Human = 1, Ai = 2 }

    /// <summary>Cumulative counters since the vehicle spawned, counted once per physics step.
    /// Consumers (telemetry) take differences between two reads.</summary>
    [Serializable]
    public struct SeatActivity
    {
        public int OccupiedSteps;   // steps with a Human or Ai occupant
        public int ActiveSteps;     // occupied steps where the command was "active" (Crew.SeatActivityRules)
    }

    /// <summary>Read-only seat status of one vehicle. Implemented by Crew.VehicleSeats.</summary>
    public interface ISeatStatus
    {
        SeatOccupant GetOccupant(SeatId seat);
        SeatActivity GetActivity(SeatId seat);
    }
}
```
**Why world-space aim floats instead of a target point:** Core cannot use `Vector3`; two angles are the smallest network-safe aim state, and world space makes the turret hold its target through drifts (the core G-01 test: "hit a rival at 30 m while the car drifts").

**Why no "switch station" field in any seat command:** switching is a *player* action, not a seat action (the seat itself doesn't switch). K-01 handles it in Crew via the player's `SwitchStation` input action.

### 3. Device → player → seat mapping (F-03)
Unity background: the Input System's **`PlayerInputManager`** creates one `PlayerInput` per joining player from a prefab and **pairs devices** to it; each `PlayerInput` gets its *own copy* of the actions asset, so player 0's `Pilot` map and player 1's `Gunner` map run independently. **Action maps** (`Pilot`, `Gunner`, `Engineer`, `Debug`, `UI`) group actions; a player enables only the map of the seat they occupy.

| Rule | Detail |
|---|---|
| Players | Max 3. Keyboard+mouse is **one** player (control scheme `KeyboardMouse`); each gamepad is one player (scheme `Gamepad`). No shared keyboard (D-005). |
| Joining | At scene start the router auto-joins keyboard+mouse as player 0 if a keyboard exists (`InputSeatConfig.autoJoinKeyboard`, default true), so Sandbox/Sprint_01 work with zero clicks as today. Gamepads join by pressing any button (`PlayerInputManager` "Join Players When Button Is Pressed"). |
| Seat on join | `Crew.SeatAssignment` gives the first free seat in `InputSeatConfig.joinOrder` (default Pilot → Gunner → Engineer). |
| Hot-seat (testing) | `Debug/CycleSeat` (F2, keyboard only, Editor + development builds) moves the keyboard player to the next **free** seat. One developer can test Gunner/Engineer without a second device. The vacated seat becomes `Empty`. |
| Pad-only pilot | Press F2 once (keyboard player leaves Pilot), then the next gamepad that joins takes Pilot; or set `autoJoinKeyboard = false` in the config. |
| Device lost (pad unplugged) | `PlayerInput.onDeviceLost`: the seat stays reserved for that player, the router submits the seat's `Neutral` command (car coasts, turret holds), the debug label shows "lost". `onDeviceRegained` (same pad back) resumes. No exceptions, no seat reshuffle. |
| Player leaves | Seat → `Empty`, `Neutral` sent once. |
| Cursor | While the keyboard player occupies Gunner, cursor is locked/hidden; unlocked otherwise and on Escape. |

**Bindings** (asset `Assets/_Convoy/Config/Input/ConvoyControls.inputactions`; exact table in `docs/specs/F-03-input-seats.md`): Pilot keeps today's bindings exactly (W/S/A/D/arrows, Space, Shift, R; RT/LT/left stick/A/RB/Y) + `LookBack` (C / R3) for C-01. Gunner: mouse delta or right stick aim, LMB/RT fire. Engineer: 1–4 / D-pad power focus, hold E / X repair. `SwitchStation` (Tab / Y) exists in Gunner and Engineer maps, unused until K-01.

**Frame vs physics timing:** the router reads devices in `Update` (per frame) and submits immediately; receivers store the latest command; the vehicle simulates it in `FixedUpdate`. One-shot buttons (Pilot `Reset`) are **latched by the receiver** until a physics step consumes them (`VehicleController` already does this). Every receiver with a one-shot field must latch the same way. Aim/power are target states, so they need no latch.

### 4. Station component contract (used by K-01, G-01, M-01)
| Seat | Receiver (consumes the command) | Module | Task |
|---|---|---|---|
| Pilot | `VehicleController : IPilotCommandReceiver` | Vehicle | exists |
| Gunner | `TurretStation : IGunnerCommandReceiver` | Combat | G-01 |
| Engineer | `EngineerStation : IEngineerCommandReceiver` (power, heat, repair) | Vehicle | M-01/M-02 |

A receiver **must**:
1. Live on the vehicle root or a child; be discoverable with `GetComponentInChildren<I…Receiver>()` once at `Awake`.
2. Treat `default(TCommand)` as "nobody is here": no throttle, no fire, power/aim unchanged.
3. Store the latest command in `Set…Command` (called from `Update` or `FixedUpdate`) and act on it only in `FixedUpdate`. Latch one-shot fields.
4. Never read devices, `Time.time` or other seats' receivers. Cross-station effects go through simulation state (e.g. Engineer's weapons power reaches the turret via `Core.IPowerSupply`, M-01).
5. Expose its status as a read-only struct for HUD/telemetry (e.g. `TurretState`, `EngineerState`).

**`Crew.VehicleSeats`** (F-03b, on the vehicle root, `[DefaultExecutionOrder(-40)]`) is the single entry point for *human* commands and the source of seat status:
```csharp
namespace Convoy.Crew
{
    public sealed class VehicleSeats : MonoBehaviour, ISeatStatus
    {
        public void SetOccupant(SeatId seat, SeatOccupant occupant);  // on change: forwards that seat's Neutral once
        public void SubmitPilot(PilotCommand command, SeatOccupant source);       // ignored unless source == occupant
        public void SubmitGunner(GunnerCommand command, SeatOccupant source);
        public void SubmitEngineer(EngineerCommand command, SeatOccupant source);
        public SeatOccupant GetOccupant(SeatId seat);
        public SeatActivity GetActivity(SeatId seat);
    }
}
```
- Missing receivers are fine (no turret until G-01): the command is dropped but activity is still counted, so telemetry measures Gunner/Engineer seats from day one.
- **AI rivals** (A-01) don't need `VehicleSeats`: `AiPilot` calls `IPilotCommandReceiver` on its own vehicle directly — the same interface a human's command ends in. A vehicle has exactly one command source per seat; `VehicleSeats` enforces that for crew vehicles via the `source == occupant` check.
- **K-01 (switching)** extends `Crew.SeatAssignment` with a timed transition (`CrewConfig.switchSeconds`, ~1 s, D-002): during the transition the player occupies no seat (both seats get `Neutral`), then the router switches the player's action map. No new interfaces needed.

### 5. AI filling empty seats (O-001 recommendation)
**Recommendation: AI assist fills empty non-pilot stations at reduced competence — but not in M1.** M1 ships with empty stations idle (`Neutral`), exactly as K-01 says, because (a) PT-02 must first show whether a 2-player crew *suffers* (telemetry: seat occupancy/activity, D-002 revisit), and (b) a good assist is real work (aiming, power heuristics) that competes with the vertical slice.

Designed-in hook so it is cheap later (task K-02, P2, only if PT-02 asks for it):
- `Convoy.AI.SeatAssist` component per vehicle: for each seat whose `ISeatStatus.GetOccupant` is `Empty`, it calls `VehicleSeats.SetOccupant(seat, Ai)` and submits commands with `source = Ai`. When a human enters (K-01 switch), Crew sets the occupant to `Human` and the assist's submissions are ignored automatically.
- Competence lives in `AiAssistProfile` (ScriptableObject): `reactionDelaySeconds`, `aimErrorDegrees`, `fireDiscipline01`, `repairBelowHealth01`, `powerFocusPolicy`. "Reduced competence" = higher delay/error, never cheating physics.
- Pilot seat on the crew vehicle is always human in M1 (no AI pilot assist).

### 6. Replacing `PilotInputSource` without breaking Sandbox / Sprint_01
| Step | Task | What happens | Scenes still work? |
|---|---|---|---|
| 1 | F-03a | Core structs, Crew `SeatAssignment`, pure input math (keyboard steer ramp extracted from `PilotInputSource`) + tests. No runtime wiring. | yes (untouched) |
| 2 | F-03b | `ConvoyControls.inputactions`, `InputSeatConfig`, `SeatInputRouter`, `VehicleSeats`, `SeatDebugLabel`. `PilotInputSource` gets a doc comment "superseded by SeatInputRouter (F-03c)"; no `[Obsolete]` (it would spam warnings). Not yet in any scene. | yes (untouched) |
| 3 | F-03c (integration script) | Disables `PilotInputSource` on `Jackal.prefab` (does **not** delete it); adds `VehicleSeats` to the prefab; creates `PlayerSlot.prefab` (PlayerInput); adds `CrewInput` object (PlayerInputManager + SeatInputRouter → the scene's Jackal) to `Sandbox.unity` and `Sprint_01.unity`; creates config assets. | yes — keyboard auto-joins as Pilot; manual checklist verifies identical driving |
| 4 | later, needs Gabriel's approval | Remove the disabled `PilotInputSource` component and file. | — |

Driving feel must be **identical** after step 3: the steer ramp (4 /s up, 6 /s return), stick deadzone 0.12 and "larger of stick vs keyboard wins" move into `PilotInputMath`/`KeyboardSteerShaper` with the same numbers in `InputSeatConfig`, covered by EditMode tests that reproduce `PilotInputSource`'s behaviour.

## Consequences
- + One path for humans (router → VehicleSeats → receiver) and one for AI (AiPilot → receiver); both end in the same interface.
- + Telemetry can measure seat occupancy/activity before turrets and power exist.
- + Hot-seat makes station work testable by one developer.
- − `PlayerInputManager` needs a `PlayerSlot` prefab (integration script). Split-screen is **not** turned on in F-03; per-seat cameras arrive with C-01/G-01 (D-003).
- Note: mouse aim = degrees-per-pixel × accumulated mouse delta (frame-rate independent by nature); stick aim = degrees-per-second × `Time.deltaTime`. Mixing them up is a classic bug — the F-03 tests cover both.

## Proposed decisions
**D-017 — Seat commands, device mapping and station contract** · Status: Proposed (architect) · Owner: Lead Dev
- Seat commands are engine-free target-state structs in `Convoy.Core`: `PilotCommand` (exists), `GunnerCommand` (world yaw/pitch degrees + `HasAim` + `Fire`), `EngineerCommand` (`PowerFocus` with `Keep = 0`, `Repair`). `default` = safe neutral. One-shot fields are latched by receivers.
- Keyboard+mouse auto-joins as player 0 (Pilot); gamepads join on button press; seat by join order Pilot → Gunner → Engineer; F2 hot-seat cycle in Editor/dev builds. Device loss = seat reserved + neutral command.
- Humans submit through `Crew.VehicleSeats` (single source per seat, activity counters, `ISeatStatus`); AI rivals call `IPilotCommandReceiver` directly.
- O-001 recommendation recorded: AI assist at reduced competence via `SeatAssist` + `AiAssistProfile`, **deferred to after PT-02** (K-02, P2). M1 empty stations idle.
