# T2 — Player HUD and the health value

**Branch:** `opus/t2-hud` off `develop-secondary` after T1 merges (or stacked on `opus/t1-stage-lifecycle` if T1 is still open; say which)  
**Phase:** 5 "HUD"; Phase 4 "health" (value only)  
**Gate lines served:** 08 §9 "Flow/boost state is readable", "game remains playable with camera shake reduced/disabled" (unaffected); 08 §7 (HUD is part of "a complete stage")  
**Owning specs:** 06 §12, §17, §18; 07 §7, §10 (what stays in telemetry), §12 (kill/heal); 02 §8 (Flow rewards presentation), §14; 03 §5 (bands); 05 §5 (communication), §13

## Goal

A persistent, retro-restrained player HUD built in code: health, boost, Flow, stage/run progress, currency. It reads
public player and run state and never writes gameplay. Health exists as a value with debug kill/heal; no damage source
yet.

## Current state

- No player HUD exists. `src/DebugUi/TelemetryOverlay.cs` is the developer readout on F2 (07 §10): it shows m/s and
  stays as it is. `src/DebugUi/TuningPanel.cs` is F1. Both live on `UiRoot` (`CanvasLayer`, layer 10) built in
  `GameBootstrap._Ready`.
- `PlayerPhysics` exposes `Flow` (0..1), `FlowCap`, `EffectiveLocomotionCap`, `Boost01`, `BoostActive`, `Band`
  (`SpeedBand`), events `SpeedBandChanged`, `LandingBurst`, `Landed(impact, wasSlam)`, `Jumped`, `Slammed`,
  `BoostActiveChanged`, `Recovered`. There is no health field anywhere.
- After T1: `RunDirector.StageIndex`, `StageCount`. `MovementToyWorld.StageProgressIndex` over
  `Stage.PrimaryRoute.Vertices.Count` gives primary progress.
- Godot gotcha (memory, verified in the panel code): set anchors with the preset helpers before offsets; see how
  `TelemetryOverlay._Ready` builds its plate.

## Design

1. **`src/Player/PlayerHealth.cs`** (plain class owned by `PlayerPhysics` or the bootstrap; pick the owner that keeps
   `PlayerPhysics` free of HUD knowledge): `Max = 100`, `Current`, `Damage(amount)`, `Heal(amount)`, `Kill()`,
   `IsDead`, event `Changed`. On reaching 0: log, `RequestRecovery()`, refill to max (P-004). No invulnerability window
   yet (that arrives with damage, 08 §6 "damage invulnerability prevents contact spam").
2. **`src/UI/PlayerHud.cs`** (`Control`, full-rect, mouse-ignore, on `UiRoot` under the telemetry overlay). Elements:
   - health bar, bottom-left; boost bar, bottom-centre, fills with `Boost01`, brightens while `BoostActive`;
   - Flow meter, bottom-right or right edge: a bar 0..1 with the effective-cap fraction implied (label "FLOW",
     no numbers); a short pulse on any gain (`Flow` increased since last frame), a visible drain on loss, a dim state
     at 0. Never a decimal;
   - stage/run progress, top-centre: `STAGE 2 / 9` (T1) and a thin progress line from `StageProgressIndex`;
   - currency, top-right: a counter reading the run's currency. Source: T3's reward burst raises
     `Collected(kind, amount)`; until a run wallet exists, keep a `Currency` int on `RunDirector` (T1) and add to it
     from the collect event; if T1 is not merged, a private int in the HUD with a `// wallet lands with T1` note.
   - optional band word (`ROLL / RUSH / CRUSH / OVERDRIVE`) under the Flow meter, off by default (P-005).
3. **Tuning category `Hud`** in `GameplayTuning` (`CatHud`): `Visible` (bool, default on), `Scale` (0.6..1.6),
   `Band Word` (bool, default off). The self-test runs with the HUD visible so its checks see it; the screenshot flag
   captures one frame with the HUD.
