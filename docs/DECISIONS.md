# Decisions & Validation Register

**Status:** Draft 0.3 — final audited accepted baseline  
**Purpose:** Record settled design rules and the small set of empirical questions intentionally left to playtesting/profiling.

## Status meanings

- **ACCEPTED** — project rule unless explicitly changed.
- **VALIDATE** — design intent is accepted; exact value/curve or retention needs playtesting.
- **DEFERRED** — intentionally postponed because current slices do not need the detail.

## Accepted core movement decisions

| ID | Status | Decision |
|---|---|---|
| D-001 | ACCEPTED | Player uses real 3D rigid-body physics with deliberate arcade authority rather than pure torque-driven marble control. |
| D-002 | ACCEPTED | High-speed steering remains available but turn radius grows substantially with speed. |
| D-003 | ACCEPTED | Intentional movement actions preserve useful momentum unless explicitly designed to trade it away. |
| D-004 | ACCEPTED | RUSHCORE uses a hard tunable maximum playable locomotion speed, not a soft asymptotic ceiling. |
| D-005 | ACCEPTED | Downhill slopes materially accelerate the player; terrain is a strategic movement resource. |
| D-006 | ACCEPTED | Base propulsion works on flat/uphill terrain; boost is not the only way to move against gravity. |
| D-007 | VALIDATE | Prototype carve/traction as one held action trading speed for tighter control; remove it if it does not clearly improve play. |
| D-008 | ACCEPTED | Prototype steering uses WASD; Space is the shared jump/slam input. |
| D-009 | ACCEPTED | Jump charges while Space is held and occurs on Space release. |
| D-010 | ACCEPTED | Jump charge maps hold duration between minimum and maximum vertical takeoff velocity. |
| D-011 | ACCEPTED | While charging, the player continues rolling without charge-induced slowdown but cannot steer. |
| D-012 | ACCEPTED | Jump preserves essentially all useful horizontal/tangent momentum. |
| D-013 | ACCEPTED | No double jump. |
| D-014 | ACCEPTED | A new Space press while airborne initiates slam. |
| D-015 | ACCEPTED | Slam preserves lateral momentum, commits strongly downward, allows limited steering, and is both movement and offense. |
| D-016 | ACCEPTED | Slam initiated near the jump apex receives a stronger “perfect apex” bonus. |
| D-017 | ACCEPTED | Perfect-apex detection stays KISS: forgiving vertical-velocity threshold, not a separate timing/combo system. |
| D-018 | ACCEPTED | Boost works in air. |
| D-019 | ACCEPTED | Boost direction blends current trajectory and desired input; trajectory dominates more at high speed. |
| D-020 | ACCEPTED | Boost has slow emergency passive regeneration plus stronger active refill sources. |

## Accepted speed/Flow/combat decisions

| ID | Status | Decision |
|---|---|---|
| D-021 | ACCEPTED | Four qualitative speed bands: Roll, Rush, Crush, Overdrive. |
| D-022 | ACCEPTED | Flow measures uninterrupted skilled execution, not raw speed. |
| D-023 | ACCEPTED | Flow rewards score plus modest XP/currency bonuses and presentation; no major base damage multiplier. |
| D-024 | ACCEPTED | Flow decays slowly during low-energy play and drops strongly on major mistakes. |
| D-025 | ACCEPTED | No separate attack button; hostile contact is the fundamental attack interaction. |
| D-026 | ACCEPTED | Combat uses relative closing impact rather than raw speed alone. |
| D-027 | ACCEPTED | Successful crushes preserve almost all useful forward momentum. |
| D-028 | ACCEPTED | Failed hostile impacts cause velocity loss, bounded deflection, health damage, and short invulnerability. |
| D-029 | ACCEPTED | Normal enemies are never mandatory for stage completion. |
| D-030 | ACCEPTED | Enemies are movement challenges; initial archetypes are Pylon, Bulwark, Strider, Shooter. |

## Accepted stage/generation decisions

| ID | Status | Decision |
|---|---|---|
| D-031 | ACCEPTED | A stage is a large bounded directional landscape with entry, broad progression, optional lines, and exit. |
| D-032 | ACCEPTED | Stages are neither narrow race corridors nor directionless open sandboxes. |
| D-033 | ACCEPTED | Each stage has one guaranteed primary traversable region plus about 1–3 meaningful optional lines/shortcuts. |
| D-034 | ACCEPTED | Mandatory primary traversal does not require boost availability. |
| D-035 | ACCEPTED | Falling normally recovers at a safe checkpoint with health/Flow/momentum penalty rather than instant run death. |
| D-036 | ACCEPTED | Chasms/bottomless hazards may appear selectively for scale, jumps, and optional shortcuts. |
| D-037 | ACCEPTED | Initial terrain families: Rolling Highlands, Canyon Run, Dune Sea. |
| D-038 | ACCEPTED | Terrain archetypes change strategy through geometry, not hidden changes to player physics. |
| D-039 | ACCEPTED | Procedural generation builds valid gameplay/route structure before adding noise/detail. |
| D-040 | ACCEPTED | Primary mandatory jump envelope must be achievable with base movement; optional lines may demand boost/stronger execution. |

## Accepted run/progression/economy decisions

