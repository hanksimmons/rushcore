# 17 — Cel Shading Technical Specification

**Status:** PROPOSED 2026-09-20. The **style decision itself is closed** — the user chose cel shading on
2026-09-20 (`14 §3`). What remains open is this document's implementation detail, which the lead graphics
developer owns and may change on measurement.
**Authority:** owns the **implementation** of the look: shader maths, uniforms, ladder values, rim derivation,
outline policy, distance tiering, VFX draw rules, budgets and failure modes. Subordinate to
`docs/14_ART_DIRECTION_BIBLE.md` (which owns the look's *intent*, the colour law and the fiction) and to
`06`, `04` and `03` above that. Where 14 and 17 disagree on a number, **17 wins**; where they disagree on
intent, **14 wins**.
**Master references:** `docs/concept/14_CEL_MASTER_ridge_descent.png` (the master frame),
`15_CEL_scree_slope_debris.png`, `16_CEL_slam_impact_rings.png`. Supporting: `12_cel_dusk_ridge.png`,
`13_cel_valley_portal_hud.png`.
**Target:** Godot 4.7 .NET, Forward+ renderer, macOS arm64, 1.2 M resident triangles, ≤120 world draw calls.

---

## 0. The one-sentence brief for the implementer

> Replace every lit material in the game with a **two-plateau hard-terminator ladder plus a hot rim term**,
> driven by one shared include file and one global set of uniforms, applied identically to terrain, wall
> shells, structures, props, the horizon ring and the ball — with **no screen-space passes, no textures on
> terrain, and no smooth N·L anywhere.**

---

## 1. Why this is unusually cheap here

RUSHCORE is a uniquely favourable case for cel shading, and the implementation should exploit all four:

1. **The terrain is already flat-shaded with per-facet normals** (`04 §9`). Cel's worst enemy — smooth normals
   producing crawling terminators across a curved surface — does not exist on the ground. Each facet resolves
   to one plateau, uniformly. **The ground will cel-shade perfectly for free.**
2. **The terrain already carries per-vertex albedo** from `SampleColor` (`StageHeightField`), including the
   height ramp, the slope tint, the canyon strata and the track tint. The cel shader consumes that as base
   colour and adds only light. **No albedo work is needed.**
3. **There is exactly one directional light** (`06 §16`). A single-light cel ladder is a handful of
   instructions with no loop and no per-light branching.
4. **There is no texture budget to protect** — the game currently ships zero terrain textures and this
   direction keeps it that way.

The consequence is that the terrain shader (S1) should cost *less* than the current `StandardMaterial3D`
lambert path, not more. If it does not, something in §3 has been implemented as a branch instead of as
arithmetic.

---

## 2. Architecture: one include, one uniform block

Create `src/Shaders/cel_core.gdshaderinc`, included by every lit shader in the game. It holds the ladder, the
rim, the tiering and the quantizer. **No shader implements its own ladder.** This is the single most important
maintainability rule in this document: a cel look falls apart the moment two materials band differently.

### 2.1 Global uniforms (set once per frame from C#)

Owned by a new `CelRenderState` singleton-ish helper that writes to a `ShaderMaterial` global uniform set
(`RenderingServer.GlobalShaderParameterSet`), so materials do not each carry copies.

| Uniform | Type | Source | Default |
|---|---|---|---|
| `cel_key_dir` | vec3 | the stage's sun direction (`14 §4.2`, G1) | — |
| `cel_key_color` | vec3 | `#FFE7B8` | 1.00, 0.906, 0.722 |
| `cel_key_energy` | float | | 1.15 |
| `cel_ambient_color` | vec3 | `#5E4A78` | 0.369, 0.290, 0.470 |
| `cel_ambient_energy` | float | | 0.35 |
| `cel_terminator` | float | N·L of the step | 0.32 |
| `cel_terminator_width` | float | feather, in N·L | 0.03 |
| `cel_deep_threshold` | float | N·L below which deep shade applies | −0.35 |
| `cel_rim_color` | vec3 | `#FFC24A` | 1.00, 0.761, 0.290 |
| `cel_rim_power` | float | | 3.5 |
| `cel_rim_strength` | float | | 0.85 |
| `cel_rim_light_bias` | float | how much the rim follows the key vs. the view | 0.65 |
| `cel_tier_start` | float | metres | 900.0 |
| `cel_tier_end` | float | metres | 6000.0 |
| `cel_tier_color` | vec3 | per-view fog colour (`14 §5.3`) | — |
| `cel_tier_flatten` | float | how far distant surfaces collapse to one plateau | 0.85 |
| `cel_quant_steps` | float | 0 = off | 3.0 |
| `cel_debug_mode` | int | 0 off, 1 ladder, 2 rim, 3 tier, 4 albedo | 0 |

**All of these are live tuning values** and must appear in the runtime panel under a new `Cel ›` group
(`07`), because this style is entirely a values exercise and the first playtest will move half of them.

### 2.2 The include's public functions

```glsl
// Returns the lit colour for a surface, given its flat albedo and world normal.
vec3 cel_shade(vec3 albedo, vec3 n, vec3 view_dir, float depth_m);

// The ladder alone, without rim or tiering (for VFX and unlit-ish surfaces that still want banding).
float cel_ladder(vec3 n);

// The rim term alone (for the ball, which drives it from its own state).
float cel_rim(vec3 n, vec3 view_dir);

// Distance value tiering, applied last by every shader.
vec3 cel_tier(vec3 color, float depth_m);

// Optional palette quantization, applied after tiering.
vec3 cel_quantize(vec3 color);
```

---

## 3. The ladder

### 3.1 The step

```glsl
float ndl = dot(normalize(n), -normalize(cel_key_dir));
float lit = smoothstep(cel_terminator - cel_terminator_width,
                       cel_terminator + cel_terminator_width,
                       ndl);
```

`cel_terminator_width` exists **only to antialias the step**, not to soften it. At 1080p with 4 m facets,
0.03 resolves to roughly one pixel on a typical ground plane. **Do not raise it to make the look "nicer" —
raising it is how a cel style silently becomes a lambert style.**

### 3.2 The plateaus

```glsl
vec3 shade_col = albedo * cel_ambient_color * cel_ambient_energy;
vec3 lit_col   = albedo * cel_key_color     * cel_key_energy;
vec3 c = mix(shade_col, lit_col, lit);
```

Note that the shade plateau is **albedo × violet ambient**, not a darkened albedo. That is what produces the
warm/cool split in the master frame: lit rock goes gold-grey, shadowed rock goes violet-grey, and the hue
shift does the modelling that a normal map would otherwise do.

Measured against the master frame, this yields shadow plateaus in the `#2A2A3A`–`#4C474B` range and lit
plateaus around `#8C8A8E`–`#B9B0A4`, which matches the sampled dominants (`18 §14`).

### 3.3 Deep shade (third plateau)

```glsl
float deep = 1.0 - smoothstep(cel_deep_threshold - 0.08, cel_deep_threshold + 0.08, ndl);
c = mix(c, c * 0.55, deep * float(cel_deep_enabled));
```

Applied on surfaces facing away from the key **and** downward. Used under lids, inside tunnels, in pockets and
at the spiral pit's floor, where a flat two-value read would make a ceiling and a floor the same value.

### 3.4 Shadow-map integration

Godot's directional shadow must **multiply the ladder's `lit` term before the mix**, not darken the final
colour:

```glsl
lit *= shadow_attenuation;       // then re-step it
lit = step(0.5, lit);            // a shadowed surface lands wholly on the shade plateau
```

Re-stepping is essential: a cel surface must be either lit or shaded, never partially. Soft shadow edges are
permitted **only** at the penumbra width needed to avoid aliasing (PCF 5×5 at most). A soft shadow gradient
across a flat plateau is the second most common way this style degrades.

### 3.5 Palette quantization

Ships **on**, 3 steps, applied after tiering:

```glsl
vec3 cel_quantize(vec3 c) {
    if (cel_quant_steps <= 0.0) return c;
    return floor(c * cel_quant_steps + 0.5) / cel_quant_steps;
}
```

Quantize in **linear** space, before tonemapping. Quantizing after tonemap produces visible banding in the
sky gradient, which is the one place the game wants smoothness.

**Exempt from quantization:** the sky, the cloud dome and deck, all SIGNAL emissive surfaces, and the HUD.

---

## 4. Edges and outlines — **the policy is: no outline pass**

A conventional cel pipeline adds screen-space edge detection or inverted-hull outlines. **RUSHCORE does
neither**, for three reasons that the implementer should not relitigate without measurement:

1. **The terrain is 1.2 M triangles of 4 m facets.** A depth/normal edge detector draws a line around every
   facet boundary. A normal-threshold filter tuned to avoid that also misses real silhouettes.
2. **An inverted-hull pass doubles the draw call count** on a budget of 120.
3. **The master frame has no uniform outline.** Look closely at `14`: edges are defined by *value contrast*
   between adjacent plateaus and by the *rim*, not by a black line. `15` uses near-black silhouettes against
   lit ground — again contrast, not outline.

**What replaces outlines:**

- **The rim term** (§5) on every lit edge.
- **Value tiering** (§5.4), which guarantees that a near surface and a far surface never share a value.
- **Chamfered structure edges** (`14 §8.3`), which catch a highlight and define form geometrically.

**Permitted local exception:** the **ball** may carry a thin inverted-hull outline (it is one small mesh, one
extra draw call, and it guarantees the player reads against every background including a bright pad or the
sun). See §9.

---

## 5. The rim, and distance

### 5.1 Why the rim is mandatory

In the master frame, every ridge, cliff lip, rock shard and structure edge carries a thin hot gold band. It is
the single effect that turns flat polygons into rock. Without it, cel terrain reads as coloured paper.

### 5.2 The derivation

A pure Fresnel rim (view-based) puts light on every silhouette regardless of the sun, which looks wrong on a
low-sun scene. A pure light-based rim misses edges facing the camera. Blend them:

```glsl
float fres = pow(1.0 - max(dot(n, view_dir), 0.0), cel_rim_power);
float lightward = smoothstep(0.0, 0.35, ndl);        // only on surfaces turning toward the key
float rim = fres * mix(1.0, lightward, cel_rim_light_bias);
rim = step(0.25, rim) * cel_rim_strength;             // hard-edged, like everything else
c += cel_rim_color * rim;
```

The `step` is deliberate: **the rim is a band, not a glow.** A soft rim reads as bloom and fights the SIGNAL
family for attention.

### 5.3 Rim on the wall ride band — **a gameplay-critical case**

The canyon wall's rideable band (`04 §5D`, `14 §8.5`) is a smooth analytic surface. The rim must **not** fire
along it, or the player will read a hot line where there is no edge, and the wall's lip will be ambiguous.
Suppress the rim where the surface is part of a wall shell below the lip:

- The wall shell mesh writes a **custom vertex attribute** (`CUSTOM0.r = 1.0` below the lip, `0.0` above).
- `cel_shade` multiplies `rim` by `(1.0 - ride_band)`.

This is a one-float change in `WallShellMesh` and it prevents the single worst art-vs-mechanics failure this
style can produce.

### 5.4 Distance value tiering

The master frame renders distance as **flat value tiers**, not as blurred fog: each successive range is a
flatter, lighter, more violet plateau. Reproduce it by reducing plateau separation with depth *and* blending
toward the view-dependent fog colour:

```glsl
vec3 cel_tier(vec3 c, float depth_m) {
    float t = smoothstep(cel_tier_start, cel_tier_end, depth_m);
    // collapse toward the surface's own mid value: distant terrain flattens to one plateau
    float lum = dot(c, vec3(0.2126, 0.7152, 0.0722));
    c = mix(c, vec3(lum), t * cel_tier_flatten);
    // then toward the atmosphere
    return mix(c, cel_tier_color, t * 0.75);
}
```

`cel_tier_color` is set per frame from the view/sun dot (`14 §5.3`): `#C8763C` into the sun, `#6A5A7A` away.

**This function is what makes the horizon ring (D-114) work under cel.** Twenty kilometres of the stage's own
terrain, drawn with the same ladder and progressively flattened, produces exactly the stacked violet ranges in
`14`'s left third — at no extra geometry cost.

---

## 6. Where cel will break, and the fixes

Flat plateaus hide nothing. These are the known hazards, in the order they will bite.

| # | Failure | Cause | Fix | Owner |
|---|---|---|---|---|
| F1 | **Wall-shell seam** — a visible hard-edged band where the shell meets the sunk heightfield | the shell is smooth-normalled, the grid is flat-normalled; two different ladder results meet | blend the shell's normal toward the grid normal over the last 3 m of the fillet's foot; share albedo via `SampleColor` at the same point (already true) | `WallShellMesh`, S3 |
| F2 | **Fine/coarse mesh boundary visible** — the 4 m window against the 16 m resident mesh (`13 §3.3`) | different facet sizes produce different plateau mosaics | keep the 6 m skirts; apply tiering (§5.4) so the boundary always sits ≥900 m out where plateaus have flattened; verify the window radius (1.2 km) stays outside `cel_tier_start` | S1 + `MovementToyWorld` |
| F3 | **Terminator crawl on the ball** | the ball is the only smooth-normalled gameplay object | it is small and moving; accept it, and use the always-on rim (§9) to define it instead | S7 |
| F4 | **Banding in the sky** | quantization applied to the gradient | exempt the sky, dome and deck (§3.5) | S6 |
| F5 | **Shadow acne on 4 m facets** | bias tuned for props, not for large flat planes | tune normal-offset bias against the facet, not the prop; cascade splits at 600 m (`14 §4.3`) | `WorldDressing` |
| F6 | **A "papery" read** — terrain looks like folded card | rim missing or too weak; no deep-shade plateau | raise `cel_rim_strength`, enable deep shade on downward faces | S1 |
| F7 | **Route illegible in shadow** | a low-key image puts much of the corridor on the shade plateau | this is exactly what the SIGNAL edge strips (`14 §12`) exist for; they are unlit and therefore equally bright in shade | `EdgeStripBuilder` |
| F8 | **Overdrive invisible** | the trail already occupies the brightest values | reserve the top of the value range: nothing in EARTH or WORKS may exceed 0.85 luminance after tiering | S1, S2 |

**F7 deserves emphasis for the design team:** cel shading makes the corridor-edge system *more* necessary, not
less. In a low-key frame the ground's own colour cannot always carry the route read. The edge strips, being
unlit SIGNAL, are the only element guaranteed legible in both plateaus.

---

## 7. Structures: how much density is allowed [A-density]

Structures are separate UV-capable meshes and are the one place extra detail is permitted. The bound:

- **Flat panel values only** — a concrete panel is one plateau pair, not a texture. The 12 m modulus
  (`14 §8.2`) is carried by **geometry (pilaster ribs, chamfers) and by a flat joint line**, not by an albedo
  map.
- **One shared tiling detail map is permitted**, at ≤1024², used at **very low contrast** (≤8% albedo
  variation) purely to stop large panels reading as pure vector fills. It must be invisible at 100 m.
- **No normal maps.** They fight the ladder — a normal map produces sub-facet terminator noise, which is the
  exact thing cel exists to remove.
- **No roughness/metallic variation.** Specular is disabled game-wide (already true in `PlaceholderPalette`).
- **Stencil type and chevrons are decals or UV'd geometry**, drawn unlit-ish: they take the ladder at reduced
  contrast so type stays readable in shade.

---

## 8. VFX under cel — dust is drawn, not simulated

The master frames are explicit and this is a real cost saving.

### 8.1 Dust (`14`)
Discrete **rounded puffs in a single flat grey**, arranged in a row behind the ball. Not a soft plume.

- Keep the delivered `GpuParticles3D` emitters and their motion (`PlayerVfx`) — only the **draw** changes.
- Billboard quads with a **hard alpha cutoff** (no soft alpha), two tones maximum (lit `#C8C2B8`, shade
  `#6A6470`), chosen by the same ladder against the key direction.
- Puff count is low and their size is large: at 148 m/s the player sees shape, not density.

### 8.2 Debris (`15`)
Flat **near-black silhouette chunks** with a **cyan underglow from the trail**.

- Chunks are unlit flat `#14141C` with a rim-less silhouette.
- The underglow is an additive SIGNAL term keyed off proximity to the trail, applied on the chunk's lower
  faces. This is what makes `15` read: **the trail lights the world, the world does not light the debris.**
- Carve debris (`03 §11`, D-089) uses the same treatment, thrown to the outside of the corner.

### 8.3 The impact rings (`16`)
`16` is very nearly a render of the delivered slam/landing-burst VFX (D-077) and confirms it. Adopt its
specifics:

- **Ground shock ring:** a flat ellipse on the ground plane, **thin and hard-edged**, white-cyan, expanding.
- **Air-parting bow ring:** a vertical arc ahead of the ball, same treatment (delivered).
- **Radial crack streaks:** straight hard lines radiating from the contact point along the ground, fading
  outward. **New** — cheap, and it is what sells the impact in `16`.
- **Thrown chunks** per §8.2.
- Colour: core `#E8F6FF`, body `#B8E6FF`, matching the delivered `BoomColor`.
- Everything is **hard-edged and short**; no soft shockwave sprite, no radial blur.

### 8.4 The trail
Per `14 §10.2`: 2–4 hard-edged parallel streaks, surface-hugging when grounded. Under cel they are **unlit,
flat, additive**, with no gradient along their width — a soft-edged trail is the fastest way to make a cel
frame look like a different game.

---

## 9. The ball under cel

The master frames show the ball as **a dark silhouette with a bright rim**, not as a lit sphere — at its size
(1.32 m at 40 m) it is a few dozen pixels and reads by contour alone.

- **Body:** `#14171F`, cel ladder applied, but with the lit plateau pulled down so the ball never becomes a
  bright object.
- **Rim:** always on, white-cyan `#9EEFFF`, stronger than the world's gold rim. This is the ball's primary
  read and it works against sky, rock, snow and concrete alike.
- **Outline:** the one permitted inverted-hull outline in the game (§4), 1–2 px equivalent, colour `#0A0C12`,
  so the ball reads against a bright pad or directly against the sun.
- **Seams:** SIGNAL emissive, unlit, quantization-exempt.
- **Speed ring:** per `14 §10.1`, hard-edged, unlit.

---

## 10. Order of work

Sequenced so each step is independently reviewable and nothing is blocked on art:

| Slice | Contents | Reviewable output |
|---|---|---|
| **X1 — the ladder** | `cel_core.gdshaderinc`, the global uniform block, `CelRenderState`, S1 terrain cel, the `Cel ›` tuning group, debug modes | the existing sample stages rendered cel, no content change |
| **X2 — rim and tiering** | §5 in full, including the ride-band suppression (§5.3) and the `CUSTOM0` attribute in `WallShellMesh` | a canyon stage with hot ridge lines and flat violet distance |
| **X3 — structures** | S2/S3/S4, chamfers, the 12 m modulus, the seam fix (F1) | a wall-shell close-up with no seam |
| **X4 — sky and horizon** | S6, the cloud dome + deck, the tiered horizon ring, the per-view fog tint | the master frame's sky |
| **X5 — VFX** | S8, dust puffs, debris silhouettes, crack streaks, the trail | the slam impact matching `16` |
| **X6 — the ball** | S7, rim, outline, seams, speed ring, Overdrive | the ball reading against every background |
| **X7 — edge strips** | `EdgeStripBuilder` and the SIGNAL route system (`14 §12`, G3–G5) | route legible at 250 m/s in shadow |

**X1 and X7 together are the minimum viable adoption**: the ladder makes it look like the concept art, and the
edge strips make it playable in a low-key image. X2 is what makes it look *good*.

---

## 11. Measurement gates

No slice is accepted without these, on the harness machine, on the 6 km canyon sample:

| Gate | Threshold |
|---|---|
| Frame time delta vs. the current build | ≤ +0.8 ms at the start pad, ≤ +1.2 ms at the cap |
| Resident triangles | unchanged (cel adds no geometry except the ball's outline and the edge strips) |
| World draw calls | ≤120, i.e. ≤ +6 over today |
| Time to first frame | unchanged within noise (shaders compile once; pre-warm at load) |
| Golden stage hashes | **unmoved** — cel is a render change only, and any hash move means art has leaked into the gameplay seed (`04 §5G`, G15) |
| Wall-ride harness | no new impacts; no new grounded-percentage regression |
| Shader compile stalls | none at stage load; every material pre-warmed in `GameBootstrap` |

---

## 12. Open implementation questions

| # | Question | Recommendation |
|---|---|---|
| 1 | `cel_terminator` at 0.32 — right for a 4°–9° sun? | measure on the master-frame composition first; expect 0.25–0.40 |
| 2 | Quantize in linear or after tonemap? | linear, before tonemap (§3.5); verify no sky banding |
| 3 | Does the coarse 16 m mesh need its own terminator width? | probably yes (larger facets, fewer pixels per plateau); expose `cel_terminator_width_coarse` |
| 4 | Ball outline: inverted hull vs. a rim-only read | try rim-only first; add the hull only if the ball is lost against bright pads |
| 5 | Deep-shade plateau: global or tunnel/pit only? | enable globally on downward-facing normals; it costs nothing and helps F6 |
| 6 | Do the edge strips need their own anti-aliasing at distance? | likely — a 0.8 m strip at 1 km is sub-pixel; consider a minimum screen-space width clamp |
| 7 | Godot 4.7 global shader parameters vs. per-material uniforms | global parameters, to keep the 120-draw-call budget and avoid per-material sync |

---

## Appendix — the cel ladder in numbers

Sampled from the master frames (`18 §14`–`§16`); use as the starting point for X1.

```text
TERRAIN, lit plateau          #8C8A8E   (rock)   #B8B37D (high grass)   #CC9966 (canyon rim)
TERRAIN, shade plateau        #383848   #2A2A3A   #4C474B
TERRAIN, deep shade           #181828
RIM (hot gold)                #FFC24A   ..  #FED042 at full strength
CONCRETE, lit plateau         #B9B0A4
CONCRETE, shade plateau       #4A4652
CONCRETE, deep               #2A2830
DISTANT TIER 1 (2-6 km)       #6A5A7A   flattened 60%
DISTANT TIER 2 (6 km+)        #524A66   flattened 85%
SKY upper                     #5E3F5D    zenith #3C2A5A
SKY horizon                   #FED042   narrow band
CLOUD lit / base              #FFD9A0 / #6E5170
SIGNAL core / trail / strip   #6CE7FB / #45C8E0 / #5CD8F0
BALL body / rim / outline     #14171F / #9EEFFF / #0A0C12
DUST lit / shade              #C8C2B8 / #6A6470
DEBRIS silhouette             #14141C  (+ SIGNAL underglow)
IMPACT ring core / body       #E8F6FF / #B8E6FF
```
