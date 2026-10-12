# ARCH-003 — Re-plan for the D-035 MVP: track kit, vehicle classes, impacted specs
- **Owner (agent):** architect (opus)            - **Human reviewer/owner:** Gabriel + Lead Dev
- **Status:** todo
- **Blocked by:** PR #10 merged (D-033..D-036, `docs/BACKLOG_MVP.md`)
- **Allowed files:** `docs/adr/ADR-007-mvp-track-classes.md` (new), `docs/tracks/T01/T01_topology.{md,json}` (new), `docs/specs/{TK-00,TK-01,VH-01,VH-02}.md` (new), `docs/tasks/{TK-00,TK-01,VH-01,VH-02}.md`, consistency edits to existing `docs/specs/*.md` and `docs/tasks/*.md` listed below (note each edit in the report)
- **Acceptance criteria:**
  1. **ADR-007:** module placement for the spline track kit (Level), vehicle classes (`VehicleClass` + per-class `VehicleConfig`, seat layout per class in Crew), class-gated pieces (Level), O-004 falling toggle (Race, recovery provider), and how AI racing lines come from track splines (AI, D-019). Confirms or amends D-036 (Unity Splines package = new package → decision entry).
  2. **Specs** for TK-00 (track kit spike), TK-01 (segment 1, ~2.5 km, three layers, O-004 toggle, route within ~5 km of origin), VH-01 (three-wheel sidecar on the existing raycast controller, 2 seats, lean = presentation only), VH-02 (heavy), each reviewable in < 20 min.
  3. **Impact review of open tasks**, each answered keep / amend / pause in the report:
     - LV-01 Rammer gate: break condition becomes class-based (heavy only), not speed-only.
     - LV-02 mud: per-class traction multipliers (heavy partly immune, sidecar worst).
     - F-03 seats: seat layout per class (sidecar = Pilot + Gunner).
     - D-01 damage: per-class `DamageConfig` (sidecar fragile, heavy armoured).
     - A-01 rivals: one rival per class on segment 1 (keep Sprint_01 integration as the test course).
     - C-01 camera: framing per class (sidecar low and fast, heavy large).
     - UP-01: uplink at the end of the track (keep Sprint_01 integration as is).
     - TR-01c: paused; say whether TR-01a terrain synthesis is reused for the backdrop.
  4. Phases 2–4 of `docs/BACKLOG_MVP.md` stay as backlog items; no specs for them yet.
  5. **T01 topology (D-037):** read `docs/tracks/T01/T01_HANDMAP.md` and the six map images; produce the node graph `docs/tracks/T01/T01_topology.md` + `.json` (nodes, typed edges, lengths scaled to ~10 km, crossing level types, checks listed in the HANDMAP). TK-01's spec = segment 1 (~2.5 km from START) of that graph, including its perk connectors. Resolve review points 3–6 as proposals for Gabriel; open point 8 is a question for him.
  - Always: no gameplay numbers in code; scene/prefab steps only as orchestrator editor scripts (D-016, D-030).
## Verdict log
| Date | Gate | Verdict | Notes |
|---|---|---|---|
| 2026-10-12 | todo | created | Lead AI, from Gabriel's MVP decisions (D-035) |
| 2026-10-12 | todo | amended | Added AC 5: T01 hand-drawn topology input (D-037) |
