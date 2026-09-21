# 18 — Concept Reference Index

**Status:** RECORD, 2026-09-20. This document does not decide anything; it records what each concept image
contains, what it establishes as canon, what it contradicts, and the measured colour values taken from it.
**Authority:** none. It is evidence for `docs/14_ART_DIRECTION_BIBLE.md`,
`docs/15_ENVIRONMENT_ART_BIBLES.md` and `docs/17_CEL_SHADING_TECHNICAL_SPEC.md`.
**Images:** `docs/concept/` — sixteen unique frames generated 2026-09-20.

---

## 0. The set at a glance

| # | File | Environment | Style | Status |
|---|---|---|---|---|
| 01 | `01_keyart_highlands_ridge` | Rolling Highlands | painterly | mood/composition reference |
| 02 | `02_highlands_descent_gates` | Rolling Highlands | painterly | mood reference; gate motif |
| 03 | `03_canyon_wallride_portal` | Canyon Run | painterly | **canon** for wall ride + portal |
| 04 | `04_lowpoly_valley_dams` | Rolling Highlands | low-poly | pre-cel reference |
| 05 | `05_highlands_stencil_viaduct` | Rolling Highlands | painterly | **canon** for signage |
| 06 | `06_lowpoly_ridge_dams` | Rolling Highlands | low-poly | pre-cel reference |
| 07 | `07_sky_terraces_hud` | Sky Terraces | painterly | **canon** for HUD + terrace numbering |
| 08 | `08_canyon_run_hud_wallride` | Canyon Run | painterly | **canon** for wall stencils + HUD |
| 09 | `09_spiral_pit` | Canyon Run set-piece | painterly | **canon** for the spiral pit |
| 10 | `10_sky_terraces_overdrive` | Sky Terraces | painterly | **canon** for Overdrive + cloud sea |
| 11 | `11_highlands_overlook_monoliths` | Rolling Highlands | painterly | **canon** for monoliths |
| 12 | `12_cel_dusk_ridge` | Rolling Highlands | **cel** | style precursor |
| 13 | `13_cel_valley_portal_hud` | Highlands / portal | **cel** | style precursor; portal read |
| **14** | `14_CEL_MASTER_ridge_descent` | Highlands / Summit | **cel** | **THE MASTER FRAME** |
| **15** | `15_CEL_scree_slope_debris` | Highlands / Summit slope | **cel** | **master: debris + trail** |
| **16** | `16_CEL_slam_impact_rings` | (pad / plaza) | **cel** | **master: slam + burst VFX** |

**Two duplicates were received and not stored:** `05_47_12 PM` (identical to 04) and `06_07_02 PM` (identical
to 14). Eight other files in the source folder were unrelated to RUSHCORE and were excluded.

**The style decision (`14 §3`) rests on 14, 15 and 16.** Images 01–11 are retained for composition, scale,
architecture, signage and HUD — not for surface treatment.

---

## 1. `01_keyart_highlands_ridge` — the branded key art

**Contains:** a grass-and-rock ridge running to a sunset horizon; the ball mid-ridge with four cyan streak
trails and a dust plume; two enormous concrete dam structures left and right, both stencilled `RUSHCORE`;
white painted chevrons on the far dam's curved face; a cloud sea filling the valleys; snow-capped ranges to
the horizon; corner type — `RUSHCORE / HIGHER FASTER FURTHER`, `A CLEANER HORIZON MOVES US ALL`,
`SPEED SHAPES A BRIGHTER TOMORROW`, `TERRAIN BUILDS PEOPLE`.

**Establishes as canon:**
- The **fiction** (`14 §1`): civic megaproject, slogans at building scale, authentic wilderness.
- The **four-streak trail** at Overdrive and its surface-hugging behaviour.
- The **cloud sea** as a major depth device (`14 §5.2`).
- Chevrons as a wall-scale directional cue.
- Composition: horizon high (~62%), ball low (~34%), a vertical mass on **both** sides — the one image that
  does so, and `14 §11.3` prefers one.

**Contradicts:** the ridge reads ~20 m wide (the real corridor is 150–300 m); the dams sit within the
headroom envelope over the route.

