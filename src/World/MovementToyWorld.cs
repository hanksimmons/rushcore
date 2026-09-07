using Godot;
using Rushcore.Generation;
using Rushcore.Player;
using Rushcore.Tuning;

namespace Rushcore.World;

/// <summary>
/// The Movement Toy laboratory: one deterministic heightfield used as the single
/// logical height source for both the rendered <see cref="ArrayMesh"/> and the
/// <see cref="HeightMapShape3D"/> collision (D-061). Layout/appearance live in the
/// active <see cref="IHeightSource"/> (<see cref="TerrainHeightField"/> lab, or the
/// <see cref="ScaleStripHeightField"/> for Gate M1) and <see cref="WorldDressing"/>.
/// </summary>
public partial class MovementToyWorld : Node3D, Rushcore.Player.IGroundSurface, Rushcore.Player.IStructureSurface
{
    /// <summary>Lab default: metres between height samples, also the size of one rendered facet.</summary>
    public const float DefaultCellSize = 4f;
    /// <summary>Lab samples per side at the default cell size. Extent = (Samples - 1) * DefaultCellSize.</summary>
    public const int Samples = 257;
    public const float Extent = (Samples - 1) * DefaultCellSize;
    /// <summary>Render tiles are this many cells square: bounded allocations, frustum culling per tile.</summary>
    private const int TileCells = 128;

    private readonly GameplayTuning _t;
    private IHeightSource _field = null!;
    private int _nx = Samples, _nz = Samples;
    private WorldDressing _dressing = null!;
    private Node3D _terrainRoot = null!;
    private Node3D _structureRoot = null!;
    private StaticBody3D _terrainBody = null!;
    private CollisionShape3D _terrainCollider = null!;
    private HeightMapShape3D _terrainShape = null!;
    private float[] _heights = System.Array.Empty<float>();
    private float _minHeight, _maxHeight;

    public MovementToyWorld(GameplayTuning tuning, int seed)
    {
        _t = tuning;
        Seed = seed;
    }

    public int Seed { get; private set; }
    public Vector3 SpawnPoint { get; private set; }
    /// <summary>Flat direction the player faces at spawn (down the calibration lane).</summary>
    public Vector3 SpawnFacing => _field.SpawnFacing;
    public float KillPlaneY { get; private set; }
    public Aabb Bounds { get; private set; }
    public IHeightSource Field => _field;
    /// <summary>True while the Gate M1 scale strip is the active terrain.</summary>
    public bool IsStrip { get; private set; }
    /// <summary>True while a generated stage (Phase 2) is the active terrain.</summary>
    public bool IsStage { get; private set; }
    /// <summary>The generated stage definition when <see cref="IsStage"/>; the debug views read it.</summary>
    public StageDefinition? Stage { get; private set; }
    /// <summary>Archetype of the built stage, and the one the world tuning asks for.</summary>
    public TerrainArchetype Archetype { get; private set; }
    public TerrainArchetype WantedArchetype => (TerrainArchetype)Mathf.Clamp(Mathf.RoundToInt(_t.World.Archetype), 0, (int)TerrainArchetype.DuneSea);
    /// <summary>One-line generation summary for the telemetry seed row.</summary>
    public string StageSummary { get; private set; } = "";
    /// <summary>Furthest primary-route vertex the player has reached on a generated stage.</summary>
    public int StageProgressIndex { get; private set; }
    /// <summary>Index into <c>Stage.Checkpoints</c> of the anchor currently armed; −1 = start pad.</summary>
    public int StageCheckpointIndex { get; private set; } = -1;
    /// <summary>Seconds since the last start/teleport on a generated stage.</summary>
    public float StageClock { get; private set; }
    /// <summary>Clock reading when the player first reached the exit pad this attempt; 0 = not yet.</summary>
    public float StageExitTime { get; private set; }

    public void ResetStageProgress()
    {
        StageProgressIndex = 0;
        StageCheckpointIndex = -1;
        StageClock = 0f;
        StageExitTime = 0f;
    }

