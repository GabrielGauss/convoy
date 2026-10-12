# LV-02a — Surface profile, modifiers and per-wheel cache (pure)
- **Owner (agent):** vehicle-engineer            - **Human reviewer/owner:** Gabriel
- **Status:** spec
- **Spec:** docs/specs/LV-02.md · ADR-006
- **Blocked by:** —
- **Allowed files:** `Scripts/Vehicle/SurfaceProfile.cs`, `WheelSurfaceCache.cs` (new); `Tests/EditMode/SurfaceModifiersTests.cs` (new) (paths under `Assets/_Convoy/` unless stated)
- **Acceptance criteria:**
  1. Spec AC 1: `None` exactly 1f per field; NaN → 1f; cache hit/miss/clear; zero-alloc
  - Always: compiles with 0 errors; all existing tests stay green; no per-frame GC alloc in Update/FixedUpdate hot paths; no hand-edited `.unity`/`.prefab`/`.asset` YAML.
## Verdict log
| Date | Gate | Verdict | Notes |
|---|---|---|---|
| 2026-10-11 | spec | written | architect (ARCH-002); awaiting Gabriel's spec approval |
