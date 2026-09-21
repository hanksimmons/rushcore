# 14 — Art Direction Bible

**Status:** PROPOSED 2026-09-20, derived from the concept-art set in `docs/concept/`. Nothing here is accepted
until the design team and the lead graphics developer sign it off and a D-number promotes it.
**Authority:** below `00`–`04` (product intent, scope, gameplay, movement, generation) and **beside** `06`
(`06_VISUAL_UX_SPEC.md`). `06` owns the *rules* of readability, HUD and procedural art; this document owns the
**chosen look** that satisfies them. Where this document and `06` disagree, `06` wins until a decision says
otherwise, and every such conflict is listed explicitly in §17.
**Owns:** the world fiction, the light and sky law, the colour law, the material and shader stack, the concrete
architectural language, signage and typography, the ball's visual identity, and the generation-logic
requirements the look imposes.
**Read with:** `docs/17_CEL_SHADING_TECHNICAL_SPEC.md` (the implementation of the decided style),
`docs/15_ENVIRONMENT_ART_BIBLES.md` (per-archetype), `docs/18_CONCEPT_REFERENCE_INDEX.md` (image-by-image
analysis), `06` (readability law), `11` (world scale), `13` (tunnels, resident mesh, horizon).

---

## 0. How to read this document

Every section is written to be **actionable by two different readers**:

- **Graphics/art**, who needs values, forms, materials and silhouettes.
- **Generation logic**, who needs rules the stage generator must satisfy so the art has somewhere to live.

Sections that bind the generator are marked **[GEN]** and collected again in §18 as a numbered, testable list.
Sections that bind the renderer are marked **[GFX]**. Sections marked **[DECISION]** are open questions the
design team must answer; a provisional recommendation is given for each so work is never blocked.

Three rules override everything else in this document, because they come from higher authority:

1. **Readability outranks beauty** (`06 §2`). The ranking is: progression direction → terrain shape/ramp/gap →
   enemy silhouette → reward line → exit → cosmetic detail.
2. **Archetypes change strategy through geometry only** (`04 §7`). Art may never imply a physics change that
   does not exist, and may never hide one that does.
3. **The movement baseline is frozen** (`03 §15`, D-095). Nothing in this document may be implemented by
   changing a movement value. The ball is 0.66 m; the base cap is 148.5 m/s; the ceiling is 254.7 m/s.

---

## 1. The world, and what the concept art decided

The concept set answers the biggest open question in `docs/13`'s handoff and in the art brief: **what is this
place?** The answer, consistent across all sixteen images, is:

> **RUSHCORE is a civic megaproject.** A vast, state-scale infrastructure programme has been cut into real
> wilderness — dams, retaining walls, viaducts, terraced platforms, spiral descent shafts, bored tunnels — and
> the programme's own propaganda is stencilled on it at building scale. The landscape is authentic: red rock
> canyons, green highland valleys, snow mountains, dune seas, cloud seas. The *construction* is monolithic
> board-formed concrete, clean, enormous, and utterly indifferent to the human body. Somewhere in all of it,
> one small hot-cyan sphere is travelling at nine hundred kilometres an hour.

The programme speaks in **slogans of optimistic collectivism** — the corpus lifted verbatim from the art:

> HIGHER FASTER FURTHER · SPEED SHAPES A BRIGHTER TOMORROW · TERRAIN BUILDS PEOPLE · A CLEANER HORIZON MOVES
> US ALL · VAST CLEAN RELENTLESS TOGETHER · GRAVITY BUILDS BETTER RACERS · MORE SPEED A KINDER PLANET ·
> CANYONS CONNECT HUMANITY · A SMALLER YOU A LARGER TOMORROW · DEEPER FASTER FURTHER · A DEEPER HUMANITY ·
> A BIGGER TOMORROW · PEOPLE MOMENTUM HIGHER · FOUNDATIONS MOTION MORE · ASCEND REFINE PUSH · PEAKS AHEAD ·
> HIGHER GROUND CLEANER LINES FASTER HUMANS · MOMENTUM BUILDS A BRIGHTER TOMORROW · A BIGGER CLEANER FASTER
> WORLD · RUSHCORE WORLDWIDE MMXXV · TERRA MOTUS ALTIORA · TERRA ALTITUDO OMNIA

**Tone:** sincere on the surface, faintly ominous underneath. Never winking, never comedic, never dystopian in
a rusted-and-ruined way — the programme is **new, clean and working**. This is the tonal register of a national
infrastructure film, not of a satire. The player is never told who they are or why they are rolling; the walls
simply encourage them.

**Why this fiction is the right one for the design:**

- It justifies **monumental smooth surfaces beside authentic terrain** — which is exactly what the engine can
  render (flat-shaded vertex-coloured heightfield + a small number of clean structure meshes with materials).
- It justifies **the wall ride** (`03 §3`, D-108): an engineered fillet and a cast face are a rideable surface
  in a way a natural cliff is not.
- It justifies **the corridor** as a built thing without making the stage a racetrack, because the built thing
  is a *valley-scale earthwork*, not a ribbon.
- It gives every readability cue a diegetic home: chevrons, edge lighting, sector numbers, gantries and pad
  markers are all things a programme like this would build.
- It gives the single cyan accent a meaning: the ball is the only thing in the world that is *moving*, and the
  programme lights its route in the same colour.

