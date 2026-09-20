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

- one guaranteed primary traversable region/route, ending at exit A,
- up to two terminal lines ending at exits B and C (§5J, D-105),
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

**The wall profile (D-109).** On a walled archetype the ground beyond a corridor's edge is not a blend but a
shape (`WallProfile`): the height above the corridor as a function of the lateral distance beyond the level width
and the setback: a **30 m circular fillet** tangent to the ground it leaves, a **72° face**, and a lip rounded into
the side terrain over 8 m of height. Along the route the profile is constant, so the heightfield's facets carry the
face exactly (a plane through grid samples is that plane) and the fillet's chords sit a sagitta off the arc; the
controller reads the same profile analytically (03 §3, `StageHeightField.WallSurface`), so a wall ride never
depends on the facets. On the outside of a bend the berm's slope carries straight into the fillet from the berm's
top, with no setback and no lip (a berm that flattened before the wall launched a cap ball). On the inside of a bend
the face eases to **30°**, which is the blind-corner rule's reach (§10) as one continuous surface rather than two
blends meeting at the bend's first vertex; the ease, like the bend's extra width and its berm, runs over 200 m of
route before and after the bend (`WorldScale.WallInsideFaceFade`), because a wall that recedes faster than a ride can
follow drops the rider off the end of the face. A ledge's corridor wins over the primary's wall zone where its ramp
crosses it. A wall curving away in plan, the inside of any bend, launches a rider: that is real physics and the
ride's exit to the air. The relief noise never enters the face: it
returns only above the lip. Before D-109 the wall was a 12 m smoothstep whose foot had a curvature radius of
0.4 m, a kink the facets turned into a hit and a shelf at every bend entry; the choppiness the user saw on the
canyon walls was that aliasing, not the flat shading.

**The wall shell (D-111).** The profile fixed the surface but not its collider: a 4 m grid samples horizontally, so
where the fillet is steep one cell spans 8–13 m of arc and the collider's chords sit up to 0.64 m proud of the true
surface (measured 0.17 m at 20 m out, 0.64 m at 26 m, 8 mm on the planar face), while the follow held the ball
0.13 m off it; the follow and the solver fought over the ball thirty times a second and bled its speed. So on a
walled archetype the wall band is a **shell**: for every profiled line and each side, the stamp itself sampled along
the line's lateral rays at the shell's stations (`WallProfile.ShellStations`: the level floor 4 m inside the edge, the
fillet every 6° of arc, a 3.1 m chord with 4 cm of sagitta, then the face every 4 m of lateral distance, planar and
so exact, out to 6 m past each vertex's own lip), swept as one indexed mesh with smooth normals and the terrain's
colour and collided as a concave shape on the structure layer (`StageHeightField.ShellStrips`, `WallShellMesh`).
The shell is the stamp, so it agrees with the analytic wall the follow reads and with everything the stamp does at
bends, berms, eases, ledges and forks. The heightfield under it is the same stamp **sunk one metre**
(`StageHeightField.Sink`, `WorldScale.WallShellSink`), fading in over three metres from the corridor's edge, one cell inside
the shell's foot (D-113: the grid is bilinear over 4 m cells, so a sunk node bends the collider for a whole cell round it; a
fade that began at the foot itself dipped the floor before the shell started and stood the shell's edge proud of it, a launch
ramp at the cap on the first tunnel drive), and out over the last four so shell and grid coincide at both edges; the collider's coarse chords never reach the ball, and the
follow holds the ball twice the shell's sagitta off the surface (`WallProfile.ShellRest`, 8 cm). The world's height
query (`MovementToyWorld.SampleHeight`) answers with the stamp wherever the sink is positive, so the ground
follow, the camera and the dressing all see the surface the ball rides; `GridHeight` is the sunk collider for
measurement. On the inside of a bend a vertex's ray stops a metre short of the centre of curvature, where the
rays would cross; stations past a vertex's lip fold onto its last point and make no face. Two lines' bands may
overlap (a ledge beside the primary): both shells sample the same stamp there and coincide. The ground stays
one single-valued heightfield; the shell is a structure like a lid, of the stamp's own surface.

