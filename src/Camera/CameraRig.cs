using Godot;
using Rushcore.Core;
using Rushcore.Player;
using Rushcore.Tuning;

namespace Rushcore.Camera;

/// <summary>
/// Follow rig. Accepted 2026-09-05 (D-072): yaw tracks the player's trajectory (chase
/// camera); the fixed-yaw A/B toggle was removed once the baseline locked. Reads the
/// player's interpolated transform every rendered frame and applies its own damping, so it
/// never inherits raw physics-step jitter. The yaw always converges on the direction of
/// travel: the only holds are below the minimum speed and a bounded, intent-gated hold
/// against a reversed heading (see UpdateYaw).
///
/// Clipping defence, in order: (1) focus is floored above the ground so casts never start
/// inside terrain; (2) two same-frame sphere casts, focus->camera and ball->camera, pull the
/// camera in instantly; (3) shake is bounded to the probe margin; (4) the camera body is
/// floored above the heightfield as a last resort and re-aimed at the focus.
/// </summary>
public partial class CameraRig : Node3D, ICameraBasis
{
    private readonly GameplayTuning _t;
    private readonly PlayerPhysics _player;
    private Camera3D _camera = null!;

    private Vector3 _focus;
    private float _zoom = 1f;
    private float _shake;
    private float _yaw;                    // radians; the only orientation state
    private float _occlusion = 1f;         // smoothed fraction of the full distance in use
    private float _reverseHold;            // seconds the yaw has been held against a reversed heading

    private readonly SphereShape3D _probeShape = new();
    private readonly PhysicsShapeQueryParameters3D _probe = new();
    private readonly RandomNumberGenerator _rng = new();

    private const float MinCameraDistance = 1.5f;
    /// <summary>cos(120°): a heading further than this from the view is a reversal, not a turn.</summary>
    private const float ReverseCone = -0.5f;

    public CameraRig(GameplayTuning tuning, PlayerPhysics player)
    {
        _t = tuning;
        _player = player;
    }

    /// <summary>World height source used for the focus/camera floors. Terrain only;
    /// props are handled by the sphere casts.</summary>
    public Func<float, float, float>? GroundHeight { get; set; }

    public Vector3 FlatForward { get; private set; } = Vector3.Forward;
    public Vector3 FlatRight { get; private set; } = Vector3.Right;
    public Camera3D Camera => _camera;
    public float Zoom => _zoom;
    public float YawDegreesCurrent => Mathf.RadToDeg(_yaw);
    /// <summary>1 = unobstructed; lower = camera pulled in toward the focus by that fraction.</summary>
    public float OcclusionFraction => _occlusion;
    public float CurrentDistance { get; private set; }
    public float CurrentLookAhead { get; private set; }
    /// <summary>True on frames where the last-resort ground floor had to lift the camera.</summary>
    public bool FlooredThisFrame { get; private set; }
    /// <summary>True while the yaw is deliberately held against a reversed travel heading.</summary>
    public bool ReverseHoldActive { get; private set; }

    public override void _Ready()
    {
        Name = "CameraRig";
        // Repositioned every rendered frame; engine interpolation must not also smear it.
        PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off;

        _camera = new Camera3D { Name = "Camera", Current = true, Near = 0.25f, Far = Mathf.Max(100f, _t.Camera.FarPlane) };
        AddChild(_camera);

        _rng.Randomize();
        _probe.Shape = _probeShape;
        _probe.Exclude = new Godot.Collections.Array<Rid> { _player.GetRid() };
        _probe.CollideWithAreas = false;
        _probe.CollideWithBodies = true;

        _yaw = 0f;                       // the bootstrap snaps it to the spawn facing
        UpdateOrientation();
        _focus = _player.GlobalPosition + Vector3.Up * _t.Camera.HeightOffset;
        ApplyTransform(FullDistance(0f), 0f);
        _player.Landed += OnLanded;
        _player.Slammed += OnSlammed;
        _player.LandingBurst += OnLandingBurst;
        _player.Recovered += SnapToPlayer;
    }

    public override void _ExitTree()
    {
        _player.Landed -= OnLanded;
        _player.Slammed -= OnSlammed;
        _player.LandingBurst -= OnLandingBurst;
        _player.Recovered -= SnapToPlayer;
    }

    public void AddShake(float amount) => _shake = Mathf.Min(_shake + amount, 4f);

    /// <summary>Point the camera along a world direction immediately (spawn, teleport).</summary>
    public void SnapYawToward(Vector3 flatDirection)
    {
        Vector3 d = new(flatDirection.X, 0f, flatDirection.Z);
        if (d.LengthSquared() < 1e-6f) return;
        _yaw = YawFor(d.Normalized());
        _reverseHold = 0f;
        UpdateOrientation();
    }

    public void SnapToPlayer()
    {
        _focus = FloorAboveGround(_player.GlobalPosition + Vector3.Up * _t.Camera.HeightOffset, FocusClearance);
        _shake = 0f;
        _occlusion = 1f;
        _reverseHold = 0f;
        UpdateOrientation();
        ApplyTransform(FullDistance(0f), 0f);
        ResetPhysicsInterpolation();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        var c = _t.Camera;
        if (!InputBootstrap.IsTextEntryFocused(GetViewport()))
        {
            if (Input.IsActionJustPressed(InputBootstrap.ZoomIn)) SetZoom(_zoom - c.ZoomStep);
            if (Input.IsActionJustPressed(InputBootstrap.ZoomOut)) SetZoom(_zoom + c.ZoomStep);
        }

        if (!Mathf.IsEqualApprox(_camera.Far, c.FarPlane)) _camera.Far = Mathf.Max(100f, c.FarPlane);

        Vector3 playerPos = _player.GetGlobalTransformInterpolated().Origin;
        Vector3 flatVel = new(_player.Velocity.X, 0f, _player.Velocity.Z);
        float speed = flatVel.Length();
        float cap = Mathf.Max(0.001f, _t.Movement.HardMaxLocomotionSpeed);
        // Above the base cap (Flow headroom, D-088) distance and FOV keep extrapolating so speed
        // earned on the edge reads as more speed; the look-ahead saturates at the base cap.
        float speed01 = Mathf.Clamp(speed / cap, 0f, 1f + Mathf.Max(0f, _t.Flow.Headroom));
        float look01 = Mathf.Min(speed01, 1f);

        // While carving the view tracks the facing (03 §11): the player looks at the exit, so does the camera.
        UpdateYaw(_player.IsCarving ? new Vector3(_player.Facing.X, 0f, _player.Facing.Z).Normalized() * Mathf.Max(speed, 1f) : flatVel, speed, dt);
        UpdateOrientation();

        Vector3 lookAhead = Vector3.Zero;
        if (speed > 1f) lookAhead = flatVel / speed * Mathf.Lerp(c.LookAheadMin, c.LookAheadMax, look01);
        CurrentLookAhead = lookAhead.Length();

        // The look-ahead point may lie inside an upslope; floor it so the focus, and
        // therefore every cast that starts there, is always above ground.
        Vector3 target = FloorAboveGround(playerPos + lookAhead + Vector3.Up * c.HeightOffset, FocusClearance);

        // Exponential damping, vertical on its own slower constant so hills and jump
        // arcs do not throw the horizon around. The damped result is floored again
        // because lag alone can sink it into a slope.
        float aH = 1f - Mathf.Exp(-Mathf.Max(0.01f, c.FollowDamping) * dt);
        float aV = 1f - Mathf.Exp(-Mathf.Max(0.01f, c.VerticalDamping) * dt);
        _focus.X = Mathf.Lerp(_focus.X, target.X, aH);
        _focus.Z = Mathf.Lerp(_focus.Z, target.Z, aH);
        _focus.Y = Mathf.Lerp(_focus.Y, target.Y, aV);
        _focus = FloorAboveGround(_focus, FocusClearance);

        if (_shake > 0f) _shake = Mathf.Max(0f, _shake - _shake * Mathf.Max(0.01f, c.ShakeDecay) * dt);

        float fullDist = FullDistance(speed01);
        UpdateOcclusion(playerPos, Basis.Z * fullDist, dt);
        ApplyTransform(fullDist, _shake);
        _camera.Fov = Mathf.Lerp(c.FovMin, c.FovMax, speed01);
    }

    // ---------------- orientation ----------------

    private void UpdateYaw(Vector3 flatVel, float speed, float dt)
    {
        var c = _t.Camera;
        float targetYaw = _yaw;
        float gain = 1f;
        ReverseHoldActive = false;
        float minSpeed = Mathf.Max(0.1f, c.YawFollowMinSpeed);
        // Follow gain ramps in with speed: near-stationary lateral residuals must
        // not be allowed to steer the view.
        gain = Mathf.Clamp((speed - minSpeed) / (2f * minSpeed), 0f, 1f);
        if (gain > 0f)
        {
            Vector3 dir = flatVel / speed;
            bool reversed = dir.Dot(FlatForward) < ReverseCone;
            // A reversed heading (wall bounce, backward slide) is held only while the
            // player pushes forward against it, and only for a bounded time, so a quick
            // recovery does not swing the view twice yet the camera always ends up
            // behind the direction of travel. S is a brake and cannot reverse (D-076),
            // so there is no reverse-drive case to hold for.
            bool fighting = _player.InputVector.Y > 0.2f;
            if (reversed && fighting && _reverseHold < c.YawReverseHoldSeconds)
            {
                _reverseHold += dt;
                ReverseHoldActive = true;
            }
            else
            {
                targetYaw = YawFor(dir);
            }
            if (!reversed) _reverseHold = 0f;
        }

        float diff = Mathf.Wrap(targetYaw - _yaw, -Mathf.Pi, Mathf.Pi);
        float step = diff * (1f - Mathf.Exp(-Mathf.Max(0.01f, c.YawFollowDamping) * gain * dt));
        float maxStep = Mathf.DegToRad(Mathf.Max(1f, c.YawMaxTurnRate)) * dt;
        step = Mathf.Clamp(step, -maxStep, maxStep);
        _yaw = Mathf.Wrap(_yaw + step, -Mathf.Pi, Mathf.Pi);
    }

    /// <summary>Yaw whose camera-forward equals the given flat direction.</summary>
    private static float YawFor(Vector3 flatDir) => Mathf.Atan2(-flatDir.X, -flatDir.Z);

    private void UpdateOrientation()
    {
        Basis b = Basis.FromEuler(new Vector3(Mathf.DegToRad(_t.Camera.PitchDegrees), _yaw, 0f));
        Basis = b;

        Vector3 fwd = -b.Z;
        fwd.Y = 0f;
        fwd = fwd.LengthSquared() < 1e-6f ? Vector3.Forward : fwd.Normalized();
        Vector3 right = b.X;
        right.Y = 0f;
        right = right.LengthSquared() < 1e-6f ? Vector3.Right : right.Normalized();
        FlatForward = fwd;
        FlatRight = right;
    }

    // ---------------- occlusion ----------------

    private void UpdateOcclusion(Vector3 playerPos, Vector3 offset, float dt)
    {
        var c = _t.Camera;
        float target = 1f;
        if (c.OcclusionProbe && offset.LengthSquared() > 1e-4f)
        {
            _probeShape.Radius = Mathf.Max(0.05f, c.OcclusionMargin);
            Vector3 cameraPos = _focus + offset;
            // Both lines matter: the focus line keeps the framing clear, the ball line
            // keeps the player visible when a crest sits between the two.
            float fromFocus = Cast(_focus, offset);
            float fromBall = Cast(playerPos + Vector3.Up * 0.5f, cameraPos - playerPos - Vector3.Up * 0.5f);
            target = Mathf.Min(fromFocus, fromBall);
        }

        // Pull in on the same frame; ease back out so a cleared line does not pop.
        _occlusion = target < _occlusion
            ? target
            : Mathf.Lerp(_occlusion, target, 1f - Mathf.Exp(-Mathf.Max(0.01f, c.OcclusionRecoverSpeed) * dt));
    }

    /// <summary>Safe fraction of the motion, run on the main thread this frame
    /// (PlayerPhysics guards that physics is not on a separate thread).</summary>
    private float Cast(Vector3 from, Vector3 motion)
    {
        _probe.Transform = new Transform3D(Basis.Identity, from);
        _probe.Motion = motion;
        float[] hit = GetWorld3D().DirectSpaceState.CastMotion(_probe);
        if (hit.Length < 2) return 1f;
        // Starting inside geometry gives no usable answer; keep what we have.
        if (hit[0] <= 0f && hit[1] <= 0f) return _occlusion;
        return Mathf.Clamp(hit[0], 0f, 1f);
    }

    // ---------------- placement ----------------

    private float FocusClearance => _t.Camera.GroundClearance + 0.5f;

    private float FullDistance(float speed01)
    {
        var c = _t.Camera;
        return (c.Distance + c.SpeedDistanceGain * speed01) * _zoom;
    }

    private Vector3 FloorAboveGround(Vector3 p, float clearance)
    {
        if (GroundHeight is null) return p;
        float minY = GroundHeight(p.X, p.Z) + clearance;
        if (p.Y < minY) p.Y = minY;
        return p;
    }

    private void ApplyTransform(float fullDist, float shake)
    {
        var c = _t.Camera;
        GlobalPosition = _focus;

        float dist = Mathf.Max(MinCameraDistance, fullDist * _occlusion);
        CurrentDistance = dist;
        Vector3 local = new(0f, 0f, dist);
        if (shake > 0f)
        {
            // Bounded to the probe margin so shake can never push the lens through a wall.
            float s = Mathf.Min(shake * c.ShakeStrength, c.OcclusionMargin * 0.8f);
            local += new Vector3(_rng.RandfRange(-s, s), _rng.RandfRange(-s, s), _rng.RandfRange(-s, s));
        }
        _camera.Position = local;
        _camera.Rotation = Vector3.Zero;

        // Last resort: never let the lens go below the heightfield.
        FlooredThisFrame = false;
        if (GroundHeight is null) return;
        Vector3 gp = _camera.GlobalPosition;
        float minY = GroundHeight(gp.X, gp.Z) + c.GroundClearance;
        if (gp.Y < minY)
        {
            gp.Y = minY;
            _camera.GlobalPosition = gp;
            _camera.LookAt(_focus, Vector3.Up);
            FlooredThisFrame = true;
        }
    }

    private void SetZoom(float value) => _zoom = Mathf.Clamp(value, _t.Camera.ZoomMin, _t.Camera.ZoomMax);

    private void OnLanded(float impactSpeed, bool wasSlam)
    {
        float baseShake = Mathf.Clamp(impactSpeed / 40f, 0f, 1f) * 0.35f;
        if (wasSlam) baseShake += 0.60f;          // every slam landing is the power impact (D-077)
        AddShake(baseShake);
    }

    private void OnSlammed() => AddShake(0.15f);

    private void OnLandingBurst(float speed) => AddShake(0.45f);
}
