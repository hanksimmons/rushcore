using Godot;
using Rushcore.Tuning;

namespace Rushcore.Player;

/// <summary>
/// Transient player-local effects driven purely by <see cref="PlayerPhysics"/>
/// events and public state (06 §10). Owns no gameplay authority.
///
/// Every emitter is created once in <see cref="_Ready"/>; the physics-step event
/// handlers only set flags. Counts are small and lifetimes short so effects never
/// hide the landing surface (08 acceptance).
/// </summary>
public partial class PlayerVfx : Node3D
{
    private readonly GameplayTuning _t;
    private readonly PlayerPhysics _player;

    // Shared draw meshes: a billboarded quad for dust/soft puffs, a chunk for
    // low-poly debris and streaks (06 §1 keeps the N64 language).
    private QuadMesh _quad = null!;
    private BoxMesh _chunk = null!;

    private GpuParticles3D _dust = null!, _trail = null!, _charge = null!;
    private GpuParticles3D _slam = null!, _slamPerfect = null!;
    private GpuParticles3D _jumpBurst = null!, _apexRing = null!, _landBurst = null!;

    private ParticleProcessMaterial _dustPm = null!, _trailPm = null!, _chargePm = null!;
    private ParticleProcessMaterial _slamPm = null!, _slamPerfectPm = null!;
    private ParticleProcessMaterial _jumpPm = null!, _apexPm = null!, _landPm = null!;

    private float _lastRadius = -1f;
    private Vector3 _travelDir = Vector3.Forward;

    // Pending one-shots: set in physics-step handlers, fired in _Process.
    private bool _pendingJump, _pendingApex, _pendingLand;
    private float _pendingJumpCharge;
    private float _pendingLandImpact;
    private bool _pendingLandSlam, _pendingLandPerfect;

    public PlayerVfx(GameplayTuning tuning, PlayerPhysics player)
    {
        _t = tuning;
        _player = player;
    }

    protected GameplayTuning Tuning => _t;
    protected PlayerPhysics Player => _player;

    public override void _Ready()
    {
        Name = "PlayerVfx";
        PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off;

        _quad = new QuadMesh { Size = new Vector2(1f, 1f) };
        _chunk = new BoxMesh { Size = new Vector3(1f, 1f, 1f) };

        var softMix = SoftMaterial(billboard: true, additive: false);
        var softAdd = SoftMaterial(billboard: true, additive: true);
        var chunkMix = SoftMaterial(billboard: false, additive: false);
        var chunkAdd = SoftMaterial(billboard: false, additive: true);

        BuildDust(softMix);
        BuildTrail(chunkAdd);
        BuildCharge(softAdd);
        BuildSlamStreaks(chunkMix, chunkAdd);
        BuildJumpBurst(softMix);
        BuildApexRing(chunkAdd);
        BuildLandBurst(softMix);

        ApplyRadius(Mathf.Max(0.05f, _t.Movement.BallRadius));

        _player.Jumped += OnJumped;
        _player.Slammed += OnSlammed;
        _player.Landed += OnLanded;
    }

    public override void _ExitTree()
    {
        _player.Jumped -= OnJumped;
        _player.Slammed -= OnSlammed;
        _player.Landed -= OnLanded;
    }

    // ---------------- events (physics step: flags only) ----------------

    private void OnJumped(float charge01)
    {
        _pendingJumpCharge = charge01;
        _pendingJump = true;
    }

    private void OnSlammed(bool perfect)
    {
        if (perfect) _pendingApex = true;
    }

    private void OnLanded(float impactSpeed, bool wasSlam, bool wasPerfectApexSlam)
    {
        _pendingLandImpact = impactSpeed;
        _pendingLandSlam = wasSlam;
        _pendingLandPerfect = wasPerfectApexSlam;
        _pendingLand = true;
    }

    // ---------------- per-frame drive ----------------

