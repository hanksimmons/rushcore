# 02 — Gameplay Systems Specification

**Status:** Draft 0.3 — final audited  
**Authority:** Canonical rules for run structure, combat, Flow, progression, routes, economy, scoring, and failure  
**Does not own:** Exact movement implementation (`03`) or terrain algorithm (`04`)

## 1. Normal stage objective

**Accepted:** reach the stage exit alive.

Normal enemies are optional opportunities rather than mandatory clear conditions. Missing an enemy must never force speed-killing cleanup/backtracking.

Special route modifiers may add/replace an objective only when communicated before stage entry.

## 2. Run structure

- Nine stages.
- Stages 1–8 are procedural traversal/combat stages.
- Stage 9 is the final encounter.
- Run ends on:
  - health reaching zero, or
  - successful stage-nine completion.
- Run death clears temporary run state while meta progression persists.
- Difficulty escalates through generation parameters, enemy composition/density, elites, modifiers, and reward quality.

**VALIDATE:** exact stage clear time and total successful-run duration after movement/world scale is calibrated.

## 3. Route choice

After normal stage completion, present **two** route choices unless a special transition intentionally replaces the route screen.

Each route communicates at minimum:

- terrain archetype,
- primary reward category,
- danger rating,
- special modifier if present.

Example:

```text
CANYON RUN
Reward: Item Cache
Danger: ★★★
Modifier: Crossfire
```

Route choice is informed strategy, not blind randomness.

Normal reward categories:

- Item,
- Currency,
- Healing,
- Upgrade/XP,
- Shop,
- explicitly communicated high-risk mixed reward.

Only the selected route's full runtime stage needs to be instantiated.

## 4. Stage topology

A stage is a **broad directional traversal landscape**:

- entry,
- general forward progression,
- primary traversable region,
- approximately 1–3 meaningful optional lines/shortcuts,
- local tactical line choices,
- exit.

It is neither a narrow race corridor nor an unstructured open sandbox.

The mandatory primary route must remain viable without requiring a stocked boost meter. Boost creates better/faster/riskier optional opportunities.

## 5. Speed bands

Velocity remains continuous. Four qualitative bands provide gameplay/readability hooks:

1. **Roll** — recovery/maneuvering.
2. **Rush** — normal offensive speed.
3. **Crush** — strong offensive state.
4. **Overdrive** — exceptional high-speed/score state.

Exact thresholds are tuning values owned by `03`.

Normal player HUD does not need to display exact m/s.

## 6. Impact combat

### Core rule

There is no separate attack button.

Hostile contact resolves from **relative closing impact**, not raw player speed alone.

Conceptual model:

```text
closingSpeed = max(0, dot(playerVelocity - enemyVelocity, impactDirection))

impactPower =
    closingSpeed
    * playerImpactMultiplier
    * attackContextMultiplier
    + flatAttackBonuses
```

Keep hidden factors few and explainable.

Attack context may include:

- slam,
- slam power impact / landing burst,
- active boost if later justified,
- item/upgrade effects.

### Successful crush

If `impactPower >= enemyResistance`:

- enemy is defeated/resolved,
- blocking collision must not cause an uncontrolled solver rebound,
- player preserves almost all useful forward momentum,
- Flow increases,
- rewards resolve once,
- strong readable VFX occurs.

### Failed hostile impact

If impact is below resistance:

1. meaningful useful-velocity loss,
2. bounded deflection,
3. health damage,
4. short damage invulnerability,
5. Flow reduction/break.

Do not produce long uncontrolled pinball/tumble states.

## 7. Enemy design contract

Enemies are **movement problems**.

Each normal enemy needs:

- distinct silhouette,
- obvious threat/interaction pattern,
- readable resistance expectation,
- behavior understandable at speed.

### Pylon
Stationary low-resistance crush target.

### Bulwark
Stationary high-resistance target/obstacle. Demands more speed, a better attack context, or avoidance.

### Strider
Slowly crosses likely travel lines. Tests prediction rather than chase combat.

### Shooter
Produces simple, slow, highly readable projectiles that force line adjustment. No dense bullet-hell behavior.

### Elite
A normal archetype with one constrained modifier, stronger resistance/behavior where appropriate, and a better reward. No parallel elite AI architecture.

## 8. Flow

Flow measures **quality and continuity of execution**, not velocity itself.

### Increase through

- crushing enemies,
- sustained skillful high-speed travel,
- large/risky jumps,
- good landings,
- well-timed slam continuation,
- landing burst,
- challenge lines,
- optional shortcuts,
- selected pickups.

### Decrease through