Two rules the shell forced on the stamp. **The wall's position is interpolated along the route** (the floor's D-093
rule applied to the wall): the profile reads a bend's extra width, berm fade and height and inside ease
interpolated toward the neighbour the point lies toward, not at the nearest vertex; as a step function they jogged
the wall 0.56 m every 4 m wherever a bend's extra width eased in (1.7 m of height on the face), which the shell,
sampled at the vertices, smoothed into a ramp while the analytic wall the follow reads still stepped, and the ride
read the wall jumping half a metre tick to tick. **A line's band stops at the line's ends**: the nearest-vertex
rule claims everything behind a line's first vertex and past its last for those vertices, so a ridge's wall band
reached the primary's floor for a hundred metres behind the ridge's start with no shell over the sunk grid, and the
ball drove under the shell's first ray and was popped up through it. The analytic wall query (03 §3) stops at a line's
ends for the same reason, and requires the wall's outward direction to run across the line, not along it: an optional
line's end vertex, nearest to a point on the primary's floor 40 m before the join, reported its fillet foot as a wall
leaning forward like a ramp, and the carry turned that into a 99 m/s vertical launch on the canyon drive.

**The tunnel (D-113, `docs/13 §2`).** A tunnel line (`RouteLineKind.Tunnel`) is an offset line with the ridge's 200 m offset,
a cosine S as long as its two straights allow (290–450 m: r 85–205 m, so the slot holds the base cap with margin and often the
ceiling), no climb and a narrow corridor (`TunnelProfile`: half-width 15 m, a 6 m fillet, a 78° face, no setback). Its floor
is **the primary's base profile read at the point's projection onto the primary**, the canyon floor continued into the
rock beside it, so the trench meets the primary's floor exactly wherever the two overlap. Tunnels stamp **after every
other line, as cuts**: the ground without tunnels (`StageHeightField.SampleWithoutTunnels`) is computed first, and each
tunnel lowers it toward its floor by its corridor weight wherever the ground stands above the floor (never raising it);
inside the primary's level width and setback a tunnel cuts nothing (the primary's corridor stays exact) and the trench
fades in over the first 8 m of the fillet's foot (`TunnelMouthFade`). The result is the trench cut into whatever stands
there: a notch through the canyon's fillet and face, a slot beneath the mesa. A tunnel line measures a point's distance to
its polyline, not to its nearest vertex: 18 m out the vertex distance ripples 0.1 m every 4 m, half a metre of height on a 78°
face between the shell's stations and the analytic wall, and the ball was punted off the shell at every station (the canyon's
walls, 83 m out, keep the vertex distance and their hashes). Under a tunnel's shell the grid is sunk 3 m (`TunnelShellSink`), not
the wall's 1 m: a 4 m cell straddles most of a 6 m fillet and its chords stand up to 1.5 m proud of the arc. The **covered run**
is where the ground
without the tunnel stands `TunnelProfile.PortalDepth` (the crown plus a 4 m cap, about 29 m) above the floor
(`RouteSkeleton.CoverStart/CoverEnd`); its ends are the portals. The tunnel's own wall shell runs to the lip in the open cut
and stops at the arch's spring line (5 m) under the roof; the other lines' shells have a **hole** wherever a tunnel's cut
reaches, and their sink stops there (the tunnel's own band takes over). The roof is a structure (§5I).

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
a ridge beside a gap carries no copy of it. A launch crest has the same two prices (D-100): its free path is
the roll-off the model integrates, its paid path the full charge released at the apex at the arrival speed,
which must land on the straight with its run; at the ceiling the slam landing off that jump must. The
skeleton reserves crest straights for whichever of the four runs longest, and a dune train's roll-off is
integrated over the train itself rather than one crest on flat ground. Measured 2026-09-06: 100 seeds, 0 fallbacks, 54 gaps, 16
ramps and 134 banked turns all passing; the harness follower drives a ramp stage within 0.7% of the
model and its uncharged lip flight lands within 9% of the model's.

Vertical grammar modules (D-096, §5I), each with the same seven fields:

7. WallTunnel — a slot through a wall closed by a lid; the camera confines (06 §11); the roof refuses a charged jump, so the module declares its ceiling (§10 headroom).
8. Tunnel — a slot cut into the ground or through a wall, roofed by a shell of rock, on an optional line; a portal through a wall or a dive beneath the landscape, sometimes the only way into a pocket area (D-112, `docs/13`). Replaces the see-through tube, removed 2026-09-19.
9. SpiralPit / SpiralRamp — a conical helix of banked bends, each turn at a different radius, descending into a pit or climbing a mesa; falling off the inner edge lands on the turn below.
10. TerraceStep — a floor step of 60–90 m reached by a ramp lip and a full charge (at most 20 m when mandatory), with a landing zone on the upper floor and a drain below its edge; larger lifts are spiral ramps.
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

Delivered (T4, P-008): `StageScatter` states the keep-out once, from `StageDefinition` alone, and both the dressing and
the harness read it — outside every line's level width plus its bend extra and half its falloff (160 m, primary and
every optional line, all floors, terminal lines included), every exit pad plus 20 m (D-105), every checkpoint anchor
plus 30 m, every lid footprint plus 20 m, and the spiral disc plus 40 m. A
module's body and its landing run lie on their line and are inside the line keep-out by construction. The stream is
`StageGenerationRequest.CosmeticSeed`, so a scatter change can never move a stage hash, and no scatter collider stands
within 200 m of a line. Scale-cue posts at the level width every 200 m of straight route are the exception that stands
inside the corridor's edge, and they carry no collider.

