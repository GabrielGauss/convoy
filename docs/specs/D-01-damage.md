# Spec D-01 — Damage model

- **Human owner/reviewer:** Gabriel (D-010: vehicle feel, M-02) · **Builder agent:** combat-engineer · **Architecture:** ADR-001, ADR-003 (D-018)
- **Split:** **D-01a** Core contracts + pure logic + config class · **D-01b** runtime adapter, `VehicleController` hook, PlayMode handling regression test · **D-01c** integration script (config asset + prefab wiring — executed by the orchestrator)

## Objective
Give every vehicle a single hull-health pool that takes impact damage from collisions above a speed threshold and accepts damage from any other module through `Convoy.Core.IDamageable`. Low health degrades top speed, engine force, grip and steering through configurable multipliers, so a heavy crash visibly changes handling. At full health the Jackal must drive **bit-identically** to today (PT-01 tuning). This is the foundation for G-01 (hits slow rivals), M-02 (repair), W-01 (wreck) and Q-01 (damage taken).

## Scope
Core contracts (`VehicleId`, `DamageKind`, `DamageEvent`, `IDamageable`), `DamageConfig`, `VehicleHealth`, `ImpactDamage`, `ImpactWindow`, `HandlingModifiers`, `VehicleDamageState`, `VehicleDamage` MonoBehaviour, multipliers in `VehicleController`, `Health01`/`Wrecked` in `VehicleState`, an HP line in the F1 debug overlay, `Convoy.Tests.PlayMode` assembly with the Jackal handling regression test.

## Out of scope
Wreck behaviour (W-01), repair input/rate (M-02), weapons (G-01), damage VFX/audio/HUD bar (U-01), per-zone or per-subsystem damage (ADR-003 option C), assigning `VehicleId`s (R-01; default `None`).

