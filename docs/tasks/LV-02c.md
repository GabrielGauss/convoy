# LV-02c — Integration: toxic mud profile, material, MudPatch prefab, Sandbox patch
- **Owner (agent):** vehicle-engineer (script) → orchestrator (runs)            - **Human reviewer/owner:** Gabriel
- **Status:** spec
- **Spec:** docs/specs/LV-02.md · ADR-006
- **Blocked by:** LV-02b, LV-01c (Sandbox owner chain), F-03c
- **Allowed files:** `Editor/Integration/LV02_MudSetup.cs` (new). Orchestrator commits `Config/Surfaces/ToxicMud_SurfaceProfile.asset`, `Art/Materials/ToxicMud.mat`, `Prefabs/Level/MudPatch.prefab`, `Sandbox.unity`, metas (paths under `Assets/_Convoy/` unless stated)
- **Acceptance criteria:**
  1. Spec AC 4 manual: mud drags and loosens the car, unchanged outside, nitro escapes
  - Always: script idempotent (second run logs 0 changes), never deletes, exposes `ApplyToScene(string scenePath)` where it touches a scene (D-030); builder never runs Unity — orchestrator runs it and commits the generated diff.
## Verdict log
| Date | Gate | Verdict | Notes |
|---|---|---|---|
| 2026-10-11 | spec | written | architect (ARCH-002); awaiting Gabriel's spec approval |
