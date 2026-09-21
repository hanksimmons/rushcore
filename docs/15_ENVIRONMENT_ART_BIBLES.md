# 15 — Environment Art Bibles

**Status:** PROPOSED 2026-09-20, derived from the concept-art set in `docs/concept/`. Not accepted until the
design team and the lead graphics developer sign off and a D-number promotes it.
**Authority:** below `00`–`04` and `06`; **subordinate to** `docs/14_ART_DIRECTION_BIBLE.md`, which owns every
global law (light, colour, material, signage, the ball, the corridor edge system). This document applies those
laws **per environment** and states what each one's generator must produce.
**Owns:** per-archetype palettes, landform art rules, structure vocabularies, signage placement, per-archetype
lighting/fog, and the per-archetype generation requirements.
**Read with:** `04 §6` (the delivered archetype geometry), `11` (the world-scale family), `12` (Summit Descent
design), `13` (tunnels, horizon, resident mesh), `18` (concept-image index), `17` (the cel shading spec).

> **Style is cel shading**, decided by the user on 2026-09-20 (`14 §3`). Every palette in this document is a
> **pair of plateaus** — a lit value and a shade value — plus the shared hot gold rim, not a continuous ramp.
> The "Lit" and "Shade" columns in each table below are literally the two plateaus the cel ladder mixes
> between (`17 §3.2`); there is no value in between. Where a table lists a single value it is the albedo the
> ladder consumes.

---

## 0. How each bible is structured, and the rules common to all five

Each environment below has the same eleven sections, so they can be diffed against one another:

1. **Concept references** — which images in `docs/concept/` govern it.
2. **The idea in one line.**
3. **What the player is doing here** — the skill, restated so art serves it.
4. **Landform rules** — the frozen geometry from `04 §6` and `11 §3`, restated as art constraints.
5. **Palette** — hex values, EARTH family, with the slope/height rules that drive them.
6. **Structure vocabulary** — which of the five concrete forms (`14 §8.1`) appear, and where.
7. **Signage and props.**
8. **Light, sky and fog** — the per-archetype deltas from `14 §4`–`§5`.
9. **Ground treatment and VFX** — dust, debris, surface feel.
10. **Generation requirements [GEN]** — numbered, testable, prefixed with the archetype's letter.
11. **Traps** — what the concept art shows that must **not** be built here, and why.

### Common law, restated because it is violated most often

- **The corridor floor is natural ground, never a paved road** (`14 §17.2`). Track tint + edge strips carry
  the read.
- **Corridor widths are 75 / 150 / 300 m**, never the 10–30 m ribbon the art draws (`14 §17.1`).
- **Nothing but a declared lid stands within 91 m + a ball above the primary's centreline** (`04 §5I`, D-102).
- **No geometric relief below a rideable wall lip** (`14 §8.5`).
- **SIGNAL cyan means "you, or your line", and nothing else** (`14 §6.1`).
- **Every art placement draws from the cosmetic seed** so no art change moves a stage hash (`04 §5G`, G15).
- **Archetypes differ by geometry and palette only** (`04 §7`). Nothing below changes a physics value.

### The five environments at a glance

| Archetype | Skill | Dominant EARTH hue | Structure density | Cloud deck | Signature asset |
|---|---|---|---|---|---|
| Rolling Highlands | terrain reading, momentum conversion | green-gold | low, isolated monuments | conditional | the slogan monolith on a ridge |
| Canyon Run | high-speed steering, banked lines | red rock | **high** — walls everywhere | off | the stencilled wall you ride |
| Dune Sea | jump rhythm, landing alignment | sand | **lowest** — almost none | off | the crest line against the sun |
| Sky Terraces | vertical commitment, fall management | pale stone above cloud | high, terraced | **on, below you** | the terrace riser wall |
| Summit Descent | hairpin commitment, line choice | snow → scree → moss | high — rails and headwalls | on, mid-descent | the rail with its crest line |

---

## 1. Rolling Highlands

### 1.1 Concept references
**`14_CEL_MASTER_ridge_descent` (the master frame — this archetype is where the style was decided)**,
`15_CEL_scree_slope_debris`, `12_cel_dusk_ridge`, `13_cel_valley_portal_hud`. Composition and architecture
only: `01_keyart_highlands_ridge` (the hero), `02_highlands_descent_gates`, `05_highlands_stencil_viaduct`,
`06_lowpoly_ridge_dams`, `11_highlands_overlook_monoliths`.

### 1.2 The idea in one line
**A green mountain valley with the programme's earthworks cut into it — long sweeping land, a few colossal
concrete interventions, and a view that runs to the horizon.**

### 1.3 What the player is doing here
Reading terrain and converting it to speed: choosing whether to take the valley (safe, fast) or the ridge
(paid, faster, exposed), spotting crests that will launch them, and holding long lines. The art's job is to
make **the shape of the land legible from a kilometre out**, because every decision here is a line choice made
early.

### 1.4 Landform rules
From `04 §6` and `11 §3b`, unchanged:

| Feature | λ / H | Reads as | Art note |
|---|---|---|---|
| Long swell | 1000–1600 m / 40–80 m | rolling, always in contact | the landscape's big shape; the sightline sits on top of it |
| Roller | 800 / 80 | contact to 126 m/s, launches at the cap | the "will it launch?" read must be visible: see G-H3 |
| Launch crest | 300–500 / 30–60 | launches above 55–90 m/s | the payoff shapes |
| Micro relief | <100 m / ≤1 m | decoration only | never silhouettes, never shadows meaningfully |

Ridge lines (D-086/D-100): 200 m lateral offset over a 290 m S, a 16–30 m climb over 300 m, a plateau, the
mirror descent — **1 260 m in all**. Straights 250–600 m. Corridor 150–300 m.

### 1.5 Palette