    public override void _Process(double delta)
    {
        var vfx = _t.Vfx;
        float r = Mathf.Max(0.05f, _t.Movement.BallRadius);
        if (!Mathf.IsEqualApprox(r, _lastRadius)) ApplyRadius(r);

        float cap = Mathf.Max(1f, _t.Movement.HardMaxLocomotionSpeed);
        float speed = _player.LocomotionSpeed;
        float speed01 = Mathf.Clamp(speed / cap, 0f, 1f);
        bool grounded = _player.IsGrounded;

        Vector3 v = _player.Velocity;
        Vector3 flat = new(v.X, 0f, v.Z);
        float flatSpeed = flat.Length();
        if (flatSpeed > 0.1f) _travelDir = flat / flatSpeed;

        float dustI = Str(vfx.DustIntensity);
        float trailI = Str(vfx.TrailIntensity);
        float chargeI = Str(vfx.ChargeEffectStrength);
        float slamI = Str(vfx.SlamEffectStrength);

        // rolling ground contact
        bool dustOn = grounded && speed > 2f && dustI > 0.01f;
        Emit(_dust, dustOn);
        if (dustOn) _dust.AmountRatio = Mathf.Clamp(speed01 * 2.0f * dustI, 0.12f, 1f);

        // boost trail, thrown backwards along travel
        bool trailOn = _player.BoostActive && trailI > 0.01f;
        Emit(_trail, trailOn);
        if (trailOn)
        {
            _trail.AmountRatio = Mathf.Clamp((0.5f + 0.5f * speed01) * trailI, 0.15f, 1f);
            Vector3 back = -_travelDir;
            Vector3 right = Vector3.Up.Cross(back);
            if (right.LengthSquared() > 1e-6f)
                _trail.Basis = new Basis(right.Normalized(), Vector3.Up, back);
        }

        // charge build-up at the contact ring
        bool chargeOn = _player.IsCharging && chargeI > 0.01f;
        Emit(_charge, chargeOn);
        if (chargeOn) _charge.AmountRatio = Mathf.Clamp((0.25f + 0.75f * _player.Charge01) * chargeI, 0.1f, 1f);

        // slam streaks: two separate emitters so a perfect apex is never confusable
        bool slamming = _player.SlamActive && slamI > 0.01f;
        bool perfect = slamming && _player.LastSlamWasPerfect;
        Emit(_slam, slamming && !perfect);
        Emit(_slamPerfect, slamming && perfect);

        FirePendingBursts(r);
    }

    private void FirePendingBursts(float r)
    {
        var vfx = _t.Vfx;

        if (_pendingJump)
        {
            _pendingJump = false;
            float s = Str(vfx.JumpReleaseStrength);
            if (s > 0.01f)
            {
                float c = Mathf.Clamp(_pendingJumpCharge, 0f, 1f);
                _jumpPm.InitialVelocityMin = (2f + 5f * c) * r;
                _jumpPm.InitialVelocityMax = (5f + 11f * c) * r;
                _jumpBurst.AmountRatio = Mathf.Clamp((0.25f + 0.75f * c) * s, 0.1f, 1f);
                _jumpBurst.Restart();
            }
        }

        if (_pendingApex)
        {
            _pendingApex = false;
            float s = Str(vfx.SlamEffectStrength);
            if (s > 0.01f)
            {
                _apexRing.AmountRatio = Mathf.Clamp(s, 0.2f, 1f);
                _apexRing.Restart();
            }
        }

        if (_pendingLand)
        {
            _pendingLand = false;
            float s = Str(vfx.ImpactEffectStrength);
            if (s > 0.01f)
            {
                float impact = Mathf.Clamp(_pendingLandImpact / 30f, 0f, 1.5f);
                float mult = _pendingLandPerfect ? 2.0f : _pendingLandSlam ? 1.4f : 1f;
                _landPm.InitialVelocityMin = (2f + 8f * impact) * mult * r;
                _landPm.InitialVelocityMax = (4f + 16f * impact) * mult * r;
                _landBurst.AmountRatio = Mathf.Clamp((0.2f + 0.8f * impact) * mult * s, 0.1f, 1f);
                _landBurst.Restart();
            }
        }
    }

    private static void Emit(GpuParticles3D e, bool on)
    {
        if (e.Emitting != on) e.Emitting = on;
    }

    private static float Str(float v) => Mathf.Max(0f, v);

