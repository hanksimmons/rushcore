# 11 — World-Scale Family Proposal (Gate M1)

**Status:** PROVISIONALLY ACCEPTED 2026-09-05 (D-082). The derived values in §3 are the generation inputs.
The five **FEEL** verdicts (fog onset, corridor minimum, monumental hill, committed ring, 27° ramp) and the
frame-time reading are deferred to the first generated-stage playtest (Phase 2, PR 2), where they are judged
on real terrain; the fog / far-plane change in §3h is not applied until then. V-009 is re-measured at S0.
**Owner of the inputs:** `docs/03 §15` (frozen movement baseline, D-078), `docs/10` (scale strip D-079,
terrain budget D-080, route speed model D-081).

## 0. What this decides, and what it does not

It decides the **generation inputs**: the world-scale family (feature wavelength/height, gap, ramp and
bend sizes, corridor width), the stage footprint derived from a target stage time, and the heightfield
cell size with its budget. It does not touch movement. Every number below is derived from the frozen
baseline and the strip measurements; where a number is a feel judgement rather than a derivation it is
marked **FEEL** and mapped to a strip station so the judgement is made on the ball, not on paper.

## 1. Inputs

| Input | Value | Source |
|---|---:|---|
| Ball diameter | 4.25 m | 03 §15 |
| Hard cap | 148.5 m/s (535 km/h) | 03 §15 |
| 0→cap, drive held, flat | **7.1 s / ≈ 580 m** (measured; the paper 5.3 s / 394 m ignored drag) | harness, D-081 |
| Drive vs gravity | 27.99 / 39.38 = 0.71 → a driven ball still accelerates up to a **45° grade** | 03 §15 |
| Turn radius at the cap | 100 m; corner speed limit v² = r·a_lat(v) | 03 §15, D-081 |
| Full-charge jump | 58.21 m/s up: 43 m, 2.96 s hang; range 177 / 296 / 351 / 439 m at 60 / 100 / 118.8 / 148.5 m/s | 03 §15 |
| Half-charge jump | 30.1 m/s up: 11.5 m, 1.53 s; range 92 / 153 / 182 / 227 m | 03 §15 |
| Landing burst | 118.8 m/s from any slam touchdown | D-077 |
| Crest contact | a crest launches below r = v²/g: 91 / 254 / 358 / 560 m at 60 / 100 / 118.8 / 148.5 m/s | 04 §8 |
| Cosine crest radius | λ² / (2π²H) | 04 §8 |
| Cell size vs contact | 4 m: 99% grounded on the 800/80 hill; 8 m: 86% | D-080 |
| Build model | ≈ 2 µs/sample warm, 4 B/sample heights, flat-shaded tiles 120 B/triangle (3 × 40 B) | D-080, code |

## 2. The governing finding: bends are the throttle, terrain is not

With the frozen baseline, **grade cannot slow a driven ball**. Drive (27.99 m/s²) exceeds the gravity
component of any slope under 45°, so the route speed model holds the cap across every hill on the strip:

| Cosine hills, W held, entering at the cap | Max slope | Minimum speed | Average over 4 km |
|---|---:|---:|---:|
| 100/10, 200/20, 400/40, 800/80 (H/λ = 0.10) | 17° | 148.5 | 145.0 m/s |
| 400/80, 800/160 (H/λ = 0.20) | 32° | 143–146 | 135 m/s |

What does set the base-kit speed on a route is, in order: the standing start (580 m), **bend radius**,
and deliberate stops (gaps, walls). The corner ladder from the steering envelope:

| Bend radius | 15 m | 25 m | 35 m | 50 m | 70 m | 100 m | ≥ 160 m |
|---|---:|---:|---:|---:|---:|---:|---:|
| Fastest speed through it | 51 | 68 | 81 | 99 | 120 | 148 | cap |
| 20 m lane change needs | 35 m | 45 m | 53 m | 63 m | 75 m | 89 m | 113 m+ |

So the family is organised around two ladders: a **bend-radius ladder** (speed control and line choice)
and a **crest-radius ladder** (contact versus launch). Hill height is then chosen for sightline and
monumentality, not for speed.

## 3. Proposed family

Units are metres; ball diameters (⌀ = 4.25 m) in brackets where it helps.

### 3a. Bends and banks

| Role | Radius | Speed it holds | Use |
|---|---:|---:|---|
| Cruise bend | ≥ 160 m | cap | primary route default; the strip's 160 / 240 m rings |
| Fast bend | 100 m | 148 | primary route; banked at the archetype's discretion |
| Committed bend | 50 m | 99 | primary route **minimum**; always banked (BankedTurn module) |
| Technical bend | 25 m | 68 | optional lines only |
| Hairpin | 15 m | 51 | never mandatory; challenge modules only |

