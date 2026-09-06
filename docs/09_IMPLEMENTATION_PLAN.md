# 09 — Implementation Plan

**Status:** Draft 0.3 — final audited  
**Authority:** Build order and gates only  
**Rule:** Must not redefine behavior owned by higher-authority specs.

## Strategy

Build in risk order.

The first risk is not “can we make a roguelike UI?” It is:

> Is RUSHCORE's rolling, charging, jumping, slamming, burst-timing, boosting, and terrain reading intrinsically fun?

Each slice should compile, run, expose useful tuning, pass its gate, and avoid prebuilding future phases.

## Phase 0 — Agent/bootstrap

The coding agent creates/repairs whatever minimal Godot .NET project/bootstrap structure is required and proves a clean C# launch.

This is implementation work, not a remaining design question.

## Phase 1 — Movement Toy

Deliver:

- calibration/test terrain,
- `RigidBody3D` player,
- camera-relative WASD movement,
- hard playable speed cap,
- slope acceleration,
- charge jump:
  - Space press begins charge,
  - release jumps,
  - min/max takeoff speeds,
  - no steering while charging,
- air control,
- Space airborne slam,
- slam power impact + landing burst,
- boost on ground/air,
- slow passive boost regeneration + active refill hook,
- chase-camera rig,
- procedural player/terrain/VFX,
- runtime tuning panel,
- telemetry,
- fall recovery with physics-interpolation reset after teleport.

Gate: **M0** — passed 2026-09-05. Carve was removed at this gate.

## Phase 1B — Scale calibration

Use known distances/features to settle:

- ball/world scale,
- max speed/speed bands,
- terrain wavelengths/heights,
- camera scale,
- gap/ramp/bank sizes.

Instrument: the scale strip (`World › Calibration Strip (M1)`, D-079). Movement is frozen (D-078); only world scale is decided here, together with its terrain budget (cell size, footprint, build/draw cost, D-080).

Gate: **M1**.

These tuned values become generation inputs.

## Phase 2 — Procedural Terrain Core

Deliver:

- deterministic generation request/definition,
- route-first skeleton,
- Rolling Highlands,
- heightfield pipeline,
- `ArrayMesh`,
- `HeightMapShape3D`,
- guaranteed primary route,
- optional line/shortcut,
- checkpoints,
- validation,
- same/new seed tooling.

## Phase 3 — Terrain Variety

Add:

- Canyon Run,
- Dune Sea,
- challenge modules,
- route/challenge debug views,
- prop scatter/MultiMesh only where useful.

Gate: **G0**.

## Phase 4 — Movement Combat

Deliver:

- relative closing-impact model,
- Pylon/Bulwark/Strider/Shooter,
- health,
- Flow,
- defeat/damage,
- basic reward drops with auto-collect,
- enemy/pickup VFX.

Gate: **C0**.

## Phase 5 — Complete Stage

Deliver:

- start/exit,
- HUD,
- stage objective,
- generated enemy/reward placement,
- completion transition,
- score accumulation.

Gate: **S0**.

## Phase 6 — Run & Routes

Deliver:

- nine-stage sequence,
- two informed route choices,
- route metadata,
- escalating difficulty,
- clean stage lifecycle.

## Phase 7 — Run Progression

Deliver:

- XP,
- queued safe level choices,
- three-result slot presentation,
- Common/Rare/Legendary,
- initial upgrade set,
- passive item framework,
- stacking rules,
- initial items.

Prove synergies before filling content ceilings.

## Phase 8 — Economy & Shop

Deliver:

- one run currency,
- magnetized/auto-collected reward presentation,
- three-offer shop,
- one recovery purchase,
- route reward integration.

## Phase 9 — Final Encounter

Deliver one Bastion-style movement-centric final encounter using existing movement/combat verbs.

## Phase 10 — Summary & Meta

Deliver:

- victory/death recap,
- score,
- meta XP,
- persistence,
- small meta tree.

Gate: **R0**.

## Phase 11 — MVP polish/performance

Only after complete loop:

- tune pacing,
- tune camera/motion effects,
- VFX/readability,
- profile,
- test physics tick rate if needed,
- optimize measured bottlenecks,
- add content only up to scope ceilings where actual variety is weak.

Gates: **V0**, **P0**.

## Coding-agent workflow

For each phase:

1. read `CLAUDE.md`,
2. read owning specs,
3. inspect current code,
4. state smallest implementation plan,
5. implement one coherent behavior at a time,
6. build/test/playtest gate,
7. fix foundational failures before adding scope,
8. report changed files and unresolved empirical findings,
9. do not implement future phases opportunistically.

## Stop conditions

Stop feature expansion and fix foundation if:

- movement is not fun/readable,
- charge jump becomes strictly superior rather than a tradeoff,
- the landing burst is too finicky or unnoticeable,
- hard speed cap produces broken-feeling motion,
- high-speed collisions are unstable,
- generation routinely produces invalid mandatory routes,
- stage transitions leak state,
- tuning values are duplicated,
- target performance fails.