    /// <summary>
    /// Tracks legitimate progress along the primary route (04 §13): the nearest vertex is searched
    /// only ahead of the last one, so falling back or cutting across never advances it. Returns
    /// the anchor to arm when a new checkpoint has been passed, else null.
    /// </summary>
    public Vector3? UpdateStageProgress(Vector3 playerPos, float dt)
    {
        if (Stage is null) return null;
        StageClock += dt;
        var v = Stage.PrimaryRoute.Vertices;
        int best = StageProgressIndex;
        float bestD = float.MaxValue;
        int hi = Mathf.Min(v.Count - 1, StageProgressIndex + 60);
        for (int i = Mathf.Max(0, StageProgressIndex - 5); i <= hi; i++)
        {
            // Height counts (D-096): a turn below or a floor above the ball never aliases as progress.
            float d = v[i].Position.DistanceSquaredTo(playerPos);
            if (d < bestD) { bestD = d; best = i; }
        }
        // Only count it as progress while the ball is inside the corridor reach of that vertex.
        if (best > StageProgressIndex && bestD < 150f * 150f) StageProgressIndex = best;

        if (StageExitTime <= 0f && new Vector2(Stage.ExitPosition.X - playerPos.X, Stage.ExitPosition.Z - playerPos.Z).Length() < WorldScale.PadRadius)
        {
            StageExitTime = StageClock;
            GD.Print($"[RUSHCORE] Stage exit reached in {StageExitTime:0.0} s (route speed model: {Stage.SpeedProfile.TotalTime:0.0} s base kit)");
        }

        var cps = Stage.Checkpoints;
        int next = StageCheckpointIndex;
        while (next + 1 < cps.Count && cps[next + 1].PrimaryIndex <= StageProgressIndex) next++;
        if (next == StageCheckpointIndex) return null;
        StageCheckpointIndex = next;
        var cp = cps[next];
        return SurfacePoint(cp.Position.X, cp.Position.Z, _t.Movement.BallRadius + 0.6f);
    }
    /// <summary>True when the built terrain matches what the world tuning currently asks for.</summary>
    public bool MatchesTuning()
    {
        bool wantStage = _t.World.GeneratedStage;
        bool wantStrip = !wantStage && _t.World.CalibrationStrip;
        return wantStage == IsStage && wantStrip == IsStrip && (!IsStage || Archetype == WantedArchetype);
    }
    /// <summary>Half extents of the active terrain in metres.</summary>
    public float HalfX { get; private set; } = Extent * 0.5f;
    public float HalfZ { get; private set; } = Extent * 0.5f;
    public bool InBounds(float x, float z, float margin = 8f)
        => Mathf.Abs(x) < HalfX - margin && Mathf.Abs(z) < HalfZ - margin;
    /// <summary>Metres between height samples for the current build (World › Cell Size).</summary>
    public float CellSize { get; private set; } = DefaultCellSize;
    /// <summary>Budget readouts for the last build (Gate M1).</summary>
    public int Triangles { get; private set; }
    public int Tiles { get; private set; }
    public int SampleCount => _heights.Length;
    public ulong BuildMillis { get; private set; }

    /// <summary>Raised when the player collects a boost pickup; carries the refill amount.</summary>
    public event Action<float>? BoostPickupCollected;

    public override void _Ready()
    {
        Name = "MovementToyWorld";

        _terrainBody = new StaticBody3D { Name = "Terrain" };
        // Must match the player: Godot's default friction of 1.0 here would combine to
        // ~0.32 and cancel most slope acceleration, breaking D-005.
        _terrainBody.PhysicsMaterialOverride =
            new PhysicsMaterial { Friction = Rushcore.Player.PlayerPhysics.ArcadeSurfaceFriction, Bounce = 0f };
        _terrainCollider = new CollisionShape3D { Name = "TerrainCollider" };
        _terrainShape = new HeightMapShape3D();
        _terrainCollider.Shape = _terrainShape;
        _terrainBody.AddChild(_terrainCollider);
        AddChild(_terrainBody);

        _terrainRoot = new Node3D { Name = "TerrainMesh" };
        AddChild(_terrainRoot);
        _structureRoot = new Node3D { Name = "Structures" };
        AddChild(_structureRoot);

        _dressing = new WorldDressing(_t, this);
        AddChild(_dressing);
        _dressing.BoostPickupCollected += amount => BoostPickupCollected?.Invoke(amount);

        Build();
    }

