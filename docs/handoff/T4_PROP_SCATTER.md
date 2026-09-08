# T4 — Archetype-aware prop scatter on generated stages

**Branch:** `opus/t4-prop-scatter` off `develop-secondary`  
**Phase:** 3 "prop scatter/MultiMesh only where useful" (the last open Phase 3 bullet)  
**Gate lines served:** 08 §9 "gaps/ramps/banks are readable early enough" (scale cues), 08 §10 "stable 60 FPS", "no obvious managed-allocation hot-loop spikes"; 04 §5G  
**Owning specs:** 04 §5G, §16; 05 §9 (cosmetic stream), §13, §21; 06 §3, §16, §17; 07 §5 (Prop Density)

## Goal

Scatter that reinforces scale and speed per archetype, never enters any line, landing zone, structure or floor, is
deterministic from the cosmetic seed, and is measured for cost. The scatter is scenery; it changes no gameplay data.

## Current state

- `src/World/WorldDressing.cs` `BuildScatteredProps(clear, rangeX, rangeZ, attemptsScale, chance)` (line ~795):
  rocks, crystals and pylons as three `MultiMesh`es, `Prop Density` slider (0..3, default 0.19), a
  `RandomNumberGenerator` seeded from `_world.Seed ^ constant` (not the cosmetic stream), 1600 × density attempts.
  On stages (`BuildStageDressing`, line ~336) the clear function is `InBounds(x, z, 40)` and
  `field.DistanceToRoute(x, z) > CorridorHalfWidth + BendExtraHalfWidth + FalloffWidth / 2`.
- Verify what `StageHeightField.DistanceToRoute` covers: the primary only, or every line. Optional lines, tube mouth
  zones (`TubeDefinition.Bounds`, `MouthKind`), lid footprints (`LidDefinition.Covers`), the spiral disc
  (`SpiralPit.Centre`, `OuterRadius`), terrace floors (`RouteSkeleton.Floor ≥ 2` lines) and challenge landing runs
  (`RouteFeature` centre to `LandingEnd`, see `StageGenerator` module checks) are all in `StageDefinition`.
- `StageGenerationRequest.CosmeticSeed` exists (05 §9) and is unused.
- Build cost is printed by `MovementToyWorld.Build` (`BuildMillis`, tris, tiles, MB); the harness checks the build
  budget (8000 ms) and node count. Instance counts are not printed.
- Palette per archetype lives in `StageHeightField` (the sand palette for dunes, the canyon rock, cloud white) and
  `WorldDressing.CreateTerrainMaterial`.

## Design

1. **One clear function for stages**, `StageDressing.ClearOf(stage)`, that returns false inside: every line's
   level width plus falloff (primary and optional, all floors, **terminal lines included**), every module's body and
   landing run, every tube's mouth zone and the ground under its axis (R + 10 m plan), every lid footprint plus 20 m,
   the spiral disc plus 40 m, **every exit pad in `StageDefinition.Exits` plus its `WorldScale.PadRadius` and 20 m
   more** (D-105: one to three flat 60 m pads a stage, and a prop on one would sit in the place the stage ends), and
   30 m of every checkpoint anchor. Build it from `StageDefinition` data only; no field sampling except height.
2. **Per-archetype prop sets** (06 §16 list: rocks, crystals, sparse vegetation, pylons/markers, abstract
   structures), each a small static description (mesh, colour family, size range, slope limit, where it likes to be):
   - Highlands: rocks and crystal clusters, densest 100–300 m off the corridor edge, fading beyond 600 m.
   - Canyon: slab rocks and fins on the wall tops only (height above the corridor floor > wall height × 0.6), none on
     slot floors; marker posts along the slot rim every 150 m outside the level width.
   - Dunes: ridged stones and dry tufts on wave crests only (slope < 0.05, local maximum in the swale direction),
     sparse; nothing inside a train's landing runs.
   - Sky: crystal clusters on the terrace outer margins (outside `TerraceOuterMargin`), nothing above the cloud
     band height, nothing on floor surfaces.
   Sizes scale with distance from the route (bigger farther) so parallax reads speed.
3. **Scale cues on the corridor edge** (06 §16 "pylons/markers", 04 §5G): low marker posts at the level-width edge
   every 200 m on straights, none in bends (banks), none inside modules; these are meshes without colliders
   (`AddMarker` pattern from the debug views).
