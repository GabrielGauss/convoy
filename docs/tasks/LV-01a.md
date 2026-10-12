# LV-01a — Convoy.Level module: RammerGateConfig, rules, fracture layout
- **Owner (agent):** combat-engineer            - **Human reviewer/owner:** Gabriel
- **Status:** pr
- **Spec:** docs/specs/LV-01.md · ADR-006
- **Blocked by:** — (ADR-006 / D-023 approval)
- **Allowed files:** `Scripts/Level/Convoy.Level.asmdef`, `RammerGateConfig.cs`, `RammerGateRules.cs`, `WallFractureLayout.cs` (new); `Tests/EditMode/Level/**` (new test asmdef + `RammerGateRulesTests.cs`, `WallFractureLayoutTests.cs`) (paths under `Assets/_Convoy/` unless stated)
- **Acceptance criteria:**
  1. Spec AC 1: closing speed / break threshold cases
  2. Spec AC 2: layout tiles the wall exactly, deterministic by seed
  3. asmdef references `Convoy.Core` only
  - Always: compiles with 0 errors; all existing tests stay green; no per-frame GC alloc in Update/FixedUpdate hot paths; no hand-edited `.unity`/`.prefab`/`.asset` YAML.
## Verdict log
| Date | Gate | Verdict | Notes |
|---|---|---|---|
| 2026-10-11 | spec | written | architect (ARCH-002); awaiting Gabriel's spec approval |
| 2026-10-11 | spec | APPROVED | Lead AI (delegated); D-023..D-030 accepted |
| 2026-10-11 | build | launched | |
| 2026-10-11 | build | relaunched | |
| 2026-10-12 | build | done | 035af4c (combat-engineer; qa pending in Unity Editor) |
| 2026-10-12 | review | APPROVE | Convoy.Level refs Core only (D-023); config SO matches spec; 0.01 km/h ShouldBreak tolerance is justified in a why-comment and keeps 22.2 m/s failing (AC1 ok); layout tiles exactly (per-column row seams, jitter clamp 0.45 keeps sizes >0); AC1-2 fully tested + overlap/tilt/clamp/null cases. QA 278/278 green in Editor (orchestrator). Notes for LV-01b perf gate: DebrisVelocity allocs one System.Random per chunk at break time (spec-mandated, one-shot); upward-only debris y bias is a spec refinement, documented. Nit: test asmdef's Convoy.Core ref unused. |
