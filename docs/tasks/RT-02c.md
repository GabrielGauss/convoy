# RT-02c — Integration: ghost material asset (Resources)
- **Owner (agent):** vehicle-engineer (script) → orchestrator (runs)            - **Human reviewer/owner:** Gabriel
- **Status:** cancelled (D-031 — open terrain, no medals)
- **Spec:** docs/specs/RT-02.md · ADR-006
- **Blocked by:** RT-02b, F-03c (editor asmdef)
- **Allowed files:** `Editor/Integration/RT02_GhostMaterialSetup.cs` (new). Orchestrator commits `Resources/Convoy/GhostMaterial.mat`, metas. No scene edits (paths under `Assets/_Convoy/` unless stated)
- **Acceptance criteria:**
  1. Spec AC 6 manual: translucent cyan ghost of the PB, no collision, fade, G toggle, frame time ± 0.2 ms
  - Always: script idempotent (second run logs 0 changes), never deletes, exposes `ApplyToScene(string scenePath)` where it touches a scene (D-030); builder never runs Unity — orchestrator runs it and commits the generated diff.
## Verdict log
| Date | Gate | Verdict | Notes |
|---|---|---|---|
| 2026-10-11 | spec | written | architect (ARCH-002); awaiting Gabriel's spec approval |