    /// <summary>Everything sized off the live collider radius, re-applied only when it changes.</summary>
    private void ApplyRadius(float r)
    {
        _lastRadius = r;
        Vector3 contact = new(0f, -r * 0.92f, 0f);

        _dust.Position = contact;
        _dustPm.EmissionRingRadius = r * 1.0f;
        _dustPm.EmissionRingInnerRadius = r * 0.45f;
        _dustPm.EmissionRingHeight = r * 0.1f;
        _dustPm.ScaleMin = 0.10f * r;
        _dustPm.ScaleMax = 0.30f * r;

        _trailPm.EmissionSphereRadius = r * 0.7f;
        _trailPm.ScaleMin = 0.10f * r;
        _trailPm.ScaleMax = 0.26f * r;

        _charge.Position = contact;
        _chargePm.EmissionRingRadius = r * 1.5f;
        _chargePm.EmissionRingInnerRadius = r * 0.9f;
        _chargePm.EmissionRingHeight = r * 0.05f;
        _chargePm.ScaleMin = 0.10f * r;
        _chargePm.ScaleMax = 0.26f * r;

        _slamPm.EmissionSphereRadius = r * 0.6f;
        _slamPm.ScaleMin = 0.10f * r;
        _slamPm.ScaleMax = 0.22f * r;

        _slamPerfectPm.EmissionSphereRadius = r * 0.8f;
        _slamPerfectPm.ScaleMin = 0.18f * r;
        _slamPerfectPm.ScaleMax = 0.42f * r;

        _jumpBurst.Position = contact;
        _jumpPm.EmissionRingRadius = r * 1.1f;
        _jumpPm.EmissionRingInnerRadius = r * 0.3f;
        _jumpPm.EmissionRingHeight = r * 0.05f;
        _jumpPm.ScaleMin = 0.14f * r;
        _jumpPm.ScaleMax = 0.36f * r;

        // Fired at slam start, in mid-air: the shockwave reads from the ball centre.
        _apexRing.Position = Vector3.Zero;
        _apexPm.EmissionRingRadius = r * 0.6f;
        _apexPm.EmissionRingInnerRadius = r * 0.3f;
        _apexPm.EmissionRingHeight = r * 0.05f;
        _apexPm.ScaleMin = 0.20f * r;
        _apexPm.ScaleMax = 0.45f * r;

        _landBurst.Position = contact;
        _landPm.EmissionRingRadius = r * 0.9f;
        _landPm.EmissionRingInnerRadius = r * 0.25f;
        _landPm.EmissionRingHeight = r * 0.05f;
        _landPm.ScaleMin = 0.16f * r;
        _landPm.ScaleMax = 0.42f * r;
    }

    // ---------------- construction ----------------

    private void BuildDust(Material draw)
    {
        _dustPm = Ring(new Vector3(0f, 1f, 0f), 45f, 1.2f, 3.0f, new Vector3(0f, -3f, 0f),
                       Ramp(new Color(0.76f, 0.71f, 0.60f), 0.55f));
        _dustPm.DampingMin = 1.5f;
        _dustPm.DampingMax = 3.0f;
        _dust = Emitter("RollDust", 64, 0.50f, false, _dustPm, _quad, draw);
    }

    private void BuildTrail(Material draw)
    {
        // Emitter basis is re-aimed every frame, so +Z is "behind the ball".
        _trailPm = Sphere(new Vector3(0f, 0f, 1f), 14f, 3.0f, 8.0f, new Vector3(0f, -1.5f, 0f),
                          Ramp(new Color(1.00f, 0.62f, 0.22f), 0.95f));
        _trailPm.DampingMin = 3f;
        _trailPm.DampingMax = 6f;
        _trailPm.AngularVelocityMin = -240f;
        _trailPm.AngularVelocityMax = 240f;
        _trail = Emitter("BoostTrail", 80, 0.42f, false, _trailPm, _chunk, draw);
    }

    private void BuildCharge(Material draw)
    {
        // Motes rising off the contact ring: the ball is winding up and is committed.
        _chargePm = Ring(new Vector3(0f, 1f, 0f), 8f, 2.0f, 4.5f, new Vector3(0f, 1.5f, 0f),
                         Ramp(new Color(1.00f, 0.88f, 0.45f), 0.95f));
        _chargePm.DampingMin = 1f;
        _chargePm.DampingMax = 2f;
        _charge = Emitter("ChargeBuild", 64, 0.40f, false, _chargePm, _quad, draw);
    }

    private void BuildSlamStreaks(Material chunkMix, Material chunkAdd)
    {
        // Parent body never rotates, so local -Y is world down.
        _slamPm = Sphere(new Vector3(0f, -1f, 0f), 6f, 6f, 14f, Vector3.Zero,
                         Ramp(new Color(0.45f, 0.52f, 1.00f), 0.90f));
        _slam = Emitter("SlamStreak", 72, 0.30f, false, _slamPm, _chunk, chunkMix);

        _slamPerfectPm = Sphere(new Vector3(0f, -1f, 0f), 4f, 14f, 26f, Vector3.Zero,
                                Ramp(new Color(1.00f, 0.95f, 0.72f), 1.00f));
        _slamPerfect = Emitter("SlamStreakPerfect", 110, 0.34f, false, _slamPerfectPm, _chunk, chunkAdd);
    }

