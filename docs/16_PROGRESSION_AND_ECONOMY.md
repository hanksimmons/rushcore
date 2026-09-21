# 16 — Progression and Economy: experience, cash, the magnet, the stat ladder, the found shop

**Status:** Draft 0.1 — the user's design of 2026-09-20 (D-119), written before its first slice
**Authority:** Owns run progression and the run economy in the MVP. Where this conflicts with `01 §Run progression`,
`01 §Economy/shop`, `02 §10`, `02 §12` or `02 §13`, this document wins: it records the user's later call, and those
sections are amended to point here.
**Inputs:** the frozen movement baseline (`03 §15`, D-095), the boost economy (D-106), the stage generator (`04`),
the pickup shapes (`06 §9`), the HUD (`06 §12`).

## 0. The brief, in the user's words

- **Experience** is small teal orbs the player picks up. **Cash** is gold balls the player picks up.
- The ball has a **magnetic radius**, like Vampire Survivors: what comes inside it flies to the ball.
- **Levelling is strict**: level 1 → 2 → 3 → 4 → 5 → 6 → 7 → 8, the player starts at 1. On a level-up the player
  **chooses a stat to upgrade**. Every stat has its normal baseline and can be upgraded **up to 3 times**: Max Speed,
  Acceleration, Turn Radius (both low and high speed), Ball Size, Jump Height, Slam Boost amount, Auto Boost Refill
  (none to start; rank 1 very very slow, rank 2 very slow, rank 3 slow, since boost is overpowered with too much of it),
  Hangtime (lower gravity, moon physics).
- The **shop** is different: it has items, bought with cash (the items are brainstormed later). It is reachable only when
  **found in a map**, tucked away on an occasional route, about every three stages and never every stage. A player who
  does not find it does not shop.

## 1. Principles

- **Movement stays fun without progression** (`01 §MVP quality gates`): the baseline is rank 0 of every stat and is the
  frozen D-095 preset, untouched. An upgrade is a multiplier read beside the tuning value at the point of use; the
  tuning, its presets and the harness's rank-0 drives never see it.
