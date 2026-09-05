# RUSHCORE — Project Design Bootstrap

**Document status:** Draft 0.3 — final audited baseline  
**Target engine:** Godot .NET 4.7.2  
**Development platform:** macOS arm64 (Apple Silicon)  
**Implementation language:** C#  
**Working directory:** `~/rushcore`  
**Current phase:** Final audited design/specification baseline; ready for coding-agent kickoff and Movement Toy implementation

## Product thesis

> **RUSHCORE is a high-speed 3D physics-action roguelite where movement is combat.**

The player becomes increasingly dangerous by reading terrain, preserving momentum, choosing aggressive lines, and converting speed into impact power and rewards.

## Source-of-truth hierarchy

When documents disagree, the higher-authority document wins until the conflict is explicitly resolved.

1. `docs/00_GAME_VISION.md` — product intent and immutable design pillars.
2. `docs/01_MVP_SCOPE.md` — what is and is not in the MVP.
3. System specifications:
   - `docs/02_GAMEPLAY_SYSTEMS_SPEC.md`
   - `docs/03_PLAYER_MOVEMENT_PHYSICS_SPEC.md`
   - `docs/04_PROCEDURAL_STAGE_GENERATION_SPEC.md`
4. `docs/05_TECHNICAL_ARCHITECTURE.md` — runtime structure and engineering rules.
5. `docs/06_VISUAL_UX_SPEC.md` — presentation, HUD, camera presentation, procedural art.
6. `docs/07_RUNTIME_TUNING_DEBUG_SPEC.md` — runtime tuning and developer tooling.
7. `docs/08_TEST_ACCEPTANCE.md` — objective verification and playtest gates.
8. `docs/09_IMPLEMENTATION_PLAN.md` — build order only; it must not redefine product behavior.
9. `docs/DECISIONS.md` — accepted decisions and empirical validation items.
10. `CLAUDE.md` — coding-agent operating rules; it references the above and must not silently override them.

## Documentation rule

Each detailed fact has **one owning document**. Other documents summarize or reference the owner instead of duplicating implementation detail.

## Repository bootstrap target

```text
~/rushcore/
├── README.md
├── CLAUDE.md
├── project.godot
├── docs/
│   ├── 00_GAME_VISION.md
│   ├── 01_MVP_SCOPE.md
│   ├── 02_GAMEPLAY_SYSTEMS_SPEC.md
│   ├── 03_PLAYER_MOVEMENT_PHYSICS_SPEC.md
│   ├── 04_PROCEDURAL_STAGE_GENERATION_SPEC.md
│   ├── 05_TECHNICAL_ARCHITECTURE.md
│   ├── 06_VISUAL_UX_SPEC.md
│   ├── 07_RUNTIME_TUNING_DEBUG_SPEC.md
│   ├── 08_TEST_ACCEPTANCE.md
│   ├── 09_IMPLEMENTATION_PLAN.md
│   └── DECISIONS.md
├── src/
└── tests/
```

The coding agent may create the remaining Godot/.NET bootstrap files and implementation directories as needed. The design package does not pre-create speculative source structure merely to make the repository look complete.

## Design status convention

- **ACCEPTED** — a project rule unless explicitly changed later.
- **VALIDATE** — intended behavior is known, but the exact value/curve or feature retention must be determined in playtesting/profiling.
- **DEFERRED** — not needed for the current implementation slice.

`VALIDATE` does not mean unspecified. It means the spec defines the desired experience and tuning handles while deliberately avoiding false numerical precision.

## Definition of MVP

The MVP is complete when:

- the Movement Toy is intrinsically fun without progression,
- three procedural terrain archetypes produce readable, traversable stages,
- movement-driven combat works,
- a nine-stage run can be completed,
- route choices, upgrades, passive items, shop/economy, scoring, one final encounter, and a small meta tree form a coherent loop,
- placeholder procedural presentation is strong enough for serious playtesting,
- acceptance and performance gates pass.

Anything else remains post-MVP unless explicitly promoted in `01_MVP_SCOPE.md`.
