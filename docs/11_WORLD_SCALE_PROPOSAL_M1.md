# 11 — World-Scale Family Proposal (Gate M1)

**Status:** PROVISIONALLY ACCEPTED 2026-09-05 (D-082). The derived values in §3 are the generation inputs.
The five **FEEL** verdicts (fog onset, corridor minimum, monumental hill, committed ring, 27° ramp) and the
frame-time reading are deferred to the first generated-stage playtest (Phase 2, PR 2), where they are judged
on real terrain; the fog / far-plane change in §3h is not applied until then. V-009 is re-measured at S0.
**Owner of the inputs:** `docs/03 §15` (frozen movement baseline, D-078), `docs/10` (scale strip D-079,
terrain budget D-080, route speed model D-081).

## 0. What this decides, and what it does not

It decides the **generation inputs**: the world-scale family (feature wavelength/height, gap, ramp and
bend sizes, corridor width), the stage footprint derived from a target stage time, and the heightfield
cell size with its budget. It does not touch movement. Every number below is derived from the frozen
baseline and the strip measurements; where a number is a feel judgement rather than a derivation it is
marked **FEEL** and mapped to a strip station so the judgement is made on the ball, not on paper.

## 1. Inputs

| Input | Value | Source |
|---|---:|---|
| Ball diameter | 4.25 m | 03 §15 |
| Hard cap | 148.5 m/s (535 km/h) | 03 §15 |
| 0→cap, drive held, flat | **7.1 s / ≈ 580 m** (measured; the paper 5.3 s / 394 m ignored drag) | harness, D-081 |
| Drive vs gravity | 27.99 / 39.38 = 0.71 → a driven ball still accelerates up to a **45° grade** | 03 §15 |
| Turn radius at the cap | 100 m; corner speed limit v² = r·a_lat(v) | 03 §15, D-081 |
| Full-charge jump | 58.21 m/s up: 43 m, 2.96 s hang; range 177 / 296 / 351 / 439 m at 60 / 100 / 118.8 / 148.5 m/s | 03 §15 |
| Half-charge jump | 30.1 m/s up: 11.5 m, 1.53 s; range 92 / 153 / 182 / 227 m | 03 §15 |
| Landing burst | 118.8 m/s from any slam touchdown | D-077 |
| Crest contact | a crest launches below r = v²/g: 91 / 254 / 358 / 560 m at 60 / 100 / 118.8 / 148.5 m/s | 04 §8 |
| Cosine crest radius | λ² / (2π²H) | 04 §8 |
| Cell size vs contact | 4 m: 99% grounded on the 800/80 hill; 8 m: 86% | D-080 |
| Build model | ≈ 2 µs/sample warm, 4 B/sample heights, flat-shaded tiles 120 B/triangle (3 × 40 B) | D-080, code |

## 2. The governing finding: bends are the throttle, terrain is not

With the frozen baseline, **grade cannot slow a driven ball**. Drive (27.99 m/s²) exceeds the gravity
component of any slope under 45°, so the route speed model holds the cap across every hill on the strip:

| Cosine hills, W held, entering at the cap | Max slope | Minimum speed | Average over 4 km |
|---|---:|---:|---:|
| 100/10, 200/20, 400/40, 800/80 (H/λ = 0.10) | 17° | 148.5 | 145.0 m/s |
| 400/80, 800/160 (H/λ = 0.20) | 32° | 143–146 | 135 m/s |

What does set the base-kit speed on a route is, in order: the standing start (580 m), **bend radius**,
and deliberate stops (gaps, walls). The corner ladder from the steering envelope:

| Bend radius | 15 m | 25 m | 35 m | 50 m | 70 m | 100 m | ≥ 160 m |
|---|---:|---:|---:|---:|---:|---:|---:|
| Fastest speed through it | 51 | 68 | 81 | 99 | 120 | 148 | cap |
| 20 m lane change needs | 35 m | 45 m | 53 m | 63 m | 75 m | 89 m | 113 m+ |

So the family is organised around two ladders: a **bend-radius ladder** (speed control and line choice)
and a **crest-radius ladder** (contact versus launch). Hill height is then chosen for sightline and
monumentality, not for speed.

## 3. Proposed family

Units are metres; ball diameters (⌀ = 4.25 m) in brackets where it helps.

### 3a. Bends and banks

