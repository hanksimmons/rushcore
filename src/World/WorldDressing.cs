using Godot;
using Rushcore.Generation;
using Rushcore.Player;
using Rushcore.Tuning;

namespace Rushcore.World;

/// <summary>
/// Lighting, environment, terrain material, scale markers, props and boost pickups
/// for the Movement Toy (06 §16). Owns nothing physics-critical.
///
/// <para>The instruments (calibration-lane distance markers, known-height pillars and the
/// colour-coded feature pylons that label every measurable terrain feature) are what turn
/// the toy world into a scale-calibration rig for Gate M1. Scattered rocks/crystals/pylons
/// are scenery and scale reference only, and are the part that Prop Density scales.</para>
/// </summary>
public partial class WorldDressing : Node3D
{
    private const float PickupRespawnSeconds = 5f;

    private readonly GameplayTuning _t;
    private readonly MovementToyWorld _world;
    private Node3D _content = null!;

    // Shared resources: created once, reused by every instance and every rebuild.
    private Material? _terrainMaterial;
    private CylinderMesh _postMesh = null!;
    private BoxMesh _barMesh = null!;
    private CylinderMesh _pillarSegmentMesh = null!;
    private SphereMesh _capMesh = null!;
    private SphereMesh _rockMesh = null!;
    private CylinderMesh _crystalMesh = null!;
    private CylinderMesh _pylonMesh = null!;
    private TorusMesh _ringMesh = null!;
    private SphereMesh _coreMesh = null!;
    private StandardMaterial3D _matPost = null!;
    private StandardMaterial3D _matGantry = null!;
    private StandardMaterial3D _matBandLight = null!;
    private StandardMaterial3D _matBandDark = null!;
    private StandardMaterial3D _matInstanced = null!;
    private StandardMaterial3D _matPickup = null!;

    private readonly List<BoostRing> _pickups = new();
    /// <summary>The EXIT pad signs in exit order, and the hidden flash ring standing on each pad (T1).</summary>
    private readonly List<Label3D> _exitSigns = new();
    private readonly List<MeshInstance3D> _exitFlashes = new();
    private int _pulseExit = -1;
    private float _pulseLeft;
    /// <summary>One static body holds every prop/marker collider; rebuilt with the content.</summary>
    private StaticBody3D _colliders = null!;
    private float _clock;

    public WorldDressing(GameplayTuning tuning, MovementToyWorld world)
    {
        _t = tuning;
        _world = world;
    }

    public event Action<float>? BoostPickupCollected;

    public StandardMaterial3D TubeShellMaterial { get; private set; } = null!;
    public StandardMaterial3D TubeRibMaterial { get; private set; } = null!;
    public StandardMaterial3D LidMaterial { get; private set; } = null!;

    protected void RaiseBoostPickup(float amount) => BoostPickupCollected?.Invoke(amount);

    public override void _Ready()
    {
        Name = "WorldDressing";
        BuildSharedResources();
        BuildLightingAndEnvironment();

        _content = new Node3D { Name = "Content" };
        AddChild(_content);
    }

    // ---------------------------------------------------------------- lighting

