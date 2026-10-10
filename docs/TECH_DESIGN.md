# CONVOY — Technical Design

Status: living document. Scope: M1 (offline greybox). Decisions referenced as D-xxx (see `DECISIONS.md`).

## Stack
Unity 6.3 LTS · URP · C# · Input System · Cinemachine 3 · ProBuilder (greybox) · Unity Test Framework · Git + LFS + GitHub.

## Architecture
Systems communicate through plain C# data and events; presentation reads simulation state and never writes it (D-007).

```
Input (devices) ──► Seat commands ──► Simulation (FixedUpdate) ──► State ──► Presentation
                    PilotCommand        Vehicle, Damage, Heat,              Camera, HUD,
                    GunnerCommand       Weapons, Race, AI                   VFX, Audio
                    EngineerCommand
AI ───────────────► (same commands)
```

| Module (asmdef) | Responsibility | Depends on |
|---|---|---|
| `Convoy.Core` | Match/race state, events, `SeatId`, command structs, config base types | — |
| `Convoy.Input` | Device → seat mapping, action maps → commands | Core |
| `Convoy.Vehicle` | Raycast suspension, drive, grip/drift, recovery, damage, heat | Core |
| `Convoy.Crew` | Stations, switching, per-seat command routing | Core, Vehicle |
| `Convoy.Combat` | Weapons, ammo, overheat, projectiles/hitscan | Core, Vehicle |
| `Convoy.Race` | Checkpoints, ordering, finish, results, telemetry hooks | Core |
| `Convoy.AI` | Spline following, avoidance → PilotCommand | Core, Race |
| `Convoy.Presentation` | Cameras, HUD, debug overlay, VFX/audio hooks | all (read-only) |
| `Convoy.Tests.EditMode` / `.PlayMode` | Tests | per module |

Rule: no module references `Presentation`. No cyclic asmdef references.

## Data (ScriptableObjects)
`VehicleConfig`, `DamageConfig`, `HeatConfig`, `WeaponConfig`, `StationConfig`, `RaceRules`. Terrain/weather configs are post-M1. Every gameplay number lives in one of these; designers tune without code changes.

## Multiplayer-readiness rules (D-007)
1. Simulation separated from presentation.
2. Every player action is a per-seat command struct.
3. No `LocalPlayer` singletons; address seats by `SeatId`.
4. Vehicle state is a serializable struct.
5. Gameplay runs in `FixedUpdate` (default 50 Hz; revisit in netcode spike).

## Project layout
```
Assets/_Convoy/
  Scripts/{Core,Input,Vehicle,Crew,Combat,Race,AI,Presentation}/
  Config/          # ScriptableObject instances
  Prefabs/
  Scenes/          # Sandbox.unity, GreyboxRace.unity
  Tests/{EditMode,PlayMode}/
Assets/ThirdParty/ # imported packages/assets, untouched
```

## Testing
- EditMode tests for pure logic (race state machine, damage curves, heat math, suspension force).
- PlayMode smoke test: load `GreyboxRace`, AI-only race completes.
- Before every PR: project compiles with 0 errors, all tests green.

## Known risks
See `GDD.md` § Risks. Technical top risks: vehicle stability at speed; multi-seat shared vehicle under latency (post-M1); Gunner camera on a rotating platform.