**Measured:** sun halo `#FDD76B`; lit faces `#926752`; midtone `#4D383B`; shade `#2C2527`; cyan peak
`#50AEC3`, glow core `#67D8E9`; sky top `#61364C`.

---

## 2. `02_highlands_descent_gates` — the gate motif

**Contains:** a wide cast-concrete apron descending into a valley; **floating cyan chevron gates** hovering
over the route ahead; scattered low-poly rocks; a distant city and lake at sunset; monolithic walls flanking.

**Establishes:** the **gate/gantry motif** (`14 §9.2`) — a frame the player drives through. The concept shows
them floating; the build must make them structures on piers, declared and validated (`14 §17.4`).

**Contradicts:** floating geometry with no support; a fully paved corridor (`14 §17.2`).

**Measured:** sun halo `#FDD173`; lit `#AC6F51`; shade `#393235`; cyan `#5CBFC2`/`#76DEDF`; horizon `#AC6546`.

---

## 3. `03_canyon_wallride_portal` — wall ride and portal in one frame

**Contains:** the ball riding a **banked concrete wall** on the outside of a bend, cyan streaks and a dust
sheet on the wall plane; a large **arched tunnel portal** in a rock face ahead, with interior wall lights
visible down the bore; a bridge crossing the canyon beyond; red stratified rock above the cast lower wall;
snow peaks on the horizon.

**Establishes as canon — the most mechanically important image in the set:**
- The **concrete-below / rock-above** wall split (`15 §2.5`): the cast portion is exactly the band the ball
  can ride; the natural rock is above it.
- The **fillet-to-face** profile reading as a rideable bank.
- The **portal** with its proud rim, interior lights and visible bore (`15 §6`, confirming D-113).
- Wall-ride dust shedding **on the wall plane**, not trailing behind — the missing VFX in `14 §10.3`.

**Contradicts:** the ride surface looks ~15 m wide; the real corridor between canyon walls is 75–150 m.

**Measured:** brightest `#FEE8B9`; lit `#A79179`; midtone `#6C5C4D`; shade `#332F2F`; cyan `#64C7C8`/`#83DFDB`.

---

## 4. `04_lowpoly_valley_dams` — the faceted precursor

**Contains:** a broad faceted valley floor in large flat polygons; conifers as dark triangles; enormous
concrete dam walls on both flanks; a viaduct and a hilltop structure; a gold river; violet sunset sky.

**Establishes:** that **large flat facets with per-facet tonal jitter read beautifully** — which is what
`FacetJitter` and `BasePalette` already produce in code. A direct precursor to the cel decision.

**Contradicts:** nothing material.

**Measured:** sun halo `#FEE277`; lit `#875751`; midtone `#493838`; shade `#2A2326`; sky top `#643F64`.

---

## 5. `05_highlands_stencil_viaduct` — the signage canon

**Contains:** a green valley with the ball crossing it; a colossal wall face left, stencilled
`RUSHCORE / HIGHER FASTER FURTHER` with a triple-bar logo mark and black banner panels; a lit doorway right,
captioned `VAST CLEAN RELENTLESS TOGETHER`; a viaduct sweeping across the middle distance to a hilltop
complex; `HIGHER A HIGHER WORLD` on a distant structure.

**Establishes as canon:**
- The **stencil system** (`14 §9.1`): wordmark, slogan block, logo mark, sizes.
- The **triple-bar logo mark** (`|||`) — recurs in 08, 14, 15. Adopt it as the programme's glyph.
- Black banner panels as a vertical accent on light concrete.
- The viaduct as **middle-distance scenery**, correctly *not* over the route.

**Measured:** sun halo `#FDDA61`; lit `#8B6359`; shade `#2C261F`; sky top `#604057`.

---

## 6. `06_lowpoly_ridge_dams` — faceted ridge, gold rim

**Contains:** a faceted ridge in olive and gold; the ball with a cyan trail and dust; dam walls with
waterfalls either side; distant spires; strong violet-to-orange sky.

**Establishes:** the **gold rim on lit ridge edges** that becomes mandatory in `17 §5`; faceted terrain with
two-value planes.