| Zone | Lit | Shade | Driver |
|---|---|---|---|
| Valley floor / low | `#3D6647` | `#22322A` | height < −14 m rel. |
| Mid slope grass | `#638C4D` | `#33452C` | the default body of the stage |
| High dry grass | `#B8B37D` | `#5E5B45` | height > +14 m rel. |
| Exposed rock | `#856B54` | `#3E3630` | slope > 0.30 |
| Cliff | `#544D4F` | `#2A272C` | slope > 0.55 |
| Basin / wet low | `#3D4F63` | `#222B36` | lowest band, riverbeds |
| Conifer band | `#22301F` / lit `#35422C` | — | slope 15°–35°, in loose bands |
| Track tint | `#80705C` at 45% | — | delivered, keep |

**The height ramp is the archetype's whole identity**: green in the folds, gold on the tops. It reads the
land's shape as a contour map without a single texture, and it is the reason this archetype survives the
vertex-colour constraint better than any other.

### 1.6 Structure vocabulary
**Low density and deliberately isolated.** Highlands is where concrete is *rare and therefore monumental*.

- **The dam/retaining arc** across a valley at one or two points per 6 km act (`01`, `06`), with the route
  running **along its crest** where the skeleton allows, or beneath it where it does not. This is the
  archetype's hero structure and it should be visible from ≥3 km.
- **The slogan monolith**, 14–24 m, on high ground beside the route (`11`).
- **The viaduct** crossing a side valley in the middle distance (`05`) — pure scenery, on the horizon ring or
  beyond the optional band, never over the route.
- **Portal/dive tunnels** (D-116, `TunnelChance` 0.5): the open cut is the read cue; see §6.
- No walls along the corridor. **The Highlands corridor is unwalled** — that is what separates it from Canyon
  Run, and adding walls here would erase the archetype.

### 1.7 Signage and props
- Wall stencils have no walls to sit on; identity comes from **monoliths and the dam faces**.
- Scatter as delivered: rocks and crystal clusters, densest just off the corridor edge, thinning beyond 600 m
  — restated in the EARTH family (`14 §13`), with crystals **desaturated** (they are currently `#527AAD`-ish
  blue, which is a SIGNAL-family intrusion; move to `#6E7A82`).
- Conifer bands (new) on 15°–35° slopes.

### 1.8 Light, sky and fog
- Standard golden hour (`14 §4`).
- Cloud deck **conditional** (`14 §5.4`): on when the route's mean height sits in the upper third of the
  footprint, at mean − 200 m, so the ridge lines look out over a filled valley — the `01` composition.
- Fog standard: 800 m → 6 km.
- Horizon ring: rolling ridges to 600 m, green foreground fading to blue-violet distance (delivered, D-114).

### 1.9 Ground treatment and VFX
- Rolling dust: warm, dull, low — grass and dry soil, not sand.
- Crest launches throw a burst of dust at the lip; landing throws a ring.
- Wet ground in basins: a slightly darker, slightly specular tint band near rivers. Cheap, and it makes the
  low ground read as low.

### 1.10 Generation requirements [GEN]

| # | Requirement |
|---|---|
| H1 | One **dam/retaining arc** per act (three per 18 km stage) placed across a valley the route crosses or runs along, from the cosmetic seed, sited ≥600 m from any fork or module body. |
| H2 | **Slogan monoliths** every 600–1000 m of straight route, on the higher side, outside all keep-outs, facing the oncoming line. |
| H3 | Any **roller or launch crest** carries a **crest line**: a 2 m band of the high-dry palette along the crest's ridge, so a launching crest is visually distinct from a rolling swell *before* the player reaches it. This is the archetype's most important readability asset. |
| H4 | **Conifer bands** scattered on 15°–35° slopes outside the corridor keep-out, in the EARTH dark family, as single dark triangular instances. |
| H5 | The cloud-deck flag and altitude are computed per §1.8 and recorded in `StageDefinition`. |
| H6 | Ridge-line plateaus receive their own edge strips at reduced intensity (they are paid lines, `14 §12`). |
| H7 | Crystal scatter is recoloured out of the SIGNAL family. |

### 1.11 Traps
- **Do not wall the corridor.** Images `05` and `12` show tall walls beside the route; on Highlands those are
  *distant scenery*, not corridor edges.
- **Do not pave.** Image `02`'s concrete apron is a pad, not a road.
- **Do not add a second dam per act** — the monument stops being monumental at density.

---

## 2. Canyon Run

### 2.1 Concept references
`03_canyon_wallride_portal` (the hero — wall ride and portal in one frame), `08_canyon_run_hud_wallride`,
`13_cel_valley_portal_hud` (the portal read).

### 2.2 The idea in one line
**A red-rock slot canyon whose walls the programme has cast and faced in concrete, so the walls are not
scenery — they are the track's ceiling, its banking, and its shortcut network.**

### 2.3 What the player is doing here
Holding speed through banked bends, riding walls (D-108) on bend outsides, taking ledge lines 25–40 m up the
wall, threading wall tunnels and portal tunnels, and finishing half the time in a spiral pit. **This is the
archetype where the art and the mechanics are most tightly coupled**: every wall the player can see is a
surface they can drive on, and the art must say so.

### 2.4 Landform rules
From `04 §6` (D-098, D-109, D-111, D-113), unchanged:

- Channel cut through side terrain raised **60–120 m** above the floor relief.
- **The wall profile**: a **30 m fillet** at the foot, a **72° face**, a rounded lip over 8 m of height, the
  berm flowing straight into the fillet on bend outsides, and a **30° eased face** on bend insides (the
  blind-corner rule) faded over 200 m of route either side.
