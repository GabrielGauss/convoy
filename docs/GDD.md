# CONVOY — Game Design Document (living)

**Working title:** MARAUDERS: *subtitle TBD* (D-034, O-004). Code and repo keep the name CONVOY.

Owner: Narrative & Game Designer · Creative director: Gabriel. Source vision: project brief "01 — Game Design & Vision".

## One-liner
A third-person **Convoy Run**: crews of 2–3 drive, fight, and patch up a single junkyard war-machine across a living wasteland, racing rival crews to deliver their cargo at the Monolith (D-032).

## Pillars
1. The crew wins, not the individual.
2. Every advantage has a trade-off (armor = weight, power = heat, ammo = space).
3. Terrain is an active opponent.
4. Combat creates racing opportunities rather than replacing racing.
5. Every crew station must make meaningful decisions.

## Format (D-032, map shape D-031)
- **The run:** crews start at the edges of one open terrain slice, cross it by one of several routes, and reconverge at the **Monolith**. Win = deliver your cargo through the uplink (UP-01).
- **Cargo:** the prize and the risk. It shares vehicle capacity with ammo and armour; it can be lost in crashes, stolen, or recovered. Losing cargo hurts, it never ends your run.
- **Routes:** fast & exposed · resources & slow · high risk & high reward. No route is best at everything; forks are telegraphed and the Engineer calls them.
- **Robbery:** channelled, visible, interruptible, capped; resources only.
- **World:** optional bosses as dilemmas; one natural event done well (dust storm); per-run variation (D-031).
- **Modes:** crews vs crews (online, after the M2 netcode spike) and crews vs AI (offline/co-op).
- Open: crews per online match and match length (O-002).

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
`Paramo_01`: 2 km × 2 km open terrain, Monolith at the centre, three player routes (canyon + chasm jump, toxic river + cave, desert + minefield + Rammer gate), three AI rival convoys from the other edges, uplink finish (D-031). Prototype runs 3–5 min (D-008); shipping target 8–15 min, set by playtest. Cargo, scrap and robbery arrive in M2 (D-032).

## Visual identity (D-033)
**Ink-and-print caricature in a junkyard world.** Comic-book ink lines and halftone on aged paper, applied to absurd, proud war-machines built from 1930s–1970s scrap. M1 stays greybox; the look is proven in the ART-01 look spike after PT-01 passes.

**Reference:** Lorenzo Etherington's *Stranski* — for the treatment only. No copied designs, emblems, characters or names.

### Principles
1. **The vehicle IS the lore.** Each car is a gang, a culture and a joke told with a straight face. Its silhouette, decals and face say who drives it before any text does.
2. **Serious universe, absurd contrast.** Nobody in the world thinks it is funny; the humour comes from pride, contrast and over-engineering.
3. **Involuntary faces.** Grille, intakes and headlights line up into an expression (a growling jaw, grinning skull-eyes). This doubles as gameplay readability at 50 m+.
4. **Exaggerated mass.** Bonnet ~40% oversized vs. the cab, small cab set back, oversized wheels in bent fenders or fully exposed suspension.
5. **Exposed mechanics.** Engines, pipes, rivets and clamps on show; every part looks bolted on by someone.
6. **Gang identity.** Hand-painted numbers, crowned skulls, worn racing stripes, sponsor plates — kept in a decal layer of each vehicle's UV atlas.
7. **Silhouette first.** Vehicles read by their massive shape before their details (menus use a 3/4 view).

### Era & world
A world that recycles everything: vehicles from the 1930s to the 1970s mixed freely, analog tech, industrial machinery, sponsor culture, improvised engineering, hostile wasteland.

### Rendering (URP target)
| Layer | Technique | Watch-out |
|---|---|---|
| Base | Flat colour + decals | Keep textures simple; colour carries identity |
| Light | 2-tone toon ramp | Readable in both day and dust-storm fog |
| Shadow | Halftone / cross-hatch density driven by light | Patterns must not swim at speed; test in motion |
| Outline | Inverted hull, ink colour `#39302F`, slight width noise | Noise must not flicker frame to frame |
| Post | Paper grain | Subtle; never hides hazards |
| Impact | 2–4 frame impact flash + 3D comic onomatopoeia on crits | Per viewport (split-screen), can be turned off (photosensitivity) |

**Starting palette:** vintage pink `#C77E9A` · industrial blue `#7788AD` · signal orange `#E88B48` (accents, hazards) · worn metal `#D8C9AA` · ink `#39302F`.

### Asset pipeline
Human concept art for 2–3 vehicles → model → shader. Commissioned art needs explicit approval (cost). AI-generated final assets need a licence/provenance check (D-011). Blender-via-MCP models are placeholders (D-013).

### Open
- First concept-art brief: JACKAL and MULE faces, gangs and sponsors (ND + Gabriel).
- Factions and their visual codes (ties into Narrative).

## Risks (ranked)
1. **Crew workload** — non-pilot seats idle or overwhelmed. Test: station telemetry + "when were you bored?". Pivot signal: any seat idle > 40%.
2. **Handling** — arcade drift vs. instability on rough terrain; trade-offs must be felt. Test: PT-01 gate.
3. **Camera/nausea** — passengers in a drifting vehicle. Test: ask every playtest; roll-stabilized Gunner cam.
4. **Multiplayer** — shared physics vehicle + moving-platform aiming under latency; several crews + AI per session raise server cost. Mitigation: D-007 rules now, spike after M1.
5. **Scope** — Convoy Run + cargo + robbery + bosses is large for ~45–50 h/week. M1 proves one loop only.

## Narrative
TBD (ND). Placeholder: world bible, factions/sponsors (each faction = a vehicle culture, D-033), why crews race, what the cargo is and why the Monolith takes it (D-032, O-002).
