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
    /// <summary>Slot wall: the channel blends into the side terrain over three cells, an ~80° face at 100 m.</summary>
    public const float CanyonWallFalloff = 12f;
    /// <summary>Every wall stands this far outside the corridor's level width, so the ground follow's ±1 cell lateral samples never read it.</summary>
    public const float WallSetback = 8f;
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
    /// <summary>Floor 2: a terrace 60–90 m above the primary (the jump-step band), 200 m to the side, climbed over 500 m
    /// (cosine ramp: the knee radius 2L²/π²H stays above the cap's contact radius at 90 m).</summary>
    public const float Floor2HeightMin = 60f, Floor2HeightMax = 90f, Floor2Offset = 200f, Floor2Ramp = 500f, Floor2Transition = 290f;
    /// <summary>Floor 3: 150–200 m up, 550 m to the side (its drain to floor 2 then stays inside the route grade), climbed over 850 m.</summary>
    public const float Floor3HeightMin = 150f, Floor3HeightMax = 200f, Floor3Offset = 550f, Floor3Ramp = 850f, Floor3Transition = 480f;
    /// <summary>Every terrace edge drains at or under the route grade limit: the corridor falloff on this archetype is the tallest step over that grade.</summary>
    public const float TerraceFalloff = 190f;
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

    // ---- tubes (04 §5I, docs/11 §7d–e, D-101; provisional sizes V-016) ----
    public const float TubeRadius = 6f;
    /// <summary>Mouth flare: the radius doubles over one tube diameter at each end.</summary>
    public const float TubeMouthFlare = 2f;
    public const float TubeRibSpacing = 25f;
    /// <summary>Axis clearance above the ground and from walls away from the mouths (the camera's room outside the shell).</summary>
    public const float TubeClearance = 30f;
    /// <summary>A ground mouth sits this far to the side of the line it leaves, inside the level width, so the free path never enters it.</summary>
    public const float TubeMouthOffset = 45f;
    /// <summary>Lateral offset of the cruise from the line it shadows.</summary>
    public const float TubeLateralOffset = 200f;
    /// <summary>A tube section in order: climb over the mouth offset, swing out, cruise, swing back, descent. The climb
    /// keeps the pitch under about 14° (longer for a higher cruise); the swing is an S of the lateral offset over this
    /// length (r ≈ 270 m: a 64° wall ride at the base cap, docs/11 §7d); the cruise is the straight between.</summary>
    public const float TubeRampLength = 300f, TubeSwingLength = 500f, TubeCruiseLength = 200f;
    /// <summary>Steepest climb pitch (tan) a tube asks: a driven ball climbs 35° without losing speed, this stays well under.</summary>
    public const float TubeMaxPitch = 0.25f;
    /// <summary>Straight required on the line before an entry mouth and after an exit mouth (the landing zone).</summary>
    public const float TubeMouthStraight = 100f;
    public const int TubesMax = 2;
    /// <summary>The lens keeps this much more than the radius from a tube axis (docs/11 §7e).</summary>
    public const float TubeCameraMargin = 1.5f;

    // ---- heightfield (docs/11 §3g) ----
    public const float CellSize = 4f;
    /// <summary>Route polyline vertex spacing; equal to the cell size so relief and stamping see every facet.</summary>
    public const float RouteSampleSpacing = CellSize;
}
