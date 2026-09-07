# 06 — Visual, Camera Presentation & UX Specification

**Status:** Draft 0.3 — final audited  
**Authority:** Canonical procedural art language, gameplay readability, HUD, camera presentation, VFX

## 1. Visual objective

Create a coherent **low-poly, N64-inspired** presentation that is:

- fast to generate,
- readable at high speed,
- attractive enough for serious playtesting,
- intentionally stylized rather than grey-box placeholder art.

“N64-inspired” means low geometric complexity, bold silhouettes, constrained palettes, simple materials, fog/lighting, and stylized shading—not deliberate unreadability.

## 2. Readability hierarchy

At gameplay speed, the player should recognize:

1. safe/general progression direction,
2. major terrain shape/ramp/gap,
3. hostile enemy silhouette,
4. pickup/reward line,
5. exit,
6. cosmetic detail.

Cosmetic detail never outranks route/hazard readability.

## 3. Terrain/palette language

Each archetype gets:

- terrain color family,
- accents,
- hazard color,
- pickup/reward color,
- atmospheric fog.

Enemy roles retain stable silhouettes/semantic treatments across stage palettes.

Do not rely only on hue.

Vertical grammar (D-096): walls read as cliffs through the slope tint; the cloud band is a per-stage height
above which the fog thickens into a layer the top floor sits in and a fall drops through; a see-through
tube is a translucent shell with opaque ribs every 25 m, the ribs being the motion cue (§2) at speed.

## 4. Terrain shading

Initial direction:

- generated low-poly mesh,
- flat/faceted normals,
- low-frequency color variation by height/slope/noise,
- dominant directional light,
- ambient/environment fill,
- distance fog,
- optional simple banded lighting.

Avoid noisy albedo and expensive layered terrain textures.

## 5. Player visual

Generated low-poly sphere/icosphere with:

- strong unique palette,
- readable speed/emissive accents,
- roll derived from travel,
- charge compression/tension,
- jump release snap,
- boost stretch/trail,
- landing squash,
- slam streak,
- slam-landing power flash,
- landing-burst sparks/boom,
- impact feedback.

Collider remains unchanged.

## 6. Charge-jump UX

Charging must be readable without requiring the player to stare at a HUD meter.

Preferred player-local cues:

- increasing ball compression,
- emissive/pulse buildup,
- subtle ground-contact energy effect,
- stronger release burst for higher charge.

A full charge bar is not required for MVP unless playtesting shows the charge amount is otherwise unreadable.

Because steering is locked while charging, presentation should clearly communicate that the player is committed.

## 7. Slam power impact and landing burst UX

Both must feel “snappy, speedy, powerful” without pausing gameplay.

On every slam landing:

- the strongest landing/impact effect and a hot flash on the ball.

On a landing burst (D-077):

- electric-blue sparks from the contact point,
- a mini sonic boom: a ground shock ring at the landing point plus an air-parting bow ring
  riding ahead of the ball,
- blue flash and a stretch along travel,
- optional compact Flow feedback.

Do not show a large timing minigame or QTE indicator.

Players should learn the timing from motion and feedback.

A carve (D-089) must read from far away: **rocks and a spray of ground are thrown from the contact
point toward the outside of the corner** (the skid side, away from the facing), trailing back and up,
rising with carve angle and speed; the ball rolls about the facing while sliding, the roll dust runs at
full, and the camera stays behind the facing. Release should read as a bite, not a launch.

Speed above the base cap (Flow headroom, D-088) must read as more speed: camera distance and FOV
keep extrapolating past the base cap (delivered with the mechanic), and Overdrive gets a distinct
look at Gate V0 (streaks, tint, stronger roll dust). A 25% number increase reads as nothing without it.

## 8. Enemy visual language

### Pylon
Simple upright low-resistance silhouette.

### Bulwark
Wide/heavy/blocky silhouette communicating high resistance.

### Strider
Shape/motion makes lateral crossing obvious.

### Shooter
Clear ranged-hazard core and firing orientation.

