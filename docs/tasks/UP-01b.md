# UP-01b — UplinkZone, SprintController uplink hook, UplinkHud, PlayMode test
- **Owner (agent):** ai-engineer            - **Human reviewer/owner:** Gabriel
- **Status:** qa
- **Spec:** docs/specs/UP-01.md · ADR-006
- **Blocked by:** UP-01a, D-01b (PlayMode asmdef), A-01b if in flight (same asmdef)
- **Allowed files:** `Scripts/Race/UplinkZone.cs` (new); `Scripts/Race/UplinkRingMath.cs` (new, orchestrator-approved UP-01a review follow-up); `Scripts/Race/SprintController.cs` (5 listed edits only); `Scripts/Presentation/UplinkHud.cs` (new); `Tests/EditMode/UplinkRingMathTests.cs`, `Tests/PlayMode/UplinkTests.cs` (new); `Tests/PlayMode/Convoy.Tests.PlayMode.asmdef` (add `Convoy.Race` only) (paths under `Assets/_Convoy/` unless stated)
- **Acceptance criteria:**
  1. Spec AC 3: legacy finish gate still finishes instantly without a zone
  2. Spec AC 4: PlayMode hold finishes 4.0–4.1 s after entering; leaving prevents finish
  3. Spec AC 6: 0 B GC in SprintController.FixedUpdate / UplinkHud.OnGUI
  4. SprintController diff limited to the 5 listed edits
  - Always: compiles with 0 errors; all existing tests stay green; no per-frame GC alloc in Update/FixedUpdate hot paths; no hand-edited `.unity`/`.prefab`/`.asset` YAML.
## Verdict log
| Date | Gate | Verdict | Notes |
|---|---|---|---|
| 2026-10-11 | spec | written | architect (ARCH-002); awaiting Gabriel's spec approval |
