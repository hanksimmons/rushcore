using Godot;

namespace Rushcore.Generation;

/// <summary>
/// The accepted world-scale family (Gate M1, D-082; derivations in docs/11 §3). Generation
/// inputs only: movement values stay in <c>GameplayTuning</c>. One home, no panel exposure.
/// Metres, seconds, radians.
/// </summary>
public static class WorldScale
{
    // ---- stage footprint and route (docs/11 §3f) ----
    public const float FootprintLength = 6000f;          // along the stage axis (+X)
    public const float FootprintWidth = 2000f;
    public const float PrimaryRouteLength = 6000f;       // target; V-009 provisional
    public const float RouteBandHalfWidth = 600f;        // the primary route stays within ±this of the axis
    public const float SceneryMargin = 400f;
    public const float EntryMargin = 100f;               // start pad distance from the stage edge
    public const float ExitMargin = 100f;
    public const float TargetBaseKitSeconds = 60f;

    // ---- bend ladder (docs/11 §3a) ----
    public const float CruiseBendRadius = 160f;          // holds the cap (D-091: 250 m/s)
    public const float FastBendRadius = 100f;            // D-082: 148 m/s; D-091: 187 m/s
    public const float CommittedBendRadius = 50f;        // D-082: 99 m/s; D-091: 124 m/s; primary-route minimum
    public const float TechnicalBendRadius = 25f;        // optional lines only (84 m/s at D-091)
    public const float HairpinRadius = 15f;              // modules only (63 m/s at D-091)
    public const float BankHeightPerRadius = 18f / 145f; // lab hairpin reference: 18 m berm at r 145

    // ---- hills (docs/11 §3b) ----
    public const float LongSwellWavelengthMin = 1000f, LongSwellWavelengthMax = 1600f;
    public const float LongSwellHeightMin = 40f, LongSwellHeightMax = 80f;
    public const float RollerWavelength = 800f, RollerHeight = 80f;
    public const float LaunchCrestWavelengthMin = 300f, LaunchCrestWavelengthMax = 500f;
    public const float LaunchCrestHeightMin = 30f, LaunchCrestHeightMax = 60f;
    /// <summary>Crest radius a cruising ball needs to stay grounded: cap² / g at the frozen baseline.</summary>
    public const float CruiseCrestRadius = 560f;
    public const float MicroReliefMaxWavelength = 100f;
    /// <summary>Summed slope of the swell layer (tan); keeps the corridor under the route grade limit with a crest on top.</summary>
    public const float LongSwellMaxSlope = 0.18f;

    /// <summary>Largest height a relief component of wavelength λ may have without launching a
    /// cruising ball (04 §5C): λ² / (2π² · cruise crest radius).</summary>
    public static float MicroReliefHeightBound(float wavelength) =>
        wavelength * wavelength / (2f * MathF.PI * MathF.PI * CruiseCrestRadius);

    // ---- gaps (docs/11 §3c) ----
    public const float MandatoryGapMin = 40f, MandatoryGapMax = 120f;
    public const float OptionalGapMin = 160f, OptionalGapMax = 320f;
    public const float SetPieceGapMax = 400f;
    public const float MandatoryGapDepthMin = 12f, MandatoryGapDepthMax = 20f;
    public const float LandingZoneLength = 200f;
    public const float TakeoffRunwayLength = 150f;
    public const float GapExitWallMaxSlope = 0.466f;     // tan 25°

    // ---- ramps (docs/11 §3d) ----
    public const float RouteRampSlope = 0.20f;           // 11.3°, charge-rewarding
    public const float ModuleRampSlope = 0.35f;          // 19.3°
    public const float SetPieceRampSlope = 0.50f;        // 26.6°
    public const float RampRise = 20f;
    public const float MicroRampRise = 10f;
    /// <summary>Flat approach before a launch ramp's foot.</summary>
    public const float RampApproachLength = 100f;
    /// <summary>Rounding at the ramp's foot and lip (metres of route), and of the 45° back face.</summary>
    public const float RampEase = 12f, RampBackFaceEase = 18f;
    /// <summary>Route length of the gap's take-off rim face (steep, so the rim is a launch, never a slope).</summary>
    public const float GapRimFace = 8f;
    /// <summary>A mandatory gap must be crossable half-charged with this much to spare past the far rim (04 §11).</summary>
    public const float MandatoryGapMargin = 10f;