    /// <summary>Rebuilds the terrain and all dressing from the current seed and world tuning.</summary>
    public void Build()
    {
        ulong start = Time.GetTicksMsec();
        IsStage = _t.World.GeneratedStage;
        IsStrip = !IsStage && _t.World.CalibrationStrip;
        CellSize = Mathf.Clamp(_t.World.CellSize, 1f, 16f);
        // Heights are stored pre-divided by CellSize so the shape can use a uniform
        // scale; a heightmap shape spans one unit per sample by definition.
        _terrainCollider.Scale = Vector3.One * CellSize;
        Stage = null;
        StageSummary = "";
        ResetStageProgress();
        if (IsStage)
        {
            var generator = new StageGenerator(_t.Movement, _t.Flow, _t.JumpSlam);
            Archetype = WantedArchetype;
            Stage = generator.Generate(new StageGenerationRequest(Seed, 0, Archetype));
            _field = Stage.HeightField!;
            var r = Stage.Report;
            StageSummary = $"{ArchetypeRules.Label(Archetype)} {(r.Passed ? "valid" : "INVALID")}{(r.UsedFallback ? " FALLBACK" : "")} " +
                           $"{Stage.PrimaryRoute.Length:0} m, base-kit {Stage.SpeedProfile.TotalTime:0.0} s ({Stage.SpeedProfile.SecondsBelow(_t.Movement.HardMaxLocomotionSpeed * 0.98f):0.0} s below cap), " +
                           $"ceiling {Stage.CeilingProfile?.TotalTime ?? 0f:0.0} s / {Stage.CeilingProfile?.Flights.Count ?? 0} flights, " +
                           $"{Stage.PrimaryRoute.Bends.Count} bends, {Stage.PrimaryRoute.Features.Count} features, {Stage.Modules.Count} modules, " +
                           $"{Stage.OptionalLines.Count} lines, {Stage.Checkpoints.Count} anchors, gen {r.TotalMillis:0.0} ms";
            GD.Print($"[RUSHCORE] Stage generated seed={Seed}/0 attempts={r.Attempts} {StageSummary} hash={Stage.Hash():X}");
            foreach (var c in r.Checks) GD.Print($"[RUSHCORE]   {(c.Passed ? "ok  " : "FAIL")} {c.Name} {c.Detail}");
        }
        else
        {
            _field = IsStrip ? new ScaleStripHeightField(_t.World) : new TerrainHeightField(Seed, _t.World);
        }
        HalfX = _field.SizeX * 0.5f;
        HalfZ = _field.SizeZ * 0.5f;
        _nx = Mathf.RoundToInt(_field.SizeX / CellSize) + 1;
        _nz = Mathf.RoundToInt(_field.SizeZ / CellSize) + 1;

        int nx = _nx, nz = _nz;
        if (_heights.Length != nx * nz) _heights = new float[nx * nz];
        _minHeight = float.MaxValue;
        _maxHeight = float.MinValue;

        for (int z = 0; z < nz; z++)
        {
            float wz = z * CellSize - HalfZ;
            for (int x = 0; x < nx; x++)
            {
                float wx = x * CellSize - HalfX;
                float h = _field.Sample(wx, wz);
                _heights[z * nx + x] = h / CellSize;   // shape space
                if (h < _minHeight) _minHeight = h;
                if (h > _maxHeight) _maxHeight = h;
            }
        }

        _terrainShape.MapWidth = nx;
        _terrainShape.MapDepth = nz;
        _terrainShape.MapData = _heights;

        BuildTerrainTiles(_dressing.CreateTerrainMaterial());
        BuildStructures();

        Bounds = new Aabb(new Vector3(-HalfX, _minHeight, -HalfZ), new Vector3(_field.SizeX, _maxHeight - _minHeight, _field.SizeZ));
        KillPlaneY = _minHeight - 120f;
        SpawnPoint = _field.SpawnXZ with { Y = SampleHeight(_field.SpawnXZ.X, _field.SpawnXZ.Z) + 4f };

        _dressing.Rebuild();
        BuildMillis = Time.GetTicksMsec() - start;
        GD.Print($"[RUSHCORE] World built seed={Seed} {(IsStage ? "GENERATED STAGE" : IsStrip ? "SCALE STRIP" : "lab")} in {BuildMillis} ms: " +
                 $"{_field.SizeX:0} x {_field.SizeZ:0} m at {CellSize:0.#} m cells = {SampleCount / 1000f:0} k samples, " +
                 $"{Triangles / 1000f:0} k tris in {Tiles} tiles, heights {SampleCount * 4 / 1e6f:0.0} MB, height {_minHeight:0.0}..{_maxHeight:0.0} m");
    }

