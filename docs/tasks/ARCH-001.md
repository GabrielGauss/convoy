# ARCH-001 — M1 architecture ADRs + specs for batch 2

- **Owner (agent):** architect (opus)            - **Human reviewer/owner:** Gabriel + Lead Dev
- **Status:** spec → building
- **Spec:** this file (meta-task)
- **Blocked by:** —
- **Allowed files:** `docs/adr/**`, `docs/specs/**`, `docs/tasks/<batch-2 ids>.md`, `Assets/_Convoy/Scripts/**/*.asmdef` (new modules only, empty folders + asmdef)
- **Acceptance criteria:**
  1. ADR-001 module & assembly map: existing (Core, Vehicle, Input, Race, Presentation, Tests) + new for M1 (Crew/Stations, Combat, AI, Telemetry); allowed dependency directions; where netcode will plug in (M2).
  2. ADR-002 seats & stations contract: `SeatId`, per-seat command structs (`PilotCommand` exists; `GunnerCommand`, `EngineerCommand`), how input devices map to seats, how AI fills empty seats (O-001).
  3. ADR-003 damage & health data model (ScriptableObjects), how damage reaches Vehicle without Combat → Vehicle cycles.
  4. ADR-004 AI driving approach: spline/waypoint path following driving through `PilotCommand` (same interface as humans), no NavMesh.
  5. ADR-005 netcode (M2 only): recommendation + what M1 code must respect so it stays network-ready (D-007).
  6. Specs, one per batch-2 task, each with objective, public interfaces (C# signatures), dependencies, allowed files, measurable acceptance criteria, test plan: **F-03** input+seats, **D-01** damage model, **A-01** AI driving logic, **C-01** Cinemachine camera, **Q-01** telemetry.
  7. Task files `docs/tasks/<id>.md` for those five, status `spec`.
  8. Consistent with `CLAUDE.md`, `docs/DECISIONS.md` (D-001..D-014), `docs/TECH_DESIGN.md`, `docs/BACKLOG.md`.

## Verdict log
| Date | Gate | Verdict | Notes |
|---|---|---|---|
| 2026-10-11 | launch | — | Gabriel approved roster/roadmap; architect launched |
