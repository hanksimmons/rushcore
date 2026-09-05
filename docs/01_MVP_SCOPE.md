# 01 — MVP Scope

**Status:** Draft 0.3 — final audited  
**Authority:** Canonical feature boundary  
**Owns:** In-scope/out-of-scope features, content ceilings, MVP completion boundary

## Scope principle

The MVP proves one proposition:

> High-speed terrain-driven rolling-ball movement remains fun when embedded in a complete repeatable roguelite run.

Anything not required to prove that proposition is deferred.

## In scope

### Player

- `RigidBody3D`-based arcade-physics controller.
- Camera-relative WASD steering for the prototype.
- Tight low-speed steering with progressively larger high-speed turning radius.
- Hard tunable maximum playable locomotion speed.
- Strong slope-driven acceleration plus player propulsion on flat/uphill terrain.
- Charge jump on Space:
  - charge begins on press,
  - takeoff occurs on release,
  - hold duration interpolates between minimum and maximum vertical takeoff speed,
  - charging does not add artificial slowdown,
  - steering is disabled while charging.
- Air steering.
- Ground slam triggered by a new jump/slam press while airborne.
- Bonus “perfect apex slam” when slam begins near the jump apex.
- Boost with finite/refillable meter, including slow emergency passive regeneration plus active refill sources.
- Boost usable in air.
- Boost direction blended between current travel direction and desired input, with current trajectory dominating at high speed.
- One prototype carve/traction action that trades speed for tighter control; retention is **VALIDATE**.
- Health/damage.
- Four qualitative speed bands: Roll, Rush, Crush, Overdrive.
- Flow execution/combo system.
- Fall recovery/checkpoint respawn.

### Camera

- Fixed orientation identity; no manual camera rotation in MVP.
- Perspective projection with isometric-like composition.
- Player zoom within bounded range.
- Speed-responsive look-ahead.
- Speed-responsive distance/FOV presentation.
- Damped follow and bounded impact/slam shake.
- Exact pitch/yaw/FOV/distance values are **VALIDATE**.

### Stages/run

- Nine stages per run.
- Stages 1–8 use procedural traversal/combat terrain.
- Stage 9 is one movement-centric final encounter.
- Normal stage objective: reach the exit alive.
- Normal enemies are optional opportunities, never mandatory cleanup.
- Two informed route choices after normal stage completion unless a special transition intentionally replaces them.
- Route cards communicate terrain/reward/danger/modifier information.
- Broad directional landscapes rather than narrow corridors or directionless open fields.
- Approximately 1–3 meaningful optional lines/shortcuts per stage, plus small local line choices.
- Mandatory primary route does not require having boost available.
- Deterministic stage seeds recorded for replay/debugging.
- Falling normally costs health/Flow/momentum and returns to a safe checkpoint rather than instantly ending the run.
- Exact physical stage scale and clear-time target are **VALIDATE**.

### Procedural terrain

Initial archetypes:

1. Rolling Highlands — terrain reading/momentum conversion.
2. Canyon Run — high-speed steering/carving.
3. Dune Sea — jump rhythm/landing alignment.

Initial reusable challenge modules:

- moderate gap,
- large optional gap/shortcut,
- launch ramp,
- banked turn,
- ridge shortcut,
- boost/pickup line.

Generation guarantees a primary traversable route by construction and validates it before play.

Terrain archetypes do not secretly change player physics.

### Combat/enemies

- No attack button; player contact is the core attack interaction.
- Relative closing impact determines crush vs harmful collision.
- Successful crushes preserve almost all useful forward momentum.
- Underpowered contacts cause velocity loss, bounded deflection, health damage, and brief damage invulnerability.
- Slam provides an offensive context; perfect-apex slam is stronger.
- Four baseline enemy archetypes:
  - Pylon,
  - Bulwark,
  - Strider,
  - Shooter.
- One elite modifier model reuses those archetypes.
- Enemies are movement problems, not conventional stop-and-fight melee units.
- Enemy defeat can contribute XP, currency, boost, Flow, and score.

### Flow

- Measures uninterrupted skilled execution, not raw speed.
- Increases through high-quality traversal/combat/shortcut play.
- Decays slowly when play loses momentum and drops strongly on major mistakes.
- Rewards score plus modest XP/currency bonuses and stronger presentation.
- Does not initially provide a large damage multiplier.

