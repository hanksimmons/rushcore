# Provisional decisions (parallel track)

P-numbers are proposals. They become accepted only when the main track promotes them to a D-number in
`docs/DECISIONS.md` with the user's verdict (`README.md` §8 in this directory). Until then the code on the `opus/`
branches implements them and every spec edit they cause carries the tag `(P-nnn)`.

Statuses: `PROPOSED` · `PROMOTED D-nnn` · `REJECTED` · `WITHDRAWN`.

Write one row per decision. Say what was chosen, the alternative, and why, in the DECISIONS.md style: dated, concrete
numbers, names of the code that carries it.

| ID | Task | Status | Decision |
|---|---|---|---|
| P-001 | T1 | PROPOSED (packet default) | Flow resets to 0 on stage entry; boost carries over. A stage is a chain boundary (02 §8 "falling/recovery ends Flow" is the nearest accepted rule); carrying boost keeps the pickup economy honest across the transition. Alternative: carry Flow. |
| P-002 | T1 | PROPOSED (packet default) | The next stage after completion uses the same archetype as the one completed (`World › Archetype`) until Phase 6 route choice exists; the run wraps to stage index 0 after stage 9 with a log line and no summary. |
| P-003 | T1 | PROPOSED (packet default) | Completion sequence: exit pad → controls locked, ball rolls free → 0.7 s of exit feedback → 0.5 s fade → rebuild → 0.4 s fade in, controls unlocked at the spawn. Numbers are tuning fields in a new `Run` category, not compiled feel values. |
| P-004 | T2 | PROPOSED (packet default) | Health is a value on the player (`PlayerHealth`, max 100, no damage sources yet). Reaching 0 before run death exists restores the checkpoint and refills to max with a log line; the HUD shows the bar. Run death is Phase 6/7. |
| P-005 | T2 | PROPOSED (packet default) | The qualitative speed-band word on the HUD is off by default (06 §12 "optional if playtests need it"), toggled by `Hud › Band Word`. |
| P-006 | T3 | PROPOSED (packet default) | Placeholder enemies have no colliders and no behaviour until the impact model (Phase 4, main track). They exist on the `World › Enemy Showcase` lab row and nowhere else. |
| P-007 | T3 | PROPOSED (packet default) | Reward burst: coins are kinematic (code-integrated ballistic arcs, no physics bodies), 0.4 s free flight, then magnetise toward the ball at up to 60 m/s, collect within 1.2 m; one `Collected(kind, amount)` event per coin. |
| P-008 | T4 | PROPOSED (packet default) | Cosmetic scatter draws from `StageGenerationRequest.CosmeticSeed` (05 §9), not the world seed, so a scatter change never touches the stage hash. |