### H — Validate

Run lightweight deterministic checks.

Failure → bounded regeneration.

Repeated failure → known-safe fallback configuration rather than broken stage load.

### I — Vertical grammar (D-096)

Three kinds of thing, and the ball drives on all of them:

- **Ground**: the one single-valued heightfield (§9). Walls (a step of any height across one or two cells),
  terraces (floor steps), slots (walls both sides of a corridor), cliff edges and spiral pits are all stamps
  in it, because a heightfield is happy with a near-vertical face. Nothing here needs a second representation.
- **Structures**: a separate generated mesh plus collider, as §9 allows for bridges and overhangs. Three:
  the **lid** (a box roof over a slot: a wall tunnel, or a bridge when used as a floor), the **wall shell**
  (D-111: the stamp's wall band swept finely over the sunk grid) and the **tunnel roof** (D-113, `docs/13 §2`:
  over a tunnel line's covered run, an arch from spring line to spring line (the circular arc tangent to the 78° face
  at the two spring points, 5 m up: a horseshoe with its crown about 25 m up, sampled every 10°, so the surface from
  the floor over the fillet, the face and the arch is tangent-continuous and a ball riding up the wall runs onto the
  arch and drops off it where the ceiling rule ends the ride, with no crease to hit), the **cap** (the ground as it would be without
  the tunnel, over the trench's footprint, so the surface continues over the tunnel and is drivable), a
  **portal face** at each end (the rock between the trench's walls, the arch and the cap, in the portal's plane)
  and its 1.5 m rim; `StageHeightField.RoofStrips`, built and collided like the wall shells). The see-through
  tube (D-101) was removed on 2026-09-19: a glass pipe in the air was the wrong idea for a landscape the
  player descends into and pierces through. Structures are never a second height layer: the ground follow,
  the route speed model's touchdown and the validators keep reading the one heightfield, and the contact
  baseline carries the ball on a structure (03 §3). The tunnel's cap is the one place a stage has two surfaces
  over one XZ, and it is the lid-as-floor case: the world's height query takes the asker's own height and
  answers the cap's top when the asker stands above the cap less a ball, the trench otherwise
  (`MovementToyWorld.SampleHeight(x, z, y)`, `IGroundSurface.Height`); the ground follow passes the ball's
  height, the camera its lens's, every other caller nothing.
- **Floors**: terraces, not stacked layers. A jump step is 60–90 m (the full-charge apex is 91 m above
  the lip at any speed, because the takeoff sets the vertical and a ramp does not add to it, `docs/11
  §7`); larger lifts are spiral ramps, which a driven ball climbs at up to 35° without losing
  speed; three floors put the top 120–270 m up. The guaranteed primary route stays on the lowest floor (the free path of the two-price
  rule), upper floors are paid lines, and under every edge of an upper floor the ground **drains**: every
  cell below the edge has a descending drivable path back to the primary. The top floor may sit in the cloud
  band (06 §3). Floors that share an XZ (a true helix inside a tower, a room under a room) are not built; if
  the design ever needs them they are the same height-source abstraction instantiated per floor with a NaN
  mask and a floor-aware follow, tracker and touchdown, and that is a separate decision.

