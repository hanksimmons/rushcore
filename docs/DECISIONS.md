# Decisions & Validation Register

**Status:** Draft 0.4 — Movement Toy accepted 2026-09-05 (Gate M0 passed)  
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
| D-007 | RESOLVED — REMOVED | Carve was prototyped and playtested (2026-09-05) and never felt impactful; it is removed from the game, its tuning, input and VFX. The movement vocabulary is steer, charge jump, slam, boost. |
| D-008 | ACCEPTED | Prototype steering uses WASD; Space is the shared jump/slam input. |
| D-009 | ACCEPTED | Jump charges while Space is held and occurs on Space release. |
| D-010 | ACCEPTED | Jump charge maps hold duration between minimum and maximum vertical takeoff velocity. |
| D-011 | ACCEPTED | While charging, the player continues rolling without charge-induced slowdown but cannot steer. |
| D-012 | ACCEPTED | Jump preserves essentially all useful horizontal/tangent momentum. |
| D-013 | ACCEPTED | No double jump. |
| D-014 | ACCEPTED | A new Space press while airborne initiates slam. |
| D-015 | ACCEPTED | Slam preserves lateral momentum, commits strongly downward, allows limited steering, and is both movement and offense. |
| D-016 | SUPERSEDED by D-077 | ~~Slam initiated near the jump apex receives a stronger “perfect apex” bonus.~~ Removed after playtest; every slam landing is the power impact. |
| D-017 | SUPERSEDED by D-077 | ~~Perfect-apex detection stays KISS: forgiving vertical-velocity threshold.~~ The KISS rule carries over to the landing burst: a touchdown window, no combo system. |
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
| D-058 | SUPERSEDED by D-072 | ~~MVP camera orientation is fixed~~. Playtest showed the fixed yaw hid what was ahead. Bounded zoom stays; still no manual rotation. |
| D-059 | SUPERSEDED by D-072 | ~~Isometric-like composition~~. The fixed 45° yaw remains available only as a tuning A/B toggle. |
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
| D-070 | SUPERSEDED by D-077 | ~~Perfect-apex slam eligibility exists only in the same airborne arc created by a player-triggered jump.~~ No apex mechanic remains. |
| D-071 | ACCEPTED | Player-controlled `RigidBody3D` starts with sleeping disabled and `CustomIntegrator` left off; standard Jolt gravity/damping remain active while `_IntegrateForces()` provides arcade control. |
| D-072 | ACCEPTED | Chase camera: yaw follows the player's flat velocity heading (damped, turn-rate capped, held below a minimum speed). After a heading reversal the yaw is held only while the player pushes forward against it and never longer than a tunable bound, so the camera always ends up behind the direction of travel (amended 2026-09-05 after playtest; the earlier unbounded reverse/coast hold left the view facing the wrong way). Fixed pitch, no manual rotation, bounded zoom. Camera never clips: focus and lens are floored above the heightfield and same-frame sphere casts (focus→camera and ball→camera) pull the camera in. |
| D-073 | SUPERSEDED by D-077 | ~~The perfect-apex window is tuned in seconds; detection is a vertical-speed test.~~ |
| D-074 | ACCEPTED | Landing at the cap on a slope converts world-horizontal speed to a larger ground-tangent speed (D-069); that excess becomes a short allowance bled at a tunable rate rather than being clipped in one tick. |
| D-075 | ACCEPTED | Tuning persistence: the saved override (diff from compiled defaults, versioned JSON under `user://`) is applied on launch; override state is always visible in the panel and telemetry; named presets are stashes and never the startup state. |
| D-076 | ACCEPTED | `S` / stick-back is a brake, not a reverse drive: it sheds speed along the current heading and never pushes through zero. Input within ~30° of straight against the heading only brakes; a hairpin needs clear lateral intent (W+A/D). Rationale: reverse drive gave the chase camera a heading it could not follow, and the exact-opposition case turned the ball in a direction chosen by float noise. |
| D-077 | ACCEPTED | Perfect-apex slam removed (supersedes D-016/D-017/D-070/D-073). Every slam landing is the **power impact** (impact ×1.35, strongest landing feedback). New **landing burst**: a fresh Space press within ±0.10 s of a slam touchdown (presses during the slam are buffered) fires blue sparks, a mini sonic boom with an air-parting ring, and sets locomotion speed to 80% of the hard cap along the current heading. The burst is a floor (never slows a faster ball), never changes direction, keeps the ball grounded, and the press never starts a charge. Charge jump is unchanged. |

## Empirical validation register

These are the only major gameplay/feel variables intentionally not frozen numerically.

| ID | Status | Validate |
|---|---|---|
| V-001 | RESOLVED | Lateral steering 151.25 m/s², rising to ×1.45 at the cap; turn radius still grows as v²/(a·mult), which satisfies D-002. |
| V-002 | RESOLVED | Carve removed (see D-007). |
| V-003 | RESOLVED (toy) | Boost 48 m/s², blend 0.25, capacity 100, drain 30/s, passive 4/s, pickup +35 (the perfect-apex refill went with D-077). Active refill from combat is re-examined at Gate C0. |
| V-004 | RESOLVED / OPEN | Cap 148.5 m/s and Overdrive 131.34 m/s accepted. Rush/Crush (18/32) were left unchanged; revisit when Flow/combat give them a purpose. |
| V-005 | RESOLVED | Ball radius 2.125 m (4.25 m diameter). The Movement Toy lab scenery amplitude is 2.125 (instruments keep their stated geometry); Gate M1 sizes generation features from the accepted speed/jump envelope. |
| V-006 | RESOLVED | Chase camera (D-072): distance 26 m (+10 with speed), pitch −34°, height 3, look-ahead 2→22 m, follow 8/s, vertical 4/s, FOV 62→78, yaw damping 3/s ≤140°/s, yaw hold below 2.035 m/s, reverse hold ≤1.0 s, occlusion margin 0.6, ground clearance 1.5. |
| V-007 | RESOLVED | 60 Hz. No high-speed instability was observed at the accepted cap with CCD; 120 Hz remains one hotkey (F4) away if evidence appears. |
| V-008 | VALIDATE | Exact terrain/stage physical dimensions and heightfield sampling density. |
| V-009 | VALIDATE | Exact stage clear-time and full-run duration targets. |
| V-010 | RESOLVED | 2.03 → 58.21 m/s over 0.445 s, linear mapping sufficient, release grace 0.10 s. A bare tap is a hop by design; the charge is the jump. |
| V-011 | SUPERSEDED (D-077) | ~~Window 0.618 s, slam ×1.35, impact ×1.35.~~ Slam itself: 42.95 m/s initial, 141.1 m/s²; impact ×1.35 on every slam landing; burst ±0.10 s → 80% of the cap. |
| V-012 | VALIDATE | Approximate successful-run level-up count; starting target 8–15. |

## Deferred implementation details

The following are not unresolved product-design gaps and should be chosen by the coding agent using the specs/KISS:

- exact source-folder tree,
- `.csproj`/solution/bootstrap generation,
- exact collision-layer numbers,
- polyline vs helper curve representation internally,
- concrete C# record/class choices,
- exact boost keyboard default keys,
- whether a specific projectile type eventually merits pooling,
- whether render terrain needs chunking after profiling.

These should not delay kickoff.