    // ---- challenge modules: the two-price rule (04 §5E, D-097) ----
    /// <summary>The free path through any module never drops the model below this fraction of the base cap.</summary>
    public const float FreePathSpeedFraction = 2f / 3f;
    /// <summary>A module's corridor profile eases from the relief to its level over this much route each side.</summary>
    public const float ModuleFlattenEase = 150f;
    /// <summary>A banked turn wants this much straight before it so the carve can be set up.</summary>
    public const float BankedTurnApproach = 100f;
    /// <summary>A module's own launch (a gap's exit wall, a ramp's lip taken uncharged) is the module's declared
    /// exception to the bend-clearance rule at the ceiling: the safe landing is the slam, pressed within this
    /// reaction time; the no-slam flight is reported, not enforced.</summary>
    public const float SlamReactionSeconds = 0.5f;

    // ---- corridor (docs/11 §3e) ----
    public const float OpenCorridorWidth = 300f;
    public const float TypicalCorridorWidth = 150f;
    public const float MinCorridorWidth = 75f;           // primary-route minimum
    public const float TechnicalCorridorWidth = 40f;     // optional lines only

    // ---- route constraints (04 §10) ----
    public const float MaxRouteGrade = 0.40f;            // tan 22°; the drive still climbs it easily (drive/g = 0.71)
    public const float MaxGradeDeltaPerSample = 0.08f;   // no abrupt kinks between 4 m samples
    public const float PadRadius = 60f;                  // flat start/exit pads
    public const float LaunchCrestSpacing = 1000f;       // at most one launch crest per this much route

    // ---- optional lines and checkpoints (04 §2, §13) ----
    public const int OptionalLinesMax = 3;
    public const float OptionalLineSpacing = 700f;       // route distance between optional lines
    public const float RidgeOffset = 200f;               // lateral offset of a ridge line from the primary
    /// <summary>Ridge plateau height: with the cosine ramp below, the knee radius 2L² / (π² H) stays above the cap's
    /// 560 m contact radius, so a ridge never launches the base kit into the bend it shadows (D-100; 25–40 m over
    /// 200 m smoothstep ramps launched every ridge at the cap).</summary>
    public const float RidgeHeightMin = 16f, RidgeHeightMax = 30f;
    /// <summary>Length of each lateral S-transition: 200 m over 290 m is a 70 m radius, the tightest S that still holds
    /// the base cap (68 m at D-091), so the base kit never brakes to leave or rejoin (D-100; was 360 m at r 108).</summary>
    public const float RidgeTransition = 290f;
    /// <summary>Shadowed section: 2 transitions + 2 ramps + a short plateau. The ramps lie wholly outside the transitions:
    /// inside one the primary's falloff blend scales the ridge's height by a rising weight, and a ramp under that
    /// weight becomes a convex knee at the cap's launch threshold (D-100, traced).</summary>
    public const float RidgeLength = 1260f;
    public const float RidgeRampLength = 300f;           // cosine climb onto / descent off the plateau (30 m → grade ≤ 0.16)
    /// <summary>An optional line stamps nothing while it is still inside the primary corridor and is fully its own beyond this offset.</summary>
    public const float OptionalStampFadeStart = 60f, OptionalStampFadeEnd = 160f;
    /// <summary>Inside a bend, an offset line needs this much radius left.</summary>
    public const float InsideOffsetMargin = 30f;         // leaves a technical-radius bend on the line (D-082: optional only)
    public const float OptionalBandHalfWidth = 800f;     // optional lines may use the scenery margin
    /// <summary>Branching exits (D-105): a terminal line leaves like a ridge (its offset and S), then widens to
    /// <c>ExitLineOffset</c> over <c>ExitLineSpread</c> (a lateral S of 150 m over 300 m: r ≈ 100, the base kit's, on the
    /// plateau where the S-transition already asked r 70), runs level for <c>ExitLineRun</c> and ends on a pad. Up to
    /// <c>ExitLinesMax</c> per stage, on opposite sides, their forks at least <c>ExitForkSpacing</c> apart, all inside the
    /// last <c>ExitZoneLength</c> of the primary, which rejoining lines leave alone.</summary>
    public const float ExitLineOffset = 350f;
    public const float ExitLineSpread = 300f;
    public const float ExitLineRun = 200f;
    public const int ExitLinesMax = 2;
    public const float ExitForkSpacing = 300f;
    public const float ExitZoneLength = 3000f;
    public const float ExitSeparationMin = 250f;        // any two exit pads, plan distance
    public const float CheckpointSpacing = 400f;

