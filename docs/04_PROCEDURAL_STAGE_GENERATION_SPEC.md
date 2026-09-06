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

Noise amplitude/frequency is bounded so it cannot erase route readability.

### D — Guaranteed corridor stamping

Modify terrain around the primary route to enforce:

- minimum width,
- acceptable grade,
- controlled cross-slope,
- readable transitions,
- valid start/exit,
- valid landing/recovery areas.

The route is not required to be flat. It may descend, climb, bank, crest, and roll.

### E — Challenge modules

Small authored procedural grammar pieces with explicit preconditions.

Each module defines:

- entrance assumptions,
- geometry stamp,
- expected speed/skill context,
- required vs optional status,
- landing/recovery zone,
- reward opportunity,
- validator.

Initial modules:

1. ModerateGap.
2. LargeShortcutGap.
3. LaunchRamp.
4. BankedTurn.
5. RidgeShortcut.
6. BoostLine.

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
- wide enough to preserve sense of scale.

### Dune Sea

**Primary skill:** jump rhythm and landing alignment.

Geometry:

- broad repeating waves,
- crest-to-crest timing,
- opportunities to land on downslopes,
- occasional large set-piece dune,
- wide landing zones.

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
(samples, triangles, build time, memory, draw distance). Cell size is a measured choice: it is
also a ground-contact-stability choice (measured: 4 m keeps 99% contact on the gentlest strip
hills, 8 m only 86%). Render terrain is tiled from the start; the budget model and measurements
live in the runbook.

Crest contact is a hard geometric input: a ball leaves the ground at any crest whose radius is
below v²/g (91 m at 60 m/s, 358 m at the burst speed, 560 m at the cap). For a cosine hill the
crest radius is λ²/(2π²H). Archetypes must place crests knowingly: below that radius a crest is
a launch, above it a roll.

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

Do not default the entire terrain to one giant concave triangle collider.

## 10. Route constraints

Primary route segments expose tunable constraints:

- minimum clear width,
- max longitudinal grade,
- max abrupt grade delta,
- curvature appropriate to expected speed,
- minimum landing area after mandatory jumps,
- no unmarked hard obstacle,
- no mandatory traversal dependent on boost availability.

Challenge modules may intentionally exceed ordinary safe constraints when their validator understands the exception.

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

## 12. Validation

### Mandatory

- start/exit exist,
- primary route continuous,
- corridor width above minimum,
- slope constraints pass,
- mandatory landing zones exist,
- mandatory jumps fit base capability envelope,
- spawn/exit/checkpoints have clearance,
- no required route crosses unrecoverable invalid terrain.

### Secondary

- travel length in target calibrated range,
- archetype-specific feature counts,
- shortcuts are meaningfully distinct,
- enemy density in range,
- cosmetic props do not compromise corridor/readability.

Do not initially build an AI agent that plays every stage. Add simulation validation only if real failures prove numeric construction checks insufficient.

## 13. Recovery checkpoints

Invisible anchors along the primary progression:

- stable terrain,
- enough sphere clearance,
- sensible continuation heading,
- update only after legitimate player progress,
- not immediately before unavoidable danger.

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
