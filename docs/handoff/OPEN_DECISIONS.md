# Open decisions — waiting on the user

**Status:** live ledger, opened 2026-09-07  
**Purpose:** every decision this track has deferred because it needs the user's judgement, in one place, each with
what was measured, what the options are, and which line of code carries it. Nothing here is blocking other work;
everything here is cheap to act on once the verdict exists.

**How to use it:** answer any subset, in any order. Write the verdict inline under the entry, or say it in a session
and the agent will record it. When an entry is closed, mark it `DECIDED (date): …` and leave it in place — the
record of what was chosen is worth more than a tidy list.

**Related ledgers.** `PROVISIONAL_DECISIONS.md` holds every P-number the parallel track has proposed; those become
accepted only when the main track promotes them to D-numbers with the user's verdict (`README.md` §8), so the whole
file is technically pending. The entries below are the ones where a *choice* is open rather than a proposal awaiting
a rubber stamp. `STATUS.md` holds the packet-by-packet state.

---

## A. Needs your eye in play

### A-1. Dune Sea scatter is very sparse — accents, or an empty stage?

About 190 instances across a 6 × 2 km stage (133 stones, 54 tufts), against 1264 on Rolling Highlands. That is the
packet's rule — crests only, sparse — and the crest ribbons are a small part of the footprint, so the rule and the
emptiness are the same thing.

- **Lever:** the `DuneSea` branch of `WorldDressing.Suits` — the crest threshold (`DuneHeight > 0.6 × wave height`),
  the slope limit (0.12) and the sparsity lottery (0.7). First cut was 0.8 / 0.09 / 0.45, which gave 100 instances.
- **If it only needs more,** it is a number. **If the swales should carry scatter too,** that is a rule change and
  wants your word.
- **To judge:** `World › Sample Stage` = 4, drive the trains at the cap.

### A-2. Canyon Run puts everything on the wall tops and carries no crystals — canyon, or fence?

1458 slabs and fins, every one above 60% of the wall height, none on the slot floors, zero crystals. Both are the
packet's rule, so the question is whether the rule is right: a bare floor beside the corridor with a dense rim may
read as a canyon or as a fence.

- **Lever:** the `CanyonRun` branch of `WorldDressing.Suits` — it sets `crystal = false` unconditionally and gates on
  `y - Relief > WallHeight * 0.6`. Allowing crystals, or lowering that fraction so scatter walks down the wall face,
  are each one line.
- **To judge:** `World › Sample Stage` = 2.

### A-3. Frame cost of the scatter at density 0.19 and 1.0

Cannot be measured from this track at all: the harness is headless, so there is no renderer to time, and no AI agent
plays stages. What is measured instead is instance counts, collider counts and scatter milliseconds per archetype
(the table in `T4_PROP_SCATTER.md`).

- **To judge:** `godot --path . -- --seed 8`, `F2` for telemetry, read the `frame` row at `World › Prop Density`
  0.19, then set 1.0 **and click Restart Same Seed** — the slider only takes effect on a rebuild — and read it again.
  At 1.0 the stage carries 2700 instances, the caps.
- **If 1.0 costs frames,** the caps (`WorldDressing.RockCap` / `CrystalCap` / `MarkerCap`) are the dial.

### A-4. The stage-completion outro: "done", or a freeze?

Reaching any exit pad locks the controls, flashes the pad's sign and a ring while the ball rolls free (0.7 s), fades
(0.5 s), builds the next stage, and fades back in (0.4 s). Whether that reads as an ending, and whether anything of
the finished stage shows through the fade, is the manual half of T1's acceptance.

- **Lever:** `Run › Outro`, `Run › Fade Out`, `Run › Fade In` (P-003 defaults). Presentation pacing, not feel values.
- **To judge:** `--seed 4`, then `E` to land 200 m short of exit A and roll in.

### A-5. HUD contrast at speed, on all four grounds

The HUD is ink with an outline rather than a plate behind text. Grass, red rock, sand and cloud white are the four
backgrounds it has to survive at the cap.

- **Lever:** the palette constants at the top of `src/UI/PlayerHud.cs`, and `HUD › Scale`.
- **To judge:** cycle `World › Sample Stage` 6 → 2 → 4 → 3 at speed.

### A-6. The ball rides about 8 cm off the tube glass — does it still read as floating?

The tube follow holds the ball clear of the shell by its rest band plus the axis reference's own uncertainty. That
was 12 cm; interpolating the axis (P-016) brought it to about 8 cm. Whether the remaining float is visible at speed
is a judgement.

- **Lever:** `GroundFollowDeadband` and `TubeAxisUncertainty` in `PlayerPhysics`, which now stand at 3 cm and 1 cm.
- **To judge:** `World › Sample Stage` = 1, boost through and watch the contact point.

### A-7. The enemy silhouettes have never been seen at the cap

