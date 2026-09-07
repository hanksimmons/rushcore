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
  300, transition 360, ramp 600, `Length = 2T + 2Ramp + 80`) then tier 2; floor 3 lands on about 3% of Sky seeds
  (3 of 82 in the last batch). D-103 names "branching floor 3 from floor 2" as the candidate.
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
   numbers.
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

- The four tables in the Delivery record below, with the seeds behind the worst cases.
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

- Branch / commits:
- Harness (default count; data-only time with and without `RUSHCORE_MEASURE`):
- Files changed:
- Table 1 — wall probe (per archetype: rejections / misreads / intrusions / share, by bend radius bucket):
- Table 2 — tube ride (histogram, count > 85°, three worst seeds, where the maximum occurs):
- Table 3 — floor-3 room (sections that could branch, seeds gained, vs current):
- Table 4 — landing run (per landing: metres to model speed, vertical speed, slam; mean):
- Recommendations (one per instrument, with file and estimated change):
- Deviations from the packet and why:
- Open items:
- Needs main track:
