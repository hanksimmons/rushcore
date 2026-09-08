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

- **Branch / commits:** `opus/t1-stage-lifecycle` off `develop-secondary` (5d940c3); 4db5348.
- **Harness (full run, count and wall time):** 337/337 in 107 s (default, Rolling Highlands); canyon 334/334,
  dunes 337/337, sky 338/338 (`RUSHCORE_ARCHETYPE`, run because the packet touches stage dressing). Golden hashes unchanged: tube 7D24840659B28767, tunnels + pit 327D9152EADB87E0, sky floor 3
  C453C38B4481CEB3, dune trains 1903C69DC8E57D46, gap + turns 3CA4CC5E70D0F945, three exits DE6A618E62DEB3EE.
- **Files changed:** `src/Run/RunDirector.cs` (new), `src/Core/GameBootstrap.cs`, `src/Core/IDebugActions.cs`,
  `src/Core/InputBootstrap.cs`, `src/World/MovementToyWorld.cs`, `src/World/WorldDressing.cs`,
  `src/Player/PlayerPhysics.cs`, `src/Tuning/GameplayTuning.cs`, `src/DebugUi/TuningPanel.cs`,
  `src/DebugUi/TelemetryOverlay.cs`, `tests/MovementToySelfTest.cs`, docs 05 §7, 07 §9/§10/§12, 08 §7, 10, this
  packet, `STATUS.md`, `PROVISIONAL_DECISIONS.md`.
- **What was built:**
  - `RunDirector`: run seed, stage index (0..8, wrapping with the placeholder line), the exit the stage was entered
    by, `Request(archetype)`, `Advance(exitIndex)`, and the static `NextArchetype` that P-011 describes.
  - The world takes a request: `Regenerate(StageGenerationRequest)` sets the seed and the stage index, `Build()`
    generates `new StageGenerationRequest(Seed, StageIndex, Archetype)` instead of always index 0, and the log and
    the seed row read `run/stage`. `StageCompleted(exitIndex)` is raised once per build at the first exit pad.
  - The outro in the composition root: controls locked at the pad, 0.7 s of exit feedback (the pad's sign swells and
    a ring flashes outward), 0.5 s fade, the rebuild, the teleport, 0.4 s fade back in, controls returned. Timings
    are `Run › Outro / Fade Out / Fade In` (P-003).
  - `PlayerPhysics.ControlsLocked` drops steering, jump, boost and carve at the one place input is read and cancels
    a charge in progress; gravity, drag and the ground follow are untouched, so the ball rolls free.
  - Flow and boost carry (P-010): the transition teleport asks to keep the chain; recovery still ends it (02 §8).
  - The exit taken picks the next archetype (P-011): exit A continues, B and C land elsewhere, from the seed chain.
  - `TeleportNearExit` (`E`, panel button, `IDebugActions`) and `--stage N`, which takes the same path as `StartRun`.
- **Deviations from the packet and why:**
  - The archetype the transition picks is written to `World › Archetype` before the rebuild rather than passed around
    it, so the panel, `MatchesTuning()` and the built stage cannot disagree (the world toggle stays the one authority
    for which archetype is built). The request still carries it.
  - The exit pulse is fired by the world beside the completion event rather than by the bootstrap: it is presentation
    on nodes the dressing owns, and putting it there keeps the bootstrap free of dressing internals.
  - The packet asked for the node count after a transition to equal the count after the first build. It cannot: a
    different stage has different dressing, so the count legitimately differs. The leak test instead is that
    rebuilding the stage the case started on, after three transitions, returns the tree to exactly its first count
    (3240 → 3240, orphans 0 → 0), plus the existing flat-count check over three regenerations.
  - Two additions the packet did not anticipate, both written up as P-012: `RunDirector.AutoAdvance` and the
    `keepChain` argument on `TeleportTo`.
  - The Flow/boost carry is measured on the tick before the rebuild and six ticks after it, not at the pad and after
    the outro: the ball rolls free for 0.7 s after the pad and whatever it meets there is a movement rule, not the
    transition. What is asserted is that the transition itself resets neither.
- **Measurements:** transition rebuild 2.9–3.1 s wall (2882 / 3088 / 3079 ms; budget 8000). Exit A reached at
  123 m/s with Flow 0.250 and boost 54.9 of 100; across the rebuild Flow and boost both came out unchanged.
  Ball travel through the 0.7 s of exit feedback with Space, W and Shift held: no charge, no jump, no drive, no
  boost, and it rolled on rather than freezing. Node count 3240 before and after three transitions, orphans 0 → 0.
  `NextArchetype` on the "three exits" sample: exit B takes highlands → sky, the same on both passes.
- **P-entries written:** P-012 (new). P-003, P-010 and P-011 are implemented as written; P-001 and P-002 stay
  superseded.
- **Spec sections edited:** 05 §7 (what `RunDirector` owns now and that the world plays `StageHost`), 07 §9 (the
  seed row reads `run/stage`, Copy Seed copies the run seed), 07 §10 (the stage row), 07 §12 (Teleport Near Exit),
  08 §7 (the harness list for S0's lifecycle lines), 10 (the `E` hotkey, `--stage N`, the completion sequence).
- **Open items:** the outro is the placeholder sequence 06 §13 allows for now: no completion card, no route cards,
  no shop, no score. The run wrap prints and starts again at stage 0. The manual read of the outro ("done", not a
  freeze) is the user's.
- **Needs main track:** nothing new. T7's axis-reference item stands unchanged.
