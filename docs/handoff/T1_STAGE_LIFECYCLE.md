# T1 — Stage completion, transition, run seed and stage index

**Branch:** `opus/t1-stage-lifecycle` off `develop-secondary`  
**Phase:** 5 (partial): "start/exit", "completion transition", "clean stage lifecycle" (also Phase 6's "clean stage lifecycle")  
**Gate lines served:** 08 §7 "starts deterministically", "supports fall recovery", "exits cleanly without previous-stage leakage"; 08 §10 "no unbounded node growth across stages", "stage generation transition instrumented in milliseconds"  
**Owning specs:** 02 §1, §2, §14; 05 §7, §9, §10 (teleport/interpolation rule); 06 §13; 07 §12; 08 §7, §10

## Goal

Reaching the exit pad ends the stage: immediate feedback, a short transition, and a rebuild into the next stage of the
same run (same run seed, stage index + 1) with no node, event, clock or checkpoint leakage. The run seed and stage
index become explicit runtime state instead of the hard-coded stage 0.

## Current state (read these first)

- `src/Core/GameBootstrap.cs` `_Ready` builds world, player, camera, UI; `_seed` is one `int`; `RestartSameSeed` /
  `RestartNewSeed` / `TeleportToStart` are the only lifecycle actions; the kill plane check at line 163 calls
  `RecoverPlayer()`; `--seed N`, `--canyon`, `--dunes`, `--sky` flags; the sample-stage block in `_Process`.
- `src/World/MovementToyWorld.cs` `Build()` generates `new StageGenerationRequest(Seed, 0, Archetype)`: the stage index
  is always 0. `UpdateStageProgress` sets `StageExitTime` and `StageExitIndex` once when the ball is within
  `WorldScale.PadRadius` of any pad in `Stage.Exits` (D-105: index 0 is the primary's, A; the rest are terminal lines'
  pads, B and C) and prints; nothing else happens. `ResetStageProgress`, `StageClock`, `StageCheckpointIndex`.
  `Regenerate(seed)` rebuilds terrain and dressing; `_dressing.Rebuild()`.
- `src/Generation/StageData.cs` `StageGenerationRequest(RunSeed, StageIndex, Archetype, DifficultyScalar)` with
  `StageSeed = SeedChain.Derive(RunSeed, "stage", StageIndex)`: the seed chain already supports a stage index.
- `src/Player/PlayerPhysics.cs` `TeleportTo`, `SetCheckpoint`, `RequestRecovery`, `Recovered` event; `Flow`;
  `RefillBoost`. The self-test injects input ahead of the player (see `MovementToySelfTest` and `MoveChild(selfTest, 0)`).
- `src/Core/IDebugActions.cs` and `src/DebugUi/TuningPanel.cs`: the panel's action buttons.
- `src/DebugUi/TelemetryOverlay.cs` row `Seed` shows `SeedText` plus `StageSummary`.
- `tests/MovementToySelfTest.cs` from line 2000: the generated-stage case drives the whole primary with the follower and
  already checks node count flat over regenerations. Extend that case; do not write a new driver.

## Design

Keep it to three pieces.

1. **`src/Run/RunDirector.cs`** (plain class, no Node): `RunSeed` (int), `StageIndex` (int), `StageCount = 9`
   (02 §2), `Advance()` (index + 1, wraps to 0 with a `[RUSHCORE] run complete (placeholder)` print), and
   `Request(TerrainArchetype a) => new StageGenerationRequest(RunSeed, StageIndex, a)`. Nothing else: currency, XP,
   route choice and death are later phases (05 §7 lists them; do not add fields for them).
2. **World takes the request.** `MovementToyWorld.Regenerate(StageGenerationRequest)` replaces the `(seed)` overload for
   stages (the lab and strip keep the seed). `SeedText` shows `run/stage` (the telemetry row already reads
   `N/0`; make the index real). `--stage N` launch flag sets the starting index. Sample stages keep stage 0.
3. **Completion in `GameBootstrap`.** `MovementToyWorld` raises `StageCompleted` once per build when `StageExitTime` is
   first set. The bootstrap runs a small outro:
   - lock controls: one `ControlsLocked` bool on `PlayerPhysics` that zeroes the steering, jump, boost and carve input
     where it is read (one place), cancels a charge in progress, and touches nothing else. The ball rolls free.
   - exit feedback: pulse the `EXIT` sign (scale or emissive) and fire one `PlayerVfx`-style burst at the pad
     (06 §10 "exit"). Reuse `PlayerVfx` materials if the burst lives there; otherwise a tiny one-shot node.
   - fade: a full-screen `ColorRect` on `UiRoot` (below the tuning panel) driven by a timer.
   - rebuild: `RunDirector.Advance()` (record which exit index was taken; Phase 6 will hash it into the next request),
     `world.Regenerate(director.Request(world.WantedArchetype))`,
     `TeleportToStart()` (which already resets progress, checkpoint, camera yaw and teleports; confirm it resets
     physics interpolation per 05 §10, add it if not), Flow → 0, boost carried (P-001), fade in, unlock.
   - timings are three floats in a new `RunTuning` category (`Run › Outro`, `Fade Out`, `Fade In`), defaults per
     P-003. They are not feel values; they are not part of the frozen baseline.
4. **Debug action `TeleportNearExit`** (07 §12): `IDebugActions` + panel button + hotkey (pick a free key; `docs/10`
   lists the taken ones). Places the ball 200 m before the exit along the primary polyline, on the ground, facing the
   route heading, with zero velocity; sets the checkpoint there.
5. **Instrumentation:** `[RUSHCORE] Stage N/i complete in X s (model Y s); next build Z ms` on the transition.

The trigger already reads `Stage.Exits` (D-105); `StageExitIndex` says which pad ended the stage. Carry that index on
the completion event so Phase 6 can seed the next stage from it.

## Pre-answered choices

**The user answered the three that mattered on 2026-09-07. These are decisions, not defaults: implement them as written.**

- **Flow and boost both carry across a stage transition** (P-010, replacing P-001). A run is one continuous chain across
  all nine stages; a transition is not a chain boundary. Nothing about Flow is reset by reaching an exit. Recovery still
  ends Flow inside a stage, as 02 §8 says — that rule is untouched.
- **The exit taken picks the next stage's archetype** (P-011, replacing P-002). Exit A (index 0, the primary's pad)
  continues the archetype just played. Each further exit selects a different one, deterministically from the seed chain
  so the same run and exit always give the same next stage: derive it with `SeedChain.Derive(stageSeed, "route", exitIndex)`
  and take it modulo the archetype count, re-rolling while it equals the current archetype so a fork always changes the
  landscape. This is the payoff of D-105 and a stub for Phase 6's route cards: the forks mean something the moment they
  are played. Still wraps to stage 0 after stage 9 with the placeholder print.
