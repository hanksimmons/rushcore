using Godot;

namespace Rushcore.Vfx;

/// <summary>What a world one-shot is announcing (06 §10's list, minus the player-local effects).</summary>
public enum WorldVfxKind
{
    /// <summary>An enemy went under the ball: shards thrown outward and a flat ring on the ground.</summary>
    Crush,
    /// <summary>The ball hit something it could not crush: a dull dark puff, no sparkle, no ring of light.</summary>
    FailedImpact,
    /// <summary>Something was collected: a small sparkle rising off the spot.</summary>
    Pickup,
    /// <summary>An exit pad was reached: a tall ring pulse climbing off the pad.</summary>
    Exit,
}

/// <summary>
/// One-shot world effects (06 §10), built on <see cref="Rushcore.Player.PlayerVfx"/>'s pattern: every emitter,
/// mesh and material is created once in <see cref="_Ready"/> and nothing is allocated at fire time.
///
/// <para>Owns no gameplay authority whatever: it is told what happened and where, and draws it. A small
/// fixed pool of <see cref="PoolSize"/> slots is reused round-robin, which is the one place 05 §15 allows
/// pooling — a burst here is routine. Effects are short and thrown outward so they never sit over the
/// surface the player has to land on (08 §9).</para>
/// </summary>
public partial class WorldVfx : Node3D
{
    /// <summary>Concurrent one-shots. Nine at once reuses the oldest slot rather than allocating a tenth.</summary>
    public const int PoolSize = 8;

    private sealed class Slot
    {
        public GpuParticles3D Particles = null!;
        public MeshInstance3D Ring = null!;
        public StandardMaterial3D RingMaterial = null!;
        public float Age = -1f;                 // < 0 = free
        public float Life;
        public float RingFrom, RingTo, RingRise, RingFlat;
        public Color RingColor;
    }

    private readonly Slot[] _slots = new Slot[PoolSize];
    private int _next;

    private readonly Dictionary<WorldVfxKind, ParticleProcessMaterial> _process = new();
    private readonly Dictionary<WorldVfxKind, Mesh> _draw = new();
    private readonly Dictionary<WorldVfxKind, Material> _drawMaterial = new();
    private readonly Dictionary<WorldVfxKind, float> _lifetime = new();
    private readonly Dictionary<WorldVfxKind, float> _amount = new();

    private QuadMesh _quad = null!;
    private BoxMesh _chunk = null!;
    private StandardMaterial3D _softMix = null!, _chunkMix = null!, _softAdd = null!;

    /// <summary>How many slots are currently drawing something (the harness reads it).</summary>
    public int ActiveCount { get; private set; }

    /// <summary>Total one-shots fired since the node was built; the harness counts them, nothing else reads it.</summary>
    public int PlayedCount { get; private set; }

    public override void _Ready()
    {
        Name = "WorldVfx";
        PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off;

        _quad = new QuadMesh { Size = new Vector2(1f, 1f) };
        _chunk = new BoxMesh { Size = Vector3.One };
        _softMix = Soft(billboard: true, additive: false);
        _softAdd = Soft(billboard: true, additive: true);
        _chunkMix = Soft(billboard: false, additive: false);

        BuildKinds();

        for (int i = 0; i < PoolSize; i++)
        {
            var slot = new Slot();
            slot.Particles = new GpuParticles3D
            {
                Name = $"OneShot{i}",
                Amount = 64,
                Lifetime = 0.6f,
                OneShot = true,
                Explosiveness = 1f,
                Randomness = 0.35f,
                Emitting = false,
                LocalCoords = false,
                ProcessMaterial = _process[WorldVfxKind.Crush],
                DrawPass1 = _chunk,
                MaterialOverride = _chunkMix,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                TopLevel = true,
                VisibilityAabb = new Aabb(new Vector3(-40f, -40f, -40f), new Vector3(80f, 80f, 80f)),
            };
            AddChild(slot.Particles);

            slot.RingMaterial = RingMaterial();
            slot.Ring = new MeshInstance3D
            {
                Name = $"OneShotRing{i}",
                Mesh = PlaceholderPalette.ThinRing,
                MaterialOverride = slot.RingMaterial,
                TopLevel = true,
                Visible = false,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(slot.Ring);
            _slots[i] = slot;
        }
    }

    /// <summary>
    /// Fire one shot at a world point. Never allocates: the slot, its emitter and its ring already exist,
    /// and the only work is swapping the prebuilt process material in and restarting.
    /// </summary>
    public void Play(WorldVfxKind kind, Vector3 at)
    {
        Slot slot = TakeSlot();
        PlayedCount++;

        var p = slot.Particles;
        p.ProcessMaterial = _process[kind];
        p.DrawPass1 = _draw[kind];
        p.MaterialOverride = _drawMaterial[kind];
        p.Lifetime = _lifetime[kind];
        p.AmountRatio = _amount[kind];
        p.GlobalPosition = at;
        p.Restart();

        (slot.RingFrom, slot.RingTo, slot.RingRise, slot.RingFlat, slot.RingColor) = kind switch
        {
            // from, to, rise, flatness, colour
            WorldVfxKind.Crush => (1.5f, 14f, 0.6f, 0.10f, new Color(1.00f, 0.86f, 0.55f)),
            WorldVfxKind.FailedImpact => (1.0f, 4.5f, 0.4f, 0.25f, new Color(0.42f, 0.40f, 0.38f)),
            WorldVfxKind.Exit => (4f, 26f, 24f, 0.06f, new Color(0.62f, 1.00f, 0.72f)),
            _ => (0f, 0f, 0f, 0f, Colors.White),
        };
        slot.Life = _lifetime[kind];
        slot.Age = 0f;
        slot.Ring.GlobalPosition = at;
        slot.Ring.Visible = slot.RingTo > 0f;
        RecountActive();
    }

    /// <summary>Round-robin, and a ninth concurrent shot takes the oldest slot rather than being dropped.</summary>
    private Slot TakeSlot()
    {
        for (int i = 0; i < PoolSize; i++)
        {
            Slot s = _slots[(_next + i) % PoolSize];
            if (s.Age >= 0f) continue;
            _next = (_next + i + 1) % PoolSize;
            return s;
        }
        Slot oldest = _slots[0];
        for (int i = 1; i < PoolSize; i++) if (_slots[i].Age > oldest.Age) oldest = _slots[i];
        return oldest;
    }

    public override void _Process(double delta)
    {
        if (ActiveCount == 0) return;
        float dt = (float)delta;
        for (int i = 0; i < PoolSize; i++)
        {
            Slot s = _slots[i];
            if (s.Age < 0f) continue;
            s.Age += dt;
            float t01 = s.Age / Mathf.Max(0.05f, s.Life);
            if (t01 >= 1f)
            {
                s.Age = -1f;
                s.Ring.Visible = false;
                continue;
            }
            if (!s.Ring.Visible) continue;

            float ease = 1f - (1f - t01) * (1f - t01);
            float r = Mathf.Lerp(s.RingFrom, s.RingTo, ease);
            s.Ring.Scale = new Vector3(r, Mathf.Max(0.05f, r * s.RingFlat), r);
            if (s.RingRise > 0.01f) s.Ring.Position += Vector3.Up * (s.RingRise * dt / Mathf.Max(0.05f, s.Life));
            s.RingMaterial.AlbedoColor = new Color(s.RingColor, 0.85f * Mathf.Pow(1f - t01, 1.5f));
        }
        RecountActive();
    }

    private void RecountActive()
    {
        int n = 0;
        for (int i = 0; i < PoolSize; i++) if (_slots[i].Age >= 0f) n++;
        ActiveCount = n;
    }

    // ---------------- construction ----------------

    private void BuildKinds()
    {
        // Crush: bright shards thrown up and out, gone in half a second (06 §10 "enemy crush").
        var crush = Base(Vector3.Up, 70f, 9f, 26f, new Vector3(0f, -34f, 0f), Ramp(new Color(1.00f, 0.88f, 0.52f), 1.0f));
        crush.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        crush.EmissionSphereRadius = 0.8f;
        crush.AngularVelocityMin = -540f;
        crush.AngularVelocityMax = 540f;
        crush.ScaleMin = 0.18f;
        crush.ScaleMax = 0.55f;
        Register(WorldVfxKind.Crush, crush, _chunk, _chunkMix, 0.55f, 1.0f);

        // Failed impact: a dull dark puff that hangs and sinks. Deliberately unrewarding to look at.
        var fail = Base(Vector3.Up, 55f, 2f, 7f, new Vector3(0f, -3f, 0f), Ramp(new Color(0.30f, 0.29f, 0.28f), 0.7f));
        fail.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        fail.EmissionSphereRadius = 0.9f;
        fail.DampingMin = 4f;
        fail.DampingMax = 8f;
        fail.ScaleMin = 0.5f;
        fail.ScaleMax = 1.4f;
        Register(WorldVfxKind.FailedImpact, fail, _quad, _softMix, 0.5f, 0.7f);

        // Pickup: a few motes rising off the spot. Small on purpose: collection is constant (06 §10).
        var pick = Base(Vector3.Up, 22f, 3f, 7f, new Vector3(0f, 1.5f, 0f), Ramp(new Color(1.00f, 0.86f, 0.42f), 1.0f));
        pick.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        pick.EmissionSphereRadius = 0.4f;
        pick.ScaleMin = 0.10f;
        pick.ScaleMax = 0.26f;
        Register(WorldVfxKind.Pickup, pick, _quad, _softAdd, 0.45f, 0.28f);

        // Exit: motes climbing off the pad under the tall ring. The stage is over; it should read from far off.
        var exit = Base(Vector3.Up, 16f, 14f, 30f, new Vector3(0f, -4f, 0f), Ramp(new Color(0.66f, 1.00f, 0.76f), 1.0f));
        exit.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Ring;
        exit.EmissionRingAxis = Vector3.Up;
        exit.EmissionRingRadius = 5f;
        exit.EmissionRingInnerRadius = 3.5f;
        exit.EmissionRingHeight = 0.4f;
        exit.ScaleMin = 0.3f;
        exit.ScaleMax = 0.9f;
        Register(WorldVfxKind.Exit, exit, _quad, _softAdd, 1.0f, 1.0f);
    }

    private void Register(WorldVfxKind kind, ParticleProcessMaterial pm, Mesh mesh, Material draw, float lifetime, float amount)
    {
        _process[kind] = pm;
        _draw[kind] = mesh;
        _drawMaterial[kind] = draw;
        _lifetime[kind] = lifetime;
        _amount[kind] = amount;
    }

    private static ParticleProcessMaterial Base(Vector3 direction, float spread, float vMin, float vMax, Vector3 gravity, Texture2D ramp)
        => new()
        {
            Direction = direction,
            Spread = spread,
            InitialVelocityMin = vMin,
            InitialVelocityMax = vMax,
            Gravity = gravity,
            ColorRamp = ramp,
            AngleMin = -180f,
            AngleMax = 180f,
            ScaleMin = 0.1f,
            ScaleMax = 0.3f,
        };

    private static GradientTexture1D Ramp(Color color, float alpha)
    {
        var g = new Gradient();
        g.SetColor(0, new Color(color, alpha));
        g.SetColor(1, new Color(color, 0f));
        return new GradientTexture1D { Gradient = g, Width = 32 };
    }

    private static StandardMaterial3D Soft(bool billboard, bool additive) => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        VertexColorUseAsAlbedo = true,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        BlendMode = additive ? BaseMaterial3D.BlendModeEnum.Add : BaseMaterial3D.BlendModeEnum.Mix,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        BillboardMode = billboard ? BaseMaterial3D.BillboardModeEnum.Particles : BaseMaterial3D.BillboardModeEnum.Disabled,
        BillboardKeepScale = billboard,
    };

    private static StandardMaterial3D RingMaterial() => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        BlendMode = BaseMaterial3D.BlendModeEnum.Add,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        AlbedoColor = new Color(1f, 1f, 1f, 0f),
    };
}
