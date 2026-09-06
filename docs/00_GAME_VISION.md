# 00 — Game Vision

**Status:** Draft 0.3 — final audited  
**Authority:** Highest product/design authority  
**Owns:** Player fantasy, design pillars, high-level loops, experience targets, non-goals

## Elevator pitch

**RUSHCORE** is a high-speed 3D physics-action roguelite where **movement is combat**.

The player controls a rolling ball across large procedurally generated landscapes. Gravity, slopes, player propulsion, boost, charge-jumping, aerial steering, and ground slams combine into an expressive arcade-physics movement system. The player becomes dangerous by building and preserving speed.

Runs consist of nine stages connected by informed branching route choices. Temporary upgrades, passive items, shops, escalating hazards, enemies, and route rewards create run-to-run variation. Run performance converts into meta-progression.

The intended emotional arc is:

> read the terrain → commit to a line → accelerate → improvise at speed → smash through danger → preserve Flow → take a bigger risk

## Core fantasy

> **Become an unstoppable projectile because you learned to read and exploit the terrain.**

RUSHCORE is not primarily a realistic marble simulator. It uses real 3D physics for causality and physicality, then deliberately applies arcade control where required for responsiveness, readability, and momentum preservation.

## Design pillars

### P1 — Momentum is power

Velocity is simultaneously:

- traversal capability,
- offensive capability,
- route access,
- jump range,
- score potential,
- risk.

The player should care about speed even when no enemy is onscreen.

### P2 — The terrain is the moveset

Terrain geometry is gameplay:

- downhill slopes create acceleration,
- bowls store/release momentum,
- ridges create launch opportunities,
- banks make high-speed turns possible,
- gaps test trajectory and resource management,
- ramps/ridges create risk-reward lines.

Terrain archetypes change strategy through **geometry**, not hidden changes to the player's physics rules.

### P3 — Physical causality, arcade authority

The player must feel gravity, inertia, slope, collision, airtime, and impact.

However, realism never outranks control quality. Steering assistance, charge-jump handling, momentum preservation, controlled landings, and bounded collision responses may deliberately bias the simulation toward fun.

### P4 — Intentional actions preserve useful momentum

Jump, slam, boost, successful enemy impacts, and good landings preserve or add useful momentum unless an action's explicit purpose is to trade speed for control.

A carve action was prototyped as the deliberate exception (control for speed) and removed after playtest.

### P5 — Successful play preserves Flow

Correct play should keep the player moving. Strong enemy hits, good landings, pickup chains, and successful shortcuts reinforce rhythm rather than repeatedly stopping the game.

### P6 — Failure attacks momentum before health

The most common immediate punishment is losing speed, Flow, positioning, boost, or route opportunity. Health is the run-level failure resource, not the only feedback channel.

### P7 — Randomness creates decisions, not nonsense

Procedural generation and roguelike rewards must generate understandable choices.

Randomness must not routinely create:

- untraversable mandatory terrain,
- unreadable combat,
- mandatory blind leaps,
- reward choices with no meaningful distinction,
- failure states that could not reasonably be anticipated.

### P8 — Complexity comes from recombination, not more verbs

> **RUSHCORE should create complexity by recombining movement, terrain, enemy placement, rewards, and build modifiers—not by continuously adding new player verbs.**

The fundamental movement vocabulary is intentionally small:

- steer,
- charge jump,
- slam,
- boost.

## Player experience targets

The MVP should make the player feel:

1. **Fast** — large spaces, long sightlines, meaningful acceleration, strong camera/motion cues.
2. **In control** — tight low-speed control and learnable high-speed turn radii.
3. **Committed** — charging a jump or taking a fast line creates meaningful temporary constraints.
4. **Physically grounded** — slopes, gravity, jumps, impacts, and landings have clear consequences.
5. **Skillful** — better lines, charge timing, landing-burst timing, and momentum management visibly improve outcomes.
6. **Greedy** — optional rewards/shortcuts tempt the player away from safer lines.
7. **Build-aware** — upgrades change how existing verbs interact without replacing mechanical mastery.
8. **Eager to retry** — death creates a clear recap and a reason to try a different route/build.

## High-level loop hierarchy

### Second-to-second

Read terrain → steer/accelerate → charge jump/slam/boost → attack or evade → preserve momentum.

### 10–30 seconds

Build Flow → choose a line → chain enemies/pickups/terrain features → recover boost → attempt a shortcut or recover from a mistake.

### Stage

Enter → move generally forward through a broad landscape → fight opportunistically → gather rewards → reach exit.

### Between stages

Resolve queued level choices/rewards/shop state → inspect two informed route options → choose risk/reward path.

### Run

Complete eight procedural stages → complete stage-nine final encounter → receive score/meta XP.

### Meta

Spend meta XP on a small permanent tree that mainly unlocks possibilities plus modest starting advantages.

## Reference inspirations

References are design language, not cloning targets.

- Sonic: speed fantasy, line choice, terrain-assisted momentum.
- Rocket League: readable arcade physics, jump/air influence/boost interplay.
- Snowboarding/racing games: carving, commitment, line reading, terrain use.
- Hades: staged run structure and informed branching choices.
- Risk of Rain: stackable passive-item synergies and escalating run power.
- Rogue Legacy: run recap and meta-progression feedback.

## Non-goals

The MVP is not:

- a realistic marble simulator,
- an open world,
- an endless procedural world,
- a precision platformer,
- a bullet-hell game,
- a character-action combo game,
- a dialogue/narrative RPG,
- multiplayer,
- a live-service content treadmill.

## Design filters

Before adding a feature:

1. Does it strengthen movement, momentum, terrain reading, or risk/reward?
2. Does it interact with an existing system rather than create a parallel minigame?
3. Can the player understand why it succeeded or failed?
4. Can we test whether it improves the experience?
5. Is it necessary for the current vertical slice?
6. Is there a smaller implementation that captures most of the value?

If the answer pattern is weak, defer it.