**Headroom as delivered (D-102):** nothing but a declared lid may stand within the full-charge apex (plus a
ball) above the primary's centreline; the validator reads every structure against it. (It caught the first
see-through tubes swinging back across their own corridor before any ball did; tubes are gone, the check stays
for tunnels and lids.)

### J — Exits and branching (D-105)

A stage ends at whichever exit pad the ball reaches. The primary's pad is exit A. A **terminal line** is an
optional line that never rejoins: it leaves the primary through the ridge family's S (200 m out over 290 m, on a
straight, its fork clear of every feature), climbs its plateau (16–30 m over a 300 m cosine ramp; on Sky Terraces
the line is a floor-2 terrace, 60 m up with its cliff draining onto the primary's flank), widens to 350 m off the
primary over 300 m where the archetype allows (Highlands, Dune Sea; a canyon exit slot and a sky terrace keep the
family offset), runs level for 200 m and ends on a pad flattened like the primary's. Up to two per stage, on
opposite sides of the primary, forks at least 300 m apart, both inside the last 3 km before the terminal stop (the
primary's pad, or a spiral pit's approach: a pit stage's exits fork before the set-piece, so taking one skips the
finale). Terminal lines claim their spans first, from their own seed stream, and rejoining lines fill around them on
the same side; on Sky Terraces the floors come first (a floor-3 stack needs those sections) and the terminal terraces
fill around them. A line lies on the outside of every bend it shadows, so a stretch of alternating bends limits how
many exits fit: measured 86% of Highlands, 97% of Canyon Run, 61% of Dune Sea and 73% of Sky Terraces seeds carry a
second exit (29 / 38 / 14 / 15 % a third). Every exit pad sits inside the footprint with its radius to spare, is
level (±3 m over ±30 m across and ±40 m along), and any two are 250 m apart. Entry is the drive-in ramp: the
choice is steering (02 §3). Lids never roof a fork or a merge. The one heightfield carries it all: a terminal line
is a stamp like any other line.

### Rolling Highlands

**Primary skill:** terrain reading and momentum conversion.

Geometry:

- broad hills/valleys,
- long ridges,
- crest launches,
- valley safety vs ridge shortcuts,
- long sweeping lines.

**Ridge lines as delivered (D-086, redesigned D-100):** a ridge shadows a bend, or a run of up to three, on
the outside: it leaves the primary on the straight before the first bend and rejoins on the straight after
the last, each transition an S of 200 m over 290 m (r 70, the tightest that holds the base cap), then a
cosine climb of 16–30 m over 300 m onto a short plateau and the mirror descent, 1 260 m in all. Both
transitions lie wholly on primary straights and the ramps wholly outside the transitions: a transition
through a banked bend rides the berm, and a ramp inside a transition is scaled by the primary's falloff
blend into a convex knee; either launched the base kit into the transition's own turn, which no check had
seen before D-100. Every optional line is now integrated at both speeds and judged on its own geometry: a
line whose base-kit flight drifts through a turn or lands too fast for a corner in its run is dropped from
the stage (not the stage from the batch); the ceiling verdict is reported, as the paid line's risk. With the
family's 250–600 m straights about 90% of Highlands seeds and 70% of Canyon seeds carry a ridge; a Dune Sea
rarely does and has no line design of its own yet.

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
holds what an archetype changes (geometry only, §7); `World › Archetype`, `-- --canyon` and
`RUSHCORE_ARCHETYPE=canyon` select it.

**The wall profile, delivered 2026-09-19 (D-109, §5D).** The slot walls are the authored profile: the 30 m fillet, the
72° face, the rounded lip, the berm flowing into the fillet on bend outsides, the 30° inside face; wall tunnels
therefore span 226 m wall to wall (the roof's ends sit inside the wall where the profile has risen past the roof's
top) instead of 190 m. The rock is in the tint: strata bands every 14 m of height on the faces (06 §3). Every canyon
hash moved with it (the golden table was re-recorded).

**Portal tunnels, delivered 2026-09-19 (D-113, `docs/13 §2`).** A rejoining line's site takes a tunnel instead of a ledge
by `ArchetypeRules.TunnelChance` (0.7): the line leaves through the ridge's S, cuts a 20 m notch through the wall's fillet
and face (the open cut, the read cue), goes under the mesa where 16 m of rock stands over its floor (the portal face with
its arched opening and rim), runs 120 m at full offset and comes back out through the far portal to the rejoin. Inside,
the floor and walls are ground (the wall ride applies), the arch a ceiling, the camera confined under it. Validators in
§12; harness in 08 §5.

**Wall tunnels and the spiral pit, delivered 2026-09-07 (D-102).** A **lid** roofs a plain slot straight
150–300 m long, wall to wall (190 m), its underside 15 m above the highest corridor point under it and 6 m
thick; the module declares its ceiling (§10 headroom: the roof refuses a charged jump), the camera confines
under it (06 §11), and the roof is a floor from above. Half of Canyon Run stages end in a **spiral pit**: after
the wander the route crosses to the far side of the band, runs one outer radius straight, then turns one full
turn inward through four quarter arcs of shrinking radius (380 → 150 m, 230 m shed per turn: two level widths on
bends, two setbacks and the cliff face between a turn and the one below it), descending 120 m as a perfect helix
on the entry level (a swell under two kilometres of bends would launch the ceiling ball inside them). The pit's
surface replaces the relief and side terrain inside the rim: the entry level out to the outer turn, then a cone
down to the pit floor at the inner radius, the rim a cliff to the side terrain; the corridor's turns are
terraces cut into the cone, no berm on the pads, and the exit pad sits on the pit floor. Headings run unbounded
through the turn (D-096): the plan constraint is the footprint alone and the exit lies wherever the turn ends
inside the disc. Measured: 100 canyon seeds valid, 193 lids and 53 pits; the spiral drives to the exit pad at
134–149 m/s fully grounded, a charged jump under a lid never puts the ball above the roof, the roof carries the
ball as a floor, the lens stays under the roof through the tunnel, and a ball dropped off a turn's inner edge
lands on the turn below and drives on to the exit.

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
- 60–90 m jump steps and spiral ramps up; cliff edges and drains down,
- the top floor in the cloud band,
- tunnels and bridges linking floors and branching lines (tunnels per `docs/13`),
- every fall lands on ground that drains back to the primary.

**Delivered 2026-09-07 (D-103), the first cut:** the Highlands family with 500–900 m straights and its
optional lines as **terrace floors**: floor 2 is a corridor 60 m above the primary and 200 m to its side,
floor 3 a corridor 120 m up and 300 m out, stacked beyond floor 2 on the same section. A terrace leaves the
primary through an S-transition on a straight (290 m for floor 2, 360 m for floor 3), climbs a cosine ramp a
driven ball takes without losing speed and whose knee never launches at the cap (420 m for 60 m, 600 m for
120 m), runs its plateau past the bend it shadows, and returns the same way; every terrace therefore rejoins
the primary and the graph stays acyclic. Its inner edge is a **cliff** (a 12 m face) whose foot lands on the
floor below: the primary's flank for floor 2, floor 2's plateau for floor 3, so a fall from any floor is a
setback of one step (60 m, a slam landing) onto drivable ground that leads back to the primary; its outer
edge, where no floor stands beyond it, descends into the relief at the route grade (a smoothstep of the
height plus half a swell over the grade limit, times the smoothstep's 1.5). The drain validator reads lateral
cuts every 100 m of plateau: the cliff foot within 15 m of the floor below, no step up over relief noise on
the way down, the outer slope inside the grade; a terrace that fails is dropped with its section (like a line
whose flight cannot hold a corner).
The cloud band is two translucent sheets 130 and 160 m above the primary's mean height, so floor 3 sits in
it. Measured: 100 seeds valid, 0 fallbacks, 96 floor-2 and 3 floor-3 terraces on 82 seeds, 13 dropped.
Floor 3 is rare because its section needs about 2 km bounded by straights; branching floor 3 from floor 2,
the jump step (a lip and a full charge onto a landing zone rather than a ramp),
bridges between floors and a second primary floor are the open work of this archetype.

Archetype rules (D-098, extended D-099): an `ArchetypeRules` record per archetype carries the wall height,
the wall and inside falloffs, the bend mix, the straight lengths, the bank scale, the swell slope budget,
the feature spacing / chance / mix, the dune-train crest count and the heading limit; the skeleton builder,
the height field and the validators read it and nothing else differs between archetypes.

### Summit Descent (D-107)

**Primary skill:** hairpin commitment and line choice at the cap on a continuous descent.

Geometry (design owned by `docs/12` until delivered; this entry is the pointer):

- summit start, 600 m of descent to a valley run, no mandatory climb,
- 4–6 switchback tiers: a leg across the face at grade 0.06–0.13, then a 140–156° hairpin of 80–120 m radius
  (holdable at the base cap, never below 80 m on the primary) carrying 45–85 m of drop as a banked descending curve,
  every convex exit eased to the 560 m crest radius so the base-cap ball never launches,
- walls everywhere: 24 m near-vertical rails on every downhill edge, 90 m headwalls behind every hairpin's berm,
  the cut face uphill; a miss is a hard impact, never a fall; the face outside the rails drains to the valley,
- blind hairpins by construction, paid for by a notched inside wedge, a hairpin board and a sightline validator
  (the archetype's declared exception to §10's outside-walls-only rule),
- the chute as the optional inside line: a 40 m rail gap, a 150 m eased lip onto a 0.35 walled straight through
  the wedge, a merge bend onto the next leg; free path grounded, paid path a half charge and a slam landing,
- the valley run as the family wander with 12 m guide banks; exits fork there (§5J).

Not delivered. The new validators (holdable hairpins, rail continuity, grounded primary, descent budget, sightline,
chute, face drain) are listed in `docs/12 §8`; `08 §5` carries the gate lines.

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

Delivered forms (D-096, D-111, D-113): the lid is a `BoxShape3D` and a box mesh; the wall shell and the tunnel roof are
`ConcavePolygonShape3D`s of their strips with backface collision. All sit on a structure physics layer the ball collides
with; the camera's occlusion probe reads the terrain (the trench's own walls are in the heightfield) and the roof
confinement handles the arch. The see-through tube's swept ring (D-101) was removed 2026-09-19 (D-112).

The rendered ground is the **resident mesh** (`docs/13 §3.3`, D-115): the same flat-shaded tile builder over the same
heights at two cell sizes, a coarse 16 m mesh resident over the whole stage and a fine window of 4 m tiles whose centres
lie within 1.2 km of the ball (re-evaluated every 100 m of travel, 300 m of hysteresis, built on worker threads and
committed on the main thread), coarse tiles hidden under fine ones, a 6 m skirt on every tile's edges. The collider is
never windowed: one `HeightMapShape3D` at 4 m over the whole stage, so physics, the follow and the validators see one
surface at all times. Known (D-115): Godot's Jolt module builds a non-square heightmap as a mesh shape (exact, 1.15 s at
752 k samples); a square one as a true heightfield (0.1 s, samples quantised per block). The map stays non-square until
the user decides.

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
  the local arrival speed and grade by default) unless the module declares a ceiling (lid, tunnel),