Primary-route minimum 50 m (**FEEL**: turn-pad rings 80 / 160 / 240 m — which ring reads as "full
speed", which as "commit"). Bank berm height for a committed bend: 18 m at r 145 in the lab hairpin is
the accepted reference; scale linearly with radius (≈ 6 m at 50 m, 12 m at 100 m).

### 3b. Hills (Rolling Highlands base relief)

| Role | λ / H | Crest radius | Behaviour at the cap | Max slope |
|---|---:|---:|---|---:|
| Long swell | 1000–1600 / 40–80 | 630–1620 m | contact, rolling; the landscape's big shape and the sightline over it | 7–9° |
| Roller | 800 / 80 | 405 m | contact up to 126 m/s, launches at the cap (strip station verified) | 17° |
| Launch crest | 300–500 / 30–60 | 76–210 m | launches above 55–90 m/s; ≈ 200 m of flight from a 400/40 crest at the cap, landing near the trough | 17–20° |
| Micro relief (noise) | < 100 / **≤ 1** | ≥ 560 m | must never launch or hop a cruising ball | — |

The micro-relief bound is the concrete form of 04 §5C ("noise cannot erase readability"): to keep crest
radius ≥ 560 m, H ≤ λ² / 11 054, i.e. 0.9 m at λ 100, 3.6 m at λ 200, 0.23 m at λ 50. Below 100 m
wavelength noise is decoration only.

### 3c. Gaps

| Role | Opening | Depth | Entry speed the model must show | Basis |
|---|---:|---:|---:|---|
| Mandatory gap | 40–120 m | 12–20 m | ≥ 60 m/s at the rim | ≤ 0.7 × full-charge range at 60 m/s (177 m); crossable half-charged from 80 m/s |
| Optional gap | 160–320 m | 20–30 m | ≥ 100 m/s or a burst | full-charge range 296 m at 100, 351 m at burst speed |
| Set-piece gap | up to 400 m | 30 m+ | cap or boost | full-charge range 439 m at the cap |

Landing zone after any mandatory gap: ≥ 200 m long past the far rim (the half-to-full charge range
spread at 100 m/s is 153 → 296 m) and at least the corridor width. Take-off rims flat for ≥ 150 m
(the strip's runways); exit walls ≤ 25° so a short landing recovers.

### 3d. Ramps

Strip lips (rise 20 m; slopes 0.20 / 0.35 / 0.50 = 11.3° / 19.3° / 26.6°; lengths 100 / 57 / 40 m):

| Ramp | Ballistic range off the lip, no jump, at 100 m/s / cap | With a full charge |
|---|---:|---|
| 11° | 98 / 216 m | jump raises takeoff to 58 m/s: ≈ 290 / 430 m — the charge matters here |
| 19° | 158 / 350 m | adds up to 25 m/s of vertical at 100 m/s; nothing at the cap |
| 27° | 203 / 449 m | **nothing**: the ramp already exceeds 58 m/s of vertical above ≈ 23° at the cap |

Proposal: 11° ramps on the primary route (route-friendly, charge-rewarding), 19° in LaunchRamp modules,
27° as rare set-pieces. Rise 20 m as the standard lip; half-size 10 m lips for micro launches.

### 3e. Corridor width

| Width | ⌀ | Role |
|---|---:|---|
| 300 m | 70 | open highlands: the default primary region |
| 150 m | 35 | typical primary corridor between features |
| **75 m** | 18 | primary-route **minimum** (canyon floors, bridges, between walls) |
| 40 m | 9 | optional technical lines only |

Derivation: at the cap with a 3 s read horizon (450 m) a 75 m corridor tolerates a 4.4° heading error,
a 40 m corridor 2.3°. **FEEL**: the corridor station (300 → 40 m at 1000–2000 m) is the direct test —
the minimum is whichever width the user can hold at the cap without dread.

### 3f. Stage footprint from the stage-time target (V-009)

Base-kit time from a standing start over rolling λ 400 / H 40 relief with the bend mix as the variable
(route speed model, drive held, no boost, no burst):

| Primary route | no bends / 4 × r100 | 4 × r50 | 4 × r50 + 4 × r25 | 8 × r35 |
|---|---:|---:|---:|---:|
| 4 km | 31 s | 35 s | 40 s | 40 s |
| **6 km** | 45 s | 48 s | **54 s** | 54 s |
| 8 km | 59 s | 62 s | 67 s | 68 s |
| 10 km | 73 s | 76 s | 81 s | 82 s |

Proposal: **target 60 s of base-kit traversal per stage** (provisional V-009; a real player with
combat, detours and mistakes lands at 90–120 s, which puts a nine-stage run at 15–20 min before shop
and route screens). That gives a **6 km primary route**. With the route meandering within ±600 m of
the stage axis plus a 400 m scenery margin each side, the footprint is **6.0 × 2.0 km**, entry at one
end, exit at the other. Optional lines live inside that band. Measured clear time at Gate S0 replaces
this target (08 §7); the footprint is not resized before then.

### 3g. Cell size

**4 m**, everywhere (one logical height source, one collider). Evidence: 99% contact against 86% at 8 m
on the gentlest strip hills; the facet kink at 4 m is 0.4° on the smallest cruise crest (r 560) and
2.3° on a 100 m launch crest, both below the ball's contact tolerance in the measurement. 8 m is
rejected on contact, not on triangles. 6 m was not measured; it is the fallback if memory (3h) forces it.

### 3h. Terrain budget at the proposed scale

| 6.0 × 2.0 km | 4 m cells | 8 m cells (rejected) |
|---|---:|---:|
| Samples | 750 k | 188 k |
| Triangles | 1.50 M | 0.38 M |
| Build, warm (2 µs/sample model; strip measured 500 ms for 258 k) | ≈ 1.5 s | ≈ 0.4 s |
| Heights (collision + source) | 3.0 MB | 0.8 MB |
| Render tiles (128 cells) | 48 | 12 |
| Flat-shaded vertex arrays, 120 B/tri, CPU copy + GPU copy | **≈ 180 MB each** | ≈ 45 MB |

Build time 1.5 s at stage load is acceptable for the MVP (stage transition is instrumented, 08 §10).
The one number that needs a measurement is the vertex-array footprint. Two mitigations, neither
needed until measured: (1) index the tile mesh (one vertex per sample) and derive flat normals in the
terrain shader from screen-space derivatives — ≈ 6× smaller, same look; (2) 6 m cells — 2.25× smaller,
contact unmeasured.

**Draw distance vs sightline.** At the cap the player needs ≈ 3 s of readable terrain to steer
(450 m) and ≈ 4 s to commit to a gap or ramp (600 m: 0.45 s charge plus a 300–440 m flight). The
current defaults (`World › Fog End` 2400 m → fog begins at 384 m; `Camera › Far Plane` 8000 m) put the
fog onset **inside** the steering horizon and draw the whole stage. Proposal: Fog End 4000 m (onset
640 m), Far Plane 5000 m, so distant tiles cull and the read horizon is clear. Both are live sliders
to be judged on the strip's 500 m gantries and the hill stations (**FEEL**).

**Tiling.** 48 tiles of 512 m at the proposed scale; frustum culling at that granularity is adequate.
Finer tiling (64 cells) is only worth it if the frame-time reading below is draw-bound; it costs no
build time.

**User's reading (required before acceptance):** frame time at the cap on the strip with F2 open,
defaults (4 m cells, Fog End 2400, Far Plane 8000): `____ ms` on the runway, `____ ms` over the
800/80 hills. With Far Plane 5000 / Fog End 4000: `____ ms`.

## 4. Strip checklist before acceptance

| Station | Question | Sets |
|---|---|---|
| Runway 0–1000 | does 580 m to the cap read as a launch, not a wait? | (confirms D-081; no change possible without D-078) |
| Corridor 1000–2000 | narrowest width holdable at the cap without dread | 3e minimum (75 m proposed) |
| Hills 2000–4200 | which station reads "monumental" and which "bump"; does the 800/80 launch at the cap feel intended? | 3b long swell / roller / launch crest |
| Gaps 4200–5080 | which gap is a "yes" at 60 m/s half-charged; which needs the burst? | 3c mandatory / optional |
| Ramps 5080–5700 | is the 27° lip a set-piece or too much? | 3d |
| Turn pad 5700–6300 | which ring is "full speed", which "commit"? | 3a |
| Whole strip at the cap, F2 | frame time; fog onset at 384 m acceptable or not | 3h |

## 5. On acceptance

- `DECISIONS.md`: one entry freezing the family (values from §3, with the user's edits); V-005, V-008,
  V-009 resolved provisionally (V-009 re-measured at S0).
- `04 §8`: references this document as the frozen scale; the micro-relief bound becomes a §5C rule.
- Code: one static `WorldScale` constants class in `src/Generation/` created with the terrain core
  (not before), the single home for these numbers; the tuning panel does not expose them.
- Runbook: the fog/far-plane defaults change only if the user accepts 3h's proposal (D-078 does not
  cover them, but the toy's "0 override values" launch state must stay true).
