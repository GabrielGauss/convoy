# PR-01b — SprintController catalog/LoadSprint, SprintSelectOverlay, unlock line
- **Owner (agent):** ui-input-engineer            - **Human reviewer/owner:** Gabriel
- **Status:** cancelled (D-031 — open terrain, no medals)
- **Spec:** docs/specs/PR-01.md · ADR-006
- **Blocked by:** PR-01a, RT-01c (SprintHud.cs chain; SprintController.cs chain via RT-01b)
- **Allowed files:** `Scripts/Race/SprintController.cs`, `Scripts/Presentation/SprintHud.cs` (listed edits only); `Scripts/Presentation/SprintSelectOverlay.cs` (new) (paths under `Assets/_Convoy/` unless stated)
- **Acceptance criteria:**
  1. Spec AC 2: locked LoadSprint refused
  2. Spec AC 4: no FixedUpdate alloc; overlay allocates on open only
  3. Overlay opens only on the result screen
  - Always: compiles with 0 errors; all existing tests stay green; no per-frame GC alloc in Update/FixedUpdate hot paths; no hand-edited `.unity`/`.prefab`/`.asset` YAML.
## Verdict log
| Date | Gate | Verdict | Notes |
|---|---|---|---|
| 2026-10-11 | spec | written | architect (ARCH-002); awaiting Gabriel's spec approval |
