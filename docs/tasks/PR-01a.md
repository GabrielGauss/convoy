# PR-01a — SprintCatalog + ProgressionRules (pure)
- **Owner (agent):** ui-input-engineer            - **Human reviewer/owner:** Gabriel
- **Status:** spec
- **Spec:** docs/specs/PR-01.md · ADR-006
- **Blocked by:** RT-01a
- **Allowed files:** `Scripts/Race/SprintCatalog.cs`, `ProgressionRules.cs` (new); `Tests/EditMode/ProgressionRulesTests.cs` (new) (paths under `Assets/_Convoy/` unless stated)
- **Acceptance criteria:**
  1. Spec AC 1: unlock by Silver, newly-unlocked once, medal never lost, lowered threshold honoured
  - Always: compiles with 0 errors; all existing tests stay green; no per-frame GC alloc in Update/FixedUpdate hot paths; no hand-edited `.unity`/`.prefab`/`.asset` YAML.
## Verdict log
| Date | Gate | Verdict | Notes |
|---|---|---|---|
| 2026-10-11 | spec | written | architect (ARCH-002); awaiting Gabriel's spec approval |