    public void Regenerate(int seed)
    {
        Seed = seed;
        Build();
    }

    /// <summary>Authoritative height source. Bilinear over the same grid the collider uses.</summary>
    public float SampleHeight(float x, float z)
    {
        float fx = Mathf.Clamp((x + HalfX) / CellSize, 0f, _nx - 1.001f);
        float fz = Mathf.Clamp((z + HalfZ) / CellSize, 0f, _nz - 1.001f);
        int x0 = (int)fx, z0 = (int)fz;
        int x1 = Mathf.Min(x0 + 1, _nx - 1), z1 = Mathf.Min(z0 + 1, _nz - 1);
        float tx = fx - x0, tz = fz - z0;
        float h00 = _heights[z0 * _nx + x0], h10 = _heights[z0 * _nx + x1];
        float h01 = _heights[z1 * _nx + x0], h11 = _heights[z1 * _nx + x1];
        return Mathf.Lerp(Mathf.Lerp(h00, h10, tx), Mathf.Lerp(h01, h11, tx), tz) * CellSize;
    }

    public Vector3 SurfacePoint(float x, float z, float above = 0f) => new(x, SampleHeight(x, z) + above, z);

    // IGroundSurface (D-092): the ground follow reads the grid the collider and mesh are built from.
    float Rushcore.Player.IGroundSurface.Height(float x, float z) => SampleHeight(x, z);
    bool Rushcore.Player.IGroundSurface.Contains(float x, float z) => Mathf.Abs(x) <= HalfX && Mathf.Abs(z) <= HalfZ;

    /// <summary>Physics layer of structures (lids, tubes): the ball collides with it, the camera's occlusion probe does not (04 §9).</summary>
    public const uint StructureLayer = 2;
    /// <summary>The built stage's tubes, for the camera push-out and the harness; empty outside a stage.</summary>
    public IReadOnlyList<TubeDefinition> Tubes => Stage?.Tubes ?? (IReadOnlyList<TubeDefinition>)System.Array.Empty<TubeDefinition>();

    // IStructureSurface (D-101): the tube follow reads the analytic shell.
    public bool Nearest(Vector3 p, out Vector3 axisPoint, out Vector3 tangent, out float radius, out float distance)
    {
        axisPoint = Vector3.Zero; tangent = Vector3.Forward; radius = 0f; distance = float.MaxValue;
        foreach (var tube in Tubes)
        {
            if (!tube.Bounds.HasPoint(p)) continue;
            int i = tube.Nearest(p, out float d);
            if (d >= distance) continue;
            distance = d; axisPoint = tube.Axis[i]; tangent = tube.TangentAt(i); radius = tube.Radius;
        }
        return distance < float.MaxValue;
    }