- **KISS**: one rank array, one magnet loop, one choice panel, one pad. No rarity, no rerolls, no random offers for
  stats (the user's call replaces `02 §10`'s three-result spin for the stat ladder; passive items, when they come,
  stay the rare behaviour-changers of `02 §11` and are the shop's stock).
- **Deterministic**: pickups and the shop are placed from the stage seed; the stage hash (geometry) does not see them.
- **Never a price on momentum** (`02 §12`): nothing here slows the ball; the magnet brings the reward to the line.

## 2. Pickups

| Kind | Shape (06 §9) | Worth | Where it lies |
|---|---|---|---|
| Experience orb | a small teal sphere, 0.5 m, bobbing, `PickupKind.Reward` recoloured teal | 1 XP | in **clusters of 3–5** along the primary every `OrbClusterSpacing` 150 m, offset laterally inside the level width so a line through them is a choice, not the racing line; along every optional line at twice the density (the line is the reward's road); none inside a module's body, on a pad, under a lid, in a tunnel's covered run or within 60 m of the start |
| Cash ball | a gold sphere, 0.8 m, spinning, `PickupKind.Currency`'s gold | 1 cash (a burst coin's worth) | **rarer and earned**: one on each optional line's plateau or floor at its middle, one past every ramp's lip on the paid path's landing, one at the far rim of every gap, one in every tunnel's covered run; a small scatter (`CashScatterPerKm` 1 per km) on the primary |

Both are placed by `WorldDressing` with the stage (a `pickups` stream derived from the stage seed, `SeedChain.Derive`),
as the boost rings are, and read the same keep-outs as scatter (`StageScatter`) plus the corridor rule above. Counts are
per stage and scale with length (an 18 km stage carries about 400 orbs and 40 cash balls at the provisional numbers).

**The field.** One `PickupField` node per stage: a `MultiMesh` per kind, plain arrays of position, state and phase, no
physics bodies, no colliders (the burst of P-007 is the model). Every physics tick it walks the live pickups: inside
`MagnetRadius` of the ball a pickup is **magnetised** and thereafter chases the ball at
`max(MagnetSpeed, ball speed + MagnetClosingMargin)` until it is within `CollectRadius`, when it is collected (a
pulse on the HUD counter, a small VFX at the ball). A magnetised pickup never gives up: at the cap the ball outruns
nothing. The reward burst (`RewardBurst`) adopts the same chase rule, which closes the open T3 item (its coins could
never catch a ball above 60 m/s).

## 3. Experience and levels

- `Level` starts at 1 and ends at 8. XP to the next level is `XpPerLevel × level` (10, 20, … 70: 280 XP to reach 8).
  Surplus XP past level 8 does nothing (no prestige, no wrap).
- Level-ups **queue** and resolve at a safe moment (`02 §10`, unchanged): the stage's completion outro, before the next
  stage builds. The **choice panel** lists the eight stats with their rank pips (0–3) and the effect of the next rank in
  words; one choice per queued level; a stat at rank 3 is greyed. Escape is not an option: a queued level is chosen
  before the next stage (a run is the only place ranks live).
- **Run death** wipes level, XP, ranks and cash (`01 §Run failure/meta`). A new run starts at level 1, rank 0.
- The HUD (`06 §12`) shows the level and an XP bar at top-left beside the stage counter, and the cash count where the
  currency label already is; both pulse on a collection.

## 4. The stat ladder (V-018, the user's numbers; provisional)

Each stat has three ranks. Rank 0 is the frozen baseline. A rank is a multiplier or a value read at the point of use
(`UpgradeState`, owned by the run; `PlayerPhysics` reads it beside the tuning):

| Stat | Tuning it reads beside | Rank 1 | Rank 2 | Rank 3 | Notes |
|---|---|---|---|---|---|
| Max Speed | `HardMaxLocomotionSpeed` (the base cap) | ×1.06 | ×1.12 | ×1.18 | the effective cap of D-088 multiplies the upgraded base; steering saturates at the upgraded base |
| Acceleration | `GroundDriveAcceleration` | ×1.15 | ×1.30 | ×1.45 | ground propulsion only; slopes and boost unchanged |
| Turn Radius | `GroundSteeringLateralAccel` and `HighSpeedSteeringMultiplier` | ×1.12 | ×1.24 | ×1.36 | both low and high speed, as asked |
| Ball Size | `BallRadius` | ×1.12 | ×1.24 | ×1.36 | collider, visual and the camera's clearance; generation sizes (corridor 75 m, tunnel 30 m wide) are untouched |
| Jump Height | `MinJumpTakeoffVerticalSpeed`, `MaxJumpTakeoffVerticalSpeed` | ×1.10 | ×1.20 | ×1.30 | height goes with the square: +21 / +44 / +69% |
| Slam Boost | `LandingBurstMultiplier` (1.15) | 1.20 | 1.25 | 1.30 | the documented ceiling of the burst (D-088: 1.0–1.3); still limited by the effective cap |
| Auto Boost Refill | `PassiveBoostRegen` (0) | 0.5 /s | 1 /s | 2 /s | of the 100 capacity (drain 30 /s): 200 s, 100 s, 50 s to fill from empty |
| Hangtime | `Gravity` | ×0.90 | ×0.80 | ×0.70 | moon physics: the follow's launch threshold (v²κ against g) moves with it, so an upgraded ball leaves gentler crests, by design |

All twenty-four values are `Upgrades` sliders in the tuning panel (`07`), so the user tunes them in play; the harness
drives at rank 0 and checks each rank's multiplier at its read point.

## 5. The found shop

- **Which stages:** the run decides from its seed (`SeedChain.Derive(runSeed, "shop", stageIndex)`) with chance
  `ShopChance` 1/3 per stage, never on two consecutive stages and never on the first; over a nine-stage run that is two
  or three shops. The stage definition carries `HasShop`; the stage hash does not see it (it is a placement).
- **Where:** tucked away. The shop pad (a 12 m level disc with a small kiosk silhouette, `PickupKind.Item`'s frame at
  scale, a lantern glow) stands **40 m to the outside of an optional line's plateau at its middle** (the first line with a
  plateau: a ridge, a terrace floor, a dive's cap over its run); the primary never sees it behind the ridge, and a player
  on the line sees the glow. A stage whose lines are all terminal or tunnels puts it on the primary's outside 300 m
  before the exit zone, behind the first prop cluster. It is never on a pad, a module or an anchor.
- **Entering** the pad (the ball inside its radius, any speed) opens the **shop panel** and holds the run (the same hold
  as the choice panel): three offers from the item pool, each with a cash price, and one **recovery purchase** (health
  to full, `02 §13`); buy any number the cash allows; close to continue. The pad stays open while the stage lasts.
- **Stock:** the item pool is brainstormed later (the user's). Until then the pool holds two placeholders so cash has a
  use: the recovery purchase, and a **boost refill** (the meter to full, 5 cash).

## 6. Harness (08 §5 additions)

- every batch seed places orbs and cash balls inside the count bounds for its length, none in a keep-out, none inside a
  tunnel's covered run, none within 60 m of the start;
- the drive over an orb cluster at the cap collects at least 90% of it (the magnet catches a ball it cannot outrun);
- XP thresholds: 10 + 20 + … reach level 8 exactly at 280 XP, and XP past it is idle;
- each rank multiplies its read point: a rank-3 Max Speed ball reaches 148.5 × 1.18 on the runway, a rank-3 Hangtime ball
  flies a full charge 1/0.7 as long, a rank-3 Auto Refill meter fills from empty in 50 s at rest;
- a run's shop stages are deterministic, never consecutive, never the first, two or three of nine on the default seed;
- the shop pad stands off a line's plateau, out of every keep-out; entering it opens the panel, buying deducts cash,
  closing resumes the run;
- the rank-0 drives (every existing check) are unchanged: the harness's numbers do not move with this document.

## 7. Slices

- **P1 — pickups, the magnet, XP, levels, the stat ladder, the choice panel, the HUD.** One commit; D-119.
- **P2 — the found shop** with the two placeholder purchases; the item pool is a later brainstorm with the user.

## 8. Decisions the user owns

- every number in §4 (V-018) and the pickup counts in §2, on play;
- whether a level-up is chosen at once (a pause mid-run) instead of at the stage's end;
- the item pool, and whether a shop can sell a stat rank.