T3's packet asked the implementing agent to tune the enemy sizes on the runway at the cap and record what it settled
on. That could not be done: the harness is headless and no AI agent plays stages, so the sizes shipped are the
packet's own starting points, unjudged. What the harness can say is that they exist, carry no collider, stand clear
of the lane and draw off twelve shared materials.

- **Delivered sizes (P-017):** Pylon 0.5 m across × 2.6 m tall; Bulwark 3.2 × 1.8 × 1.6 m, leaning 8°; Strider a
  2.4 m body 1.6 m up on blade legs; Shooter a 1.2 m core with a barrel and a ring, 2.4 m overall; elite = the same
  silhouette at 1.3× under a halo.
- **The read to make:** does the Bulwark say "not at this speed" and the Pylon say "ram it" *before* you are on
  them (02 §7)? Is the Strider's crossing axis obvious? Is the elite unmistakable at a glance?
- **Lever:** the size constants in `EnemyVisual.Create`'s four builders, and `Height`.
- **To judge:** `World › Enemy Showcase` on, then drive the lab lane past the row at the cap.

### A-8. A reward burst can never catch a ball at speed — is 60 m/s the right magnet?

Measured: twelve coins thrown 30 m to the side of a ball travelling at 73 m/s collected **0 of 12** and the burst
freed itself at its 6 s limit. The magnet's ceiling is P-007's 60 m/s and the ball's cap is 148.5 m/s, so a coin
that is not already on the ball's line is simply lost. Parked, the same burst collects all twelve in well under a
second.

- **What this means:** a burst has to be thrown *on* the ball's line, which is exactly where a crush puts it. It is
  not a bug so much as a constraint on where Phase 4 is allowed to spawn one.
- **Options:** leave it (crushes spawn on the line anyway); raise `RewardBurst.MagnetSpeed` above the cap so a coin
  can always catch up; or give the magnet a lead on the ball's velocity rather than its position.
- **Lever:** `RewardBurst.MagnetSpeed`, currently 60 m/s (P-007).
- **To judge:** `Burst 12 Coins` from the panel while boosting, and the showcase row's burst pad at speed.

---

## B. Needs your call on approach

### B-1. The tube-camera guard measures frame timing as well as the snap it was written to catch

`the tube camera does not snap along the axis sample grid` bounds the worst change in the lens's step between ticks
at 3.5 m on the sky tube ride. It is sampled per rendered frame against an interpolated player transform, so it moves
with how much work a frame did. Two runs of `develop-secondary` at 004894b with **no changes at all** measured
3.37 m and 3.65 m: one passed, one failed. It has more margin since the axis fix (2.30 m on sky, 1.85 m default,
0.84 m dunes), so it is not currently red — but the metric is still wrong in kind.

- **Options:** (a) re-express it per physics tick; (b) measure the lens's *station along the tube* directly, which is
  the quantity the original snap actually moved; (c) leave it and stop asserting it, reporting the number only.
- **Recommendation:** (b). It is what the guard was written to catch, and it is immune to frame timing.

### B-2. The tube arrest: pursue, or leave it?

One tick in roughly 800 travels more than 10% short of the ball's own velocity inside a tube, none of them near the
bottom, and you reported the ride as smooth after T7. It has never been diagnosed to a cause.

- **Options:** leave it reported-not-asserted as it is now; or spend a session on it.
- **Note:** it is not the entry collision you reported — that was the mouth flare, and it is fixed.

### B-3. What `develop-secondary` needs before it merges to `develop`

You said the branching exits "definitely need refinement before I am comfortable merging". `develop` is still at
a793dbe. `develop-secondary` has since gained T1, T2, T4, the tube fixes and the golden hashes.

- **Open:** what "refined" means for the exits — fork readability, spacing, how often a stage gets two or three, the
  archetype rotation the forks now drive (P-011).

---

## C. Main track only — generation, and therefore stage hashes

### C-1. The 12 cm lip at a tube mouth

`TubeBuilder.Make` sets the mouth axis height from the *route vertex* (`y = pv.Position.Y + R + cruise * e`) while the
ground under the mouth is sampled at the tube's lateral offset; the two differ by 12 cm on the tube sample, and the
validator tolerates up to a metre (`if (y - ground < R - 1f) ok = false`). It is what is left of the entry collision
you reported: rolling in at the cap now costs 2% of the entry speed, down from 16%, and this lip is the 2%.

- **Fix:** take the mouth height from the ground under the mouth point, or tighten that validator at the mouths.
- **Why deferred:** either changes tube geometry and therefore every stage hash, which this track may not do.

---

## D. Sequencing

### D-1. What the parallel track takes next

- **T3** (enemy and pickup visuals) is **delivered** on `opus/t3-visuals` (2026-09-08). It left A-7 and A-8 above.
- **T5** (generation measurements) is isolated and can go at any time, and is now the only packet left that is not
  waiting on you.
- **T6** (sample-stage presentation fixes) opens as soon as playtest notes exist. Two have landed and are done: the
  tube entry collision and the speedometer.