    /// <summary>Structures (04 §5I, §9): each tube is a swept see-through shell with opaque ribs and an inward-facing
    /// concave collider with backface collision, on the structure layer. Rebuilt with the terrain.</summary>
    private void BuildStructures()
    {
        foreach (Node child in _structureRoot.GetChildren())
        {
            _structureRoot.RemoveChild(child);
            child.QueueFree();
        }
        if (Stage is null) return;
        int k = 0;
        foreach (var tube in Stage.Tubes)
        {
            var built = TubeMesh.Build(tube);
            var shell = new MeshInstance3D { Name = $"Tube{k}Shell", Mesh = built.Shell, MaterialOverride = _dressing.TubeShellMaterial, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            var ribs = new MeshInstance3D { Name = $"Tube{k}Ribs", Mesh = built.Ribs, MaterialOverride = _dressing.TubeRibMaterial };
            var body = new StaticBody3D
            {
                Name = $"Tube{k}Body",
                CollisionLayer = StructureLayer,
                CollisionMask = 0,
                PhysicsMaterialOverride = new PhysicsMaterial { Friction = PlayerPhysics.ArcadeSurfaceFriction, Bounce = 0f },
            };
            body.AddChild(new CollisionShape3D { Shape = new ConcavePolygonShape3D { Data = built.CollisionTriangles, BackfaceCollision = true } });
            _structureRoot.AddChild(shell);
            _structureRoot.AddChild(ribs);
            _structureRoot.AddChild(body);
            k++;
        }
    }

    /// <summary>One ArrayMesh per tile: each allocation is bounded (~4 MB at 128 cells) and the
    /// renderer culls tiles the camera cannot see. Collision stays a single heightfield.</summary>
    private void BuildTerrainTiles(Material material)
    {
        foreach (Node child in _terrainRoot.GetChildren())
        {
            _terrainRoot.RemoveChild(child);
            child.QueueFree();
        }
        Triangles = 0;
        Tiles = 0;
        for (int tz = 0; tz < _nz - 1; tz += TileCells)
        {
            for (int tx = 0; tx < _nx - 1; tx += TileCells)
            {
                int cx = Mathf.Min(TileCells, _nx - 1 - tx);
                int cz = Mathf.Min(TileCells, _nz - 1 - tz);
                _terrainRoot.AddChild(new MeshInstance3D
                {
                    Name = $"Tile_{tx}_{tz}",
                    Mesh = BuildTile(tx, tz, cx, cz),
                    MaterialOverride = material,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
                });
                Triangles += cx * cz * 2;
                Tiles++;
            }
        }
    }

    private ArrayMesh BuildTile(int tx, int tz, int cx, int cz)
    {
        int nx = _nx;
        int vertCount = cx * cz * 6;                  // non-indexed: flat/faceted normals (06 §4)
        var verts = new Vector3[vertCount];
        var norms = new Vector3[vertCount];
        var colors = new Color[vertCount];
        int w = 0;

        for (int z = tz; z < tz + cz; z++)
        {
            for (int x = tx; x < tx + cx; x++)
            {
                float x0 = x * CellSize - HalfX, x1 = x0 + CellSize;
                float z0 = z * CellSize - HalfZ, z1 = z0 + CellSize;
                Vector3 a = new(x0, _heights[z * nx + x] * CellSize, z0);
                Vector3 b = new(x1, _heights[z * nx + x + 1] * CellSize, z0);
                Vector3 c = new(x0, _heights[(z + 1) * nx + x] * CellSize, z1);
                Vector3 d = new(x1, _heights[(z + 1) * nx + x + 1] * CellSize, z1);

                // Godot front faces are CLOCKWISE (unlike OpenGL). Seen from above,
                // a->b->c and b->d->c are clockwise, so the surface faces the sky
                // and survives back-face culling.
                w = EmitTriangle(verts, norms, colors, w, a, b, c);
                w = EmitTriangle(verts, norms, colors, w, b, d, c);
            }
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts;
        arrays[(int)Mesh.ArrayType.Normal] = norms;
        arrays[(int)Mesh.ArrayType.Color] = colors;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    private int EmitTriangle(Vector3[] verts, Vector3[] norms, Color[] colors, int w, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 n = (b - a).Cross(c - a);
        n = n.LengthSquared() > 1e-12f ? n.Normalized() : Vector3.Up;
        if (n.Y < 0f) n = -n;
        Vector3 centroid = (a + b + c) / 3f;
        Color col = _field.SampleColor(centroid, n);

        verts[w] = a; norms[w] = n; colors[w] = col; w++;
        verts[w] = b; norms[w] = n; colors[w] = col; w++;
        verts[w] = c; norms[w] = n; colors[w] = col; w++;
        return w;
    }
}
