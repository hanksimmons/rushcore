using Godot;

namespace Rushcore.Generation;

/// <summary>Terrain archetypes (04 §6). Rolling Highlands (D-085), Canyon Run (D-098) and Dune Sea (D-099); Sky Terraces follows.</summary>
public enum TerrainArchetype { RollingHighlands, CanyonRun, DuneSea, SkyTerraces }

/// <summary>
/// The dune wave of a Dune Sea stage (04 §6, D-099): one directional cosine train across the whole stage,
/// crests 0..<see cref="Height"/> above the swells, chosen by the skeleton builder so its dune trains sit on the
/// wave's own crests and the height field raises the same wave in the relief. Zero height = no dunes.
/// </summary>
public readonly record struct DuneWave(float Wavelength, float Height, float Angle, float Phase)
{
    public bool Exists => Height > 0f;
    /// <summary>Distance along the wave's travel direction.</summary>
    public float Along(float x, float z) => x * Mathf.Cos(Angle) + z * Mathf.Sin(Angle);
    /// <summary>Wave height at a point: 0 in the troughs, <see cref="Height"/> on the crests.</summary>
    public float At(float x, float z) => Exists ? 0.5f * Height * (1f + Mathf.Cos(Mathf.Tau * Along(x, z) / Wavelength + Phase)) : 0f;
}

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

public enum RouteLineKind { Primary, Ridge, Terrace }

/// <summary>
/// A spiral pit (04 §5I, §6; D-102): the primary's last section turns inward through one full turn of shrinking
/// radius, descending <see cref="Depth"/> into a conical pit cut through the canyon's side terrain, the exit pad on
/// its floor. Between two turns the cone is a cliff: falling off the inner edge lands on the turn below.
/// </summary>
public sealed class SpiralPit
{
    public Vector3 Centre;
    /// <summary>Radius of the outermost turn (the entry) and of the innermost (the exit terrace).</summary>
    public float OuterRadius, InnerRadius;
    public float Depth;
    /// <summary>Primary vertex indices of the spiral's first and last vertex.</summary>
    public int StartIndex, EndIndex;
    /// <summary>Route distance the descent begins at, and the spiral's route length.</summary>
    public float StartDistance, Length;
    /// <summary>Route distance the straight into the pit begins at: the corridor blends from the relief to the pit's entry level over it.</summary>
    public float ApproachDistance;
    /// <summary>Corridor height at the entry, the level the pit's rim floor and cone are cut from (set by the height field).</summary>
    public float EntryHeight;
}

/// <summary>
/// A lid (04 §5I, D-096; delivered D-102): a box roof over a slot section of the primary, a wall tunnel. The module
/// declares its ceiling (the roof refuses a charged jump, §10 headroom) and the camera confines under it (06 §11).
/// </summary>
public sealed class LidDefinition
{
    public int StartIndex, EndIndex;
    /// <summary>Plan centre of the roof and the straight's heading.</summary>
    public Vector3 Centre;
    public float Heading, Length, Width, Thickness;
    /// <summary>Height of the roof's underside; the clearance above the corridor under it is at least the family's.</summary>
    public float RoofBottom;
    /// <summary>Smallest clearance between the corridor and the roof along the tunnel.</summary>
    public float Clearance;
    public bool Passed;
    public string Detail = "";
    /// <summary>True when a plan position lies under the roof.</summary>
    public bool Covers(float x, float z)
    {
        float dx = x - Centre.X, dz = z - Centre.Z;
        float along = dx * Mathf.Cos(Heading) + dz * Mathf.Sin(Heading);
        float across = -dx * Mathf.Sin(Heading) + dz * Mathf.Cos(Heading);
        return Mathf.Abs(along) <= Length * 0.5f && Mathf.Abs(across) <= Width * 0.5f;
    }
}

public enum MouthKind { Ground, Edge, Midair }