    // ---- Flow ceiling (04 §12, D-094; derived at D-091: 148.5 × 1.715 = 254.7 m/s, steering saturated at 322.9 m/s²) ----
    // Generation computes the live values from tuning; these are the family's reference numbers.
    public const float CeilingSpeedReference = 254.7f;
    public const float CeilingCrestRadius = 1647f;         // v²/g at the ceiling: every swell launches a full-chain ball
    public const float CeilingBendRadius = 201f;           // holds the ceiling; 68 m holds the base cap at D-091
    /// <summary>Straight route required past any touchdown (base kit or ceiling) before a bend may start.</summary>
    public const float LandingRunAfterFlight = 100f;
    /// <summary>Every emitted bend (≥ 10°) invites the carve that grants Flow (03 §11: the facing swings, not the road).</summary>
    public const float FlowBendMinDegrees = 10f;
    /// <summary>Metres a straight flight may drift off a bend's arc before it counts as flying the bend.</summary>
    public const float FlightDriftTolerance = 10f;

    // ---- Canyon Run (04 §6, D-098; docs/11 §7c walls) ----
    /// <summary>Side terrain above the channel floor.</summary>
    public const float CanyonWallHeightMin = 60f, CanyonWallHeightMax = 120f;
    /// <summary>Slot wall blend on the inside of bends and wherever the profile does not apply (blind corners, 04 §10).</summary>
    public const float CanyonWallFalloff = 12f;
    /// <summary>The wall profile (D-109, `WallProfile`): a 30 m circular foot fillet tangent to the floor, a 72° face (4–8
    /// cells across for 60–120 m of wall, so the facets carry the plane exactly), and a lip rounded into the side terrain
    /// over 8 m of height. Replaces the 12 m smoothstep foot, whose bottom had a curvature radius of 0.4 m: a kink the
    /// facets turned into a hit and no follow could carry (D-108).</summary>
    public const float WallFootRadius = 30f, WallFaceDegrees = 72f, WallLipEase = 8f;
    /// <summary>Face angle the profile eases to on the inside of a bend (with the bend's own fade): a 60 m wall then spans
    /// about 100 m, the blind-corner rule's reach (04 §10), and the wall is continuous along the route instead of
    /// switching between two blends at the bend's first vertex.</summary>
    public const float WallInsideFaceDegrees = 30f;
    /// <summary>Route over which the inside face eases from 72° to 30° before and after a bend: the face then recedes at
    /// about 12° along the route, a curve a ball riding the wall into the bend steers along, rather than the 45° step
    /// the bend's own 60 m fade made (the first ride into a bend's inside fell off the end of the face).</summary>
    public const float WallInsideFaceFade = 200f;
    /// <summary>Every wall stands this far outside the corridor's level width, so the ground follow's ±1 cell lateral samples never read it.</summary>
    public const float WallSetback = 8f;
    /// <summary>The wall shell (D-111): the wall band is a swept shell of the stamp, sampled every this many degrees round
    /// the fillet (a 3.1 m chord on the 30 m fillet, 4 cm of sagitta) and every <c>WallShellFaceStep</c> metres of lateral
    /// distance on the face (planar, so any step is exact); it begins <c>WallShellFoot</c> metres inside the corridor's
    /// edge on the level floor and ends <c>WallShellPastLip</c> metres beyond the lip. The heightfield under it is the same
    /// stamp sunk by <c>WallShellSink</c> so its coarse chords (up to 0.64 m proud of the fillet at 4 m cells) never reach
    /// the ball; the sink fades to nothing at both edges, where shell and grid coincide.</summary>
    public const float WallShellStepDegrees = 6f, WallShellFaceStep = 4f, WallShellFoot = 4f, WallShellPastLip = 6f, WallShellSink = 1f;
    /// <summary>Canyon bends carry a taller berm: banked lines are the archetype's skill (04 §6).</summary>
    public const float CanyonBankScale = 1.5f;

    // ---- Dune Sea (04 §6, D-099; docs/11 §3b dune row) ----
    /// <summary>The dune wave: one seeded directional cosine train across the stage, crests 0..H above the swells.</summary>
    public const float DuneWavelengthMin = 350f, DuneWavelengthMax = 500f;
    /// <summary>Dune height as a fraction of its wavelength: π × ratio is the face slope (11–15°), inside the
    /// route grade limit with the archetype's swell slope on top, so the corridor rides the wave untrimmed.</summary>
    public const float DuneHeightRatioMin = 0.06f, DuneHeightRatioMax = 0.085f;
    /// <summary>The wave's crest lines run within this angle of across the stage axis.</summary>
    public const float DuneWaveAngleMax = Mathf.Pi / 18f;                 // 10°
    /// <summary>A straight hosts a dune train only within this angle of the wave's travel direction: the corridor is
    /// level across, and a crest line crossing it more obliquely would leave a seam at the corridor's edges.</summary>
    public const float DuneTrainAlignment = Mathf.Pi * 35f / 180f;
    /// <summary>Crests in a dune train: the rhythm is crest to crest, so one crest is never a train.</summary>
    public const int DuneTrainCrestsMin = 2, DuneTrainCrestsMax = 4;
    /// <summary>Swell slope budget on a dune sea: the dunes are the relief, the swells only tilt the field.</summary>
    public const float DuneSwellMaxSlope = 0.08f;

