# T4 — Archetype-aware prop scatter on generated stages

**Branch:** `opus/t4-prop-scatter` off `develop`  
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
   level width plus falloff (primary and optional, all floors), every module's body and landing run, every tube's
   mouth zone and the ground under its axis (R + 10 m plan), every lid footprint plus 20 m, the spiral disc plus 40 m,
   and 30 m of every checkpoint anchor. Build it from `StageDefinition` data only; no field sampling except height.
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
  nearest offending distance if any).
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

- Branch / commits:
- Harness (full run and the three archetype runs, counts and wall times):
- Files changed:
- What was built (per-archetype sets and caps):
- Deviations from the packet and why:
- Measurements (instance counts, scatter ms, frame time at density 0.19 and 1.0):
- P-entries written:
- Spec sections edited:
- Open items:
- Needs main track:
