# 04 — Procedural Stage Generation Specification

**Status:** Draft 0.3 — final audited  
**Authority:** Canonical terrain/stage generation pipeline, determinism, archetypes, route structure, challenge placement, validation

## 1. Core rule

> **Generate traversable gameplay structure first; procedural terrain/noise decorates and reshapes that structure second.**

Noise is variation, not level design.

## 2. Stage experience contract

A normal RUSHCORE stage is:

- a large bounded landscape,
- generally oriented from entry toward exit,
- broad enough for free line choice and high speed,
- not a narrow racetrack,
- not a directionless open sandbox.

Each stage contains:

- one guaranteed primary traversable region/route,
- approximately 1–3 meaningful optional lines/shortcuts,
- many small local line choices,
- hazards/enemies/rewards integrated into those lines,
- safe recovery checkpoints.

The primary route must not require the player to arrive with boost available.

## 3. Inputs/outputs

### `StageGenerationRequest`

Conceptually:

- run seed,
- stage index,
- route seed,
- terrain archetype,
- danger tier,
- route modifier(s),
- reward category,
- difficulty scalar.

### `StageDefinition`

Pure data describing:

- heightfield samples,
- render source data,
- primary route samples,
- optional route/shortcut samples,
- start transform,
- exit transform,
- recovery checkpoints,
- challenge placements,
- enemy placements,
- pickup/reward placements,
- prop-scatter instructions,
- environment/palette,
- validation report.

Generate definition data independently of the active SceneTree where practical.

## 4. Determinism

Given the same request, gameplay-relevant generation is deterministic.

Rules:

- explicit seed chain,
- no unseeded/global random calls in generation,
- derive subsystem seeds predictably,
- separate cosmetic RNG from gameplay RNG,
- record run/stage seed,
- avoid dependence on unordered collection iteration.

## 5. Generation pipeline

### A — Macro route skeleton

Generate:

- start,
- exit,
- primary route,
- 1–3 optional line opportunities as allowed by archetype/stage,
- reserved challenge zones,
- landmark zones.

Primary route uses width, curvature, slope, and clearance constraints.

The skeleton is ordered by route distance, not by stage X (D-096 amends D-084): headings are unbounded, so
a route may switch back, spiral or turn through more than 360°, and the only plan constraint is the
footprint. Spatial lookups bucket vertices by cell; progress, checkpoints and features already read route
distance.

