# UP-01b — UplinkZone, SprintController uplink hook, UplinkHud, PlayMode test
- **Owner (agent):** ai-engineer            - **Human reviewer/owner:** Gabriel
- **Status:** pr
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
| 2026-10-12 | build | done | 6bc085c |
| 2026-10-12 | review | REQUEST_CHANGES | Core uplink is correct and to spec (both-or-neither wiring, polled Contains, CompleteFinish extraction behaviour-identical, gate ignored with zone, 101-label table, 0 B GC by inspection + UplinkHold.Tick constraint, D-007 HUD read-only). But task AC4 fails: SprintController has a 6th edit (_uplink.ReportCompleted) and UplinkZone adds an unspecified public API (Winner/HasWinner/Completed/ReportCompleted/Track/Untrack/GetTrackedHold/_trackedConfig + FixedUpdate) that pre-empts R-01 (spec lists rivals in the zone as out of scope) and has a double-tick footgun (Track on a SprintController-owned vehicle). Required (doc-only, preferred): architect amends UP-01 spec + ADR-006 s5 to adopt the D-031 winner API and the 6th edit, and Track() refuses/logs a vehicle already owned by a SprintController - or revert both to spec. Minor: Awake comment says Contains is unscaled but InverseTransformPoint applies scale while the gizmo ignores it. |
| 2026-10-12 | review (cycle 2) | APPROVE | AC4 now met: UP-01 spec D-031 amendment lists the 6th SprintController edit (ReportCompleted) and the UplinkZone winner API (consumed by R-01 later). Double-tick closed: Track() rejects a SprintController-owned vehicle (warning, null); FindObjectsByType there is setup-time only, zone FixedUpdate still 0 B GC. UplinkTests 5/5 in Editor. Non-blocking: guard only checks at Track time (a SprintController wired later isn't caught); mirror the amendment in ADR-006 s5; Awake comment on scale still inaccurate; same #if UNITY_EDITOR player-test note as A-01b. |