- Walls stand **8 m outside the corridor's level width** (`WorldScale.WallSetback`).
- Straights 200–450 m; bend mix 45% cruise / 45% fast; berm ×1.5.
- Ledge lines cut into the wall **25–40 m above the floor**.
- Wall tunnels (lids): 150–300 m long, **226 m wall-to-wall**, underside 15 m above the corridor, 6 m thick.
- Spiral pit on ~50% of stages: 380 → 150 m radius, descending 120 m in one turn through four quarter arcs.
- Portal tunnels at `TunnelChance` 0.7; covered runs 490–750 m.

### 2.5 Palette

| Zone | Lit | Shade | Driver |
|---|---|---|---|
| Canyon floor | `#8A7256` | `#3E362C` | the base palette at floor height |
| Wall rock, upper | `#9E5C3D` | `#4A2C22` | delivered `CanyonRock` |
| Wall rock, lower | `#663C28` | `#33201A` | strata darkening with depth (delivered rule) |
| Strata bands | every **14 m** of height on faces > 0.3 slope, lower bands darker | delivered — keep exactly |
| Rim / mesa top | `#CC9966` | `#66503A` | delivered `CanyonRim` |
| Concrete facing | `#B9B0A4` / `#4A4652` | | WORKS family |
| Ledge line surface | floor palette + track tint | | it is drivable, so it reads as ground |

**The concrete/rock relationship is the archetype's signature.** From the art (`03`, `08`): the programme has
**faced the fillet and the lower face in concrete** and left the upper wall as raw stratified rock. That is
not decoration — it is a perfect diegetic encoding of `14 §8.5`: *the part you ride is smooth and cast; the
part you cannot reach is rough and natural.* Adopt it as law:

> **Canyon walls are cast concrete from the fillet's foot up to the lip-minus-15 m, and natural stratified
> rock above that.** The transition is a horizontal line the player can read at any distance, and it tells
> them exactly how high the rideable band goes.

### 2.6 Structure vocabulary
Every one of the five forms appears here; this is the densest archetype.

- **The wall** — the corridor's slot walls, faced per §2.5, with pilaster ribs **above the lip only**.
- **The dam/retaining arc** — the berm on every bend outside, read as a cast bank.
- **The deck/viaduct** — wall-tunnel lids, and scenery bridges crossing the canyon in the middle distance
  (`03`, `08` both show these and they are excellent depth cues).
- **The buttressed shaft** — the spiral pit (see §7).
- **The portal** — tunnel mouths with their 1.5 m rim (`03`, `13`).

### 2.7 Signage and props
- **Wall stencils are the archetype's identity asset.** Above the lip, glyph height 8–20 m, on straights,
  every 400–800 m. `RUSHCORE`, sector numbers, and the canyon-tagged slogans (`CANYONS CONNECT HUMANITY`,
  `A BIGGER CLEANER FASTER WORLD`).
- **Chevron runs above the lip at every bend entry** (`08` shows exactly this) and flush on the floor at forks.
- Scatter as delivered: red slabs and standing fins **on wall tops only** (above 60% of wall height), never on
  a slot floor.
- Sector-number slabs on the wall at checkpoints.

### 2.8 Light, sky and fog
- Sun azimuth constrained further here: prefer **75°–105°** off the stage axis, i.e. near-perpendicular, so
  one wall is lit and the opposite wall is in shade. **This is the archetype's key lighting rule** — it gives
  the canyon a lit side and a dark side, which is both beautiful and functionally useful (the lit wall is the
  one the player can read to ride).
- Cloud deck **off**.
- Fog: bring the end in to **4.5 km** (a canyon's read horizon is shorter and the haze stacks depth between
  bends).
- Horizon ring: mesas and buttes in three flat tiers to 700 m (delivered).

### 2.9 Ground treatment and VFX
- Floor dust is **red-brown and heavier** than Highlands.
- **Wall-ride VFX is the missing asset** (`14 §10.3`): when riding, the streaks lie on the wall plane and dust
  sheds *downward* under gravity rather than trailing. Without this the wall ride reads as a bug.
- Ledge lines get a subtle SIGNAL edge strip on their outer edge — they are a paid line with a fall.

### 2.10 Generation requirements [GEN]

| # | Requirement |
|---|---|
| C1 | The wall shell's material splits at **lip − 15 m**: concrete below, rock above, as a vertex-colour or shader threshold on the shell strip. |
| C2 | **No geometric relief below the lip** — ribs, frames and fittings generate above it only. Assert in the wall-ride harness that no new impacts appear. |
| C3 | **Wall stencils** placed above the lip on straights every 400–800 m from the cosmetic seed, facing the corridor, never within 100 m of a portal or a lid mouth. |
| C4 | **Chevron runs above the lip** at every bend entry, count 3, pointing through the bend. |
| C5 | Sun azimuth for this archetype is drawn from **75°–105°** (or its mirror) off the stage axis. |
| C6 | Fog end is **4.5 km** on this archetype. |
| C7 | Ledge lines carry an outer edge strip; the primary carries both edges at full width. |
| C8 | Portal rims, portal faces and cap surfaces use the WORKS palette; the trench floor uses the canyon floor palette (so the tunnel reads as *built*, and its floor as *ground*). |
| C9 | The wall-top scatter keep-out (above 60% of wall height) is unchanged and asserted. |

### 2.11 Traps
- **The corridor is not 20 m wide.** Image `08` reads as a narrow ledge; the real corridor between the walls
  is 75–150 m. The wall-ride hero shot works at the real width — it just needs the camera lower.
- **Do not let stencils or ribs intrude below the lip** (C2). This is the one art decision that can silently
  break a frozen mechanic.
- **Do not put glass, lights or fittings on the ride band** — `13 §2.6` already killed the glass tube idea
  once (D-112).
- Image `03`'s tunnel mouth sits in a *natural* rock face with a cast portal; do not wrap the whole mesa in
  concrete.

---

## 3. Dune Sea

