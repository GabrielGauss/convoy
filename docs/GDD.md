# CONVOY — Game Design Document (living)

Owner: Narrative & Game Designer · Creative director: Gabriel. Source vision: project brief "01 — Game Design & Vision".

## One-liner
A third-person **Race Royale**: crews of 2–3 drive, fight, and patch up a single retro-industrial war-machine across a hostile wasteland packed with rival crews, AI convoys, and bosses. Few reach the prize (D-006).

## Pillars
1. The crew wins, not the individual.
2. Every advantage has a trade-off (armor = weight, power = heat, ammo = space).
3. Terrain is an active opponent.
4. Combat creates racing opportunities rather than replacing racing.
5. Every crew station must make meaningful decisions.

## Format (D-006)
- **Online:** several crews + heavy AI (rival convoys, ambushes, bosses) on a long, branching route. Attrition is the point: weak crews may not finish.
- **Offline/co-op:** crews vs. AI.
- Open: crews per match, elimination vs. respawn, what "the prize" is (O-002).

## Crew stations (D-002)
| Station | Owns | Key decisions |
|---|---|---|
| Pilot | Driving, traction, drift, recovery | Line, risk, when to brake for the Gunner |
| Gunner | Turret, ammo, overheat, defenses | Target choice, ammo economy, suppress vs. finish |
| Engineer (Mechanic + Tactician) | Power split, heat, repairs, map/route intel, pings | Boost vs. weapons vs. cooling, repair now vs. later, route call |

2 players: the non-pilot switches stations. Empty station handling: O-001.

## Vehicles
- **R-01 JACKAL** — light, agile, fast acceleration, low armor/payload. *M1 vehicle.*
- **H-02 MULE** — heavy, durable, more payload/weapons, slower. *Post-M1.*

## Race (M1)
3–5 min greybox loop, ≥ 6 checkpoints, one fork (short/risky vs. long/safe), jump, rough terrain, 3 AI rivals. Shipping target length: 10–15 min (playtest-dependent, D-008).

## Visual identity
1970s–80s retro-industrial retrofuturism: analog tech, industrial machinery, hostile wastelands, sponsor culture, improvised engineering. *Not in scope for M1 (greybox).*

## Risks (ranked)
1. **Crew workload** — non-pilot seats idle or overwhelmed. Test: station telemetry + "when were you bored?". Pivot signal: any seat idle > 40%.
2. **Handling** — arcade drift vs. instability on rough terrain; trade-offs must be felt. Test: PT-01 gate.
3. **Camera/nausea** — passengers in a drifting vehicle. Test: ask every playtest; roll-stabilized Gunner cam.
4. **Multiplayer** — shared physics vehicle + moving-platform aiming under latency; Race Royale scale raises server cost. Mitigation: D-007 rules now, spike after M1.
5. **Scope** — Race Royale + heavy AI + bosses is large for ~45–50 h/week. M1 proves one loop only.

## Narrative
TBD (ND). Placeholder: world bible, factions/sponsors, why crews race, what the prize is — tie "the prize" to O-002.