- **wall clearance**: a wall face stands at least two cells outside the corridor's level width, so the ground
  follow's lateral samples never read it (delivered D-098 as "the primary's stamp weight is still 1 at the
  level width plus the 8 m setback on both sides", the inside of a bend tighter than that reach excepted),
- **drains**: below every edge of an upper floor the ground descends drivably to the primary: no wall foot,
  no NaN, grade within the route limit,
- **blind corners**: a wall on the inside of a bend hides the read horizon; until difficulty is reassessed
  after Phase 4, walls sit on the outside of bends.

Optional lines (D-100) obey the flight rules on their own geometry at the base kit: no flight drifts more than the
tolerance through a turn of the line (drift ≈ length × turn / 2) and every corner limit inside a landing run holds
the landing speed after the brake's shed; a line that fails is dropped and the stage stands. At the ceiling the
same reading is reported, not enforced (§11: a paid line may demand more).

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

A mandatory terrace step is a mandatory jump and obeys this section. Paid floors may demand the full charge or
the ceiling (D-096).

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
- vertical grammar (D-096): headroom, wall clearance and drains hold; every exit has its landing zone; the line graph is acyclic in route distance and every line rejoins the primary or reaches an exit,
- exits (D-105): at least the primary's; every pad inside the footprint, level, and any two 250 m apart; a terminal line's pad is level over the pad radius and its profile never stalls.