### 3.1 Concept references
**None of the thirteen images depicts Dune Sea.** This bible is therefore derived from `04 §6` (D-099), the
global law in `14`, and the one dune-adjacent frame (`02`'s dry terrain). **A concept round targeting Dune Sea
is an outstanding commission** — see §3.11 and `14 §20`.

### 3.2 The idea in one line
**An ocean of sand under the same sunset, where the programme has built almost nothing — and the few things it
has built stand utterly alone.**

### 3.3 What the player is doing here
Jump rhythm and landing alignment: launching off every crest at the cap, landing on the far side near the
trough, climbing the next dune and launching again. The art's job is **making the wave's direction and the
crest lines instantly readable**, because the whole archetype is a timing puzzle whose clock is the terrain.

### 3.4 Landform rules
From `04 §6` (D-099), unchanged:
- One seeded directional cosine dune wave: **λ 350–500 m, crests 21–43 m** (0.06–0.085 λ) above the swells.
- Crest lines within **10°** of across the stage axis; route within **12°** of the stage axis.
- Dune trains: 2–4 launch crests one wavelength apart on a feature straight; other sections run at the swale
  level between dunes.
- Swell slope budget drops to 0.08 — **the dunes are the relief**.

### 3.5 Palette

| Zone | Lit | Shade | Driver |
|---|---|---|---|
| Trough | `#B88F57` | `#5A4429` | delivered `SandTrough` |
| Crest | `#EDCC85` | `#7A6845` | delivered `SandCrest` |
| Lee face (away from the wave's travel) | `#946B42` | `#463321` | delivered `SandLee`, 70% blend |
| Exposed rock outcrop | `#8A7256` | `#3E362C` | rare, for landmarks |
| Concrete | `#B9B0A4` / `#4A4652` | | WORKS |

The delivered lee-face darkening is the single most valuable thing in this palette and it must survive any
restyle: **it is what tells the player which way the wave runs**, and therefore which crests will launch them.

### 3.6 Structure vocabulary
**Minimum density — this is the archetype of emptiness.** Concrete appears only as:
- a handful of **slogan monoliths**, half-buried in sand at their bases, standing alone on crests;
- **pads** (start, exit, checkpoints);
- the occasional **dive tunnel** portal (`TunnelChance` on Dune Sea is tried at every site, though its
  straights are usually trains, so only ~8% of seeds carry one) — and a portal mouth emerging from a dune
  face is potentially the best image in the game.

No walls, no viaducts, no dams. The restraint is the point: after a Canyon Run stage, Dune Sea should feel
like the programme stopped.

### 3.7 Signage and props
- Monoliths with **sand drifts** against their windward faces.
- Scatter as delivered: ridged stones and dry tufts on crests only, sparse.
- **Chevron runs are more important here than anywhere else** and should be placed flush on the *approach to
  each train*, because a dune train is a commitment and the player needs to know it has begun.

### 3.8 Light, sky and fog
- Sun **low and near the wave's travel direction** (so crest lines rim-light and lee faces go deep) — draw
  azimuth from within **25°** of the wave direction, subject to `14 §4.2`'s no-dead-ahead rule.
- Cloud deck **off**. Fewer, higher, thinner clouds than elsewhere; more open sky.
- Fog: warmer and slightly denser near the ground — a heat-haze read. Fog end 5 km.
- Horizon ring: a flat sea of dunes to 6 km, then a ridged range to 800 m (delivered).

### 3.9 Ground treatment and VFX
- **Sand spray, not dust**: lighter, more volume, longer-lived, thrown high on a landing.
- **Wind streamers**: thin translucent sheets of blowing sand along crest lines, aligned with the wave. One
  unshaded scrolling quad strip per crest; cheap, and it animates an otherwise static landscape.
- Landing impacts throw a fan of sand forward; a slam landing throws a crater ring.
- Ball trails over sand kick a **continuous rooster tail** rather than intermittent puffs.

### 3.10 Generation requirements [GEN]

| # | Requirement |
|---|---|
| D1 | Every crest in a **dune train** receives a crest line (a 2–3 m band of the crest palette along the ridge), so the launch points are countable from the approach. |
| D2 | **Wind streamers** are emitted along crest lines within the visible band, aligned to the wave direction, from the cosmetic seed. |
| D3 | A **chevron run** is placed on the approach to every dune train, 150 m before the first crest. |
| D4 | Sun azimuth is drawn within 25° of the wave direction, respecting the global no-dead-ahead constraint. |
| D5 | Monolith placement here is **rare and isolated**: at most one per 2 km, always on a crest, always with a drift stamp at its base. |
| D6 | Fog end 5 km, fog tint warmer than the global default. |
| D7 | Corridor edge strips on Dune Sea run at **reduced intensity on train straights** — the crest lines are the primary read there and two competing cues fight. |

### 3.11 Traps and the open commission
- **Do not add structures to fill the emptiness.** The emptiness is the archetype.
- **Do not texture the sand.** Ripple detail is invisible at 900 km/h and expensive; the lee-face shading does
  the work.
- The set-piece dune and the wide landing zones are **still not distinct** in the delivered generator
  (`04 §6`) — art cannot fix that; it needs the aligned-relief bump.
- **Commission:** a concept round for Dune Sea specifically, targeting (a) a crest-line launch at the cap
  against a low sun, (b) a dive portal emerging from a dune face, (c) an isolated monolith with drifts.

---

## 4. Sky Terraces

### 4.1 Concept references
`07_sky_terraces_hud` (the hero — numbered terraces, cloud sea, banners, HUD), `10_sky_terraces_overdrive`
(the Overdrive causeway above cloud), `09_spiral_pit` (shares the buttressed language).

### 4.2 The idea in one line
**A programme built on the tops of mountains that pierce a cloud sea, where the drivable world is a stack of
cast terraces and everything between them is a fall.**

### 4.3 What the player is doing here
Vertical commitment and fall management: taking paid lines up to floors 2 and 3, accepting that a mistake
drops them a floor (a setback, never a death), and reading drains so they know where a fall lands. The art's
job is to **make the height instantly legible and the fall survivable-looking** — the player must believe,
from the picture alone, that going up is worth it and coming down is not fatal.

### 4.4 Landform rules
From `04 §6` (D-096, D-103), unchanged:
- Three floors: primary on the lowest, **floor 2 at +60 m / 200 m out**, **floor 3 at +120 m / 300 m out**.
- Terrace transitions: an S of 290 m (floor 2) or 360 m (floor 3), then a cosine ramp of 420 m (60 m) or
  600 m (120 m) that never launches at the cap.
- **Inner edge is a 12 m cliff** whose foot lands on the floor below; **outer edge descends into relief at
  the route grade**.
- Highlands family otherwise: straights 500–900 m.
- The delivered **cloud band**: two translucent sheets at 130 m and 160 m above the primary's mean height,
  55% and 35% alpha. Floor 3 sits in it.

### 4.5 Palette

| Zone | Lit | Shade | Driver |
|---|---|---|---|
| Terrace surface | `#A89C86` | `#4C463E` | pale weathered stone — the archetype's ground |
| Terrace riser (cliff face) | `#B9B0A4` | `#4A4652` | **cast concrete** — this is the key move |
| Mountain rock (beyond the floors) | `#6E6A70` | `#33313A` | cool grey, so it separates from the warm terraces |
| Snow caps on distant peaks | `#E8ECF2` | `#9FB0C8` | |
| Pale crystal scatter | `#8E97A0` | | desaturated out of SIGNAL |
| Cloud band sheets | `#FFD9A0` top / `#6E5170` base | | delivered geometry, restyled colour |

**The key move:** terrace **risers are cast concrete** while terrace **surfaces are pale stone**. That
instantly encodes the vertical grammar — every horizontal band you can drive on is natural, every vertical
band between them is built, and the player counts floors at a glance. Image `07`'s numbered terrace blocks
(`01 FOUNDATIONS MOTION MORE`, `02 ASCEND REFINE PUSH`, `03 PEAKS AHEAD`) do exactly this and should be built
almost literally.

### 4.6 Structure vocabulary
- **The wall** as terrace riser, with the floor's number stencilled at 20–40 m glyph height (`07`).
- **The deck/viaduct** as bridges between floors and as the Overdrive causeway (`10`).
- **The portal** where tunnels link floors (`TunnelChance` 0.4, and on this archetype a dive takes a site no
  terrace fits).
- **Banners**: the one archetype where hanging fabric banners are permitted (`07` shows them on the upper
  structures). They are a cheap, strong vertical accent and they move a little, which is welcome in an
  otherwise static world. Keep them **out of the corridor** and **off the ride band**.

### 4.7 Signage and props
- **Floor numbers are the archetype's signature signage**: huge thin numerals on every riser, visible from the
  floor below and from the approach.
- `PEAKS AHEAD`, `ASCEND REFINE PUSH`, `TERRA ALTITUDO OMNIA` as the tagged slogans.
- Pale crystals on the margins around floors, below the cloud band; nothing on a floor surface (delivered).
- **Drain markers**: where a cliff foot drains back to the primary, place a chevron run pointing down the
  drain. A fall is not a failure here, and the art should say so — the player who falls should immediately
  see where to go.

### 4.8 Light, sky and fog
- **Cloud deck ON, below the player**, at primary mean − 250 m (`14 §5.4`). This is the archetype's defining
  image and it is one draw call.
- The delivered **cloud band** stays independent, at +130/+160 m.
- Fog end 6 km, with a stronger sky-affect so peaks dissolve into the gradient.
- Sun: standard rule; prefer an azimuth that rakes across the terrace stack so risers cast onto the floor
  below (it makes the vertical structure legible in shadow as well as in silhouette).
- Horizon ring: peaks to 900 m rising **through** the deck.

### 4.9 Ground treatment and VFX
- Terrace surfaces produce a fine, pale dust.
- **Falling through the cloud band** should get a visible transit effect: a brief wash of brightness and a
  particle streak as the ball passes each sheet. One of the cheapest, most memorable moments available.
- The Overdrive causeway (`10`) is the archetype's showcase for the Overdrive treatment (`14 §10.3`).

### 4.10 Generation requirements [GEN]

| # | Requirement |
|---|---|
| S1 | Terrace **risers** are emitted as cast-concrete structure surfaces (WORKS palette), distinct from the terrace surface's EARTH palette. |
| S2 | Every riser carries its **floor number** as a stencil, 20–40 m glyph height, facing the approach from the floor below. |
| S3 | A **chevron run** is placed at every drain, on the floor below, pointing back toward the primary. |
| S4 | The **cloud deck** is enabled at primary mean − 250 m and recorded in `StageDefinition`. |
| S5 | Terrace inner-edge cliffs carry a SIGNAL edge strip along the **top** edge (so the drop is visible from the terrace) and **no** strip at the foot. |
| S6 | Banners are placed only on structures ≥30 m from the corridor edge and never under the headroom envelope. |
| S7 | Crystal scatter is recoloured out of SIGNAL (shared with H7). |

### 4.11 Traps
- **The causeway in `10` is a viaduct over a void with a 20 m deck.** The real floor-2 terrace is a 150 m-wide
  corridor 200 m to the side of the primary. Build the *look* (edge strips, risers, the deck below cloud), not
  the dimension.
- **Do not roof the corridor with the cloud band** — it is at +130 m, well above the ball, and it must stay a
  thing you fall *through*, not a ceiling you drive under.
- Do not let banners or gantries violate the 91 m headroom envelope (`14 §17.4`).

---

## 5. Summit Descent

### 5.1 Concept references
**`14_CEL_MASTER_ridge_descent`** (a descending route with rim-lit edges and tiered distance — the closest
frame to this archetype), **`15_CEL_scree_slope_debris`** (the scree band, and a slope that reads as a fall),
`01_keyart_highlands_ridge` (snow and cloud), `09_spiral_pit` (descending-turns language),
`12_cel_dusk_ridge` (value structure on a descent), and `docs/12` for the geometry.

### 5.2 The idea in one line
**Six hundred metres of engineered mountain road, walled on every side, dropping from a snow summit through
scree and cloud to a green valley — the programme's most obviously *built* landscape.**

### 5.3 What the player is doing here
Hairpin commitment and line choice at the cap on a continuous descent: holding 140–156° hairpins of 80–120 m
radius, reading blind corners, choosing the chute over the switchback. The art's job is to **make every
hairpin legible before it is entered** (the archetype has a declared sightline exception, `docs/12 §3e`) and
to **make a missed line read as a wall, not a fall** — which is the archetype's central promise.

### 5.4 Landform rules
From `docs/12`, unchanged (design accepted, not yet built):
- Summit start, **600 m of descent**, 4–6 switchback tiers, no mandatory climb.
- Legs at grade 0.06–0.13; hairpins **80–120 m radius** carrying 45–85 m of drop as banked descending curves.
- Every convex exit eased to the **560 m** crest radius so a base-cap ball never launches.
- **Walls everywhere**: 24 m near-vertical rails on every downhill edge, **90 m headwalls** behind every
  hairpin's berm, a cut face uphill.
- The **chute**: a 40 m rail gap, a 150 m eased lip onto a 0.35 walled straight, a merge bend onto the next leg.
- Valley run with 12 m guide banks; exits fork there.

### 5.5 Palette (height-banded — the archetype's identity)

| Band | Lit | Shade | Height |
|---|---|---|---|
| Summit cone / snow | `#E8ECF2` | `#9FB0C8` | above +350 m over the valley floor |
| **Rail shadow faces in snow** | — | `#8DA3C4` (distinctly blue) | per `docs/12 §7` |
| Scree | `#7A7268` | `#3A362F` | +150 to +350 m |
| Rock and moss | `#5C6B45` / `#6E6A5C` | `#2C3325` | valley run |
| Headwall / cut face rock | `#6E6A70` | `#33313A` | every height |
| Cast rail and headwall facing | `#B9B0A4` | `#4A4652` | every height |
| **Rail crest line** | `#D8D2C6` on the top 2 m | | per `docs/12 §7` — mandatory |

The **blue shadow on snow** is the most important single value in this table: it is what makes a white rail
read as a bank rather than as a flat white void, and it is free (it falls out of the violet ambient in
`14 §4.1`).

### 5.6 Structure vocabulary
- **The wall** as rails (24 m) and headwalls (90 m). Headwalls behind hairpins are the archetype's hero
  structures — they should carry the largest signage in the game.
- **The dam/retaining arc** as hairpin berms.
- **The portal** — `TunnelChance` 0 until slice S2.
- **The deck** — not used in the first cut.

### 5.7 Signage and props
Per `docs/12 §7`, all confirmed by the art's language:
- **Hairpin boards** (`↰ 4`) — a countdown board before each hairpin, on the headwall, at 20–40 m glyph height.
  This is a racing-game affordance and it is exactly right for an archetype with declared blind corners.
- **`CHUTE ↓`** at each rail gap.
- **Chevron runs** on berm crests through every hairpin, and on the approach to every chute gap.
- **`EXIT A/B/C`** in the valley run.
- Scatter: boulders on the face outside the rails, snow drifts against the uphill cut, none on benches.

### 5.8 Light, sky and fog
- **Cloud deck ON at valley floor + 150 m** — so the summit looks down onto a filled valley and the descent
  passes *through* the deck around tier 3 or 4. That transit is the archetype's signature moment and it costs
  nothing.
- Sun **low and across the face**, so rails cast down onto the bench below.
- Fog end **longer than the family's — 7 km** (`docs/12 §7` asks for it) so the valley reads from the summit.
- Horizon ring: a cirque of higher summits.

### 5.9 Ground treatment and VFX
- Snow spray on the upper tiers (bright, light, short-lived), scree dust in the middle, earth dust in the
  valley — **the particle colour changes with the height band**, which is a cheap way to make the descent feel
  like progress.
- Rail impacts throw concrete dust and sparks, never debris chunks (they would read as a fall).
- The chute's walled straight is the best place in the game for the Overdrive streak treatment.

### 5.10 Generation requirements [GEN]

| # | Requirement |
|---|---|
| M1 | **Rail crest lines**: the top 2 m of every rail is tinted to `#D8D2C6`, on both the snow and rock bands. |
| M2 | **Hairpin boards** generated on every headwall, carrying the count of remaining hairpins, facing the incoming leg, readable from ≥300 m. |
| M3 | **`CHUTE ↓` signs** at every rail gap, plus a chevron run on the approach. |
| M4 | Particle palette is selected by **height band** (snow / scree / earth). |
| M5 | The **cloud deck** is enabled at valley floor + 150 m and recorded in `StageDefinition`. |
| M6 | Fog end **7 km** on this archetype. |
| M7 | Rails and headwalls obey the ride-band rule: no relief below the rail's lip; the crest line is a tint, not a moulding. |
| M8 | Sun azimuth is drawn to rake across the face (perpendicular to the fall line ±30°). |

### 5.11 Traps
- **The rails are 24 m tall and the corridor is 75–150 m wide.** Do not draw a narrow alpine road.
- **Walls everywhere is a promise, not a suggestion** — the archetype's whole risk profile assumes a miss is
  an impact. Any art treatment that implies a drivable gap in a rail where there is none is a bug.
- Do not put a hairpin board where it blocks the sightline it exists to serve.

---

## 6. Tunnels, portals and pockets (cross-cutting set-piece)

### 6.1 Concept references
`03_canyon_wallride_portal` (the portal in a canyon wall, with lit interior), `13_cel_valley_portal_hud`
(the circular portal in a mesa, read from distance).

### 6.2 What the art confirms
Both images independently confirm the delivered design (`13 §2.6`, D-113/D-116) with no conflict:
- The mouth is **an arched opening with a proud rim** in a vertical face.
- The interior is **rock and concrete, lit by discrete wall lights**, not by ambient.
- The **far end's daylight is visible down the bore** and is the strongest read cue.
- The approach is **aimed** — you see the opening square-on from far out.

This is the single best-corroborated piece of the whole design and it should be built exactly as specified.

### 6.3 Art rules
- **Portal rim**: 1.5 m proud, WORKS palette, catching the key light so the ring reads against the face at
  ≥600 m.
- **Portal face**: cast concrete, with the archetype's stencil and a sector number.
- **Interior**: delivered darkening gradient (100% at mouths → 45% at 60 m in). Trench floor keeps the
  archetype's **ground** palette; walls and arch are WORKS with a strata tint.
- **Guide strips**: emissive SIGNAL bands at ball height every 25 m on both walls (delivered). At 148 m/s
  these tick past at 5.9 Hz — a perfect speed cue, and the reason they are at ball height rather than overhead.
- **The cap** (the drivable surface over the tunnel) reads as EARTH ground, not as a roof, because from above
  it *is* ground. Only the portal ends reveal the structure.
- **The open cut** of a dive (D-116) is the read cue: the trench's walls rise out of flat ground and the
  landscape closes over the line. Give the cut's lip a chevron run and an edge-strip termination so the player
  reads it as an entrance, not as a hole.

### 6.4 Pockets (slice T3, not yet built)
- A bowl 120–200 m across, sunk 25–40 m, rim walled all round, reachable only by tunnel.
- **Art intent:** the pocket is the one place in RUSHCORE that is *quiet*. Lower the ambient, let the rim
  shadow it, put a single shaft of key light across it, and let the reward glow be the brightest thing in
  frame. It should feel found.
- The rim's inner face is cast; the floor is ground.

### 6.5 Generation requirements [GEN]

| # | Requirement |
|---|---|
| T1 | Portal faces carry a stencil and a sector number from the cosmetic seed. |
| T2 | The open cut of a dive receives a chevron run 150 m before the lip and an edge-strip termination at the lip. |
| T3 | Guide strips remain at ball height, 25 m spacing, SIGNAL colour, no collider (delivered — do not move them overhead). |
| T4 | A pocket's ambient is locally reduced and its reward carries the frame's brightest value. |

---

## 7. The spiral pit (cross-cutting set-piece)

### 7.1 Concept reference
`09_spiral_pit` — the most directly buildable image in the entire set.

### 7.2 What the art establishes
Image `09` is very nearly a render of the delivered spiral pit (D-102: 380 → 150 m radius, one full turn
through four quarter arcs, descending 120 m, terraces cut into a cone, exit pad on the floor). It adds four
things, all cheap and all adoptable:

1. **Radial buttresses** between the turns — vertical concrete fins from the cone face, at the 12 m modulus.
   They give the cone scale and stop it reading as a smooth funnel.
2. **A continuous lit edge on every turn's outer lip** — the SIGNAL edge strip, spiralling. From the rim, the
   whole descent reads as a single luminous helix. This is the best argument in the set for the edge-strip
   system.
3. **Chevrons on every turn**, pointing down-spiral.
4. **Slogan slabs on the rim**, facing inward (`GRAVITY BUILDS BETTER RACERS`, `DEEPER FASTER FURTHER`,
   `A DEEPER HUMANITY` — the tagged corpus).

### 7.3 Art rules
- The cone face is **natural rock**; the turn surfaces are **ground with track tint**; the buttresses, rim
  and retaining faces are **cast concrete**. Same law as the canyon wall (§2.5).
- Lighting: the pit is the one place where the low sun cannot reach the bottom. Let it go genuinely dark at
  the floor — with the ball's own light (`14 §10.4`) and the edge strips carrying the read. The descent from
  lit rim to shadowed floor is a dramatic arc that costs nothing.
- The exit pad on the floor is cast concrete with a SIGNAL ring — the brightest thing at the bottom.

### 7.4 Generation requirements [GEN]

| # | Requirement |
|---|---|
| P1 | **Radial buttresses** at the 12 m modulus between turns, generated with the pit, no collider inside any turn's level width plus setback. |
| P2 | Every turn's outer lip carries the SIGNAL edge strip at full width (a pit is a committed line). |
| P3 | A chevron run on every turn, pointing down-spiral. |
| P4 | Slogan slabs on the rim facing inward, from the cosmetic seed. |
| P5 | The pit floor's exit pad carries the brightest SIGNAL value in the stage. |

---

## 8. Enemies and pickups under this direction

### 8.1 The state of it
**No concept image contains an enemy.** The four archetypes plus the elite (`02 §7`, `06 §8`) are therefore
undesigned under this direction, and this is the largest outstanding art commission in the project.

### 8.2 What they must satisfy, restated for the concept brief
Any proposal must hold all of these simultaneously:

- **Family:** enemies are WORKS, not EARTH — they belong to the programme (machines, markers, automata), which
  is the only reading consistent with the fiction. They must **not** be SIGNAL (that colour means the player).
- **Sizes are fixed** (`06 §8`): Pylon 9.1 m, Bulwark 11.2 × 6.3 × 5.6 m, Strider 8.4 m body on 5.6 m legs,
  Shooter 8.4 m overall, Elite ×1.3 with a halo and crown.
- **Mass must read from silhouette alone** at 900 km/h — the Pylon must look rammable and the Bulwark must
  look like a wall, from 400 m, in fog, in shade.
- **The elite treatment is identical on all four.**
- **Hazard orange is available and reserved**; it may appear on the Bulwark's band and nowhere else.
- No persistent text labels (`06 §8`).

### 8.3 Pickups
The delivered shape language (`06 §9`) is sound and the art does not contradict it. Two changes follow from
the colour law:
- **Boost rings** move from `#4DE6B8` to SIGNAL cyan `#5CD8F0`, and gain a diegetic home: a **gantry**
  (`14 §9.2`) frames each ring, so a boost ring is something the programme built over the route.
- **Exit pillars** move from mint `#9EFFB8` to WORKS concrete with a SIGNAL edge, per `14 §6.4`.
- **Teal XP orbs** and **gold cash balls** (the 2026-09-20 progression spec) fit the law as-is: teal is SIGNAL,
  gold is the sanctioned warm signal.

### 8.4 Commission
A second concept round covering: the four enemies to scale against a 1.32 m ball; the elite overlay; a gantry
with a boost ring; an in-map shop landmark (`14 §20`, still undesigned); the XP orb and cash ball; and one
enemy encounter framed from the chase camera at Crush speed.

---

## 8A. Cel-specific notes, per environment

The cel ladder (`17 §3`) is global, but each archetype stresses it differently. These are the per-environment
consequences the graphics developer should expect.

| Archetype | What cel does well here | What will fight it | Mitigation |
|---|---|---|---|
| **Rolling Highlands** | the height ramp becomes a contour map of flat plateaus; ridge rims define every crest (the master frame is this archetype) | long swells at 1000–1600 m wavelength produce very large single-plateau areas that can read as empty | rely on the rim at every crest (H3's crest lines double as rim anchors) and on scatter parallax for mid-distance value |
| **Canyon Run** | the strata bands are already flat colour steps — they cel-shade perfectly; the lit-wall / shade-wall split (C5) gives the archetype its whole read | the wall shell's smooth normals against the flat-normalled grid (F1); the rim must be suppressed on the ride band (`17 §5.3`) | F1's normal blend at the fillet foot, plus the `CUSTOM0` ride-band flag |
| **Dune Sea** | lee-face darkening becomes a hard two-value wave — the crest lines will read more strongly than in any painterly treatment | a dune field is smooth and curved; a hard terminator sweeping across it can crawl as the sun-relative normal changes slowly | widen `cel_terminator_width` slightly for this archetype only, or accept the moving terminator as the wave's read (test first — it may be a feature) |
| **Sky Terraces** | risers (concrete, vertical) and surfaces (stone, horizontal) land on different plateaus automatically; the cloud deck is a flat unlit plane, which is native to the style | a low-key image plus a bright cloud deck below inverts the usual value order — the ground can go darker than the sky under the player | raise the deck's shade tone and drop its luminance so it never exceeds the lit terrain plateau |
| **Summit Descent** | snow gives the one genuinely bright EARTH plateau in the game; blue shadow on snow is exactly the ladder's violet ambient | snow's lit plateau risks exceeding the SIGNAL family's luminance (F8) | cap EARTH luminance at 0.85 after tiering; the ball and its trail must stay the brightest things on a snowfield |
| **Tunnels / pockets** | the deep-shade plateau (`17 §3.3`) gives interiors a third value for free; the delivered darkening gradient layers on top | a fully flat interior with no rim can become unreadable | the guide strips are SIGNAL and unlit, so they hold; enable deep shade and keep the far portal's daylight |
| **Spiral pit** | the descent from lit rim to shadowed floor is a value arc the ladder renders for free | every turn is a curved bank — terminator crawl around the helix | the lit edge strip on every turn (P2) carries the read regardless of where the terminator sits |

## 9. Per-environment acceptance shots

For review, each environment must produce these three stills from the in-game chase camera, debug off:

| Environment | Shot A | Shot B | Shot C |
|---|---|---|---|
| Rolling Highlands | ridge line at Overdrive, dam in the middle distance | a launch crest read from 600 m out | a dive's open cut swallowing an optional line |
| Canyon Run | wall ride on a bend outside, stencil above the lip | portal approach from 400 m | spiral pit entry from the rim |
| Dune Sea | crest line launch against a low sun | a dune train read from its approach | an isolated monolith with drifts |
| Sky Terraces | three floors stacked above the cloud deck | a fall through the cloud band | a riser's floor number from the floor below |
| Summit Descent | the summit start looking down the whole descent | a hairpin with its board and headwall | the chute gap with its sign |

Every shot must satisfy `14 §19`'s ten acceptance criteria.

---

## 10. Summary of generation requirements, by owner

| Owner | Requirements |
|---|---|
| `StageGenerator` | G1, G2, G4, G5, H5, S4, M5, D4, C5, C6, D6, M6 (per-stage art parameters: sun, deck, fog) |
| new `EdgeStripBuilder` | G3, G4, H6, C7, S5, D7, P2 |
| `WallShellMesh` | G9, G10, G11, C1, C2, C3, C4, M1, M7 |
| `LidBuilder` (extended) | G8, P1 |
| `WorldDressing` | G6, G7, G14, H1, H2, S2, S6, M2, M3, T1, P4 |
| `StageScatter` | G13, H4, H7, D5, S7, M4 |
| `OptionalLineBuilder` | G5, T2 |
| `StageHeightField` / `SampleColor` | H3, D1, S1, and every palette table above |
| `TunnelProfile` | T3, C8 |

**Every one of these draws from the cosmetic seed and must not move a golden stage hash (G15).** That
assertion is the first thing to write and the last thing to check.
