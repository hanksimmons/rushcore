using Godot;

namespace Rushcore.Generation;

/// <summary>Terrain archetypes (04 §6). Phase 2 delivers Rolling Highlands; the others arrive in Phase 3.</summary>
public enum TerrainArchetype { RollingHighlands }

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

public enum RouteFeatureKind { LaunchCrest }

/// <summary>A reserved zone on the route (04 §5A): a straight long enough to host a feature and its landing.</summary>
public sealed class RouteFeature
{
    public RouteFeatureKind Kind;
    public int StartIndex, EndIndex;
    /// <summary>Route distance of the feature centre (crest apex).</summary>
    public float CentreDistance;
    public float Wavelength, Height;
    /// <summary>Filled by validation from the speed profile: straight run needed after the apex.</summary>
    public float LandingDistance;
    public bool IsLaunch;
}

public sealed class RouteBend
{
    public int StartIndex, EndIndex;
    public float Radius, TurnAngle;
    /// <summary>Fastest speed the steering envelope holds through it (route speed model).</summary>
    public float CornerLimit;
}

/// <summary>Macro route skeleton (04 §5A): the primary route as arcs and straights, sampled.</summary>
public sealed class RouteSkeleton
{
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

    public IEnumerable<ValidationCheck> Failures => Checks.Where(c => !c.Passed);
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
        return h;
    }
}