- tunnels (D-113, `docs/13 §2.7`): every tunnel line's covered run is at least 60 m with 16 m of rock over the floor at
  every covered vertex, the trench's floor at the centreline is the line's floor, the ground beside the middle of the
  covered run rises from the floor as the tunnel's 6 m fillet and 78° face within a metre, the corridor's corner limits
  hold the base cap along its whole length, no lid or other tunnel's portal stands within 100 m of a portal, and the
  covered run never comes within a corridor width of another line; the fork-to-rejoin time against the primary's is
  reported (a line that leaves through an S is longer than the straight it shadows).

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
- structure bounds (lids, wall shells, tunnels),
- floor ids and drains,
- validation report,
- generation phase timing.

Delivered (D-104): `World › Route Debug Lines` draws the primary (cyan straights, orange bends, magenta feature
zones), optional lines by floor (green ridges and floor 2, gold floor 3) and checkpoint posts;
`World › Stage Debug Views` (off by default, 07 §11) adds corridor bounds on every line, challenge zones (a yellow
line from a module's entrance to the end of its landing zone with posts at both ends, and over every committed
bend), structure bounds (lid box outlines, the spiral pit's rim), floor bounds in the floor's
colour and each terrace's drain (a red line along its cliff foot), and a ring at the pad radius around every exit
(D-105). Terminal lines draw in their own colour. The telemetry `seed` row carries the counts (lines, terraces,
lids, the pit, exits) and the validation status; the `stage` row names the exit reached; the log prints
every check and the phase timings; signs mark gaps, ramps, crests, tunnels, floors, the pit, every exit pad
(`EXIT A`, `EXIT B`, ...) and every fork (`EXIT B ↑` at the top of its S). `RUSHCORE_EXIT_TRACE=1` tallies why
terminal candidates were rejected across a batch.

## Empirical validation items

1. ball/world scale,
2. terrain grid spacing/resolution,
3. stage physical dimensions,
4. hill/valley/gap scale,
5. exact stage clear-time target,
6. render chunking threshold if profiling requires it.

Measured (T5, 2026-09-08, `RUSHCORE_MEASURE=1`; instruments only, no rule, tolerance or verdict changed):

7. **the wall-clearance probe.** Over 400 batch seeds the check rejects 6 attempt-1 builds — 4 on Canyon Run, 2 on
   Sky Terraces, none on Rolling Highlands or Dune Sea — and every one of them on a **straight**, not a tight bend.
   Reading the terrain across the probe window separates them: Canyon's four stand in front of a real wall face
   (the ground rises 83 m on average, 93 m at the worst), Sky's two stand on ground that rises 1 m with no optional
   line within 337 m. The probe is sound on Canyon and is measuring something other than a wall on Sky.
8. *(the tube ride table; removed with the tubes, D-112.)*
9. **floor-3 room on Sky Terraces.** Of 95 floor-2 sections on 100 seeds, 4 could carry a floor 3 branching off them
   under the shipped `Floor3` numbers — the same 4 seeds that carry one today. Room is not the constraint (4248 m
   mean against a 2000 m need); the floor-3 shape's own transitions are, blocked by a bend on 39 sections and by the
   inside of a bend on 32.
10. **landing run against the model.** Over 7 real flights on Highlands seed 8 and Dune Sea seed 1, the ball is back
    within 2% of the model's speed inside **0–12 m** of touching down (mean 4 m), against the 100 m the model
    reserves. The reservation is not challenged by speed: the model already predicts the landing loss.

Route representation details (polyline/curve helper, internal data structures) are implementation decisions as long as this behavioral contract is preserved.