- prolonged low-energy play,
- failed hostile impacts,
- severe environmental crashes,
- damage,
- falling/recovery,
- inactivity.

### Rewards

- score multiplier,
- modest XP bonus,
- modest currency bonus,
- stronger presentation.

Flow does not initially provide major direct damage amplification.

Flow should decay slowly enough to feel like maintaining rhythm, not servicing a frantic combo timer.

## 9. Boost economy

Boost is finite and refillable.

Baseline sources:

- normal stage/start allocation,
- **slow emergency passive regeneration**,
- meaningful refill from successful offensive play,
- boost pickups/lanes,
- items/upgrades.

Passive regeneration must be too slow to support continuous boost. Its purpose is to prevent an empty meter from producing a long dead-energy state.

Exact rates are **VALIDATE**.

Boost does not require Flow as a primary refill source. If Flow later affects boost, keep the relationship modest to prevent runaway positive feedback.

## 10. Level-up upgrades

Level-ups are frequent capability modifiers.

Presentation:

```text
spin/reveal → 3 randomized results → choose 1
```

Rarity tiers for MVP:

- Common,
- Rare,
- Legendary.

Upgrade categories may include:

- Momentum,
- Boost,
- Impact,
- Air,
- Jump/Slam,
- Survival,
- Economy.

Avoid generic stat soup; each choice should imply a meaningful playstyle consequence.

**VALIDATE pacing target:** approximately 8–15 level selections in a successful run.

Level choices queue during active high-speed play and resolve at safe/inter-stage moments in the first MVP.

## 11. Passive items

Items are rarer behavior-changing passive synergies.

Examples:

- **Kinetic Dynamo** — crush kills restore boost.
- **Meteor Core** — slam landing emits radial damage.
- **Gravity Lens** — improves payoff from favorable downhill travel.
- **Afterburner** — reaches Overdrive-related benefits faster during sustained boost.
- **Slipstream Core** — Flow decays more slowly above Rush.

Items may stack. Every item explicitly defines its stacking behavior:

- additive,
- multiplicative,
- diminishing,
- capped,
- or non-stacking where necessary.

No active-use item system in MVP.

## 12. Currency and reward collection

Run money exists only for shop purchases in MVP.

Enemy/reward currency should not create cleanup behavior.

Preferred presentation:

> reward objects burst visibly, then magnetize/auto-collect toward the player.

This preserves the satisfaction of physical drops without requiring U-turns for coins.

## 13. Shop

MVP shop:

- appears only at designated safe/inter-stage opportunities,
- three offers,
- may include one simple recovery purchase,
- spends run currency.

No selling, crafting, reputation, inventory management, or separate reroll economy.

## 14. Failure and recovery

### Falling/out of bounds

1. detect unrecoverable fall,
2. quickly transition,
3. restore at latest valid checkpoint,
4. apply health penalty,
5. reduce/break Flow,
6. reset momentum to a safe recoverable value.

Fall is not automatic run death.

### Environmental crashes

Normal failure hierarchy is momentum first.

- ordinary collisions primarily cost speed,
- severe, clearly communicated environmental hazards may also damage health,
- bounded response only; no prolonged solver chaos.

### Stuck recovery

A production-safe fallback may restore the latest checkpoint when the player is genuinely unrecoverable. It must not fire merely because the player intentionally stops.

### Run death

Health reaches zero → run ends → recap/meta XP.

## 15. Scoring and run recap

Track at minimum:

- stages completed,
- victory/death,
- time,
- enemies/elites defeated,
- peak speed,
- highest Flow,
- landing bursts,
- damage taken,
- falls/recoveries,
- currency earned/spent,
- items collected.

Final score should remain understandable.

Meta XP derives primarily from progress plus performance bonuses, with no incentive for indefinite safe farming.

Stage spawns/rewards should be finite or otherwise designed so delaying the exit cannot become optimal grinding.

## 16. Meta progression

MVP tree: approximately 12–20 nodes.

Preferred node types:

- unlock item into pool,
- unlock upgrade family,
- unlock terrain/modifier possibility,
- modest starting boost/health/economy options,
- extra shop option,
- small quality-of-life advantage.

Avoid a tree dominated by compulsory tiny percentage gains. Veteran progression should increase possibility more than raw stat superiority.

## 17. Final encounter direction

Accepted MVP direction:

A movement-centric **Bastion-style** stage-nine encounter in which the player builds speed around/through an arena and uses terrain, boost, jump/slam, and impact timing to attack vulnerable targets while avoiding readable hazards.

Do not introduce a separate melee/shooter combat language for the boss.

Exact encounter design is deferred until movement, generation, and combat have passed their earlier gates.
