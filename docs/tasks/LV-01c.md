# LV-01c — Integration: RammerGate prefab, config, materials, Sandbox test gate
- **Owner (agent):** combat-engineer (script) → orchestrator (runs)            - **Human reviewer/owner:** Gabriel
- **Status:** spec
- **Spec:** docs/specs/LV-01.md · ADR-006
- **Blocked by:** LV-01b, F-03c (editor asmdef), C-01b (Sandbox owner chain)
- **Allowed files:** `Editor/Integration/LV01_RammerGateSetup.cs` (new), `Editor/Convoy.Editor.asmdef` (add `Convoy.Level`). Orchestrator commits `Prefabs/Level/RammerGate.prefab`, `Config/Level/RammerGate_Default.asset`, `Art/Materials/RammerRock.mat`, `RammerMark.mat`, `Sandbox.unity`, metas (paths under `Assets/_Convoy/` unless stated)
- **Acceptance criteria:**
  1. Spec AC 4 manual: wall at ~60 km/h, shatters at ≥ 80 km/h, no flip 5/5, reload restores, marks read as breakable
  2. Existing prefab never overwritten
  - Always: script idempotent (second run logs 0 changes), never deletes, exposes `ApplyToScene(string scenePath)` where it touches a scene (D-030); builder never runs Unity — orchestrator runs it and commits the generated diff.
## Verdict log
| Date | Gate | Verdict | Notes |
|---|---|---|---|
| 2026-10-11 | spec | written | architect (ARCH-002); awaiting Gabriel's spec approval |