| ID | Status | Decision |
|---|---|---|
| D-041 | ACCEPTED | Normal stage ends by reaching exit alive. |
| D-042 | ACCEPTED | MVP run contains nine stages; stage nine is the final encounter. |
| D-043 | ACCEPTED | Normal stage transitions present two informed route choices unless intentionally replaced by a special transition. |
| D-044 | ACCEPTED | Level upgrades are frequent capability modifiers; passive items are rarer behavior-changing synergies. |
| D-045 | ACCEPTED | Level choices resolve at safe/inter-stage moments in first MVP. |
| D-046 | ACCEPTED | Level-up presentation reveals three randomized choices; player selects one. |
| D-047 | ACCEPTED | MVP upgrade rarity tiers: Common, Rare, Legendary. |
| D-048 | ACCEPTED | Passive items can stack; stacking rule is defined per item. |
| D-049 | ACCEPTED | No active-use item system in MVP. |
| D-050 | ACCEPTED | Run money is for shop purchases only in MVP. |
| D-051 | ACCEPTED | Currency/reward drops visibly burst then magnetize/auto-collect; no coin-cleanup backtracking. |
| D-052 | ACCEPTED | Route reward categories are explicit and legible. |
| D-053 | ACCEPTED | Final encounter uses the same core movement/combat language; Bastion-style terrain/speed encounter is the MVP direction. |
| D-054 | ACCEPTED | Run death wipes temporary run progression/currency/stage state while meta XP/unlocks persist. |
| D-055 | ACCEPTED | Meta progression favors unlocks/possibilities and modest starting advantages rather than overwhelming raw power. |

## Accepted UX decisions

| ID | Status | Decision |
|---|---|---|
| D-056 | ACCEPTED | Normal gameplay does not require an exact numerical speedometer; raw m/s is debug telemetry. |
| D-057 | ACCEPTED | Crushability/threat is communicated primarily through speed VFX, enemy silhouette/material, and interaction feedback. |
| D-058 | ACCEPTED | MVP camera orientation is fixed; player may control bounded zoom but not manual rotation. |
| D-059 | ACCEPTED | Perspective camera is composed to read as isometric-like while preserving depth/speed cues. |
| D-060 | ACCEPTED | Complexity should come from recombining existing verbs/systems, not adding more player actions. |

## Accepted engineering constraints

| ID | Status | Decision |
|---|---|---|
| D-061 | ACCEPTED | Main terrain render uses generated `ArrayMesh`; main heightfield collision uses `HeightMapShape3D` unless testing disproves the fit. |
| D-062 | ACCEPTED | Start generation on main thread; add worker computation only if profiling justifies it. |
| D-063 | ACCEPTED | No generic global EventBus, generalized pooling, DI framework, or speculative streaming architecture initially. |
| D-064 | ACCEPTED | One minimal Godot bootstrap scene/root is acceptable; runtime gameplay content is constructed/configured in C#. |
| D-065 | ACCEPTED | No audio content now; semantic gameplay events make later audio integration straightforward. |
| D-066 | ACCEPTED | Player `RigidBody3D` uses continuous collision detection from the Movement Toy onward because high-speed play makes tunneling a first-order risk. |
| D-067 | ACCEPTED | Player contact reporting is enabled with a small sufficient contact budget because grounded-state logic reads direct-body contacts. |
| D-068 | ACCEPTED | Checkpoint/debug/stage teleports reset physics interpolation after repositioning. |
| D-069 | ACCEPTED | Hard locomotion cap means ground-tangent speed while grounded and world-horizontal speed while airborne; jump/slam vertical velocity is not clipped by the gameplay cap. |
| D-070 | ACCEPTED | Perfect-apex slam eligibility exists only in the same airborne arc created by a player-triggered jump, not generic falls/external launches. |
| D-071 | ACCEPTED | Player-controlled `RigidBody3D` starts with sleeping disabled and `CustomIntegrator` left off; standard Jolt gravity/damping remain active while `_IntegrateForces()` provides arcade control. |

## Empirical validation register

These are the only major gameplay/feel variables intentionally not frozen numerically.

| ID | Status | Validate |
|---|---|---|
| V-001 | VALIDATE | Exact high-speed steering curve/authority. |
| V-002 | VALIDATE | Whether carve earns its input and remains in the game. |
| V-003 | VALIDATE | Exact passive boost regeneration and active refill rates. |
| V-004 | VALIDATE | Effective hard playable speed cap plus Rush/Crush/Overdrive thresholds. |
| V-005 | VALIDATE | Ball diameter and world/terrain scale relationship. |
| V-006 | VALIDATE | Camera pitch/yaw/FOV/distance/look-ahead/damping values. |
| V-007 | VALIDATE | Physics tick rate: begin at 60 Hz; test 120 only if evidence warrants it. |
| V-008 | VALIDATE | Exact terrain/stage physical dimensions and heightfield sampling density. |
| V-009 | VALIDATE | Exact stage clear-time and full-run duration targets. |
| V-010 | VALIDATE | Jump min/max takeoff speeds, max charge duration, and whether linear charge mapping is sufficient. |
| V-011 | VALIDATE | Perfect-apex vertical-velocity window and slam/impact bonus strength. |
| V-012 | VALIDATE | Approximate successful-run level-up count; starting target 8–15. |

## Deferred implementation details

The following are not unresolved product-design gaps and should be chosen by the coding agent using the specs/KISS:

- exact source-folder tree,
- `.csproj`/solution/bootstrap generation,
- exact collision-layer numbers,
- polyline vs helper curve representation internally,
- concrete C# record/class choices,
- exact boost/carve keyboard default keys,
- whether a specific projectile type eventually merits pooling,
- whether render terrain needs chunking after profiling.

These should not delay kickoff.
