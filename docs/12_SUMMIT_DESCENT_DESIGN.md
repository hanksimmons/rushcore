# 12 — Summit Descent (fifth archetype) — Design

**Status:** DESIGN ACCEPTED as direction 2026-09-19 (D-107); nothing delivered yet  
**Authority:** owns the Summit Descent archetype until it is delivered, when its accepted parts fold into `04 §6`
and this file becomes the record. Everything else (movement `03`, generation pipeline `04 §5`, validation `04 §12`,
acceptance `08`) is unchanged and binds this design.  
**Inputs:** the frozen movement baseline (`03 §15`, D-095), the world-scale family (`11`), the vertical grammar
(`04 §5I`, D-096), the module grammar and two-price rule (`04 §5E`, D-097), the ceiling addendum (`11 §6`, D-094),
branching exits (`04 §5J`, D-105), the boost economy (D-106).

## 0. What was asked, and what was answered

The user's brief (2026-09-19): every map needs more verticality and dynamic route pathing; switchbacks up a giant
mountain; bombing or carving down the hill through hairpin turns; steep mountainous walls, canyon rock, snow banks
or architecture that make the player commit to a line and stay on course, like Mario Kart or a racing game;
prototype one new map type with these features prominent and the vertical amplitude turned up; the user playtests
it when it works.

Four questions narrowed the feel. The answers are the design's fixed points:

| Question | Answer | Consequence |
|---|---|---|
| A hairpin at the cap | **Both, by line**: the primary's hairpins are holdable at the cap; optional inside lines cut tighter for a Flow gain | primary hairpin radius ≥ 80 m; the inside line is the chute (§5), see the deviation in §11 |
| Where the stage spends its time | **Summit start**: the whole stage is the descent; climbs only as short optional lines | spawn at the top; no mandatory climb; the mountain is seen from above first |
| A missed line | **Walls everywhere**: a miss is a hard impact, speed and Flow lost, never a fall | rails on every downhill edge, headwalls on every hairpin; no cliff edge on the primary |
| Height over a ~6 km stage | **About 600 m** | 4–6 switchback tiers plus a valley run; the counts in §3 replace the "ten to fourteen legs" guessed in the question |

## 1. The governing constraint (read this before the geometry)

`11 §2` already found it: **bends are the throttle, terrain is not.** With the frozen kit a driven ball holds the cap
up or down any grade under 45°, so a mountain cannot make the player slow down, and height is free to the player.
What a mountain *can* do is put corners in front of a ball at the cap and walls beside them. This archetype is
therefore designed around three numbers, none of which are ours to change:

| Kit fact (D-095) | Value | Source |
|---|---:|---|
| Turn radius that holds the base cap (148.5 m/s) | 68 m | `RouteSpeedModel.CornerSpeedLimit` |
| Turn radius that holds the Flow ceiling (254.7 m/s) | 201 m | `WorldScale.CeilingBendRadius` |
| Crest radius that launches a ball at the base cap / the ceiling | 560 m / 1647 m | `04 §8`, `11 §6b` |