- Outro 0.7 s, fade out 0.5 s, fade in 0.4 s (P-003, unchanged).
- The user's answer on the exits: **build on `StageDefinition.Exits` as it stands.** They may still refine the exit
  geometry later; T1 only reads the list, so that will not force a rewrite.
- Completion also fires in the harness (no special-casing); the harness asserts on it.
- `RestartSameSeed` rebuilds the *current* stage index; `RestartNewSeed` picks a new run seed and index 0.

## Non-goals (leave alone)

- Route cards, level-up, shop, score, run summary, death (Phases 5 to 7).
- Multiple exits (D-105, main track). Archetype rotation, difficulty scalar.
- Any HUD element (T2 reads what T1 exposes: `RunDirector.StageIndex`, `StageCount`).
- Chunked or streamed loading. The rebuild is synchronous, as today (about 3.5 s); instrument it, do not optimise it.

## Acceptance (harness)

Add to the generated-stage case, after the existing full-route drive:

- `StageCompleted` fired exactly once on reaching an exit, carrying the exit index; re-entering the pad after the outro
  does not fire again. On a seed with a terminal line (sample stage 6, Highlands seed 4), reaching exit B fires it with
  index 1.
- During the outro, injected Space and W do nothing (no charge started, no drive change), and the ball keeps rolling
  (speed at outro end ≥ 50% of speed at the trigger, or grounded and slowing on the pad; state the rule you used).
- After the transition: `StageIndex == 1`, the stage hash differs from stage 0's, `StageClock == 0`,
  `StageExitTime == 0`, `StageCheckpointIndex == -1`, **Flow and boost both equal to their values before the transition**
  (P-010), the ball within `PadRadius` of the new spawn with speed < 1 m/s, the camera yaw within 10° of the spawn facing.
- The archetype after the transition follows the exit taken (P-011): exiting by A keeps it, exiting by B or C changes it,
  and the same run seed with the same exit gives the same next archetype twice running. Drive a seed with a terminal
  line (sample stage 6, Highlands seed 4) to exit B and assert both.
- Node count after the transition equals the count after the first build (± 0; the existing flat-count check).
- `--stage 3` (self-test can set the flag through the same code path) builds index 3; its hash equals a direct
  `StageGenerationRequest(seed, 3, archetype)` build.
- `TeleportNearExit` lands the ball 150–250 m before the exit on the primary, inside the corridor, grounded.
- Transition time printed and under 8000 ms (same budget as the build check).

Manual (the user, not the agent): the outro reads as "done", not as a freeze; nothing from the previous stage is
visible after the fade.

## Files expected

`src/Run/RunDirector.cs` (new), `src/Core/GameBootstrap.cs`, `src/Core/IDebugActions.cs`, `src/World/MovementToyWorld.cs`,
`src/Player/PlayerPhysics.cs` (the lock flag only), `src/DebugUi/TuningPanel.cs`, `src/DebugUi/TelemetryOverlay.cs`,
`src/Tuning/GameplayTuning.cs` (`RunTuning`), `src/World/WorldDressing.cs` (exit pulse), `tests/MovementToySelfTest.cs`,
docs 05 §7, 07 §12, 08 §7, 10 (flag, hotkey, panel entries), this packet, `STATUS.md`, `PROVISIONAL_DECISIONS.md`.

## Stop conditions

Stop and write "Needs main track" if: the rebuild needs the generator to change; the exit trigger needs the route
skeleton to change; the interpolation reset misbehaves in a way that needs a physics-rule change; a harness check
outside this packet fails.

## Delivery record (filled by the implementing agent)

- Branch / commits:
- Harness (full run, count and wall time):
- Files changed:
- What was built (three to eight lines, concrete):
- Deviations from the packet and why:
- Measurements (transition ms, node counts before/after, outro speeds):
- P-entries written:
- Spec sections edited:
- Open items:
- Needs main track:
