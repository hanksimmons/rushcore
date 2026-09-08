using Godot;
using Rushcore.Tuning;

namespace Rushcore.Player;

/// <summary>
/// Transient player-local effects driven purely by <see cref="PlayerPhysics"/>
/// events and public state (06 §10). Owns no gameplay authority.
///
/// Every emitter is created once in <see cref="_Ready"/>; the physics-step event
/// handlers only set flags. Counts are small and lifetimes short so effects never
/// hide the landing surface (08 acceptance). The landing burst (D-077) adds blue
/// sparks, a ground shock ring and an air-parting bow ring ahead of the ball.
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
    private GpuParticles3D _slam = null!;
    private GpuParticles3D _jumpBurst = null!, _landBurst = null!, _sparks = null!;
    private GpuParticles3D _carveRocks = null!, _carveSpray = null!;

    private ParticleProcessMaterial _dustPm = null!, _trailPm = null!, _chargePm = null!;
    private ParticleProcessMaterial _slamPm = null!;
    private ParticleProcessMaterial _jumpPm = null!, _landPm = null!, _sparksPm = null!;
    private ParticleProcessMaterial _carveRocksPm = null!, _carveSprayPm = null!;
    private static readonly Color RockColor = new(0.50f, 0.45f, 0.39f);
    private static readonly Color SprayColor = new(0.72f, 0.66f, 0.55f);

    /// <summary>True while the carve debris is being thrown (the harness reads it).</summary>
    public bool CarveDebrisActive { get; private set; }

    // Sonic-boom rings (mesh, not particles, so the shape stays a clean ring at speed).
    private MeshInstance3D _boomRing = null!, _bowRing = null!;
    private StandardMaterial3D _boomMat = null!, _bowMat = null!;
    private float _burstAge = -1f;
    private Vector3 _burstOrigin;
    private const float BurstEffectSeconds = 0.42f;

    // Damage (06 §10): a red-edged pulse on the ball itself. There is no damage source yet (T2's health is a
    // value); this is the cue the impact model will fire, built now so the main track only has to call it.
    private GpuParticles3D _damage = null!;
    private ParticleProcessMaterial _damagePm = null!;
    private MeshInstance3D _damageFlash = null!;
    private StandardMaterial3D _damageMat = null!;
    private float _damageAge = -1f;
    private const float DamageEffectSeconds = 0.30f;
    private static readonly Color DamageColor = new(1.00f, 0.24f, 0.20f);
    private static readonly Color SparkColor = new(0.40f, 0.78f, 1.00f);
    private static readonly Color BoomColor = new(0.72f, 0.90f, 1.00f);

    private float _lastRadius = -1f;
    private Vector3 _travelDir = Vector3.Forward;

    // Pending one-shots: set in physics-step handlers, fired in _Process.
    private bool _pendingJump, _pendingLand, _pendingBurst, _pendingDamage;
    private float _pendingJumpCharge;
    private float _pendingLandImpact;
    private bool _pendingLandSlam;

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
        BuildSlamStreak(chunkMix);
        BuildJumpBurst(softMix);
        BuildLandBurst(softMix);
        BuildBurst(chunkAdd);
        BuildDamage(softAdd);
        BuildCarve(chunkMix, softMix);

        ApplyRadius(Mathf.Max(0.05f, _t.Movement.BallRadius));

        _player.Jumped += OnJumped;
        _player.Landed += OnLanded;
        _player.LandingBurst += OnLandingBurst;
    }

    public override void _ExitTree()
    {
        _player.Jumped -= OnJumped;
        _player.Landed -= OnLanded;
        _player.LandingBurst -= OnLandingBurst;
    }

    // ---------------- events (physics step: flags only) ----------------

    private void OnJumped(float charge01)
    {
        _pendingJumpCharge = charge01;
        _pendingJump = true;
    }

    private void OnLanded(float impactSpeed, bool wasSlam)
    {
        _pendingLandImpact = impactSpeed;
        _pendingLandSlam = wasSlam;
        _pendingLand = true;
    }

    private void OnLandingBurst(float speed) => _pendingBurst = true;

    /// <summary>
    /// The player took damage (06 §10, T3): a short red flash around the ball and a spray of red motes off it.
    /// Player-local and 0.3 s long, so it never sits over the surface the ball is about to land on (08 §9).
    /// Fired by the debug action today and by the impact model when it lands.
    /// </summary>
    public void PlayDamage() => _pendingDamage = true;

    /// <summary>True while the damage pulse is drawing (the harness reads it).</summary>
    public bool DamagePulseActive => _damageAge >= 0f;

    // ---------------- per-frame drive ----------------

    public override void _Process(double delta)
    {
        float dt = (float)delta;
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
        if (dustOn) _dust.AmountRatio = _player.IsCarving ? 1f : Mathf.Clamp(speed01 * 2.0f * dustI, 0.12f, 1f);

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

        // slam streak while committed downward
        Emit(_slam, _player.SlamActive && slamI > 0.01f);

        UpdateCarveDebris(r, speed);

        FirePendingBursts(r);
        AnimateBurst(dt, r);
        AnimateDamage(dt, r);
    }

    /// <summary>
    /// Carve debris (03 §11, 06 §7, D-089): while the ball slides, rocks and a spray of ground
    /// are thrown from the contact point toward the OUTSIDE of the corner (the side the ball is
    /// skidding toward, away from where it faces), trailing back and up. Rate and speed rise with
    /// the carve angle and the ball's speed, so a hot, deep carve reads from far away.
    /// </summary>
    private void UpdateCarveDebris(float r, float speed)
    {
        float s = Str(_t.Vfx.CarveEffectStrength);
        bool on = _player.IsCarving && _player.IsGrounded && s > 0.01f && speed > 1f;
        Vector3 facing = new(_player.Facing.X, 0f, _player.Facing.Z);
        Vector3 outward = Vector3.Zero;
        if (on && facing.LengthSquared() > 1e-6f)
        {
            facing = facing.Normalized();
            outward = _travelDir - facing * _travelDir.Dot(facing);      // travel's component across the facing: the skid side
            if (outward.LengthSquared() < 1e-4f) on = false;              // no angle yet: nothing to throw
            else outward = outward.Normalized();
        }
        CarveDebrisActive = on;
        Emit(_carveRocks, on);
        Emit(_carveSpray, on);
        if (!on) return;

        float angle01 = Mathf.Clamp(_player.CarveAngleDegrees / 45f, 0f, 1f);
        Vector3 dir = (outward * 1.0f - _travelDir * 0.45f + Vector3.Up * 0.55f).Normalized();
        Basis aim = Basis.LookingAt(dir, Vector3.Up);                     // -Z = throw direction
        Vector3 origin = new Vector3(0f, -r * 0.85f, 0f) + outward * r * 0.7f;

        _carveRocks.Basis = aim;
        _carveRocks.Position = origin;
        _carveRocksPm.InitialVelocityMin = 4f + 0.12f * speed;
        _carveRocksPm.InitialVelocityMax = 10f + 0.30f * speed;
        _carveRocks.AmountRatio = Mathf.Clamp((0.35f + 0.65f * angle01) * s, 0.15f, 1f);

        _carveSpray.Basis = aim;
        _carveSpray.Position = origin;
        _carveSprayPm.InitialVelocityMin = 6f + 0.15f * speed;
        _carveSprayPm.InitialVelocityMax = 14f + 0.35f * speed;
        _carveSpray.AmountRatio = Mathf.Clamp((0.4f + 0.6f * angle01) * s, 0.15f, 1f);
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

        if (_pendingLand)
        {
            _pendingLand = false;
            float s = Str(vfx.ImpactEffectStrength);
            if (s > 0.01f)
            {
                float impact = Mathf.Clamp(_pendingLandImpact / 30f, 0f, 1.5f);
                // Every slam landing is the power impact (D-077).
                float mult = _pendingLandSlam ? 2.0f : 1f;
                _landPm.InitialVelocityMin = (2f + 8f * impact) * mult * r;
                _landPm.InitialVelocityMax = (4f + 16f * impact) * mult * r;
                _landBurst.AmountRatio = Mathf.Clamp((0.2f + 0.8f * impact) * mult * s, 0.1f, 1f);
                _landBurst.Restart();
            }
        }

        if (_pendingDamage)
        {
            _pendingDamage = false;
            _damage.Restart();
            _damageAge = 0f;
            _damageFlash.Visible = true;
        }

        if (_pendingBurst)
        {
            _pendingBurst = false;
            float s = Str(vfx.BurstEffectStrength);
            if (s > 0.01f)
            {
                _sparks.AmountRatio = Mathf.Clamp(s, 0.2f, 1f);
                _sparks.Restart();
                _burstAge = 0f;
                _burstOrigin = GlobalPosition + Vector3.Up * (-r * 0.9f);
                _boomRing.Visible = true;
                _bowRing.Visible = true;
            }
        }
    }

    /// <summary>Ground shock ring expands from the landing point; the bow ring rides
    /// ahead of the ball, perpendicular to travel, and widens as it fades: the air parting.</summary>
    private void AnimateBurst(float dt, float r)
    {
        if (_burstAge < 0f) return;
        _burstAge += dt;
        float t01 = _burstAge / BurstEffectSeconds;
        if (t01 >= 1f)
        {
            _burstAge = -1f;
            _boomRing.Visible = false;
            _bowRing.Visible = false;
            return;
        }
        float s = Str(_t.Vfx.BurstEffectStrength);
        float fade = Mathf.Pow(1f - t01, 1.6f) * Mathf.Min(1f, s);
        float ease = 1f - (1f - t01) * (1f - t01);

        float boomScale = r * (1.2f + 12f * ease);
        _boomRing.GlobalTransform = new Transform3D(Basis.Identity.Scaled(new Vector3(boomScale, r * 0.6f, boomScale)), _boomOriginAbove(r));
        _boomMat.AlbedoColor = new Color(BoomColor, 0.85f * fade);

        Vector3 fwd = _travelDir;
        Vector3 right = Vector3.Up.Cross(fwd);
        if (right.LengthSquared() < 1e-6f) right = Vector3.Right; else right = right.Normalized();
        Vector3 up = fwd.Cross(right).Normalized();
        // Torus lies in its local XZ plane; aim local Y along travel so the ring faces the way we go.
        Basis face = new(right, fwd, -up);
        float bowScale = r * (1.0f + 3.5f * ease);
        _bowRing.Basis = face * Basis.Identity.Scaled(new Vector3(bowScale, r * 0.5f, bowScale));
        _bowRing.Position = fwd * r * (1.3f + 2.2f * ease);
        _bowMat.AlbedoColor = new Color(BoomColor, 0.7f * fade);
    }

    /// <summary>The damage flash: a shell around the ball that swells a little and fades out fast.</summary>
    private void AnimateDamage(float dt, float r)
    {
        if (_damageAge < 0f) return;
        _damageAge += dt;
        float t01 = _damageAge / DamageEffectSeconds;
        if (t01 >= 1f)
        {
            _damageAge = -1f;
            _damageFlash.Visible = false;
            return;
        }
        float fade = Mathf.Pow(1f - t01, 1.4f);
        _damageFlash.Scale = Vector3.One * r * (2.1f + 1.6f * t01);
        _damageMat.AlbedoColor = new Color(DamageColor, 0.55f * fade);
    }

    private Vector3 _boomOriginAbove(float r) => _burstOrigin + Vector3.Up * (r * 0.12f);

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

        _slamPm.EmissionSphereRadius = r * 0.7f;
        _slamPm.ScaleMin = 0.12f * r;
        _slamPm.ScaleMax = 0.30f * r;

        _jumpBurst.Position = contact;
        _jumpPm.EmissionRingRadius = r * 1.1f;
        _jumpPm.EmissionRingInnerRadius = r * 0.3f;
        _jumpPm.EmissionRingHeight = r * 0.05f;
        _jumpPm.ScaleMin = 0.14f * r;
        _jumpPm.ScaleMax = 0.36f * r;

        _landBurst.Position = contact;
        _landPm.EmissionRingRadius = r * 0.9f;
        _landPm.EmissionRingInnerRadius = r * 0.25f;
        _landPm.EmissionRingHeight = r * 0.05f;
        _landPm.ScaleMin = 0.16f * r;
        _landPm.ScaleMax = 0.42f * r;

        _carveRocksPm.EmissionSphereRadius = r * 0.45f;
        _carveRocksPm.ScaleMin = 0.14f * r;
        _carveRocksPm.ScaleMax = 0.40f * r;
        _carveSprayPm.EmissionSphereRadius = r * 0.6f;
        _carveSprayPm.ScaleMin = 0.30f * r;
        _carveSprayPm.ScaleMax = 0.80f * r;

        _damage.Position = Vector3.Zero;
        _damagePm.EmissionSphereRadius = r * 1.0f;
        _damagePm.InitialVelocityMin = 5f * r;
        _damagePm.InitialVelocityMax = 12f * r;
        _damagePm.ScaleMin = 0.10f * r;
        _damagePm.ScaleMax = 0.24f * r;

        _sparks.Position = contact;
        _sparksPm.EmissionRingRadius = r * 0.8f;
        _sparksPm.EmissionRingInnerRadius = r * 0.2f;
        _sparksPm.EmissionRingHeight = r * 0.05f;
        _sparksPm.InitialVelocityMin = 6f * r;
        _sparksPm.InitialVelocityMax = 15f * r;
        _sparksPm.ScaleMin = 0.07f * r;
        _sparksPm.ScaleMax = 0.18f * r;
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

    private void BuildSlamStreak(Material draw)
    {
        // Parent body never rotates, so local -Y is world down.
        _slamPm = Sphere(new Vector3(0f, -1f, 0f), 6f, 8f, 18f, Vector3.Zero,
                         Ramp(new Color(0.45f, 0.52f, 1.00f), 0.90f));
        _slam = Emitter("SlamStreak", 96, 0.30f, false, _slamPm, _chunk, draw);
    }

    private void BuildJumpBurst(Material draw)
    {
        _jumpPm = Ring(new Vector3(0f, 1f, 0f), 55f, 3f, 9f, new Vector3(0f, -8f, 0f),
                       Ramp(new Color(1.00f, 0.82f, 0.42f), 0.85f));
        _jumpPm.DampingMin = 2f;
        _jumpPm.DampingMax = 4f;
        _jumpBurst = Emitter("JumpReleaseBurst", 72, 0.42f, true, _jumpPm, _quad, draw);
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

    /// <summary>Carve debris (D-089): rock chunks and a dust spray, re-aimed every frame toward the outside of the corner.</summary>
    private void BuildCarve(Material rockDraw, Material sprayDraw)
    {
        // Emitter basis is re-aimed every frame with -Z = throw direction (Basis.LookingAt).
        _carveRocksPm = Sphere(new Vector3(0f, 0f, -1f), 24f, 6f, 16f, new Vector3(0f, -32f, 0f), Ramp(RockColor, 1.0f));
        _carveRocksPm.DampingMin = 0.5f;
        _carveRocksPm.DampingMax = 1.5f;
        _carveRocksPm.AngularVelocityMin = -540f;
        _carveRocksPm.AngularVelocityMax = 540f;
        _carveRocks = Emitter("CarveRocks", 140, 0.85f, false, _carveRocksPm, _chunk, rockDraw);

        _carveSprayPm = Sphere(new Vector3(0f, 0f, -1f), 34f, 8f, 20f, new Vector3(0f, -6f, 0f), Ramp(SprayColor, 0.75f));
        _carveSprayPm.DampingMin = 2f;
        _carveSprayPm.DampingMax = 4f;
        _carveSpray = Emitter("CarveSpray", 120, 0.60f, false, _carveSprayPm, _quad, sprayDraw);
    }

    /// <summary>Landing burst (D-077): electric-blue sparks plus two boom rings.</summary>
    private void BuildBurst(Material draw)
    {
        _sparksPm = Ring(new Vector3(0f, 1f, 0f), 70f, 12f, 30f, new Vector3(0f, -30f, 0f),
                         Ramp(SparkColor, 1.00f));
        _sparksPm.DampingMin = 4f;
        _sparksPm.DampingMax = 9f;
        _sparksPm.AngularVelocityMin = -420f;
        _sparksPm.AngularVelocityMax = 420f;
        _sparks = Emitter("BurstSparks", 140, 0.50f, true, _sparksPm, _chunk, draw);

        var torus = new TorusMesh { InnerRadius = 0.86f, OuterRadius = 1.0f, Rings = 32, RingSegments = 8 };
        _boomMat = RingMaterial();
        _bowMat = RingMaterial();
        // World-space: it must stay on the landing point while the ball surges away.
        _boomRing = new MeshInstance3D
        {
            Name = "BurstBoomRing", Mesh = torus, MaterialOverride = _boomMat, TopLevel = true, Visible = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_boomRing);
        _bowRing = new MeshInstance3D
        {
            Name = "BurstBowRing", Mesh = torus, MaterialOverride = _bowMat, Visible = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_bowRing);
    }

    /// <summary>Damage (06 §10): red motes off the whole ball plus a shell flash, both player-local.</summary>
    private void BuildDamage(Material draw)
    {
        _damagePm = Sphere(new Vector3(0f, 1f, 0f), 180f, 5f, 12f, new Vector3(0f, -12f, 0f), Ramp(DamageColor, 1.0f));
        _damagePm.DampingMin = 3f;
        _damagePm.DampingMax = 7f;
        _damage = Emitter("DamagePulse", 64, 0.30f, true, _damagePm, _quad, draw);

        _damageMat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            CullMode = BaseMaterial3D.CullModeEnum.Back,
            AlbedoColor = new Color(DamageColor, 0f),
        };
        _damageFlash = new MeshInstance3D
        {
            Name = "DamageFlash",
            Mesh = new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 12, Rings = 7 },
            MaterialOverride = _damageMat,
            Visible = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_damageFlash);
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

    private static StandardMaterial3D RingMaterial() => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        BlendMode = BaseMaterial3D.BlendModeEnum.Add,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        NoDepthTest = false,
        AlbedoColor = new Color(BoomColor, 0f),
    };
}
