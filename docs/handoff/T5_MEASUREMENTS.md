# T5 — Generation instruments: wall probe, tube ride, floor-3 room, landing run

**Branch:** `opus/t5-measurements` off `develop-secondary`  
**Phase:** 3 follow-up (the open gaps recorded in D-100..D-103); data only  
**Gate lines served:** 08 §5 (G0 measurements), 04 "Empirical validation items"  
**Owning specs:** 04 §5A, §5E, §5I, §6, §12 (Secondary, Route speed model), the "Empirical validation items" list; 08 §5, §11; 11 §7; DECISIONS D-100, D-101, D-103, V-015, V-016

## Goal

Four measurements the main track wants before it changes any rule. Each is an instrument behind an env knob, a
harness print, and a table in this packet's Delivery record. **No validator verdict, builder rule, tolerance or
`WorldScale` number changes in this task.** Recommendations go in the Delivery record as proposals.

## Current state

- `RUSHCORE_BATCH_FAILS=1` (`tests/MovementToySelfTest.cs` line ~1922) tallies attempt-1 rejection reasons per
  archetype batch with the first failing seed's features and bends. `RUSHCORE_LINE_TRACE=1`, `RUSHCORE_DRIVE_TRACE=<m>`
  are the other instruments (docs/10).
- Wall clearance check: `StageGenerator` line ~574–590, "wall faces stay outside the corridor's level width plus the
  setback": samples the stamp weight at the level-width reach along the route; the lowest weight must be ≥ 0.99. About
  4% of attempt-1 rejections on tight bends are believed to be probe misreads (the sample lands on the falloff of the
  bend's own bank, not a wall), unverified.
- Tube ride: `TubeDefinition.MaxRideDegrees`; `StageGenerator` line ~311–355 reports "wall ride ≤ N°" per tube;
  `TubeBuilder.LateralEnvelope` and the swing rule at line ~110. Over primary bends the ride reaches about 85°.
- Floor 3: `OptionalLineBuilder` placement tries tier 3 (floor 3 stacked on a floor 2, `LineShape` Floor3: offset
  300, transition 360, ramp 600, `Length = 2T + 2Ramp + 80`) then tier 2. **Refreshed after D-105** (branching exits
  claim their spans first, so the floors compete with them): floor 3 now lands on about 1% of Sky seeds (1 to 4 per
  100, against 3 per 100 before), and tubes fell with it — Highlands 22 to 12, Dune Sea 44 to 25, Sky 63 to 36 per 100
  seeds. Take the current numbers from a fresh batch run rather than from this packet. D-103 names "branching floor 3
  from floor 2" as the candidate, and it matters more now that the sections are contested.
- Landing run: the route speed model reserves a 100 m landing run after every flight (D-094, D-100 "landing speed after
  brake shed"); the real ball keeps 7–9% less through chained landings (D-094 measured on the strip); the gap between
  the 100 m reservation and the real braking distance is recorded as a known non-blocking item.

## Instruments (all under `RUSHCORE_MEASURE=1`, data-only unless stated)

1. **Wall-probe classification.** In the batch case, for every attempt-1 rejection by the wall check, re-sample the
   height field around the failing probe point at 1 m spacing over ±12 m across the route and classify: `misread`
   if the finest sample along the level-width reach is ≥ 0.99 weight (the coarse probe hit a bank falloff),
   `intrusion` otherwise. Bucket by the bend radius at that route distance (straight, r ≥ 160, 100–160, < 100). Print
   one table per archetype: rejections, misreads, intrusions, share of all attempt-1 rejections. Also print the mean
   extra attempts those seeds cost.
2. **Tube ride source.** For every tube in the four batches: `MaxRideDegrees`, the tightest primary bend radius whose
   arc the swing overlaps in plan, the lateral offset at the ride maximum, and whether the maximum occurs on the swing
   term, the climb, or the cruise. Histogram ride angle in 10° bins; count > 85°. Print the three worst seeds with the
   numbers. **Read T7 and T8 first**: the ride angle is already known to reach 179° under a sustained lean, and the
   dominant imprecision in the tube ride is the structure query answering with the nearest axis *sample* (measured 1.4
   cm on a Dune Sea tube, 3.5 cm on Highlands, 21 cm on a steeply climbing Sky one). Do not re-derive those; measure
   what they leave open, which is where the ride angle *comes from* in the builder's own terms.
3. **Floor-3 room.** On the Sky batch: for each floor-2 section, whether a floor 3 could branch from it under the
   existing `LineShape` numbers (remaining primary-straight length after the floor-2 transition ≥ the floor-3 length,
   side valid, no feature overlap) and how many seeds would gain a floor 3 that way, against the current 3%. Use
   `OptionalLineBuilder.JoinsOnStraights`, `SideValid`, `OverlapsFeature` read-only; do not build anything.
4. **Landing run vs braking.** In the stage drive case (`RUSHCORE_DRIVE_TRACE` machinery), after every real flight
   landing record the route metres until the ball is within 2% of the model's speed at that distance, the landing
   vertical speed, and whether it was a slam. Print per landing and the mean, on Highlands seed 8 (gap + ramp) and
   Dunes seed 1 (train). Compare against the 100 m reservation.

Each instrument prints under a `[MEASURE]` prefix so the output can be grepped. None of them adds a `Check`; the
existing harness count must not change under `RUSHCORE_MEASURE=1`.

## Deliverable

- The four tables in the Delivery record below, with the seeds behind the worst cases. Every "current" figure quoted in
  this packet predates D-105 unless it says otherwise; re-measure before comparing against it.
- One recommendation per instrument, each phrased as a proposal for the main track (for example: "refine the wall probe
  over ±12 m at 1 m when the coarse weight < 0.99 and the bend radius < 160; would recover N% of attempt-1
  rejections"), with an estimate of what would change in which file. No code for the proposals.
- Runbook lines for `RUSHCORE_MEASURE=1` in docs/10; a line under 04 "Empirical validation items" per measurement
  saying it is measured, with the date and the packet.

## Non-goals

- Changing a rule, a tolerance, a number, a builder or a validator verdict. Adding regression seeds (that is a fix's
  job). Touching the route speed model.

## Acceptance (harness)

- Default run count unchanged. `RUSHCORE_MEASURE=1 RUSHCORE_SELFTEST_DATA_ONLY=1` prints the first three tables;
  `RUSHCORE_MEASURE=1` with `--seed 8` and with `RUSHCORE_ARCHETYPE=dunes --seed 1` prints the fourth.
- Instruments add under 10 s to the data-only run (state the time).

## Files expected

`tests/MovementToySelfTest.cs`, small read-only reporting helpers in `src/Generation/StageGenerator.cs` or
`OptionalLineBuilder.cs` (public read-only accessors only; no behaviour), docs 04 (validation items), 10, this packet,
`STATUS.md`.

## Stop conditions

Stop and write "Needs main track" if a measurement needs a builder to expose data it discards (say what), or if an
instrument shows an existing check is wrong (report; do not fix).

## Delivery record (filled by the implementing agent)

- **Branch / commits:** `opus/t5-measurements` off `develop-secondary` (84d3aa2, with T1–T4 and the tube fixes merged).
- **Harness (default count; data-only time with and without `RUSHCORE_MEASURE`):** **386/386 default (110.6 s)**, golden hashes unchanged.
  Data-only **44 s with `RUSHCORE_MEASURE=1` and 44 s without** — the instruments cost nothing measurable, against the
  packet's 10 s allowance. The instruments add
  **no check**: the count is identical with and without the knob, on the data-only run (93/93 both) and on the full
  one (386/386 both).
- **Files changed:** `src/Generation/StageGenerator.cs` (two read-only readings, `MeasureWallProbe` and
  `MeasureTubeRide`; no behaviour), `src/Generation/OptionalLineBuilder.cs` (`Floor3CouldBranch`, `Floor3Length`;
  read-only), `tests/MovementToySelfTest.cs` (the four instruments and their tables), docs 04 (Empirical validation
  items 7–10), 10 (three runbook lines), this packet, `STATUS.md`.

### Table 1 — wall probe

Per archetype, 100 seeds each. "Rejections" counts attempt-1 builds the wall check rejected; the bucket is the bend
radius at the failing probe vertex.

| archetype | wall rejections | of all attempt-1 rejections | seeds | extra attempts each | bucket | misread | intrusion | mean m of the 24 m window under 0.99 | mean ground rise |
|---|---|---|---|---|---|---|---|---|---|
| highlands | 0 | 0 % of 5 | 0 | — | — | — | — | — | — |
| canyon | 4 | 50 % of 8 | 4 | 1.0 | straight ×4 | 0 | **4** | 15.8 m | **82.9 m** |
| dunes | 0 | 0 % of 0 | 0 | — | — | — | — | — | — |
| sky | 2 | 17 % of 12 | 2 | 1.5 | straight ×4 | **2** | 0 | 14.0 m | 0.6 m |

Worst canyon case, seed 10/2 at vertex 950 (straight): coarse weight 0.834, corridor interior never below 1.000,
18 m of the window under 0.99, ground rises 92.8 m, nearest optional line 87 m away. Its profile across the route
(weight / height above the route): `0m w1.00/+0 · 20m w1.00/+0 · 40m w1.00/+0 · 60m w1.00/+0 · 80m w0.94/+0 ·
100m w0.00/+93 · 120m w0.00/+92`. A wall face, squarely.

Worst sky case, seed 4/5 at vertex 1575 (straight): coarse weight 0.966, ground rises 1.1 m, nearest optional line
**337 m** away. Profile: `… 80m w1.00/+0 · 100m w0.87/+1 · 120m w0.69/+0`. Flat ground, nothing near it, and the
primary's own stamp simply does not reach the level width plus the setback there.

**Three things the packet's stated belief got wrong.** It expected "about 4% of attempt-1 rejections on tight bends"
to be misreads. Measured: (a) the wall check is a far bigger share of rejections than 4% where it fires at all —
half of Canyon's, a sixth of Sky's — and zero on the other two archetypes; (b) **not one** of the six rejections was
at a bend; every one was on a straight, so the "sample lands on the falloff of the bend's own bank" hypothesis has no
instances in 400 seeds; (c) weight alone cannot classify these, because a stamp always fades outward and a fine
re-sample inward therefore always finds full weight — the first cut of this instrument said "6 of 6 misreads" on
exactly that artefact before the terrain was read instead.

### Table 2 — tube ride

74 tubes over the four batches. The maximum is the shipped `tan φ = v²κ/g` over the axis's plan curvature; the phase
is which term of `TubeBuilder`'s envelope is running where the maximum falls.

| archetype | tubes | 50–59° | 70–79° | 80–89° | > 85° | where the maximum sits |
|---|---|---|---|---|---|---|
| highlands | 12 | 0 | 3 | 9 | 6 | swing out ×8 (88°), climb ×4 (84°) |
| canyon | 1 | 0 | 0 | 1 | 0 | swing out ×1 (83°) |
| dunes | 25 | 6 | 6 | 13 | 5 | swing out ×14 (88°), climb ×6 (84°), swing back ×3 (86°), descent ×2 (78°) |
| sky | 36 | 0 | 5 | 31 | 4 | swing out ×22 (86°), swing back ×7 (87°), climb ×5 (84°), descent ×2 (84°) |

Nothing reached 90°. Three worst:

- **88° highlands 10/3** — swing out at 486 m of 1799 m, lateral offset 92 m, 149 m/s, tightest primary bend r160 m.
- **88° dunes 8/3** — swing out at 485 m of 1799 m, lateral offset 92 m, 149 m/s, tightest primary bend r160 m.
- **87° sky 5/3** — swing back at 1470 m of 1802 m, lateral offset 47 m, 149 m/s, tightest primary bend r100 m.

**The answer to the question the packet asked.** The ride angle is the builder's, not the route's: 55 of 74 maxima
fall on the lateral swing (45 out, 10 back) and only 15 on the climb, and the worst cases sit in sections whose
tightest primary bend is a wide r160. Every maximum is taken at the cap, so the angle is `v²κ/g` at 149 m/s with κ
coming from the swing's own S. The primary's bends are not what puts a ball 88° up a tube wall; `TubeSwingLength`
against `TubeLateralOffset` is.

### Table 3 — floor-3 room (Sky Terraces, 100 seeds)

| | count |
|---|---|
| floor-2 sections | 95 on 80 seeds |
| could carry a floor 3 branching off them | **4** (4%) |
| seeds that would gain one | **4** of 100 |
| seeds carrying floor 3 today | 4 |
| floor-3 length needed | 2000 m |
| mean room where it fits | 4248 m |
| blocked: a transition crosses a bend | 39 |
| blocked: inside of a bend on the floor 2's side | 32 |
| blocked: not enough room after the floor-2 transition | 20 |

**Branching from floor 2 as the packet specifies it gains nothing** — the same four seeds, because the criteria are
still applied against the *primary*: a 2000 m floor-3 span with 360 m transitions at both ends has to find 2 km of
primary with straights at both ends, and on a 6.5 km route with ten bends it rarely does. Room itself is never the
problem (4248 m mean against a 2000 m need).

### Table 4 — landing run

Every real flight (30 m of route or more in the air) on the two named seeds. "Metres" is route distance from
touchdown until the ball is back within 2% of the model's speed there.

| seed | at | metres | landed | slam |
|---|---|---|---|---|
| highlands 8/0 | 2483 m | 12 m | 18 m/s down | no |
| highlands 8/0 | 2687 m | 0 m | 30 m/s down | no |
| dunes 1/0 | 1391 m | 8 m | 42 m/s down | no |
| dunes 1/0 | 1739 m | 0 m | 47 m/s down | no |
| dunes 1/0 | 4294 m | 8 m | 43 m/s down | no |
| dunes 1/0 | 4666 m | 0 m | 46 m/s down | no |
| dunes 1/0 | 5042 m | 0 m | 47 m/s down | no |

Highlands seed 8: 2 landings, mean 6 m, worst 12 m. Dune Sea seed 1: 5 landings, mean 3 m, worst 8 m. **0 of 7 over
the model's 100 m reservation**, and none within an order of magnitude of it.

Read this carefully: what it says is that the speed model already predicts the landing loss, so the ball is back on
the model's number almost immediately. It does **not** say the 100 m reservation is too big, because the reservation
buys geometric room — somewhere flat to come down and recover the line — which this instrument does not measure. The
recorded 7–9% retention gap (D-094) is about chained landings under a slam; the harness follower never slams, so
every landing here is a plain one and the slam column is empty throughout.

### Recommendations (one per instrument, as proposals for the main track)

1. **Wall probe — do not "refine the probe"; split the check.** The suspected fix (a fine re-sample on tight bends)
   would recover nothing: there are no bend cases. What the data shows instead is one sound check and one broken one
   wearing the same name. On Canyon the check is doing its job — a 93 m wall face at 100 m from the centreline is
   exactly what "wall faces stay outside the corridor's level width plus the setback" exists to catch, and rejecting
   those two attempts costs one extra attempt each, which is cheap and correct. On Sky it fires on flat ground with
   nothing within 337 m. Proposal: keep the weight test as the wall test where `field.WallHeight > 0`, and on
   archetypes without walls either drop it or replace it with a terrain test (`Sample` across the reach against the
   route height). **File:** `StageGenerator.cs` around line 613–630, about ten lines. **Recovers:** 2 attempt-1
   rejections in 400 seeds, so the value is correctness, not throughput.
2. **Tube ride — the swing is the lever, not the route.** 55 of 74 maxima are on the lateral swing at the cap.
   Proposal: if 85° is to be a ceiling rather than a report, lengthen `WorldScale.TubeSwingLength` (the swing's κ
   falls as 1/L²) or shorten `TubeLateralOffset`, and re-measure with this instrument; changing which bends a tube
   may sit near would not help, since the worst cases are in r160 sections. **File:** `WorldScale.cs` — which is a
   number this track may not touch, hence a proposal only. **Note:** every stage hash moves with it.
3. **Floor 3 — shorten the shape, or give a line its own segment kinds.** Branching from floor 2 changes nothing
   while the criteria are read off the primary. The honest version of D-103's candidate is to test the floor-3
   transitions against the **floor 2's own geometry**, which is offset and gentler than the primary it shadows — and
   that cannot be done today, see "Needs main track" below. The alternative that needs no new data is to shorten
   `Floor3Transition` (360 m) and `Floor3Ramp` (600 m): the shape needs 2000 m and the blockers are both about
   fitting that length between bends. **File:** `WorldScale.cs` lines ~187, or `OptionalLineBuilder`'s tier-3 branch.
4. **Landing run — leave the 100 m reservation alone, and re-measure it against geometry.** Speed reconverges in
   0–12 m, so nothing about the model's accuracy argues for changing it. If the reservation is to be revisited, the
   quantity to measure is lateral recovery (metres until the ball is back on the line within the corridor's half
   width) and it wants a slamming driver, which the follower is not. **File:** none proposed.

### Deviations from the packet and why

- **The wall-probe classifier reads the terrain, not the stamp.** The packet says "misread if the finest sample along
  the level-width reach is ≥ 0.99 weight". Implemented literally, that classified 6 of 6 rejections as misreads
  including the 93 m canyon wall, because a stamp always fades outward and sampling inward always recovers full
  weight. The rule shipped is `RiseMetres > 3f` — how far the ground climbs above the route across the window — with
  the full weight/height profile printed for the worst case so the main track can see the shape rather than take the
  classification on trust. The 24 m window and the 1 m spacing are the packet's.
- **The floor-3 test is applied against the primary**, as the packet's criteria state, and the result is therefore a
  measurement of *that* candidate rather than of branching in the fuller sense. See "Needs main track".
- **Instrument 4 runs on the archetype the run is driving**, so the two named seeds are two runs rather than one; the
  runbook gives both commands.

### Open items

- Sky's two "misread" rejections are not explained, only ruled out: no wall, no optional line within 337 m, flat
  ground, and the primary's own stamp under 0.99 at the reach. Something else on a Sky stage is thinning the
  primary's stamp out there; the instrument reports it without diagnosing it, which is where the packet's boundary is.
- The slam column of table 4 is empty on all seven landings, so the chained-slam case D-094 measured is untouched
  here.

### Needs main track

- **`BuildLine` discards a line's own segment kinds.** Every vertex of an optional line is written
  `Kind = RouteSegmentKind.Straight` and the skeleton carries no `Bends`, so a line has no bend structure of its own.
  D-103's actual candidate — a floor 3 whose transitions ride the floor 2 rather than the primary — cannot be
  evaluated without it: `JoinsOnStraights` and `SideValid` against a floor-2 skeleton would be vacuously true. This
  is the packet's "a measurement needs a builder to expose data it discards" stop condition. Giving `BuildLine` the
  primary's segment kinds mapped onto the line's own vertices would be a few lines and would change no geometry.
- **The wall-clearance check is measuring something other than a wall on Sky Terraces** (recommendation 1). Reported,
  not fixed, per the packet's other stop condition.
