using Godot;

namespace Rushcore.Generation;

/// <summary>Terrain archetypes (04 §6). Rolling Highlands (D-085) and Canyon Run (D-098); Dune Sea and Sky Terraces follow.</summary>
public enum TerrainArchetype { RollingHighlands, CanyonRun }

/// <summary>
/// What a stage is generated from (04 §3). Danger tier, route modifiers and reward category
/// join when the run layer exists (Phases 5–6); nothing reads them yet.
/// </summary>
public sealed record StageGenerationRequest(int RunSeed, int StageIndex, TerrainArchetype Archetype = TerrainArchetype.RollingHighlands, float DifficultyScalar = 1f)
{
    public ulong StageSeed => SeedChain.Derive(RunSeed, "stage", StageIndex);
    /// <summary>Gameplay seed for the route skeleton; bumped per bounded regeneration attempt.</summary>
    public ulong RouteSeed(int attempt) => SeedChain.Derive(StageSeed, "route", attempt);
    /// <summary>Separate stream for cosmetics so scatter never perturbs geometry (05 §9).</summary>
    public ulong CosmeticSeed => SeedChain.Derive(StageSeed, "cosmetic");
}

public enum RouteSegmentKind { Straight, Bend }

/// <summary>One sample of the primary route, every <see cref="WorldScale.RouteSampleSpacing"/> metres.</summary>
public struct RouteVertex
{
    public Vector3 Position;
    /// <summary>Travel heading in radians from +X toward +Z.</summary>
    public float Heading;
    /// <summary>Bend radius at this sample; +∞ on straights.</summary>
    public float Radius;
    /// <summary>Cumulative route distance.</summary>
    public float Distance;
    public RouteSegmentKind Kind;
}

public enum RouteFeatureKind { LaunchCrest, Gap, LaunchRamp }

/// <summary>
/// A reserved zone on the route (04 §5A): a straight long enough to host a feature and its landing.
/// Since D-097 the zone hosts a challenge module (04 §5E) as well as a launch crest: a mandatory gap
/// (take-off runway, opening, landing zone) or a launch ramp (approach, lip, back face, landing zone).
/// </summary>
public sealed class RouteFeature
{
    public RouteFeatureKind Kind;
    public int StartIndex, EndIndex;
    /// <summary>Route distance of the feature centre: the crest apex, the gap's take-off rim, the ramp's lip.</summary>
    public float CentreDistance;
    /// <summary>Crest: cosine wavelength and height.</summary>
    public float Wavelength, Height;
    /// <summary>Gap: opening (rim to rim) and floor depth.</summary>
    public float Opening, Depth;
    /// <summary>Ramp: slope (tan) and lip height above the approach.</summary>
    public float Slope, Rise;
    /// <summary>Straight run needed after <see cref="FeatureEnd"/>: reserved by the skeleton for a gap or a ramp
    /// (the full-charge flight at the cap plus the landing run), filled by validation for a crest.</summary>
    public float LandingDistance;
    public bool IsLaunch;
    /// <summary>Route distance where the geometry ends: the far rim, the foot of the back face, the crest's end.</summary>
    public float FeatureEnd => Kind switch
    {
        RouteFeatureKind.Gap => CentreDistance + Opening,
        RouteFeatureKind.LaunchRamp => CentreDistance + Rise + WorldScale.RampBackFaceEase * 0.5f,
        _ => CentreDistance + Wavelength * 0.5f,
    };
    /// <summary>End of everything the feature reserves on the route.</summary>
    public float ReservedEnd => FeatureEnd + LandingDistance;
}

public enum ChallengeModuleKind { ModerateGap, LaunchRamp, BankedTurn }

/// <summary>
/// A challenge module's seven fields (04 §5E), filled by validation from the route speed model: what the
/// entrance assumes, what the geometry is, the expected speed at both speeds, required or optional,
/// the landing zone, where the Flow opportunity is taken, and the validator's verdict. Two prices
/// (D-097): the free path never stops or drops below the free-path fraction of the base cap; the paid
/// path grants Flow.
/// </summary>
public sealed class ChallengeModule
{
    public ChallengeModuleKind Kind;
    public bool Required;
    /// <summary>Route distance of the feature: the rim, the lip, the bend's start.</summary>
    public float Distance;
    public string Geometry = "";
    public string Entrance = "";
    /// <summary>The model's arrival speed at the feature, base kit and ceiling.</summary>
    public float EntrySpeed, CeilingEntrySpeed;
    public float LandingStart, LandingEnd;
    /// <summary>Free path: the model's speed at the landing zone's end.</summary>
    public float FreeExitSpeed;
    /// <summary>Paid path: the flight the module's jump makes at the entry speed (half charge for a mandatory gap, full for a ramp).</summary>
    public float PaidRange;
    public float FlowDistance;
    public bool Passed;
    public string Detail = "";
}

public sealed class RouteBend
{
    public int StartIndex, EndIndex;
    public float Radius, TurnAngle;
    /// <summary>Fastest speed the steering envelope holds through it (route speed model).</summary>
    public float CornerLimit;
}

public enum RouteLineKind { Primary, Ridge }

