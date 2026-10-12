# RT-01c — SprintHud: live split deltas, medal target, result screen, quick restart
- **Owner (agent):** ui-input-engineer            - **Human reviewer/owner:** Gabriel
- **Status:** spec
- **Spec:** docs/specs/RT-01.md · ADR-006
- **Blocked by:** RT-01b
- **Allowed files:** `Scripts/Presentation/SprintHud.cs`, `SprintHudText.cs` (new); `Tests/EditMode/SprintHudTextTests.cs` (new) (paths under `Assets/_Convoy/` unless stated)
- **Acceptance criteria:**
  1. `SprintHudText` tests incl. es-AR culture
  2. HUD allocates only when displayed text changes (spec AC 7)
  3. Legacy (no definition) HUD identical to today
  - Always: compiles with 0 errors; all existing tests stay green; no per-frame GC alloc in Update/FixedUpdate hot paths; no hand-edited `.unity`/`.prefab`/`.asset` YAML.
## Verdict log
| Date | Gate | Verdict | Notes |
|---|---|---|---|
| 2026-10-11 | spec | written | architect (ARCH-002); awaiting Gabriel's spec approval |