### Run progression

- XP and queued level-ups.
- Approximately 8–15 level selections in a successful run as a **VALIDATE** pacing target.
- Three randomized upgrade results; choose one.
- Three rarity tiers for MVP: Common, Rare, Legendary.
- Level choices resolve at safe/inter-stage moments in the first MVP.
- Approximately 12–18 upgrade definitions.
- Approximately 12 passive item definitions.
- Level upgrades = frequent capability modifiers.
- Passive items = rarer behavior-changing synergies.
- Passive items can stack; stacking rule defined per item.
- No active-use item system.

### Economy/shop

- One run currency.
- Money is for shop purchases only in MVP.
- Enemy currency rewards auto-collect/magnetize; the game should not create cleanup backtracking for coins.
- One simple shop:
  - three offers,
  - optional simple recovery purchase.
- Route rewards use highly legible categories such as Item, Currency, Healing, Upgrade/XP, Shop.

### Final encounter

- One stage-nine movement-centric final encounter.
- Accepted direction: a Bastion-style arena/structure encounter that tests speed-building, terrain use, boost, jump/slam, and impact timing rather than introducing a new combat language.
- Exact encounter content is deferred until the core systems exist.

### Run failure/meta

- Zero health ends the run.
- Run death wipes temporary upgrades, passive items, currency, and stage progress.
- Meta XP/unlocks persist.
- Meta tree: approximately 12–20 nodes.
- Meta progression emphasizes unlocked possibilities and modest starting advantages, not overwhelming raw-stat escalation.

### Presentation

- 100% placeholder gameplay art generated/configured in C#.
- Low-poly/N64-inspired visual language.
- Procedural geometry for player, enemies, pickups, terrain, props, effects.
- Runtime-created lighting, environment, camera, shaders/materials, and UI.
- Visual speed bands rather than a mandatory exact speedometer.
- Readability of crushable/dangerous enemies comes primarily from silhouette/material/VFX, not giant text labels.
- High-quality readable placeholder VFX.
- No external game-art dependency required for MVP.

### Debug/developer tooling

- Toggleable runtime tuning panel.
- Runtime controls for core movement, charge jump, apex slam, boost, camera, combat, generation, and VFX parameters.
- Same-seed restart.
- New-seed restart.
- Seed display/copy.
- Debug telemetry.
- Save/load local tuning overrides.
- Basic debug cheats.

### Audio preparation

- No audio content.
- Semantic gameplay events make later audio integration straightforward.
- Gameplay code has no hard-coded audio dependency.

## Explicitly out of scope

Unless later promoted:

- multiplayer/networking,
- online leaderboards,
- daily runs,
- achievements,
- cloud saves,
- platform SDKs,
- narrative/dialogue/quests,
- crafting,
- inventory grids,
- active-use equipment,
- weapon classes,
- character roster,
- cosmetic customization,
- procedural caves/overhang worlds as a core system,
- infinite-world streaming,
- destructible terrain,
- complex physics destruction,
- wall-running,
- grappling,
- homing attacks,
- double jump,
- separate dash mechanic,
- complex navigation-mesh enemy AI,
- animation-heavy humanoid boss pipeline,
- external art production pipeline,
- audio production/mixing,
- generalized ECS,
- dependency-injection framework,
- generic service locator,
- speculative pooling everywhere,
- mod system,
- generalized save migration framework.

## Content ceilings

| Content/system | MVP ceiling |
|---|---:|
| Terrain archetypes | 3 |
| Challenge modules | 6 |
| Normal enemy archetypes | 4 |
| Elite modifier families | 1 |
| Final encounters | 1 |
| Passive items | 12 |
| Level-up upgrades | 18 |
| Route/stage modifiers | 6 |
| Meta nodes | 20 |
| Route choices per transition | 2 |

These are ceilings, not quotas.

## MVP quality gates

The project is not complete merely because every box exists.

It must pass `08_TEST_ACCEPTANCE.md`, especially:

- movement remains fun without progression,
- charge-jump constraint creates useful risk/reward,
- apex slam is learnable and satisfying,
- stage seeds are deterministic,
- generated primary routes are valid,
- combat outcomes are readable,
- complete runs work,
- presentation remains readable at maximum playable speed,
- target-machine performance is acceptable.