/// <summary>
/// A see-through tube (04 §5I, D-096; delivered D-101): a circle of <see cref="Radius"/> swept along a 3D axis that
/// leaves a line through an entry mouth and rejoins it (or another line) through a flared exit mouth onto a landing
/// zone. The ball inside is carried by the walls (no bend loss in the route speed model); the shell is a structure
/// collider, never a second height layer. Axis points are spaced <see cref="WorldScale.RouteSampleSpacing"/> apart.
/// </summary>
public sealed class TubeDefinition
{
    public Vector3[] Axis = System.Array.Empty<Vector3>();
    public float Radius;
    public MouthKind EntryKind = MouthKind.Ground;
    /// <summary>Primary-route vertex indices the tube leaves from and lands back at.</summary>
    public int JoinStart, JoinEnd;
    /// <summary>+1 left of the line, −1 right (the side the tube swings out to).</summary>
    public float Side;
    /// <summary>Height of the cruise above the corridor at the mouths.</summary>
    public float CruiseHeight;
    public float Length;
    /// <summary>The carried profiles: base kit and ceiling, from the join's arrival speed.</summary>
    public RouteSpeedProfile? Profile, CeilingProfile;
    /// <summary>Steepest wall ride the base kit's speed asks of the tube's tightest turn (tan φ = v²κ / g), degrees.</summary>
    public float MaxRideDegrees;
    public bool Passed;
    public string Detail = "";
    /// <summary>Tangent of the axis at an axis index.</summary>
    public Vector3 TangentAt(int i)
    {
        int a = Mathf.Max(0, i - 1), b = Mathf.Min(Axis.Length - 1, i + 1);
        Vector3 t = Axis[b] - Axis[a];
        return t.LengthSquared() > 1e-8f ? t.Normalized() : Vector3.Forward;
    }
    /// <summary>Nearest axis point to a world position (squared distance and index), linear over the axis.</summary>
    public int Nearest(Vector3 p, out float distance)
    {
        int best = 0; float bestD = float.MaxValue;
        for (int i = 0; i < Axis.Length; i++)
        {
            float d = Axis[i].DistanceSquaredTo(p);
            if (d < bestD) { bestD = d; best = i; }
        }
        distance = Mathf.Sqrt(bestD);
        return best;
    }
    public Aabb Bounds { get; internal set; }
}

/// <summary>Macro route skeleton (04 §5A): a route as arcs and straights, sampled. Optional lines
/// are skeletons too, joined to the primary at two of its vertices.</summary>
public sealed class RouteSkeleton
{
    public RouteLineKind Kind = RouteLineKind.Primary;
    /// <summary>Primary-route vertex indices where an optional line leaves and rejoins.</summary>
    public int JoinStart = -1, JoinEnd = -1;
    /// <summary>Stamped corridor half-width for this line.</summary>
    public float CorridorHalfWidth = WorldScale.TypicalCorridorWidth * 0.5f;
    /// <summary>Ridge and terrace lines: extra height above the primary profile at the plateau.</summary>
    public float RidgeHeight;
    /// <summary>Offset lines (D-100, D-103): lateral offset from the primary, the S-transition length at each end, and the
    /// climb / descent ramp length just inside the transitions.</summary>
    public float Offset = WorldScale.RidgeOffset, Transition = WorldScale.RidgeTransition, RampLength = WorldScale.RidgeRampLength;
    /// <summary>Sky Terraces (D-103): the floor this line is (1 = the primary's floor, 2 and 3 the terraces above it).</summary>
    public int Floor = 1;
    /// <summary>Which side of the primary the line lies on (+1 its left, −1 its right); the primary is toward −Side.</summary>
    public float Side = 1f;
    /// <summary>Offset lines: the stamp's falloff toward the primary (−Side) and away from it; 0 = the archetype's (D-103).</summary>
    public float InnerFalloff, OuterFalloff;
    /// <summary>Primary route of a Dune Sea stage: the wave its dune trains ride (D-099).</summary>
    public DuneWave Dunes;
    /// <summary>Canyon Run's set-piece (04 §6, D-102): the spiral pit the route descends into at its end; null when the stage has none.</summary>
    public SpiralPit? Spiral;
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
    /// <summary>Each optional line at the Flow ceiling, chained from its join (D-100).</summary>
    public List<RouteSpeedProfile> OptionalCeilingProfiles { get; } = new();
    /// <summary>Optional lines the generator dropped because their base-kit flight could not hold a corner (D-100).</summary>
    public int DroppedLines { get; internal set; }
    public string DroppedDetail { get; internal set; } = "";
    /// <summary>Challenge modules on the primary (04 §5E) with their validator verdicts.</summary>
    public List<ChallengeModule> Modules { get; } = new();
    /// <summary>See-through tubes (04 §5I, D-101): the line graph's branches through the air.</summary>
    public List<TubeDefinition> Tubes { get; } = new();
    /// <summary>Lids (04 §5I, D-102): wall tunnels over slot sections.</summary>
    public List<LidDefinition> Lids { get; } = new();
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
        foreach (var t in Tubes)
            foreach (var a in t.Axis) { Mix(a.X); Mix(a.Y); Mix(a.Z); }
        foreach (var l in Lids) { Mix(l.Centre.X); Mix(l.Centre.Z); Mix(l.RoofBottom); Mix(l.Length); }
        return h;
    }
}
