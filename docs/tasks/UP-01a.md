# UP-01a — Uplink pure hold logic + UplinkConfig
- **Owner (agent):** ai-engineer            - **Human reviewer/owner:** Gabriel
- **Status:** review
- **Spec:** docs/specs/UP-01.md · ADR-006
- **Blocked by:** — (ADR-006 approval)
- **Allowed files:** `Scripts/Race/UplinkConfig.cs`, `UplinkHold.cs` (new); `Tests/EditMode/UplinkHoldTests.cs` (new) (paths under `Assets/_Convoy/` unless stated)
- **Acceptance criteria:**
  1. Spec AC 1: completes on exactly the 200th 0.02 s step for 4 s; Complete sticky
  2. Spec AC 2: Reset/Decay/speed-limit cases
  3. Zero-alloc `Tick` (`Is.Not.AllocatingGCMemory()`)
  - Always: compiles with 0 errors; all existing tests stay green; no per-frame GC alloc in Update/FixedUpdate hot paths; no hand-edited `.unity`/`.prefab`/`.asset` YAML.
## Verdict log
| Date | Gate | Verdict | Notes |
|---|---|---|---|
| 2026-10-11 | spec | written | architect (ARCH-002); awaiting Gabriel's spec approval |
| 2026-10-11 | spec | APPROVED | Lead AI (delegated); D-023..D-030 accepted |
| 2026-10-11 | build | launched | |
| 2026-10-11 | build | done | 498683c |
| 2026-10-11 | qa | FAIL | CS1061 `Is.Not.AllocatingGCMemory` needs `UnityEngine.TestTools.Constraints` (Unity's extended `Is`) |
| 2026-10-11 | qa-fix | orchestrator | 1-line using alias added (builder rate-limited) |
| 2026-10-11 | qa | PASS | Gabriel's Editor: EditMode 246/246 after fix (daeb646) |
| 2026-10-11 | perf | PASS | Tick verified 0 B with Unity's AllocatingGCMemory constraint |
