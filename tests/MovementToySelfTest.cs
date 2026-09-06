using System.Collections;
using Godot;
using Rushcore.Core;
using Rushcore.Generation;
using Rushcore.Player;
using Rushcore.Tuning;
using Rushcore.World;

namespace Rushcore.Testing;

/// <summary>
/// Headless acceptance harness for the objective half of Gate M0 (08 §3). It drives
/// the real controller with synthetic input on an isolated high-altitude rig so the
/// results do not depend on the calibration terrain. Subjective feel is NOT tested
/// here; that remains a human playtest.
/// Run with: godot --headless --path . -- --rushcore-selftest
/// </summary>
public partial class MovementToySelfTest : Node
{
    private const float PlatformY = 302f;
    private const float RampAngleDegrees = 20f;
    private static readonly Vector3 PlatformCenter = new(0f, PlatformY, 0f);
    private static readonly Vector3 RampOrigin = new(0f, PlatformY - 2f, 1300f);

    /// <summary>Injected input is observed by the player on the next physics frame and
    /// acted on during that frame's integration, so assertions wait two frames.</summary>
    private const int EdgeFrames = 2;
    private const float WallDistance = 110f;
    private static readonly Vector3 WallLaneCentre = new(0f, PlatformY, -1400f);
    private static readonly Vector3 LandingLaneCentre = new(0f, PlatformY, 2600f);
    private const float LandingSlopeDegrees = 25f;
    private Vector3 _landingStart;
    private Vector3 _wallCentre;
    private Vector3 _wallLaneStart;

    private readonly IDebugActions _debug;
    private PlayerPhysics _player = null!;
    private IEnumerator _script = null!;

    private readonly List<string> _failures = new();
    private int _checks;
    private bool _done;

    private int _jumpedCount, _chargeCanceledCount, _slammedCount, _recoveredCount, _pickupCount, _landedCount, _burstCount;
    private bool _releaseJumpFromProcess;
    /// <summary>When set, WASD is re-derived every tick so a rotating camera cannot bend the drive line.</summary>
    private Vector3? _worldDrive;
    private bool _visCheck;
    private int _visFrames, _visBlocked;
    private int _groundViolations;
    private Vector3 _laneFwd = Vector3.Forward;
    private float _lastJumpCharge;

    public MovementToySelfTest(IDebugActions debug) => _debug = debug;

    private static Vector3 RampSurfacePoint(float alongSlope)
    {
        float a = Mathf.DegToRad(RampAngleDegrees);
        // Local (alongSlope, +2, 0) on the box top, rotated about Z, then offset.
        return new Vector3(
            alongSlope * Mathf.Cos(a) - 2f * Mathf.Sin(a),
            alongSlope * Mathf.Sin(a) + 2f * Mathf.Cos(a),
            0f) + RampOrigin;
    }

    public override void _Ready()
    {
        Name = "MovementToySelfTest";
        _player = _debug.Player;
        BuildRig();

        _player.Jumped += c => { _jumpedCount++; _lastJumpCharge = c; };
        _player.JumpChargeCanceled += () => _chargeCanceledCount++;
        _player.Slammed += () => _slammedCount++;
        _player.Recovered += () => _recoveredCount++;
        _player.Landed += (_, _) => _landedCount++;
        _player.LandingBurst += _ => _burstCount++;
        _debug.World.BoostPickupCollected += _ => _pickupCount++;

        _script = Run();
        GD.Print("[SELFTEST] starting");
    }

    public override void _Process(double delta)
    {
        // Real key-ups land between physics ticks; this mimics that path.
        if (_releaseJumpFromProcess)
        {
            _releaseJumpFromProcess = false;
            Input.ActionRelease(InputBootstrap.Jump);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_done) return;

        if (!_player.Velocity.IsFinite())
        {
            Fail("velocity stays finite", $"velocity={_player.Velocity}");
            Finish();
            return;
        }

        if (_worldDrive is { } drive) PressWorldDirection(drive);
        SampleCameraInvariants();

        if (!_script.MoveNext()) Finish();
    }

    /// <summary>Every tick: the lens must be above the heightfield, and while a run is
    /// flagged for visibility the camera->ball line must be clear.</summary>
    private void SampleCameraInvariants()
    {
        if (_player.CameraBasis is not Rushcore.Camera.CameraRig rig) return;
        var world = _debug.World;
        Vector3 cam = rig.Camera.GlobalPosition;
        if (world.InBounds(cam.X, cam.Z) && cam.Y < world.Bounds.End.Y + 50f)
        {
            if (cam.Y < world.SampleHeight(cam.X, cam.Z) + _debug.Tuning.Camera.GroundClearance - 0.15f)
                _groundViolations++;
        }

        if (!_visCheck) return;
        var q = PhysicsRayQueryParameters3D.Create(cam, _player.GlobalPosition + Vector3.Up * 0.5f);
        q.Exclude = new Godot.Collections.Array<Rid> { _player.GetRid() };
        q.CollideWithAreas = false;
        var hit = _player.GetWorld3D().DirectSpaceState.IntersectRay(q);
        _visFrames++;
        if (hit.Count > 0) _visBlocked++;
    }