    // ---- Sky Terraces (04 §6, §5I; docs/11 §7a–b; D-103) ----
    /// <summary>A floor step: each terrace stands this much above the floor below, so every drain (a step over its
    /// falloff, <see cref="TerraceFalloff"/>) stays at the route grade limit. The bottom of the jump-step band (docs/11 §7a).</summary>
    public const float FloorStep = 60f;
    /// <summary>Route length a floor step drains over: a smoothstep's steepest grade is 1.5× its mean, so the step over
    /// the route grade times 1.5 keeps every drain (and every terrace's outer falloff, scaled by its height) at the limit.</summary>
    public const float TerraceFalloff = 1.5f * FloorStep / MaxRouteGrade;      // 225 m per 60 m step
    /// <summary>The primary's own falloff on a terraced stage: short enough that floor 2's cliff foot lies beyond it on the flat flank.</summary>
    public const float SkyPrimaryFalloff = 60f;
    /// <summary>A terrace's inner edge, toward the floor below, is a cliff (a wall of the family kind, docs/11 §7c): the ball falls
    /// onto that floor, which is the drain.</summary>
    public const float TerraceCliffFalloff = 12f;
    /// <summary>A terrace's outer falloff allows for the relief under it sitting a half swell below the primary's base.</summary>
    public const float TerraceOuterMargin = LongSwellHeightMax * 0.5f;
    /// <summary>Floor 2: 60 m up, 200 m to the side (the ridge offset: its cliff foot lies just beyond the primary's falloff), an
    /// S of 290 m at r 70, a 420 m cosine climb (knee radius 2L²/π²H above the cap's contact radius).</summary>
    public const float Floor2Offset = 200f, Floor2Ramp = 420f, Floor2Transition = 290f;
    /// <summary>Floor 3: 120 m up, 300 m to the side (floor 2's outer edge plus a cliff and a half width), an S of 360 m, a 600 m
    /// climb; it shares its section with a floor 2, whose outer edge is then the cliff up to it.</summary>
    public const float Floor3Offset = 300f, Floor3Ramp = 600f, Floor3Transition = 360f;
    /// <summary>Outer falloff of a terrace of the given height: the height plus the relief margin, over the grade limit, times the smoothstep's 1.5.</summary>
    public static float TerraceOuterFalloff(float height) => 1.5f * (height + TerraceOuterMargin) / MaxRouteGrade;
    /// <summary>The cloud band begins this far above the primary's mean height: the top floor sits in it (06 §3).</summary>
    public const float CloudBandHeight = 130f;

    // ---- spiral pit (04 §6 Canyon Run set-piece, docs/11 §7c, D-102) ----
    /// <summary>The outermost turn's radius: the band holds it with its corridor once the approach has moved the centre near the axis.</summary>
    public const float SpiralOuterRadius = 380f;
    /// <summary>Radius shed per full turn: two level widths on bends plus their setbacks plus a face for the cliff between turns.</summary>
    public const float SpiralRadiusPerTurn = 230f;
    public const float SpiralTurns = 1f;
    /// <summary>Depth of the pit floor below the entry: the cliff between turns, and the fall onto the turn below.</summary>
    public const float SpiralDepth = 120f;
    /// <summary>Largest berm any bend carries: the family bank grows with radius and a 380 m turn would otherwise carry 70 m.</summary>
    public const float MaxBankHeight = 30f;

    // ---- lids: wall tunnels (04 §5I, docs/11 §7e, D-102; provisional sizes V-016) ----
    /// <summary>Clearance between the corridor and the roof's underside; the rig's 6.7 m rise fits under it.</summary>
    public const float LidClearance = 15f;
    public const float LidLengthMin = 150f, LidLengthMax = 300f;
    public const float LidThickness = 6f;
    public const int LidsMax = 2;
    /// <summary>The lens keeps this much below a roof it is under.</summary>
    public const float LidCameraMargin = 1.5f;

