# QA-CI-01 — Cloud compile + EditMode test harness (no Unity)
- **Owner (agent):** qa-engineer            - **Human reviewer/owner:** Lead Dev
- **Status:** cancelled
- **Blocked by:** —
- **Allowed files:** `tools/ci/**` (new), `docs/tasks/QA-CI-01.md`, `README.md` (one short section)
- **Acceptance criteria:**
  1. `tools/ci/run.sh` generates one .NET project per Unity asmdef under `Assets/_Convoy/**` (references, `noEngineReferences`, Editor-only test asmdefs), compiles them against a minimal UnityEngine/Input System stub assembly + real NUnit, and runs every EditMode test that does not need a live engine.
  2. asmdef dependency violations fail the build (a reference not declared in the asmdef cannot compile).
  3. Reports: compile errors per assembly, tests passed/failed/skipped; exit code non-zero on failure.
  4. Tests that need real Unity behaviour (ScriptableObject.CreateInstance, physics, scenes) are either supported by faithful stubs or listed as "Unity-only" and skipped with a reason — never silently.
  5. Documented limits: this is a pre-gate; Gabriel's Unity Editor remains the final QA gate.
## Verdict log
| Date | Gate | Verdict | Notes |
|---|---|---|---|
| 2026-10-11 | launch | — | Gabriel unavailable for Editor QA; cloud pre-gate requested by orchestrator |
| 2026-10-11 | — | cancelled | Gabriel's Editor available again; agent run rejected by Gabriel |