    private void BuildRig()
    {
        var plate = new StaticBody3D { Name = "TestPlate", Position = PlatformCenter - Vector3.Up * 2f };
        plate.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(1600f, 4f, 1600f) } });
        plate.PhysicsMaterialOverride = new PhysicsMaterial { Friction = PlayerPhysics.ArcadeSurfaceFriction, Bounce = 0f };
        AddChild(plate);

        // Isolated CCD lane (08 §3): a runway aligned with the drive direction and a
        // 0.3 m plate across it. Kept away from the main plate so no other case can
        // strike the wall by accident.
        Vector3 fwd = _player.CameraBasis?.FlatForward ?? Vector3.Forward;
        _laneFwd = fwd;
        var laneBasis = Basis.LookingAt(fwd, Vector3.Up);          // lane -Z == drive direction
        var lane = new StaticBody3D
        {
            Name = "TestCcdLane",
            Transform = new Transform3D(laneBasis, WallLaneCentre - Vector3.Up * 2f),
            PhysicsMaterialOverride = new PhysicsMaterial { Friction = PlayerPhysics.ArcadeSurfaceFriction, Bounce = 0f },
        };
        lane.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(80f, 4f, 340f) } });
        AddChild(lane);

        _wallCentre = WallLaneCentre + fwd * WallDistance + Vector3.Up * 6f;
        _wallLaneStart = WallLaneCentre - fwd * 140f + Vector3.Up * 3f;
        var wall = new StaticBody3D
        {
            Name = "TestThinWall",
            Transform = new Transform3D(laneBasis, _wallCentre),
            PhysicsMaterialOverride = new PhysicsMaterial { Friction = PlayerPhysics.ArcadeSurfaceFriction, Bounce = 0f },
        };
        wall.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(80f, 12f, 0.3f) } });
        AddChild(wall);

        // Landing lane: runway into a 25 deg downslope. Arriving at the cap, the ball
        // leaves the crest ballistically and lands on the slope with tangent speed above
        // the cap (1/cos 25 deg); the cap must bleed that, not clip it (D-069, 03 §5).
        var landRunway = new StaticBody3D
        {
            Name = "TestLandingRunway",
            Transform = new Transform3D(laneBasis, LandingLaneCentre - Vector3.Up * 2f),
            PhysicsMaterialOverride = new PhysicsMaterial { Friction = PlayerPhysics.ArcadeSurfaceFriction, Bounce = 0f },
        };
        landRunway.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(120f, 4f, 600f) } });
        AddChild(landRunway);

        float sa = Mathf.DegToRad(LandingSlopeDegrees);
        Vector3 crest = LandingLaneCentre + fwd * 300f;
        _landingStart = LandingLaneCentre - fwd * 290f + Vector3.Up * 3f;
        // Slope box: tilt the lane basis nose-down about its local X, centre it half a
        // length down the incline, and sink it by half its thickness.
        Basis slopeBasis = laneBasis * new Basis(Vector3.Right, -sa);
        Vector3 slopeCentre = crest + fwd * (600f * Mathf.Cos(sa)) - Vector3.Up * (600f * Mathf.Sin(sa) + 2f * Mathf.Cos(sa));
        var landSlope = new StaticBody3D
        {
            Name = "TestLandingSlope",
            Transform = new Transform3D(slopeBasis, slopeCentre),
            PhysicsMaterialOverride = new PhysicsMaterial { Friction = PlayerPhysics.ArcadeSurfaceFriction, Bounce = 0f },
        };
        landSlope.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(160f, 4f, 1200f) } });
        AddChild(landSlope);

        var ramp = new StaticBody3D { Name = "TestRamp", Position = RampOrigin };
        ramp.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(400f, 4f, 200f) } });
        ramp.PhysicsMaterialOverride = new PhysicsMaterial { Friction = PlayerPhysics.ArcadeSurfaceFriction, Bounce = 0f };
        ramp.RotateZ(Mathf.DegToRad(RampAngleDegrees));   // +X is uphill
        AddChild(ramp);
    }

    // ---------------- helpers ----------------
    private void Check(string name, bool ok, string detail = "")
    {
        _checks++;
        if (ok) GD.Print($"[SELFTEST] PASS  {name}");
        else Fail(name, detail);
    }

    private void Fail(string name, string detail)
    {
        _failures.Add(name + (string.IsNullOrEmpty(detail) ? "" : $"  ({detail})"));
        GD.Print($"[SELFTEST] FAIL  {name}  {detail}");
    }

    private void CheckNear(string name, float actual, float expected, float tolerance) =>
        Check(name, Mathf.Abs(actual - expected) <= tolerance,
            $"actual={actual:0.###} expected={expected:0.###} tol={tolerance:0.###}");

    private static IEnumerable Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
    private static IEnumerable Seconds(float s) => Frames(Mathf.Max(1, (int)(s * Engine.PhysicsTicksPerSecond)));
    private static IEnumerable Act() => Frames(EdgeFrames);

    private static readonly string[] AllActions =
    {
        InputBootstrap.MoveForward, InputBootstrap.MoveBack, InputBootstrap.MoveLeft,
        InputBootstrap.MoveRight, InputBootstrap.Jump, InputBootstrap.Boost
    };

    private void ReleaseAll()
    {
        _worldDrive = null;
        foreach (var a in AllActions) Input.ActionRelease(a);
    }

    /// <summary>Presses the WASD combination that steers toward a world-space direction.</summary>
    private void PressWorldDirection(Vector3 worldDir)
    {
        Vector3 d = new Vector3(worldDir.X, 0f, worldDir.Z).Normalized();
        float ix = FlatRight.Dot(d), iy = Forward.Dot(d);
        Input.ActionRelease(InputBootstrap.MoveLeft);
        Input.ActionRelease(InputBootstrap.MoveRight);
        Input.ActionRelease(InputBootstrap.MoveForward);
        Input.ActionRelease(InputBootstrap.MoveBack);
        if (ix >= 0f) Input.ActionPress(InputBootstrap.MoveRight, ix); else Input.ActionPress(InputBootstrap.MoveLeft, -ix);
        if (iy >= 0f) Input.ActionPress(InputBootstrap.MoveForward, iy); else Input.ActionPress(InputBootstrap.MoveBack, -iy);
    }

    private IEnumerable Settle(Vector3 at, float seconds = 1.2f)
    {
        ReleaseAll();
        _player.TeleportTo(at);
        foreach (var _ in Seconds(seconds)) yield return null;
    }

    private Vector3 Forward => _player.CameraBasis?.FlatForward ?? Vector3.Forward;
    private Vector3 FlatRight => _player.CameraBasis?.FlatRight ?? Vector3.Right;
    private Vector3 FlatVel => new(_player.Velocity.X, 0f, _player.Velocity.Z);

    private void Finish()
    {
        _done = true;
        ReleaseAll();
        GD.Print($"[SELFTEST] ---- {_checks - _failures.Count}/{_checks} checks passed ----");
        foreach (var f in _failures) GD.Print("[SELFTEST] FAILED: " + f);
        GD.Print(_failures.Count == 0 ? "[SELFTEST] RESULT: PASS" : $"[SELFTEST] RESULT: FAIL ({_failures.Count})");
        GetTree().Quit(_failures.Count == 0 ? 0 : 1);
    }

    // ---------------- the script ----------------
    private IEnumerator Run()
    {
        var t = _debug.Tuning;
        var m = t.Movement;
        var js = t.JumpSlam;

        // ---- body configuration invariants ----
        Check("CCD enabled", _player.ContinuousCd);
        Check("player cannot sleep", !_player.CanSleep);
        Check("contact monitoring on", _player.ContactMonitor);
        Check("contact budget > 0", _player.MaxContactsReported >= 4, $"reported={_player.MaxContactsReported}");
        Check("CustomIntegrator off", !_player.CustomIntegrator);
        Check("standard gravity active", _player.GravityScale > 0f, $"scale={_player.GravityScale}");

        foreach (var _ in Settle(PlatformCenter + Vector3.Up * 3f)) yield return null;
        Check("ground contact produces usable data",
            _player.IsRawGrounded && _player.GroundNormal.Dot(Vector3.Up) > 0.9f,
            $"grounded={_player.IsRawGrounded} n={_player.GroundNormal}");

        // ---- accelerates from rest ----
        Input.ActionPress(InputBootstrap.MoveForward, 1f);
        foreach (var _ in Seconds(1.5f)) yield return null;
        Check("accelerates from rest on flat", _player.LocomotionSpeed > 12f, $"speed={_player.LocomotionSpeed:0.0}");

        // ---- hard cap, including under boost ----
        Input.ActionPress(InputBootstrap.Boost, 1f);
        float maxSeen = 0f;
        foreach (var _ in Seconds(6f))
        {
            maxSeen = Mathf.Max(maxSeen, _player.LocomotionSpeed);
            yield return null;
        }
        Check("hard locomotion cap is never exceeded", maxSeen <= m.HardMaxLocomotionSpeed + 0.5f,
            $"max={maxSeen:0.00} cap={m.HardMaxLocomotionSpeed}");
        Check("boost cannot bypass the cap", _player.LocomotionSpeed <= m.HardMaxLocomotionSpeed + 0.5f,
            $"speed={_player.LocomotionSpeed:0.00}");
        Check("cap is actually reachable", maxSeen > m.HardMaxLocomotionSpeed - 2f, $"max={maxSeen:0.00}");
        ReleaseAll();

        // ---- downhill acceleration with no input ----
        foreach (var _ in Settle(RampSurfacePoint(120f) + Vector3.Up * 3f, 2.0f)) yield return null;
        Check("player is resting on the test ramp",
            _player.IsGrounded && Mathf.Abs(_player.GroundNormal.Dot(Vector3.Up) - Mathf.Cos(Mathf.DegToRad(RampAngleDegrees))) < 0.05f,
            $"grounded={_player.IsGrounded} n={_player.GroundNormal}");
        float v0 = _player.LocomotionSpeed;
        foreach (var _ in Seconds(1.5f)) yield return null;
        float measured = (_player.LocomotionSpeed - v0) / 1.5f;
        float expected = m.Gravity * Mathf.Sin(Mathf.DegToRad(RampAngleDegrees));
        Check("downhill terrain materially accelerates the player",
            measured > expected * 0.55f && measured < expected * 1.15f,
            $"measured={measured:0.00} expected~{expected:0.00} m/s^2 ({v0:0.0} -> {_player.LocomotionSpeed:0.0})");

        // ---- the same must hold on the real generated terrain, not just the rig ----
        foreach (var e in RunRealTerrainSlopeCase()) yield return e;
        foreach (var e in RunBoostPickupCase()) yield return e;

        // ---- uphill propulsion still works without boost ----
        foreach (var _ in Settle(RampSurfacePoint(-60f) + Vector3.Up * 3f, 1.0f)) yield return null;
        _worldDrive = Vector3.Right;             // +X is uphill on the test ramp
        foreach (var _ in Seconds(1.2f)) yield return null;
        float uphillY = _player.GlobalPosition.Y;
        foreach (var _ in Seconds(2.0f)) yield return null;
        Check("base propulsion climbs uphill without boost", _player.GlobalPosition.Y > uphillY + 5f,
            $"{uphillY:0.0} -> {_player.GlobalPosition.Y:0.0} m");
        ReleaseAll();

        // ---- charging applies no artificial slowdown, and locks steering ----
        foreach (var _ in Settle(PlatformCenter + Vector3.Up * 3f)) yield return null;
        Input.ActionPress(InputBootstrap.MoveForward, 1f);
        foreach (var _ in Seconds(2.0f)) yield return null;
        float speedBeforeCharge = _player.LocomotionSpeed;
        Input.ActionPress(InputBootstrap.Jump, 1f);
        foreach (var _ in Act()) yield return null;
        Check("Space while grounded begins charge", _player.IsCharging);
        Check("steering authority is zero while charging", Mathf.IsZeroApprox(_player.SteeringAuthority),
            $"authority={_player.SteeringAuthority:0.###}");

        Vector3 headingAtChargeStart = FlatVel.Normalized();
        Input.ActionPress(InputBootstrap.MoveLeft, 1f);
        foreach (var _ in Seconds(0.55f)) yield return null;
        Input.ActionRelease(InputBootstrap.MoveLeft);
        float headingDrift = Mathf.RadToDeg(headingAtChargeStart.AngleTo(FlatVel.Normalized()));
        Check("no steering while charging", headingDrift < 1.0f, $"drift={headingDrift:0.00} deg");
        Check("charging applies no artificial slowdown", _player.LocomotionSpeed >= speedBeforeCharge - 0.5f,
            $"{speedBeforeCharge:0.0} -> {_player.LocomotionSpeed:0.0}");

        // ---- charge clamps but never auto-jumps ----
        foreach (var _ in Seconds(js.MaxJumpChargeSeconds * 2.5f)) yield return null;
        Check("charge clamps at maximum", Mathf.IsEqualApprox(_player.Charge01, 1f), $"charge01={_player.Charge01:0.###}");
        Check("full charge does not auto-jump", _player.IsCharging && _player.IsGrounded);

        // ---- full-charge release ----
        float flatBefore = FlatVel.Length();
        int jumpsBefore = _jumpedCount;
        Input.ActionRelease(InputBootstrap.Jump);
        foreach (var _ in Act()) yield return null;
        Check("release performs the jump", _jumpedCount == jumpsBefore + 1);
        CheckNear("full hold produces max takeoff", _player.LastTakeoffVerticalSpeed, js.MaxJumpTakeoffVerticalSpeed, 0.05f);
        CheckNear("jump preserves horizontal momentum", FlatVel.Length(), flatBefore, 1.0f);

        // ---- a fresh airborne press is slam, never a new charge ----
        foreach (var _ in Frames(6)) yield return null;
        int slamsBefore = _slammedCount;
        Input.ActionPress(InputBootstrap.Jump, 1f);
        foreach (var _ in Act()) yield return null;
        Check("airborne press does not begin a new charge", !_player.IsCharging);
        Check("airborne press triggers slam", _slammedCount == slamsBefore + 1 && _player.SlamActive);
        Input.ActionRelease(InputBootstrap.Jump);

        // ---- no double jump ----
        foreach (var _ in Act()) yield return null;
        jumpsBefore = _jumpedCount;
        Input.ActionPress(InputBootstrap.Jump, 1f);
        foreach (var _ in Act()) yield return null;
        Input.ActionRelease(InputBootstrap.Jump);
        foreach (var _ in Act()) yield return null;
        Check("no double jump", _jumpedCount == jumpsBefore);
        ReleaseAll();
        foreach (var _ in Seconds(2.5f)) yield return null;

        // ---- quick tap produces the minimum takeoff ----
        foreach (var _ in Settle(PlatformCenter + Vector3.Up * 3f)) yield return null;
        Input.ActionPress(InputBootstrap.Jump, 1f);
        foreach (var _ in Frames(1)) yield return null;
        Input.ActionRelease(InputBootstrap.Jump);
        foreach (var _ in Act()) yield return null;
        CheckNear("quick tap produces min takeoff", _player.LastTakeoffVerticalSpeed, js.MinJumpTakeoffVerticalSpeed, 0.6f);
        Check("quick tap charge was near zero", _lastJumpCharge < 0.2f, $"charge01={_lastJumpCharge:0.###}");

        // ---- a release arriving between physics ticks must still jump (held-state fallback) ----
        foreach (var _ in Seconds(2.0f)) yield return null;
        foreach (var _ in Settle(PlatformCenter + Vector3.Up * 3f)) yield return null;
        Input.ActionPress(InputBootstrap.Jump, 1f);
        foreach (var _ in Seconds(0.3f)) yield return null;
        Check("charge is active before the off-tick release", _player.IsCharging);
        jumpsBefore = _jumpedCount;
        _releaseJumpFromProcess = true;
        foreach (var _ in Frames(5)) yield return null;
        Check("a release arriving between physics ticks still jumps", _jumpedCount == jumpsBefore + 1 && !_player.IsCharging,
            $"jumps={_jumpedCount - jumpsBefore} charging={_player.IsCharging}");
        ReleaseAll();
        foreach (var _ in Seconds(2.5f)) yield return null;

        // ---- quick tap produces the minimum takeoff ----
        foreach (var _ in Settle(PlatformCenter + Vector3.Up * 3f)) yield return null;
        Input.ActionPress(InputBootstrap.Jump, 1f);
        foreach (var _ in Frames(1)) yield return null;
        Input.ActionRelease(InputBootstrap.Jump);
        foreach (var _ in Act()) yield return null;
        CheckNear("quick tap produces min takeoff (again, after off-tick case)", _player.LastTakeoffVerticalSpeed, js.MinJumpTakeoffVerticalSpeed, 0.6f);

        // ---- landing burst (D-077): Space at the slam touchdown ----
        // (a) press just after touchdown, inside the window
        foreach (var _ in Seconds(2.0f)) yield return null;
        foreach (var _ in Settle(PlatformCenter + Vector3.Up * 3f)) yield return null;
        Input.ActionPress(InputBootstrap.MoveForward, 1f);
        foreach (var _ in Seconds(1.5f)) yield return null;
        Input.ActionPress(InputBootstrap.Jump, 1f);
        foreach (var _ in Seconds(js.MaxJumpChargeSeconds + 0.1f)) yield return null;
        Input.ActionRelease(InputBootstrap.Jump);
        foreach (var _ in Frames(10)) yield return null;
        Input.ActionPress(InputBootstrap.Jump, 1f);                  // slam
        foreach (var _ in Act()) yield return null;
        Input.ActionRelease(InputBootstrap.Jump);
        Check("slam started for the burst case", _player.SlamActive);
        int landingsBefore = _landedCount, burstsBefore = _burstCount;
        int guard = 0;
        while (_landedCount == landingsBefore && guard++ < 600) yield return null;
        Check("slam landed", _landedCount == landingsBefore + 1, $"guard={guard}");
        Vector3 headingAtLanding = FlatVel.Normalized();
        float speedAtLanding = _player.LocomotionSpeed;
        Check("slam landing opens the burst window", _player.BurstWindowOpen,
            $"remaining={_player.BurstWindowRemaining:0.###}");
        Check("no burst without a press", _burstCount == burstsBefore);
        foreach (var _ in Frames(1)) yield return null;
        jumpsBefore = _jumpedCount;
        Input.ActionPress(InputBootstrap.Jump, 1f);
        foreach (var _ in Act()) yield return null;
        Input.ActionRelease(InputBootstrap.Jump);
        foreach (var _ in Act()) yield return null;
        Check("Space inside the window fires the landing burst", _burstCount == burstsBefore + 1,
            $"bursts={_burstCount - burstsBefore}");
        Check("the burst press never starts a charge or a jump", !_player.IsCharging && _jumpedCount == jumpsBefore);
        CheckNear("burst sets the tuned fraction of the cap", _player.LocomotionSpeed,
            js.LandingBurstSpeedFraction * m.HardMaxLocomotionSpeed, 2.5f);
        Check("burst raised the speed", _player.LocomotionSpeed > speedAtLanding + 20f,
            $"{speedAtLanding:0.0} -> {_player.LocomotionSpeed:0.0}");
        float burstDrift = Mathf.RadToDeg(headingAtLanding.AngleTo(FlatVel.Normalized()));
        Check("burst keeps the heading (seamless)", burstDrift < 3f, $"drift={burstDrift:0.00} deg");
        Check("burst keeps the ball on the ground", _player.IsGrounded);
        Check("burst is consumed: the window closes", !_player.BurstWindowOpen);
        ReleaseAll();
        foreach (var _ in Seconds(1.0f)) yield return null;

        // (b) press after the window has closed is an ordinary charge
        foreach (var _ in Settle(PlatformCenter + Vector3.Up * 3f)) yield return null;
        Input.ActionPress(InputBootstrap.MoveForward, 1f);
        foreach (var _ in Seconds(1.0f)) yield return null;
        Input.ActionPress(InputBootstrap.Jump, 1f);
        foreach (var _ in Seconds(js.MaxJumpChargeSeconds + 0.1f)) yield return null;
        Input.ActionRelease(InputBootstrap.Jump);
        foreach (var _ in Frames(10)) yield return null;
        Input.ActionPress(InputBootstrap.Jump, 1f);
        foreach (var _ in Act()) yield return null;
        Input.ActionRelease(InputBootstrap.Jump);
        landingsBefore = _landedCount; burstsBefore = _burstCount;
        guard = 0;
        while (_landedCount == landingsBefore && guard++ < 600) yield return null;
        foreach (var _ in Seconds(js.LandingBurstWindowSeconds + 0.15f)) yield return null;
        Check("the window has closed", !_player.BurstWindowOpen);
        Input.ActionPress(InputBootstrap.Jump, 1f);
        foreach (var _ in Act()) yield return null;
        Check("Space after the window is an ordinary charge, not a burst",
            _player.IsCharging && _burstCount == burstsBefore, $"charging={_player.IsCharging} bursts={_burstCount - burstsBefore}");
        Input.ActionRelease(InputBootstrap.Jump);
        ReleaseAll();
        foreach (var _ in Seconds(2.5f)) yield return null;

        // (c) press just before touchdown, during the slam, is buffered and counts
        foreach (var _ in Settle(PlatformCenter + Vector3.Up * 8f, 0.05f)) yield return null;
        guard = 0;
        while (_player.IsGrounded && guard++ < 60) yield return null;
        Input.ActionPress(InputBootstrap.Jump, 1f);                  // slam from 8 m
        foreach (var _ in Act()) yield return null;
        Input.ActionRelease(InputBootstrap.Jump);
        Check("slam from a fall started", _player.SlamActive);
        Input.ActionPress(InputBootstrap.Jump, 1f);                  // early burst press
        foreach (var _ in Act()) yield return null;
        Input.ActionRelease(InputBootstrap.Jump);
        landingsBefore = _landedCount; burstsBefore = _burstCount;
        guard = 0;
        while (_landedCount == landingsBefore && guard++ < 120) yield return null;
        foreach (var _ in Act()) yield return null;
        Check("a press just before touchdown fires the burst on landing", _burstCount == burstsBefore + 1,
            $"bursts={_burstCount - burstsBefore} landed={_landedCount - landingsBefore}");
        CheckNear("buffered burst also reaches the tuned speed", _player.LocomotionSpeed,
            js.LandingBurstSpeedFraction * m.HardMaxLocomotionSpeed, 2.5f);
        ReleaseAll();
        foreach (var _ in Seconds(1.0f)) yield return null;

        // (d) a plain fall (no slam) never opens the window: Space on landing is a charge
        foreach (var _ in Settle(PlatformCenter + Vector3.Up * 8f, 0.05f)) yield return null;
        landingsBefore = _landedCount; burstsBefore = _burstCount;
        guard = 0;
        while (_landedCount == landingsBefore && guard++ < 120) yield return null;
        Check("a plain landing does not open the burst window", !_player.BurstWindowOpen);
        Input.ActionPress(InputBootstrap.Jump, 1f);
        foreach (var _ in Act()) yield return null;
        Check("Space after a plain landing is a charge, never a burst",
            _player.IsCharging && _burstCount == burstsBefore, $"charging={_player.IsCharging}");
        Input.ActionRelease(InputBootstrap.Jump);
        ReleaseAll();
        foreach (var _ in Seconds(2.5f)) yield return null;

        // (e) the burst is a floor: a faster ball is never slowed and never turned
        {
            float savedFraction = js.LandingBurstSpeedFraction;
            js.LandingBurstSpeedFraction = 0.05f;
            foreach (var _ in Settle(PlatformCenter + Vector3.Up * 8f, 0.05f)) yield return null;
            _player.LinearVelocity = Vector3.Right * 40f;
            foreach (var _ in Frames(1)) yield return null;
            Input.ActionPress(InputBootstrap.Jump, 1f);              // slam
            foreach (var _ in Act()) yield return null;
            Input.ActionRelease(InputBootstrap.Jump);
            Input.ActionPress(InputBootstrap.Jump, 1f);              // early burst press
            foreach (var _ in Act()) yield return null;
            Input.ActionRelease(InputBootstrap.Jump);
            landingsBefore = _landedCount; burstsBefore = _burstCount;
            guard = 0;
            while (_landedCount == landingsBefore && guard++ < 120) yield return null;
            foreach (var _ in Act()) yield return null;
            Check("burst fired for the floor case", _burstCount == burstsBefore + 1);
            Check("burst never slows a faster ball", _player.Velocity.Dot(Vector3.Right) > 36f,
                $"vel.x={_player.Velocity.X:0.0}");
            js.LandingBurstSpeedFraction = savedFraction;
            ReleaseAll();
            foreach (var _ in Seconds(1.0f)) yield return null;
        }

        // ---- slam preserves lateral momentum and commits downward ----
        foreach (var _ in Settle(PlatformCenter + Vector3.Up * 3f)) yield return null;
        Input.ActionPress(InputBootstrap.MoveForward, 1f);
        foreach (var _ in Seconds(2.0f)) yield return null;
        Input.ActionPress(InputBootstrap.Jump, 1f);
        foreach (var _ in Seconds(js.MaxJumpChargeSeconds + 0.1f)) yield return null;
        Input.ActionRelease(InputBootstrap.Jump);
        foreach (var _ in Frames(10)) yield return null;
        ReleaseAll();
        float lateralBefore = FlatVel.Length();
        Input.ActionPress(InputBootstrap.Jump, 1f);
        foreach (var _ in Act()) yield return null;
        Check("slam preserves lateral momentum",
            FlatVel.Length() >= lateralBefore * js.SlamLateralRetention - 0.6f,
            $"{lateralBefore:0.0} -> {FlatVel.Length():0.0}");
        Check("slam commits downward", _player.VerticalSpeed <= -js.SlamInitialDownwardSpeed + 0.5f,
            $"vy={_player.VerticalSpeed:0.0}");
        ReleaseAll();
        foreach (var _ in Seconds(2.5f)) yield return null;

        // ---- charge release grace after losing ground ----
        foreach (var e in RunChargeGraceCase(true)) yield return e;
        foreach (var e in RunChargeGraceCase(false)) yield return e;

        // ---- air steering is bounded relative to ground steering ----
        foreach (var _ in Settle(PlatformCenter + Vector3.Up * 3f)) yield return null;
        Input.ActionPress(InputBootstrap.MoveForward, 1f);
        foreach (var _ in Seconds(2.0f)) yield return null;
        Vector3 groundHeading = FlatVel.Normalized();
        Input.ActionRelease(InputBootstrap.MoveForward);
        Input.ActionPress(InputBootstrap.MoveLeft, 1f);
        foreach (var _ in Seconds(0.5f)) yield return null;
        float groundTurn = Mathf.RadToDeg(groundHeading.AngleTo(FlatVel.Normalized()));
        ReleaseAll();

        foreach (var _ in Settle(PlatformCenter + Vector3.Up * 3f)) yield return null;
        Input.ActionPress(InputBootstrap.MoveForward, 1f);
        foreach (var _ in Seconds(2.0f)) yield return null;
        Input.ActionPress(InputBootstrap.Jump, 1f);
        foreach (var _ in Seconds(js.MaxJumpChargeSeconds + 0.1f)) yield return null;
        Input.ActionRelease(InputBootstrap.Jump);
        foreach (var _ in Frames(8)) yield return null;
        Vector3 airHeading = FlatVel.Normalized();
        Input.ActionRelease(InputBootstrap.MoveForward);
        Input.ActionPress(InputBootstrap.MoveLeft, 1f);
        foreach (var _ in Seconds(0.5f)) yield return null;
        float airTurn = Mathf.RadToDeg(airHeading.AngleTo(FlatVel.Normalized()));
        Check("air steering is bounded and weaker than ground steering",
            airTurn > 0.2f && airTurn < groundTurn * 0.75f, $"air={airTurn:0.0} ground={groundTurn:0.0} deg");
        ReleaseAll();
        foreach (var _ in Seconds(3.0f)) yield return null;

        // ---- cap decomposition across the grounded/airborne transition ----
        foreach (var _ in Settle(PlatformCenter + Vector3.Up * 3f)) yield return null;
        Input.ActionPress(InputBootstrap.MoveForward, 1f);
        Input.ActionPress(InputBootstrap.Boost, 1f);
        foreach (var _ in Seconds(5f)) yield return null;
        Input.ActionRelease(InputBootstrap.Boost);
        Vector3 preJumpHeading = FlatVel.Normalized();
        float preJumpSpeed = FlatVel.Length();
        Input.ActionPress(InputBootstrap.Jump, 1f);
        foreach (var _ in Frames(1)) yield return null;
        Input.ActionRelease(InputBootstrap.Jump);
        Input.ActionRelease(InputBootstrap.MoveForward);
        foreach (var _ in Act()) yield return null;
        float airborneSpeed = FlatVel.Length();
        float headingRotation = Mathf.RadToDeg(preJumpHeading.AngleTo(FlatVel.Normalized()));
        Check("grounded->airborne transition does not rotate velocity", headingRotation < 1.0f,
            $"rotation={headingRotation:0.00} deg");
        Check("grounded->airborne transition does not manufacture energy", airborneSpeed <= preJumpSpeed + 0.3f,
            $"{preJumpSpeed:0.00} -> {airborneSpeed:0.00}");
        Check("locomotion stays within the cap while airborne", airborneSpeed <= m.HardMaxLocomotionSpeed + 0.5f,
            $"speed={airborneSpeed:0.00}");
        ReleaseAll();
        foreach (var _ in Seconds(3f)) yield return null;

        // ---- boost works in the air ----
        foreach (var _ in Settle(PlatformCenter + Vector3.Up * 3f)) yield return null;
        _player.RefillBoost(t.Boost.BoostCapacity);
        Input.ActionPress(InputBootstrap.MoveForward, 1f);
        foreach (var _ in Seconds(1.0f)) yield return null;
        Input.ActionPress(InputBootstrap.Jump, 1f);
        foreach (var _ in Seconds(js.MaxJumpChargeSeconds + 0.1f)) yield return null;
        Input.ActionRelease(InputBootstrap.Jump);
        foreach (var _ in Frames(6)) yield return null;
        float airSpeedBefore = FlatVel.Length();
        Input.ActionPress(InputBootstrap.Boost, 1f);
        foreach (var _ in Seconds(0.5f)) yield return null;
        Check("boost works in the air", FlatVel.Length() > airSpeedBefore + 2f,
            $"{airSpeedBefore:0.0} -> {FlatVel.Length():0.0}");
        ReleaseAll();
        foreach (var _ in Seconds(3f)) yield return null;

        // ---- boost drain and slow passive regeneration ----
        foreach (var _ in Settle(PlatformCenter + Vector3.Up * 3f)) yield return null;
        _player.RefillBoost(t.Boost.BoostCapacity);
        float boostFull = _player.BoostAmount;
        Input.ActionPress(InputBootstrap.MoveForward, 1f);
        Input.ActionPress(InputBootstrap.Boost, 1f);
        foreach (var _ in Seconds(1.0f)) yield return null;
        float drained = boostFull - _player.BoostAmount;
        CheckNear("boost drains at the tuned rate", drained, t.Boost.BoostDrainRate, t.Boost.BoostDrainRate * 0.2f);
        Input.ActionRelease(InputBootstrap.Boost);
        float boostAfterDrain = _player.BoostAmount;
        foreach (var _ in Seconds(1.0f)) yield return null;
        CheckNear("passive boost regeneration follows tuning",
            _player.BoostAmount - boostAfterDrain, t.Boost.PassiveBoostRegen, t.Boost.PassiveBoostRegen * 0.3f);
        ReleaseAll();

        // ---- CCD: a max-speed run into a 0.3 m plate must stop, not tunnel ----
        foreach (var _ in Settle(_wallLaneStart)) yield return null;
        _player.RefillBoost(t.Boost.BoostCapacity);
        _worldDrive = _laneFwd;
        Input.ActionPress(InputBootstrap.Boost, 1f);
        float maxProgress = float.MinValue, maxSpeedSeen = 0f;
        foreach (var _ in Seconds(6f))
        {
            maxProgress = Mathf.Max(maxProgress, (_player.GlobalPosition - _wallCentre).Dot(_laneFwd));
            maxSpeedSeen = Mathf.Max(maxSpeedSeen, _player.LocomotionSpeed);
            yield return null;
        }
        float finalProgress = (_player.GlobalPosition - _wallCentre).Dot(_laneFwd);
        Check("reached the thin wall at playable max speed",
            maxSpeedSeen > m.HardMaxLocomotionSpeed - 3f && maxProgress > -3f,
            $"maxSpeed={maxSpeedSeen:0.0} closest={maxProgress:0.00} m");
        Check("did not tunnel through a 0.3 m plate at max speed (CCD)",
            finalProgress < 0f && maxProgress < 1.5f,
            $"final={finalProgress:0.00} m maxPast={maxProgress:0.00} m");
        Check("velocity finite after the high-speed impact", _player.Velocity.IsFinite());
        ReleaseAll();
        foreach (var _ in Seconds(0.5f)) yield return null;

        // ---- landing at the cap on a slope bleeds the tangent excess instead of clipping it ----
        foreach (var _ in Settle(_landingStart)) yield return null;
        _player.RefillBoost(t.Boost.BoostCapacity);
        _worldDrive = _laneFwd;
        Input.ActionPress(InputBootstrap.Boost, 1f);
        int landGuard = 0;
        while (_player.IsGrounded && landGuard++ < 900) yield return null;       // reach the crest and leave it
        Input.ActionRelease(InputBootstrap.Boost);
        _worldDrive = null;
        bool leftCrest = landGuard < 900;
        float speedAtCrest = _player.LocomotionSpeed;
        while (!_player.IsGrounded && landGuard++ < 1200) yield return null;     // land on the slope
        bool landed = landGuard < 1200;
        float prev = _player.LocomotionSpeed, worstDrop = 0f, peakOverCap = 0f;
        landingsBefore = _landedCount;
        foreach (var _ in Seconds(0.6f))
        {
            float now = _player.LocomotionSpeed;
            worstDrop = Mathf.Max(worstDrop, prev - now);
            peakOverCap = Mathf.Max(peakOverCap, now - m.HardMaxLocomotionSpeed);
            prev = now;
            yield return null;
        }
        Check("reached the cap and flew off the landing crest", leftCrest && speedAtCrest > m.HardMaxLocomotionSpeed - 3f,
            $"leftCrest={leftCrest} speed={speedAtCrest:0.0}");
        Check("landed on the 25 deg slope", landed);
        Check("landing at the cap on a slope produces a tangent excess (allowance engaged)", peakOverCap > 1.0f,
            $"peak over cap={peakOverCap:0.00} m/s (expect ~{m.HardMaxLocomotionSpeed * (1f / Mathf.Cos(Mathf.DegToRad(LandingSlopeDegrees)) - 1f):0.0})");
        float maxTickDrop = (m.LandingCapBleed + 5f) / Engine.PhysicsTicksPerSecond + 0.5f;
        Check("the excess bleeds instead of clipping in one tick", worstDrop < maxTickDrop,
            $"worst single-tick drop={worstDrop:0.00} m/s allowed<{maxTickDrop:0.00}");
        // The fall's vertical momentum also folds into tangent speed on landing, so the
        // excess can be well above the pure 1/cos(slope) figure; wait for the measured peak
        // to bleed at the tuned rate before asserting the cap is back.
        foreach (var _ in Seconds(Mathf.Max(0f, peakOverCap / Mathf.Max(1f, m.LandingCapBleed) - 0.6f) + 0.3f)) yield return null;
        Check("allowance decays back to the cap", _player.LocomotionSpeed <= m.HardMaxLocomotionSpeed + 0.5f,
            $"speed={_player.LocomotionSpeed:0.0} cap={m.HardMaxLocomotionSpeed} peakOverCap={peakOverCap:0.0} re-landings={_landedCount - landingsBefore}");
        ReleaseAll();

        // ---- camera occlusion probe pulls the camera in when terrain blocks the focus ----
        var rig = (Rushcore.Camera.CameraRig)_player.CameraBasis!;
        foreach (var _ in Settle(PlatformCenter + Vector3.Up * 3f)) yield return null;
        Check("camera is unobstructed in the open", rig.OcclusionFraction > 0.95f, $"fraction={rig.OcclusionFraction:0.00}");
        foreach (var _ in Settle(_wallCentre - Vector3.Up * 6f + _laneFwd * 3f + Vector3.Up * 3f)) yield return null;
        rig.SnapYawToward(_laneFwd);
        foreach (var _ in Frames(4)) yield return null;
        Check("camera pulls in when a wall blocks the line of sight", rig.OcclusionFraction < 0.5f,
            $"fraction={rig.OcclusionFraction:0.00}");
        t.Camera.OcclusionProbe = false;
        foreach (var _ in Seconds(1.5f)) yield return null;
        Check("occlusion probe can be disabled", rig.OcclusionFraction > 0.95f, $"fraction={rig.OcclusionFraction:0.00}");
        t.Camera.OcclusionProbe = true;

        // ---- chase camera: yaw follows the trajectory, never flips on reverse ----
        foreach (var _ in Settle(PlatformCenter + Vector3.Up * 3f)) yield return null;
        rig.SnapYawToward(Vector3.Forward);
        _worldDrive = Vector3.Right;
        foreach (var _ in Seconds(3.0f)) yield return null;
        Check("chase yaw converges on the travel heading",
            rig.FlatForward.Dot(Vector3.Right) > 0.96f,
            $"camFwd={rig.FlatForward} yaw={rig.YawDegreesCurrent:0.0}");
        // ---- S is a brake (D-076): sheds speed along the heading, never reverses, never turns the view ----
        float speedBeforeBrake = _player.LocomotionSpeed;
        _worldDrive = Vector3.Left;                      // camera faces +X, so this is pure S
        float minAlong = float.MaxValue, minCamDot = 1f;
        foreach (var _ in Seconds(2.5f))
        {
            minAlong = Mathf.Min(minAlong, _player.Velocity.Dot(Vector3.Right));
            minCamDot = Mathf.Min(minCamDot, rig.FlatForward.Dot(Vector3.Right));
            yield return null;
        }
        Check("S brakes to a stop", speedBeforeBrake > 10f && _player.LocomotionSpeed < 1.5f,
            $"before={speedBeforeBrake:0.0} after={_player.LocomotionSpeed:0.0}");
        Check("S never drives backward", minAlong > -1f, $"min along heading={minAlong:0.0}");
        Check("braking never turns the chase camera", minCamDot > 0.96f, $"min camFwd.x={minCamDot:0.00}");
        ReleaseAll();

        // ---- reversed heading with no forward input: the view swings behind the new travel direction ----
        _player.LinearVelocity = Vector3.Left * 25f;
        foreach (var _ in Seconds(2.5f)) yield return null;
        Check("camera follows a reversal the player is not fighting",
            rig.FlatForward.Dot(Vector3.Left) > 0.9f && _player.Velocity.Dot(Vector3.Left) > 5f,
            $"camFwd={rig.FlatForward} vel.x={_player.Velocity.X:0.0}");

        // ---- W against a short reversal: yaw holds, the recovery never swings the view ----
        foreach (var _ in Settle(PlatformCenter + Vector3.Up * 3f)) yield return null;
        rig.SnapYawToward(Vector3.Right);
        foreach (var _ in Frames(2)) yield return null;
        _player.LinearVelocity = Vector3.Left * 20f;     // rolling toward the camera
        Input.ActionPress(InputBootstrap.MoveForward, 1f); // raw W: push back the way the view faces
        bool heldEarly = false;
        minCamDot = 1f;
        int tick = 0;
        foreach (var _ in Seconds(1.5f))
        {
            if (tick++ == 12) heldEarly = rig.ReverseHoldActive && _player.Velocity.Dot(Vector3.Left) > 5f;
            minCamDot = Mathf.Min(minCamDot, rig.FlatForward.Dot(Vector3.Right));
            yield return null;
        }
        Check("W against a reversal holds the yaw", heldEarly, $"holdActive={rig.ReverseHoldActive}");
        Check("a quick recovery never swings the view", minCamDot > 0.96f && _player.Velocity.Dot(Vector3.Right) > 5f,
            $"min camFwd.x={minCamDot:0.00} vel.x={_player.Velocity.X:0.0}");
        ReleaseAll();

        // ---- W+A against a reversal is a deliberate hairpin, and it turns to the input side ----
        foreach (var _ in Settle(PlatformCenter + Vector3.Up * 3f)) yield return null;
        rig.SnapYawToward(Vector3.Right);                // view +X: A is camera-left = -Z
        foreach (var _ in Frames(2)) yield return null;
        _player.LinearVelocity = Vector3.Left * 20f;
        Input.ActionPress(InputBootstrap.MoveForward, 1f);
        Input.ActionPress(InputBootstrap.MoveLeft, 1f);
        float maxSide = 0f, minSide = 0f;
        foreach (var _ in Seconds(0.35f))
        {
            maxSide = Mathf.Max(maxSide, _player.Velocity.Z);
            minSide = Mathf.Min(minSide, _player.Velocity.Z);
            yield return null;
        }
        Check("a hairpin turns to the input side", minSide < -5f && maxSide < 1f,
            $"vel.z range [{minSide:0.0}, {maxSide:0.0}] vel={_player.Velocity}");
        ReleaseAll();

        // ---- W against a long reversal: the hold is bounded, the camera still ends up behind the travel ----
        foreach (var _ in Settle(PlatformCenter + Vector3.Up * 3f)) yield return null;
        rig.SnapYawToward(Vector3.Right);
        foreach (var _ in Frames(2)) yield return null;
        _player.LinearVelocity = Vector3.Left * 60f;
        Input.ActionPress(InputBootstrap.MoveForward, 1f);
        foreach (var _ in Seconds(t.Camera.YawReverseHoldSeconds + 0.35f)) yield return null;
        Check("the reverse hold is bounded",
            !rig.ReverseHoldActive && rig.FlatForward.Dot(Vector3.Right) < 0.95f,
            $"camFwd={rig.FlatForward} hold={rig.ReverseHoldActive} vel.x={_player.Velocity.X:0.0}");
        foreach (var _ in Seconds(2.5f)) yield return null;
        Vector3 travelDir = FlatVel.Length() > 1e-3f ? FlatVel.Normalized() : Vector3.Zero;
        Check("camera ends up behind the direction of travel",
            FlatVel.Length() > 5f && rig.FlatForward.Dot(travelDir) > 0.9f,
            $"camFwd={rig.FlatForward} travel={travelDir} speed={FlatVel.Length():0.0}");
        ReleaseAll();

        // ---- real terrain crest run: climb, mesa lip, ramp, drop, chasms at speed ----
        foreach (var _ in Settle(_debug.World.SurfacePoint(TerrainHeightField.Ramp1X, 210f, m.BallRadius + 1.5f), 0.8f)) yield return null;
        rig.SnapYawToward(Vector3.Forward);
        foreach (var _ in Frames(3)) yield return null;
        _player.RefillBoost(t.Boost.BoostCapacity);
        _worldDrive = Vector3.Forward;                   // -Z through the whole central column
        Input.ActionPress(InputBootstrap.Boost, 1f);
        _visFrames = 0; _visBlocked = 0; _visCheck = true;
        int floored = 0;
        foreach (var _ in Seconds(11f))
        {
            if (rig.FlooredThisFrame) floored++;
            if (_player.GlobalPosition.Z < -430f) break;
            yield return null;
        }
        _visCheck = false;
        ReleaseAll();
        Check("crest run covered the central column", _player.GlobalPosition.Z < -150f,
            $"z={_player.GlobalPosition.Z:0}");
        Check("camera->ball line stays clear over crests (<=2% of ticks)",
            _visFrames > 0 && _visBlocked <= Mathf.CeilToInt(_visFrames * 0.02f),
            $"blocked {_visBlocked}/{_visFrames} ticks, floored {floored} frames");

        // ---- tuning persistence: diff-only JSON round-trip on a scratch path ----
        {
            const string scratch = "user://tuning_selftest.json";
            float driveDefault = m.GroundDriveAcceleration;
            bool probeDefault = t.Camera.OcclusionProbe;
            Check("harness starts on compiled defaults", t.OverrideCount == 0, $"overrides={t.OverrideCount}");
            m.GroundDriveAcceleration = driveDefault + 7f;
            t.Camera.OcclusionProbe = !probeDefault;
            Check("modified values are counted", t.OverrideCount == 2, $"overrides={t.OverrideCount}");
            Check("override saves", t.SaveOverride(scratch));

            string text = Godot.FileAccess.GetFileAsString(scratch);
            var parsed = Json.ParseString(text).Obj as Godot.Collections.Dictionary;
            var values = parsed?["values"].Obj as Godot.Collections.Dictionary;
            Check("override file is a versioned diff of only the changed values",
                parsed is not null && (int)parsed["version"] == 1 && values is { Count: 2 },
                $"keys={values?.Count}");

            t.ResetAll();
            Check("reset returns to compiled defaults", t.OverrideCount == 0 && Mathf.IsEqualApprox(m.GroundDriveAcceleration, driveDefault));
            int applied = t.LoadOverride(scratch);
            Check("override loads on top of defaults",
                applied == 2 && Mathf.IsEqualApprox(m.GroundDriveAcceleration, driveDefault + 7f) && t.Camera.OcclusionProbe == !probeDefault,
                $"applied={applied} drive={m.GroundDriveAcceleration}");

            t.ResetAll();
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(scratch));
            Check("scratch override removed and defaults restored", !Godot.FileAccess.FileExists(scratch) && t.OverrideCount == 0);
        }

        // ---- named presets: save, list, load, delete ----
        {
            var installed = GameplayTuning.ListPresets();
            Check("bundled starter presets were installed", installed.Contains("baseline") && installed.Contains("arcade-snap"),
                $"presets={string.Join(",", installed)}");
            // Validate what ships in the repo, not whatever else the developer has stashed locally.
            var bundled = new List<string>();
            using (var dir = DirAccess.Open(GameplayTuning.BundledPresetDir))
                foreach (string f in dir?.GetFiles() ?? System.Array.Empty<string>())
                    if (f.EndsWith(".json")) bundled.Add(f[..^5]);
            // Every bundled preset must load with every key recognised: a typo in a key
            // would otherwise be dropped silently by the loader.
            var unrecognised = new List<string>();
            foreach (string preset in bundled)
            {
                var doc = Json.ParseString(Godot.FileAccess.GetFileAsString(GameplayTuning.PresetPath(preset))).Obj as Godot.Collections.Dictionary;
                int keyCount = doc?["values"].Obj is Godot.Collections.Dictionary dv ? dv.Count : -1;
                int applied = t.LoadPreset(preset);
                if (applied != keyCount) unrecognised.Add($"{preset}({applied}/{keyCount})");
            }
            Check("every bundled preset loads with all keys recognised", unrecognised.Count == 0,
                string.Join(" ", unrecognised));
            t.ResetAll();
            string name = GameplayTuning.SanitizePresetName(" selftest/variant #A ");
            Check("preset names are made file-safe", name == "selftest_variant__A", $"name='{name}'");
            float driveDefault = m.GroundDriveAcceleration;
            m.GroundDriveAcceleration = driveDefault + 3f;
            Check("preset saves", t.SavePreset(name));
            Check("preset appears in the list", GameplayTuning.ListPresets().Contains(name));
            t.ResetAll();
            Check("preset loads on top of defaults",
                t.LoadPreset(name) == 1 && Mathf.IsEqualApprox(m.GroundDriveAcceleration, driveDefault + 3f));
            t.ResetAll();
            Check("preset deletes", GameplayTuning.DeletePreset(name) && !GameplayTuning.PresetExists(name));
        }

        // ---- Gate M1 scale strip: builds, and its instruments have their stated geometry ----
        {
            t.World.CalibrationStrip = true;
            _debug.RestartSameSeed();
            foreach (var _ in Frames(3)) yield return null;
            var world = _debug.World;
            Check("scale strip is the active terrain", world.IsStrip && world.HalfX > 3000f, $"halfX={world.HalfX}");
            CheckNear("runway is flat", world.SampleHeight(ScaleStripHeightField.X(500f), 0f), 0f, 0.05f);
            float wall = world.SampleHeight(ScaleStripHeightField.X(1875f), 60f);
            Check("40 m corridor walls stand beside the lane", wall > 25f, $"h={wall:0.0}");
            CheckNear("40 m corridor floor is flat", world.SampleHeight(ScaleStripHeightField.X(1875f), 0f), 0f, 0.05f);
            CheckNear("800 m hill station crests at 80 m", world.SampleHeight(ScaleStripHeightField.X(3800f), 0f), 80f, 1.0f);
            CheckNear("80 m gap floor is 20 m deep",
                world.SampleHeight(ScaleStripHeightField.X(ScaleStripHeightField.GapRimS[1] + 20f), 0f), -20f, 1.0f);
            float lip = 0f;
            for (float d = 12f; d >= 0f; d -= 4f)
                lip = Mathf.Max(lip, world.SampleHeight(ScaleStripHeightField.X(ScaleStripHeightField.RampLipS(2) - d), 180f));
            CheckNear("27 deg ramp lip stands 20 m", lip, ScaleStripHeightField.RampRise, 1.5f);
            CheckNear("turn pad is flat", world.SampleHeight(ScaleStripHeightField.X(ScaleStripHeightField.PadCentreS), 100f), 0f, 0.05f);
            foreach (var _ in Seconds(1.0f)) yield return null;
            Check("player spawned on the strip, grounded, facing down it",
                _player.IsGrounded && _player.GlobalPosition.X > 3000f && Forward.Dot(Vector3.Left) > 0.99f,
                $"pos={_player.GlobalPosition} fwd={Forward}");
            // ---- M1 budget: facet hops over the 800 m hill station, 4 m vs 8 m cells (W held from 60 m/s) ----
            int[] hops = new int[2];
            float[] groundedFrac = new float[2];
            int[] tris = new int[2];
            ulong[] buildMs = new ulong[2];
            float[] cells = { 4f, 8f };
            for (int k = 0; k < cells.Length; k++)
            {
                t.World.CellSize = cells[k];
                _debug.RestartSameSeed();
                foreach (var _ in Frames(3)) yield return null;
                world = _debug.World;
                tris[k] = world.Triangles;
                buildMs[k] = world.BuildMillis;
                foreach (var _ in Settle(world.SurfacePoint(ScaleStripHeightField.X(3400f), 0f, m.BallRadius + 0.4f), 0.6f)) yield return null;
                _player.LinearVelocity = Vector3.Left * 60f;
                Input.ActionPress(InputBootstrap.MoveForward, 1f);
                int ticks = 0, airborne = 0, blips = 0, airStart = 0;
                bool wasGrounded = true;
                while (_player.GlobalPosition.X > ScaleStripHeightField.X(4200f) && ticks < Engine.PhysicsTicksPerSecond * 25)
                {
                    ticks++;
                    bool g = _player.IsGrounded;
                    if (!g) airborne++;
                    if (wasGrounded && !g) airStart = ticks;
                    if (!wasGrounded && g && ticks - airStart <= 9) blips++;   // <= 0.15 s: a facet hop, not a crest launch
                    wasGrounded = g;
                    yield return null;
                }
                ReleaseAll();
                hops[k] = blips;
                groundedFrac[k] = ticks > 0 ? 1f - airborne / (float)ticks : 0f;
            }
            GD.Print($"[SELFTEST] M1 budget  4 m: {tris[0] / 1000} k tris, build {buildMs[0]} ms, hops {hops[0]}, grounded {groundedFrac[0]:P0}   " +
                     $"8 m: {tris[1] / 1000} k tris, build {buildMs[1]} ms, hops {hops[1]}, grounded {groundedFrac[1]:P0}");
            Check("cell size is a live world parameter", Mathf.IsEqualApprox(_debug.World.CellSize, 8f), $"cell={_debug.World.CellSize}");
            Check("8 m cells cut the triangle count to about a quarter", tris[1] < tris[0] * 0.3f, $"{tris[0]} -> {tris[1]}");
            Check("4 m facets cause few short hops over the hill station", hops[0] <= 6, $"hops={hops[0]} grounded={groundedFrac[0]:P0}");
            Check("hill station run completed", groundedFrac[0] > 0.3f && groundedFrac[1] > 0.3f, $"grounded {groundedFrac[0]:P0} / {groundedFrac[1]:P0}");

            t.World.CellSize = MovementToyWorld.DefaultCellSize;
            t.World.CalibrationStrip = false;
            _debug.RestartSameSeed();
            foreach (var _ in Frames(3)) yield return null;
            Check("lab terrain restored", !_debug.World.IsStrip && Mathf.IsEqualApprox(_debug.World.HalfX, MovementToyWorld.Extent * 0.5f)
                && Mathf.IsEqualApprox(_debug.World.CellSize, MovementToyWorld.DefaultCellSize));
        }

        // ---- Phase 2 generation, pure data: route skeletons for a seed batch (04 §12, 08 §5) ----
        RunStageGenerationBatchCase();

        // ---- route speed model (D-081): the generator's speed oracle must track the real ball ----
        foreach (var e in RunRouteSpeedModelCase()) yield return e;

        // ---- Phase 2 dependency (04 §9): NaN vertices in HeightMapShape3D are holes under Jolt ----
        {
            var body = new StaticBody3D { Name = "NaNHoleTest", Position = new Vector3(0f, PlatformY, -2200f) };
            var shape = new HeightMapShape3D { MapWidth = 9, MapDepth = 9 };
            var data = new float[81];
            for (int zi = 3; zi <= 5; zi++)
                for (int xi = 3; xi <= 5; xi++)
                    data[zi * 9 + xi] = float.NaN;
            shape.MapData = data;
            body.AddChild(new CollisionShape3D { Shape = shape, Scale = Vector3.One * 4f });
            AddChild(body);
            foreach (var _ in Frames(2)) yield return null;
            foreach (var _ in Settle(new Vector3(0f, PlatformY + 3f, -2200f), 0.6f)) yield return null;
            Check("NaN cells in a HeightMapShape3D are holes: the ball falls through",
                !_player.IsGrounded && _player.GlobalPosition.Y < PlatformY - 3f, $"y={_player.GlobalPosition.Y:0.0} grounded={_player.IsGrounded}");
            foreach (var _ in Settle(new Vector3(12f, PlatformY + 3f, -2200f), 1.0f)) yield return null;
            Check("cells beside the hole still collide",
                _player.IsGrounded && Mathf.Abs(_player.GlobalPosition.Y - (PlatformY + m.BallRadius)) < 0.8f,
                $"y={_player.GlobalPosition.Y:0.0} grounded={_player.IsGrounded}");
            body.QueueFree();
            foreach (var _ in Frames(2)) yield return null;
        }

        // ---- fall recovery ----
        foreach (var _ in Seconds(0.5f)) yield return null;
        _player.SetCheckpoint(PlatformCenter + Vector3.Up * 3f);
        int recoveredBefore = _recoveredCount;
        _player.TeleportTo(new Vector3(0f, -5000f, 0f));
        foreach (var _ in Frames(4)) yield return null;
        _player.RequestRecovery();
        foreach (var _ in Frames(8)) yield return null;
        Check("recovery restores a valid pose",
            _player.GlobalPosition.DistanceTo(PlatformCenter + Vector3.Up * 3f) < 4f,
            $"pos={_player.GlobalPosition}");
        Check("recovery clears transient movement state",
            !_player.IsCharging && !_player.SlamActive && !_player.BurstWindowOpen);
        Check("recovery resets physics interpolation", _recoveredCount > recoveredBefore);

        foreach (var _ in Seconds(0.5f)) yield return null;
        Check("velocity is finite at the end of the run", _player.Velocity.IsFinite());
        Check("camera lens never went below the heightfield during the whole run", _groundViolations == 0,
            $"violations={_groundViolations}");
    }

    /// <summary>
    /// Validates the calibration terrain itself and then confirms gravity still
    /// materially accelerates the player on it (D-005). The engineered grade fan exists
    /// precisely so this can be measured against known geometry, and it also feeds the
    /// scale-calibration gate M1.
    /// </summary>
    private IEnumerable RunRealTerrainSlopeCase()
    {
        var world = _debug.World;
        var m = _debug.Tuning.Movement;

        // --- geometry: the instruments must have the gradients they advertise ---
        CheckGrade("8 deg", TerrainHeightField.Grade8X, 8f);
        CheckGrade("15 deg", TerrainHeightField.Grade15X, 15f);
        CheckGrade("25 deg", TerrainHeightField.Grade25X, 25f);

        float laneMin = float.MaxValue, laneMax = float.MinValue;
        for (float d = 0f; d <= TerrainHeightField.LaneLength; d += 10f)
        {
            float h = world.SampleHeight(TerrainHeightField.LaneStartX - d, TerrainHeightField.LaneZ);
            laneMin = Mathf.Min(laneMin, h);
            laneMax = Mathf.Max(laneMax, h);
        }
        Check("measured calibration lane is flat over its full 500 m",
            laneMax - laneMin < 0.5f, $"height varies {laneMax - laneMin:0.###} m");

        // --- behaviour: steeper grades must accelerate the player harder ---
        float a15 = 0f, a25 = 0f;
        foreach (var e in MeasureGradeAcceleration(TerrainHeightField.Grade15X, 15f, v => a15 = v)) yield return e;
        foreach (var e in MeasureGradeAcceleration(TerrainHeightField.Grade25X, 25f, v => a25 = v)) yield return e;

        float ideal15 = m.Gravity * Mathf.Sin(Mathf.DegToRad(15f));
        float ideal25 = m.Gravity * Mathf.Sin(Mathf.DegToRad(25f));
        Check("15 deg grade materially accelerates the player", a15 > ideal15 * 0.7f,
            $"measured={a15:0.00} ideal~{ideal15:0.00} m/s^2");
        Check("25 deg grade materially accelerates the player", a25 > ideal25 * 0.7f,
            $"measured={a25:0.00} ideal~{ideal25:0.00} m/s^2");
        Check("steeper terrain is strategically faster", a25 > a15 + 1.0f,
            $"15deg={a15:0.00} vs 25deg={a25:0.00} m/s^2");
    }

    /// <summary>
    /// The toy's active-refill hook: rolling the 15 deg reward line must collect rings
    /// and refill the meter by the tuned amount (03 §10, D-020).
    /// </summary>
    private IEnumerable RunBoostPickupCase()
    {
        var t = _debug.Tuning;

        // Drain the meter while parked on the rig (boost persists across teleports), so
        // the first ring collected on the reward line produces a measurable refill.
        foreach (var _ in Settle(PlatformCenter + Vector3.Up * 3f, 0.6f)) yield return null;
        Input.ActionPress(InputBootstrap.Boost, 1f);
        foreach (var _ in Seconds(t.Boost.BoostCapacity / Mathf.Max(1f, t.Boost.BoostDrainRate) + 0.2f)) yield return null;
        Input.ActionRelease(InputBootstrap.Boost);

        Vector3 start = _debug.World.SurfacePoint(TerrainHeightField.Grade15X, TerrainHeightField.FanCrestZ - 4f,
            t.Movement.BallRadius + 1.5f);
        foreach (var _ in Settle(start, 0.3f)) yield return null;
        float before = _player.BoostAmount;
        int pickupsBefore = _pickupCount;

        int guard = 0;
        while (_pickupCount == pickupsBefore && guard++ < 600) yield return null;
        foreach (var _ in Act()) yield return null;

        Check("boost pickup line is collectable on the 15 deg grade", _pickupCount > pickupsBefore,
            $"pickups={_pickupCount - pickupsBefore} after {guard} frames");
        Check("pickup refills boost by the tuned amount",
            _player.BoostAmount >= before + t.Boost.PickupRefillAmount - 2f,
            $"{before:0.0} -> {_player.BoostAmount:0.0} (expected +{t.Boost.PickupRefillAmount})");
        ReleaseAll();
    }

    private void CheckGrade(string label, float x, float expectedDegrees)
    {
        var world = _debug.World;
        // Sample inside the linear section, clear of the eased crest and runout.
        float hi = world.SampleHeight(x, 120f);
        float lo = world.SampleHeight(x, 80f);
        float measured = Mathf.RadToDeg(Mathf.Atan((hi - lo) / 40f));
        CheckNear($"grade fan {label} lane has its stated gradient", measured, expectedDegrees, 1.0f);
    }

    private IEnumerable MeasureGradeAcceleration(float x, float degrees, Action<float> report)
    {
        Vector3 start = _debug.World.SurfacePoint(x, 115f, _debug.Tuning.Movement.BallRadius + 1.5f);
        foreach (var _ in Settle(start, 0.9f)) yield return null;
        float v0 = _player.LocomotionSpeed;
        foreach (var _ in Seconds(1.5f)) yield return null;
        report((_player.LocomotionSpeed - v0) / 1.5f);
        ReleaseAll();
    }

    /// <summary>
    /// Drives off the plate edge while charging. releaseInsideGrace=true must still
    /// jump; false must cancel the charge instead (03 §7).
    /// </summary>
    private IEnumerable RunChargeGraceCase(bool releaseInsideGrace)
    {
        var js = _debug.Tuning.JumpSlam;
        string label = releaseInsideGrace ? "inside grace" : "after grace expiry";

        foreach (var _ in Settle(PlatformCenter + Forward * 300f + Vector3.Up * 3f)) yield return null;
        Input.ActionPress(InputBootstrap.MoveForward, 1f);
        foreach (var _ in Seconds(2.0f)) yield return null;
        Input.ActionPress(InputBootstrap.Jump, 1f);
        foreach (var _ in Act()) yield return null;

        int guard = 0;
        while (_player.IsRawGrounded && guard++ < 1200) yield return null;
        bool leftGround = guard < 1200;

        int jumpsBefore = _jumpedCount;
        int cancelsBefore = _chargeCanceledCount;

        if (releaseInsideGrace)
        {
            Input.ActionRelease(InputBootstrap.Jump);
            foreach (var _ in Act()) yield return null;
            Check($"charge survives ground loss and releasing {label} still jumps",
                leftGround && _jumpedCount == jumpsBefore + 1,
                $"leftGround={leftGround} jumps={_jumpedCount - jumpsBefore}");
        }
        else
        {
            foreach (var _ in Seconds(js.ChargeReleaseGraceSeconds + 0.25f)) yield return null;
            bool canceled = _chargeCanceledCount == cancelsBefore + 1 && !_player.IsCharging;
            Input.ActionRelease(InputBootstrap.Jump);
            foreach (var _ in Act()) yield return null;
            Check($"charge cancels {label} and release does not jump",
                leftGround && canceled && _jumpedCount == jumpsBefore,
                $"leftGround={leftGround} canceled={canceled} jumps={_jumpedCount - jumpsBefore}");
        }

        ReleaseAll();
        foreach (var _ in Seconds(0.4f)) yield return null;
    }

    // ---------------- route speed model calibration (04 §12, 08 §5, D-081) ----------------

    /// <summary>Real (arclength, speed) samples of one drive, one per physics tick.</summary>
    private readonly List<(float s, float v)> _trace = new();

    private static float TraceSpeedAt(List<(float s, float v)> trace, float s)
    {
        for (int i = 1; i < trace.Count; i++)
        {
            if (trace[i].s < s) continue;
            float span = trace[i].s - trace[i - 1].s;
            float f = span > 0f ? (s - trace[i - 1].s) / span : 1f;
            return Mathf.Lerp(trace[i - 1].v, trace[i].v, f);
        }
        return float.NaN;
    }

    private IEnumerable RunRouteSpeedModelCase()
    {
        var t = _debug.Tuning;
        var m = t.Movement;
        var model = new RouteSpeedModel(m);
        const float Tolerance = 0.05f;

        // --- envelope helpers ---
        CheckNear("corner limit at 100 m is the cap-scale turn radius", model.CornerSpeedLimit(100f), m.HardMaxLocomotionSpeed, 3f);
        Check("corner limit tightens with radius", model.CornerSpeedLimit(25f) < model.CornerSpeedLimit(50f) && model.CornerSpeedLimit(50f) < model.CornerSpeedLimit(100f),
            $"25={model.CornerSpeedLimit(25f):0.0} 50={model.CornerSpeedLimit(50f):0.0} 100={model.CornerSpeedLimit(100f):0.0}");
        CheckNear("turn radius is the inverse of the corner limit", model.TurnRadius(model.CornerSpeedLimit(60f)), 60f, 0.5f);
        CheckNear("crest launch radius at the cap", model.CrestLaunchRadius(m.HardMaxLocomotionSpeed), m.HardMaxLocomotionSpeed * m.HardMaxLocomotionSpeed / m.Gravity, 0.1f);
        Check("800/80 cosine crest is a launch at the cap and a roll at 60 m/s",
            model.CrestIsLaunch(RouteSpeedModel.CosineCrestRadius(800f, 80f), m.HardMaxLocomotionSpeed)
            && !model.CrestIsLaunch(RouteSpeedModel.CosineCrestRadius(800f, 80f), 60f),
            $"r={RouteSpeedModel.CosineCrestRadius(800f, 80f):0.0}");

        // --- lab grade fan: drive held down each lane from wherever the ball is after settling ---
        float[] lanes = { TerrainHeightField.Grade8X, TerrainHeightField.Grade15X, TerrainHeightField.Grade25X };
        string[] laneNames = { "8 deg", "15 deg", "25 deg" };
        float[] fanChecks = { 25f, 50f, 75f };
        ulong firstHash = 0, secondHash = 0;
        for (int lane = 0; lane < lanes.Length; lane++)
        {
            float x = lanes[lane];
            foreach (var _ in Settle(_debug.World.SurfacePoint(x, 140f, m.BallRadius + 1.5f), 0.9f)) yield return null;
            float z0 = _player.GlobalPosition.Z;
            float v0 = _player.LocomotionSpeed;
            var poly = new List<Vector3>();
            for (float d = 0f; d <= 220f; d += 1f) poly.Add(_debug.World.SurfacePoint(x, z0 - d, m.BallRadius));
            var profile = model.Integrate(poly, v0);
            if (lane == 0) { firstHash = profile.Hash(); secondHash = model.Integrate(poly, v0).Hash(); }

            _trace.Clear();
            Vector3 prev = _player.GlobalPosition;
            float arc = 0f;
            _trace.Add((0f, v0));
            _worldDrive = Vector3.Forward;                  // -Z is downhill on the fan; re-pressed every frame under the moving camera
            int ticks = 0, groundedTicks = 0;
            while (arc < fanChecks[^1] + 5f && ticks++ < Engine.PhysicsTicksPerSecond * 6)
            {
                yield return null;
                if (_player.IsGrounded) groundedTicks++;
                Vector3 pos = _player.GlobalPosition;
                arc += pos.DistanceTo(prev);
                prev = pos;
                _trace.Add((arc, _player.LocomotionSpeed));
            }
            ReleaseAll();
            float worst = 0f;
            string detail = "";
            foreach (float s in fanChecks)
            {
                float real = TraceSpeedAt(_trace, s), pred = profile.SpeedAt(s);
                float err = float.IsNaN(real) ? 1f : Mathf.Abs(pred - real) / Mathf.Max(1f, real);
                worst = Mathf.Max(worst, err);
                detail += $" s={s:0}: real {real:0.0} model {pred:0.0} ({err:P1});";
            }
            GD.Print($"[SELFTEST] route speed model  fan {laneNames[lane]}: v0={v0:0.0} grounded={groundedTicks / (float)Mathf.Max(1, ticks):P0};{detail}");
            Check($"speed model tracks the {laneNames[lane]} grade-fan descent within 5%", worst <= Tolerance,
                $"v0={v0:0.0} grounded={groundedTicks / (float)Mathf.Max(1, ticks):P0};{detail}");
        }
        Check("speed model is deterministic (same polyline, same hash)", firstHash == secondHash && firstHash != 0, $"hash={firstHash:X}");

        // --- strip runway: 0 -> cap with drive held on the flat ---
        {
            t.World.CalibrationStrip = true;
            _debug.RestartSameSeed();
            foreach (var _ in Frames(3)) yield return null;
            var world = _debug.World;
            foreach (var _ in Settle(world.SurfacePoint(ScaleStripHeightField.StartX, 0f, m.BallRadius + 0.4f), 1.2f)) yield return null;
            float v0 = _player.LocomotionSpeed;
            float x0 = _player.GlobalPosition.X;
            var poly = RouteSpeedModel.StraightPolyline(_player.GlobalPosition, Vector3.Left, ScaleStripHeightField.RunwayEnd);
            var profile = model.Integrate(poly, v0);
            float modelCapS = float.NaN, modelCapT = float.NaN;
            for (int i = 0; i < profile.Count; i++)
                if (profile.Speed[i] >= m.HardMaxLocomotionSpeed - 0.5f) { modelCapS = profile.Distance[i]; modelCapT = profile.Time[i]; break; }

            _trace.Clear();
            _trace.Add((0f, v0));
            Input.ActionPress(InputBootstrap.MoveForward, 1f);
            int ticks = 0;
            float realCapS = float.NaN, realCapT = float.NaN;
            while (ticks++ < Engine.PhysicsTicksPerSecond * 14)
            {
                yield return null;
                float s = x0 - _player.GlobalPosition.X;
                _trace.Add((s, _player.LocomotionSpeed));
                if (float.IsNaN(realCapS) && _player.LocomotionSpeed >= m.HardMaxLocomotionSpeed - 0.5f)
                {
                    realCapS = s;
                    realCapT = ticks / (float)Engine.PhysicsTicksPerSecond;
                }
                if (!float.IsNaN(realCapS) || s > ScaleStripHeightField.RunwayEnd - 10f) break;
            }
            ReleaseAll();
            float[] checks = { 50f, 100f, 200f, 300f, 400f, 500f };
            float worst = 0f;
            string detail = "";
            foreach (float s in checks)
            {
                if (!float.IsNaN(realCapS) && s > realCapS) break;
                float real = TraceSpeedAt(_trace, s), pred = profile.SpeedAt(s);
                float err = float.IsNaN(real) ? 1f : Mathf.Abs(pred - real) / Mathf.Max(1f, real);
                worst = Mathf.Max(worst, err);
                detail += $" s={s:0}: real {real:0.0} model {pred:0.0} ({err:P1});";
            }
            GD.Print($"[SELFTEST] route speed model  runway 0->cap: real {realCapT:0.00} s / {realCapS:0} m, model {modelCapT:0.00} s / {modelCapS:0} m;{detail}");
            Check("speed model tracks the runway 0->cap curve within 5%", worst <= Tolerance, detail.Trim());
            Check("speed model predicts the 0->cap distance within 5%",
                !float.IsNaN(realCapS) && Mathf.Abs(modelCapS - realCapS) / realCapS <= Tolerance,
                $"real {realCapS:0} m, model {modelCapS:0} m");
            Check("speed model reaches and holds the cap", Mathf.IsEqualApprox(profile.Speed[^1], m.HardMaxLocomotionSpeed) && profile.MaxSpeed <= m.HardMaxLocomotionSpeed + 1e-3f,
                $"end={profile.Speed[^1]:0.0} max={profile.MaxSpeed:0.0}");

            // --- bends and stalls: the conservative rules do what the validators will lean on ---
            var bend = new List<Vector3>();
            Vector3 c = _player.GlobalPosition;
            for (float d = 0f; d <= 600f; d += 1f) bend.Add(c + Vector3.Left * d);
            const float R = 50f;
            Vector3 centre = bend[^1] + Vector3.Forward * R;
            for (float a = 0f; a <= Mathf.Pi * 0.5f; a += 1f / R)
                bend.Add(centre + new Vector3(-Mathf.Sin(a) * R, 0f, Mathf.Cos(a) * R));   // quarter circle, entered heading -X
            var bendProfile = model.Integrate(bend, 0f);
            Check("a 50 m bend caps the arrival speed at its corner limit",
                bendProfile.Speed[^1] <= model.CornerSpeedLimit(R) + 1f && bendProfile.Speed[^1] < profile.Speed[^1] - 10f,
                $"exit={bendProfile.Speed[^1]:0.0} limit={model.CornerSpeedLimit(R):0.0}");
            var uphill = new List<Vector3> { Vector3.Zero, new(0f, 30f, -40f), new(0f, 60f, -80f) };
            var stalled = model.Integrate(uphill, 5f, driveHeld: false);
            Check("a driverless ball stalls on a climb and the profile says so",
                stalled.Stalled && float.IsPositiveInfinity(stalled.Time[^1]), $"stalled={stalled.Stalled} at {stalled.StallVertex}");
            Check("with drive held the same climb is completed", !model.Integrate(uphill, 5f).Stalled);

            t.World.CalibrationStrip = false;
            _debug.RestartSameSeed();
            foreach (var _ in Frames(3)) yield return null;
        }
    }

    // ---------------- Phase 2: stage generation batch (04 §4, §5A, §12; 08 §5) ----------------

    private void RunStageGenerationBatchCase()
    {
        var gen = new StageGenerator(_debug.Tuning.Movement);
        const int Count = 100;
        var hashes = new HashSet<ulong>();
        int passed = 0, fallbacks = 0, deterministic = 0, bends = 0, committed = 0;
        float lenMin = float.MaxValue, lenMax = 0f, lenSum = 0f, tMin = float.MaxValue, tMax = 0f, tSum = 0f;
        double msSum = 0, msMax = 0;
        string firstFailure = "";
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < Count; i++)
        {
            var req = new StageGenerationRequest(RunSeed: 1 + i / 9, StageIndex: i % 9);
            var def = gen.Generate(req);
            var again = gen.Generate(req);
            if (def.Report.Passed) passed++; else if (firstFailure == "") firstFailure = $"seed {req.RunSeed}/{req.StageIndex}: " + string.Join("; ", def.Report.Failures.Select(f => f.Name + " " + f.Detail));
            if (def.Report.UsedFallback) { fallbacks++; if (fallbacks <= 3) GD.Print($"[SELFTEST] fallback seed {req.RunSeed}/{req.StageIndex}: " + string.Join("; ", def.Report.Checks.Where(c => c.Name.StartsWith("regeneration")).Select(c => c.Name + " " + c.Detail))); }
            if (i == 0) GD.Print($"[SELFTEST] generation timings: " + string.Join(", ", def.Report.Timings.Select(t => $"{t.phase} {t.ms:0.00} ms")));
            if (def.Hash() == again.Hash()) deterministic++;
            hashes.Add(def.Hash());
            bends += def.PrimaryRoute.Bends.Count;
            committed += def.PrimaryRoute.Bends.Count(b => b.Radius <= WorldScale.CommittedBendRadius + 1e-3f);
            float len = def.PrimaryRoute.Length, t = def.SpeedProfile.TotalTime;
            lenMin = Mathf.Min(lenMin, len); lenMax = Mathf.Max(lenMax, len); lenSum += len;
            tMin = Mathf.Min(tMin, t); tMax = Mathf.Max(tMax, t); tSum += t;
            msSum += def.Report.TotalMillis; msMax = Math.Max(msMax, def.Report.TotalMillis);
        }
        sw.Stop();
        GD.Print($"[SELFTEST] generation batch  {Count} stages: {passed} valid, {fallbacks} fallbacks, {hashes.Count} distinct; " +
                 $"length {lenMin:0}..{lenMax:0} (avg {lenSum / Count:0}) m; base-kit time {tMin:0.0}..{tMax:0.0} (avg {tSum / Count:0.0}) s; " +
                 $"bends avg {bends / (float)Count:0.0} ({committed / (float)Count:0.0} committed); {msSum / Count:0.00} ms avg, {msMax:0.0} ms max, {sw.ElapsedMilliseconds} ms wall");
        Check("every seed in the batch generates a valid primary route", passed == Count, $"{passed}/{Count}; first failure: {firstFailure}");
        Check("no seed needed the known-safe fallback", fallbacks == 0, $"fallbacks={fallbacks}");
        Check("same request gives the same stage hash", deterministic == Count, $"{deterministic}/{Count}");
        Check("different requests give different stages", hashes.Count >= Count - 1, $"{hashes.Count} distinct");
        Check("route lengths sit around the 6 km target", lenMin >= WorldScale.PrimaryRouteLength * 0.9f && lenMax <= WorldScale.PrimaryRouteLength * 1.3f,
            $"{lenMin:0}..{lenMax:0} m");
        Check("base-kit travel time brackets the 60 s target", tMin >= 40f && tMax <= 90f, $"{tMin:0.0}..{tMax:0.0} s");
        Check("skeleton generation is cheap", msSum / Count < 25.0, $"{msSum / Count:0.00} ms avg");

        // The fallback path itself must be valid: a straight axis route passes every skeleton check.
        var straight = gen.Generate(new StageGenerationRequest(RunSeed: int.MaxValue, StageIndex: 0));
        Check("generation report carries phase timings", straight.Report.Timings.Count >= 3 && straight.Report.TotalMillis >= 0.0);
    }
}