Feature straights (D-097, D-099): a straight at the archetype's feature spacing hosts, by the archetype's
mix, a launch crest, a mandatory gap, a launch ramp or (on a dune sea) a train of crests on the stage's wave,
and is sized for the feature's body plus the ceiling's flight off it and the landing run. Two rules keep the
chain intact (§12): a feature that no longer fits once the band or the closing point has clipped its straight
leaves a plain straight (never a featureless run of the feature's length), and a bend the heading limit would
clip below 10° turns the other way instead of being skipped (two straights with no bend between them are one
chain gap).

### B — Archetype base heightfield

Apply structured analytic functions that establish strategic identity:

- broad waves,
- valleys,
- ridges,
- bowls,
- canyon masks,
- dune fields.

### C — Low-frequency procedural variation

Use seeded noise/domain warping sparingly.

Noise amplitude/frequency is bounded so it cannot erase route readability. The concrete bound (D-082):
micro relief must keep every crest radius at or above the cap's contact radius (v²/g = 560 m), so a
component of wavelength λ has height H ≤ λ² / (2π² · 560). Below 100 m wavelength that is under a metre;
such noise is decoration only.

### D — Guaranteed corridor stamping

Modify terrain around the primary route to enforce:

- minimum width,
- acceptable grade,
- controlled cross-slope,
- readable transitions,
- valid start/exit,
- valid landing/recovery areas.

The route is not required to be flat. It may descend, climb, bank, crest, and roll.

The stamped profile is continuous along the route: the corridor height at any point interpolates
the profile between the two route samples it lies between (D-093). A nearest-sample height is a
staircase at the sample spacing, invisible to validators that read the samples but resolved by a
heightfield grid of the same spacing into flat treads and double-grade risers.

### E — Challenge modules

Small authored procedural grammar pieces with explicit preconditions.

Each module defines:

- entrance assumptions,
- geometry stamp,
- expected speed/skill context (read from the route speed model, §12, never guessed),
- required vs optional status,
- landing/recovery zone,
- reward opportunity,
- validator.

Initial modules:

1. ModerateGap — delivered 2026-09-06 (D-097).
2. LargeShortcutGap.
3. LaunchRamp — delivered 2026-09-06 (D-097).
4. BankedTurn — delivered 2026-09-06 (D-097).
5. RidgeShortcut.
6. BoostLine.

**Delivered grammar (D-097).** A module is a reserved feature straight on the skeleton (§5A) plus a stamp
on the corridor profile plus a validator that fills the seven fields from the two speed profiles
(`ChallengeModule` in the stage definition; `challenge modules pass their validators` in every report).
The **two-price rule** governs each one: the *free path* (drive into the pit and ride its exit wall out,
fly off the lip uncharged, take the bend at its corner limit) never stops and never drops the model below
2/3 of the base cap; the *paid path* (a half-charged jump across the gap, a full charge off the lip, a
carve through the bend) grants Flow and must land on the reserved straight. Mandatory gaps are 40–120 m
wide and 12–20 m deep behind a 150 m runway; the exit wall is the gentlest the opening allows (never over
the family's 25°), and the depth is trimmed to keep it so, because a ball riding out of the pit leaves
the far rim like a ramp. Launch ramps are 11° or 19° lips of 20 m after a 100 m approach with a 45° back
face. Banked turns are the committed 50 m bends with their family bank and at least 100 m of straight
before them. Modules are stamped on the corridor profile as it slopes (a level module would need eases
whose convexity launches a ball before the rim); the validator reads the real rim heights, so a gap on a
0.18 grade demands the jump its far rim actually needs. The straight after a gap or a ramp is sized for
the base kit's full-charge flight at the cap over falling ground (about 850 m) and for the ceiling
ball's slam landing off the module's own launch; that launch (the far rim, the lip taken uncharged) is
the module's declared exception to the bend-clearance rule at the ceiling (§10): the base kit's flight
must land on the straight, the ceiling's must land there with a slam pressed within 0.5 s, and the
no-slam flight is reported, not enforced. Optional ridge lines ride the primary's pre-module profile, so
a ridge beside a gap carries no copy of it. Measured 2026-09-06: 100 seeds, 0 fallbacks, 54 gaps, 16
ramps and 134 banked turns all passing; the harness follower drives a ramp stage within 0.7% of the
model and its uncharged lip flight lands within 9% of the model's.

Vertical grammar modules (D-096, §5I), each with the same seven fields:

7. WallTunnel — a slot through a wall closed by a lid; the camera confines (06 §11); the roof refuses a charged jump, so the module declares its ceiling (§10 headroom).
8. Tube — a see-through swept cylinder with flared mouths; entry from a ramp lip, an edge or midair, exit onto a landing zone of any line or floor; a branch point of the line graph.
9. SpiralPit / SpiralRamp — a conical helix of banked bends, each turn at a different radius, descending into a pit or climbing a mesa; falling off the inner edge lands on the turn below.
10. TerraceStep — a floor step of 60–90 m reached by a ramp lip and a full charge (at most 20 m when mandatory), with a landing zone on the upper floor and a drain below its edge; larger lifts are spiral ramps and tubes.
11. Bridge — a lid used as a floor across a valley or a gap; falling off it lands on the drain.

### F — Gameplay object placement

Place enemies/pickups as movement patterns:

- crush chains,
- slaloms,
- cross-path hazards,
- high-value arcs,
- approach-to-ramp lines,
- shortcut reward lines.

Avoid world-uniform random scattering.

### G — Cosmetic scatter

Props:

- never block mandatory corridor,
- obey density/performance budgets,
- reinforce scale/speed,
- do not accidentally become gameplay collision.

### H — Validate

Run lightweight deterministic checks.

Failure → bounded regeneration.

Repeated failure → known-safe fallback configuration rather than broken stage load.

### I — Vertical grammar (D-096)

Three kinds of thing, and the ball drives on all of them:

- **Ground**: the one single-valued heightfield (§9). Walls (a step of any height across one or two cells),
  terraces (floor steps), slots (walls both sides of a corridor), cliff edges and spiral pits are all stamps
  in it, because a heightfield is happy with a near-vertical face. Nothing here needs a second representation.
- **Structures**: a separate generated mesh plus collider, as §9 allows for bridges and overhangs. Exactly
  two: the **lid** (a box roof over a slot: a wall tunnel, or a bridge when used as a floor) and the
  **tube** (a circle of radius 4–8 m swept along a 3D path of straights and arcs of any pitch, rendered as a
  see-through shell with opaque ribs, inward-facing triangles for the collider). Structures are never a
  second height layer: the ground follow, the route speed model's touchdown and the validators keep reading
  the one heightfield, and the contact baseline carries the ball on a structure (03 §3).
- **Floors**: terraces, not stacked layers. A jump step is 60–90 m (the full-charge apex is 91 m above
  the lip at any speed, because the takeoff sets the vertical and a ramp does not add to it, `docs/11
  §7`); larger lifts are spiral ramps and tubes, which a driven ball climbs at up to 35° without losing
  speed; three floors put the top 120–270 m up. The guaranteed primary route stays on the lowest floor (the free path of the two-price
  rule), upper floors are paid lines, and under every edge of an upper floor the ground **drains**: every
  cell below the edge has a descending drivable path back to the primary. The top floor may sit in the cloud
  band (06 §3). Floors that share an XZ (a true helix inside a tower, a room under a room) are not built; if
  the design ever needs them they are the same height-source abstraction instantiated per floor with a NaN
  mask and a floor-aware follow, tracker and touchdown, and that is a separate decision.

Tubes carry the line graph's branches. A tube leaves through a mouth on a ground line, at an edge or in
midair (reachable from the take-off runway at the arrival speed with at most half a charge on a mandatory
tube, a full charge or the ceiling on an optional one, the mouth radius as the aim tolerance) and exits
through a flared mouth onto a 200 m landing zone of any line or floor; two tubes from one platform, or a
tube exit onto a floor with its own lines, are the branches. Every line still rejoins the primary or
reaches the exit and the graph is acyclic in route distance. Inside a tube the walls carry the ball (it
rides up to tan φ = v²/(g·R)), so a tube path may bend tighter than the bend ladder: the route speed model
treats tube segments as **carried** (no bend loss; grade, drag and cap only) and the exit velocity runs
along the axis. A tube never passes through terrain mass and keeps its axis clear of the ground and of
walls by the camera clearance (§10), so the camera stays outside it; a tube through a wall is a wall tunnel
(lid) instead. Forks inside a tube are not built.

## 6. Archetypes

### Rolling Highlands

**Primary skill:** terrain reading and momentum conversion.

Geometry:

- broad hills/valleys,
- long ridges,
- crest launches,
- valley safety vs ridge shortcuts,
- long sweeping lines.

### Canyon Run

**Primary skill:** high-speed steering and banked lines.

Geometry:

- winding low channel,
- broad banked bends,
- higher side terrain,
- occasional upper ledge/shortcut,
- wide enough to preserve sense of scale,
- slot walls beside the channel, on the outside of bends only until difficulty is reassessed (blind corners, §10),
- wall tunnels (lids) through spurs,
- a spiral pit or spiral ramp as the set-piece (D-096).

**Delivered 2026-09-07 (D-098):** the corridor is cut as a channel through side terrain raised 60–120 m
above the floor relief (the channel floor follows the swells, the walls are the difference), with slot
walls that blend over three cells (about 80° at 100 m) on straights and bend outsides and over the family's
120 m on the inside of every bend (blind corners, §10). Every wall stands 8 m outside the corridor's level
width (the ground follow's lateral samples never read it, `WorldScale.WallSetback`), the bend mix leans on
the cruise and fast radii with a 1.5× berm, straights are 200–450 m, and the palette is red rock with a
pale rim. Ridge lines become ledges cut into the wall 25–40 m above the floor. An `ArchetypeRules` record
holds what an archetype changes (geometry only, §7); `World › Canyon Run (Phase 3)`, `-- --canyon` and
`RUSHCORE_ARCHETYPE=canyon` select it. Wall tunnels (lids) and the spiral pit follow in their own slices.

### Dune Sea

**Primary skill:** jump rhythm and landing alignment.

Geometry:

- broad repeating waves,
- crest-to-crest timing,
- opportunities to land on downslopes,
- occasional large set-piece dune,
- wide landing zones.

**Delivered 2026-09-07 (D-099):** the stage carries one seeded dune wave, a directional cosine train of
wavelength 350–500 m whose crests stand 0.06–0.085 × λ (21–43 m) above the swells, its crest lines within
10° of across the stage axis; the swell slope budget drops to 0.08 so the dunes are the relief. The skeleton
builder draws the wave first (gameplay structure before noise) and makes most feature straights **dune
trains**: two to four launch-crest features a wavelength apart, each placed on one of the wave's own crests,
with the straight sized for the ceiling's flight off the last crest (plus a 15% margin) and the landing run.
Inside a train the corridor profile adds the same crests, so the corridor rides the dunes exactly; every
other section (bends, plain straights, modules) leaves the wave out and runs at the swale level between the
dunes, because a wave under a bend would launch the ceiling ball inside it. Trains only take straights
within 35° of the wave's travel direction (the corridor is level across; a seam of ≤ 4 m at the corridor
edge is the residue), the route keeps within 12° of the axis so a 2 km train fits the route band, and a
clipped train keeps as many crests as still fit. At the base cap every crest launches (v²/g·r ≈ 1.5): the
flight lands on the same dune's downslope near the trough at 55–60 m/s down (a slam keeps Flow, anything
else pays it, 03 §9), the ball climbs the next dune and launches again, which is the rhythm; a charged jump
clears the next crest, which is the alignment. At the ceiling the ball leaves near each trough and flies
over the next dune. The set-piece dune and the wide landing zones are not yet distinct: the whole dune field
around a train is the corridor's own shape (no cut or fill), and the set piece waits on the aligned-relief
bump. Palette: sand from the troughs to pale crests, lee faces darker. Selected by `World › Dune Sea
(Phase 3)`, `-- --dunes` and `RUSHCORE_ARCHETYPE=dunes`.

### Sky Terraces (D-096)

**Primary skill:** vertical commitment and fall management.

Geometry:

- three floors of terraces, the primary on the lowest, the top 120–270 m up,
- 60–90 m jump steps, spiral ramps and tube lifts and midair tube mouths up; cliff edges and drains down,
- the top floor in the cloud band,
- tubes and bridges linking floors and branching lines,
- every fall lands on ground that drains back to the primary.

Archetype rules (D-098, extended D-099): an `ArchetypeRules` record per archetype carries the wall height,
the wall and inside falloffs, the bend mix, the straight lengths, the bank scale, the swell slope budget,
the feature spacing / chance / mix, the dune-train crest count and the heading limit; the skeleton builder,
the height field and the validators read it and nothing else differs between archetypes.

## 7. Archetype physics rule

Terrain archetypes alter strategy **only through geometry/presentation**.

They do not secretly change:

- gravity,
- steering strength,
- jump values,
- boost rules,
- player friction.

Explicit stage modifiers may alter rules later only when communicated as modifiers.

## 8. Physical scale

**VALIDATE in the Movement Toy/calibration environment.**

Do not freeze kilometer dimensions, terrain cell size, hill wavelengths, ball diameter, or expected speed from paper design alone.

The generator must ultimately support:

- long sustained high-speed lines,
- enough sight distance to make decisions,
- terrain features large enough to feel monumental relative to the ball,
- stable collision at the accepted hard playable speed cap.

Calibration should explicitly compare known distances/features and speeds before fixing stage dimensions.
The frozen movement baseline (03 §15, D-078) is the input: cap 148.5 m/s, full-charge jump
2.03→58.21 m/s at g 39.38, and the landing burst (D-077), which can establish 80% of the cap from
any slam landing. The calibration environment must be large enough to measure these at the cap;
the Movement Toy lab (±512 m) is crossed in about seven seconds at that speed, so the 6.4 km scale strip (`World › Calibration Strip (M1)`, D-079, runbook) is the M1 instrument.

Scale is decided on two sides at once (D-080): feel, and the **terrain budget** at that scale
(samples, triangles, build time, memory, draw distance). Cell size is a measured choice: it was
also a ground-contact-stability choice (measured 2026-09-05: 4 m kept 99% contact on the gentlest
strip hills, 8 m only 86%). Since the analytic ground follow (03 §3, D-092) the controller reads the
terrain grid and keeps contact wherever contact is physically possible, so contact no longer
depends on the cell size (measured 2026-09-06: 100% raw contact on the same hill at 4, 8 and 16 m)
and cell size is decided on draw cost, silhouette and validator resolution alone. Render terrain
is tiled from the start; the budget model and measurements live in the runbook.

Crest contact is a hard geometric input: a ball leaves the ground at any crest whose radius is
below v²/g (91 m at 60 m/s, 358 m at the burst speed, 560 m at the cap). For a cosine hill the
crest radius is λ²/(2π²H). Archetypes must place crests knowingly: below that radius a crest is
a launch, above it a roll.

**Accepted family (Gate M1, D-082):** `docs/11_WORLD_SCALE_PROPOSAL_M1.md` §3 holds the frozen
generation inputs (bend ladder, hill roles, gap / ramp / corridor sizes, footprint, cell size), with
the feel verdicts re-judged on the first generated stage. Code reads them from one `WorldScale` class.

## 9. Heightfield/render representation

Preferred MVP:

- one logical height source,
- generated `ArrayMesh` for render geometry,
- `HeightMapShape3D` for main static terrain collision,
- spherical player collider.

Render and collision derive from the same source data.

True chasms/holes are compatible with the heightfield approach:

- omit the corresponding render triangles (or otherwise create the visible opening),
- use `NaN` height vertices in `HeightMapShape3D` where a collision hole is required,
- keep hole edges comfortably larger than the player/collision sampling scale,
- validate mandatory landing/approach geometry around every required gap.

Verified 2026-09-05: the Godot 4.7 `HeightMapShape3D` class reference states "Holes can be punched through the collision by assigning NAN to the height of the desired vertices (this is supported in both GodotPhysics3D and Jolt Physics)", and the harness drops the ball through a NaN block and rests it on the neighbouring cells (D-079).

Special non-heightfield structures such as bridges/ramps/overhangs can use separate generated meshes/colliders.

Delivered forms (D-096): the lid is a `BoxShape3D` and a box mesh; the tube is a swept ring mesh (8–12
sides) whose triangles face inward, with a `ConcavePolygonShape3D` built from the same triangles and
backface collision on so the ball never leaves through a face at the cap. Both sit on a structure physics
layer; the tube's collider is additionally invisible to the camera's occlusion probe (06 §11). The harness
verifies a tube carries the ball at the cap with CCD the way D-079 verified NaN holes.

Do not default the entire terrain to one giant concave triangle collider.

## 10. Route constraints

Primary route segments expose tunable constraints:

- minimum clear width,
- max longitudinal grade,
- max abrupt grade delta,
- curvature appropriate to expected speed,
- minimum landing area after mandatory jumps,
- no unmarked hard obstacle,
- no mandatory traversal dependent on boost availability,
- no bend inside a launch's flight: after any crest or ramp the route speed model classifies as a
  launch, the route stays straight (radius ≥ the cruise bend) for the landing distance at the arrival
  speed, because an airborne ball cannot brake to a corner limit. Since D-094 this is checked from the
  model's own flights at both speeds: no flight launches in or across a bend (beyond a 10 m drift),
  and a bend starting inside the 100 m landing run must hold the landing speed after what the brake
  sheds over that run; feature straights are sized for the ceiling flight over falling ground,
- **headroom** (D-096): nothing within the declared jump apex above any corridor (the full-charge apex from
  the local arrival speed and grade by default) unless the module declares a ceiling (lid, tube),
- **wall clearance**: a wall face stands at least two cells outside the corridor's level width, so the ground
  follow's lateral samples never read it (delivered D-098 as "the primary's stamp weight is still 1 at the
  level width plus the 8 m setback on both sides", the inside of a bend tighter than that reach excepted),
- **drains**: below every edge of an upper floor the ground descends drivably to the primary: no wall foot,
  no NaN, grade within the route limit,
- **tube clearance**: a tube axis stays at least the camera clearance (`docs/11 §7`) above the ground and
  clear of walls except at its mouths,
- **blind corners**: a wall on the inside of a bend hides the read horizon; until difficulty is reassessed
  after Phase 4, walls sit on the outside of bends.

Challenge modules may intentionally exceed ordinary safe constraints when their validator understands the exception.
Delivered exceptions (D-097): a module's own faces (a gap's rim and exit wall, a ramp's back face) are
outside the grade and grade-delta checks; a module's own launch at the ceiling is judged by its slam
landing (§5E) instead of the bend-clearance rule.

## 11. Mandatory jump envelope

Mandatory gaps/jumps must be completable by a reasonably executed base movement kit without requiring a stocked boost meter or a rare build.

Charge jump may be part of the player's base capability envelope.

Optional shortcuts/rewards may demand:

- stronger charge timing,
- boost,
- higher entry speed,
- more precise landing control.

Do not require landing-burst timing for mandatory progression. The burst is nevertheless a
first-class speed source for optional lines: module preconditions that assume an entry speed, and
stage clear-time targets, must account for a player who can reach 80% of the cap from any slam
landing (D-077).

A mandatory tube mouth or terrace step is a mandatory jump and obeys this section. Paid floors and midair
mouths may demand the full charge or the ceiling (D-096).

## 12. Validation

### Mandatory

- start/exit exist,
- primary route continuous,
- corridor width above minimum,
- slope constraints pass,
- mandatory landing zones exist,
- mandatory jumps fit base capability envelope (delivered D-097: every mandatory gap is crossable half-charged at the model's arrival speed, with the far rim's real height, plus the ball's diameter and 10 m),
- spawn/exit/checkpoints have clearance,
- no required route crosses unrecoverable invalid terrain,
- vertical grammar (D-096): headroom, wall clearance, drains and tube clearance hold; every tube mouth is reachable per §5I and every exit has its landing zone; the line graph is acyclic in route distance and every line rejoins the primary or reaches the exit.

### Secondary

- travel length in target calibrated range,
- archetype-specific feature counts,
- shortcuts are meaningfully distinct,
- enemy density in range,
- cosmetic props do not compromise corridor/readability,
- an upper floor is reachable by the base kit and worth its climb (reward placement joins in Phase 5).

### Route speed model (D-081)

Every guarantee above is a speed-at-a-point question, so validation reads one shared,
deterministic **route speed model**: a 1D integration of the frozen movement baseline (03 §15)
along the primary-route polyline, metre by metre, assuming the base kit only:

- drive held, no boost, no landing burst,
- gravity times the local grade, the solver friction as a constant slip loss, drag, the hard cap,
- a conservative speed loss on bends from the steering envelope (turn radius = v²/(a·mult)),
- a stage entry speed of zero unless the stage definition says otherwise.

Its outputs feed: mandatory-gap crossability (jump range at the arrival speed), module
"expected speed" preconditions, crest placement (a crest is a launch below r = v²/g), the
secondary travel-time check (V-009), and checkpoint headings. Boost and the landing burst are
then optional-line multipliers on top of a conservative base, which is the intent of §2 and §11.

The model is calibrated against the real controller: the harness predicts the 0→cap
curve on the scale-strip runway and the descent speeds on the lab grade fan and asserts the
model is within 5% of what the ball does (delivered 2026-09-05: runway within 0.8%; real 0→cap
is 7.1 s / ≈ 580 m). It is pure data code with no scene dependency
(`src/Generation/RouteSpeedModel.cs`). Its first whole-route reading (Gate G0, D-087): the harness follower
drives a generated stage end to end within 4.3% of the model's time (8.5% at the D-091 baseline before
D-093, 0.5% after it). The model ran slightly fast because
it has no airborne phase: contact per kilometre was 90 / 94 / 58 / 87 / 76 / 22%, the low readings on the
two launch-crest kilometres, and after the second crest (taken at the cap) the ball skipped for most of
the remaining kilometre and arrived at 119 m/s against the model's 149. With the ground follow (D-092) and
the interpolated corridor profile (D-093) the same drive reads 0.5% (47.2 s vs 47.4 s) with 100 / 100 / 67 /
100 / 100 / 75% raw contact: every loss of contact is now a real launch (the harness confirms each crest is
left exactly when v² > g·r) and the ball lands at the cap, so what remains of the model error is the
airborne-and-landing phase at the crests. Landing-zone and expected-speed assumptions for the Phase 3
modules (§5E, §11) must still read the measured landing, not the model's 193 m estimate.

**Two speeds (D-088).** From the Flow headroom slice on, generation reads two speeds. The base-kit
profile above (entry speed 0, base cap, no Flow) decides mandatory crossability and the base-kit
time. The **ceiling**, base cap × (1 + headroom), decides safety: crest contact radii, corridor width
and bank on bends, sightline and fog, landing runs. A stage must be safe for a player holding a full
chain and completable by one who never gains Flow. Two secondary checks follow: a **chainable line
exists** (consecutive Flow opportunities on some line are never further apart than the chain window
at the ceiling speed), and every report carries **seconds below the base cap** on the primary, the
one figure the Phase 4 difficulty reassessment reads.

Delivered 2026-09-06 (D-094): the model integrates a **ceiling profile** (entry at the ceiling, cap
base × (1 + headroom), steering saturated at the base cap) beside the base-kit profile, and both carry
an **airborne-and-landing phase**: the ball leaves where the polyline's curvature demand exceeds
gravity over a 3-cell window (the controller's ground-follow rule, 03 §3), flies under air control and
drag, and lands where its path meets the polyline again, keeping the tangent component; each flight
records launch, landing, hang time, landing vertical speed and the speed kept. Validators: no bend
inside any flight or its 100 m landing run at either speed; a chainable line on the primary (launch
crests and bends of ≥ 30° as the opportunities, never further apart than 6 s × the ceiling speed);
notes for seconds below the base cap and the ceiling profile (flights, and how many land at or above
the 30 m/s plain-landing loss, which only a slam avoids). Launch-crest landing runs now read the
model's own flight when it is longer than the height-fall estimate. Reference numbers at D-091 are in
`docs/11 §6`; `WorldScale` carries them as constants and the generator computes the live values.

Do not initially build an AI agent that plays every stage. Add simulation validation only if real failures prove the numeric checks plus the route speed model insufficient.

## 13. Recovery checkpoints

Invisible anchors along the primary progression:

- stable terrain,
- enough sphere clearance,
- sensible continuation heading,
- update only after legitimate player progress,
- not immediately before unavoidable danger,
- on a vertical stage the nearest-vertex match includes height, so a turn below or a floor above never aliases (D-096),
- a module owns its whole straight and pushes the anchor past it; a launch crest owns only its own span, so the approach before it and the troughs of a dune train keep theirs (a restore at rest between two crests is safe, D-099); one anchor per pushed spacing, never a stack on the same vertex,
- anchors never regress: after a fall the highest reached anchor stays armed; a stuck recovery restores there and a plain fall restores nothing.

## 14. Threading

Default implementation remains KISS:

- main thread until measured generation latency is a problem,
- if workers become necessary, generate pure arrays/data off-thread,
- active SceneTree/engine-object composition stays in safe main-thread handoff.

No generalized streaming/job framework before profiling proves need.

## 15. Performance strategy

- generate full runtime content only for selected route,
- cache reusable primitive meshes/materials,
- use MultiMesh for repeated cosmetic props when counts justify it,
- partition repeated geometry spatially when culling matters,
- avoid physics bodies for purely decorative scenery,
- use particles for transient effects.

## 16. Debug requirements

Expose:

- run/stage seed,
- regenerate same/new seed,
- route lines,
- corridor bounds,
- challenge bounds,
- checkpoints,
- spawn anchors,
- slope/invalid-region visualization,
- structure bounds (lids, tubes, tube paths and mouths),
- floor ids and drains,
- validation report,
- generation phase timing.

## Empirical validation items

1. ball/world scale,
2. terrain grid spacing/resolution,
3. stage physical dimensions,
4. hill/valley/gap scale,
5. exact stage clear-time target,
6. render chunking threshold if profiling requires it.

Route representation details (polyline/curve helper, internal data structures) are implementation decisions as long as this behavioral contract is preserved.