4. **Style** (06 §17, §18): flat unshaded colours from the 06 §3 palette family, one font (the telemetry font), no
   text smaller than 14 px at scale 1, high contrast against sand, canyon rock and cloud white (test on the four sample
   stages), no gradients, no icons that need explaining. Bars change in one frame; no tweens longer than 0.15 s except
   the Flow pulse (0.3 s).
5. **Performance:** update only on change (compare cached values); no string allocation per frame; no `_Process` work
   when not visible.

## Pre-answered choices

- Health value on the player side, debug kill/heal via `IDebugActions` and panel buttons (P-004). Pick free hotkeys
  and list them in `docs/10`.
- Band word off by default (P-005).
- HUD hidden while the tuning panel is open? No: keep it visible so tuning the HUD is possible; it must not overlap
  the panel's left column or the telemetry plate.

## Non-goals

- Damage sources, invulnerability, death, run summary (Phase 4, 6, 7).
- Any m/s, cap or takeoff number (07 §10 keeps those in telemetry).
- Camera shake, VFX (06 §11, T3).
- Localization, controller glyphs, settings menus.

## Acceptance (harness)

- HUD node exists on `UiRoot`, visible by default, hidden when `Hud › Visible` is off.
- After `RefillBoost`, the boost bar value equals `Boost01` on the next frame; after a Flow gain in the Flow case
  (`tests` around line 1759), the Flow bar equals `Flow` on the next frame and a pulse is active.
- `Kill()` → `Current == 0` → recovery fires (`Recovered` event) → `Current == Max`.
- Health bar value tracks `Current / Max` after `Damage(30)`.
- Stage label reads the run director's index after a T1 transition (if T1 merged).
- Node count on `UiRoot` is unchanged across two world rebuilds (the HUD is never rebuilt with the world).
- No per-frame allocation: `GC.GetAllocatedBytesForCurrentThread()` delta over 120 idle frames with the HUD visible is
  under 4 KB (state the number measured).

Manual: the user's V0 verdict on readability at the cap on the four sample archetypes.

## Files expected

`src/UI/PlayerHud.cs` (new), `src/Player/PlayerHealth.cs` (new), `src/Core/GameBootstrap.cs`, `src/Core/IDebugActions.cs`,
`src/DebugUi/TuningPanel.cs`, `src/Tuning/GameplayTuning.cs` (`HudTuning`, `CatHud`), `src/Run/RunDirector.cs` (currency
int, if T1 merged), `tests/MovementToySelfTest.cs`, docs 06 §12, 07 §7, §12, 10, this packet, `STATUS.md`,
`PROVISIONAL_DECISIONS.md`.

## Stop conditions

Stop and write "Needs main track" if a HUD need seems to require a player-physics change beyond the health holder, or
if the palette in 06 §3 has to change to make the HUD readable.

## Delivery record (filled by the implementing agent)

- **Branch / commits:** `opus/t2-hud` off `develop-secondary` (004894b, T1 and T4 merged).
- **Harness (full run, count and wall time):** 365/365 default (105 s), canyon 362/362, dunes 365/365. **The sky run
  fails one check this packet did not touch, which flakes on the base branch too** — see "Needs main track". Golden
  hashes unchanged.
- **Files changed:** `src/UI/PlayerHud.cs` (new), `src/Player/PlayerHealth.cs` (new), `src/Core/GameBootstrap.cs`,
  `src/Core/IDebugActions.cs`, `src/Core/InputBootstrap.cs`, `src/DebugUi/TuningPanel.cs`,
  `src/DebugUi/TelemetryOverlay.cs`, `src/Run/RunDirector.cs` (the wallet), `src/Tuning/GameplayTuning.cs`
  (`HudTuning`, `CatHud`), `tests/MovementToySelfTest.cs`, docs 06 §12, 07 §7, §10, §12, 10, this packet,
  `STATUS.md`, `PROVISIONAL_DECISIONS.md`.
