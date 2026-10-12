# RT-01b — SprintController: save, PB splits, medals, attempts, quick-restart request
- **Owner (agent):** ui-input-engineer            - **Human reviewer/owner:** Gabriel
- **Status:** spec
- **Spec:** docs/specs/RT-01.md · ADR-006
- **Blocked by:** RT-01a, UP-01b (SprintController.cs owner chain)
- **Allowed files:** `Scripts/Race/SprintController.cs` (6 listed edits only), `Scripts/Race/AssemblyInfo.cs` (new); `Tests/PlayMode/SprintSaveTests.cs` (new) (paths under `Assets/_Convoy/` unless stated)
- **Acceptance criteria:**
  1. Spec AC 4: PlayMode save written with finishes/attempts/splits; slower run not a PB
  2. Spec AC 5: no definition → today's behaviour
  3. Save IO only at finish/restart; 0 B GC in FixedUpdate
  - Always: compiles with 0 errors; all existing tests stay green; no per-frame GC alloc in Update/FixedUpdate hot paths; no hand-edited `.unity`/`.prefab`/`.asset` YAML.
## Verdict log
| Date | Gate | Verdict | Notes |
|---|---|---|---|
| 2026-10-11 | spec | written | architect (ARCH-002); awaiting Gabriel's spec approval |