    // ---- tunnels (docs/13 §2, D-113; every number provisional, V-017) ----
    /// <summary>The tunnel corridor. The plan's 10 m half-width was a wall-hit generator for the harness follower at the cap through
    /// the S (16 m off the line, every touch a climb): 15 m gives the line-holding error room, and the wall ride inside is still
    /// a lane change, not a loop.</summary>
    public const float TunnelHalfWidth = 15f;
    /// <summary>The tunnel's own wall profile: a 6 m fillet that fits the slot and a 78° face (a tunnel wall is a wall, not a bank); no setback.</summary>
    public const float TunnelFootRadius = 6f, TunnelFaceDegrees = 78f;
    /// <summary>Where the arch springs from the walls (height above the floor). The arch is the circular arc tangent to the face
    /// there (a horseshoe; <c>TunnelProfile.Crown</c> follows, about 33 m), so a ball riding up the wall runs onto the arch and
    /// drops off it where the ceiling rule ends the ride, with no crease to hit. The wall stands this high so a touch at up to
    /// 30 m/s of lateral speed (12° off the bore at the cap; a climb of v²/2g) rides the wall and comes back to the floor, and
    /// only a worse line reaches the arch and falls from it (a priced landing). A full charge under it is refused, as under a lid.</summary>
    public const float TunnelSpringHeight = 12f;
    /// <summary>The arch is sampled every this many degrees of its arc.</summary>
    public const float TunnelArchStepDegrees = 10f;
    /// <summary>Rock over the crown a tunnel needs before it is covered (<c>TunnelProfile.PortalDepth</c> is the crown plus this); the
    /// portal face stands where the surrounding ground reaches it.</summary>
    public const float TunnelCapThickness = 4f;
    /// <summary>The cap (the ground restored over the trench) runs this far past the trench's lip, like the wall shell past its lip.</summary>
    public const float TunnelCapMargin = 6f;
    /// <summary>The tunnel line's offset from the primary (the ridge's: on a canyon the trench then lies wholly beyond the wall's lip), its S
    /// and the run at full offset between the two S's. The S is a cosine (peak curvature O·π²/2L², 18% under the smoothstep's) and
    /// as long as its site allows: at least the ridge's 290 m (r ≈ 85 m, which holds the base cap with margin) and up to 450 m
    /// (r ≈ 205 m, which holds the Flow ceiling), because both S's must lie on primary straights, 200–450 m on a canyon, and a slot
    /// asks for a gentler entry than a 75 m corridor does.</summary>
    public const float TunnelOffset = RidgeOffset, TunnelTransition = RidgeTransition, TunnelTransitionMax = 450f, TunnelPlateau = 120f;
    /// <summary>The sink under a tunnel's wall shell (D-111 for the canyon wall is 1 m). A 4 m cell straddles most of a 6 m fillet, so
    /// the grid's chords stand up to 1.5 m proud of the arc there (the ball hit them through the shell at 1 m: a 10 m/s loss and
    /// a launch on the first tunnel drive); 3 m keeps every chord under the shell, and the trench is deep enough to hide it.</summary>
    public const float TunnelShellSink = 3f;
    /// <summary>A tunnel cuts nothing inside the primary's level width and setback; its trench fades in over this much of the fillet's foot.</summary>
    public const float TunnelMouthFade = 8f;
    /// <summary>The shortest covered run that counts as a tunnel.</summary>
    public const float TunnelCoveredMin = 60f;
    /// <summary>No lid and no other tunnel's portal within this much plan distance of a portal.</summary>
    public const float TunnelPortalClearance = 100f;
    /// <summary>The camera's confinement eases in over this much before a portal and out after it (06 §11).</summary>
    public const float TunnelCameraEase = 30f;
    /// <summary>The portal's rock rim around the arch, and the guide strips along the walls at ball height every this far.</summary>
    public const float TunnelRimSize = 1.5f, TunnelGuideSpacing = 25f, TunnelGuideHeight = 1.5f;
    /// <summary>Inside, the rock darkens to this share of its lit colour this far from the nearer portal (06 §3).</summary>
    public const float TunnelShadeFloor = 0.45f, TunnelShadeDepth = 60f;

    // ---- heightfield (docs/11 §3g) ----
    public const float CellSize = 4f;
    /// <summary>Route polyline vertex spacing; equal to the cell size so relief and stamping see every facet.</summary>
    public const float RouteSampleSpacing = CellSize;
}
