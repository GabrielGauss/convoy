# ADR-005 — Netcode direction for M2 (no implementation in M1)

- **Status:** Proposed (architect, ARCH-001, 2026-10-11) — a **recommendation for the post-M1 netcode spike**, not a commitment. · **Human owner/reviewer:** Lead Dev (owns the netcode spike, D-010)
- **Builds on:** D-006 (Race Royale, several crews + heavy AI), D-007 (netcode deferred, rules now), O-003 (hosting cost). **Proposes:** D-020.

## Context
- M2 Technical Direction stages: (1) offline + bots → (2) **LAN test with two machines** → (3) server-authoritative simulation → (4) lobbies/matchmaking/infrastructure.
- Hardest problem: a **shared physics vehicle** with up to 3 people from different machines in it, plus moving-platform aiming (GDD risk 4). PhysX (Unity's built-in physics) is not rollback-friendly, so client-side prediction with re-simulation of a whole vehicle is expensive and complex.
- Constraints: ~45–50 h/week team, no paid services without approval (O-003), Unity 6.3, GameObject-based code (not ECS).

## Options
| Option | Fit | Cost / risk | Verdict |
|---|---|---|---|
| **Netcode for GameObjects (NGO) 2.x** | Unity first-party, GameObject/MonoBehaviour world (ours), host (listen-server) or dedicated server, owner-authoritative or server-authoritative `NetworkTransform`/`NetworkRigidbody`, RPCs, `INetworkSerializable` structs. Free; Unity Transport works on LAN with no service. | No built-in client prediction/rollback. Fine on LAN; online remote pilots will feel latency unless the pilot owns the vehicle. | **Recommended for the M2 spike** |
| Netcode for Entities | Server-authoritative with prediction built in | Requires ECS (DOTS) + Unity Physics — **a rewrite** of vehicle, race and AI. | rejected |
| NGO "distributed authority" | Each client owns objects; good for co-op | Needs Unity's cloud session service (recurring cost, O-003); weak for competitive PvP | rejected for now |
| Photon Fusion 2 | Prediction, lag compensation, physics add-ons, mature | Commercial; CCU pricing → O-003; vendor lock-in | fallback if spike fails |
| Fish-Net | Free/OSS, client prediction v2 | Third-party, smaller ecosystem, prediction still non-trivial with PhysX | fallback if spike fails |

## Recommendation (for the spike, then a decision entry)
1. **Topology:** NGO 2.x, **host-authoritative (listen server)** for LAN; Unity Transport; no relay/lobby services in M2 (free, local).
2. **Authority split:**
   - **Vehicle physics: owned by the pilot's machine** (owner-authoritative rigidbody, replicated to others with interpolation). Gives the pilot zero input latency — critical for the feel Gabriel rated 5/5. Gunner/Engineer on other machines send their seat commands to the vehicle's owner (they tolerate RTT far better than the pilot).
   - **Everything else host-authoritative:** race flow/checkpoints/results, damage & health (`IDamageable` runs on host only), hit validation, AI rivals (simulated on host), power/heat state.
   - Trade-off: owner-authoritative vehicles are cheat-prone and vehicle-vs-vehicle collisions between different owners can jitter. Acceptable for LAN/co-op; the spike must measure it before PvP online.
3. **Replication shape (maps 1:1 onto M1 code):**
   - Up (client → owner/host): `PilotCommand`, `GunnerCommand`, `EngineerCommand` per tick, wrapped as `SeatCommandFrame<T> { uint Tick; SeatId Seat; T Command; }` (`INetworkSerializable` lives in `Convoy.Net`, not Core).
   - Down: `VehicleState` (pose/velocity), `VehicleDamageState`, race state, `SeatOccupant` per seat.
4. **Spike exit criteria (timebox 2 weeks, Lead Dev):** two machines on LAN; one crew vehicle with pilot on machine A and gunner on machine B; 3 host-simulated AI rivals; full Sprint_01 run. Measure: pilot feel with 0/50/100/150 ms simulated latency (Unity Transport network simulator), gunner hit rate vs. offline, bandwidth per vehicle (target < 64 kbit/s at 30 Hz state). Outcome → decision entry choosing NGO or a fallback.
5. **Online (post-spike):** dedicated server and relay costs are an **O-003** decision with explicit approval.

## Rules M1 code must follow now (review checklist, extends D-007)
1. **Simulation only in `FixedUpdate`**, driven by: seat commands + ScriptableObject configs + previous state. Use `Time.fixedDeltaTime`, never `Time.time`/`Time.deltaTime`/`Time.frameCount` in simulation.
2. **Commands are blittable structs in `Convoy.Core`**: only `float`/`int`/`bool`/enums — no references, strings, arrays, `UnityEngine` types. Prefer target state over deltas; one-shot fields are latched by the receiver until consumed by a step.
3. **No device reads in simulation.** Only `Convoy.Input` touches `UnityEngine.InputSystem` (Presentation may read view-only input like look-back via the router).
4. **Seats by `SeatId`, vehicles by `VehicleId`.** No `LocalPlayer` singletons, no `static` mutable gameplay state, no `FindAnyObjectByType` in simulation (allowed in presentation bootstrap and editor scripts).
5. **One choke point per authoritative change:** damage only via `IDamageable.ApplyDamage`; repair only via `VehicleDamage.Repair`; checkpoints only via the race controller; recovery only via `VehicleRecovery`. In M2 each becomes "host only".
6. **State is serializable structs** (`VehicleState`, `VehicleDamageState`, `SprintRun` fields, `SeatActivity`). Events (`Finished`, `Damaged`) are for presentation/telemetry; simulation logic must never depend on an event being delivered.
7. **Deterministic-ish AI:** no `UnityEngine.Random` in simulation; use a seeded `System.Random` owned by the system.
8. **Presentation never writes simulation** (ADR-001). Flow requests (restart) go through named methods that can become server requests.
9. **No persistence in simulation:** `PlayerPrefs`/files only from presentation/telemetry/bootstrap. (Known exception to clean up in R-01: `SprintController` saves the best time.)
10. **Per-seat views:** cameras/HUD are created per seat, never one global "player camera" (D-003). Split-screen and network clients then reuse the same rigs.

## Consequences
- + M1 code needs no netcode package and no shape changes for M2; `Convoy.Net` adds adapters only (ADR-001).
- + Pilot-owned vehicles protect the handling feel, the project's strongest asset after PT-01.
- − PvP online integrity is not solved by this; it's explicitly a spike output.

## Proposed decisions
**D-020 — M2 netcode direction (for the post-M1 spike)** · Status: Proposed (architect) · Owner: Lead Dev
- Spike Netcode for GameObjects 2.x, host-authoritative listen server over Unity Transport on LAN, no paid services.
- Vehicle physics owned by the pilot's machine; race, damage, hits, AI and power state host-authoritative; non-pilot seats send seat commands to the vehicle owner.
- Netcode for Entities rejected (ECS rewrite). Photon Fusion 2 / Fish-Net are fallbacks if the spike fails its latency criteria.
- The 10 network-readiness rules in ADR-005 are part of the review checklist from now on.