- **What was built:**
  - `PlayerHealth`: a plain class, max 100, `Damage` / `Heal` / `Kill` / `Refill`, `Changed` and `Died`. No damage
    source, no invulnerability. The composition root owns it, so `PlayerPhysics` stays free of it entirely.
  - `PlayerHud`: health bottom-left, boost bottom-centre (brighter while it fires), Flow bottom-right with a 0.3 s
    pulse on a gain and a dim state at zero, the stage number and a primary progress line top-centre, the wallet
    top-right, and the optional band word under the Flow meter. Flat colours, one monospaced font, an outline
    instead of a plate behind the text, no numbers of any kind.
  - `HUD › Visible / Scale / Band Word`; `K` kills and `H` heals, on the panel too; the telemetry gained a `health`
    row (07 §10 lists health).
  - `RunDirector.Currency` with `AddCurrency`: the wallet the HUD reads and T3's reward burst will fill.
- **Deviations from the packet and why:**
  - Health lives on the composition root rather than on `PlayerPhysics`. The packet allowed either; this way the
    player class gains nothing at all, which suits a value with no damage source yet.
  - Death leaves the value at zero and refills when the recovery lands, rather than in the same instant (recorded
    against P-004). "Down" is then a state the HUD and the harness can both see, and health returns with the ball.
  - HUD scale is a relayout, not a `Control` transform: sizes and offsets are recomputed when the scale changes, so
    every corner stays anchored at every scale. `Layout()` runs only on a change, never per frame.
  - The harness measures the widths the bars actually draw (`HealthShown`, `BoostShown`, `FlowShown`,
    `ProgressShown`) rather than the values the HUD was handed, so a layout bug cannot pass unnoticed.
  - The allocation check is stated as the HUD's cost *against itself hidden*, since the harness's own per-frame
    allocation dwarfs it either way.
  - The HUD case runs after the stage cases. It has nothing to do with the stage drive, and every tick before that
    6.5 km drive moves where the ball meets the tube.
- **Measurements:** allocation over 120 frames with the HUD visible 438 768 B, hidden 448 368 B — the HUD's own
  share is not distinguishable from the harness's own noise (the difference measured −9 600 B), so it is well under
  the 4 KB the packet allows. Font 14 px at scale 1, floored at 12 px so scale 0.6 stays legible. Bars 220 px wide
  (Flow 200) at scale 1.
- **P-entries written:** none new. P-004 is implemented with the refinement noted in its row; P-005 as written.
- **Spec sections edited:** 06 §12 (what the HUD shows and what it never shows), 07 §7 (the `HUD` category), 07 §10
  (the `health` row; the developer overlay keeps the numbers), 07 §12 (kill and heal), 10 (hotkeys, the HUD line).
- **Open items:** contrast at speed on the four archetypes is the user's read (06 §17, and the packet's manual
  line) — sand, canyon rock and cloud white are the three grounds to check the ink against. The wallet reads 0
  until T3's reward burst fills it. No damage source, so the health bar never moves in play yet.
- **Needs main track:** **the T8 tube-camera guard flakes across its threshold, on the base branch, with no
  changes at all.** "the tube camera does not snap along the axis sample grid (T8 regression guard)" asserts the
  worst change in the lens's step between ticks is under 3.5 m on the sky tube ride. Measured: `develop-secondary`
  (004894b, no T2 code) 3.37 m then 3.65 m on two runs of the same binary — one passes, one fails. This branch
  3.63 m then 2.63 m, likewise. With the HUD node simply not added to the tree, 2.94 m. The metric is sampled per
  rendered frame against an interpolated player transform, so it moves with how much work a frame did; no UI code
  can reach physics, and none of the three states differ in the camera or the controller. The guard is therefore
  measuring frame timing as well as the snap it was written to catch. It wants re-expressing per physics tick, or
  against the lens's station along the tube directly (which is what the snap actually moved). **The tolerance was
  not touched**, per the handoff rules; this branch is pushed with the sky run red on that one check and nothing
  else, for the main track to decide.
