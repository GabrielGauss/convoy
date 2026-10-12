# Spec Q-01 — Race telemetry (CSV per race)

- **Human owner/reviewer:** Lead Dev (code) + Narrative & Game Designer (CSV usability; owns the playtest protocol, D-010) · **Builder agent:** qa-engineer · **Architecture:** ADR-001 (`Convoy.Telemetry` is read-only), ADR-002 (`ISeatStatus`), ADR-003 (`VehicleDamageState`)
- **Split:** **Q-01a** pure recorder + CSV writer + tests · **Q-01b** vehicle counters + self-bootstrapping runtime recorder (no scene edits → no integration script)
- **Must exist before PT-02.**

## Objective
Write one CSV file per race (finished or abandoned) with the metrics of `docs/playtests/TEMPLATE.md` — time, route, flips/resets, damage taken, seat occupancy and activity %, checkpoint splits — so the ND can build a playtest report in < 30 min without watching recordings. Shots/hits and overheats columns exist now and stay empty until G-01/M-01 provide data.

## Scope / out of scope
- In: `Convoy.Telemetry` module, pure `RaceTelemetryRecorder` + `TelemetryCsv`, runtime `RaceTelemetry` bootstrapped next to a `SprintController`, cumulative reset/auto-right counters in Vehicle, column dictionary in `TEMPLATE.md`.
- Out: rival (AI) rows (R-01 extends to all racers), shots/hits/overheats data (G-01/M-01 add sources), in-game survey, uploading anywhere (local files only, no services), charts/report generation.

## CSV format
- File per race: `<root>/Telemetry/race_<yyyyMMdd-HHmmss>_<scene>.csv`, UTF-8, header + exactly one data row.
  `<root>` = `Directory.GetParent(Application.dataPath)` → in the Editor the **project folder** (`/Telemetry/` is already in `.gitignore`), in a Windows build the **folder next to the .exe**. If not writable, fall back to `Application.persistentDataPath/Telemetry`. The full path is logged with `Debug.Log` after writing.
- **All numbers with `CultureInfo.InvariantCulture`** (decimal point `.`). The team's PCs use Argentine Spanish locale, where the default `ToString()` writes `1,5` and breaks the CSV. Booleans `0/1`. Fields containing `;` or `,` are quoted.

| Column | Type | Source / definition |
|---|---|---|
| `race_id` | text | `yyyyMMdd-HHmmss` local start time |
| `scene` | text | active scene name |
| `route` | text | `"main"` until R-01/T-01 provide a route id |
| `vehicle` | text | `VehicleConfig` asset name (e.g. `Jackal_VehicleConfig`) |
| `build` | text | `Application.version` |
| `players` | int | seats with `SeatOccupant.Human` at race start (blank if no `ISeatStatus`) |
| `finished` | 0/1 | `SprintController.Finished` fired |
| `race_time_s` | float | `SprintRun.Elapsed` at finish/abandon (2 decimals) |
| `checkpoints_passed` / `checkpoint_count` | int | `SprintRun.LastCheckpoint + 1` / `CheckpointCount` |
| `splits_s` | text | `;`-separated elapsed time at each checkpoint, quoted |
| `resets` | int | manual resets during the race (`VehicleState.ResetCount` delta) |
| `flips` | int | auto-rights during the race (`VehicleState.AutoRightCount` delta) |
| `damage_taken` | float | `VehicleDamageState.TotalDamageTaken` delta (blank before D-01) |
| `hits_taken` | int | `HitCount` delta |
| `min_health_pct` / `end_health_pct` | float | from `VehicleState.Health01` |
| `max_speed_kmh` / `avg_speed_kmh` | float | from `VehicleState.SpeedKmh` over running ticks |
| `pilot_occupied_pct`, `pilot_active_pct`, `gunner_…`, `engineer_…` | float | from `ISeatStatus.GetActivity` deltas: occupied% = OccupiedSteps / race steps; active% = ActiveSteps / OccupiedSteps (blank when occupied = 0). GDD risk 1 pivot signal: active% < 60 (idle > 40 %). |
| `shots`, `hits`, `overheats` | int | **blank** until G-01/M-01 |