/// <summary>Macro route skeleton (04 §5A): a route as arcs and straights, sampled. Optional lines
/// are skeletons too, joined to the primary at two of its vertices.</summary>
public sealed class RouteSkeleton
{
    public RouteLineKind Kind = RouteLineKind.Primary;
    /// <summary>Primary-route vertex indices where an optional line leaves and rejoins.</summary>
    public int JoinStart = -1, JoinEnd = -1;
    /// <summary>Stamped corridor half-width for this line.</summary>
    public float CorridorHalfWidth = WorldScale.TypicalCorridorWidth * 0.5f;
    /// <summary>Ridge lines: extra height above the primary profile at the plateau.</summary>
    public float RidgeHeight;
    public List<RouteVertex> Vertices { get; } = new();
    public List<RouteBend> Bends { get; } = new();
    public List<RouteFeature> Features { get; } = new();
    public float Length => Vertices.Count > 0 ? Vertices[^1].Distance : 0f;
    public Vector3 Start => Vertices[0].Position;
    public Vector3 Exit => Vertices[^1].Position;

    /// <summary>Index of the vertex at or just past a route distance (binary search).</summary>
    public int IndexAtDistance(float distance)
    {
        int lo = 0, hi = Vertices.Count - 1;
        while (lo < hi) { int mid = (lo + hi) >> 1; if (Vertices[mid].Distance < distance) lo = mid + 1; else hi = mid; }
        return lo;
    }

    public Vector3[] Polyline()
    {
        var pts = new Vector3[Vertices.Count];
        for (int i = 0; i < pts.Length; i++) pts[i] = Vertices[i].Position;
        return pts;
    }
}

public sealed class ValidationCheck
{
    public string Name = "";
    public bool Passed;
    public string Detail = "";
}

/// <summary>Deterministic checks (04 §12) with the phase timings (04 §16).</summary>
public sealed class ValidationReport
{
    public List<ValidationCheck> Checks { get; } = new();
    public bool Passed => Checks.All(c => c.Passed);
    public int Attempts;
    public bool UsedFallback;
    public List<(string phase, double ms)> Timings { get; } = new();
    public double TotalMillis => Timings.Sum(t => t.ms);

    public void Add(string name, bool passed, string detail = "") =>
        Checks.Add(new ValidationCheck { Name = name, Passed = passed, Detail = detail });

    /// <summary>A figure every report carries (04 §12); printed like a check, never fails.</summary>
    public void Note(string name, string detail) => Add(name, true, detail);

    public IEnumerable<ValidationCheck> Failures => Checks.Where(c => !c.Passed);
}

/// <summary>Invisible recovery anchor on the primary progression (04 §13).</summary>
public struct Checkpoint
{
    public Vector3 Position;       // ball centre when restored
    public float Heading;          // continuation heading, radians from +X toward +Z
    public int PrimaryIndex;
    public float Distance;
}

/// <summary>
/// Pure data describing one stage (04 §3). Phase 2 fills it in slices: the skeleton and its
/// speed profile now; heightfield samples, optional lines and checkpoints as they are built.
/// </summary>
public sealed class StageDefinition
{
    public StageGenerationRequest Request { get; }
    public ulong RouteSeedUsed;
    public RouteSkeleton PrimaryRoute { get; }
    public RouteSpeedProfile SpeedProfile { get; }
    /// <summary>The same route at the Flow ceiling (04 §12, D-094): safety reads this, crossability the base profile.</summary>
    public RouteSpeedProfile? CeilingProfile { get; internal set; }
    /// <summary>Widest route distance between consecutive Flow opportunities on the primary (the chainable-line figure).</summary>
    public float WidestFlowGap { get; internal set; }
    public List<RouteSkeleton> OptionalLines { get; } = new();
    public List<RouteSpeedProfile> OptionalProfiles { get; } = new();
    /// <summary>Challenge modules on the primary (04 §5E) with their validator verdicts.</summary>
    public List<ChallengeModule> Modules { get; } = new();
    public List<Checkpoint> Checkpoints { get; } = new();
    public ValidationReport Report { get; }
    /// <summary>The one logical height source for render and collision (04 §9); null only for skeleton-only builds.</summary>
    public StageHeightField? HeightField { get; internal set; }
    public Vector3 StartPosition => PrimaryRoute.Start;
    public Vector3 StartFacing => new(Mathf.Cos(PrimaryRoute.Vertices[0].Heading), 0f, Mathf.Sin(PrimaryRoute.Vertices[0].Heading));
    public Vector3 ExitPosition => PrimaryRoute.Exit;

    public StageDefinition(StageGenerationRequest request, RouteSkeleton route, RouteSpeedProfile profile, ValidationReport report)
    {
        Request = request;
        PrimaryRoute = route;
        SpeedProfile = profile;
        Report = report;
    }

    /// <summary>Same request → same hash (08 §5). Covers the gameplay-relevant geometry only.</summary>
    public ulong Hash()
    {
        ulong h = 14695981039346656037UL ^ RouteSeedUsed;
        void Mix(float f)
        {
            uint bits = (uint)BitConverter.SingleToInt32Bits(f);
            for (int i = 0; i < 4; i++) { h ^= (bits >> (8 * i)) & 0xFF; h *= 1099511628211UL; }
        }
        foreach (var v in PrimaryRoute.Vertices) { Mix(v.Position.X); Mix(v.Position.Y); Mix(v.Position.Z); }
        foreach (var line in OptionalLines)
            foreach (var v in line.Vertices) { Mix(v.Position.X); Mix(v.Position.Y); Mix(v.Position.Z); }
        foreach (var c in Checkpoints) { Mix(c.Position.X); Mix(c.Position.Y); Mix(c.Position.Z); }
        return h;
    }
}
