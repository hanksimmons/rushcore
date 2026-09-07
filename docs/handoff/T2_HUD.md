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

- Branch / commits:
- Harness (full run, count and wall time):
- Files changed:
- What was built:
- Deviations from the packet and why:
- Measurements (allocation delta, text sizes at scale 1, contrast notes per archetype):
- P-entries written:
- Spec sections edited:
- Open items:
- Needs main track:
