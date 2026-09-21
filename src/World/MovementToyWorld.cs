using System.Collections.Generic;
using System.Linq;
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
public partial class MovementToyWorld : Node3D, Rushcore.Player.IGroundSurface
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
    private Rushcore.Vfx.WorldVfx _vfx = null!;
    private Node3D _terrainRoot = null!;
    private Node3D _structureRoot = null!;
    private Node3D _horizonRoot = null!;
    /// <summary>The far horizon ring of the built stage (docs/13 §4, D-114): triangles, the inner ring's points on the footprint's edge, every point.</summary>
    public int HorizonTriangles { get; private set; }
    public Vector3[] HorizonEdge { get; private set; } = System.Array.Empty<Vector3>();
    public Vector3[] HorizonPoints { get; private set; } = System.Array.Empty<Vector3>();
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
    /// <summary>Which stage of the run this build is (T1); the seed chain has always taken it (05 §9).</summary>
    public int StageIndex { get; private set; }
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
    public TerrainArchetype WantedArchetype => (TerrainArchetype)Mathf.Clamp(Mathf.RoundToInt(_t.World.Archetype), 0, (int)TerrainArchetype.SkyTerraces);
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
    /// <summary>Which exit the ball reached (D-105): index into <c>Stage.Exits</c>; −1 until one is reached.</summary>
    public int StageExitIndex { get; private set; } = -1;
    public string StageExitLabel => Stage is { } st && StageExitIndex >= 0 && StageExitIndex < st.Exits.Count ? st.Exits[StageExitIndex].Label : "";

    public void ResetStageProgress()
    {
        StageProgressIndex = 0;
        StageCheckpointIndex = -1;
        StageClock = 0f;
        StageExitTime = 0f;
        StageExitIndex = -1;
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

        // Any exit pad ends the stage (02 §4, D-105): the primary's or a terminal line's; the first one reached counts.
        if (StageExitTime <= 0f)
            foreach (var e in Stage.Exits)
                if (new Vector2(e.Position.X - playerPos.X, e.Position.Z - playerPos.Z).Length() < WorldScale.PadRadius && Mathf.Abs(e.Position.Y - playerPos.Y) < 40f)
                {
                    StageExitTime = StageClock;
                    StageExitIndex = e.Index;
                    GD.Print($"[RUSHCORE] Stage exit {e.Label} reached in {StageExitTime:0.0} s (route speed model: {Stage.SpeedProfile.TotalTime:0.0} s base kit to exit A)");
                    _dressing.PulseExit(e.Index);
                    StageCompleted?.Invoke(e.Index);
                    break;
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
        // The showcase row is lab-only (P-006), so the toggle is compared only where it can be honoured. Comparing
        // it everywhere would leave a stage permanently mismatched while the toggle is on and rebuild the world every frame.
        bool showcaseOk = wantStage || wantStrip || _t.World.EnemyShowcase == BuiltShowcase;
        return wantStage == IsStage && wantStrip == IsStrip && (!IsStage || Archetype == WantedArchetype)
               && _t.World.StageDebugViews == BuiltDebugViews && showcaseOk;
    }
    /// <summary>Whether the last dressing build drew the stage debug views (the toggle rebuilds the world).</summary>
    public bool BuiltDebugViews { get; private set; }
    /// <summary>Whether the last dressing build stood the T3 showcase row up (the toggle rebuilds the world).</summary>
    public bool BuiltShowcase { get; private set; }
    /// <summary>Half extents of the active terrain in metres.</summary>
    public float HalfX { get; private set; } = Extent * 0.5f;
    public float HalfZ { get; private set; } = Extent * 0.5f;
    public bool InBounds(float x, float z, float margin = 8f)
        => Mathf.Abs(x) < HalfX - margin && Mathf.Abs(z) < HalfZ - margin;
    /// <summary>Metres between height samples for the current build (World › Cell Size).</summary>
    public float CellSize { get; private set; } = DefaultCellSize;
    /// <summary>Cosmetic scatter of the last stage build (T4): counts, colliders and cost.</summary>
    public int ScatterRocks { get; private set; }
    public int ScatterCrystals { get; private set; }
    public int ScatterMarkers { get; private set; }
    public int ScatterColliders { get; private set; }
    public ulong ScatterMillis { get; private set; }

    /// <summary>Where the last stage build's scatter and edge markers stand (T4), for the harness.</summary>
    public IReadOnlyList<Vector3> ScatterPositions => _dressing.ScatterPositions;
    public IReadOnlyList<Vector3> MarkerPositions => _dressing.MarkerPositions;
    public IReadOnlyList<Vector3> ScatterColliderPositions => _dressing.ScatterColliderPositions;

    /// <summary>Budget readouts (Gate M1): triangles resident in the terrain mesh right now (the coarse mesh plus the fine
    /// window, docs/13 §3.3), and the tile count.</summary>
    public int Triangles => CoarseTriangles + FineTriangles;
    public int Tiles { get; private set; }
    public int SampleCount => _heights.Length;
    public ulong BuildMillis { get; private set; }
    /// <summary>Of <see cref="BuildMillis"/>, the milliseconds Godot's Jolt module spent building the terrain collider (a mesh shape for the
    /// non-square map, D-115: 2–5 s at 3×, the user's decision; the harness budgets the rest of the build).</summary>
    public ulong ColliderMillis { get; private set; }

    /// <summary>Raised when the player collects a boost pickup; carries the refill amount.</summary>
    public event Action<float>? BoostPickupCollected;

    /// <summary>Raised once per build when the ball first reaches an exit pad; carries the exit index (T1).</summary>
    public event Action<int>? StageCompleted;

    /// <summary>The showcase row's burst pad was driven over (T3): the composition root throws the coins.</summary>
    public event Action<Vector3>? RewardPadTriggered;

    /// <summary>Dressing of the built world; the completion outro pulses the exit through it.</summary>
    public WorldDressing Dressing => _dressing;

    /// <summary>The world's one-shot effects (T3, 06 §10): crush, failed impact, pickup, exit.</summary>
    public Rushcore.Vfx.WorldVfx Vfx => _vfx;

    /// <summary>The T3 lab showcase row, or null when <c>World › Enemy Showcase</c> is off (P-006).</summary>
    public WorldDressing.ShowcaseRow? Showcase => _dressing.Showcase;

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
        _horizonRoot = new Node3D { Name = "Horizon" };
        AddChild(_horizonRoot);

        // World one-shot VFX (T3, 06 §10): built once with a fixed pool, so a rebuild never disturbs it and
        // nothing is allocated when an effect fires.
        _vfx = new Rushcore.Vfx.WorldVfx();
        AddChild(_vfx);

        _dressing = new WorldDressing(_t, this);
        AddChild(_dressing);
        _dressing.BoostPickupCollected += amount => BoostPickupCollected?.Invoke(amount);
        _dressing.RewardPadTriggered += at => RewardPadTriggered?.Invoke(at);

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
            Stage = generator.Generate(new StageGenerationRequest(Seed, StageIndex, Archetype));
            _field = Stage.HeightField!;
            var r = Stage.Report;
            StageSummary = $"{ArchetypeRules.Label(Archetype)} {(r.Passed ? "valid" : "INVALID")}{(r.UsedFallback ? " FALLBACK" : "")} " +
                           $"{Stage.PrimaryRoute.Length:0} m, base-kit {Stage.SpeedProfile.TotalTime:0.0} s ({Stage.SpeedProfile.SecondsBelow(_t.Movement.HardMaxLocomotionSpeed * 0.98f):0.0} s below cap), " +
                           $"ceiling {Stage.CeilingProfile?.TotalTime ?? 0f:0.0} s / {Stage.CeilingProfile?.Flights.Count ?? 0} flights, " +
                           $"{Stage.PrimaryRoute.Bends.Count} bends, {Stage.PrimaryRoute.Features.Count} features, {Stage.Modules.Count} modules, " +
                           $"{Stage.OptionalLines.Count} lines ({Stage.OptionalLines.Count(l => l.Floor >= 2)} terraces), {Stage.Lids.Count} lids{(Stage.PrimaryRoute.Spiral is not null ? ", spiral pit" : "")}, {Stage.Exits.Count} exits, {Stage.Checkpoints.Count} anchors, gen {r.TotalMillis:0.0} ms";
            GD.Print($"[RUSHCORE] Stage generated seed={Seed}/{StageIndex} attempts={r.Attempts} {StageSummary} hash={Stage.Hash():X}");
            foreach (var c in r.Checks) GD.Print($"[RUSHCORE]   {(c.Passed ? "ok  " : "FAIL")} {c.Name} {c.Detail}");
        }
        else
        {
            _field = IsStrip ? new ScaleStripHeightField(_t.World) : new TerrainHeightField(Seed, _t.World);
        }
        ulong tGenerate = Time.GetTicksMsec() - start;
        HalfX = _field.SizeX * 0.5f;
        HalfZ = _field.SizeZ * 0.5f;
        _nx = Mathf.RoundToInt(_field.SizeX / CellSize) + 1;
        _nz = Mathf.RoundToInt(_field.SizeZ / CellSize) + 1;

        int nx = _nx, nz = _nz;
        // Always a fresh array: a tile worker still reading the previous build's heights (docs/13 §3.3) keeps its own.
        _heights = new float[nx * nz];
        // Sampled a row per task (docs/13 §3.3: the height source is pure once generated, and the 752 k samples of a stage
        // took 2.3 s on one core, 0.2 s here); each row keeps its own extremes, reduced afterwards.
        var heights = _heights; var field = _field; float cell = CellSize, halfX = HalfX, halfZ = HalfZ;
        var rowMin = new float[nz]; var rowMax = new float[nz];
        System.Threading.Tasks.Parallel.For(0, nz, z =>
        {
            float lo = float.MaxValue, hi = float.MinValue;
            float wz = z * cell - halfZ;
            for (int x = 0; x < nx; x++)
            {
                float wx = x * cell - halfX;
                float h = field.Sample(wx, wz) - field.Sink(wx, wz);   // under a wall shell the grid is sunk (D-111)
                heights[z * nx + x] = h / cell;   // shape space
                if (h < lo) lo = h;
                if (h > hi) hi = h;
            }
            rowMin[z] = lo; rowMax[z] = hi;
        });
        _minHeight = rowMin.Min();
        _maxHeight = rowMax.Max();

        ulong tSample = Time.GetTicksMsec() - start;
        // The collider: Godot's Jolt module builds the shape here, synchronously (a mesh shape for a non-square map, D-115).
        _terrainShape.MapWidth = nx;
        _terrainShape.MapDepth = nz;
        _terrainShape.MapData = _heights;
        ulong tHeights = Time.GetTicksMsec() - start;
        ulong tCollider = tHeights - tSample;
        ColliderMillis = tCollider;
        if (System.Environment.GetEnvironmentVariable("RUSHCORE_COLLIDER_PROBE") == "1")
        {
            ulong p0 = Time.GetTicksMsec(); _terrainShape.MapData = _heights; ulong p1 = Time.GetTicksMsec();
            var copy = (float[])_heights.Clone(); copy[nx * nz / 2] += 0.01f; _terrainShape.MapData = copy; ulong p2 = Time.GetTicksMsec();
            _terrainShape.MapData = _heights; ulong p3 = Time.GetTicksMsec();
            GD.Print($"[RUSHCORE] collider probe: same array again {p1 - p0} ms, a changed copy {p2 - p1} ms, the original again {p3 - p2} ms");
        }

        BuildTerrainTiles(_dressing.CreateTerrainMaterial());
        ulong tTiles = Time.GetTicksMsec() - start - tHeights;
        BuildStructures();
        BuildHorizon();
        ulong tStructures = Time.GetTicksMsec() - start - tHeights - tTiles;

        Bounds = new Aabb(new Vector3(-HalfX, _minHeight, -HalfZ), new Vector3(_field.SizeX, _maxHeight - _minHeight, _field.SizeZ));
        KillPlaneY = _minHeight - 120f;
        SpawnPoint = _field.SpawnXZ with { Y = SampleHeight(_field.SpawnXZ.X, _field.SpawnXZ.Z) + 4f };

        _dressing.Rebuild();
        ScatterRocks = _dressing.ScatterRocks;
        ScatterCrystals = _dressing.ScatterCrystals;
        ScatterMarkers = _dressing.ScatterMarkers;
        ScatterColliders = _dressing.ScatterColliders;
        ScatterMillis = _dressing.ScatterMillis;
        if (IsStage)
            GD.Print($"[RUSHCORE] scatter: {ScatterRocks} rocks, {ScatterCrystals} crystals, {ScatterMarkers} markers " +
                     $"({ScatterColliders} with colliders) in {ScatterMillis} ms at density {_t.World.PropDensity:0.00}: {_dressing.ScatterPhases}");
        BuiltDebugViews = _t.World.StageDebugViews;
        BuiltShowcase = _dressing.Showcase is not null;
        BuildMillis = Time.GetTicksMsec() - start;
        GD.Print($"[RUSHCORE] World built seed={Seed} {(IsStage ? "GENERATED STAGE" : IsStrip ? "SCALE STRIP" : "lab")} in {BuildMillis} ms " +
                 $"(generate {tGenerate}, heights {tSample - tGenerate}, collider {tCollider}, mesh {tTiles}, structures {tStructures}, dressing {BuildMillis - tHeights - tTiles - tStructures}): " +
                 $"{_field.SizeX:0} x {_field.SizeZ:0} m at {CellSize:0.#} m cells = {SampleCount / 1000f:0} k samples, " +
                 $"coarse {CoarseTriangles / 1000f:0} k tris at {CellSize * _stride:0} m, fine window {FineTiles} of {Tiles} tiles = {FineTriangles / 1000f:0} k tris, " +
                 $"heights {SampleCount * 4 / 1e6f:0.0} MB, height {_minHeight:0.0}..{_maxHeight:0.0} m");
    }

    /// <summary>Rebuilds on a new seed, keeping the stage index (the lab, the strip and "restart same seed").</summary>
    public void Regenerate(int seed)
    {
        Seed = seed;
        Build();
    }

    /// <summary>
    /// Rebuilds as the requested stage of a run (T1). The archetype still comes from the world toggle the
    /// request was made with, so the panel, <see cref="MatchesTuning"/> and the built stage never disagree.
    /// </summary>
    public void Regenerate(StageGenerationRequest request)
    {
        Seed = request.RunSeed;
        StageIndex = request.StageIndex;
        Build();
    }

    /// <summary>
    /// Debug teleports that skip forward along the primary (07 §12) move progress with them, so the tracker
    /// and the armed anchor stay honest instead of trailing at the start pad. The clock is untouched.
    /// </summary>
    public void SkipStageProgressTo(int vertexIndex)
    {
        if (Stage is null) return;
        StageProgressIndex = Mathf.Clamp(vertexIndex, 0, Stage.PrimaryRoute.Vertices.Count - 1);
        var cps = Stage.Checkpoints;
        int next = -1;
        while (next + 1 < cps.Count && cps[next + 1].PrimaryIndex <= StageProgressIndex) next++;
        StageCheckpointIndex = next;
    }

    /// <summary>The surface a ball rides at a point: the stamp itself under a wall shell (D-111: the shell is the stamp
    /// sampled finely, and the grid lies sunk beneath it), the collided heightfield everywhere else. Under a tunnel's cap
    /// (docs/13, D-113) the stage has two surfaces over one XZ, the cap and the trench: given the asker's own height
    /// <paramref name="y"/> (the ball's, or the camera lens's) the answer is the cap's top when the asker stands above
    /// the cap's underside less a ball, the trench otherwise; with no height the trench, as every other caller expects.</summary>
    public float SampleHeight(float x, float z, float y = float.NaN)
    {
        if (!float.IsNaN(y) && Stage?.HeightField is { } hf && hf.CapOver(x, z, out float capTop) && y >= capTop - 2f * _t.Movement.BallRadius) return capTop;
        return _field.Sink(x, z) > 0f ? _field.Sample(x, z) : GridHeight(x, z);
    }

    /// <summary>The collided heightfield's own height (bilinear over the grid), sunk where a wall shell stands.</summary>
    public float GridHeight(float x, float z)
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
    float Rushcore.Player.IGroundSurface.Height(float x, float z, float y) => SampleHeight(x, z, y);
    bool Rushcore.Player.IGroundSurface.Contains(float x, float z) => Mathf.Abs(x) <= HalfX && Mathf.Abs(z) <= HalfZ;
    /// <summary>The authored wall profile of a walled stage (D-109); the lab and the strip have none (their walls are grid-followed).</summary>
    bool Rushcore.Player.IGroundSurface.WallSurface(Vector3 position, float ballRadius, out Vector3 normal, out float gap, out float curvature)
    {
        if (Stage?.HeightField is { } f) return f.WallSurface(position, ballRadius, out normal, out gap, out curvature);
        normal = Vector3.Up; gap = float.MaxValue; curvature = 0f;
        return false;
    }

    /// <summary>Physics layer of structures (lids, wall shells): the ball collides with it, the camera's occlusion probe does not (04 §9).</summary>
    public const uint StructureLayer = 2;
    /// <summary>Collision triangles in the wall shells of the current stage (D-111); 0 on an unwalled archetype.</summary>
    public int WallShellTriangles { get; private set; }
    /// <summary>Collision triangles in the tunnel roofs (docs/13, D-113): arches, caps, portal faces and rims.</summary>
    public int TunnelRoofTriangles { get; private set; }
    /// <summary>The wall shells' collision triangles per strip (harness instruments only).</summary>
    public List<Vector3[]> WallShellData { get; } = new();
    /// <summary>Structures (04 §5I, §9): wall shells and lids, on the structure layer. Rebuilt with the terrain.</summary>
    private void BuildStructures()
    {
        foreach (Node child in _structureRoot.GetChildren())
        {
            _structureRoot.RemoveChild(child);
            child.QueueFree();
        }
        if (Stage is null) return;
        // Wall shells (D-111): the wall band of every profiled line as a swept surface of the stamp, collided as an
        // outward-facing concave shape on the structure layer and drawn with the terrain material. The heightfield under
        // it is sunk, so the shell is the wall the ball meets and the follow's analytic profile is exactly its surface.
        WallShellTriangles = 0;
        TunnelRoofTriangles = 0;
        WallShellData.Clear();
        if (Stage.HeightField is { } hf)
        {
            var shells = hf.ShellStrips();
            int w = 0;
            foreach (var strip in shells) WallShellTriangles += AddShell(strip, $"Wall{w++}");
            // Tunnel roofs (docs/13 §2.1, D-113): the arch, the cap, the portal faces and rims, built and collided as the shells are.
            int r = 0;
            foreach (var strip in hf.RoofStrips()) TunnelRoofTriangles += AddShell(strip, $"Roof{r++}");
        }
        int j = 0;
        foreach (var lid in Stage.Lids)
        {
            var size = new Vector3(lid.Length, lid.Thickness, lid.Width);
            var xf = new Transform3D(Basis.FromEuler(new Vector3(0f, -lid.Heading, 0f)), new Vector3(lid.Centre.X, lid.RoofBottom + lid.Thickness * 0.5f, lid.Centre.Z));
            _structureRoot.AddChild(new MeshInstance3D { Name = $"Lid{j}", Mesh = new BoxMesh { Size = size }, MaterialOverride = _dressing.LidMaterial, Transform = xf });
            var body = new StaticBody3D
            {
                Name = $"Lid{j}Body", CollisionLayer = StructureLayer, CollisionMask = 0, Transform = xf,
                PhysicsMaterialOverride = new PhysicsMaterial { Friction = PlayerPhysics.ArcadeSurfaceFriction, Bounce = 0f },
            };
            body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
            _structureRoot.AddChild(body);
            j++;
        }
    }

    /// <summary>The far horizon (docs/13 §4.1, D-114): one ring mesh from the footprint's edge to 20 km on a generated stage; the lab
    /// and the strip have none. No collider.</summary>
    private void BuildHorizon()
    {
        foreach (Node child in _horizonRoot.GetChildren())
        {
            _horizonRoot.RemoveChild(child);
            child.QueueFree();
        }
        HorizonTriangles = 0;
        HorizonEdge = System.Array.Empty<Vector3>();
        HorizonPoints = System.Array.Empty<Vector3>();
        if (Stage?.HeightField is not { } hf) return;
        var built = HorizonRing.Build(hf, Archetype);
        HorizonTriangles = built.Triangles;
        HorizonEdge = built.Edge;
        HorizonPoints = built.Vertices;
        _horizonRoot.AddChild(new MeshInstance3D
        {
            Name = "HorizonRing", Mesh = built.Mesh, MaterialOverride = _dressing.CreateTerrainMaterial(),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
    }

    /// <summary>One shell strip as a drawn mesh and a concave collider on the structure layer; returns its triangle count.</summary>
    private int AddShell(WallShellStrip strip, string name)
    {
        var built = WallShellMesh.Build(strip);
        if (built.CollisionTriangles.Length == 0) return 0;
        WallShellData.Add(built.CollisionTriangles);
        _structureRoot.AddChild(new MeshInstance3D { Name = name, Mesh = built.Mesh, MaterialOverride = _dressing.CreateTerrainMaterial(), CastShadow = GeometryInstance3D.ShadowCastingSetting.On });
        var body = new StaticBody3D
        {
            Name = name + "Body",
            CollisionLayer = StructureLayer,
            CollisionMask = 0,
            PhysicsMaterialOverride = new PhysicsMaterial { Friction = PlayerPhysics.ArcadeSurfaceFriction, Bounce = 0f },
        };
        body.AddChild(new CollisionShape3D { Shape = new ConcavePolygonShape3D { Data = built.CollisionTriangles, BackfaceCollision = true } });
        _structureRoot.AddChild(body);
        return built.CollisionTriangles.Length / 3;
    }

    /// <summary>The lid whose roof a plan position is under, if any (the confined camera, D-102).</summary>
    public LidDefinition? LidOver(float x, float z)
    {
        if (Stage is null) return null;
        foreach (var lid in Stage.Lids) if (lid.Covers(x, z)) return lid;
        return null;
    }

    /// <summary>The roof's underside over a plan position (docs/13 §2.5, D-113): a lid's bottom, or a tunnel's arch at that
    /// lateral offset (eased upward over the 30 m outside a portal); null under open sky. The confined camera reads it.</summary>
    public float? RoofOver(float x, float z)
    {
        if (LidOver(x, z) is { } lid) return lid.RoofBottom;
        if (Stage?.HeightField is { } hf)
        {
            float arch = hf.ArchOver(x, z);
            if (!float.IsNaN(arch)) return arch;
        }
        return null;
    }

    // ---------------------------------------------------------------- the resident mesh (docs/13 §3.3, D-115)
    // The whole stage is drawn once, coarsely, and finely only around the ball: every tile (128 fine cells square,
    // 512 m at 4 m) has two mesh instances, a coarse one at CoarseCellSize built with the stage from every stride-th
    // height, and a fine one at CellSize that holds a mesh only while the tile's centre lies inside the window round
    // the ball. Both are the same flat-shaded builder over the same heights; the collider is never windowed. The
    // window is re-evaluated after FineWindowStep of travel: tiles within FineWindowRadius are built on the thread
    // pool (plain arrays over the heights and the field's colours) nearest first and committed here, one
    // AddSurfaceFromArrays each; tiles beyond FineWindowKeep drop their mesh. A coarse tile is hidden while the fine
    // tile over it is resident, and that is the whole level of detail. Every tile hangs a skirt along its edges so
    // a fine/coarse seam never shows a crack. Nodes are never added or removed by the window, so the tree stays flat.
    /// <summary>Cell size of the coarse mesh, resident over the whole stage.</summary>
    public const float CoarseCellSize = 16f;
    /// <summary>A tile whose centre lies within this plan distance of the ball gets its fine mesh...</summary>
    public const float FineWindowRadius = 1200f;
    /// <summary>...and keeps it until the centre lies beyond this (300 m of hysteresis).</summary>
    public const float FineWindowKeep = 1500f;
    /// <summary>Travel between two evaluations of the window.</summary>
    public const float FineWindowStep = 100f;
    /// <summary>Depth of the skirt every tile hangs along its edges.</summary>
    public const float SkirtDepth = 6f;

    private sealed class TerrainTile
    {
        public int Tx, Tz, Cx, Cz;               // fine-sample origin and size in fine cells
        public Vector2 Centre;
        public MeshInstance3D Coarse = null!, Fine = null!;
        public int CoarseTriangles, FineTriangles;
        public bool Pending;                     // a worker is building its fine mesh
        public bool Resident => Fine.Mesh is not null;
    }
    private readonly List<TerrainTile> _tiles = new();
    /// <summary>Fine cells per coarse cell; 1 means the coarse mesh is the fine mesh and there is no window.</summary>
    private int _stride = 1;
    /// <summary>Bumped by every build; a worker's result from an older build is dropped.</summary>
    private int _generation;
    private Vector2 _windowAt = new(float.NaN, float.NaN);
    private readonly System.Collections.Concurrent.ConcurrentQueue<(int Generation, int Tile, TileArrays Arrays)> _finished = new();
    private readonly record struct TileJob(float[] Heights, int Nx, float Cell, float HalfX, float HalfZ, IHeightSource Field, int Tx, int Tz, int Cx, int Cz, int Stride);
    private readonly record struct TileArrays(Vector3[] Verts, Vector3[] Norms, Color[] Cols, int Triangles);

    /// <summary>Triangles of the coarse mesh (all of it, hidden tiles included) and of the fine tiles resident now.</summary>
    public int CoarseTriangles { get; private set; }
    public int FineTriangles { get; private set; }
    /// <summary>Fine tiles resident, and fine tiles a worker is still building.</summary>
    public int FineTiles => _tiles.Count(t => t.Resident);
    public int PendingTiles => _tiles.Count(t => t.Pending);
    /// <summary>Cell size of the coarse mesh as built (the stage's cell size times the stride).</summary>
    public float CoarseCell => CellSize * _stride;
    /// <summary>Every tile's plan centre and whether its fine mesh is resident or being built (the harness's window checks).</summary>
    public IEnumerable<(Vector2 Centre, bool Fine, bool Pending)> TerrainTileStates => _tiles.Select(t => (t.Centre, t.Resident, t.Pending));

    /// <summary>Builds the coarse mesh over the whole terrain and the fine window round the spawn point, complete before the
    /// run starts (docs/13 §3.3). Both in parallel over the heights; the nodes and meshes are made here.</summary>
    private void BuildTerrainTiles(Material material)
    {
        foreach (Node child in _terrainRoot.GetChildren())
        {
            _terrainRoot.RemoveChild(child);
            child.QueueFree();
        }
        _tiles.Clear();
        _generation++;
        while (_finished.TryDequeue(out _)) { }
        CoarseTriangles = 0;
        FineTriangles = 0;
        Tiles = 0;
        _stride = Mathf.Max(1, Mathf.RoundToInt(CoarseCellSize / CellSize));
        for (int tz = 0; tz < _nz - 1; tz += TileCells)
            for (int tx = 0; tx < _nx - 1; tx += TileCells)
            {
                int cx = Mathf.Min(TileCells, _nx - 1 - tx), cz = Mathf.Min(TileCells, _nz - 1 - tz);
                _tiles.Add(new TerrainTile
                {
                    Tx = tx, Tz = tz, Cx = cx, Cz = cz,
                    Centre = new Vector2((tx + cx * 0.5f) * CellSize - HalfX, (tz + cz * 0.5f) * CellSize - HalfZ),
                    Coarse = new MeshInstance3D { Name = $"Coarse_{tx}_{tz}", MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.On },
                    Fine = new MeshInstance3D { Name = $"Fine_{tx}_{tz}", MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.On },
                });
            }
        Tiles = _tiles.Count;
        var coarse = new TileArrays[_tiles.Count];
        System.Threading.Tasks.Parallel.For(0, _tiles.Count, i => coarse[i] = BuildTileArrays(Job(_tiles[i], _stride)));
        for (int i = 0; i < _tiles.Count; i++)
        {
            var tile = _tiles[i];
            tile.Coarse.Mesh = ToMesh(coarse[i]);
            tile.CoarseTriangles = coarse[i].Triangles;
            CoarseTriangles += tile.CoarseTriangles;
            _terrainRoot.AddChild(tile.Coarse);
            _terrainRoot.AddChild(tile.Fine);
        }
        // The window round the start pad, complete before the run starts.
        _windowAt = new Vector2(_field.SpawnXZ.X, _field.SpawnXZ.Z);
        if (_stride <= 1) return;
        var wanted = _tiles.Where(t => t.Centre.DistanceTo(_windowAt) <= FineWindowRadius).ToList();
        var fine = new TileArrays[wanted.Count];
        System.Threading.Tasks.Parallel.For(0, wanted.Count, i => fine[i] = BuildTileArrays(Job(wanted[i], 1)));
        for (int i = 0; i < wanted.Count; i++) CommitFine(wanted[i], fine[i]);
    }

    private TileJob Job(TerrainTile t, int stride) => new(_heights, _nx, CellSize, HalfX, HalfZ, _field, t.Tx, t.Tz, t.Cx, t.Cz, stride);

    /// <summary>Slides the fine window after the ball (docs/13 §3.3): commits the tiles the workers have finished, and after
    /// <see cref="FineWindowStep"/> of travel frees the tiles that fell beyond the keep radius and sends the tiles that came
    /// inside the window to the thread pool, nearest first. Called every frame by the composition root.</summary>
    public void UpdateTerrainWindow(Vector3 focus)
    {
        CommitFinishedTiles();
        if (_stride <= 1) return;
        var f = new Vector2(focus.X, focus.Z);
        if (f.DistanceTo(_windowAt) < FineWindowStep) return;
        _windowAt = f;
        var requests = new List<TerrainTile>();
        foreach (var tile in _tiles)
        {
            float d = tile.Centre.DistanceTo(f);
            if (d > FineWindowKeep) { if (tile.Resident) FreeFine(tile); }
            else if (d <= FineWindowRadius && !tile.Resident && !tile.Pending) requests.Add(tile);
        }
        requests.Sort((a, b) => a.Centre.DistanceSquaredTo(f).CompareTo(b.Centre.DistanceSquaredTo(f)));
        foreach (var tile in requests)
        {
            tile.Pending = true;
            var job = Job(tile, 1);
            int generation = _generation, index = _tiles.IndexOf(tile);
            System.Threading.Tasks.Task.Run(() => _finished.Enqueue((generation, index, BuildTileArrays(job))));
        }
    }

    private void CommitFinishedTiles()
    {
        while (_finished.TryDequeue(out var r))
        {
            if (r.Generation != _generation) continue;
            var tile = _tiles[r.Tile];
            tile.Pending = false;
            if (tile.Resident || tile.Centre.DistanceTo(_windowAt) > FineWindowKeep) continue;   // fell out of the window while building
            CommitFine(tile, r.Arrays);
        }
    }

    private void CommitFine(TerrainTile tile, TileArrays arrays)
    {
        tile.Fine.Mesh = ToMesh(arrays);
        tile.FineTriangles = arrays.Triangles;
        FineTriangles += tile.FineTriangles;
        tile.Coarse.Visible = false;
    }

    private void FreeFine(TerrainTile tile)
    {
        FineTriangles -= tile.FineTriangles;
        tile.FineTriangles = 0;
        tile.Fine.Mesh = null;
        tile.Coarse.Visible = true;
    }

    private static ArrayMesh ToMesh(TileArrays a)
    {
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = a.Verts;
        arrays[(int)Mesh.ArrayType.Normal] = a.Norms;
        arrays[(int)Mesh.ArrayType.Color] = a.Cols;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    /// <summary>The coarse mesh's own height at a plan point (bilinear over the coarse grid) and the height span of the coarse
    /// cell's four corners, for the harness's chord check against the collider (docs/13 §3.4).</summary>
    public float CoarseHeight(float x, float z, out float span)
    {
        float cell = CoarseCell;
        int cx = (_nx - 1) / _stride, cz = (_nz - 1) / _stride;   // whole coarse cells
        float fx = Mathf.Clamp((x + HalfX) / cell, 0f, cx - 0.001f), fz = Mathf.Clamp((z + HalfZ) / cell, 0f, cz - 0.001f);
        int x0 = (int)fx * _stride, z0 = (int)fz * _stride;
        int x1 = Mathf.Min(x0 + _stride, _nx - 1), z1 = Mathf.Min(z0 + _stride, _nz - 1);
        float tx = fx - (int)fx, tz = fz - (int)fz;
        float h00 = _heights[z0 * _nx + x0] * CellSize, h10 = _heights[z0 * _nx + x1] * CellSize;
        float h01 = _heights[z1 * _nx + x0] * CellSize, h11 = _heights[z1 * _nx + x1] * CellSize;
        span = Mathf.Max(Mathf.Max(h00, h10), Mathf.Max(h01, h11)) - Mathf.Min(Mathf.Min(h00, h10), Mathf.Min(h01, h11));
        return Mathf.Lerp(Mathf.Lerp(h00, h10, tx), Mathf.Lerp(h01, h11, tx), tz);
    }

    /// <summary>One tile's arrays at a stride (1 = fine, the coarse stride otherwise): non-indexed, flat/faceted normals
    /// (06 §4), the field's colour per facet, and the skirt along its four edges. Pure arrays over the job's own
    /// references, so it runs on any thread.</summary>
    private static TileArrays BuildTileArrays(TileJob j)
    {
        int cellsX = (j.Cx + j.Stride - 1) / j.Stride, cellsZ = (j.Cz + j.Stride - 1) / j.Stride;   // the last cell may be narrower
        int ground = cellsX * cellsZ * 2, skirt = (cellsX + cellsZ) * 4;
        int vertCount = (ground + skirt) * 3;
        var verts = new Vector3[vertCount];
        var norms = new Vector3[vertCount];
        var cols = new Color[vertCount];
        float H(int x, int z) => j.Heights[z * j.Nx + x] * j.Cell;
        Vector3 P(int x, int z) => new(x * j.Cell - j.HalfX, H(x, z), z * j.Cell - j.HalfZ);
        int xEnd = j.Tx + j.Cx, zEnd = j.Tz + j.Cz;

        // A row of cells per task, each cell's six vertices at a fixed offset (a fine tile is ready in tens of milliseconds
        // rather than 150, which is what keeps the window ahead of a ball at the cap).
        System.Threading.Tasks.Parallel.For(0, cellsZ, row =>
        {
            int z = j.Tz + row * j.Stride, z1 = Mathf.Min(z + j.Stride, zEnd);
            int w = row * cellsX * 6;
            for (int x = j.Tx; x < xEnd; x += j.Stride)
            {
                int x1 = Mathf.Min(x + j.Stride, xEnd);
                Vector3 a = P(x, z), b = P(x1, z), c = P(x, z1), d = P(x1, z1);
                // Godot front faces are CLOCKWISE (unlike OpenGL). Seen from above, a->b->c and b->d->c are
                // clockwise, so the surface faces the sky and survives back-face culling.
                w = EmitTriangle(j.Field, verts, norms, cols, w, a, b, c);
                w = EmitTriangle(j.Field, verts, norms, cols, w, b, d, c);
            }
        });
        int w = ground * 3;
        // Skirts (docs/13 §3.3): a band SkirtDepth deep hanging from each edge, facing outward, coloured and lit like the
        // ground beside it, so where a fine tile's edge and its coarse neighbour's disagree the gap shows ground, not sky.
        for (int x = j.Tx; x < xEnd; x += j.Stride)
        {
            int x1 = Mathf.Min(x + j.Stride, xEnd);
            w = EmitSkirt(j.Field, verts, norms, cols, w, P(x, j.Tz), P(x1, j.Tz), new Vector3(0f, 0f, -1f));
            w = EmitSkirt(j.Field, verts, norms, cols, w, P(x, zEnd), P(x1, zEnd), new Vector3(0f, 0f, 1f));
        }
        for (int z = j.Tz; z < zEnd; z += j.Stride)
        {
            int z1 = Mathf.Min(z + j.Stride, zEnd);
            w = EmitSkirt(j.Field, verts, norms, cols, w, P(j.Tx, z), P(j.Tx, z1), new Vector3(-1f, 0f, 0f));
            w = EmitSkirt(j.Field, verts, norms, cols, w, P(xEnd, z), P(xEnd, z1), new Vector3(1f, 0f, 0f));
        }
        return new TileArrays(verts, norms, cols, w / 3);
    }

    private static int EmitTriangle(IHeightSource field, Vector3[] verts, Vector3[] norms, Color[] colors, int w, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 n = (b - a).Cross(c - a);
        n = n.LengthSquared() > 1e-12f ? n.Normalized() : Vector3.Up;
        if (n.Y < 0f) n = -n;
        Vector3 centroid = (a + b + c) / 3f;
        Color col = field.SampleColor(centroid, n);

        verts[w] = a; norms[w] = n; colors[w] = col; w++;
        verts[w] = b; norms[w] = n; colors[w] = col; w++;
        verts[w] = c; norms[w] = n; colors[w] = col; w++;
        return w;
    }

    /// <summary>Two triangles from an edge segment down <see cref="SkirtDepth"/>, wound to face <paramref name="outward"/>.</summary>
    private static int EmitSkirt(IHeightSource field, Vector3[] verts, Vector3[] norms, Color[] cols, int w, Vector3 p0, Vector3 p1, Vector3 outward)
    {
        Vector3 q0 = p0 - Vector3.Up * SkirtDepth, q1 = p1 - Vector3.Up * SkirtDepth;
        Color col = field.SampleColor((p0 + p1) * 0.5f, Vector3.Up);
        w = Facing(verts, norms, cols, w, p0, p1, q0, outward, col);
        w = Facing(verts, norms, cols, w, p1, q1, q0, outward, col);
        return w;

        static int Facing(Vector3[] verts, Vector3[] norms, Color[] cols, int w, Vector3 a, Vector3 b, Vector3 c, Vector3 outward, Color col)
        {
            if ((b - a).Cross(c - a).Dot(outward) < 0f) (b, c) = (c, b);
            verts[w] = a; norms[w] = Vector3.Up; cols[w] = col; w++;
            verts[w] = b; norms[w] = Vector3.Up; cols[w] = col; w++;
            verts[w] = c; norms[w] = Vector3.Up; cols[w] = col; w++;
            return w;
        }
    }
}