    private void BuildLightingAndEnvironment()
    {
        AddChild(new DirectionalLight3D
        {
            Name = "SunLight",
            Rotation = new Vector3(Mathf.DegToRad(-46f), Mathf.DegToRad(-125f), 0f),
            LightEnergy = 1.15f,
            LightColor = new Color(1.0f, 0.95f, 0.86f),
            LightSpecular = 0.1f,
            ShadowEnabled = true,
            DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel4Splits,
            DirectionalShadowMaxDistance = 450f,
            ShadowBias = 0.06f,
            ShadowNormalBias = 1.5f,
        });

        var sky = new ProceduralSkyMaterial
        {
            SkyTopColor = new Color(0.16f, 0.30f, 0.53f),
            SkyHorizonColor = new Color(0.66f, 0.74f, 0.79f),
            SkyCurve = 0.14f,
            GroundBottomColor = new Color(0.15f, 0.16f, 0.19f),
            GroundHorizonColor = new Color(0.55f, 0.58f, 0.60f),
            SunAngleMax = 14f,
            SunCurve = 0.08f,
        };

        // Fog exists for aerial depth only: it must never hide terrain the player has to
        // read at 60 m/s, so it starts well beyond the useful sightline (06 §2).
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky { SkyMaterial = sky },
            AmbientLightSource = Godot.Environment.AmbientSource.Sky,
            AmbientLightSkyContribution = 0.7f,
            AmbientLightEnergy = 1.0f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
            TonemapWhite = 6f,
            FogEnabled = true,
            FogMode = Godot.Environment.FogModeEnum.Depth,
            FogLightColor = new Color(0.60f, 0.68f, 0.76f),
            FogDepthBegin = 380f,
            FogDepthEnd = 2400f,
            FogDepthCurve = 1.6f,
            FogDensity = 0.8f,
            FogSkyAffect = 0.35f,
            // Player emissive cues (charge buildup, Overdrive band, the slam-landing and
            // burst flashes) drive emission above 1.0; without HDR glow they would simply
            // clip to white and lose the readability those cues exist for (06 §6/§7).
            GlowEnabled = true,
            GlowIntensity = 0.7f,
            GlowStrength = 1.0f,
            GlowBloom = 0.05f,
            GlowBlendMode = Godot.Environment.GlowBlendModeEnum.Softlight,
            GlowHdrThreshold = 1.0f,
            GlowHdrScale = 2.0f,
        };
        env.SetGlowLevel(3, 1.0f);
        env.SetGlowLevel(4, 0.6f);
        env.SetGlowLevel(5, 0.3f);
        _env = env;
        ApplyFog();
        AddChild(new WorldEnvironment { Name = "WorldEnvironment", Environment = env });
    }

    private Godot.Environment _env = null!;

    /// <summary>World › Fog End is a live sightline instrument (M1); begin tracks it at 16%.</summary>
    private void ApplyFog()
    {
        float end = Mathf.Max(300f, _t.World.FogEnd);
        if (Mathf.IsEqualApprox(_env.FogDepthEnd, end)) return;
        _env.FogDepthEnd = end;
        _env.FogDepthBegin = end * 0.16f;
    }

    /// <summary>
    /// Terrain material. The mesh carries per-facet flat normals and per-vertex colours and
    /// has no UVs or tangents, so albedo comes entirely from vertex colour; lambert diffuse
    /// with specular disabled keeps the faceted low-poly read clean at speed (06 §4).
    /// </summary>
    public Material CreateTerrainMaterial() => _terrainMaterial ??= new StandardMaterial3D
    {
        VertexColorUseAsAlbedo = true,
        DiffuseMode = BaseMaterial3D.DiffuseModeEnum.Lambert,
        SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled,
        Roughness = 1f,
        Metallic = 0f,
        TextureFilter = BaseMaterial3D.TextureFilterEnum.NearestWithMipmaps,
    };

    // ---------------------------------------------------------------- resources

    private void BuildSharedResources()
    {
        // Every mesh is unit-sized on its varying axis and scaled per instance so a single
        // resource serves every marker, prop and pickup in the world.
        _postMesh = new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.7f, Height = 1f, RadialSegments = 6, Rings = 1 };
        _barMesh = new BoxMesh { Size = Vector3.One };
        _pillarSegmentMesh = new CylinderMesh { TopRadius = 2.2f, BottomRadius = 2.2f, Height = 5f, RadialSegments = 8, Rings = 1 };
        _capMesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 8, Rings = 4 };
        _rockMesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 6, Rings = 3 };
        _crystalMesh = new CylinderMesh { TopRadius = 0.04f, BottomRadius = 1f, Height = 1f, RadialSegments = 5, Rings = 1 };
        _pylonMesh = new CylinderMesh { TopRadius = 0.3f, BottomRadius = 0.85f, Height = 1f, RadialSegments = 4, Rings = 1 };
        _ringMesh = new TorusMesh { InnerRadius = 2.4f, OuterRadius = 3.1f, Rings = 14, RingSegments = 5 };
        _coreMesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 4, Rings = 2 };

        _matPost = Flat(new Color(0.84f, 0.84f, 0.78f));
        _matGantry = Glow(new Color(0.20f, 0.78f, 0.86f), 0.7f);
        _matBandLight = Flat(new Color(0.90f, 0.89f, 0.83f));
        _matBandDark = Flat(new Color(0.72f, 0.20f, 0.18f));
        _matPickup = Glow(new Color(0.30f, 0.90f, 0.72f), 1.1f);

        _matInstanced = Flat(Colors.White);
        _matInstanced.VertexColorUseAsAlbedo = true;

        // Tube shell (06 §3, D-096): a translucent skin seen from outside, opaque ribs as the motion cue at speed.
        TubeShellMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.55f, 0.85f, 1.0f, 0.22f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Back,
            DiffuseMode = BaseMaterial3D.DiffuseModeEnum.Lambert,
            SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled,
            Roughness = 0.4f,
            EmissionEnabled = true,
            Emission = new Color(0.25f, 0.5f, 0.7f),
            EmissionEnergyMultiplier = 0.25f,
        };
        TubeRibMaterial = Glow(new Color(0.85f, 0.95f, 1.0f), 0.5f);
        LidMaterial = Flat(new Color(0.48f, 0.30f, 0.22f));
    }

    private static StandardMaterial3D Flat(Color albedo) => new()
    {
        AlbedoColor = albedo,
        DiffuseMode = BaseMaterial3D.DiffuseModeEnum.Lambert,
        SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled,
        Roughness = 1f,
        Metallic = 0f,
    };

    private static StandardMaterial3D Glow(Color albedo, float energy)
    {
        var m = Flat(albedo);
        m.EmissionEnabled = true;
        m.Emission = albedo;
        m.EmissionEnergyMultiplier = energy;
        return m;
    }

    // ---------------------------------------------------------------- rebuild

    /// <summary>Rebuilds every non-terrain object. Called after each terrain build.</summary>
    public virtual void Rebuild()
    {
        foreach (Node child in _content.GetChildren())
        {
            _content.RemoveChild(child);
            child.QueueFree();
        }
        _pickups.Clear();
        _exitSigns.Clear();
        _exitFlashes.Clear();
        _pulseExit = -1;

        _colliders = new StaticBody3D
        {
            Name = "PropColliders",
            PhysicsMaterialOverride = new PhysicsMaterial { Friction = PlayerPhysics.ArcadeSurfaceFriction, Bounce = 0f },
        };
        _content.AddChild(_colliders);

        if (_world.IsStage)
        {
            BuildStageDressing();
            return;
        }
        if (_world.IsStrip)
        {
            BuildStripDressing();
            return;
        }

        BuildLaneMarkers();
        BuildPillars(TerrainHeightField.LaneStartX - 10f, TerrainHeightField.LaneZ + 62f);
        BuildFeaturePylons();
        BuildScatteredProps(IsClearOfInstruments, 500f, 500f, 1f, PlacementChance);
        BuildBoostPickups();
    }

    // ---------------------------------------------------------------- generated stage (Phase 2)

    /// <summary>Start/exit marks, the route debug view (04 §16), corridor-clear scatter, boost rings on the line.</summary>
    private void BuildStageDressing()
    {
        var stage = _world.Stage;
        if (stage is null || stage.HeightField is null) return;
        var route = stage.PrimaryRoute;
        var field = stage.HeightField;

        AddSign(_world.SurfacePoint(route.Start.X, route.Start.Z, 26f), "START", 10f);
        // Exits (02 §4, D-105): every pad is named, and a terminal line's fork carries the name at the top of its S.
        // The sign and one hidden ring per pad are what the completion pulse animates (T1, 06 §10 "exit").
        foreach (var e in stage.Exits)
        {
            _exitSigns.Add(AddSign(_world.SurfacePoint(e.Position.X, e.Position.Z, 26f), $"EXIT {e.Label}", 10f));
            var flash = new MeshInstance3D
            {
                Mesh = _ringMesh,
                MaterialOverride = _matPickup,
                Position = _world.SurfacePoint(e.Position.X, e.Position.Z, 3f),
                Scale = Vector3.One * WorldScale.PadRadius,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Visible = false,
            };
            _content.AddChild(flash);
            _exitFlashes.Add(flash);
        }
        foreach (var e in stage.Exits)
        {
            if (e.IsPrimary) continue;
            var line = stage.OptionalLines[e.LineIndex];
            var fork = line.Vertices[Mathf.Min(line.Vertices.Count - 1, line.IndexAtDistance(line.Transition))];
            AddSign(fork.Position + Vector3.Up * 30f, $"EXIT {e.Label} ↑", 8f);
        }
        foreach (var f in route.Features)
        {
            var c = route.Vertices[route.IndexAtDistance(f.CentreDistance)];
            string label = f.Kind switch
            {
                RouteFeatureKind.Gap => $"GAP {f.Opening:0} m",
                RouteFeatureKind.LaunchRamp => $"RAMP {Mathf.RadToDeg(Mathf.Atan(f.Slope)):0}°",
                _ => f.IsLaunch ? "CREST ▲" : "crest",
            };
            AddSign(_world.SurfacePoint(c.Position.X, c.Position.Z, 30f), label, 7f);
        }
        // Scale pillars are solid: they stand just outside the corridor, never inside it (04 §5G).
        BuildPillars(route.Start.X + 30f, route.Start.Z + StageHeightField.CorridorHalfWidth + 12f);

        foreach (var tube in stage.Tubes)
            AddSign(tube.Axis[0] + Vector3.Up * (tube.Radius * 2f + 8f), tube.Passed ? "TUBE" : "TUBE ✗", 7f);
        foreach (var lid in stage.Lids)
        {
            var e = route.Vertices[lid.StartIndex];
            AddSign(_world.SurfacePoint(e.Position.X, e.Position.Z, lid.RoofBottom - e.Position.Y - 4f), "TUNNEL", 6f);
        }
        foreach (var line in stage.OptionalLines)
        {
            if (line.Kind != RouteLineKind.Terrace) continue;
            var c = line.Vertices[Mathf.Min(line.Vertices.Count - 1, line.Vertices.Count / 6)];
            AddSign(c.Position + Vector3.Up * 30f, $"FLOOR {line.Floor} ↑", 8f);
        }
        if (stage.HeightField is { } hf && hf.Rules.Floors >= 2)
        {
            // The cloud band (06 §3, D-103): two translucent sheets above the primary's mean height; the top floor sits in it.
            float mean = route.Vertices.Average(x => x.Position.Y);
            for (int i = 0; i < 2; i++)
                _content.AddChild(new MeshInstance3D
                {
                    Name = $"CloudBand{i}",
                    Mesh = new PlaneMesh { Size = new Vector2(_world.HalfX * 2.2f, _world.HalfZ * 2.2f) },
                    Position = new Vector3(0f, mean + WorldScale.CloudBandHeight + i * 30f, 0f),
                    MaterialOverride = new StandardMaterial3D
                    {
                        AlbedoColor = new Color(0.95f, 0.96f, 0.98f, i == 0 ? 0.55f : 0.35f),
                        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                    },
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                });
        }
        if (stage.PrimaryRoute.Spiral is { } pit)
            AddSign(_world.SurfacePoint(route.Vertices[pit.StartIndex].Position.X, route.Vertices[pit.StartIndex].Position.Z, 30f), "PIT ↓", 8f);

        if (_t.World.RouteDebugLines)
        {
            BuildRouteLines(route);
            foreach (var line in stage.OptionalLines) BuildRouteLines(line, line.Terminal ? ExitLineColor : line.Floor == 3 ? FloorThreeColor : line.Floor == 2 ? FloorTwoColor : RouteOptionalColor);
            foreach (var cp in stage.Checkpoints) AddCheckpointPost(cp.Position);
            foreach (var tube in stage.Tubes) BuildPolyline(tube.Axis, RouteTubeColor, 0f);
        }
        if (_t.World.StageDebugViews) BuildStageDebugViews(stage);

        // Cosmetic scatter never enters the corridor (04 §5G): keep clear of the stamp and its falloff.
        float clearance = StageHeightField.CorridorHalfWidth + StageHeightField.BendExtraHalfWidth + StageHeightField.FalloffWidth * 0.5f;
        float area = (_world.HalfX * _world.HalfZ) / (MovementToyWorld.Extent * MovementToyWorld.Extent * 0.25f);
        BuildScatteredProps(
            (x, z) => _world.InBounds(x, z, 40f) && field.DistanceToRoute(x, z) > clearance,
            _world.HalfX - 50f, _world.HalfZ - 50f, area, (_, _) => 1f);

        // Boost rings on the line every ~1.2 km so the toy's boost loop stays exercised.
        for (float d = 900f; d < route.Length - 400f; d += 1200f)
        {
            var v = route.Vertices[route.IndexAtDistance(d)];
            AddPickup(v.Position.X, v.Position.Z, new Vector3(Mathf.Cos(v.Heading), 0f, Mathf.Sin(v.Heading)));
        }
    }

    private static readonly Color RouteStraightColor = new(0.35f, 0.95f, 1.0f);
    private static readonly Color RouteBendColor = new(1.0f, 0.62f, 0.2f);
    private static readonly Color RouteCrestColor = new(1.0f, 0.3f, 0.85f);
    private static readonly Color RouteOptionalColor = new(0.45f, 1.0f, 0.4f);
    private static readonly Color RouteTubeColor = new(0.85f, 0.45f, 1.0f);
    private static readonly Color FloorTwoColor = new(0.55f, 1.0f, 0.75f);
    private static readonly Color FloorThreeColor = new(1.0f, 0.85f, 0.35f);
    private static readonly Color CorridorColor = new(0.35f, 0.65f, 0.75f);
    private static readonly Color ChallengeColor = new(1.0f, 0.95f, 0.25f);
    private static readonly Color StructureColor = new(1.0f, 0.55f, 0.85f);
    private static readonly Color DrainColor = new(0.95f, 0.45f, 0.35f);
    private static readonly Color ExitLineColor = new(1.0f, 0.75f, 0.55f);

    /// <summary>A collider-free post: debug markers may stand inside a corridor.</summary>
    private void AddMarker(Vector3 at, float height, Material material)
    {
        Vector3 basePos = _world.SurfacePoint(at.X, at.Z);
        _content.AddChild(new MeshInstance3D { Mesh = _postMesh, MaterialOverride = material, Position = basePos + Vector3.Up * height * 0.5f, Scale = new Vector3(1.2f, height, 1.2f) });
    }

    /// <summary>
    /// Stage debug views (04 §16, 07 §11; D-104): corridor bounds on every line (its level width, wider on bends),
    /// challenge zones (a yellow line over each module's straight from its entrance to the end of its landing zone,
    /// posts at both ends), structure bounds (lid box outlines, tube mouth rings), floors (terrace bounds in the
    /// floor's colour) and drains (a red line along a terrace's cliff foot on the floor below). Off by default.
    /// </summary>
    private void BuildStageDebugViews(StageDefinition stage)
    {
        var route = stage.PrimaryRoute;
        var v = route.Vertices;
        var bendAt = new bool[v.Count];
        foreach (var b in route.Bends) for (int i = b.StartIndex; i <= b.EndIndex; i++) bendAt[i] = true;
        // Corridor bounds: the primary's level width, plus the bend extra; optional lines at their own width.
        foreach (float side in new[] { 1f, -1f })
        {
            var pts = new Vector3[v.Count];
            for (int i = 0; i < v.Count; i++)
            {
                float w = StageHeightField.CorridorHalfWidth + (bendAt[i] ? StageHeightField.BendExtraHalfWidth : 0f);
                float x = v[i].Position.X - Mathf.Sin(v[i].Heading) * w * side, z = v[i].Position.Z + Mathf.Cos(v[i].Heading) * w * side;
                pts[i] = _world.SurfacePoint(x, z, 1.5f);
            }
            BuildPolyline(pts, CorridorColor, 0f);
        }
        foreach (var line in stage.OptionalLines)
        {
            var lv = line.Vertices;
            Color c = line.Floor == 3 ? FloorThreeColor : line.Floor == 2 ? FloorTwoColor : CorridorColor;
            foreach (float side in new[] { 1f, -1f })
            {
                var pts = new Vector3[lv.Count];
                for (int i = 0; i < lv.Count; i++)
                    pts[i] = _world.SurfacePoint(lv[i].Position.X - Mathf.Sin(lv[i].Heading) * line.CorridorHalfWidth * side, lv[i].Position.Z + Mathf.Cos(lv[i].Heading) * line.CorridorHalfWidth * side, 1.5f);
                BuildPolyline(pts, c, 0f);
            }
            if (line.Kind == RouteLineKind.Terrace)
            {
                // The drain: the cliff foot on the floor below, along the plateau.
                float foot = line.CorridorHalfWidth + WorldScale.WallSetback + WorldScale.TerraceCliffFalloff + 6f;
                var pts = new List<Vector3>();
                for (int i = 0; i < lv.Count; i += 4)
                {
                    float d = lv[i].Distance;
                    if (d < line.Transition + line.RampLength || d > line.Length - line.Transition - line.RampLength) continue;
                    float inward = -line.Side;
                    pts.Add(_world.SurfacePoint(lv[i].Position.X - Mathf.Sin(lv[i].Heading) * foot * inward, lv[i].Position.Z + Mathf.Cos(lv[i].Heading) * foot * inward, 1.5f));
                }
                if (pts.Count > 1) BuildPolyline(pts.ToArray(), DrainColor, 0f);
            }
        }
        // Exit pads (D-105): a ring at the pad radius around every exit.
        foreach (var e in stage.Exits)
        {
            var ring = new Vector3[49];
            for (int i = 0; i < ring.Length; i++)
            {
                float ang = Mathf.Tau * i / (ring.Length - 1);
                ring[i] = _world.SurfacePoint(e.Position.X + Mathf.Cos(ang) * WorldScale.PadRadius, e.Position.Z + Mathf.Sin(ang) * WorldScale.PadRadius, 1.5f);
            }
            BuildPolyline(ring, ExitLineColor, 0f);
        }
        // Challenge zones.
        foreach (var f in route.Features)
        {
            if (f.Kind == RouteFeatureKind.LaunchCrest) continue;
            float start = f.Kind == RouteFeatureKind.Gap ? f.CentreDistance - WorldScale.TakeoffRunwayLength : f.CentreDistance - WorldScale.RampApproachLength - f.Rise / Mathf.Max(0.01f, f.Slope);
            float end = f.FeatureEnd + WorldScale.LandingZoneLength;
            var pts = new List<Vector3>();
            for (int i = route.IndexAtDistance(start); i < v.Count && v[i].Distance <= end; i += 2) pts.Add(v[i].Position + Vector3.Up * 6f);
            if (pts.Count > 1) BuildPolyline(pts.ToArray(), ChallengeColor, 0f);
            // Markers only: a debug post inside the corridor must never be a collider (04 §5G, 08 §5).
            AddMarker(v[route.IndexAtDistance(start)].Position, 10f, _matBandLight);
            AddMarker(v[Mathf.Min(v.Count - 1, route.IndexAtDistance(end))].Position, 10f, _matBandDark);
        }
        foreach (var b in route.Bends)
        {
            if (b.Radius > WorldScale.CommittedBendRadius + 1e-3f) continue;
            var pts = new List<Vector3>();
            for (int i = Mathf.Max(0, b.StartIndex - 25); i <= b.EndIndex; i += 2) pts.Add(v[i].Position + Vector3.Up * 6f);
            BuildPolyline(pts.ToArray(), ChallengeColor, 0f);
        }
        // Structure bounds.
        foreach (var lid in stage.Lids)
        {
            Vector3 a = new(Mathf.Cos(lid.Heading), 0f, Mathf.Sin(lid.Heading)), s = new(-Mathf.Sin(lid.Heading), 0f, Mathf.Cos(lid.Heading));
            Vector3 c = new(lid.Centre.X, lid.RoofBottom, lid.Centre.Z);
            float hl = lid.Length * 0.5f, hw = lid.Width * 0.5f;
            foreach (float y in new[] { 0f, lid.Thickness })
            {
                Vector3 up = Vector3.Up * y;
                BuildPolyline(new[] { c + a * hl + s * hw + up, c - a * hl + s * hw + up, c - a * hl - s * hw + up, c + a * hl - s * hw + up, c + a * hl + s * hw + up }, StructureColor, 0f);
            }
        }
        foreach (var tube in stage.Tubes)
        {
            foreach (int end in new[] { 0, tube.Axis.Length - 1 })
            {
                Vector3 t = tube.TangentAt(end);
                Vector3 n = Mathf.Abs(t.Y) < 0.9f ? Vector3.Up - t * t.Y : Vector3.Right;
                n = n.Normalized();
                Vector3 bnorm = t.Cross(n).Normalized();
                float r = tube.Radius * WorldScale.TubeMouthFlare;
                var ring = new Vector3[25];
                for (int i = 0; i < 25; i++) ring[i] = tube.Axis[end] + (n * Mathf.Cos(Mathf.Tau * i / 24f) + bnorm * Mathf.Sin(Mathf.Tau * i / 24f)) * r;
                BuildPolyline(ring, StructureColor, 0f);
            }
        }
        if (route.Spiral is { } pit)
        {
            var ring = new Vector3[49];
            for (int i = 0; i < 49; i++)
            {
                float ang = Mathf.Tau * i / 48f;
                float x = pit.Centre.X + Mathf.Cos(ang) * pit.OuterRadius, z = pit.Centre.Z + Mathf.Sin(ang) * pit.OuterRadius;
                ring[i] = _world.SurfacePoint(x, z, 3f);
            }
            BuildPolyline(ring, StructureColor, 0f);
        }
    }

    /// <summary>Unshaded line strip through world points, lifted by <paramref name="lift"/>.</summary>
    private void BuildPolyline(Vector3[] points, Color color, float lift)
    {
        var mesh = new ImmediateMesh();
        mesh.SurfaceBegin(Mesh.PrimitiveType.LineStrip);
        foreach (var p in points) { mesh.SurfaceSetColor(color); mesh.SurfaceAddVertex(p + Vector3.Up * lift); }
        mesh.SurfaceEnd();
        _content.AddChild(new MeshInstance3D
        {
            Name = "StructureLine", Mesh = mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true },
        });
    }

    /// <summary>Unshaded line strip 3 m above the route: cyan straights, orange bends, magenta crests.</summary>
    private void BuildRouteLines(RouteSkeleton route, Color? fixedColor = null)
    {
        var mesh = new ImmediateMesh();
        mesh.SurfaceBegin(Mesh.PrimitiveType.LineStrip);
        var crestRanges = route.Features.Select(f => (f.StartIndex, f.EndIndex)).ToArray();
        for (int i = 0; i < route.Vertices.Count; i++)
        {
            var v = route.Vertices[i];
            bool crest = false;
            foreach (var (s, e) in crestRanges) if (i >= s && i <= e) { crest = true; break; }
            mesh.SurfaceSetColor(fixedColor ?? (crest ? RouteCrestColor : v.Kind == RouteSegmentKind.Bend ? RouteBendColor : RouteStraightColor));
            mesh.SurfaceAddVertex(_world.SurfacePoint(v.Position.X, v.Position.Z, 3f));
        }
        mesh.SurfaceEnd();
        _content.AddChild(new MeshInstance3D
        {
            Name = "RouteDebugLine",
            Mesh = mesh,
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                VertexColorUseAsAlbedo = true,
                NoDepthTest = false,
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
    }

    /// <summary>A recovery anchor: a short post with a cap, so progression checkpoints can be seen in the debug view.</summary>
    private void AddCheckpointPost(Vector3 anchor)
    {
        Vector3 basePos = _world.SurfacePoint(anchor.X, anchor.Z);
        _content.AddChild(new MeshInstance3D
        {
            Mesh = _postMesh,
            MaterialOverride = _matGantry,
            Position = basePos + Vector3.Up * 4f,
            Scale = new Vector3(1.2f, 4f, 1.2f),
        });
        _content.AddChild(new MeshInstance3D
        {
            Mesh = _capMesh,
            MaterialOverride = _matPickup,
            Position = basePos + Vector3.Up * 8.6f,
            Scale = Vector3.One * 1.6f,
        });
    }

    // ---------------------------------------------------------------- scale strip (Gate M1)

    /// <summary>Rulers, signs and markers for the 6.4 km scale strip. Instruments, not scenery.</summary>
    private void BuildStripDressing()
    {
        // Distance rulers the whole length: 100 m posts, 500 m gantries, every post labelled.
        float offset = ScaleStripHeightField.LaneHalfWidth + 14f;
        for (float sd = 0f; sd <= ScaleStripHeightField.PadEnd + 0.5f; sd += 100f)
        {
            float x = ScaleStripHeightField.X(sd);
            bool five = Mathf.PosMod(sd, 500f) < 1f;
            float height = five ? 12f : 6f;
            var mat = five ? _matGantry : _matPost;
            AddPost(x, -offset, height, mat);
            AddPost(x, offset, height, mat);
            float y = Mathf.Max(_world.SampleHeight(x, -offset), _world.SampleHeight(x, offset)) + height + 0.6f;
            if (five)
            {
                _content.AddChild(new MeshInstance3D
                {
                    Mesh = _barMesh,
                    MaterialOverride = _matGantry,
                    Position = new Vector3(x, y, 0f),
                    Scale = new Vector3(1.6f, 1.4f, offset * 2f),
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
                });
            }
            AddSign(new Vector3(x, y + (five ? 7f : 3.5f), 0f), $"{sd:0} m", five ? 9f : 4.5f);
        }

        // Station signs: what is being measured, at its stated size.
        AddStationSign(0f, "RUNWAY  0->cap, brake, burst");
        float seg = (ScaleStripHeightField.CorridorEnd - ScaleStripHeightField.RunwayEnd) / ScaleStripHeightField.CorridorWidths.Length;
        for (int i = 0; i < ScaleStripHeightField.CorridorWidths.Length; i++)
            AddStationSign(ScaleStripHeightField.RunwayEnd + i * seg, $"CORRIDOR {ScaleStripHeightField.CorridorWidths[i]:0} m");
        float s0 = ScaleStripHeightField.CorridorEnd;
        for (int i = 0; i < ScaleStripHeightField.HillWavelengths.Length; i++)
        {
            AddStationSign(s0, $"HILLS  wavelength {ScaleStripHeightField.HillWavelengths[i]:0} m  height {ScaleStripHeightField.HillHeights[i]:0} m");
            s0 += ScaleStripHeightField.HillStationLengths[i];
        }
        for (int i = 0; i < ScaleStripHeightField.GapRimS.Length; i++)
            AddStationSign(ScaleStripHeightField.GapRimS[i] - 70f, $"GAP {ScaleStripHeightField.GapOpenings[i]:0} m");
        for (int i = 0; i < ScaleStripHeightField.RampLaneZ.Length; i++)
        {
            float deg = Mathf.RadToDeg(Mathf.Atan(ScaleStripHeightField.RampSlopes[i]));
            AddSign(_world.SurfacePoint(ScaleStripHeightField.X(ScaleStripHeightField.RampsStart - 90f), ScaleStripHeightField.RampLaneZ[i], 14f),
                $"RAMP {deg:0} deg  lip {ScaleStripHeightField.RampRise:0} m", 6f);
        }
        AddStationSign(ScaleStripHeightField.RampsEnd, "TURN PAD  rings r 80 / 160 / 240 m");

        BuildPillars(ScaleStripHeightField.StartX - 20f, 90f);

        // Pylons: gap take-off rims, ramp lips, corridor wall starts.
        var pylons = new List<Transform3D>();
        var colors = new List<Color>();
        var warn = new Color(0.95f, 0.32f, 0.22f);
        var lipColor = new Color(0.98f, 0.82f, 0.30f);
        var wallColor = new Color(0.30f, 0.80f, 0.90f);
        for (int i = 0; i < ScaleStripHeightField.GapRimS.Length; i++)
            for (float z = -ScaleStripHeightField.LaneHalfWidth - 60f; z <= ScaleStripHeightField.LaneHalfWidth + 60f; z += 30f)
                AddPylon(pylons, colors, ScaleStripHeightField.X(ScaleStripHeightField.GapRimS[i] - 8f), z, 9f, 1.2f, warn);
        for (int i = 0; i < ScaleStripHeightField.RampLaneZ.Length; i++)
            for (int sgn = -1; sgn <= 1; sgn += 2)
                AddPylon(pylons, colors, ScaleStripHeightField.X(ScaleStripHeightField.RampLipS(i)),
                    ScaleStripHeightField.RampLaneZ[i] + sgn * (ScaleStripHeightField.RampHalfWidth + 6f), 10f, 1.3f, lipColor);
        for (int i = 0; i < ScaleStripHeightField.CorridorWidths.Length; i++)
            for (int sgn = -1; sgn <= 1; sgn += 2)
                AddPylon(pylons, colors, ScaleStripHeightField.X(ScaleStripHeightField.RunwayEnd + i * seg + 8f),
                    sgn * (ScaleStripHeightField.CorridorWidths[i] * 0.5f - 6f), 12f, 1.4f, wallColor);
        AddMultiMesh("StripPylons", _pylonMesh, pylons, colors);

        // Boost line on the runway for the boosted 0->cap read.
        for (int i = 0; i < 8; i++)
            AddPickup(ScaleStripHeightField.X(550f + i * 50f), 0f, Vector3.Left);

        // Scenery only on the shoulders, never in the measured corridor.
        BuildScatteredProps(
            (x, z) => _world.InBounds(x, z, 40f) && Mathf.Abs(z) > 150f && Mathf.Abs(z) < 290f,
            _world.HalfX - 50f, _world.HalfZ - 20f, 3.5f, (_, _) => 1f);
    }

    private void AddStationSign(float sd, string text)
        => AddSign(_world.SurfacePoint(ScaleStripHeightField.X(sd), 0f, 24f), text, 8f);

    /// <summary>Billboarded world text, sized in metres of glyph height.</summary>
    private Label3D AddSign(Vector3 position, string text, float glyphMetres)
    {
        var label = new Label3D
        {
            Text = text,
            FontSize = 64,
            PixelSize = glyphMetres / 64f,
            OutlineSize = 14,
            Modulate = new Color(0.96f, 0.96f, 0.92f),
            OutlineModulate = new Color(0.05f, 0.05f, 0.07f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Position = position,
        };
        _content.AddChild(label);
        return label;
    }

    /// <summary>
    /// Stage completion (T1, 06 §10 "exit"): the pad's sign swells and a ring flashes outward from the pad
    /// over the outro. Presentation only, on nodes built with the stage, so the node count never moves.
    /// </summary>
    public void PulseExit(int exitIndex)
    {
        if (exitIndex < 0 || exitIndex >= _exitFlashes.Count) return;
        _pulseExit = exitIndex;
        _pulseLeft = Mathf.Max(0.05f, _t.Run.OutroSeconds);
        _exitFlashes[exitIndex].Visible = true;
    }

    private void StepExitPulse(float dt)
    {
        if (_pulseExit < 0) return;
        float total = Mathf.Max(0.05f, _t.Run.OutroSeconds);
        _pulseLeft -= dt;
        float done = Mathf.Clamp(1f - _pulseLeft / total, 0f, 1f);
        var sign = _exitSigns[_pulseExit];
        var flash = _exitFlashes[_pulseExit];
        sign.Scale = Vector3.One * (1f + 0.6f * Mathf.Sin(done * Mathf.Pi));
        flash.Scale = Vector3.One * WorldScale.PadRadius * (1f + 2.5f * done);
        flash.Transparency = done;
        if (_pulseLeft > 0f) return;
        sign.Scale = Vector3.One;
        flash.Visible = false;
        flash.Transparency = 0f;
        _pulseExit = -1;
    }

    /// <summary>Distance markers along the flat calibration lane: 50 m posts, 100 m gantries.</summary>
    private void BuildLaneMarkers()
    {
        for (float d = 0f; d <= TerrainHeightField.LaneLength + 0.5f; d += 50f)
        {
            float x = TerrainHeightField.LaneStartX - d;
            bool hundred = Mathf.PosMod(d, 100f) < 1f;
            float height = hundred ? 9f : 4f;
            float offset = TerrainHeightField.LaneHalfWidth + (hundred ? 4f : 0f);
            var mat = hundred ? _matGantry : _matPost;

            AddPost(x, TerrainHeightField.LaneZ - offset, height, mat);
            AddPost(x, TerrainHeightField.LaneZ + offset, height, mat);

            if (!hundred) continue;

            // A crossbar makes the 100 m stations readable from a long way down the lane.
            float barY = _world.SampleHeight(x, TerrainHeightField.LaneZ) + height + 0.6f;
            _content.AddChild(new MeshInstance3D
            {
                Mesh = _barMesh,
                MaterialOverride = _matGantry,
                Position = new Vector3(x, barY, TerrainHeightField.LaneZ),
                Scale = new Vector3(1.4f, 1.2f, offset * 2f),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
            });
        }
    }

    private void AddPost(float x, float z, float height, Material material)
    {
        Vector3 centre = _world.SurfacePoint(x, z, height * 0.5f - 0.4f);
        _content.AddChild(new MeshInstance3D
        {
            Mesh = _postMesh,
            MaterialOverride = material,
            Position = centre,
            Scale = new Vector3(1f, height, 1f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
        });
        AddCollider(new CylinderShape3D { Radius = 0.65f, Height = height }, centre);
    }

    /// <summary>
    /// Known-height banded pillars beside the spawn. Each band is exactly 5 m, so the
    /// 2 m ball, the camera distance and every terrain feature have a physical yardstick.
    /// </summary>
    private void BuildPillars(float xStart, float z)
    {
        ReadOnlySpan<float> heights = stackalloc float[] { 5f, 10f, 20f, 40f };
        for (int i = 0; i < heights.Length; i++)
        {
            float x = xStart - i * 42f;
            float baseY = _world.SampleHeight(x, z);
            int segments = Mathf.RoundToInt(heights[i] / 5f);

            for (int s = 0; s < segments; s++)
            {
                _content.AddChild(new MeshInstance3D
                {
                    Mesh = _pillarSegmentMesh,
                    MaterialOverride = (s & 1) == 0 ? _matBandLight : _matBandDark,
                    Position = new Vector3(x, baseY + s * 5f + 2.5f, z),
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
                });
            }

            _content.AddChild(new MeshInstance3D
            {
                Mesh = _capMesh,
                MaterialOverride = _matGantry,
                Position = new Vector3(x, baseY + heights[i] + 1.4f, z),
                Scale = Vector3.One * 1.8f,
            });
            AddCollider(new CylinderShape3D { Radius = 2.2f, Height = heights[i] },
                new Vector3(x, baseY + heights[i] * 0.5f, z));
        }
    }

    /// <summary>
    /// Colour-coded pylons labelling every measurable feature. One MultiMesh, one material,
    /// per-instance colour. These are instruments, so they are not scaled by Prop Density.
    /// </summary>
    private void BuildFeaturePylons()
    {
        var transforms = new List<Transform3D>(64);
        var colors = new List<Color>(64);

        var gradeColors = new[]
        {
            new Color(0.35f, 0.80f, 0.35f),   //  8 deg
            new Color(0.95f, 0.72f, 0.20f),   // 15 deg
            new Color(0.90f, 0.28f, 0.22f),   // 25 deg
        };
        ReadOnlySpan<float> gradeX = stackalloc float[]
        {
            TerrainHeightField.Grade8X, TerrainHeightField.Grade15X, TerrainHeightField.Grade25X,
        };
        for (int i = 0; i < 3; i++)
            for (int s = -1; s <= 1; s += 2)
                AddPylon(transforms, colors, gradeX[i] + s * 42f, TerrainHeightField.FanCrestZ + 8f, 14f, 1.5f, gradeColors[i]);

        // Launch ramp lips.
        var rampColor = new Color(0.98f, 0.55f, 0.12f);
        for (int i = 0; i < 3; i++)
            for (int s = -1; s <= 1; s += 2)
                AddPylon(transforms, colors, TerrainHeightField.RampCentreX(i) + s * 34f, TerrainHeightField.RampLipZ(i), 9f, 1.2f, rampColor);

        // Chasm south rims — the hazard line has to be readable well before the edge.
        var warnColor = new Color(0.95f, 0.20f, 0.30f);
        for (float x = -20f; x <= 280f; x += 60f)
        {
            AddPylon(transforms, colors, x, TerrainHeightField.ChasmARimZ + 7f, 8f, 1.1f, warnColor);
            AddPylon(transforms, colors, x, TerrainHeightField.ChasmBRimZ + 7f, 8f, 1.1f, warnColor);
        }

        // Bowl rim.
        var bowlColor = new Color(0.30f, 0.70f, 0.85f);
        for (int i = 0; i < 10; i++)
        {
            float a = Mathf.Tau * i / 10f;
            AddPylon(transforms, colors,
                TerrainHeightField.BowlX + Mathf.Cos(a) * TerrainHeightField.BowlRadius * 1.04f,
                TerrainHeightField.BowlZ + Mathf.Sin(a) * TerrainHeightField.BowlRadius * 1.04f,
                10f, 1.2f, bowlColor);
        }

        // Banked hairpin: pylons on the crest of the outer berm trace the racing line.
        var bankColor = new Color(0.85f, 0.35f, 0.75f);
        float outer = TerrainHeightField.HairpinRadius + TerrainHeightField.HairpinHalfWidth + 8f;
        for (int i = 0; i < 12; i++)
        {
            float a = Mathf.DegToRad(Mathf.Lerp(186f, 354f, i / 11f));
            AddPylon(transforms, colors,
                TerrainHeightField.HairpinX + Mathf.Cos(a) * outer,
                TerrainHeightField.HairpinZ + Mathf.Sin(a) * outer,
                7f, 1.0f, bankColor);
        }

        AddMultiMesh("FeaturePylons", _pylonMesh, transforms, colors);
    }

    private void AddPylon(List<Transform3D> transforms, List<Color> colors,
                          float x, float z, float height, float radius, Color color)
    {
        if (!_world.InBounds(x, z)) return;
        var basis = Basis.Identity.Scaled(new Vector3(radius, height, radius));
        transforms.Add(new Transform3D(basis, _world.SurfacePoint(x, z, height * 0.5f - 0.5f)));
        colors.Add(color);
    }

    /// <summary>
    /// Seeded scenery scatter. Density rises with distance from the calibration lane and
    /// every instrument corridor is excluded, so nothing ever blocks a measured line.
    /// </summary>
    private void BuildScatteredProps(Func<float, float, bool> clear, float rangeX, float rangeZ,
                                     float attemptsScale, Func<float, float, float> chance)
    {
        float density = Mathf.Clamp(_t.World.PropDensity, 0f, 3f);
        if (density <= 0f) return;

        var rocks = new List<Transform3D>(512);
        var rockColors = new List<Color>(512);
        var crystals = new List<Transform3D>(192);
        var crystalColors = new List<Color>(192);
        var pylons = new List<Transform3D>(160);
        var pylonColors = new List<Color>(160);

        var rng = new RandomNumberGenerator { Seed = (ulong)(uint)_world.Seed ^ 0x5BF03635UL };
        int attempts = Mathf.RoundToInt(1600f * density * attemptsScale);

        for (int i = 0; i < attempts; i++)
        {
            float x = rng.RandfRange(-rangeX, rangeX);
            float z = rng.RandfRange(-rangeZ, rangeZ);
            float roll = rng.Randf();
            float yaw = rng.Randf() * Mathf.Tau;
            float size = rng.RandfRange(0.7f, 1.0f);
            float tint = rng.RandfRange(-0.06f, 0.06f);

            if (!clear(x, z)) continue;
            if (rng.Randf() > chance(x, z)) continue;
            if (Steepness(x, z) > 0.8f) continue;

            if (roll < 0.62f)
            {
                float r = size * rng.RandfRange(1.6f, 4.4f);
                float sy = rng.RandfRange(0.5f, 0.9f);
                float sz = rng.RandfRange(0.8f, 1.2f);
                var basis = new Basis(Vector3.Up, yaw).Scaled(new Vector3(r, r * sy, r * sz));
                Vector3 pos = _world.SurfacePoint(x, z, -r * 0.25f);
                rocks.Add(new Transform3D(basis, pos));
                rockColors.Add(Shift(new Color(0.46f, 0.42f, 0.38f), tint));
                AddCollider(new BoxShape3D { Size = new Vector3(2f * r, 2f * r * sy, 2f * r * sz) * 0.9f }, pos, yaw);
            }
            else if (roll < 0.85f)
            {
                float h = size * rng.RandfRange(5f, 13f);
                float r = h * rng.RandfRange(0.14f, 0.24f);
                var basis = new Basis(Vector3.Up, yaw).Scaled(new Vector3(r, h, r));
                Vector3 pos = _world.SurfacePoint(x, z, h * 0.5f - h * 0.15f);
                crystals.Add(new Transform3D(basis, pos));
                crystalColors.Add(Shift(new Color(0.32f, 0.55f, 0.68f), tint));
                AddCollider(new BoxShape3D { Size = new Vector3(1.2f * r, h, 1.2f * r) }, pos, yaw);
            }
            else
            {
                float h = size * rng.RandfRange(6f, 16f);
                float r = h * rng.RandfRange(0.09f, 0.16f);
                var basis = new Basis(Vector3.Up, yaw).Scaled(new Vector3(r, h, r));
                Vector3 pos = _world.SurfacePoint(x, z, h * 0.5f - 0.6f);
                pylons.Add(new Transform3D(basis, pos));
                pylonColors.Add(Shift(new Color(0.60f, 0.58f, 0.52f), tint));
                AddCollider(new BoxShape3D { Size = new Vector3(1.4f * r, h, 1.4f * r) }, pos, yaw);
            }
        }

        AddMultiMesh("Rocks", _rockMesh, rocks, rockColors);
        AddMultiMesh("Crystals", _crystalMesh, crystals, crystalColors);
        AddMultiMesh("PropPylons", _pylonMesh, pylons, pylonColors);
    }

    private static Color Shift(Color c, float d) => new(
        Mathf.Clamp(c.R + d, 0f, 1f), Mathf.Clamp(c.G + d, 0f, 1f), Mathf.Clamp(c.B + d, 0f, 1f));

    /// <summary>Keeps scenery out of the calibration lane, the ramps and the banked turn.</summary>
    private bool IsClearOfInstruments(float x, float z)
    {
        if (!_world.InBounds(x, z)) return false;

        // Calibration lane corridor plus turnaround room at both ends.
        if (Mathf.Abs(z - TerrainHeightField.LaneZ) < 55f
            && x > TerrainHeightField.LaneEndX - 70f && x < TerrainHeightField.LaneStartX + 70f) return false;

        // Uphill climb, mesa, launch ramps and their landing run.
        if (x > TerrainHeightField.Ramp1X - 95f && x < TerrainHeightField.Ramp3X + 95f
            && z > -180f && z < TerrainHeightField.ClimbStartZ + 60f) return false;

        // Banked hairpin track.
        float hdx = x - TerrainHeightField.HairpinX, hdz = z - TerrainHeightField.HairpinZ;
        float hr = Mathf.Sqrt(hdx * hdx + hdz * hdz);
        if (z < TerrainHeightField.HairpinZ + 45f
            && Mathf.Abs(hr - TerrainHeightField.HairpinRadius) < TerrainHeightField.HairpinHalfWidth + 16f) return false;

        // The three measured grade lanes.
        if (z < TerrainHeightField.FanCrestZ + 30f && z > -300f
            && (Mathf.Abs(x - TerrainHeightField.Grade8X) < 40f
                || Mathf.Abs(x - TerrainHeightField.Grade15X) < 40f
                || Mathf.Abs(x - TerrainHeightField.Grade25X) < 40f)) return false;

        return true;
    }

    private static float PlacementChance(float x, float z)
    {
        float lx = Mathf.Clamp(x, TerrainHeightField.LaneEndX, TerrainHeightField.LaneStartX);
        float dx = x - lx, dz = z - TerrainHeightField.LaneZ;
        return 0.12f + 0.88f * Mathf.SmoothStep(70f, 400f, Mathf.Sqrt(dx * dx + dz * dz));
    }

    private float Steepness(float x, float z)
    {
        float gx = _world.SampleHeight(x + 4f, z) - _world.SampleHeight(x - 4f, z);
        float gz = _world.SampleHeight(x, z + 4f) - _world.SampleHeight(x, z - 4f);
        return Mathf.Sqrt(gx * gx + gz * gz) / 8f;
    }

    private void AddMultiMesh(string name, Mesh mesh, List<Transform3D> transforms, List<Color> colors)
    {
        if (transforms.Count == 0) return;

        var mm = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = mesh,
        };
        mm.InstanceCount = transforms.Count;
        for (int i = 0; i < transforms.Count; i++)
        {
            mm.SetInstanceTransform(i, transforms[i]);
            mm.SetInstanceColor(i, colors[i]);
        }

        _content.AddChild(new MultiMeshInstance3D
        {
            Name = name,
            Multimesh = mm,
            MaterialOverride = _matInstanced,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
        });
    }

    // ---------------------------------------------------------------- pickups

    /// <summary>
    /// Props and markers are solid so the toy has thin, hard-edged objects to test
    /// high-speed collision/CCD against (08 §3). Shapes are sized directly rather than
    /// scaling the node, which keeps Jolt free of non-uniform-scale warnings.
    /// </summary>
    private void AddCollider(Shape3D shape, Vector3 position, float yaw = 0f)
    {
        _colliders.AddChild(new CollisionShape3D
        {
            Shape = shape,
            Position = position,
            Rotation = new Vector3(0f, yaw, 0f),
        });
    }

    private sealed class BoostRing
    {
        public Node3D Visual = null!;
        public Node3D Core = null!;
        public bool Collected;
        public float Respawn;
        public float Phase;
    }

    /// <summary>
    /// Two reward lines on fast routes: straight down the 15 deg measured grade, and
    /// through the banked hairpin. Rings hide on collection and return after a few seconds
    /// so the toy can be exercised continuously.
    /// </summary>
    private void BuildBoostPickups()
    {
        for (int i = 0; i < 10; i++)
        {
            float z = Mathf.Lerp(TerrainHeightField.FanCrestZ - 20f, -60f, i / 9f);
            AddPickup(TerrainHeightField.Grade15X, z, Vector3.Forward);
        }

        float r = TerrainHeightField.HairpinRadius;
        for (int i = 0; i < 10; i++)
        {
            float a = Mathf.DegToRad(Mathf.Lerp(196f, 344f, i / 9f));
            AddPickup(
                TerrainHeightField.HairpinX + Mathf.Cos(a) * r,
                TerrainHeightField.HairpinZ + Mathf.Sin(a) * r,
                new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)));
        }
    }

    private void AddPickup(float x, float z, Vector3 facing)
    {
        if (!_world.InBounds(x, z)) return;

        var holder = new Node3D { Name = "BoostRing", Position = _world.SurfacePoint(x, z, 3.6f) };
        _content.AddChild(holder);

        var area = new Area3D { Name = "Trigger", Monitoring = true, Monitorable = false };
        area.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 3.4f } });
        holder.AddChild(area);

        var visual = new Node3D { Name = "Visual" };
        holder.AddChild(visual);

        // The torus axis is its local Y, so aim local Y along the route to make the ring
        // a gate the player drives through rather than a disc they see edge-on.
        Vector3 f = facing.Normalized();
        Vector3 side = f.Cross(Vector3.Up).Normalized();
        visual.AddChild(new MeshInstance3D
        {
            Mesh = _ringMesh,
            MaterialOverride = _matPickup,
            Basis = new Basis(side, f, side.Cross(f)),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });

        var core = new MeshInstance3D
        {
            Mesh = _coreMesh,
            MaterialOverride = _matPickup,
            Scale = Vector3.One * 0.9f,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        visual.AddChild(core);

        var ring = new BoostRing { Visual = visual, Core = core, Phase = _pickups.Count * 0.7f };
        _pickups.Add(ring);
        area.BodyEntered += body => OnRingEntered(ring, body);
    }

    private void OnRingEntered(BoostRing ring, Node3D body)
    {
        if (ring.Collected || body is not PlayerPhysics) return;
        ring.Collected = true;
        ring.Respawn = PickupRespawnSeconds;
        ring.Visual.Visible = false;
        RaiseBoostPickup(_t.Boost.PickupRefillAmount);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _clock += dt;
        ApplyFog();
        StepExitPulse(dt);

        for (int i = 0; i < _pickups.Count; i++)
        {
            BoostRing p = _pickups[i];
            if (p.Collected)
            {
                p.Respawn -= dt;
                if (p.Respawn > 0f) continue;
                p.Collected = false;
                p.Visual.Visible = true;
                continue;
            }

            p.Visual.Position = new Vector3(0f, Mathf.Sin(_clock * 1.8f + p.Phase) * 0.4f, 0f);
            p.Core.Rotate(Vector3.Up, dt * 2.2f);
        }
    }
}
