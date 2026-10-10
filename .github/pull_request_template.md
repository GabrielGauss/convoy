## Task
ID: <!-- e.g. V-01 --> · Branch: `feat/...`

## What changed
-

## How it was verified
- [ ] Project compiles with 0 errors
- [ ] EditMode / PlayMode tests pass (list them)
- [ ] Tested in Play mode: <!-- what you did -->

## Architecture checklist (D-007)
- [ ] Simulation does not reference Presentation
- [ ] Player/AI actions go through per-seat command structs
- [ ] No `LocalPlayer` singletons; seats addressed by `SeatId`
- [ ] Gameplay numbers live in ScriptableObject configs
- [ ] No scene edited without the scene lock

## Docs
- [ ] BACKLOG.md status updated · [ ] DECISIONS.md entry added (if needed)
