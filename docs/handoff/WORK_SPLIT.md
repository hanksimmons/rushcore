# Work split — what remains, and which model does it

**Status:** written 2026-09-20 after W2 (D-118) and the docs/16 draft, on the user's instruction ("classify all
remaining work: what is best done with Fable 5.1 and what can be done efficiently yet effectively with Opus")
**Authority:** an operating report, below `CLAUDE.md` and `docs/handoff/README.md`. It changes no spec. Where it
names an Opus packet, that packet obeys `README.md` §4–§7 (branch off `develop-secondary`, P-numbers only, never a
generator rule, never a baseline default, harness green before every push).
**Reads:** `docs/09` (phases), `docs/12` (Summit), `docs/13 §5–§7` (tunnels, scale, horizon), `docs/16` (progression),
`docs/14`/`15`/`17`/`18` (the user's art programme, PROPOSED), `OPEN_DECISIONS.md`, `STATUS.md`.

## 1. The rule used to split

The line already drawn in `README.md` §5 is the right one, and it held over seven packets without a hash moving:

- **Fable 5.1 (main track)** takes anything that (a) moves generation — the skeleton, a stamp, a validator, a
  `WorldScale` number, route topology, and so every golden hash; (b) reads or writes velocity in `PlayerPhysics`
  or adds a read point beside a frozen D-095 value; (c) writes a new harness rule whose tolerance is a physical
  claim (a settle, a flight, a scrub); (d) needs diagnosis rather than construction (a model that disagrees with
  the ball); (e) resolves an authority conflict between documents, or writes a D-number.
- **Opus 5 (parallel track)** takes work that is fully specified in a document, self-contained inside dressing, UI,
  shaders, run bookkeeping or instruments, checkable with `Check(name, ok, detail)`, and never needs a generator
  rule or a baseline default to land. It stops and writes "Needs main track" the moment it does.
- **The user** owns every FEEL / VALIDATE call and every number marked provisional; those are listed so no agent
  spends time on them.

Where a slice straddles the line it is split at the data contract: Fable lands the contract and the rule, Opus
builds the presentation on top in a later packet. A slice is never split across two branches at the same time.

**Operating rule for two tracks on one machine.** Never run two harnesses at once (they race on the preset
directory), and never `dotnet build` while the Godot editor is open on the project (the editor reloads assemblies
and crashes). Two agents alternate their harness runs; each says in its Delivery record which build it ran against.

## 2. The inventory

Size: **S** under a session, **M** a session, **L** several sessions, **XL** a slice programme.

### 2.1 `docs/13` — tunnels, the 3× course, the horizon

| Item | Source | Track | Size | Why | Depends on |
|---|---|---|---|---|---|
| **H2 — the cloud dome and hero clouds** | 13 §4.2, §4.4, §4.5 | **Opus** | M | A hemisphere mesh, one unshaded two-noise shader, six to ten camera-facing quads, the sky affect on the fog; harness: draws on every archetype, ≤ 1.5 M triangles and ≤ 120 world draw calls at the start pad. No generator rule, no hash. | Read `docs/14 §5` (sky law, PROPOSED) for the palette so the dome is not built twice; record its colours as a P-entry until `14` is accepted. |
| **T3 — pockets** | 13 §2.3, §2.7, §2.8 | **Fable** | L | A new stamp (a bowl 120–200 m across, 25–40 m deep, the wall profile round the rim), a tunnel line that enters and leaves it, the fall-in setback, three validators (rim a wall ≥ 20 m everywhere, the exit tunnel never stalls from a standing start, the reward inside), hashes re-recorded. Route topology. | The user: dead-end pockets or always an exit tunnel (`13 §6`). |
| W2 windowed shots read | 13 §3.4, 08 §1 | either (S) | S | `RUSHCORE_SHOTS=1` on the three W2 samples and a look at the stills; not run for W2 because Godot was crashing under the builds. | The editor closed. |
| The collider (square pad) | 13 §3.3, D-115, D-118 | **Fable** after the user decides | S | Padding the map square is a few lines but changes the collider under the frozen ball (2×2 quantisation on unshelled slopes); the harness's collider budget moves with it. | The user's call. |
| The ramp lip kick (+9 m/s vertical) | D-118 open item | **Fable** | M | The ball leaves a lip with vertical speed the model lacks; the harness holds ramps to 30% until the cause is found (lip ease vs the follow's release). Diagnosis. | — |
| The exit-line descent flight on a falling swell | D-117/D-118 open item | **Fable** | S–M | 22 of 99 ticks airborne on the level run's descent; a shaping rule on the terminal line or a model term. Generation. | — |
| The ramp-settle harness rule (D-109 canyon) | memory / harness | **Fable** | S | A settle rule that passes on current geometry but is a rule, not a fix. | — |
| The acts table 1.25/0.75/1.0, V-017 tunnel numbers, footprint width | 13 §3.1, §2.4, §6 | **user** | — | On play. | W2 played. |

### 2.2 `docs/16` — progression and economy

| Item | Source | Track | Size | Why | Depends on |
|---|---|---|---|---|---|
| **P1 — pickups, the magnet, XP, levels, the stat ladder, the choice panel, the HUD** — **delivered 2026-09-20 (D-119)** | 16 §2–§4, §6, §7 | **Fable** | L | Eight read points beside frozen tuning values inside `PlayerPhysics` (cap, drive, steering both speeds, ball radius, takeoff, burst, passive regen, gravity); pickup placement reads generation data (modules, lids, tunnel runs, pads, the corridor) and a seed stream; the magnet chase rule; harness lines whose numbers are physical (148.5 × 1.18 on the runway, flight 1/0.7 as long); D-119; the 01/02 amendments. The panel and HUD land plain and working. | The user's §8 choices (chosen at the outro vs mid-run). |
| P1b — the choice panel and HUD polish, pickup VFX | 16 §3, 06 §12, later 14 §14 | **Opus** | S–M | Presentation over P1's contract (`UpgradeState`, `RunDirector` XP/level events, `PickupField` collect events). | P1 merged. |
| **P2 — the found shop** | 16 §5, §6 | **Opus** | M | Run-level selection from the run seed (never consecutive, never first), the pad 40 m outside an optional line's plateau midpoint from the line's own vertices, entry hold, a panel with the two placeholder purchases; harness: deterministic shop stages, the pad out of every keep-out, buying deducts cash. Dressing and run bookkeeping; no hash. Escape hatch: if the placement needs a line's lateral geometry the definition does not carry, "Needs main track" (a field on `OptionalLineDefinition` is Fable's, a few lines). | P1 merged; the user's item pool for the real stock. |
| The item pool, whether a shop sells a rank | 16 §8 | **user** (brainstorm) | — | Then: an item that changes a movement rule is Fable; one that only reads state is Opus. | — |

### 2.3 `docs/12` — Summit Descent (D-107, not started)

| Item | Source | Track | Size | Why | Depends on |
|---|---|---|---|---|---|
| **S1 — the mountain** | 12 §8, §9 | **Fable** | XL | A new skeleton emit path with unbounded headings, the face, benches, rails and headwalls on the wall profile, berms, the valley run, five validators, the harness wall test, `--summit`, the sample and its hash. The heaviest generation work in the backlog. | The user's order: after `docs/13` and P1, unless re-prioritised. |
| **S2 — the chute and the read** | 12 §5, §3e, §9 | **Fable** | L | Chutes as optional-line stamps with rails, the notch and sightline validator, the harness chute drive. | S1. |
| S3 — the vista (palette, rail crest lines, fog end, scatter suit) | 12 §7, §9 | **Opus** | M | Presentation over the S1 geometry; the summit cone and cirque walls are heightfield stamps and go with S1. | S1 + S2 merged. |
| Tier rhythm, rail height, the hairpin at the ceiling, the fixed camera pitch | 12 §10 | **user** | — | FEEL. | S1 + S2 played. |

### 2.4 `docs/09` Phases 4–11 (the loop)

| Item | Source | Track | Size | Why | Depends on |
|---|---|---|---|---|---|
| **Phase 4 — the impact model and its Flow integration, damage, defeat, invulnerability** | 02 §7–§9, 03 | **Fable** | L | Relative-closing impact is a velocity rule on the frozen controller; the D-095 boundary. | P1 (the run's health and rewards exist). |
| Phase 4 — enemy placement rules (never on a mandatory landing zone, never in a tunnel's covered run, keep-outs) | 04, 02 §7 | **Fable** | M | Reads the module and line data; a validator per archetype. | The impact model. |
| Phase 4 — Pylon / Bulwark / Strider / Shooter behaviours, the elite modifier, reward drops through the burst, combat VFX | 02 §7, 06 §9 | **Opus** | M–L | Movement patterns and projectile logic on placeholder bodies once the impact model says what a contact is; the burst and the VFX one-shots exist (T3). | The impact model merged. |
| Phase 5 — score accumulation, the stage objective line on the HUD | 02, 06 §12 | **Opus** | S | `RunDirector` bookkeeping and HUD. | P1. |
| Phase 6 — route cards on the exit signs (terrain / reward / danger / modifier of the stage each exit leads to) | 02 §3–§4, D-105, P-011 | **Opus** | M | The next stage's archetype is already derivable from the exit taken; the card renders it on the sign; modifiers wait for the modifier set. No generation move. | The user's modifier list (`01` ceiling 6); difficulty is parked. |
| Phase 6 — escalating difficulty, route modifiers | 01, 09 | **parked** by the user until after Phase 4 | — | — | — |
| Phase 7 — passive item framework and initial items | 02 §11 | **user** then split per item | — | The stat ladder replaced the random offers (D-119); items are the shop's stock. | The item brainstorm. |
| Phase 9 — the final encounter | 01, 09 | **Fable** | XL | A designed arena generated as a stage plus the encounter's rules on the movement verbs. | Phase 4. |
| Phase 10 — recap, score screen, meta XP, persistence, the meta tree (confirmed by the user 2026-09-20, D-120: a tree of nodes to unlock and level up when a run ends; design it with the user before building) | 01, 02, 09 | **Opus** | M | UI plus one small save file (no migration framework, per `CLAUDE.md`); meta nodes that give "modest starting advantages" apply through the read points P1 builds (`UpgradeState`), so no physics edit. | P1; a complete loop. |
| Phase 11 — render profiling, draw calls, mesh budgets | 09, 13 §4.4 | **Opus** | M | Measured with `Performance` monitors on the samples. | — |
| Phase 11 — the physics tick-rate test (120 Hz) if high-speed behaviour needs it | 09, CLAUDE.md | **Fable** | S | Only if measured; a harness comparison. | — |

### 2.5 The art programme (`docs/14`, `15`, `17`, `18` — PROPOSED, the user's; nothing accepted)

The bibles are a shader and materials programme with fifteen generator requirements attached. Once the user accepts
`14` (its §20 open decisions, and the §17 conflicts against the frozen design read first), the split is:

| Item | Source | Track | Size | Why | Depends on |
|---|---|---|---|---|---|
| **G15 first: the hash-safety assertion** (every art element draws from the cosmetic seed) | 14 §18 | **Opus** | S | The harness already pins every sample's hash; the packet adds the cosmetic-seed rule and asserts it. | Acceptance. |
| **C1 — the cel include and ground materials** (one uniform block, the ladder, plateaus, deep shade, shadow-map integration, palette quantisation, distance tiering) | 17 §2–§3, §5.4 | **Opus** | L | Shaders and materials on the existing meshes; measurement gates in `17 §11`. | Acceptance. |
| **C2 — the rim, and the wall-ride band** | 17 §5 | **Opus** with a Fable read | M | The rim is a shader term; `17 §5.3` calls the band gameplay-critical, so the wall-ride harness shots are the check and the main track reads them. | C1. |
| C3 — structures under cel, the ball's shader, VFX under cel (dust, debris, impact rings, the trail) | 17 §7–§9, 14 §10 | **Opus** | L | Materials and the existing VFX one-shots; ball colour cyan is `14 §20` #4 (the user's). | C1. |
| C4 — HUD under the art direction (type, units, the dial) | 14 §14, 06 §12 | **Opus** | M | `14 §20` #3 and #6 are the user's first. | Acceptance. |
| C5 — environment bibles: per-archetype palettes, light and fog, ground treatment | 15 | **Opus** | M each | `WorldDressing` palettes and the environment, one archetype per commit. | C1. |
| G1, G2 — sun and cloud-deck fields on the stage from the seed | 14 §18 | **Opus** | S | Derived from the cosmetic seed they move no hash; if from the stage seed they do, and that is a Fable call. | Acceptance. |
| G6, G7, G9, G13, G14 — slogan monoliths, sector markers, wall stencils, value-weighted scatter, concrete pads | 14 §18 | **Opus** | M | Dressing from the cosmetic seed inside the existing keep-outs. | C1; the user's #1 (the authority's name) and #3 (type). |
| **G3, G4 — corridor edge strips with HAZARD before every rim** | 14 §12, §18 | **Fable** | M | A new builder over the route polylines reading module data (rims, drops); the read of the route at 250 m/s is a gameplay claim. | Acceptance; `14 §20` #5. |
| **G5 — chevron runs 150 m before every fork** | 14 §9.2, §18 | **Fable** | S–M | Placed from the skeleton's forks (departures, terminal ramps, portals, chute gaps). | Acceptance. |
| **G8 — gantries as structures through the headroom validator** | 14 §17.4, §18 | **Fable** | M | A structure kind; the validator's verdict. | Acceptance; `14 §20` #9. |
| **G10, G11 — the panel modulus and chamfer on shells, lids, tunnels; no relief below a wall lip** | 14 §8, §18 | **Fable** | M | The wall shell is the collider the ball rides (D-111); any relief on it is felt; the harness asserts no new impacts. | Acceptance. |
| **G12 — asymmetric vertical mass** | 14 §11.3, §18 | **Fable** | M | An `ArchetypeRules` rule; hashes move. | Acceptance; a design read with the user. |
| The `14 §17` conflicts and `14 §20` decisions | 14 | **user**, then **Fable** for the D-numbers | — | Every accepted sentence that changes 06 or 04 needs a D-entry. | — |

### 2.6 Debts in the handoff ledger

| Item | Source | Track | Size | Why |
|---|---|---|---|---|
| **Reconciliation of P-003..P-017** to D-numbers | README §8, STATUS "Reconciled" column (all empty) | **Fable** with the user's verdicts | S | D-numbers are main-track by contract; mechanical once the verdicts exist. |
| T5 "Needs main track": `BuildLine` discards a line's segment kinds; the Sky wall-clearance check measures something other than a wall | STATUS.md | **Fable** | S | A few lines in the line builder (no geometry change) and a validator rule. |
| T6 — sample-stage presentation fixes | T6 packet | **Opus** | M | Blocked on the user's notes; the six samples are now the W2 samples (`docs/10`). |
| OPEN_DECISIONS A-1..A-6, B-3 | OPEN_DECISIONS.md | **user** | — | Play reads. |
| OPEN_DECISIONS A-8 (the reward burst cannot catch a fast ball) | OPEN_DECISIONS.md | closed by P1 | — | The chase rule `max(MagnetSpeed, ball speed + margin)` is P1's; mark it when P1 merges. |
| OPEN_DECISIONS B-1, B-2, C-1 (tube items) | OPEN_DECISIONS.md | closed by D-112 | — | Mark them. |

## 3. The two queues

**Fable, in order (heaviest first, the user's instruction):**

1. **P1** progression core (`docs/16 §7`) — the user's explicit ask; it also unblocks P1b, P2, Phase 5 score and Phase 10.
2. **T3** pockets — closes `docs/13`.
3. **Summit S1**, then **S2** — the heaviest generation work; the user plays S1 + S2.
4. **Phase 4** the impact model, then enemy placement rules — opens the Opus enemy packet.
5. The open findings as they fit between slices: the lip kick, the exit-descent flight, the ramp-settle rule, the T5 items, the reconciliation, the collider once decided.
6. The art programme's [GEN] items (G3–G5, G8, G10–G12) once `14` is accepted; then Phase 9.

**Opus, in order (each unblocked as its dependency merges):**

1. **H2** the cloud dome — unblocked now.
2. W2 shots read — unblocked now, quick.
3. **P2** the found shop, then **P1b** the panel and HUD polish — after P1 merges.
4. Phase 5 score accumulation; Phase 10 recap, persistence, meta tree — after P1.
5. Phase 6 route cards on the exit signs — after the user's modifier list, or now with terrain only.
6. **S3** the Summit vista — after S1 + S2.
7. Phase 4 enemy behaviours and the elite modifier — after the impact model.
8. The cel-shading packets C1 → C2 → C3 → C4 → C5 and G1/G2/G6/G7/G9/G13/G14 — after the user accepts `14`/`17`.
9. T6 — when the user's notes exist.

**Kickoff.** Paste `PROMPTS.md`'s common preamble, then: "You are assigned `<item>` from `docs/handoff/WORK_SPLIT.md`
§2; write its packet as `docs/handoff/T<n>_<SLUG>.md` in the shape of `T4_PROP_SCATTER.md` before coding, fill its
Delivery record, and add its row to `STATUS.md`." The next free packet number is T9.

## 4. What this split does not decide

The order of Fable's queue after P1 (pockets before the Summit, or the Summit first) is the user's; the queue above
follows `docs/13 §5` and the date order of the user's asks. Nothing in §2 marked **user** should be attempted by
either agent.
