# LV-03a — BiomeLayout data + layout/heightfield maths + validator (pure)
- **Owner (agent):** level-builder            - **Human reviewer/owner:** Narrative & Game Designer + Gabriel
- **Status:** spec
- **Spec:** docs/specs/LV-03.md · ADR-006
- **Blocked by:** LV-01a (Convoy.Level + test asmdef)
- **Allowed files:** `Scripts/Level/BiomeLayout.cs`, `LayoutMath.cs`, `HeightfieldMath.cs`, `LayoutValidator.cs` (new); `Tests/EditMode/Level/LayoutMathTests.cs`, `HeightfieldMathTests.cs`, `LayoutValidatorTests.cs` (new) (paths under `Assets/_Convoy/` unless stated)
- **Acceptance criteria:**
  1. Spec AC 1: ballistic min speed, pose sampling, deterministic heightfield
  2. Spec AC 2: every validation rule has a passing and a failing case
  - Always: compiles with 0 errors; all existing tests stay green; no per-frame GC alloc in Update/FixedUpdate hot paths; no hand-edited `.unity`/`.prefab`/`.asset` YAML.
## Verdict log
| Date | Gate | Verdict | Notes |
|---|---|---|---|
| 2026-10-11 | spec | written | architect (ARCH-002); awaiting Gabriel's spec approval |