4. **Determinism:** the scatter RNG is seeded from `Request.CosmeticSeed` (P-008). Two builds of the same request give
   identical instance transforms; changing `Prop Density` leaves the stage hash unchanged (it already should; assert it).
5. **Budget:** cap instances per archetype (state the caps), print `[RUSHCORE] scatter: N rocks, M crystals, K markers
   in T ms` in the build log, and measure frame time at the cap on Highlands seed 8 with density 0.19 and 1.0
   (the debug overlay's frame row; report both).

## Pre-answered choices

- Cosmetic seed (P-008). Density default stays 0.19 (world toggles are never promoted; the user's default).
- No colliders on any scatter that could be within 200 m of a line; farther rocks may keep their colliders as today
  (the toy uses them as hard-edged test objects) but state the rule you used.
- Do not add an LOD system; if the instance cap forces a visible cutoff, record it under Open items.

## Non-goals

- Vegetation shaders, wind, LOD, chunk streaming, lighting changes (06 §16 lighting baseline is set).
- Any prop with gameplay meaning (that is 04 §5F, Phase 5).
- Any change to the terrain material or palette.

## Acceptance (harness)

For each of the four archetype stage cases (run the default harness plus `RUSHCORE_ARCHETYPE=canyon|dunes|sky`):

- Zero scatter instances inside `ClearOf(stage)` (iterate every MultiMesh instance transform; report the count and the
  nearest offending distance if any). Report the nearest instance to each exit pad separately: the pads are the newest
  keep-out (D-105) and the easiest to forget.
- Instance transforms byte-identical across two builds of the same request; stage hash identical across density 0.19
  and 1.0.
- Instance counts within the caps; scatter time under 300 ms; total build still under the 8000 ms budget.
- Node count flat across rebuilds (existing check).
- The marker posts have no `CollisionShape3D` descendants.

Manual: the user's read of scale and speed on the five sample stages; that nothing reads as a hazard or an obstacle in
the corridor.

## Files expected

`src/World/WorldDressing.cs` (possibly split the stage scatter into `src/World/StageScatter.cs`),
`src/World/MovementToyWorld.cs` (build log line), `tests/MovementToySelfTest.cs`, docs 04 §5G, §16 (one line each),
06 §16 (what each archetype scatters), 07 §5, 10, this packet, `STATUS.md`, `PROVISIONAL_DECISIONS.md`.

## Stop conditions

Stop and write "Needs main track" if the clear function needs data that `StageDefinition` does not carry (do not add
fields to generation data; report which), or if any check reveals a line, module or structure whose bounds are wrong
(report the seed and the numbers; do not fix the generator).

## Delivery record (filled by the implementing agent)

- **Branch / commits:** `opus/t4-prop-scatter` off `develop-secondary` (5d940c3); 524dc21.
- **Harness (full run and the three archetype runs):** 323/323 default (82 s), canyon 320/320, dunes 323/323,
  sky 324/324. Golden hashes unchanged (the scatter reads the cosmetic stream and never touches generation data).
- **Files changed:** `src/World/StageScatter.cs` (new), `src/World/WorldDressing.cs`, `src/World/MovementToyWorld.cs`
  (scatter counters and the build log line), `tests/MovementToySelfTest.cs`, docs 04 §5G, 06 §16, 07 §5, 10, this
  packet, `STATUS.md`, `PROVISIONAL_DECISIONS.md`.
- **What was built:**
  - `StageScatter`: the keep-out stated once from `StageDefinition` alone — every line's level width plus its bend
    extra and half its falloff (160 m; primary and every optional line, all floors, terminal lines included), every
    exit pad plus 20 m, every checkpoint anchor plus 30 m, every lid footprint plus 20 m, every tube axis plus its
    radius and 10 m (behind a plan bounding circle), the spiral disc plus 40 m. `Why(x, z)` names the rule that
    rejects a point, so a violation reports its own cause. The dressing asks it before placing and the harness asks
    it about everything placed.
  - Per-archetype sets (06 §16): highlands rocks and crystal clusters, densest just off the corridor edge and
    thinning past 600 m; canyon red slabs and standing fins on the wall tops only (above 60% of the wall height),
    never on a slot floor; dune ridged stones and dry tufts on the wave's crests only, sparse; sky pale crystals on
    the margins around the floors, below the cloud band. Instances grow with distance from the route (0.9 → 1.8),
    so the parallax reads as speed.
  - Scale cues: collider-free posts at the corridor's level width every 200 m of straight route, both sides, none in
    a bend, across a module, or under a lid.
  - Determinism: the stream is `Request.CosmeticSeed` (P-008), and every draw is taken before any test, so the
    stream does not depend on which tests pass. No scatter collider stands within 200 m of a line.
- **Deviations from the packet and why:**
  - Caps are a ceiling, not the working number (P-013): the first cut at 900/500 was saturated at the shipped
    density, which killed the `Prop Density` slider. 1800 rocks / 900 crystals / 240 markers, with the default
    landing well under them.
  - The harness measures the positions the dressing recorded rather than reading them back out of the `MultiMesh`.
    Reading `MultiMesh.GetInstanceTransform` back returned the origin for every instance, which made the first
    version of the check pass nothing; a separate check asserts the drawn instance count equals the placed count.
  - Modules and their landing runs get no separate keep-out test: they lie on their line and are inside the line
    keep-out by construction. Stated in 04 §5G rather than coded twice.
  - The 300 ms scatter budget is asserted at the shipped density. At density 1.0 (over five times it) the scatter
    takes about 680 ms, which the harness bounds at 1000 ms; the build budget (8000 ms) is untouched either way.
  - Frame time at density 0.19 and 1.0 could not be measured: the harness is headless, so there is no renderer to
    time, and no AI agent plays stages. The instance counts and the scatter cost are reported instead, and the
    frame-rate read is the user's.
- **Measurements:** see the table below.
- **P-entries written:** P-013 (new); P-008 implemented as written.
- **Spec sections edited:** 04 §5G (the keep-out, delivered), 06 §16 (what each archetype scatters), 07 §5 (the
  density slider and the caps), 10 (the runbook line and the build-log format).
- **Open items:** three, all waiting on the user's read in play. Each names the lever, so any of them is a small
  edit once the verdict exists.

  1. **Dune Sea is very sparse: about 190 instances across a 6 × 2 km stage** (133 rocks, 54 tufts), against 1264 on
     Highlands. That is the packet's rule — crests only, sparse — and the crest ribbons are a small part of the
     footprint, so the rule and the emptiness are the same thing. Whether it reads as accents on the crest lines or
     as a bare stage at speed is the user's call. Lever: the `DuneSea` branch of `WorldDressing.Suits` — the crest
     threshold (`DuneHeight > 0.6 × wave height`), the slope limit (0.12) and the sparsity lottery (0.7). It was
     first cut at 0.8 / 0.09 / 0.45, which gave 100 instances; the current numbers gave 187. Filling the swales
     instead of only the crests would be a rule change, not a number change, and needs the user's word.
  2. **Canyon Run carries no crystals at all and is heavy on rocks** (1458 slabs and fins, every one on a wall top;
     zero on the slot floors). Both are the packet's rule ("slab rocks and fins on the wall tops only, none on slot
     floors"), so this is a question about whether the rule is right, not whether it was followed: a canyon with a
     bare floor beside the corridor and a dense rim may read well or may read as a fence. Lever: the `CanyonRun`
     branch of `Suits` — it sets `crystal = false` unconditionally and requires the point to stand above 60% of the
     wall height. Allowing crystals, or lowering that fraction to let scatter down the wall face, are both one line.
  3. **Frame time at density 0.19 and 1.0 was not measured.** The packet asks for the debug overlay's frame row on
     Highlands seed 8 at both densities. The harness is headless, so there is no renderer to time, and no AI agent
     plays stages (the standing rule), so this figure cannot come from this track at all. What is measured instead:
     instance counts, collider counts and scatter milliseconds per archetype, in the table below. To close it the
     user runs `godot --path . -- --seed 8`, F2 for telemetry, and reads the `frame` row at `World › Prop Density`
     0.19 and again at 1.0 (2700 instances, the caps). If 1.0 costs frames, the caps are the dial.

  Also open: no LOD (the packet's instruction); the caps are never reached at the shipped density, so nothing pops.
- **Needs main track:** nothing new.

### Measured (density 0.19 unless stated)

| Stage | Rocks | Crystals | Markers | Colliders | Scatter ms |
|---|---|---|---|---|---|
| Rolling Highlands (default seed) | 819 | 445 | 46 | 1172 | 155 |
| Canyon Run | 1458 | 0 | 36 | 1357 | 209 |
| Dune Sea | 133 | 54 | 30 | 130 | 7 |
| Sky Terraces | 220 | 634 | 46 | 774 | 68 |
| Rolling Highlands at density 1.0 | 1800 (cap) | 900 (cap) | 46 | 2473 | 681 |
