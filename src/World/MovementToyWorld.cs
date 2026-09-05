using Godot;
using Rushcore.Player;
using Rushcore.Tuning;

namespace Rushcore.World;

/// <summary>
/// The Movement Toy laboratory: one deterministic heightfield used as the single
/// logical height source for both the rendered <see cref="ArrayMesh"/> and the
/// <see cref="HeightMapShape3D"/> collision (D-061). Layout/appearance live in
/// <see cref="TerrainHeightField"/> and <see cref="WorldDressing"/>.
/// </summary>
public partial class MovementToyWorld : Node3D
{
    /// <summary>Metres between height samples. Also the size of one rendered facet.</summary>
    public const float CellSize = 4f;
    /// <summary>Samples per side. Extent = (Samples - 1) * CellSize metres.</summary>
    public const int Samples = 257;
    public const float Extent = (Samples - 1) * CellSize;

    private readonly GameplayTuning _t;
    private TerrainHeightField _field = null!;
    private WorldDressing _dressing = null!;
    private MeshInstance3D _terrainMesh = null!;
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
    public TerrainHeightField Field => _field;

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
        // Heights are stored pre-divided by CellSize so the shape can use a uniform
        // scale; a heightmap shape spans one unit per sample by definition.
        _terrainCollider.Scale = Vector3.One * CellSize;
        _terrainBody.AddChild(_terrainCollider);
        AddChild(_terrainBody);

        _terrainMesh = new MeshInstance3D { Name = "TerrainMesh", CastShadow = GeometryInstance3D.ShadowCastingSetting.On };
        AddChild(_terrainMesh);

        _dressing = new WorldDressing(_t, this);
        AddChild(_dressing);
        _dressing.BoostPickupCollected += amount => BoostPickupCollected?.Invoke(amount);

        Build();
    }

    /// <summary>Rebuilds the terrain and all dressing from the current seed and world tuning.</summary>
    public void Build()
    {
        ulong start = Time.GetTicksMsec();
        _field = new TerrainHeightField(Seed, _t.World);

        int n = Samples;
        if (_heights.Length != n * n) _heights = new float[n * n];
        _minHeight = float.MaxValue;
        _maxHeight = float.MinValue;

        float half = Extent * 0.5f;
        for (int z = 0; z < n; z++)
        {
            float wz = z * CellSize - half;
            for (int x = 0; x < n; x++)
            {
                float wx = x * CellSize - half;
                float h = _field.Sample(wx, wz);
                _heights[z * n + x] = h / CellSize;   // shape space
                if (h < _minHeight) _minHeight = h;
                if (h > _maxHeight) _maxHeight = h;
            }
        }

        _terrainShape.MapWidth = n;
        _terrainShape.MapDepth = n;
        _terrainShape.MapData = _heights;

        _terrainMesh.Mesh = BuildTerrainMesh();
        _terrainMesh.MaterialOverride = _dressing.CreateTerrainMaterial();

        Bounds = new Aabb(new Vector3(-half, _minHeight, -half), new Vector3(Extent, _maxHeight - _minHeight, Extent));
        KillPlaneY = _minHeight - 120f;
        SpawnPoint = _field.SpawnXZ with { Y = SampleHeight(_field.SpawnXZ.X, _field.SpawnXZ.Z) + 4f };

        _dressing.Rebuild();
        GD.Print($"[RUSHCORE] World built seed={Seed} in {Time.GetTicksMsec() - start} ms " +
                 $"({Extent:0} m across, height {_minHeight:0.0}..{_maxHeight:0.0} m)");
    }

    public void Regenerate(int seed)
    {
        Seed = seed;
        Build();
    }

    /// <summary>Authoritative height source. Bilinear over the same grid the collider uses.</summary>
    public float SampleHeight(float x, float z)
    {
        float half = Extent * 0.5f;
        float fx = Mathf.Clamp((x + half) / CellSize, 0f, Samples - 1.001f);
        float fz = Mathf.Clamp((z + half) / CellSize, 0f, Samples - 1.001f);
        int x0 = (int)fx, z0 = (int)fz;
        int x1 = Mathf.Min(x0 + 1, Samples - 1), z1 = Mathf.Min(z0 + 1, Samples - 1);
        float tx = fx - x0, tz = fz - z0;
        float h00 = _heights[z0 * Samples + x0], h10 = _heights[z0 * Samples + x1];
        float h01 = _heights[z1 * Samples + x0], h11 = _heights[z1 * Samples + x1];
        return Mathf.Lerp(Mathf.Lerp(h00, h10, tx), Mathf.Lerp(h01, h11, tx), tz) * CellSize;
    }

    public Vector3 SurfacePoint(float x, float z, float above = 0f) => new(x, SampleHeight(x, z) + above, z);

    private ArrayMesh BuildTerrainMesh()
    {
        int n = Samples;
        int quads = (n - 1) * (n - 1);
        int vertCount = quads * 6;                    // non-indexed: flat/faceted normals (06 §4)
        var verts = new Vector3[vertCount];
        var norms = new Vector3[vertCount];
        var colors = new Color[vertCount];

        float half = Extent * 0.5f;
        int w = 0;
        Span<Vector3> tri = stackalloc Vector3[3];

        for (int z = 0; z < n - 1; z++)
        {
            for (int x = 0; x < n - 1; x++)
            {
                float x0 = x * CellSize - half, x1 = x0 + CellSize;
                float z0 = z * CellSize - half, z1 = z0 + CellSize;
                Vector3 a = new(x0, _heights[z * n + x] * CellSize, z0);
                Vector3 b = new(x1, _heights[z * n + x + 1] * CellSize, z0);
                Vector3 c = new(x0, _heights[(z + 1) * n + x] * CellSize, z1);
                Vector3 d = new(x1, _heights[(z + 1) * n + x + 1] * CellSize, z1);

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