### Elite
Base silhouette plus one unmistakable elite treatment.

The game should communicate crushability primarily through:

- enemy silhouette/material,
- player speed-state VFX,
- interaction feedback.

Avoid persistent giant text labels.

## 9. Pickups and affordances

Consistent shape language for:

- boost,
- currency/reward burst,
- XP/reward,
- item,
- stage exit,
- high-risk shortcut markers.

Currency may burst physically, then magnetize/auto-collect to preserve momentum.

## 10. VFX priorities

VFX explains physics:

- rolling contact dust,
- boost trail,
- jump charge,
- jump release,
- slam streak,
- landing-burst cue,
- slam landing,
- enemy crush,
- failed impact,
- damage,
- Flow escalation,
- pickup,
- exit.

Prefer particles/shared simple meshes/shaders over temporary heavy node hierarchies.

## 11. Camera presentation

Accepted identity (D-072, after playtest):

- perspective, fixed pitch,
- chase composition: yaw follows the trajectory so the player always sees what is ahead,
- no manual rotation,
- player-adjustable bounded zoom,
- never clips terrain (03 §14).

At higher speed:

- increase forward look-ahead,
- modestly increase FOV,
- modestly increase distance,
- strengthen motion/trail cues,
- keep enough view ahead to read terrain.

No extreme FOV distortion.

### Structures (D-096)

- Tubes: the camera stays outside the shell and sees the player through it. The tube's collider is invisible
  to the occlusion probe; after the chase placement the lens is pushed radially out of the shell to at least
  the occlusion margin; ribs are thin (≤ 0.5 m) and spaced so any occlusion is momentary. The camera never
  enters a tube and is never pulled in by its walls.
- Wall tunnels (lids): confined framing, distance and pitch bounded to the declared clearance (`docs/11 §7`),
  still never clipping.
- Floors: a fall keeps the yaw and the framing bands hold the ball; no cut, no fade.

### Camera shake

- short,
- event-driven,
- bounded,
- stronger for slam landings/major impact/landing burst,
- reducible/disable-able.

No permanent noise shake during high speed.

## 12. HUD

Persistent MVP HUD:

- health,
- boost,
- Flow/multiplier,
- stage/run progress,
- currency.

Normal HUD does **not** need exact m/s.

Speed state is communicated primarily through player/camera/VFX; a small qualitative indicator is optional if playtests need it.

## 13. Stage/inter-stage UX

On normal stage completion, resolve needed safe-state interactions without building a long stack of menus.

Possible sequence:

1. immediate completion feedback,
2. queued level choice(s),
3. shop if applicable,
4. two route cards,
5. selected-stage load.

Combine screens where doing so reduces interruption.

## 14. Level-up presentation

Quick slot/reel-like reveal:

- anticipation,
- rarity cue,
- three final results,
- one choice.

Rarity tiers:

- Common,
- Rare,
- Legendary.

Presentation should be energetic but short.

## 15. Run summary

Show:

- victory/death,
- stage reached,
- score,
- major performance stats,
- build/items,
- landing-burst count if useful,
- meta XP.

Prioritize emotional recap over exhaustive telemetry.

## 16. Props/lighting

Procedural props:

- rocks,
- crystals,
- sparse vegetation where appropriate,
- pylons/markers,
- abstract structures.

Props reinforce scale/speed and do not accidentally obstruct mandatory lines.

Lighting baseline:

- one dominant directional light,
- WorldEnvironment ambient/fog,
- minimal local dynamic lights,
- readable shadows.

## 17. Retro restraint

Optional later:

- palette quantization,
- subtle color banding,
- vertex-color emphasis,
- constrained pixel/texture feel.

Not baseline requirements:

- aggressive vertex wobble,
- forced 240p,
- affine warping,
- heavy dithering that hides terrain.

Readability wins.

## 18. Minimal accessibility/readability settings

Even before a full accessibility pass:

- camera shake intensity/disable,
- motion-effect intensity if practical,
- HUD scale if straightforward,
- keyboard support and later controller parity.

No large settings framework during the Movement Toy.