**[DECISION 1] — Name of the in-fiction body.** The art says only "RUSHCORE". Recommended: leave it. The
programme and the game share a name; the slogans do the rest. Alternative: an authority name ("THE RUSHCORE
AUTHORITY", "RC WORLDWIDE") used on plaques only.

---

## 2. The look law, in one paragraph

> **Cel-shaded faceted natural terrain — two flat plateaus, a hard terminator and a hot gold rim on every lit
> edge — interrupted by vast neutral-grey concrete structures, under a low golden-hour sun and a
> violet-to-orange sky, with most of the frame sitting in cool violet shadow and exactly one saturated accent
> colour — electric cyan — reserved for the player, their trail, and the lines the programme has drawn to
> guide them. Value contrast carries the read; hue is a second-order cue. Everything is enormous except the
> ball.**

If a frame cannot be described by that sentence, it is off-model.

---

## 3. The style is cel shading — **DECIDED 2026-09-20 by the user**

> **"We need the cel shaded style."** — the user, 2026-09-20, on seeing `14_CEL_MASTER_ridge_descent`,
> `15_CEL_scree_slope_debris` and `16_CEL_slam_impact_rings`.

**This decision is closed.** RUSHCORE is a **cel-shaded** game: hard-terminator banded lighting, flat colour
plateaus, hot rim light on every lit edge, and a low-key image in which most of the frame sits in a cool
violet shadow mass while narrow gold edges and one cyan accent carry the read.

The three images that decided it — `docs/concept/14`, `15`, `16` — are the **master style targets**. Where any
other image in the set disagrees with them, they win. `14_CEL_MASTER_ridge_descent` in particular is the
single reference to hold every build against; it is referred to below simply as **the master frame**.

The full implementation specification lives in **`docs/17_CEL_SHADING_TECHNICAL_SPEC.md`**. This section
records only *why* it is the right decision and what it costs.

**Why cel is correct here, beyond the user's preference:**

- **It is what the engine can actually render.** The terrain is a flat-shaded, UV-less, vertex-coloured
  heightfield at two cell sizes (`04 §9`, `13 §3.3`). Cel shading wants exactly that: flat planes, no texture,
  banded light. The painterly alternative needed a terrain pipeline that does not exist and cannot be added
  inside the budgets in §16.
- **It is the readability law made visible.** `06 §2` ranks route legibility above all cosmetic detail. A
  two-value image with hot edges *is* a legibility system: the lit band tells you where the ground faces the
  sky, the rim tells you where an edge is, and the shadow mass tells you where you are not going.
- **It holds up at 255 m/s.** Flat plateaus do not alias, do not shimmer, and do not need temporal
  antialiasing. Photographic rock at 4 m facets turns to noise in motion.
- **It aligns with `06 §1` and `06 §17`** ("low-poly, N64-inspired", retro restraint, palette quantization
  permitted) without the failure modes `06 §17` rejects (no vertex wobble, no forced 240p, no affine warping,
  no readability-destroying dither).
- **It is cheap to author.** No texture budget, no normal maps on terrain, no LOD-matched material work.
  Structures carry their detail in silhouette and flat panel value, not in maps.

**What it costs, honestly:**

- Flat plateaus are unforgiving of bad geometry. Every kink in the heightfield, every facet seam, every
  wall-shell/grid mismatch becomes a visible hard-edged shape rather than being hidden by texture noise. The
  wall-shell seam (`17 §6`) and the fine/coarse mesh boundary (`13 §3.3`) are now **art-critical**, not just
  technically-critical.
- A low-key image needs its bright elements to be genuinely bright and genuinely rare. Any additional glowing
  thing steals from the ball. The colour law (§6) stops being a preference and becomes load-bearing.
- Distant readability depends entirely on **value tiering** (`17 §5.4`), because there is no texture detail to
  fall back on.

### 3.1 What the master frames establish, in order of importance

1. **Two values per surface, not a gradient.** Every plane in `14` resolves to a lit value and a shade value
   with a hard boundary between them. There is no soft falloff anywhere in the frame.
2. **A hot gold rim on every lit edge.** Ridges, cliff lips, rock shards and structure edges all carry a thin
   saturated gold band where the surface turns away from the light. This single effect does more work than
   any other in the style — it is what makes flat polygons read as sculpted rock.
3. **The frame is mostly dark.** Measured across `14`, `15` and `16`, the dominant plateaus are violet-greys
   between `#181828` and `#484848`, occupying 50–70% of the image. Lit gold covers well under 10%.
4. **Shadow is violet, light is gold.** The warm/cool split from §4.1 is not subtle here; it is the entire
   modelling system.
5. **Clouds are flat shapes with hard edges**, stacked in bands, two-tone, with the sun a plain pale disc.
6. **Distance is rendered as flat value tiers**, not as fog blur — each successive mountain range is a flatter,
   lighter, more violet plateau (`14`'s left third is a textbook example).
7. **Dust is drawn, not simulated.** `14` shows the ball's dust as a row of discrete rounded puffs in a single
   flat grey. This is a style rule with a real cost saving; see `17 §8`.
8. **Debris is silhouette plus underglow.** `15` shows thrown rock as flat near-black chunks lit *from below*
   by the cyan trail. The trail lights the world; the world does not light the debris.
9. **The impact rings are hard, thin and white-hot.** `16` renders the delivered slam/burst VFX (D-077)
   almost exactly: a flat ground shock ellipse, a vertical bow arc, radial crack streaks, and thrown chunks.

### 3.2 The superseded fork (kept for the record)

Before the decision the set contained two treatments. The comparison is retained because it documents what
was given up.

| | **A — Painterly realism** (`01`, `03`, `07`, `08`, `09`, `10`, `11`) | **B — Graphic / cel** (`04`, `06`, `12`, `13`) |
|---|---|---|
| Terrain surface | photographic rock, stratified, detailed | large flat facets, per-facet tonal jitter, no texture |
| Lighting | soft, atmospheric, volumetric haze | hard terminator, two or three tonal steps, posterised |
| Concrete | board-form texture, weathering, AO | flat planes, value blocks, thin bright edge lines |
| Silhouette read at speed | good | **excellent** |
| Cost at 1.2 M triangles and 4 m cells | **cannot be reached** — needs UV'd, normal-mapped, LOD'd terrain the heightfield architecture does not have (`04 §9`, `13 §3.3`) | **reachable today** — it is what `FacetJitter` + `BasePalette` already do |
| Consistency with `06 §1` ("low-poly, N64-inspired") and `06 §17` (retro restraint) | in tension | aligned |

**Outcome: B, and harder than B.** The user's choice went past "graphic realism" to full cel shading, which
images `12`, `13`, `14`, `15` and `16` exemplify. The painterly images (`01`, `03`, `07`, `08`, `09`, `10`,
`11`) are retained as **composition, scale and lighting-mood references only** — never as surface targets.

Everything below is written for cel. A handful of places still permit extra density on **structures**, which
are separate UV-capable meshes; they are marked **[A-density]** and are bounded by `17 §7`.

---

## 4. Light [GFX]

### 4.1 The hour is fixed

**Golden hour, always.** Every image in the set is within roughly 30 minutes of sunset. This is a hard
identity choice, not a mood: it gives long raking shadows that reveal terrain undulation (the thing the player
must read), a warm/cool split that separates lit ground from shadowed rock for free, and a sky bright enough
to silhouette every structure.

- **No time-of-day cycle.** Explicitly out of scope (`13 §0`).
- **Sun elevation: 4°–9° above the horizon.** Below 4° the terminator crawls and shadows stripe the corridor
  unreadably; above 9° the long-shadow read is lost.
- **One dominant `DirectionalLight3D`**, as delivered (`06 §16`), colour **`#FFE7B8`** (warmer than today's
  `#FFF2DB`), energy ~1.15.
- Ambient/fill from the `WorldEnvironment` sky, tinted violet-blue **`#5E4A78`** at roughly 0.35 energy, so
  shadowed faces go cool and the warm/cool split does the modelling work that a normal map would otherwise do.
  **This is the single most important lighting rule in the document** — it is what makes untextured flat
  surfaces read as material.

### 4.2 Sun azimuth is a generation input [GEN]

In the art the sun sits ahead of or beside the player and is *never* dead-centre in the travel direction at
ground level. Reproduce that as a rule, not a hope:

- The sun's azimuth is chosen per stage from the stage seed, constrained to **35°–145° or 215°–325° from the
  stage's primary axis** — i.e. never within 35° of straight ahead, never within 35° of straight behind.
- Ahead-and-to-one-side is preferred (weight the 35°–80° band): it puts the sun in frame for the vista shots,
  rakes the corridor across the travel direction, and lights the *inside* faces of wall bands the player is
  about to ride.
- Sun azimuth is recorded in `StageDefinition` so it is deterministic and reproducible from the seed, and it
  is exposed as a debug slider.

### 4.3 Shadows

- Directional shadow only. **No local dynamic lights** except the ball's own (see §10.4).
- Shadow distance is the steering horizon, not the draw distance: **600 m**, with a 4-split cascade. Beyond it,
  terrain relies on slope shading alone, which at 16 m coarse cells is sufficient.
- **Structures cast; terrain casts.** Scatter props below 8 m do not cast (cost, and they flicker at speed).
- Shadow bias must be tuned against the 4 m facet size, not against prop scale: the facet is the shadow
  receiver that matters.

### 4.4 The cel ladder [GFX] — **the core of the style**

Since 2026-09-20 this is not a "three-value discipline" but a hard cel ladder. Full implementation in
`17 §3`; the direction-level statement is:

Terrain and structure shading resolve to **two plateaus, one hard terminator, and one rim**, with an optional
third plateau reserved for the deepest occluded shade.

1. **Lit plateau** — full key, warm. Flat. No falloff across the plane.
2. **Hard terminator** — a step, not a gradient. Step width ≤0.04 in N·L, feathered only enough to
   antialias (roughly one pixel at 1080p).
3. **Shade plateau** — ambient only, cool violet. Flat.
4. **Rim** — a thin hot gold band where the surface turns away from the key. **Mandatory, not optional**:
   this is the effect that makes cel terrain read as rock (§3.1 item 2).
5. **Deep shade** (optional third plateau) — for surfaces facing fully away plus downward, e.g. under a lid,
   inside a pocket, at a pit floor.

**No smooth lambert appears anywhere in the game**, including on props, structures, the ball and the horizon
ring. A single smoothly-shaded object in a cel frame reads as a bug.

---

## 5. Sky, cloud and atmosphere [GFX]

### 5.1 The gradient

Measured from the set (medians across all thirteen images):

| Zone | Value | Note |
|---|---|---|
| Zenith | **`#3C2A5A`** | deep violet; today's `#294D87` is too blue and too light |
| Upper sky | **`#5E3F5D`** | violet-magenta |
| Horizon band | **`#C8763C`** | hot orange, narrow |
| Sun disc / core | **`#FFF0C8`** | small, hard-edged, bloom-free core |
| Sun halo | **`#FDD76B`** | the brightest 2% of every image lands here |
| Ground horizon (below) | **`#6B5A50`** | warm neutral |
| Ground bottom | **`#241E22`** | |

The horizon band must be **narrow and hot** — roughly the bottom 8% of the sky hemisphere — with the violet
taking over quickly above it. That narrowness is what makes the sky read as sunset rather than as a generic
warm gradient.

### 5.2 Clouds

The art shows three distinct cloud systems. All three are in scope; all three are cheap.

1. **The cumulus band** (every image): hard-edged stylized cumulus, **two-tone** — lit top `#FFD9A0`, shaded
   base `#6E5170` — sitting in a band between roughly 8° and 25° above the horizon. This is the planned cloud
   dome of `13 §4.2`: a 15 km hemisphere, unshaded, two thresholded value-noise layers, slow scroll. The art
   confirms the design and fixes its colours. **Hard-edged is mandatory** — soft volumetric cloud destroys the
   graphic read and costs what we do not have.
2. **The cloud sea** (`01`, `07`, `09`, `10`, `13`): a horizontal deck the player looks *down* onto from
   elevated stages, with peaks and structures piercing it. This is new and it is the single biggest
   contributor to the "massive world" impression in the set. Implement as **one unshaded horizontal plane**
   at a per-stage altitude with the same two-layer noise and a soft alpha edge, drawn after the horizon ring
   and before the sky. Cost: one draw call. See §5.4.
3. **The Sky Terraces cloud band** (delivered, D-103): two translucent sheets at 130 m and 160 m above the
   primary's mean height, 55% and 35% alpha. This stays exactly as delivered and is *not* the cloud sea —
   it is a local, drivable-through atmospheric layer. Both may coexist on that archetype.

### 5.3 Fog

- Depth fog as delivered (`13 §4.3`): begins **800 m**, ends **6 km**, sky affect raised.
- Fog colour is **not** a constant: it must be the sky's horizon colour in the sun's direction and the sky's
  upper colour away from it. A cheap approximation that matches the art: tint fog by the sun-view dot, from
  **`#C8763C`** looking into the sun to **`#6A5A7A`** looking away. This one change does more for atmospheric
  depth than any amount of extra geometry.
- **Fog never begins inside the steering horizon (450 m).** Non-negotiable (`11 §3h`, `06 §2`).

### 5.4 Cloud-sea altitude is a generation input [GEN]

The deck is only correct when it sits *below* the player on the elevated archetypes and *above* nothing on the
low ones:

- Sky Terraces: deck at **primary mean height − 250 m**, so floors 2 and 3 look down onto it.
- Summit Descent: deck at **valley floor + 150 m**, so the summit start looks down onto a filled valley and
  the descent passes *through* it — the single best-value shot the archetype has.
- Canyon Run: deck **off** (the canyon is below the surrounding terrain; a deck would roof it).
- Rolling Highlands: deck **off** by default; **on** for a seed whose route mean height sits in the upper
  third of the footprint, at mean − 200 m.
- Dune Sea: deck **off** (a dune sea reads as heat and dust, not altitude; see `15 §3`).

The deck's altitude is recorded in `StageDefinition` and is deterministic.

---

## 6. The colour law [GFX] [GEN]

### 6.1 Three families, and only three

Every pixel in a RUSHCORE frame belongs to one of three colour families. **A fourth family is a bug.**

| Family | Role | Saturation | Hue range | Where |
|---|---|---|---|---|
| **EARTH** | the natural world — terrain, rock, sand, snow, vegetation | low to medium | warm: 15°–55°, plus desaturated greens 70°–120° | the heightfield, scatter, horizon ring |
| **WORKS** | everything the programme built — concrete, steel, signage | **near-zero** (chroma < 0.08) | neutral, faintly warm in light, faintly violet in shade | structures, pads, signs, props |
| **SIGNAL** | the player, their energy, and the lines drawn for them | **maximum** | cyan 185°–195° only | the ball, trails, edge strips, chevrons, rings, HUD |

Plus one reserved exception:

| **HAZARD** | threat, and nothing else | high | orange-red 8°–14° | the Bulwark's band and the high-risk shortcut post *only* (`06 §9`) |

**The law:** *a saturated cool colour in a RUSHCORE frame always means "this is you, or this is your line".*
That is why the accent must never be spent on decoration. No cyan windows, no cyan ambience, no cyan rock, no
cyan sky. When image `10` puts cyan light strips down a causeway, it is telling the player where the drivable
surface is — that is a legitimate use and it is the *only* kind.

### 6.2 The SIGNAL palette, measured

Sampled across the whole set; the consistency is remarkable and should be treated as canon.

| Use | Value | Notes |
|---|---|---|
| Core / emitter centre | **`#6CE7FB`** | the ball's glow core; the hottest cyan in the game |
| Trail body | **`#45C8E0`** | the parallel streak lines |
| Trail falloff / haze | **`#3F9AC4`** | where a trail meets dust |
| Edge strip (structures) | **`#5CD8F0`** | emissive, unshaded |
| Chevron (lit) | **`#6CE7FB`** at 0.6 energy | |
| HUD primary | **`#7FE9F5`** | |
| Landing burst spark | **`#66C7FF`** | delivered, keep |
| Boom ring | **`#B8E6FF`** | delivered, keep |

The delivered `BurstColor` `#73CCFF` and `SparkColor` `#66C7FF` are already inside this family — **the existing
VFX colours do not need to change.** The change is to *remove* competing saturated colours elsewhere (see
§6.4).

### 6.3 The WORKS palette, measured

| Surface | Lit | Shade | Notes |
|---|---|---|---|
| Concrete, primary | **`#B9B0A4`** | **`#4A4652`** | the lit/shade pair is the whole material |
| Concrete, secondary (older/lower) | **`#9A9288`** | **`#3E3B46`** | used for foundations and buttresses |
| Concrete, dark (signage slabs, lids) | **`#6B6862`** | **`#2A2830`** | |
| Steel / gantry | **`#7E7A74`** | **`#2E2C33`** | |
| Stencil ink | **`#3A3630`** on light, **`#D8D2C6`** on dark | | never pure black, never pure white |
| Panel joint line | **`#8E877C`** (light side), 1–2 px equivalent | | see §8.3 |

**Concrete is never pure grey.** It carries a faint warm cast in light and a faint violet cast in shade; that
split is doing the modelling work on a surface with no normal map.

### 6.4 What must be removed from the current build [GFX]

The delivered placeholder palette contains saturated colours that the colour law forbids. They are placeholder
debug/lab colours and their removal is part of adopting this direction:

- `_matGantry` cyan-teal `#33C7DB` — **keep** (it is SIGNAL, used correctly).
- `_matPickup` / `Boost` green-teal `#4DE6B8` — **move to SIGNAL cyan** `#5CD8F0`. A second cool hue splits
  the "this is your line" read.
- `Reward` `#5CDBFA` — already SIGNAL; keep, but differentiate XP from boost by **shape**, not hue
  (tetrahedron vs ring), which `06 §9` already requires.
- `Currency` `#FFCC4D` — **keep as the one warm SIGNAL exception**; gold reads as gold and the art's sunset
  supports it. Document it as the deliberate fourth signal.
- `ExitMarker` `#9EFFB8` (mint green) — **replace** with WORKS concrete + a SIGNAL cyan edge strip. Green is
  a fourth family and it does not appear anywhere in the concept set.
- `PylonCap` `#6BEBFF`, `ShooterAim` `#FFBD3D`, `Elite` `#FFDB6B` — enemy semantics; see `15 §7`.
- Route-debug polyline colours (magenta, yellow, etc.) are **debug-only** and are exempt, provided the debug
  views stay off by default (they do).

---

## 7. Material and shader stack [GFX]

> **Implementation detail moved.** Since the cel decision (§3), the authoritative shader specification —
> uniforms, ladder maths, rim derivation, outline policy, per-shader pseudocode, budgets and failure modes —
> is **`docs/17_CEL_SHADING_TECHNICAL_SPEC.md`**. What follows is the direction-level summary of *which*
> shaders exist and what each is for; where 17 and this section differ, **17 wins**.

The engine reality (`04 §9`, `13 §3.3`) splits the world in two, and the art direction must respect the split:

- **Terrain** is a flat-shaded, **vertex-coloured, UV-less** mesh regenerated at two cell sizes. It cannot
  take a conventional texture.
- **Structures** (wall shells, lids, tunnel roofs, pads, signs, props) are ordinary generated meshes that
  **can** carry UVs and materials.

Seven shaders cover the whole game. Build them in this order.

### S1 — Terrain cel (`terrain_cel.gdshader`) [A-density: no]
- **In:** vertex colour (albedo, from `SampleColor`), world position, flat normal.
- **Does:** the cel ladder (§4.4, `17 §3`) — lit/shade plateaus, hard terminator, hot gold rim; the height and
  slope tint stays CPU-side in `SampleColor` where it already is; the **strata bands** for walled archetypes
  stay there too; distance **value tiering** toward the fog/sky colour (`17 §5.4`) replaces simple fog blend.
- **Must not:** sample a texture, require UVs, compute per-pixel noise, or produce any smooth N·L falloff. At
  1.2 M triangles this shader runs on a very large number of fragments and must stay arithmetic-only.
- **Ships with palette quantization ON** (`17 §3.5`), 3 steps, exposed as `World › Palette Steps` for
  comparison. This is no longer optional: it is part of the decided style.

### S2 — Concrete (`concrete.gdshader`) [A-density: yes]
- **In:** triplanar world-space projection (structures have UVs, but triplanar removes all UV authoring for
  procedurally swept shells), a panel-joint mask, an AO-ish curvature term.
- **Does:** lit/shade colour pair (§6.3), board-form horizontal banding at the panel modulus (§8.2), a thin
  darker line at panel joints, subtle vertical streaking below horizontal edges (the single cheapest
  "this is real concrete" cue), dirt accumulation weighted by downward-facing normals.
- **Must not:** use a normal map on the **ride band** (§8.5).

### S3 — Wall shell (`wallshell.gdshader`)
A variant of S2 that blends to terrain vertex colour at the fillet's foot, so a wall shell (D-111) marries the
heightfield it is sunk into without a visible seam. **This is the highest-risk shader in the list** — the seam
between the shell and the sunk grid is exactly where the eye goes.

### S4 — Tunnel interior (`tunnel.gdshader`)
S2 plus the delivered baked darkening gradient (100% at the mouths → 45% at 60 m in, `TunnelProfile.Shade`),
plus the emissive guide-strip band. No lighting change, as delivered.

### S5 — Emissive signal (`signal.gdshader`)
Unshaded, additive-ish, constant colour from §6.2, with an optional slow pulse (edge strips do **not** pulse;
rings and pads may). Used by edge strips, chevrons, rings, exit pillars, guide bands and the gantry lines.

### S6 — Sky dome + cloud sea (`clouds.gdshader`)
Two thresholded value-noise layers, two-tone, slow scroll, sun-direction lit (`13 §4.2`). One shader serves
both the dome and the horizontal deck; the deck passes a different projection and an alpha falloff.

### S7 — Player cel (`player_cel.gdshader`)
The ball's shell: near-black faceted body under the same cel ladder, a **white-cyan rim that is always on**
(the master frames show the ball as a silhouette with a bright rim, never as a lit sphere), the SIGNAL
emissive seam network (§10.2), state tint, charge compression bloom and burst flash. Partly delivered in
`PlayerVisual`; `17 §9` specifies the rest.

### S8 — Cel dust and debris (`celdust.gdshader`)
New, and required by the style: dust is **drawn as discrete flat puffs**, debris as **flat silhouettes with a
cyan underglow from the trail** (§3.1 items 7 and 8, `17 §8`). The delivered `GpuParticles3D` emitters keep
their motion; only their draw changes — flat unlit billboards with a hard alpha cutoff and at most two tones,
never soft-alpha smoke.

**Not in the stack, deliberately:** SSAO (cost, and the ladder substitutes), SSR, volumetrics, motion blur as
a post effect (see §15 and §17.6), parallax/POM, terrain splat mapping, and — importantly for a cel style —
**no screen-space outline pass**. Outline policy is in `17 §4`: RUSHCORE gets its edges from the rim term and
from hard value contrast, not from an edge-detect post-process, which at 4 m facets would draw a line around
every triangle in the world.

---

## 8. The concrete language [GFX] [GEN]

This is the part of the look that the art establishes most strongly and that the current build has least of.

### 8.1 The five forms

Every structure in the game is a recombination of five forms. Nothing else gets built.

1. **The wall** — a vertical or battered cast face, 20–120 m tall, with vertical pilaster ribs at a regular
   modulus. Images `01`, `05`, `11`, `13`. This is what a **wall shell** (D-111) becomes on a built stage.
2. **The dam / retaining arc** — a wall curved in plan, concave toward the valley, usually with a rounded
   crest and often with the route running along its top. Images `01`, `03`, `04`, `06`. This is the
   **banked-bend berm** and the **canyon slot wall** at their most expressive.
3. **The deck / viaduct** — a horizontal slab on regular piers crossing a void. Images `05`, `10`, `13`. This
   is the **lid** (`04 §5I`) and the **bridge** module.
4. **The buttressed shaft** — a circular or polygonal void lined with terraced ramps and radial buttresses.
   Image `09`. This is the **spiral pit** (D-102), and image `09` should be treated as its build target.
5. **The portal** — an arched opening in a vertical face with a proud rim. Images `03`, `13`. This is the
   **tunnel portal** (D-113) exactly as specified, and the art confirms the 1.5 m rim reads correctly.

### 8.2 The modulus [GEN]

All concrete is cast in a **12 m panel modulus** (chosen so that at the 4 m heightfield cell it lands on a
whole number of cells, and so that a 120 m wall is exactly ten panels).

- Vertical pilaster ribs every **12 m**, projecting **0.6 m**.
- Horizontal board-form banding every **2 m** within a panel (texture/shader, not geometry).
- Panel joints as a recessed line **0.15 m** wide.
- Large faces get a **24 m** secondary rhythm (every second rib heavier) so a 120 m wall does not read as
  wallpaper.

At the ball's 1.32 m diameter, one panel is roughly nine ball-diameters — which means the modulus is a usable
**speed and scale cue**: panels tick past at 12.4 per second at the base cap.

### 8.3 Chamfers and edges

Every exposed concrete edge is **chamfered 0.4 m**. This is not decoration: an unchamfered edge on an
untextured flat-shaded mesh disappears when it aligns with the light, and appears as a hard aliasing line
otherwise. The chamfer catches a highlight and defines the form at any angle. It is also cheap (two extra
triangles per edge run).

### 8.4 Weathering discipline [A-density]

The programme is **new and maintained**. Weathering is limited to:

- vertical streaking below horizontal edges and drainage points,
- a darker band at the base of every wall where it meets ground (water and dust, ~3 m tall),
- **no** cracks, no rust stains, no graffiti, no vegetation in joints, no rubble.

### 8.5 The ride-band rule [GFX] [GEN] — **critical**

`04 §5D` already says it in geometry terms: *"the rock is in the tint, not the mesh; shape variety belongs
above the lip, never on the ride band."* The art direction extends it to materials:

> **Below the wall's lip, on any surface the ball can ride, there is no geometric relief, no normal map, no
> protruding rib, and no decal with a raised profile. The ride band carries colour, strata, panel joints and
> stencil type — all of them perfectly flush.**

Pilaster ribs, gantries, signage frames, lights and every other projecting element start **above** the lip
line. This is a gameplay rule wearing an art costume: a 0.6 m rib on a surface a 1.32 m ball rides at 148 m/s
is a collision, and the wall ride (D-108) depends on the band being analytically smooth (`WallSurface`).

### 8.6 Where concrete is allowed [GEN]

Concrete is not a ground material. It appears only:

- as the **wall band** on a walled archetype (Canyon Run, Summit Descent rails and headwalls, pocket rims),
- as **lids, bridges and tunnel roofs/caps/portals**,
- as **pads** (start, exit, checkpoint) — a flattened disc of concrete with a chamfered edge,
- as **terrace risers** on Sky Terraces (the cliff faces between floors),
- as **signage and scale-cue props**,
- as **the spiral pit's lining**.

Everywhere else the ground is earth, rock, sand or snow. **The corridor floor is never a concrete road.** See
§17.1 — this is the most important conflict in the whole document.

---

## 9. Signage, typography and the propaganda layer [GFX] [GEN]

### 9.1 The type system

| Role | Treatment |
|---|---|
| Wordmark `RUSHCORE` | wide-tracked geometric sans, uppercase, letter-spacing ~0.35 em, weight medium |
| Slogans | same family, lighter weight, 3–4 short lines, ragged left, generous leading |
| Sector numbers (`01`, `03`, `07`) | very large, thin, uppercase numerals, often 60–80% of the slab height |
| Latin mottos (`TERRA MOTUS ALTIORA`) | small caps, wide tracking, used sparingly as a seal |
| HUD | monospace or tabular-figure sans, uppercase, wide tracking, hairline rules |

Recommended: a single wide geometric sans for everything (the delivered HUD monospace is retained only for
numerals, where tabular figures stop the speedometer jittering).

**[DECISION 3] — font licensing.** Placeholder use of a system face is fine; shipping needs a licensed or
open face. Recommended shortlist: Inter Tight / Archivo / Space Grotesk (open), set at the tracking above.

### 9.2 Sign families and their placement rules [GEN]

The stage generator must be able to place five sign families. All are **keep-out-safe by construction**
(`04 §5G` `StageScatter`) and all carry **no collider** except the monolith's base where noted.

| Family | Size | Where the generator places it | Purpose |
|---|---|---|---|
| **Wall stencil** | glyph height 8–20 m | on any wall face above the lip, facing the corridor, on straights only, every 400–800 m | identity, scale, parallax |
| **Slogan monolith** | 14–24 m tall slab, 1.5 m thick | outside the corridor keep-out, on high ground beside the route, facing the oncoming line | the scale-cue post upgraded (replaces the plain 200 m post) |
| **Sector marker** | 20–40 m numeral on a slab or wall | at each **checkpoint** (every 400 m of route today) | progress read without HUD |
| **Gantry** | spans the corridor, 18–30 m clear height | over the route at **boost rings** and at **act transitions** (`13 §3.1`) | a frame the player drives through; the ring's diegetic home |
| **Directional chevron** | 6–10 m, in runs of 3 | on wall faces **above the lip** at bend entries, and painted flush on the corridor floor at forks | the single most valuable readability asset in the set |

**Chevron rule [GEN]:** at every fork (an optional line leaving, a terminal line's drive-in ramp, a tunnel
portal's approach, a chute's rail gap) the generator places a run of three chevrons **flush on the ground**,
pointing along the branch, starting 150 m before the divergence. `02 §3` requires that route choice be
informed; this is how it becomes informed at 148 m/s.

### 9.3 The slogan corpus [GEN]

Slogans are drawn deterministically from the `CosmeticSeed` stream (never the gameplay stream, so a slogan can
never move a stage hash — `04 §5G`). The corpus in §1 is the starting set; each entry is tagged with the
archetypes it suits (`CANYONS CONNECT HUMANITY` on Canyon Run, `PEAKS AHEAD` on Sky Terraces and Summit
Descent, `A DEEPER HUMANITY` near tunnels and the spiral pit).

---

## 10. The ball [GFX]

### 10.1 Form

Across every image the ball is the same object: **a small dark sphere with a bright cyan energy structure.**
It is never a vehicle, never a creature, never a face. That answers open question 2 of the art brief:

> **The ball is a manufactured core — a polished dark object the programme moves through its landscape.**

- **Silhouette:** perfect sphere. Diameter 1.32 m (`03 §15`). Never deform the collider (`03 §13`).
- **Body:** near-black, low roughness, faceted — an icosphere at 2 subdivisions as delivered reads correctly
  at every distance the player sees it. Body colour **`#14171F`** (already delivered as `FaceDark`).
- **Seams:** a network of **inset emissive seams** in SIGNAL cyan tracing the icosphere's edges — visible in
  `01`, `07`, `08`, `09`. The seam network is what makes the ball read as rolling: as it turns, the seam
  pattern turns with it, and the player perceives spin without needing a painted pattern.
- **The equatorial ring:** several images show a brighter ring or halo around the ball, roughly 1.3× its
  diameter (`01`, `02`, `11`, `13`). Adopt it as the **speed-state indicator**: invisible at Roll, a faint
  arc at Rush, a full ring at Crush, a double ring with a shock disc at Overdrive.

**[DECISION 4] — the orange legacy.** The delivered ball is hot orange (`#FF6B1A`). The concept art is
unanimously cyan. Recommended: **go cyan**, and reassign orange to the Currency/gold signal and to the sun.
A cyan ball on warm terrain has higher value *and* hue separation than an orange ball on warm terrain, which
is the whole reason the art reads so cleanly.

### 10.2 The trail

The most distinctive element in the set, and precisely specified by it:

- **2 to 4 parallel hard-edged streak lines**, not a soft plume, emitted from the ball's lower hemisphere and
  running back along the exact travel direction.
- Length scales with speed: roughly **10 m at Rush, 60 m at Crush, 140 m+ at Overdrive**, with the count
  increasing from 2 to 4 across the bands.
- Colour: core `#6CE7FB`, body `#45C8E0`, fading to transparent, never to white.
- **They lie on the surface when grounded** (images `01`, `08`, `10`, `12` all show the streaks hugging the
  ground plane) and hang in air when airborne. That grounded-decal quality is what sells contact.
- A **dust/debris plume** accompanies them on natural ground (`03`, `08`, `11`, `13`) — warm, dull, unlit,
  in the EARTH family, thrown outward and *behind*, never upward into the view.
- On concrete the dust is replaced by a **fine bright spark scatter** — cheaper and it differentiates surface
  by feel.

### 10.3 State treatments

Mapped onto the frozen verb set (`03`), reusing the delivered VFX where they already match:

| State | Treatment | Delivered? |
|---|---|---|
| Roll | body only, seams dim | yes |
| Rush | seams lit, 2 streaks, faint ring arc | partial |
| Crush | seams hot, 3 streaks, full ring, ground dust doubled | partial |
| **Overdrive** | 4 streaks, double ring + shock disc, **the body itself goes bright at the leading edge**, air distortion ring ahead, roll dust at full | **no — this is the priority commission** |
| Charging | compression squash, seams pulse *brighter and faster* as charge rises, a cyan energy pool at the contact point, steering-lock read: the ring **stops rotating and locks square to travel** | partial |
| Jump release | radial snap ring at the lip, seams flash white-cyan | yes |
| Slam | body tints toward `#6B7AFF`, a single hard vertical streak, all trails collapse to one | yes |
| Slam landing | hot white flash `#FFF5CC`, dust ring, screen-space shake | yes |
| Landing burst | `#66C7FF` sparks, ground shock ring + air-parting bow ring `#B8E6FF`, stretch along travel | yes — keep exactly |
| Carve | debris to the *outside* of the corner, ball visibly rolling about its facing, ring tilts with the facing | yes |
| Wall ride | streaks lie on the **wall** plane, dust sheds downward under gravity, ring plane follows the wall normal | **no — needed** |
| Damage | red motes `#FF3D33` + shell flash, 0.3 s, player-local | yes |

### 10.4 The ball's own light

The ball carries **one small `OmniLight3D`** in SIGNAL cyan, range ~12 m, energy scaled by speed band. It is
the one exception to "no local dynamic lights". It matters most inside tunnels (`13 §2.6`) and at dusk, and
it is what makes the ball feel like it belongs to the world rather than being composited over it.

---

## 11. Composition and camera [GFX]

The set is unusually consistent in framing, and the rules below are extracted from it. They apply to the
**delivered chase camera** (`03 §14`, `06 §11`) and change none of its mechanics.

1. **The ball sits low and central-ish**: between 28% and 38% up from the bottom edge, within ±8% of centre
   horizontally. The delivered framing-pivot band (D-090) already enforces this; the art confirms the band's
   size is right.
2. **The horizon sits high**: between 55% and 72% up the frame. This is the single biggest compositional
   signature of the set — it gives two thirds of the frame to *where you are going* and one third to *what you
   are on*. The delivered fixed base pitch should be validated against it.
3. **A vertical mass occupies one side of the frame in every wide shot** — a wall, a monolith, a dam. It
   provides parallax, scale and a value anchor. **[GEN]** This is a placement rule, not a camera rule: the
   generator should prefer to put a wall, a terrace riser or a monolith **on one side of the corridor at a
   time** rather than symmetrically on both, except where the archetype demands a slot.
4. **The route is visible ahead as a continuous readable band** in every image — the eye can trace it from the
   ball to the horizon. This is the compositional statement of `06 §2`'s rule 1 and it is what the corridor
   edge treatment (§12) exists to guarantee.
5. **The sun is in frame in 11 of 13 images**, usually a third of the way in from one side. See §4.2.
6. **Nothing occupies the upper-centre** except sky, clouds and distant peaks. Gantries are the deliberate
   exception and they are framed to be driven through.

---

## 12. Reading the route at 250 m/s [GFX] [GEN] — the corridor edge system

The concept art solves a problem the current build has: at 148–255 m/s the player cannot tell where the
drivable corridor ends. The art's answer, used in every single image, is a **continuous luminous edge**.

**The system, as a generation rule:**

1. Along **both edges of the primary corridor's level width**, the generator emits a continuous **edge strip**:
   a flush, 0.8 m wide, unshaded SIGNAL-cyan band, following the corridor profile exactly, with **no
   geometry above the surface** (it is a decal-like strip mesh sitting 2 cm proud, or a vertex-colour band in
   the terrain — see below).
2. The strip's **brightness is constant**; it does not pulse, animate or chase. Animation on a 20 km line is
   nausea.
3. At a **bend**, the strip on the outside thickens to 1.6 m over the bend's extra width — so the bank reads
   before it arrives.
4. At a **fork**, both branches carry the strip, and the chevron run (§9.2) disambiguates.
5. At a **gap or a drop**, the strip **stops 20 m short of the rim and turns to HAZARD orange** for those
   20 m. This is the one sanctioned use of hazard colour outside the enemy family, and it is worth the
   exception: a 120 m gap at 148 m/s gives the player 0.8 s of warning, and the colour change is the only
   cue fast enough.
6. On a **wall-ride band**, the strip runs at **ball height (0.66 m) along the wall**, exactly as the tunnel's
   delivered guide strip does (`13 §2.6`) — the art shows this on the canyon wall in image `08`.
7. Inside a **tunnel**, the delivered 25 m lantern bands stay; the edge strip continues along the trench floor.

**Implementation choice [DECISION 5]:** the strip can be (a) a separate thin swept mesh with the SIGNAL shader,
or (b) a vertex-colour band written into the terrain by `SampleColor` with an emissive term keyed off colour.
Recommended **(a)** for the primary (crisp at any cell size, independent of the 4 m grid, ~2 k triangles per
km) and **(b)** for optional lines (cheaper, and a softer read is correct for a paid line).

**Why this is worth building before any other art asset:** it is the highest readability-per-triangle item in
the entire project, it is diegetic under the fiction, it is pure SIGNAL family, and it makes every screenshot
look like the concept art immediately.

---

## 13. Vegetation and scatter [GFX] [GEN]

The art uses vegetation sparingly and graphically:

- **Conifers as dark triangular masses** (`04`, `05`, `13`), value `#1E2A1E`–`#2E3A28`, in loose bands on
  slopes between 15° and 35°, never on the corridor, never individually detailed. At 4 m cells and 900 km/h a
  tree is a dark triangle; model it as one.
- **No grass, no undergrowth, no foliage cards.** They cost fill rate and add nothing at speed.
- Scatter rules stay exactly as delivered (`06 §16`, `04 §5G`): density rises with distance from the route,
  parallax is the speed cue, and every keep-out is respected.
- **Scatter is a value asset, not a colour asset**: its job is to darken the middle distance so the corridor's
  lit band separates from it.

---

## 14. HUD [GFX]

Images `07`, `08` and `13` are full HUD mockups and they are close to the delivered HUD (`06 §12`). Adopt:

| Corner | Content in the art | Mapping to delivered HUD |
|---|---|---|
| Top-left | `SKY TERRACES / SECTOR 1.3 / ALT 2468 M / TO NEXT 238 M` | stage name + sector; the delivered top-centre stage number and progress line move here |
| Top-right | `TIME 00:42.817 / BEST 01:28.304 / RINGS 042` | run timer, wallet; keep |
| Bottom-left | `OVERDRIVE` + a thin cyan bar | **Flow**, relabelled to the band word when in Overdrive |
| Bottom-right | `418 KM/H` — very large, thin numerals | the delivered speedometer dial |
| Corners | small slogan blocks | optional flavour, off by default |

**Points of difference to resolve [DECISION 6]:**

- The art shows **km/h**; the delivered HUD and every spec number are **m/s**. Recommended: display **km/h**
  to the player (535 and 918 are more legible and more thrilling than 148.5 and 254.7), keep m/s everywhere in
  code, docs and the F2 telemetry. This does not violate `06 §12` ("normal HUD does not need exact m/s").
- The art's big number **replaces** the dial. The delivered dial encodes Flow headroom as a redline, which is
  a real design win (P-015). Recommended: **keep the dial, add the big number inside it**, as image `07`
  nearly does.
- HUD type is thin, wide-tracked, uppercase, with hairline rules and **no plates** — matches the delivered
  rule (outline for contrast, never a plate). Keep.

---

## 15. Motion, and the one thing the art cannot tell us

Every image renders speed with **long directional blur on the ground plane**. That is a still-image
convention. In motion at 60 fps the same read comes from:

- the **edge strips** streaming past (§12),
- the **12 m panel modulus** ticking (§8.2),
- **scatter parallax** (§13),
- the **trail** (§10.2),
- **camera distance and FOV extrapolation above the base cap** (already delivered, `06 §7`),
- and at Overdrive, a **radial screen-edge streak overlay** — the one post-effect worth its cost, at low
  opacity, driven by the Flow-headroom fraction, never present below Crush.

**Do not ship per-object or camera motion blur.** It costs a full-screen pass, it fights the graphic style,
and at 4 m facets it turns terrain into mush — which is the opposite of the readability law.

---

## 16. Budget compliance

The direction is chosen to fit the delivered budgets (`13 §3.3`, `04 §9`), not to strain them.

| Item | Budget | This direction's cost |
|---|---|---|
| Resident triangles | ≤1.2 M | terrain unchanged; edge strips ~2 k/km ≈ 40 k at 20 km; signage ≤ 30 k; gantries ≤ 15 k |
| World draw calls | ≤120 | +1 cloud deck, +1 dome, +1 edge-strip multimesh per line, +1 signage multimesh, +1 chevron multimesh ≈ **+6** |
| Shaders | — | seven (§7), none with a texture fetch on terrain |
| Textures | none today | concrete triplanar: 3 small tiling maps (albedo/roughness/detail-normal) at 1024², shared by every structure ≈ 6 MB |
| Time to first frame | ≤2.0 s | edge strips and signage build with the stage on worker threads with the shells; estimated +0.15 s |
| Post-processing | none today | one radial streak overlay, Overdrive only |

**The direction adds no per-frame cost that scales with stage length**, which is the property that matters for
the 18 km stage (`13 §3.1`, D-118).

---

## 17. Conflicts between the concept art and the frozen design — read this before building anything

The concept art was generated from a brief, not from the codebase, and it contains things that **cannot or
must not** be built. Each conflict below is resolved in favour of the higher-authority document, with a note
on how to keep what the art was actually reaching for.

### 17.1 The art shows a racetrack; the game is a landscape — **the critical conflict**

Images `01`, `05`, `07`, `08`, `10`, `12` and `13` all show a **narrow ribbon road with hard edges**, 10–30 m
wide, with the surrounding terrain non-drivable. That is flatly contrary to `00 P2`, `01`, `02 §4` and
`04 §2`: a RUSHCORE stage is a **broad directional landscape**, the corridor minimum is **75 m**, the typical
is **150 m** and the open default is **300 m**, and free line choice inside that width is a pillar.

**Resolution:**
- **Build the width from the spec, and the *read* from the art.** The corridor stays 75–300 m. What the art
  is actually supplying is the **edge treatment** (§12) that makes a wide corridor legible — the ribbon look
  comes from the luminous edges, not from the width.
- A useful calibration for the art team: at 150 m, the corridor is **114 ball-diameters wide**. In image `07`
  the road is roughly 12 ball-diameters. **The art is off by an order of magnitude and must not be copied
  dimensionally.**
- Where the art's intimacy is genuinely wanted, the design already has places for it: the **tunnel** (20 m),
  the **chute** on Summit Descent, **ledge lines** on canyon walls (`04 §6`), and **optional technical lines**
  (40 m, `11 §3e`). Those are the narrow moments; they are paid lines, never the primary.

### 17.2 The art paves the ground; the design does not

Nearly every image shows the drivable surface as **cast concrete**. `04 §5D` stamps a corridor into natural
ground; `04 §7` forbids implying a physics change; a concrete road beside grass implies exactly that (and the
friction model is uniform).

**Resolution:** the corridor floor is **natural ground** — earth, rock, sand, snow — with a **track tint**
(the delivered `Track` `#80705C` blend already does this at 45%) plus the edge strips. Concrete appears only
per §8.6. **Pads are the exception** and should be unmistakably cast.

### 17.3 Photoreal stratified rock vs. the vertex-coloured heightfield

**Resolved by the user's cel decision (§3).** The painterly images are composition and mood references only;
surface treatment follows `docs/17`. Structures carry bounded extra density within `17 §7`.

### 17.4 The art's structures ignore the headroom rule

Image `10`'s gantry, image `05`'s viaduct and image `01`'s overhead arcs all cross above the route. `04 §5I`
(D-102) forbids anything but a **declared lid** within the full-charge apex (**91 m** plus a ball) above the
primary's centreline, and the validator enforces it.

**Resolution:** gantries and arches over the route are legal **only** as declared structures with the lid's
ceiling treatment — which means they must either (a) sit above 92 m, or (b) be declared, validated and
camera-confining like a lid. Recommended: place gantries at **boost rings and act transitions**, declare them,
and set the clear height at **30 m** so they read as a frame the player passes under rather than a ceiling.
Every such structure must pass the existing headroom validator; none may be added outside it.

### 17.5 Cloud sea vs. the Sky Terraces cloud band

They are different systems (§5.2) and both exist. Do not merge them.

### 17.6 Motion blur

See §15. Still-image convention; do not ship it.

### 17.7 The ball's colour

Delivered orange vs. the art's cyan. See §10.1 [DECISION 4].

### 17.8 Enemies are entirely absent from the set

Not one of the thirteen images contains an enemy. The four archetypes plus the elite (`06 §8`, `02 §7`) remain
**undesigned under this direction** and are the largest open commission. `15 §7` sets out what they must
satisfy; a second concept round should target them specifically.

### 17.9 Vertical exaggeration

The art's mountains and walls are steeper and more numerous than the generator's rules produce (grades are
bounded by `04 §10`, walls are 60–120 m, terraces 60–90 m). **Do not loosen the generation rules to chase the
art.** The perceived verticality in the set comes mostly from **camera height and the cloud sea**, both of
which are free.

---

## 18. Generation-logic requirements [GEN] — the numbered, testable list

Everything the stage generator must do for this art direction to be reachable. Each is phrased so a validator
or a harness check can assert it. None of them changes a movement value; none changes a route's geometry
except where noted.

| # | Requirement | Owner | Notes |
|---|---|---|---|
| G1 | `StageDefinition` carries a **sun azimuth and elevation**, deterministic from the stage seed, azimuth 35°–145° or 215°–325° off the stage axis, elevation 4°–9°. | `StageGenerator` | §4.2 |
| G2 | `StageDefinition` carries a **cloud-deck altitude** (or "none") per §5.4. | `StageGenerator` | §5.4 |
| G3 | The generator emits **corridor edge strips** as polylines with a width, for the primary (0.8 m, 1.6 m on bend outsides) and every optional line. | new `EdgeStripBuilder` | §12 |
| G4 | Edge strips turn **HAZARD orange for the 20 m before any rim** with a drop over 8 m. | `EdgeStripBuilder` + `ChallengeModule` data | §12.5 |
| G5 | The generator emits a **chevron run** (3 chevrons, flush, along the branch) starting 150 m before **every fork**: optional-line departures, terminal-line ramps, tunnel portals, chute gaps. | `OptionalLineBuilder`, `StageGenerator` | §9.2 |
| G6 | **Slogan monoliths** replace the plain 200 m scale-cue posts, placed outside every keep-out, on the higher side of the corridor, facing the oncoming line, never in a bend. | `StageScatter` / `WorldDressing` | §9.2 |
| G7 | **Sector markers** are placed at every checkpoint anchor, carrying the checkpoint's index. | `WorldDressing` | §9.2 |
| G8 | **Gantries** are placed at boost rings and act transitions, declared as structures, clear height 30 m, and pass the existing headroom validator. | `LidBuilder` (extended) | §17.4 |
| G9 | Wall faces receive **stencil placements** above the lip on straights every 400–800 m, from the cosmetic seed. | `WallShellMesh` + dressing | §9.2 |
| G10 | Structures obey the **12 m panel modulus** and the **0.4 m chamfer** in their generated geometry. | `WallShellMesh`, `LidBuilder`, `TunnelProfile` | §8.2, §8.3 |
| G11 | **No geometric relief below any wall lip** on a rideable band; ribs, frames and fittings begin above it. | `WallShellMesh` | §8.5 — assert in the wall-ride harness |
| G12 | The generator prefers **asymmetric vertical mass** — one side of the corridor at a time — except where the archetype requires a slot. | `ArchetypeRules` | §11.3 |
| G13 | Scatter becomes **value-weighted**: conifer bands on 15°–35° slopes, dark, in the EARTH family. | `StageScatter` | §13 |
| G14 | Pads (start, exit, checkpoint) are **cast concrete discs with chamfered edges** and a SIGNAL edge ring. | `WorldDressing` | §8.6 |
| G15 | All of G3–G14 draw from the **cosmetic seed**, so no art change can move a golden stage hash. | all | `04 §5G` — assert in the hash harness |

**G15 is the safety property that lets this whole direction be built without disturbing the frozen generation
baseline**, and it should be the first harness assertion written.

---

## 19. Acceptance — how we will know the look landed

A build adopting this direction passes when, on the sample stages (`docs/10`) with debug views off:

1. A still from the chase camera on any archetype is describable by §2's sentence.
2. The corridor's extent is unambiguous at 250 m/s in a 1 s clip, with the HUD hidden.
3. Every fork is identifiable **before** the player must commit to it (≥3 s of route at the current speed).
4. A gap's rim is identifiable at ≥4 s of route (the commit horizon, `11 §3h`).
5. No saturated cool colour appears anywhere except the ball, its trail, and route SIGNAL elements.
6. Hazard orange appears only on rim warnings, the Bulwark's band, and the shortcut post.
7. Frame time and draw-call counts are inside §16's budgets on the harness machine.
8. Time to first frame is ≤2.0 s on the 6 km stage (≤3.0 s at 18 km pending the collider decision, `13 §3.3`).
9. The wall-ride harness reports **no new impacts** after G10/G11 land — the art has not made a wall grabby.
10. Every golden stage hash is unmoved (G15).

---

## 20. Open decisions, collected

| # | Decision | Recommendation | Blocks |
|---|---|---|---|
| 1 | In-fiction name of the authority | leave it as "RUSHCORE" | signage copy |
| 2 | ~~Painterly (A) vs graphic/cel (B)~~ | **CLOSED 2026-09-20 — cel shading, by the user. See §3 and `docs/17`.** | — |
| 3 | Type family and licence | open wide geometric sans + tabular numerals | all signage, HUD |
| 4 | Ball colour: delivered orange vs art cyan | **cyan**; orange goes to currency and the sun | player shader, all VFX |
| 5 | Edge strip: swept mesh vs vertex band | mesh on primary, vertex band on optional lines | G3 |
| 6 | HUD units and dial vs number | km/h to the player, m/s in code; keep the dial, put the number in it | HUD work |
| 7 | Cloud sea on Rolling Highlands | conditional on route mean height | G2 |
| 8 | Enemy design under this direction | commission a second concept round | `15 §7` |
| 9 | Gantry clear height (30 m) vs headroom validator | declare as structures at 30 m | G8 |
| 10 | Whether palette quantization ships on | ship at 3 steps, expose a slider | S1 |

---

## Appendix A — the master palette, as code

Values are sRGB hex; divide by 255 for Godot `Color`. Families per §6.1.

```text
EARTH   basin      #3D4F63   low        #3D6647   mid        #638C4D   high       #B8B37D
        rock       #856B54   cliff      #544D4F   track      #80705C
        canyonRock #9E5C3D   canyonRim  #CC9966   strataLow  #663C28
        sandTrough #B88F57   sandCrest  #EDCC85   sandLee    #946B42
        snow       #E8ECF2   snowShade  #9FB0C8   scree      #7A7268   moss      #5C6B45
        conifer    #22301F   coniferLit #35422C

WORKS   concLit    #B9B0A4   concShade  #4A4652   conc2Lit   #9A9288   conc2Shade #3E3B46
        concDark   #6B6862   concDarkSh #2A2830   steel      #7E7A74   steelShade #2E2C33
        inkDark    #3A3630   inkLight   #D8D2C6   joint      #8E877C

SIGNAL  core       #6CE7FB   trail      #45C8E0   trailFar   #3F9AC4   strip     #5CD8F0
        hudPrimary #7FE9F5   spark      #66C7FF   boom       #B8E6FF   currency  #FFCC4D

HAZARD  band       #EB4724   rimWarn    #FF6A2A

SKY     zenith     #3C2A5A   upper      #5E3F5D   horizon    #C8763C   sunCore   #FFF0C8
        sunHalo    #FDD76B   groundHrz  #6B5A50   groundBot  #241E22
        cloudLit   #FFD9A0   cloudBase  #6E5170   fogWarm    #C8763C   fogCool   #6A5A7A

LIGHT   keyColor   #FFE7B8 (energy 1.15)         ambient    #5E4A78 (energy 0.35)
```

## Appendix B — concept-art provenance

Sixteen unique images, generated 2026-09-20, stored in `docs/concept/`. Per-image analysis, measured palettes,
and what each one establishes as canon: `docs/18_CONCEPT_REFERENCE_INDEX.md`. The three cel frames (14, 15,
16) are the master style targets and govern `docs/17_CEL_SHADING_TECHNICAL_SPEC.md`.