**Measured:** sun halo `#FDC54A`; midtone `#564149`; shade `#2B262A`; cyan `#46C3C6`/`#44E3E5`; sky top
`#703471` (the most saturated violet in the set).

---

## 7. `07_sky_terraces_hud` — the HUD and the terrace stack

**Contains:** a full HUD mockup over Sky Terraces. Terrace blocks numbered `01 FOUNDATIONS MOTION MORE`,
`02 ASCEND REFINE PUSH`, `03 PEAKS AHEAD`; a `SKY TERRACES` banner with a triangle glyph; a `RUSHCORE /
HIGHER FASTER FURTHER` wall; cyan edge lighting along the route; a cloud sea with peaks piercing it; cypress
trees. HUD: top-left `SKY TERRACES / SECTOR 1.3 / ALT 2468 M / TO NEXT 238 M`; top-right `TIME / BEST /
RINGS`; bottom-left `OVERDRIVE` + bar; bottom-right `418 KM/H`.

**Establishes as canon:**
- The **HUD layout** (`14 §14`).
- **Numbered terrace risers** (`15 §4.5`) — the archetype's signature signage.
- **Cyan edge lighting on the drivable edge** — the origin of the corridor edge system (`14 §12`).
- Banners as a permitted Sky Terraces element.

**Contradicts:** km/h vs the spec's m/s (resolved in `14 §14`); the route reads ~20 m wide.

**Measured:** sun halo `#FDD568`; lit `#B67756`; midtone `#664849`; cyan `#61D6DE`; horizon `#A2603B`.

---

## 8. `08_canyon_run_hud_wallride` — the wall you ride is the wall that speaks

**Contains:** the ball riding a banked wall stencilled `RUSHCORE / FURTHER FASTER / A CLEANER TOMORROW` with
a triple-bar mark; a sector block `03 PEOPLE MOMENTUM HIGHER`; `04 CANYONS CONNECT HUMANITY` on a further
slab; red stratified canyon walls; bridges below; chevron runs on a lower roadway; HUD top-left
`RUSHCORE / CANYON RUN // SECTOR 03`, top-right `TIME / SPEED 412 km/h`, corner slogans.

**Establishes as canon:**
- **Wall stencils above the ride band** (`15 §2.7`, C3) — and critically, that the type is **flush**, with no
  raised profile, satisfying the ride-band rule (`14 §8.5`).
- **Sector numbering on wall slabs** at checkpoints.
- **Chevron runs** on a roadway surface.
- The canyon palette: red rock `#9E5C3D` family with strata.

**Measured:** sun halo `#F7D372`; lit `#73554D`; midtone `#47342F`; shade `#30201E`; cyan `#63B5C6`/`#6CE7FB`.

---

## 9. `09_spiral_pit` — very nearly a render of D-102

**Contains:** a descending helix of banked roadways into a buttressed shaft; lit edges along every turn;
chevrons on each turn; radial concrete buttresses between turns; rock between them; rim slabs reading
`GRAVITY BUILDS BETTER RACERS`, `DEEPER FASTER FURTHER`, `A SMALLER YOU A LARGER TOMORROW`, `A DEEPER
HUMANITY`, `SPEED SHAPES A BRIGHTER TOMORROW`; `RUSHCORE` banners; a sunset and cloud sea beyond the rim.

**Establishes as canon (`15 §7`):** radial buttresses at the panel modulus; a continuous lit edge spiralling
down; chevrons per turn; slogan slabs on the rim facing inward; the light falling off toward the pit floor.

**Contradicts:** nothing — the delivered geometry (380→150 m over 120 m of descent) matches this image's
structure closely. **This is the most directly buildable image in the set.**

**Measured:** brightest `#B7E5F8` (the lit edges, not the sun — unique in the set); lit `#7B6757`; shade
`#252324`; cyan `#45A8CE`/`#6AE2FA` at 6% of pixels, the highest SIGNAL density of any frame.

---

## 10. `10_sky_terraces_overdrive` — what Overdrive should look like