## Public interfaces
Exactly as in ADR-003 § Decision. Files:
```csharp
// D-01a — Scripts/Core/Damage.cs
namespace Convoy.Core { public struct VehicleId : IEquatable<VehicleId>; public enum DamageKind : byte; public struct DamageEvent; public interface IDamageable { float ApplyDamage(in DamageEvent damage); } }

// D-01a — Scripts/Vehicle/DamageConfig.cs, VehicleHealth.cs, ImpactDamage.cs (ImpactDamage + ImpactWindow), HandlingModifiers.cs
namespace Convoy.Vehicle
{
    public struct HandlingModifiers { public float TopSpeed, EngineForce, Grip, Steer; public static HandlingModifiers None { get; }
                                      public static HandlingModifiers FromHealth(float health01, DamageConfig config); }
    public static class ImpactDamage { public static float Compute(float deltaV, float normalUpDot, DamageConfig config); }
    public sealed class ImpactWindow { public ImpactWindow(DamageConfig config); public float Filter(float rawDamage, double time); }
    [Serializable] public struct VehicleDamageState { public float Health, MaxHealth, TotalDamageTaken; public int HitCount; public bool Wrecked; public float Health01 { get; } }
    public sealed class VehicleHealth { public VehicleHealth(DamageConfig c); public VehicleDamageState State { get; }
                                        public float Apply(in DamageEvent d); public float Repair(float amount); public void Restore(); }
}

// D-01b — Scripts/Vehicle/VehicleDamage.cs
namespace Convoy.Vehicle
{
    [RequireComponent(typeof(Rigidbody))] [DisallowMultipleComponent]
    public sealed class VehicleDamage : MonoBehaviour, IDamageable
    {
        [SerializeField] private DamageConfig _config;
        public VehicleId Id { get; set; }
        public DamageConfig Config { get; }
        public VehicleDamageState State { get; }
        public HandlingModifiers Modifiers { get; }       // cached; recomputed only when health changes
        public event Action<DamageEvent> Damaged;         // presentation/telemetry only
        public float ApplyDamage(in DamageEvent damage);
        public float Repair(float amount);
        // OnCollisionEnter(Collision c): dv = c.impulse.magnitude / body.mass; up = Vector3.Dot(c.GetContact(0).normal, Vector3.up);
        //   raw = ImpactDamage.Compute(dv, up, cfg); amt = _window.Filter(raw, Time.fixedTimeAsDouble); if (amt > 0) ApplyDamage(Impact, source = other VehicleDamage?.Id ?? None)
    }
}
```
**`ImpactDamage.Compute` rule:** `threshold = normalUpDot > landingNormalDot ? landingSpeedThreshold : impactSpeedThreshold; return min(max(0, deltaV − threshold) × impactDamagePerMps, maxImpactDamage)`. NaN/negative input → 0.
**`ImpactWindow.Filter` rule:** if `time − windowStart > impactWindowSeconds` → new window, `max = raw`, return raw; else return `max(0, raw − max)` and `max = max(max, raw)`.
**`HandlingModifiers.FromHealth`:** `h >= degradeBelowHealth01 → 1f exactly` (return the literal, don't compute a lerp that might give 0.99999); else `lerp(min, 1, h / degradeBelowHealth01)` per stat; `h` clamped to [0,1].

### `VehicleController` edit (D-01b) — the only allowed changes
1. Field `private VehicleDamage _damage;` set in `Awake` with `TryGetComponent(out _damage)`.
2. In `FixedUpdate` after `dt`: `HandlingModifiers mods = _damage != null ? _damage.Modifiers : HandlingModifiers.None;`
3. `driveTopSpeed` forward branch: `_config.topSpeed * mods.TopSpeed + (boosting ? _config.boostTopSpeedBonus : 0f)`; boost push's `boostedTop` likewise.
4. Engine: `drive * _config.engineForce * mods.EngineForce * driveFactor / drivenCount`.
5. Grip: `float grip = (wheel.rear ? _config.rearLateralGrip : _config.frontLateralGrip) * mods.Grip;`
6. Steer: `VehicleMath.SteerAngle(_command.Steer, _config.maxSteerAngle * mods.Steer, forwardSpeed, _config.topSpeed, _config.highSpeedSteerFactor)` (falloff uses the **unscaled** top speed).
7. `WriteState`: `Health01 = _damage != null ? _damage.State.Health01 : 1f; Wrecked = _damage != null && _damage.State.Wrecked;`
Each change gets a one-line comment "D-01: damage multiplier (1.0 at full health)". No other edits; physics changes need the handling regression test (README rule).

## Data assets
`DamageConfig` fields/defaults: ADR-003 table. D-01c creates `Assets/_Convoy/Config/Jackal_DamageConfig.asset` with those defaults.

## Dependencies
- Blocked by: — (D-01a); D-01a (D-01b); D-01b (D-01c).
- Blocks: G-01, M-02, W-01, Q-01b (health/damage columns), U-01 damage bar.
- Conflict note: Q-01b also edits `VehicleController.cs`/`VehicleState.cs` → Q-01b is blocked by D-01b.

## Allowed files
- **D-01a:** `Assets/_Convoy/Scripts/Core/Damage.cs`; `Assets/_Convoy/Scripts/Vehicle/DamageConfig.cs`, `VehicleHealth.cs`, `ImpactDamage.cs`, `HandlingModifiers.cs`; `Assets/_Convoy/Tests/EditMode/DamageTests.cs`, `HandlingModifiersTests.cs` (existing test assembly already references Core + Vehicle).
- **D-01b:** `Assets/_Convoy/Scripts/Vehicle/VehicleDamage.cs` (new); `VehicleController.cs` and `VehicleState.cs` (only the edits listed); `Assets/_Convoy/Scripts/Presentation/DebugOverlayText.cs` + `Tests/EditMode/DebugOverlayTextTests.cs` (add "HP nn%" line); `Assets/_Convoy/Tests/PlayMode/**` (new asmdef `Convoy.Tests.PlayMode` + `JackalHandlingRegressionTests.cs`).
- **D-01c:** `Assets/_Convoy/Editor/Integration/D01_DamageSetup.cs` (+ `Convoy.Editor.asmdef` reference edit if needed). Orchestrator commits the resulting `Jackal.prefab` and `.asset`/`.meta` changes.

## D-01c integration script — `Convoy/Integration/D-01 Damage Setup`
1. Create `Jackal_DamageConfig.asset` if missing (defaults). 2. `Jackal.prefab`: add `VehicleDamage` with that config if missing. 3. Log summary. Idempotent, never deletes.

## Acceptance criteria
1. EditMode: all new tests green; existing tests green.
2. `HandlingModifiers.FromHealth(h ≥ 0.6)` returns exactly `1f` for all four fields (`Assert.AreEqual(1f, x)` — exact, no tolerance).
3. PlayMode `JackalHandlingRegressionTests` (flat 2 km box ground built in code, Jackal prefab loaded via `AssetDatabase` under `#if UNITY_EDITOR`, `PilotInputSource` disabled, full throttle via `IPilotCommandReceiver`):
   a. 0→100 km/h time with `VehicleDamage` at full health equals the time **without** `VehicleDamage` within 0.02 s, and is 3.4 s ± 0.3 s (PT-01 measurement).
   b. At health 0 (apply 100 damage before the run), speed after 6 s full throttle ≤ 0.85 × the undamaged value.
4. Manual (Gabriel, after D-01c): in `Sprint_01`, drive into a canyon wall at ~100 km/h → overlay HP drops by 25–35; three such crashes → car noticeably slower and looser ("heavy crash visibly changes handling", backlog AC). A clean run with jumps and the rough section costs < 10 HP.
5. No GC allocation in `VehicleDamage.OnCollisionEnter` or `VehicleController.FixedUpdate` (Profiler, 0 B).

## Test plan
- **EditMode:** `ImpactDamage` (below threshold 0, landing uses higher threshold, cap, NaN → 0); `ImpactWindow` (two contacts in window count once, new window after timeout); `VehicleHealth` (clamp at 0, Wrecked, Repair clamps at max, Repair no-op when wrecked, TotalDamageTaken monotonic, negative/NaN ignored); `HandlingModifiers` (exact 1, linear midpoint, min at 0); `VehicleId` equality; overlay line.
- **PlayMode:** as AC 3 (run by orchestrator; tag `[Category("Slow")]`).
- **Manual checklist:** AC 4.

## Perf constraints
`Modifiers` cached (no per-step curve evaluation); no LINQ/boxing; `Damaged` invoked only on change; `TryGetComponent` on the other body only inside `OnCollisionEnter`.

## Risks
- PhysX impulses can spike on deep penetration → cap bounds it; tune thresholds in playtest, not code.
- Contacts with the vehicle's own wheels don't exist (raycast suspension), but body-scraping on mounds may tick small damage → landing threshold + window; check AC 4 "clean run < 10 HP".
- Editing `VehicleController` is the riskiest change in batch 2 → strict edit list + regression test; reviewer diffs line by line.
