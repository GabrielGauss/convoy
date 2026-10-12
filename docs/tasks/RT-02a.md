# RT-02a — Ghost track, sampler and binary file format (pure)
- **Owner (agent):** vehicle-engineer            - **Human reviewer/owner:** Gabriel
- **Status:** spec
- **Spec:** docs/specs/RT-02.md · ADR-006
- **Blocked by:** —
- **Allowed files:** `Scripts/Race/Ghost.cs` (new); `Tests/EditMode/GhostTrackTests.cs`, `GhostFileTests.cs` (new) (paths under `Assets/_Convoy/` unless stated)
- **Acceptance criteria:**
  1. Spec AC 1–3: interpolation, clamp, teleport snap, sampler rate, bit-exact round-trip, bad files rejected
  2. Spec AC 4: two tracks ≤ 512 KB; zero-alloc TryAdd/Sample
  - Always: compiles with 0 errors; all existing tests stay green; no per-frame GC alloc in Update/FixedUpdate hot paths; no hand-edited `.unity`/`.prefab`/`.asset` YAML.
## Verdict log
| Date | Gate | Verdict | Notes |
|---|---|---|---|
| 2026-10-11 | spec | written | architect (ARCH-002); awaiting Gabriel's spec approval |