Corner-limit ladder at the base kit (the model's closed form, steering saturated above the base cap):

| Radius | 50 m | 68 m | 80 m | 100 m | 120 m | 160 m | 201 m |
|---|---:|---:|---:|---:|---:|---:|---:|
| Speed held | 124 | **148.5** | 161 | 180 | 197 | 227 | **255** |

Two more facts shape the descent:

- **Every convex transition is a launch unless it is eased.** A grade change of Δ needs an ease of at least
  Δ × 560 m to keep a base-cap ball grounded (Δ × 1647 m at the ceiling). A hairpin exiting from grade 0.24 onto a
  leg at 0.085 needs ≥ 90 m of ease; a chute entry from 0.085 to 0.35 needs ≥ 150 m. A "bomb down the hill" that
  stays on the ground is a *profile with no unearned convexity*, exactly as the spiral pit was built (D-102). Any
  drop the profile does not ease is a flight of a kilometre or more at the cap, and no rail catches that.
- **The carve runs wide, not tight.** The carve (D-089) swings the facing at 84°/s while the velocity understeers at
  0.459 of the steering authority, then re-aims the velocity along the facing on release. Its path radius at the base
  cap is ≈ 149 m (v² / (0.459 × a_lat)), at 100 m/s ≈ 75 m. A carve therefore takes a hairpin by drifting *out* to
  the berm and firing out along the new heading; it cannot take a tighter inside radius than steering can.

## 2. The stage in one paragraph

The ball spawns on a **summit pad** and looks down a cirque: a fan of switchback benches cut into a mountain face,
each bench walled by a snow rail on its downhill edge, each hairpin closed by a rock headwall on its outside, the
valley floor 600 m below in the fog. A 300 m approach drops onto the first leg. Four to six tiers follow: a leg of
about 600 m across the face at a gentle grade, then a 150° hairpin of 80–120 m radius that carries most of the
tier's drop as a banked, descending curve. Every hairpin is holdable at the cap on its line; a wide line rides the
berm into the headwall, costs speed and, if it is hard, Flow. Before some hairpins a rail gap opens onto a **chute**,
a walled straight through the hairpin's inside wedge that rejoins the next leg: driven, it is grounded the whole
way; half-charged off its lip it is a flight into the chute with a slam landing, which is the Flow. After the last
hairpin the **valley run** opens: 1.5 km of fast cruise bends on the floor, where the exits fork (D-105). At the
ceiling nothing here is holdable but the valley, so the chain's headroom is spent on the legs and the run, and the
hairpin is where a chained player decides between the brake and the berm.

## 3. Geometry and numbers

All numbers are generation inputs (`WorldScale`, `ArchetypeRules`), seeded within the stated ranges. The route
speed model checks every one at both speeds (`04 §12`). Nothing here reads or changes player physics (`04 §7`).

### 3a. The face

- The mountain face descends along the stage axis (+X) at **face slope S = 0.30–0.38** (17–21°) from the summit
  pad to the valley floor; the drop from the summit pad to the last hairpin's exit is the **descent target 600 m**
  (validator band 540–660 m). The valley floor is the stage's reference height; the summit pad stands ≈ 600 m
  plus the valley run's own fall above the exit pads.
- Outside the route band the face continues as terrain, drivable (grade ≤ 0.40) and NaN-free, draining to the
  valley run: a ball that leaves the rails in flight (only a ceiling flight can) lands on ground that leads back.
  In the scenery margins (|Z| > 600 m) the face rises into **cirque walls** 200–400 m above the local face, the
  vista's frame; the **summit cone** rises 150 m behind the start pad. Both are scenery: outside every corridor,
  outside every validator but the footprint.
- The switchback section uses **no long swells** (swell budget 0): the profile is authored by the builder (§3c).
  The valley run uses the family's swells at a 0.10 budget, so the floor rolls.

### 3b. Benches (the legs)

- A leg crosses the **fan width W = 600 m** (Z from −300 to +300) at **θ = 12–20°** off the contour, alternating
  direction each tier, so its length is L = W / cos θ ≈ 610–630 m and its grade is S × sin θ = **0.06–0.13**.
- The bench is the corridor stamp (`04 §5D`) at the typical width (150 m level) cut into the face: the uphill side is
  a **cut face** (the mountain, blended over the 12 m wall falloff so it reads as a wall from the bench); the
  downhill side is filled to the bench level and closed by the **rail** (§3d). Wall clearance holds by
  construction: both faces stand 8 m outside the level width (`WorldScale.WallSetback`).
- At the cap a leg takes ≈ 4.2 s. The tier rhythm (leg + hairpin) is ≈ 6 s. This is the number to feel.

### 3c. Hairpins

- A hairpin joins two legs with a turn of **180° − 2θ = 140–156°** at **radius r ∈ {80, 100, 120} m**
  (`HairpinRadiusMin/Max`; seeded per hairpin). Corner limit 161–197 m/s: **holdable at the base cap on the
  line, with margin for a partial chain**. Never below 80 m on the primary (the accepted committed bend of 50 m
  would not be holdable, and the user chose holdable).
- The hairpin advances the route ≈ 2r cos θ ≈ 150–230 m down the face, so it **carries 2r cos θ × S ≈ 45–85 m of
  drop over an arc of 195–330 m**: a grade of 0.20–0.29, inside the 0.40 limit. It is a descending banked curve:
  the family bank (18/145 × r) × **bank scale 1.5** = 15–22 m at the outside, capped at 30 m.
- **Eases:** the entry (leg grade → hairpin grade) is concave and free. The **exit** (hairpin grade → leg grade)
  is convex and is eased over **max(90 m, Δgrade × 560 m)** so the base-cap ball never leaves the ground; the
  ceiling ball's launch off the exit is reported per hairpin (it lands on the next leg or its rail; see §7).
- **Tier count:** the builder adds tiers until the accumulated drop reaches the descent target, then turns onto the
  valley run. With the ranges above that is **4–6 tiers** (S 0.33, θ 15°, r 100: 117 m a tier, 5 tiers, 585 m).
  A smaller r and θ give more, shallower tiers; larger give fewer, steeper. Both are one seed draw.

Worked default (S 0.33, θ 15°, r 100, W 600):

| Element | Length | Drop | Grade | Time at the cap |
|---|---:|---:|---:|---:|
| Summit approach | 300 m | 25 m | 0.085 | 2.0 s |
| Leg | 621 m | 53 m | 0.085 | 4.2 s |
| Hairpin (150°) | 262 m | 64 m | 0.24 | 1.8 s |
| Tier (leg + hairpin) × 5 | 4.4 km | 585 m | | 30 s |
| Valley run | 1.5 km | 60 m | ≤ 0.10 swells | 10 s |
| **Stage** | **≈ 6.2 km** | **≈ 670 m** | | **≈ 42 s at the cap** |

The primary route target is 6 km (`WorldScale.PrimaryRouteLength`, V-009); the builder trims the valley run to hit it.

### 3d. Walls everywhere (the commitment rule)

The user's rule: a missed line is a hard impact, never a fall. Three wall kinds, all stamps in the one heightfield:

| Wall | Where | Height | Inner face | Purpose |
|---|---|---:|---|---|
| **Rail** (snow bank) | the downhill edge of every leg, both sides of a chute, both sides of the valley run | 24 m (20–30) | near-vertical, 12 m falloff (63°+) | catches a shallow miss (≤ 25° at the cap lifts the ball ≤ 15 m: it stays on the bench) |
| **Headwall** (rock) | the outside of every hairpin, from the berm's crest | 90 m (80–100) | near-vertical, 12 m falloff | catches a missed hairpin at any angle (a head-on hit at the cap lifts ≈ 88 m) |
| **Cut face** | the uphill edge of every leg and the inside of every hairpin | the mountain itself | 12 m falloff to the face slope | the mountain; nothing to design |

Why near-vertical rather than banked: a wall face removes the velocity component into it and keeps the rest, so a
73° face lifts a ball by about 0.28 × its lateral speed; a 45° bank would instead turn all of that lateral speed into
climb (u²/2g: 130 m at a 51 m/s graze) and throw the ball over. The berm (§3c) is the bank; the headwall behind it
is the catch. **Rail heights are the first thing the harness measures (§8):** the rail must hold a base-cap ball
driven into it at 25°, the headwall a base-cap ball driven straight at it.

A rail contact is not a Flow event by itself. The accepted rule (`03 §9`, D-091) takes Flow only for a **hard
impact**, one tick shedding more than 20 m/s of locomotion. A shallow rub sheds less and costs only the speed it
sheds; a real hit costs 0.5 Flow. That is the user's "speed and Flow lost" at two prices, and it needs no new rule.
Whether the threshold reads right on a berm is a sample verdict (§10).

### 3e. Blind corners and sightline

Every hairpin's inside is the mountain: a blind corner by construction, which `04 §10` forbids on other archetypes
until difficulty is reassessed. Here it is the point, and it is paid for with a sightline rule instead:

- the **inside wedge** (the face inside the hairpin's arc, ≈ 30 m above the bench at the arc's centre) is
  **notched** down to at most 8 m above the higher leg over the arc's inner 60 m, so from the entry the exit leg's
  rail crest and its sign show above the wedge; the notch is also where a chute runs (§5),
- a **hairpin board** stands at the entry, 100 m before the arc, reading the turn direction and the hairpins left
  (`↰ 4`), on the existing sign system; posts every 20 m along the berm's crest mark the outside of the arc from
  the entry,
- the **sightline validator**: from 12 m above the centreline at the board, the line to the top of the exit sign
  (a 20 m post at the arc's exit) clears the notched wedge by 2 m; a hairpin that fails is notched deeper, and one
  that cannot be is regenerated.

### 3f. The valley run and the exits

After the last hairpin the route eases (concave) onto the floor and runs the family's wander (headings within 45°,
cruise and fast bends, straights 250–500 m, swells at 0.10) for the rest of the stage, railed at 12 m on both sides
(guide banks, so "walls everywhere" holds without walling the view). One launch-crest feature may sit on the run
(the family's, at its normal spacing) as the run's Flow opportunity. Terminal lines (D-105) fork here in the last
third, through rail gaps, onto their own plateaus; `ExitSpread` 300 m as on Highlands.

## 4. The two prices on a hairpin (`04 §5E`)

A hairpin is a **BankedTurn** of the module grammar at a new radius family with rails, not a new module:

- **Free path:** steer it. The line holds at the base cap by construction (§3c). The model's arrival speed is
  carried through; nothing stops, nothing drops below 2/3 of the base cap.
- **Paid path:** carve it. The carve swings the facing through the turn while the ball drifts out toward the berm
  and fires out along the exit leg at no less than its entry speed; a real turn (≥ 30°) grants the carve's Flow
  (`03 §11`). The corridor at a hairpin is widened so the carve's ≈ 149 m path at the cap fits between the line
  and the berm: **hairpin level width 200 m** (the family widens bends already; here it is a fixed number).
- **At the ceiling:** no hairpin is holdable (201 m needed). A chained ball either brakes to the corner limit
  (Flow loss 1.0/s while braking, the accepted rule) or runs wide onto the berm and comes off it at the base cap.
  Which of these the sample favours, and whether the berm ride stays under the hard-impact threshold, is the
  archetype's central feel question (§10). The design does not resolve it on paper.

## 5. The chute (the optional inside line)

The user asked for inside lines that cut a hairpin tighter for a Flow gain. §1 shows the carve cannot do that, so
the inside line is a **chute**: an optional line (`RouteLineKind.Chute`) through the hairpin's notched wedge.

- **Entry:** a **rail gap** of 40 m (the technical corridor width) in leg n's downhill rail, 120 m before the
  hairpin's arc, marked `CHUTE ↓`. Committing means aiming at a 40 m gap at the cap.
- **Body:** a walled straight (rails 24 m both sides, width 40 m) down the wedge on the leg's heading rotated
  toward the next leg, **eased convexly over 150 m** from the leg's grade to **0.35**, then straight, then eased
  concavely onto **leg n + 1** 100 m past the hairpin's exit through a **merge bend of r ≥ 100 m** aligned within
  30° of the leg. Length ≈ 350 m, drop ≈ 100 m (the tier's drop plus the leg's).
- **Free path:** drive it. Grounded at the base cap the whole way (the ease is sized for it); the merge bend holds
  the arrival speed. Time saved against the hairpin ≈ 1 s.
- **Paid path:** a **half charge at the lip** (the ease's start is the lip; the model's `JumpRange` at the arrival
  speed over the chute's falling ground) lands inside the chute's last 200 m (the landing zone), and the **slam
  landing** there is the Flow (`03 §9` gains) plus the burst on the touchdown press. A full charge overshoots the
  chute at the cap (439 m of range plus the fall) and is not a line: the validator confirms the full-charge flight
  lands on the face, drivable, not on a rail crest.
- **At the ceiling:** the lip launches (150 m of ease is under the 1647 m radius's 400 m) and the flight lands past
  the merge on the next leg or the face; reported, not enforced (`04 §10`, optional lines).
- **Placement:** `ChuteChance` 0.5 per hairpin, at most 2 per stage, never on the last hairpin (its exit is the
  valley ease). The chute is the only rail gap on a leg.

A **second cut** (not in this design's first delivery): the **gallery chute**, which crosses leg n + 1 on a lid roof
(the leg runs through a short wall tunnel, D-102) and lands on leg n + 2, skipping a whole tier: 230 m of drop,
a flight at the base cap, a real shortcut. It waits for the first chute's verdict.

## 6. Verticality budget ("juice")

What the user will see from the summit pad, in metres above the valley floor:

| Element | Height | Note |
|---|---:|---|
| Summit cone (scenery, behind the pad) | +750 | white |
| Summit pad | 600 + valley fall | the start |
| Cirque walls (scenery margins) | face + 200–400 | frame the fan on both sides |
| Hairpin headwalls | bench + 90 | the tallest thing on the route |
| Tier step (bench to bench) | 100–170 | 4–6 of them |
| Rails | bench + 24 | continuous, the line's edge |
| Berms | bench + 15–22 | on hairpins |
| Valley floor | 0 | the run, the exits |

Against the family: Canyon Run's walls are 60–120 m over a floor that rolls 40–80 m; Sky Terraces' top floor is
120–270 m up. The Summit puts 600 m of continuous descent under the ball with 90 m headwalls at every turn, and
that is the amplitude the brief asked for. Fog: the archetype sets its fog end to 3200 m (a presentation number,
`WorldDressing`, not baseline) so the valley is a shape from the summit rather than a wall of fog.

## 7. Presentation (`06 §3`)

- **Palette by height** over the valley floor: **snow** above +350 m (white ground, blue shadow faces on the
  rails so a rail reads as a bank from the bench), a **scree** band +150 to +350 (grey-brown), **rock and moss** on
  the valley run; headwalls grey rock at every height; the summit cone white; cirque walls darker rock with snow on
  their tops. Slope tint as on the family, so cut faces and rails read as walls.
- **Rails** carry a crest line (a lighter band on the top 2 m) so the edge of the drivable width reads from the
  bench and, on the far leg, from above. **Berms** keep the family's bend tint.
- **Signs:** the hairpin board (`↰ 4`), `CHUTE ↓` at each rail gap, `EXIT A/B/C` as delivered (D-105), and the
  route's chevron posts on berm crests. Sizes at the 0.66 m ball (the T6 audit's open question applies here too).
- **Camera:** no new rule. The chase camera at its fixed pitch on a 0.24 grade hairpin is a FEEL item; the
  confined camera (lids) is unused in the first cut.
- **Scatter (T4):** boulders on the face outside the rails, none on benches; snow drifts against the uphill cut.

## 8. Generation plan (`04 §5`)

Structure before noise, as everywhere:

1. **Skeleton** (`RouteSkeletonBuilder`, a new emit path beside `EmitSpiral`): summit pad → 300 m approach turning
   onto the first leg's heading → tiers (leg, hairpin) with **unbounded headings** (D-096, as the spiral) until the
   descent target is met → concave ease onto the valley → the family wander to the exit zone → terminal lines
   (D-105). The builder writes the **route height profile itself** (bench grades, hairpin grades, every ease) so
   the heightfield stamps it as authored; `RouteSegmentKind` gains nothing (a hairpin is a `Bend` with a radius).
2. **Heightfield** (`StageHeightField`): the face (S along +X) with the summit cone, the cirque walls in the
   margins and micro-relief; the corridor stamp with per-side treatment (`InnerFalloff/OuterFalloff` already
   exist from D-103): uphill = cut at 12 m, downhill = fill + **rail stamp** (a ridge 24 m high, 12 m inner
   falloff, 8 m crest, outer face to the ground); the **headwall stamp** around each hairpin's outside beyond the
   berm; the **notch** in each wedge; chutes as optional-line stamps with rails both sides; the valley run's
   12 m guide banks.
3. **Archetype rules** (`ArchetypeRules.SummitDescent`): walls 0 (the rails and headwalls are their own stamps,
   not the canyon's side terrain), bank scale 1.5, swells 0 on the switchbacks / 0.10 on the run, no launch crests
   on the switchbacks, one allowed on the run, `TubeChance` 0, `SpiralChance` 0, `LidChance` 0, `Floors` 1,
   `ExitSpread` 300; new fields `FaceSlopeMin/Max`, `LegAngleMin/Max`, `HairpinRadiusMin/Max`, `FanWidth`,
   `DescentTarget`, `RailHeight`, `HeadwallHeight`, `ChuteChance`. Constants in `WorldScale` under a `// Summit`
   block, pinned by the golden hashes.
4. **Validators** (`04 §12`), new for this archetype and run on every stage of it:
   - *holdable hairpins*: every primary vertex's corner limit ≥ the base cap,
   - *rail continuity*: sampled every 10 m along every primary edge (both sides), the ground at the level width
     plus the setback plus 6 m stands ≥ 20 m (rails) / ≥ 80 m (hairpin outsides) above the corridor, except at
     declared chute gaps and exit forks,
   - *grounded primary*: the base-cap profile has **zero launches** on the switchback section (the model's flight
     list is empty there); the ceiling's launches are reported per hairpin exit,
   - *descent budget*: 540–660 m from the summit pad to the last hairpin's exit; every tier 90–170 m,
   - *sightline* (§3e) per hairpin,
   - *chute* (§5): grounded free path; the half-charge landing inside the landing zone; the merge bend holds the
     arrival speed; the full-charge flight lands drivable,
   - *face drain*: the face outside every rail drains to the valley run at ≤ 0.40 with no NaN,
   - and the existing set unchanged: grade, grade delta, corner limits at both speeds, wall clearance, headroom,
     chainable line, exits, hash determinism.
5. **Regeneration** is bounded as on every archetype; a stage that fails the descent budget re-draws its tier
   parameters before it re-draws anything else.
6. **Selection:** `TerrainArchetype.SummitDescent` (index 4), `World › Archetype` = 4, `-- --summit`,
   `RUSHCORE_ARCHETYPE=summit`; a **sample stage 7 "summit"** on the seed that first passes with 5 tiers and a
   chute, pinned in `GoldenHashes`. The exit rotation (P-011) gains the fifth archetype automatically.
7. **Runtime dials** (rebuild on settle, like `Cell Size`; defaults pinned): `World › Summit Face Slope`,
   `World › Rail Height`. They exist so the playtest can move the two numbers most likely to be wrong without a
   build; a changed value is an override, never a default, until a D-number promotes it.

## 9. Delivery slices (each one PR, harness green, golden table updated in the same commit)

1. **S1 — the mountain.** Enum, rules, the switchback skeleton, the face, benches, rails, headwalls, berms, the
   valley run, exits; validators *holdable*, *rail continuity*, *grounded primary*, *descent budget*, *face drain*;
   the harness wall test (§3d); `--summit` and the sample; a first palette (height bands, no polish). Playable.
2. **S2 — the chute and the read.** Chutes with their validator, the notch and sightline validator, the hairpin
   boards and berm posts, the harness chute drive (free path grounded; the half-charge slam landing inside the
   zone). The playtest build is S1 + S2.
3. **S3 — the vista.** Summit cone, cirque walls, snowline palette, rail crest lines, fog end, scatter suit; the
   sample verdict; fold the accepted parts into `04 §6` and mark this file the record.

Gate: **G0 (Summit Descent)** per `08 §5`, plus the lines added there for this archetype, plus the user's sample.

## 10. What only the playtest can decide (FEEL / VALIDATE)

- **The tier rhythm:** ≈ 6 s at the cap, 4–6 tiers. Too few and it is a hill; too many and the hairpins are the
  stage. Lever: `FanWidth`, `LegAngle`, `HairpinRadius` ranges.
- **The hairpin at the ceiling** (§4): brake, or berm. If the berm ride reads as the right answer and the
  hard-impact threshold takes Flow for it, that threshold is a `03 §9` value and frozen under D-095: it would
  need the user's preset, not this archetype's rule.
- **Rail height vs vista:** 24 m rails at 83 m from the centreline cut 16° off the horizon from the ball. Whether
  the valley still reads from a bench, and whether the rail reads as a bank rather than a trench wall.
- **The blind hairpin:** whether the notch, the board and the berm posts are enough to commit to a 150° corner at
  the cap without seeing its exit.
- **The chute's commitment:** a 40 m gap at 148 m/s. Whether it is found, whether it is taken, whether the
  half-charge slam reads as the reward.
- **Fixed camera pitch on a 0.24 grade descending bank.**
- **Palette:** whether snow, scree and rock read as height at speed, and whether the rails' shadow face reads.

## 11. Deviations from the answers, and conflicts reported

- **The inside line is a chute, not a carve** (§1, §5). The user chose inside lines that "cut tighter and demand a
  carve". The frozen carve drifts wide by construction (understeer 0.459), so a tighter inside radius is not
  something a carve can hold; forcing a brake there would make the line a Flow loss. The chute keeps the answer's
  intent (an inside line through the hairpin with a Flow gain) with the verbs the kit has: a rail-gap commitment,
  a half-charge lip and a slam landing. The carve's Flow is earned on the primary's hairpin (§4). If the user wants
  a true tight inside carve, the lever is the carve's understeer, a D-095 baseline value.
- **Archetype ceiling.** `01` caps terrain archetypes at four; this is the fifth. The ceiling is raised to five in
  `01` with this decision (D-107) rather than replacing an archetype, because the user asked for a new type and the
  four delivered ones are in the exit rotation. Ceilings are ceilings, not quotas.
- **Blind corners.** `04 §10` keeps walls on the outside of bends until difficulty is reassessed after Phase 4. The
  Summit's hairpin insides are the mountain and cannot be otherwise; the sightline rule (§3e) is the archetype's
  declared exception, in the way modules declare theirs.
- **Tier count.** The question offered "ten to fourteen legs" for 600 m. With cap-holdable hairpins (arc ≥ 195 m)
  inside a 6 km route the honest number is 4–6 tiers. More hairpins would need either a longer stage (V-009) or
  hairpins below 80 m, which the user's first answer rules out for the primary.

## 12. Not in this design

No climbs on the primary (the summit start settles it); no mandatory chute; no tubes, lids or spiral pit in the
first cut (they are compatible and can come later as optional lines); no new movement verb, threshold or camera
rule; no difficulty escalation (parked until after Phase 4); no enemies (Phase 4 places them on every archetype at
once); no second primary floor; no retrofit of rails onto the other four archetypes until this one has its verdict.

## 13. Addendum: the wall ride (D-108, 2026-09-19)

Written after §3d. Walls are now ride surfaces: above 30 m/s any wall is ground, a fillet carries the ball up it
without loss, gravity brings it back or the top releases it. That changes what the Summit's walls are for:

- **Rails** (24 m) become the thing a leg is ridden on: a wide line goes up the rail and comes back, at no cost,
  and a deliberate line rides the rail through a bend. The rail's foot is a **30 m arc**, never a smoothstep (a
  smoothstep foot is a kink the facets turn into a hit); the rail's top is a convex lip a fast ball leaves, so a
  rail must be tall enough that a momentum ride never reaches the top: with the climb scrub (D-110, 45 m/s up the
  wall plus the scrubbed run-up: measured 20 m at 125 m/s head-on, about 30 m at the cap, near 50 m at the Flow ceiling) a wall 55 m above its foot
  contains every ride at every speed, so **rails go to 55 m** (was 30 m under the D-108 clip) and the harness
  measures it; a head-on hit scrubs its excess over tenths of a second and costs Flow once, which is the commitment.
- **Headwalls** (90 m) become the hairpin's wall ride: a ball that runs wide rides up the headwall, round the
  outside of the arc and back down onto the exit leg. The headwall's foot is the same 30 m arc above the berm.
  Whether the primary's hairpin should be *designed* to be ridden (Trackmania), and whether the ride costs the
  Flow that the brake would, is the sample's first question; the two-price rule in §4 gains a third path.
- **The chute's** rails are rideable too, which makes the 40 m gap less of a commitment than §5 says: a ball that
  clips the gap's edge rides it. Keep the gap at 40 m and read it in play.
- **Walls everywhere** (§3d) still holds as "never a fall"; the cost of a miss is now the ride, not the hit,
  except for a hit square on, which is still a hard impact.
- **The profile is delivered** (D-109, 04 §5D): rails and headwalls are `WallProfile` stamps, the rails' foot
  continuing the bench's edge and the headwalls' foot continuing the berm, so §3d's "near-vertical, 12 m falloff"
  reads as the 72° face of the profile; the Summit's own numbers are the heights.
