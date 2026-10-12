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
| 2026-10-11 | spec | done | architect: deliverables complete — ADR-001..005 (`docs/adr/`), specs F-03/D-01/A-01/C-01/Q-01 (`docs/specs/`), 13 split task files. Proposed D-015 (Cinemachine, in C-01 spec), D-016..D-020 (in ADRs); DECISIONS.md untouched. No asmdefs created (builders create them per ADR-001). |

## Open questions for humans
1. **Accept D-015 (Cinemachine 3)?** C-01a is blocked until then (Gabriel + Lead Dev).
2. **Damage depth (D-018):** single hull pool for M1, per-subsystem damage only if PT-02 shows Engineer active % < 60. OK? (Gabriel)
3. **O-001:** recommendation recorded (AI assist at reduced competence) but deferred to after PT-02 (K-02, P2); M1 empty stations idle. Confirm. (Gabriel + ND)
4. **D-014 vs T-01:** A-01/Q-01 target `Sprint_01` (single route "main"); route columns and fork choice wait for T-01. Is Sprint_01 the M1 race track or is T-01 still a loop? (ND)
5. **Human reference time** for A-01 AC 4: Gabriel's best `Sprint_01` time needs recording (provisional bound ≤ 2:00).
6. **Removal of `PilotInputSource` / `SandboxChaseCamera`:** integration scripts only disable them; deleting needs explicit approval after F-03c/C-01b pass.
7. **D-020 (M2 netcode):** pilot-owned vehicle physics + host-authoritative everything else, NGO 2.x spike. Lead Dev to confirm direction before the post-M1 spike.

## Suggested batch-2 order (file conflicts serialised)
Wave 1 (parallel): F-03a, D-01a, A-01a · then Q-01a (needs F-03a), C-01a (needs D-015).
Wave 2: F-03b, D-01b, A-01b → Q-01b (after D-01b).
Wave 3 (orchestrator, one at a time — shared prefab/scenes): F-03c → D-01c → A-01c → C-01b.