**Contains:** a causeway above a cloud sea; **cyan light strips running the full length of both edges**; the
ball at speed with a wide cyan flare and long streaks; a gantry arch over the route; mountains and towers
beyond; HUD `SKY TERRACES / SECTOR 07 / > OVERDRIVE / SPEED 1,248`.

**Establishes as canon:** the **Overdrive treatment** (`14 §10.3`) — the ball's flare widening, streaks
lengthening dramatically, the world's value dropping around it; the **gantry over the route**; the edge-strip
system at its most explicit.

**Contradicts:** the gantry crosses the route (needs declaring, `14 §17.4`); the deck is ~20 m wide.

**Measured:** brightest `#FBE4A8`; midtone `#594B53`; shade `#2E303C`; cyan `#4086AD`/`#65DAF9` at 8% — the
highest in the set, which is itself the lesson about Overdrive.

---

## 11. `11_highlands_overlook_monoliths` — monoliths and a banked bowl

**Contains:** a concrete overlook of stepped monoliths reading `RUSHCORE / HIGHER FASTER FURTHER`,
`SPEED SHAPES A BRIGHTER WORLD`, `TERRA MOTUS ALTIORA`, `MORE SPEED A KINDER PLANET`, `A BIGGER TOMORROW`; a
green river valley below; a **banked concrete bowl/quarter-pipe** on the right; a circular arch monument;
snow peaks.

**Establishes as canon:** the **slogan monolith** family (`14 §9.2`, H2); stepped monolith clusters as
scale-cue props; the banked bowl as a legitimate structure form; the Latin motto usage.

**Measured:** brightest `#F6D39E`; lit `#9C7763`; midtone `#62504B`; sky top `#664B63`.

---

## 12. `12_cel_dusk_ridge` — the first cel frame

**Contains:** a dark faceted ridge descending; the ball with a bright cyan trail; dam walls and silhouetted
structures; a gold river and sunset; the whole image reduced to three or four values.

**Establishes:** that a **posterised, low-key frame reads the route more clearly at speed** than the
painterly frames do. The direct precursor to the style decision.

**Measured:** sun halo `#FCCE4D`; midtone `#312830`; shade `#18161A`; sky top `#5E3450`.

---

## 13. `13_cel_valley_portal_hud` — cel with a portal and a HUD

**Contains:** a near-black monolithic wall left with thin gold edge highlights; a green valley with a river; a
**circular portal in a mesa** right, with a viaduct running over it; distant spires; HUD `612 KM/H` and a
cyan bar bottom-left.

**Establishes as canon:** the **portal read from distance** (a dark circle in a lit face, unmistakable at
range); thin gold edge highlights as the definition of form in shadow — the direct ancestor of the rim term
(`17 §5`); a minimal two-element HUD.

**Measured:** sun halo `#FDDA60`; midtone `#221D23`; shade `#0F1018`; darkest `#030305`; sky top `#3F2E4B`.

---

## 14. `14_CEL_MASTER_ridge_descent` — **the master frame**

**Contains:** a ridge route descending toward a valley; the ball with a hard cyan trail and a row of **flat
grey dust puffs**; large flat terrain planes in violet-grey with **hot gold rim light on every lit edge**; a
gold river in a shadowed canyon left; flat-tiered violet mountain ranges receding; a viaduct and a spired
complex in silhouette; hard-edged cel clouds in stacked bands; a plain pale sun disc; angular rock shards
right.

**Establishes as canon — this frame governs the entire visual specification:**

| What it shows | Where it is specified |
|---|---|
| Two plateaus per surface, hard terminator | `17 §3.1`–`§3.2` |
| Hot gold rim on every lit edge | `17 §5` (mandatory) |
| Low-key image, shadow dominant | `14 §3.1` item 3 |
| Violet shade / gold light split | `17 §3.2` |
| Distance as flat value tiers, not blur | `17 §5.4` |
| Dust as discrete flat puffs | `17 §8.1` |
| Hard-edged banded clouds, flat sun disc | `14 §5.2` |
| Silhouetted structures as pure value | `15 §1.6` |