| Role | Radius | Speed it holds | Use |
|---|---:|---:|---|
| Cruise bend | ≥ 160 m | cap | primary route default; the strip's 160 / 240 m rings |
| Fast bend | 100 m | 148 | primary route; banked at the archetype's discretion |
| Committed bend | 50 m | 99 | primary route **minimum**; always banked (BankedTurn module) |
| Technical bend | 25 m | 68 | optional lines only |
| Hairpin | 15 m | 51 | never mandatory; challenge modules only |

Primary-route minimum 50 m (**FEEL**: turn-pad rings 80 / 160 / 240 m — which ring reads as "full
speed", which as "commit"). Bank berm height for a committed bend: 18 m at r 145 in the lab hairpin is
the accepted reference; scale linearly with radius (≈ 6 m at 50 m, 12 m at 100 m).

### 3b. Hills (Rolling Highlands base relief)

| Role | λ / H | Crest radius | Behaviour at the cap | Max slope |
|---|---:|---:|---|---:|
| Long swell | 1000–1600 / 40–80 | 630–1620 m | contact, rolling; the landscape's big shape and the sightline over it | 7–9° |
| Roller | 800 / 80 | 405 m | contact up to 126 m/s, launches at the cap (strip station verified) | 17° |
| Launch crest | 300–500 / 30–60 | 76–210 m | launches above 55–90 m/s; ≈ 200 m of flight from a 400/40 crest at the cap, landing near the trough | 17–20° |
| Micro relief (noise) | < 100 / **≤ 1** | ≥ 560 m | must never launch or hop a cruising ball | — |
| Dune wave (Dune Sea, D-099) | 350–500 / 21–43 (0.06–0.085 λ) | 220–300 m | launches above 93–108 m/s; at the cap the flight lands on the same dune's downslope near the trough, 55–60 m/s down; the corridor rides it only on train straights | 11–15° |

The micro-relief bound is the concrete form of 04 §5C ("noise cannot erase readability"): to keep crest
radius ≥ 560 m, H ≤ λ² / 11 054, i.e. 0.9 m at λ 100, 3.6 m at λ 200, 0.23 m at λ 50. Below 100 m
wavelength noise is decoration only.

### 3c. Gaps

| Role | Opening | Depth | Entry speed the model must show | Basis |
|---|---:|---:|---:|---|
| Mandatory gap | 40–120 m | 12–20 m | ≥ 60 m/s at the rim | ≤ 0.7 × full-charge range at 60 m/s (177 m); crossable half-charged from 80 m/s |
| Optional gap | 160–320 m | 20–30 m | ≥ 100 m/s or a burst | full-charge range 296 m at 100, 351 m at burst speed |
| Set-piece gap | up to 400 m | 30 m+ | cap or boost | full-charge range 439 m at the cap |

Landing zone after any mandatory gap: ≥ 200 m long past the far rim (the half-to-full charge range
spread at 100 m/s is 153 → 296 m) and at least the corridor width. Take-off rims flat for ≥ 150 m
(the strip's runways); exit walls ≤ 25° so a short landing recovers.

*Delivered 2026-09-06 (D-097):* the exit wall is as gentle as the opening allows (the depth is trimmed
so it never exceeds 25°), because a ball riding it out leaves the far rim like a ramp: at the base cap
that flight is about 300 m, at the ceiling over a kilometre, so the straight past a gap is sized for the
base kit's full-charge flight at the cap over falling ground (≈ 850 m) and for the ceiling ball's slam
landing off the far rim; module straights therefore run about 1 km. Rims sit on the sloped corridor
profile and the validator reads their real heights.

### 3d. Ramps

Strip lips (rise 20 m; slopes 0.20 / 0.35 / 0.50 = 11.3° / 19.3° / 26.6°; lengths 100 / 57 / 40 m):

| Ramp | Ballistic range off the lip, no jump, at 100 m/s / cap | With a full charge |
|---|---:|---|
| 11° | 98 / 216 m | jump raises takeoff to 58 m/s: ≈ 290 / 430 m — the charge matters here |
| 19° | 158 / 350 m | adds up to 25 m/s of vertical at 100 m/s; nothing at the cap |
| 27° | 203 / 449 m | **nothing**: the ramp already exceeds 58 m/s of vertical above ≈ 23° at the cap |

Proposal: 11° ramps on the primary route (route-friendly, charge-rewarding), 19° in LaunchRamp modules,
27° as rare set-pieces. Rise 20 m as the standard lip; half-size 10 m lips for micro launches.

### 3e. Corridor width

| Width | ⌀ | Role |
|---|---:|---|
| 300 m | 70 | open highlands: the default primary region |
| 150 m | 35 | typical primary corridor between features |
| **75 m** | 18 | primary-route **minimum** (canyon floors, bridges, between walls) |
| 40 m | 9 | optional technical lines only |

Derivation: at the cap with a 3 s read horizon (450 m) a 75 m corridor tolerates a 4.4° heading error,
a 40 m corridor 2.3°. **FEEL**: the corridor station (300 → 40 m at 1000–2000 m) is the direct test —
the minimum is whichever width the user can hold at the cap without dread.

### 3f. Stage footprint from the stage-time target (V-009)

Base-kit time from a standing start over rolling λ 400 / H 40 relief with the bend mix as the variable
(route speed model, drive held, no boost, no burst):

| Primary route | no bends / 4 × r100 | 4 × r50 | 4 × r50 + 4 × r25 | 8 × r35 |
|---|---:|---:|---:|---:|
| 4 km | 31 s | 35 s | 40 s | 40 s |
| **6 km** | 45 s | 48 s | **54 s** | 54 s |
| 8 km | 59 s | 62 s | 67 s | 68 s |
| 10 km | 73 s | 76 s | 81 s | 82 s |

Proposal: **target 60 s of base-kit traversal per stage** (provisional V-009; a real player with
combat, detours and mistakes lands at 90–120 s, which puts a nine-stage run at 15–20 min before shop
and route screens). That gives a **6 km primary route**. With the route meandering within ±600 m of
the stage axis plus a 400 m scenery margin each side, the footprint is **6.0 × 2.0 km**, entry at one
end, exit at the other. Optional lines live inside that band. Measured clear time at Gate S0 replaces
this target (08 §7); the footprint is not resized before then.

### 3g. Cell size

**4 m**, everywhere (one logical height source, one collider). Evidence: 99% contact against 86% at 8 m
on the gentlest strip hills; the facet kink at 4 m is 0.4° on the smallest cruise crest (r 560) and
2.3° on a 100 m launch crest, both below the ball's contact tolerance in the measurement. 8 m is
rejected on contact, not on triangles. 6 m was not measured; it is the fallback if memory (3h) forces it.
*Amended 2026-09-06 (D-092): the analytic ground follow removed contact from this decision (100% raw contact
at 4, 8 and 16 m on the same hill); 4 m stands on silhouette and validator resolution, to be re-decided on
draw cost with the ceiling addendum and the LOD work.*

### 3h. Terrain budget at the proposed scale

| 6.0 × 2.0 km | 4 m cells | 8 m cells (rejected) |
|---|---:|---:|
| Samples | 750 k | 188 k |
| Triangles | 1.50 M | 0.38 M |
| Build, warm (2 µs/sample model; strip measured 500 ms for 258 k) | ≈ 1.5 s | ≈ 0.4 s |
| Heights (collision + source) | 3.0 MB | 0.8 MB |
| Render tiles (128 cells) | 48 | 12 |
| Flat-shaded vertex arrays, 120 B/tri, CPU copy + GPU copy | **≈ 180 MB each** | ≈ 45 MB |

Build time 1.5 s at stage load is acceptable for the MVP (stage transition is instrumented, 08 §10).
The one number that needs a measurement is the vertex-array footprint. Two mitigations, neither
needed until measured: (1) index the tile mesh (one vertex per sample) and derive flat normals in the
terrain shader from screen-space derivatives — ≈ 6× smaller, same look; (2) 6 m cells — 2.25× smaller,
contact unmeasured.

**Draw distance vs sightline.** At the cap the player needs ≈ 3 s of readable terrain to steer
(450 m) and ≈ 4 s to commit to a gap or ramp (600 m: 0.45 s charge plus a 300–440 m flight). The
current defaults (`World › Fog End` 2400 m → fog begins at 384 m; `Camera › Far Plane` 8000 m) put the
fog onset **inside** the steering horizon and draw the whole stage. Proposal: Fog End 4000 m (onset
640 m), Far Plane 5000 m, so distant tiles cull and the read horizon is clear. Both are live sliders
to be judged on the strip's 500 m gantries and the hill stations (**FEEL**).

**Tiling.** 48 tiles of 512 m at the proposed scale; frustum culling at that granularity is adequate.
Finer tiling (64 cells) is only worth it if the frame-time reading below is draw-bound; it costs no
build time.

**User's reading (required before acceptance):** frame time at the cap on the strip with F2 open,
defaults (4 m cells, Fog End 2400, Far Plane 8000): `____ ms` on the runway, `____ ms` over the
800/80 hills. With Far Plane 5000 / Fog End 4000: `____ ms`.

## 4. Strip checklist before acceptance

| Station | Question | Sets |
|---|---|---|
| Runway 0–1000 | does 580 m to the cap read as a launch, not a wait? | (confirms D-081; no change possible without D-078) |
| Corridor 1000–2000 | narrowest width holdable at the cap without dread | 3e minimum (75 m proposed) |
| Hills 2000–4200 | which station reads "monumental" and which "bump"; does the 800/80 launch at the cap feel intended? | 3b long swell / roller / launch crest |
| Gaps 4200–5080 | which gap is a "yes" at 60 m/s half-charged; which needs the burst? | 3c mandatory / optional |
| Ramps 5080–5700 | is the 27° lip a set-piece or too much? | 3d |
| Turn pad 5700–6300 | which ring is "full speed", which "commit"? | 3a |
| Whole strip at the cap, F2 | frame time; fog onset at 384 m acceptable or not | 3h |

## 5. On acceptance

- `DECISIONS.md`: one entry freezing the family (values from §3, with the user's edits); V-005, V-008,
  V-009 resolved provisionally (V-009 re-measured at S0).
- `04 §8`: references this document as the frozen scale; the micro-relief bound becomes a §5C rule.
- Code: one static `WorldScale` constants class in `src/Generation/` created with the terrain core
  (not before), the single home for these numbers; the tuning panel does not expose them.
- Runbook: the fog/far-plane defaults change only if the user accepts 3h's proposal (D-078 does not
  cover them, but the toy's "0 override values" launch state must stay true).

## 6. Ceiling addendum at the D-091 baseline (D-094)

Written 2026-09-06 after the baseline lock (steering 222.66 rising ×1.45, max jump 84.63, Flow
headroom 0.715 → ceiling 254.7 m/s, ball 0.66 m). The family's radii and sizes above stand; what
changed is which speed each one holds, and every safety figure now has a second, ceiling value.
Generation computes the live numbers from tuning (`RouteSpeedModel` with a headroom); `WorldScale`
carries the reference constants.

### 6a. Bend ladder re-derived

| Bend radius | 15 m | 25 m | 35 m | 50 m | 70 m | 100 m | 160 m | 201 m |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| D-082 (steering 151.25) | 51 | 68 | 81 | 99 | 120 | 148 | cap | cap |
| **D-091 (steering 222.66)** | 63 | 84 | 101 | 124 | 151 | 187 | 250 | **254.7 = ceiling** |

Steering saturates at the base cap (a_lat = 322.9 m/s²), so 68 m holds the base cap and 201 m holds
the ceiling (`WorldScale.CeilingBendRadius`). Consequence for §2: at D-091 bends throttle a base-kit
ball only below 68 m; the committed 50 m bend holds 124 m/s (was 99), so on the primary route the
throttle is now the launch crests and their landings, and bends price the *ceiling* player instead
(a 160 m cruise bend costs 5 m/s of Flow headroom, a 100 m bend 68 m/s). Bank heights unchanged.

### 6b. Crest ladder and jump envelope at the ceiling

| | 60 m/s | 100 m/s | 148.5 (base cap) | 254.7 (ceiling) |
|---|---:|---:|---:|---:|
| Minimum crest radius to stay grounded (v²/g) | 91 m | 254 m | 560 m | **1647 m** |
| Full-charge jump range (84.63 m/s up, 4.30 s hang, 91 m high) | 258 m | 430 m | 638 m | 1094 m |
| Half-charge jump range (43.3 m/s up, 2.20 s hang, 24 m high) | 132 m | 220 m | 327 m | 560 m |
| 20 m lane change needs | — | — | 52 m | 90 m |

Every long swell (crest radius 630–1620 m) launches a ceiling ball; the roller (405 m) and the launch
crests (76–210 m) launch it far earlier on their up-slopes. A ceiling flight off a swell is long
(the strip's 800/80 roller at 254.7 m/s flies 317 m and lands 67 m/s down) and its landing is hard: the route speed model's airborne phase (D-094) reports the
landing vertical speed, and any landing at or above the plain-landing threshold (30 m/s) costs Flow
unless it is a slam. That is the intended loop: the ceiling is held by slamming every landing, and a
missed slam prices the chain back down without ever dropping the ball below the base cap's reach.

### 6c. Gaps and ramps at the new jump

The full-charge range at 60 m/s grew from 177 to 258 m, so the mandatory gap band (40–120 m, ≤ 0.7 ×
range at 60 m/s) is now crossable half-charged from 55 m/s (132 m). Optional gaps (160–320 m) fit a
full charge from 100 m/s (430 m) or a half charge from the cap (327 m); set-piece gaps up to 400 m a
full charge from 100 m/s. The 27° ramp still adds nothing at the cap; the 11° ramp's charge reward is
now larger (lip speed 84.6 m/s of vertical from a full charge against 27 m/s from the ramp alone).
Landing zones: 200 m past the far rim still covers the half-to-full spread at 100 m/s (220 → 430 m
minus the gap itself) for the mandatory band; the 300 m spread at the cap is why optional gaps stay
optional.

### 6d. Sightline and corridor at the ceiling

Fog end 2400 m is 9.4 s at the ceiling (16 s at the base cap); the 3 s read horizon becomes 764 m,
which a 150 m corridor tolerates at a 5.6° heading error and the 75 m minimum at 2.8°. The
corridor width verdict stays a feel question for Gate F0 with Flow on. The camera's look-ahead and
framing bands do not change with speed above the base cap; the lens only widens (D-088).

### 6e. What the harness measures (08 §4 addendum)

Every run: the ceiling model's corner limits and saturation; a cliff flight against closed-form
ballistics; the turn radius at the ceiling on the turn pad (204 m measured against 201 m predicted (28.1° over 100 m at 254 m/s)); the hill stations at the
ceiling, flights and landings against the model over the same centreline (four flights in both, real [2048→2757 m, 53 m/s down → 198 m/s] [2777→3132, 41 → 166] [3151→3523, 53 → 134] [3735→4052, 67 → 151] against model [2029→2811, 56 → 212] [2815→3102, 47 → 181] [3148→3601, 52 → 141] [3763→4128, 72 → 153]: the ball keeps 7–9% less speed through each landing than the tangent rule and leaves the next facet later and lower, so the model's chained flights run up to 27% long (the safe direction for the validators), landing vertical speeds within 15%); on the
generated stage the base-kit flights at each launch crest against the ball (ball 2456→2752 m, model 2457→2760 m (3%), landing 50 m/s down → 138 m/s); and over
the 100-seed batch the two-speed figures (100 seeds: 100 valid, 0 fallbacks; seconds below the base cap 11.0 s average; 2.4 ceiling flights per stage, 3.7 s airborne; widest opportunity gap 1516 m against the 1528 m window).

## 7. Vertical ladder at the D-091 baseline (D-096)

Written 2026-09-06 for the vertical grammar (04 §5I). The family above is unchanged; this section adds
the sizes that walls, floors, tubes and lids need, derived from the same frozen inputs (g 39.39, full
charge 84.63 m/s, half charge 43.3 m/s, base cap 148.5, ceiling 254.7, ball 0.66 m, rig 18.8 m at
−20.6°). Every size is provisional until measured (V-016). The reach rows follow the controller's
takeoff rule (03 §6, D-012): the release sets the vertical speed to max(current vertical, charge
takeoff), so a ramp adds nothing to a charged jump until its own vertical exceeds the charge; the
lip's rise (20 m) does add. Measured on the strip ramps before any floor is placed.

### 7a. Reach: how far up one move goes

| Move | Vertical takeoff | Apex above the lip |
|---|---:|---:|
| Full charge, flat or any ramp ≤ 27° at the base cap | 84.6 m/s | 91 m |
| Half charge, same | 43.3 m/s | 24 m |
| 19° ramp at the ceiling, charged or not | 84.2 m/s | 90 m |
| 27° ramp at the ceiling, charged or not | 114 m/s | 165 m |

**Jump step band 60–90 m.** A full charge lifts the ball 91 m above the lip at any speed, so a floor
step taken by a jump is 60–90 m (plus the lip); a mandatory step is at most 20 m (half charge, 04
§11), which is a lip rather than a floor. Larger lifts are not jumps: a driven ball climbs any grade up
to 0.71 (35°, §2) without losing speed, so spiral ramps and tubes (whose walls carry the ball at any
pitch) link floors of any spacing, and three floors put the top 120–270 m up. A midair tube mouth is
placed inside the reachable set from its runway at the arrival speed, with the mouth radius as the aim
tolerance; the hang time at 84.6 m/s of vertical is 4.3 s, so the horizontal reach at the lip's height
is 640 m at the base cap and never binds.

### 7b. Falls

| Drop | Landing vertical speed |
|---|---:|
| 100 m | 89 m/s |
| 200 m | 126 m/s |
| 300 m | 154 m/s |

Every floor fall lands above the 30 m/s plain-landing loss and sheds more than 20 m/s in a tick: a
slam keeps the Flow, anything else pays it (03 §9). The drain under an edge is drivable ground at or
below the route grade limit with no wall foot and no NaN, and the kill plane stays at the stage
minimum minus 120 m, so a fall is never a restore.

### 7c. Walls, slots and spirals

- A wall of height H across one 4 m cell is a face of atan(H / 4): 100 m reads as 88°. Two cells is
  the readable minimum for the low-poly silhouette.
- Wall margin: 2 cells (8 m) outside the corridor's level width, so the ground follow's ±1 cell lateral
  sample never reads the face. The 150 m corridor already leaves 75 m; slots at the 75 m minimum are
  the case to measure. *Delivered (D-098):* `WorldScale.WallSetback` 8 m; Canyon Run walls 60–120 m over a
  12 m falloff (about 80°), the inside of every bend over 120 m.
- Sightline: a wall on the inside of a bend hides the 445 m (base cap) / 764 m (ceiling) read horizon.
  Walls sit on the outside of bends until difficulty is reassessed after Phase 4.
- Spiral pit / ramp: successive turns differ in radius by Δr ≥ 200 m (150 m corridor plus both wall
  margins and the face). Turns from r 800 down to r 200 hold the ceiling everywhere (201 m); the 50 m
  committed radius appears only as the top turn of a spiral ramp. A drop of 100–200 m per turn at r 200
  is a grade ≤ 0.16, far inside the 0.40 route limit.

### 7d. Tubes

| Quantity | Provisional value |
|---|---:|
| Tube radius | 4–8 m |
| Mouth flare | ×2 over one tube diameter |
| Rib spacing / thickness | 25 m / ≤ 0.5 m |
| Path radius | ≥ 3 × tube radius (the sweep never self-intersects) |
| Tube length | 150–800 m |
| Axis clearance above ground and from walls | ≥ 30 m except at the mouths |

Wall ride: inside a bend of path radius R the ball rides to tan φ = v² / (g·R).

| Path radius at the base cap | 100 m | 300 m | 600 m |
|---|---:|---:|---:|
| Ride angle | 80° | 62° | 43° |

The ground-normal limit (0.498, about 60°) is crossed below a path radius of about 320 m at the base
cap (and about 900 m at the ceiling), so the ball inside a tight tube bend reads airborne to the
controller (V-015). A tube bend loses no speed in the route speed model (carried segment); the exit
velocity runs along the axis into the landing zone. *Delivered (D-101):* the tube follow makes the wall ground
at any angle (03 §3), so the limit no longer applies inside tubes; the swing is an S of 200 m over 500 m
(r ≈ 270, a 64° ride at the base cap), the primary's own bends under the section push the worst ride to about
80°, and the ride is reported per tube. Radius 6 m, mouth offset 45 m, climb pitch ≤ 14°, cruise clearance 30 m,
section 1 800 m at the lowest cruise.

### 7e. Camera clearance

*Delivered (D-101):* the lens is pushed radially to R + 1.5 m from the nearest axis point after the chase
placement (×2 at the flared mouths), the shell is on a physics layer the occlusion probe ignores, and the harness
holds the lens outside with a clear line of sight for a whole ride. The original derivation follows.

The rig sits 17.6 m behind and 6.7 m above the ball. Inside a tube of radius R with the ball on the
floor, the lens must clear the top of the shell by the occlusion margin, 2R + 0.6 m above the floor:
at R 6 that is a radial push of about 5 m. The push is applied after the chase placement along the
radial from the tube axis through the lens, so when the ball rides a wall or the ceiling the lens
follows around the outside. A tube axis therefore needs 2R + 7 m plus margin of clear space on every
side; 30 m covers R 8. Lid tunnels use the confined framing instead: clearance ≥ 15 m above the ball,
width ≥ 75 m (the minimum corridor), length ≤ 300 m provisional.
