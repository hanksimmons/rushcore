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
    public const float RidgeHeightMin = 25f, RidgeHeightMax = 40f;
    /// <summary>Length of each lateral S-transition: 200 m over 360 m is a 108 m radius, a fast bend.</summary>
    public const float RidgeTransition = 360f;
    public const float RidgeLength = 1300f;              // shadowed section: 2 transitions + 2 ramps + plateau
    public const float RidgeRampLength = 200f;           // climb onto / descent off the plateau (40 m → grade 0.20)
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

    // ---- heightfield (docs/11 §3g) ----
    public const float CellSize = 4f;
    /// <summary>Route polyline vertex spacing; equal to the cell size so relief and stamping see every facet.</summary>
    public const float RouteSampleSpacing = CellSize;
}
