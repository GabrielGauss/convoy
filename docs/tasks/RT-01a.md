# RT-01a — Splits in SprintRun, medals, SprintDefinition, save model + store
- **Owner (agent):** ui-input-engineer            - **Human reviewer/owner:** Gabriel
- **Status:** cancelled (D-031 — open terrain, no medals)
- **Spec:** docs/specs/RT-01.md · ADR-006
- **Blocked by:** —
- **Allowed files:** `Scripts/Race/SprintRun.cs` (additive), `Medals.cs`, `SprintDefinition.cs`, `SaveData.cs`, `SaveStore.cs` (new); `Tests/EditMode/SprintRunTests.cs` (add), `MedalRulesTests.cs`, `SaveStoreTests.cs` (new) (paths under `Assets/_Convoy/` unless stated)
- **Acceptance criteria:**
  1. Spec AC 1: splits; existing SprintRun tests unchanged and green
  2. Spec AC 2: medal boundaries at hundredths
  3. Spec AC 3: JSON round-trip, corrupt/newer moved aside, medal never lowered, legacy import once
  - Always: compiles with 0 errors; all existing tests stay green; no per-frame GC alloc in Update/FixedUpdate hot paths; no hand-edited `.unity`/`.prefab`/`.asset` YAML.
## Verdict log
| Date | Gate | Verdict | Notes |
|---|---|---|---|
| 2026-10-11 | spec | written | architect (ARCH-002); awaiting Gabriel's spec approval |
| 2026-10-11 | spec | APPROVED | Lead AI (delegated); D-023..D-030 accepted |
| 2026-10-11 | build | launched | |
| 2026-10-11 | build | relaunched | |