## Public interfaces
```csharp
namespace Convoy.Telemetry   // Scripts/Telemetry/, asmdef refs Core, Vehicle, Race
{
    public struct RaceTelemetryHeader { public string RaceId, Scene, Route, Vehicle, Build; public int Players; public int CheckpointCount; }

    /// <summary>One physics step's readings. Cumulative counters are absolute; the recorder takes deltas from Begin.</summary>
    public struct TelemetrySample
    {
        public float DeltaTime;
        public bool Running;                 // SprintRun.Phase == Running
        public float Elapsed;                // SprintRun.Elapsed
        public int LastCheckpoint;
        public float SpeedKmh;
        public float Health01;
        public bool HasDamage; public float TotalDamageTaken; public int HitCount;
        public int ResetCount; public int AutoRightCount;
        public bool HasSeats; public SeatActivity Pilot, Gunner, Engineer;
    }

    public struct RaceTelemetryResult { /* every CSV column as a typed field; splits in a float[] sized at Begin */ }

    public sealed class RaceTelemetryRecorder
    {
        public RaceTelemetryRecorder(int maxCheckpoints);       // preallocates the splits array
        public bool Started { get; }  public bool Ended { get; }
        public void Begin(in RaceTelemetryHeader header, in TelemetrySample first);
        public void Tick(in TelemetrySample sample);            // no allocation; records a split when LastCheckpoint increases
        public RaceTelemetryResult End(bool finished, in TelemetrySample last);   // idempotent: second call returns the same result
    }

    public static class TelemetryCsv
    {
        public const string Header = "race_id,scene,…";         // exact column order of the table above
        public static string Row(in RaceTelemetryResult result); // invariant culture
        public static string FileName(string raceId, string scene); // invalid filename chars replaced with '_'
    }
}
```
### Q-01b
```csharp
// Vehicle (additive only): VehicleRecovery gains `public int ResetCount { get; }` and `public int AutoRightCount { get; }`
// incremented in Step() when it returns Reset / AutoRight; VehicleState gains `public int ResetCount; public int AutoRightCount;`
// written in VehicleController.WriteState from _recovery.

namespace Convoy.Telemetry
{
    /// <summary>Self-bootstraps next to a SprintController (same pattern as SprintHud: RuntimeInitializeOnLoadMethod
    /// AfterSceneLoad + SceneManager.sceneLoaded). Samples in FixedUpdate after the vehicle; writes the CSV on
    /// SprintController.Finished or, if the run started but didn't finish, in OnDestroy (restart / scene change / quit).</summary>
    [DefaultExecutionOrder(50)]
    public sealed class RaceTelemetry : MonoBehaviour
    {
        public string LastFilePath { get; }
    }
}
```
Sources resolved once at start: `SprintController` (same GameObject), its `Vehicle`, `TryGetComponent<VehicleDamage>`, `GetComponent<ISeatStatus>()` on the vehicle (null before F-03c → seat columns blank). Begin when `SprintRun.Phase` becomes `Running`.

## Dependencies
- Q-01a blocked by: — (only Core types `SeatActivity` from F-03a; if F-03a isn't merged yet, Q-01a is blocked by F-03a).
- Q-01b blocked by: Q-01a, **D-01b** (both edit `VehicleController.cs`/`VehicleState.cs`; serialise), F-03b (`ISeatStatus` implementation; seat columns are blank without it, so not a hard block if late).
- Blocks: PT-02, A-01 human-reference time capture (optional), LV-02b (next editor of `VehicleController.cs`/`VehicleState.cs`, ADR-006 § 9).
- ADR-006 notes: with an uplink zone (UP-01) `Finished` fires when the hold completes, so `finished`/`race_time_s` include the hold — no change needed. Once RT-01a is merged, `splits_s` may read `SprintRun.TryGetSplit` instead of tracking splits itself (optional).

## Allowed files
- **Q-01a:** `Assets/_Convoy/Scripts/Telemetry/**` (new asmdef + `RaceTelemetryRecorder.cs`, `TelemetryCsv.cs`, structs), `Assets/_Convoy/Tests/EditMode/Telemetry/**` (new test asmdef + tests).
- **Q-01b:** `Assets/_Convoy/Scripts/Telemetry/RaceTelemetry.cs`; `Assets/_Convoy/Scripts/Vehicle/VehicleRecovery.cs`, `VehicleState.cs`, `VehicleController.cs` (**additive only**: the counters above, nothing else); `Assets/_Convoy/Tests/EditMode/VehicleRecoveryTests.cs` (counter tests); `docs/playtests/TEMPLATE.md` (add a "CSV column → table column" mapping line under Metrics).

## Acceptance criteria
1. EditMode tests green; `TelemetryCsv.Row` output parses back to the same values with `CultureInfo.InvariantCulture` **while `CultureInfo.CurrentCulture` is set to `es-AR`** in the test.
2. Recorder: splits recorded once per checkpoint in order; deltas exclude counts from before `Begin`; `End` twice → identical result; `Tick` allocates 0 B (`GC.GetAllocatedBytesForCurrentThread` before/after 1 000 ticks in the test).
3. Play `Sprint_01` and finish → exactly one new CSV in `<project>/Telemetry/` with `finished=1`, `checkpoints_passed=5`, 5 splits, `race_time_s` equal to the HUD time ± 0.02 s, `resets` equal to R presses that actually reset.
4. Restart mid-run (Enter after starting) → one CSV with `finished=0`. Restart before moving → no CSV.
5. ND opens two CSVs in Google Sheets/Excel (es-AR locale, import as CSV with `.` decimal) and fills the TEMPLATE metrics table in < 30 min for 6 races (checked at PT-02 prep).
6. Perf: `RaceTelemetry.FixedUpdate` < 0.02 ms, 0 B GC; file IO only at race end.

## Test plan
- **EditMode:** `TelemetryCsvTests` (header column count = row column count; quoting; es-AR culture; filename sanitising), `RaceTelemetryRecorderTests` (splits, deltas, percentages incl. occupied = 0 → blank, idempotent End, zero-alloc Tick), `VehicleRecoveryTests` additions (ResetCount/AutoRightCount increment exactly once per action, not on cooldown-blocked requests).
- **Manual checklist (orchestrator + Gabriel):** AC 3 and 4 in `Sprint_01`; open the CSV and compare with the HUD.

## Risks
- `OnDestroy` during application quit may run after some objects are gone → only use cached values captured in `FixedUpdate`, wrap the write in try/catch and log failures (never throw into Unity's shutdown).
- Seat activity is meaningless while one human hot-seats → `players` column makes that visible.
- Editing Vehicle files from a QA task: kept to two counters, serialised after D-01b.