    private void BuildJumpBurst(Material draw)
    {
        _jumpPm = Ring(new Vector3(0f, 1f, 0f), 55f, 3f, 9f, new Vector3(0f, -8f, 0f),
                       Ramp(new Color(1.00f, 0.82f, 0.42f), 0.85f));
        _jumpPm.DampingMin = 2f;
        _jumpPm.DampingMax = 4f;
        _jumpBurst = Emitter("JumpReleaseBurst", 72, 0.42f, true, _jumpPm, _quad, draw);
    }

    private void BuildApexRing(Material draw)
    {
        // Flat outward shockwave: horizontal so it never covers the ground ahead.
        _apexPm = Ring(new Vector3(1f, 0f, 0f), 180f, 13f, 20f, new Vector3(0f, -4f, 0f),
                       Ramp(new Color(1.00f, 0.96f, 0.78f), 1.00f));
        _apexPm.Flatness = 1f;
        _apexPm.DampingMin = 6f;
        _apexPm.DampingMax = 12f;
        _apexRing = Emitter("PerfectApexRing", 120, 0.45f, true, _apexPm, _chunk, draw);
    }

    private void BuildLandBurst(Material draw)
    {
        _landPm = Ring(new Vector3(1f, 0f, 0f), 180f, 4f, 12f, new Vector3(0f, -8f, 0f),
                       Ramp(new Color(0.82f, 0.78f, 0.68f), 0.75f));
        _landPm.Flatness = 0.9f;
        _landPm.DampingMin = 3f;
        _landPm.DampingMax = 7f;
        _landBurst = Emitter("LandBurst", 96, 0.38f, true, _landPm, _quad, draw);
    }

    private GpuParticles3D Emitter(string name, int amount, float lifetime, bool oneShot,
                                   ParticleProcessMaterial pm, Mesh mesh, Material draw)
    {
        var e = new GpuParticles3D
        {
            Name = name,
            Amount = amount,
            Lifetime = lifetime,
            OneShot = oneShot,
            Explosiveness = oneShot ? 1f : 0f,
            Randomness = 0.35f,
            Emitting = false,
            LocalCoords = false,          // particles stay behind in world space
            ProcessMaterial = pm,
            DrawPass1 = mesh,
            MaterialOverride = draw,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            VisibilityAabb = new Aabb(new Vector3(-40f, -40f, -40f), new Vector3(80f, 80f, 80f)),
        };
        AddChild(e);
        return e;
    }

    private static ParticleProcessMaterial Base(Vector3 direction, float spread,
                                                float vMin, float vMax, Vector3 gravity, Texture2D ramp)
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

    private static ParticleProcessMaterial Ring(Vector3 direction, float spread,
                                                float vMin, float vMax, Vector3 gravity, Texture2D ramp)
    {
        var pm = Base(direction, spread, vMin, vMax, gravity, ramp);
        pm.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Ring;
        pm.EmissionRingAxis = Vector3.Up;
        pm.EmissionRingRadius = 1f;
        pm.EmissionRingInnerRadius = 0.5f;
        pm.EmissionRingHeight = 0.1f;
        return pm;
    }

    private static ParticleProcessMaterial Sphere(Vector3 direction, float spread,
                                                  float vMin, float vMax, Vector3 gravity, Texture2D ramp)
    {
        var pm = Base(direction, spread, vMin, vMax, gravity, ramp);
        pm.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        pm.EmissionSphereRadius = 0.5f;
        return pm;
    }

    /// <summary>Colour ramp that fades to nothing, so nothing lingers over the surface.</summary>
    private static GradientTexture1D Ramp(Color color, float alpha)
    {
        var g = new Gradient();
        g.SetColor(0, new Color(color, alpha));
        g.SetColor(1, new Color(color, 0f));
        return new GradientTexture1D { Gradient = g, Width = 32 };
    }

    private static StandardMaterial3D SoftMaterial(bool billboard, bool additive) => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        VertexColorUseAsAlbedo = true,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        BlendMode = additive ? BaseMaterial3D.BlendModeEnum.Add : BaseMaterial3D.BlendModeEnum.Mix,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        BillboardMode = billboard
            ? BaseMaterial3D.BillboardModeEnum.Particles
            : BaseMaterial3D.BillboardModeEnum.Disabled,
        BillboardKeepScale = billboard,
    };
}