**Measured plateaus (the starting values for `17` X1):** dominant flats `#282838` (11.5%), `#484848` (11.2%),
`#383838` (9.9%), `#181828` (6.4%), `#483848` (6.4%) — **five violet-grey plateaus covering 45% of the
frame.** Sun/horizon gold `#FED042`; rim gold `#FFC24A`; far ranges `#2A273D`; sky upper `#523A4E`.

---

## 15. `15_CEL_scree_slope_debris` — the trail lights the world

**Contains:** a steep scree slope in near-black; the ball far up-slope with a **ground-hugging cyan wash**
trailing back; **flat black debris chunks with cyan underglow** thrown across the foreground; a long concrete
structure above stencilled `S.07 / HIGHER FASTER FURTHER` with the triple-bar mark; a hanging banner reading
`A GREATER HORIZON`; gold cel clouds; a viaduct and hilltop complex at sunset; a gold river.

**Establishes as canon:**
- **Debris treatment** (`17 §8.2`): flat silhouettes lit from below by the trail. The trail is a light source
  in the composition; nothing else is.
- The **`S.07` sector-code format** — adopt for sector markers (`14 §9.2`).
- **Hanging banners** on structures.
- The extreme low-key register: `18.9%` of the frame is `#080808`.

**Measured:** brightest `#F9BB53`; midtone `#29242F`; shade `#0A070E`; darkest `#020203`; cloud gold
`#E39241`.

---

## 16. `16_CEL_slam_impact_rings` — the slam, rendered

**Contains:** the ball at the centre of an impact on a cast plaza; a **flat white-cyan ground shock ellipse**;
a **vertical bow arc** rising over it; **radial crack streaks** running outward along the ground; flat
debris chunks thrown up; a cracked slab surface; monolithic structures framing left and right; a silhouetted
city, viaducts and cel clouds at sunset behind.

**Establishes as canon — it is very nearly a render of the delivered D-077 VFX:**

| What it shows | Delivered? |
|---|---|
| Ground shock ring, flat on the surface, hard-edged | yes (`PlayerVfx`) |
| Air-parting bow ring ahead/above | yes |
| Thrown debris chunks | yes (carve debris; extend to slams) |
| **Radial crack streaks along the ground** | **no — new, adopt** (`17 §8.3`) |
| White-cyan `#E8F6FF` / `#B8E6FF` core and body | yes, matches `BoomColor` |

**This is the strongest corroboration in the set that the delivered VFX design was right.** The only addition
is the crack streaks.

**Measured:** brightest `#E2E5F3` (the rings — the only frame where the brightest element is not the sun);
midtone `#262330`; shade `#0F0D15`; sky top `#403049`.

---

## 17. What the set does not contain

Gaps, and therefore the outstanding commissions (`14 §20`, `15 §8.4`):

| Missing | Consequence |
|---|---|
| **Any enemy** | Pylon, Bulwark, Strider, Shooter and the Elite are undesigned under this direction. Largest outstanding commission. |
| **Dune Sea** | The entire archetype is unreferenced; `15 §3` is derived from spec alone. |
| **Pickups** | No boost ring, XP orb, cash ball or item in any frame. |
| **The in-map shop** | Undesigned and unreferenced; it must be findable but missable. |
| **Summit Descent proper** | Only adjacent imagery (14, 15). No hairpin, rail, headwall or chute. |
| **A pocket interior** | Slice T3's set-piece is unreferenced. |
| **Level-up / run-summary UI** | Only in-run HUD appears. |
| **Daylight other than sunset** | Deliberate — the hour is fixed (`14 §4.1`). |

---

## 18. Provenance

Sixteen unique frames, generated 2026-09-20 between 17:40 and 18:07 local, delivered as a bulk folder drop and
copied into `docs/concept/` on the same day. Two duplicates and eight unrelated images in the source folder
were excluded. Colour values throughout this document were measured programmatically (median of a resized
sample, plus quantized dominant-plateau analysis for the cel frames); they are sRGB hex and should be divided
by 255 for Godot `Color`, with the caveat that concept-art values are a **starting point for tuning**, not a
specification — the final values are whatever the `Cel ›` tuning group lands on in playtest.
