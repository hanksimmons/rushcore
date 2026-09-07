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
    private bool _frameCheck;
    private int _frameSamples, _frameOut;
    private float _frameWorst, _frameSideWorst;
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

        // Wall-clock budget: every check counts physics ticks, so the run is launched with
        // `--fixed-fps 60` (runbook): one 1/60 s tick per frame with no real-time sync, so a headless
        // frame runs as fast as the CPU allows and the physics stays byte-identical to real time.
        // (Engine.TimeScale is not that lever: it scales the step size, not the tick count.)
        _clock = System.Diagnostics.Stopwatch.StartNew();
        _script = Run();
        GD.Print($"[SELFTEST] starting ({Engine.PhysicsTicksPerSecond} Hz ticks)");
    }

    private System.Diagnostics.Stopwatch _clock = null!;
    private string Stamp => $"@{_clock.Elapsed.TotalSeconds:0.0}s";

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

        // Framing pivot (D-090): every tick the ball is inside the lens's vertical frustum and
        // within the band (one render frame of slack, since the pivot runs per rendered frame).
        if (!rig.FlooredThisFrame && _frameCheck)
        {
            float angle = rig.FrameAngleTo(_player.GlobalPosition);
            float side = rig.FrameSideAngleTo(_player.GlobalPosition);
            float ballAngle = Mathf.RadToDeg(Mathf.Atan(_debug.Tuning.Movement.BallRadius / Mathf.Max(0.5f, rig.Camera.GlobalPosition.DistanceTo(_player.GlobalPosition))));
            float halfFov = rig.Camera.Fov * 0.5f;
            var vp = rig.Camera.GetViewport().GetVisibleRect().Size;
            float halfFovH = Mathf.RadToDeg(Mathf.Atan(Mathf.Tan(Mathf.DegToRad(halfFov)) * Mathf.Max(0.5f, vp.X / Mathf.Max(1f, vp.Y))));
            _frameSamples++;
            if (float.IsNaN(angle) || Mathf.Abs(angle) + ballAngle > halfFov || Mathf.Abs(side) + ballAngle > halfFovH) _frameOut++;
            if (!float.IsNaN(angle)) _frameWorst = Mathf.Max(_frameWorst, Mathf.Abs(angle));
            if (!float.IsNaN(side)) _frameSideWorst = Mathf.Max(_frameSideWorst, Mathf.Abs(side));
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
        if (ok) GD.Print($"[SELFTEST] PASS  {name}  {Stamp}");
        else Fail(name, detail);
    }

    private void Fail(string name, string detail)
    {
        _failures.Add(name + (string.IsNullOrEmpty(detail) ? "" : $"  ({detail})"));
        GD.Print($"[SELFTEST] FAIL  {name}  {detail}  {Stamp}");
    }

    private void CheckNear(string name, float actual, float expected, float tolerance) =>
        Check(name, Mathf.Abs(actual - expected) <= tolerance,
            $"actual={actual:0.###} expected={expected:0.###} tol={tolerance:0.###}");

    /// <summary>Every tuning value as text: generation and stage builds must leave it byte-identical (04 §7, 08 §5).</summary>
    private string TuningSnapshot()
    {
        var t = _debug.Tuning;
        return string.Join(";", t.Parameters.Select(p => p.Key + "=" + p.Get().ToString("R")))
             + "|" + string.Join(";", t.Toggles.Select(x => x.Key + "=" + x.Get()));
    }

    /// <summary>The rigid body's hidden physics: an archetype may only change geometry, never these.</summary>
    private string BodySnapshot() =>
        $"g={_player.GravityScale:R} m={_player.Mass:R} ld={_player.LinearDamp:R} ad={_player.AngularDamp:R} ccd={_player.ContinuousCd} sleep={_player.CanSleep} " +
        $"ci={_player.CustomIntegrator} friction={(_player.PhysicsMaterialOverride?.Friction ?? -1f):R} bounce={(_player.PhysicsMaterialOverride?.Bounce ?? -1f):R}";

    private static IEnumerable Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
    private static IEnumerable Seconds(float s) => Frames(Mathf.Max(1, (int)(s * Engine.PhysicsTicksPerSecond)));
    private static IEnumerable Act() => Frames(EdgeFrames);

    private static readonly string[] AllActions =
    {
        InputBootstrap.MoveForward, InputBootstrap.MoveBack, InputBootstrap.MoveLeft,
        InputBootstrap.MoveRight, InputBootstrap.Jump, InputBootstrap.Boost, InputBootstrap.Carve
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
        GD.Print($"[SELFTEST] ---- {_checks - _failures.Count}/{_checks} checks passed in {_clock.Elapsed.TotalSeconds:0.0} s wall ----");
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

        // ---- the locked preset is the compiled baseline, verbatim (D-078 procedure, D-091) ----
        {
            int applied = t.LoadOverride("res://tuning/presets/manual-small-3.json");
            Check("the locked preset manual-small-3 loads as compiled defaults (verbatim promotion)", applied > 0 && t.OverrideCount == 0,
                $"applied={applied} overrides={t.OverrideCount}: " + string.Join(", ", t.Parameters.Where(GameplayTuning.IsModified).Select(x => $"{x.Key}={x.Get():R} vs {x.DefaultValue:R}")));
            t.ResetAll();
        }

        // RUSHCORE_SELFTEST_DATA_ONLY=1: only the pure-data cases (generation batch, regression seeds,
        // the speed model's closed-form checks) in under a minute, for generation work.
        if (System.Environment.GetEnvironmentVariable("RUSHCORE_SELFTEST_DATA_ONLY") == "1")
        {
            RunStageGenerationBatchCase();
            RunRegressionSeedsCase();
            RunSpeedModelDataChecks();
            yield break;
        }

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
        CheckNear("burst multiplies the landing speed by the tuned factor (D-088)", _player.LocomotionSpeed,
            Mathf.Min(speedAtLanding * js.LandingBurstMultiplier, _player.FlowCap), 4f);
        Check("burst raised the speed", _player.LocomotionSpeed > speedAtLanding + 2f,
            $"{speedAtLanding:0.0} -> {_player.LocomotionSpeed:0.0}");
        Check("slam landing and burst granted Flow", _player.Flow >= t.Flow.GainSlamLanding + t.Flow.GainBurst - 0.01f, $"flow={_player.Flow:0.00}");
        Check("Flow raised the effective cap above the base cap", _player.FlowCap > m.HardMaxLocomotionSpeed + 5f,
            $"cap {_player.FlowCap:0.0} vs base {m.HardMaxLocomotionSpeed:0.0}");
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
        _player.LinearVelocity = Vector3.Right * 40f;
        foreach (var _ in Frames(1)) yield return null;
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
        float speedBeforeLanding = _player.LocomotionSpeed;
        while (_landedCount == landingsBefore && guard++ < 120) { speedBeforeLanding = _player.LocomotionSpeed; yield return null; }
        foreach (var _ in Act()) yield return null;
        Check("a press just before touchdown fires the burst on landing", _burstCount == burstsBefore + 1,
            $"bursts={_burstCount - burstsBefore} landed={_landedCount - landingsBefore}");
        CheckNear("buffered burst also multiplies the landing speed", _player.LocomotionSpeed,
            Mathf.Min(speedBeforeLanding * js.LandingBurstMultiplier, _player.FlowCap), 4f);
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

        // (e) the burst never slows or turns the ball: multiplier 1.0 leaves the speed alone
        {
            float savedFraction = js.LandingBurstMultiplier;
            js.LandingBurstMultiplier = 1.0f;
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
            Check("burst fired for the multiplier-1 case", _burstCount == burstsBefore + 1);
            Check("burst never slows the ball", _player.Velocity.Dot(Vector3.Right) > 36f,
                $"vel.x={_player.Velocity.X:0.0}");
            js.LandingBurstMultiplier = savedFraction;
            ReleaseAll();
            foreach (var _ in Seconds(1.0f)) yield return null;
        }

        // ---- Flow headroom (02 §8, 03 §5, D-088): earned speed above the base cap ----
        foreach (var e in RunFlowHeadroomCase()) yield return e;

        // ---- carve (03 §11, D-089): a held drift that preserves speed ----
        foreach (var e in RunCarveCase()) yield return e;

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

        // ---- CCD at the Flow ceiling (D-088): the same plate at base cap × (1 + headroom) ----
        {
            float savedCap = m.HardMaxLocomotionSpeed;
            float ceiling = savedCap * (1f + t.Flow.Headroom);
            m.HardMaxLocomotionSpeed = ceiling;        // the harness stands in for full Flow; restored exactly below
            foreach (var _ in Settle(_wallLaneStart)) yield return null;
            _player.LinearVelocity = _laneFwd * 140f;
            _player.RefillBoost(t.Boost.BoostCapacity);
            _worldDrive = _laneFwd;
            Input.ActionPress(InputBootstrap.Boost, 1f);
            maxProgress = float.MinValue; maxSpeedSeen = 0f;
            foreach (var _ in Seconds(3f))
            {
                maxProgress = Mathf.Max(maxProgress, (_player.GlobalPosition - _wallCentre).Dot(_laneFwd));
                maxSpeedSeen = Mathf.Max(maxSpeedSeen, _player.LocomotionSpeed);
                yield return null;
            }
            finalProgress = (_player.GlobalPosition - _wallCentre).Dot(_laneFwd);
            Check("reached the thin wall at the Flow ceiling", maxSpeedSeen > ceiling - 5f && maxProgress > -3f,
                $"maxSpeed={maxSpeedSeen:0.0} ceiling={ceiling:0.0} closest={maxProgress:0.00} m");
            Check("did not tunnel through a 0.3 m plate at the Flow ceiling (CCD)", finalProgress < 0f && maxProgress < 1.5f,
                $"final={finalProgress:0.00} m maxPast={maxProgress:0.00} m");
            Check("velocity finite after the ceiling impact", _player.Velocity.IsFinite());
            m.HardMaxLocomotionSpeed = savedCap;
            ReleaseAll();
            foreach (var _ in Seconds(0.5f)) yield return null;
        }

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

        // ---- framing pivot (03 §14, D-090): the ball never leaves the frame on a jump, a dive or at the cap ----
        {
            foreach (var _ in Settle(PlatformCenter - Forward * 600f + Vector3.Up * 3f)) yield return null;
            _frameCheck = true; _frameSamples = 0; _frameOut = 0; _frameWorst = 0f; _frameSideWorst = 0f;
            Input.ActionPress(InputBootstrap.Jump, 1f);                          // full charge, straight up
            foreach (var _ in Seconds(js.MaxJumpChargeSeconds + 0.05f)) yield return null;
            Input.ActionRelease(InputBootstrap.Jump);
            foreach (var _ in Seconds(1.0f)) yield return null;
            Input.ActionPress(InputBootstrap.Jump, 1f);                          // slam: the dive
            foreach (var _ in Act()) yield return null;
            Input.ActionRelease(InputBootstrap.Jump);
            int lb = _landedCount, g = 0;
            while (_landedCount == lb && g++ < 300) yield return null;
            foreach (var _ in Seconds(0.5f)) yield return null;
            int jumpSamples = _frameSamples, jumpOut = _frameOut;
            float jumpWorst = _frameWorst;
            _player.RefillBoost(t.Boost.BoostCapacity);                          // then the cap on the runway
            _worldDrive = Forward;
            Input.ActionPress(InputBootstrap.Boost, 1f);
            float lookWorst = 0f;
            foreach (var _ in Seconds(5f))
            {
                float reach = rig.CurrentDistance * Mathf.Cos(Mathf.DegToRad(t.Camera.PitchDegrees));
                lookWorst = Mathf.Max(lookWorst, rig.CurrentLookAhead - reach * 0.7f);
                yield return null;
            }
            ReleaseAll();
            // A held carve at the cap with a close lens (the user's 6 m preset): the ball slides across
            // the screen, the sideways pivot must engage, and the ball must park inside the frame.
            int runwayOut = _frameOut, runwaySamples = _frameSamples;
            float runwayWorst = _frameWorst;                                    // the band-lag check below is for the 26 m lens; at 6 m one frame is ~10°
            float savedDistance = t.Camera.Distance, savedLookMax = t.Camera.LookAheadMax;
            t.Camera.Distance = 6f; t.Camera.LookAheadMax = 5.8f;
            foreach (var _ in Settle(PlatformCenter - Forward * 600f + Vector3.Up * 3f)) yield return null;
            _player.RefillBoost(t.Boost.BoostCapacity);
            _worldDrive = Forward;
            Input.ActionPress(InputBootstrap.Boost, 1f);
            foreach (var _ in Seconds(2.0f)) yield return null;
            Input.ActionRelease(InputBootstrap.Boost);
            Vector3 carveEntry = FlatVel.Normalized();
            _worldDrive = carveEntry.Rotated(Vector3.Up, Mathf.Pi * 0.5f);     // aim a left turn and hold the carve
            Input.ActionPress(InputBootstrap.Carve, 1f);
            int carveStart = _frameSamples, carveOutStart = _frameOut;
            float sideBefore = _frameSideWorst;
            _frameSideWorst = 0f;
            float lowestPitch = 0f, worstRoll = 0f;
            foreach (var _ in Seconds(1.2f))
            {
                lowestPitch = Mathf.Min(lowestPitch, rig.FramePitchDegrees);
                Vector3 camRight = rig.Camera.GlobalBasis.X;
                worstRoll = Mathf.Max(worstRoll, Mathf.RadToDeg(Mathf.Asin(Mathf.Clamp(camRight.Y, -1f, 1f))));
                yield return null;
            }
            bool carved = _player.IsCarving;
            float carveAngle = _player.CarveAngleDegrees;
            ReleaseAll();
            foreach (var _ in Seconds(0.3f)) yield return null;
            _frameCheck = false;
            float sideWorstCarve = _frameSideWorst;
            GD.Print($"[SELFTEST] framing carve: carving={carved} angle {carveAngle:0}°, {_frameOut - carveOutStart}/{_frameSamples - carveStart} ticks out, worst side {sideWorstCarve:0.0}° (band {rig.FrameBandDegreesHorizontal:0}°), frame yaw {rig.FrameYawDegrees:0.0}°");
            Check("a held carve slides the ball sideways but never out of frame", carved && carveAngle > 10f && _frameOut - carveOutStart == 0,
                $"carving={carved} angle {carveAngle:0}° out {_frameOut - carveOutStart}");
            Check("the sideways pivot engages and parks the ball on the horizontal band", sideWorstCarve <= rig.FrameBandDegreesHorizontal + 6f && sideWorstCarve >= rig.FrameBandDegreesHorizontal - 3f,
                $"worst side {sideWorstCarve:0.0}° vs band {rig.FrameBandDegreesHorizontal:0}°");
            Check("a sideways hold never pitches the lens into the ground", lowestPitch > -12f, $"lowest frame pitch {lowestPitch:0.0}°");
            Check("a sideways hold never rolls the horizon", worstRoll < 1.5f, $"worst roll {worstRoll:0.00}°");
            t.Camera.Distance = savedDistance; t.Camera.LookAheadMax = savedLookMax;
            _frameOut = runwayOut; _frameSamples = runwaySamples; _frameSideWorst = sideBefore; _frameWorst = runwayWorst;
            GD.Print($"[SELFTEST] framing: jump+dive {jumpOut}/{jumpSamples} ticks out of frame (worst {jumpWorst:0.0}°), runway {_frameOut - jumpOut}/{_frameSamples - jumpSamples} out (worst {_frameWorst:0.0}°), band {rig.FrameBandDegreesVertical:0}°, look-ahead over reach {lookWorst:0.00} m, frame pitch {rig.FramePitchDegrees:0.0}°");
            // One tick of slack: the slam sets 90 m/s of downward speed inside a single tick (1.5 m, 4.6° at
            // the 18.8 m lens), which the pivot's one-frame lead cannot see before it happens; at 60 Hz the
            // ball's lower edge clips the frustum for that one frame and never again.
            Check("the ball stays in frame through a full jump and a slam dive (one slam-step tick of slack)", jumpOut <= 1, $"{jumpOut}/{jumpSamples} ticks out, worst {jumpWorst:0.0}°");
            Check("the ball stays in frame at the cap on the runway", _frameOut - jumpOut == 0, $"{_frameOut - jumpOut} ticks out, worst {_frameWorst:0.0}°");
            Check("the framing pivot holds the ball near the band", _frameWorst <= rig.FrameBandDegreesVertical + 6f, $"worst {_frameWorst:0.0}° vs band {rig.FrameBandDegreesVertical:0}°");
            Check("the look-ahead never exceeds the lens's horizontal reach", lookWorst <= 0.05f, $"over by {lookWorst:0.00} m");
            foreach (var _ in Seconds(0.5f)) yield return null;
        }

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
            // ---- M1 budget (D-080) + ground follow (D-092): the 800 m / 80 m hill station at 4 / 8 / 16 m
            // cells with the follow on, then 16 m with it off as the contrast. W held from 60 m/s; Space is
            // held from 100 m before the apex and released at it, so a facet hop on the convex approach
            // would cancel the charge (grace 0.10 s) and the release would not jump. ----
            const int Runs = 4;
            int[] hops = new int[Runs];
            float[] groundedFrac = new float[Runs], rawFrac = new float[Runs], apexSpeed = new float[Runs];
            bool[] chargedAtApex = new bool[Runs], jumpedAtApex = new bool[Runs];
            int[] tris = new int[Runs];
            ulong[] buildMs = new ulong[Runs];
            float[] cells = { 4f, 8f, 16f, 16f };
            bool[] follow = { true, true, true, false };
            float apexX = ScaleStripHeightField.X(3800f), holdX = ScaleStripHeightField.X(3700f);
            for (int k = 0; k < Runs; k++)
            {
                t.World.CellSize = cells[k];
                m.GroundFollow = follow[k];
                _debug.RestartSameSeed();
                foreach (var _ in Frames(3)) yield return null;
                world = _debug.World;
                tris[k] = world.Triangles;
                buildMs[k] = world.BuildMillis;
                foreach (var _ in Settle(world.SurfacePoint(ScaleStripHeightField.X(3400f), 0f, m.BallRadius + 0.4f), 0.6f)) yield return null;
                _player.LinearVelocity = Vector3.Left * 60f;
                Input.ActionPress(InputBootstrap.MoveForward, 1f);
                int ticks = 0, airborne = 0, raw = 0, blips = 0, airStart = 0, jumpsAtStart = _jumpedCount;
                bool wasGrounded = true, holding = false, released = false;
                int total = 0;
                while (_player.GlobalPosition.X > ScaleStripHeightField.X(4200f) && total++ < Engine.PhysicsTicksPerSecond * 25)
                {
                    float x = _player.GlobalPosition.X;
                    if (!released)
                    {
                        // Contact statistics cover the approach only: the jump at the apex is the charge check's flight.
                        ticks++;
                        bool g = _player.IsGrounded;
                        if (!g) airborne++;
                        if (_player.IsRawGrounded) raw++;
                        if (wasGrounded && !g) airStart = ticks;
                        if (!wasGrounded && g && ticks - airStart <= 9) blips++;   // <= 0.15 s: a facet hop, not a crest launch
                        wasGrounded = g;
                    }
                    if (!holding && x <= holdX) { holding = true; Input.ActionPress(InputBootstrap.Jump, 1f); }
                    if (holding && !released && x <= apexX)
                    {
                        released = true;
                        chargedAtApex[k] = _player.IsCharging;
                        apexSpeed[k] = _player.LocomotionSpeed;
                        Input.ActionRelease(InputBootstrap.Jump);
                    }
                    yield return null;
                }
                ReleaseAll();
                jumpedAtApex[k] = _jumpedCount > jumpsAtStart;
                hops[k] = blips;
                groundedFrac[k] = ticks > 0 ? 1f - airborne / (float)ticks : 0f;
                rawFrac[k] = ticks > 0 ? raw / (float)ticks : 0f;
            }
            m.GroundFollow = true;
            string Col(int k) => $"{cells[k]:0} m{(follow[k] ? "" : " (follow off)")}: {tris[k] / 1000} k tris, build {buildMs[k]} ms, hops {hops[k]}, grounded {groundedFrac[k]:P0}, raw contact {rawFrac[k]:P0}, apex {apexSpeed[k]:0} m/s charge {(chargedAtApex[k] ? "held" : "LOST")}";
            GD.Print($"[SELFTEST] M1 budget  {Col(0)}   |   {Col(1)}   |   {Col(2)}   |   {Col(3)}");
            Check("cell size is a live world parameter", Mathf.IsEqualApprox(_debug.World.CellSize, 16f), $"cell={_debug.World.CellSize}");
            Check("8 m cells cut the triangle count to about a quarter", tris[1] < tris[0] * 0.3f, $"{tris[0]} -> {tris[1]}");
            Check("the ground follow removes facet hops over the hill station at 4 and 8 m cells", hops[0] == 0 && hops[1] == 0, $"hops {hops[0]} / {hops[1]}");
            Check("the ground follow keeps raw contact on the hill station at 4 / 8 / 16 m cells", rawFrac[0] > 0.97f && rawFrac[1] > 0.97f && rawFrac[2] > 0.95f,
                $"raw {rawFrac[0]:P0} / {rawFrac[1]:P0} / {rawFrac[2]:P0}");
            Check("a charge held on the roll-crest approach survives to the apex and jumps there, at every cell size",
                chargedAtApex[0] && jumpedAtApex[0] && chargedAtApex[1] && jumpedAtApex[1] && chargedAtApex[2] && jumpedAtApex[2],
                $"4 m {chargedAtApex[0]}/{jumpedAtApex[0]}, 8 m {chargedAtApex[1]}/{jumpedAtApex[1]}, 16 m {chargedAtApex[2]}/{jumpedAtApex[2]}");
            Check("with the follow off, 16 m facets hop the ball and break the charge (the toggle is the baseline)",
                hops[3] > hops[2] && rawFrac[3] < rawFrac[2] - 0.1f && !chargedAtApex[3],
                $"hops {hops[2]} -> {hops[3]}, raw {rawFrac[2]:P0} -> {rawFrac[3]:P0}, charge at apex {chargedAtApex[3]}");
            Check("hill station approach completed at speed in every run", apexSpeed.All(v => v > 60f), string.Join(" / ", apexSpeed.Select(v => $"{v:0}")));

            t.World.CellSize = MovementToyWorld.DefaultCellSize;
            t.World.CalibrationStrip = false;
            _debug.RestartSameSeed();
            foreach (var _ in Frames(3)) yield return null;
            Check("lab terrain restored", !_debug.World.IsStrip && Mathf.IsEqualApprox(_debug.World.HalfX, MovementToyWorld.Extent * 0.5f)
                && Mathf.IsEqualApprox(_debug.World.CellSize, MovementToyWorld.DefaultCellSize));
        }

        // ---- Phase 2 generation, pure data: route skeletons for a seed batch (04 §12, 08 §5) ----
        RunStageGenerationBatchCase();
        RunRegressionSeedsCase();

        // ---- route speed model (D-081): the generator's speed oracle must track the real ball ----
        foreach (var e in RunRouteSpeedModelCase()) yield return e;

        // ---- Phase 2: a generated stage builds, spawns the player and is driveable along its route ----
        foreach (var e in RunGeneratedStageCase()) yield return e;

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

    /// <summary>The route speed model's closed-form checks: envelope helpers, the ceiling model, the airborne phase (D-094).</summary>
    private void RunSpeedModelDataChecks()
    {
        var t = _debug.Tuning;
        var m = t.Movement;
        var model = new RouteSpeedModel(m);
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

        // --- the ceiling model (04 §12, D-094): capped at base × (1 + headroom), steering saturated at the base cap ---
        {
            var ceil = new RouteSpeedModel(m, t.Flow.Headroom);
            float ceiling = m.HardMaxLocomotionSpeed * (1f + t.Flow.Headroom);
            CheckNear("ceiling model caps at base × (1 + headroom)", ceil.Cap, ceiling, 0.01f);
            Check("steering authority saturates at the base cap in the ceiling model",
                Mathf.IsEqualApprox(ceil.LateralAuthority(ceiling), model.LateralAuthority(m.HardMaxLocomotionSpeed)),
                $"{ceil.LateralAuthority(ceiling):0.0} vs {model.LateralAuthority(m.HardMaxLocomotionSpeed):0.0}");
            CheckNear("the ceiling bend radius holds the ceiling", ceil.CornerSpeedLimit(WorldScale.CeilingBendRadius), ceiling, 3f);
            CheckNear("below the base cap the ceiling model's corner limits are the base kit's", ceil.CornerSpeedLimit(50f), model.CornerSpeedLimit(50f), 0.01f);
            CheckNear("the cruise bend holds the base cap with margin at D-091", model.CornerSpeedLimit(WorldScale.CruiseBendRadius), m.HardMaxLocomotionSpeed, 0.01f);
            GD.Print($"[SELFTEST] bend ladder at D-091: " + string.Join(", ", new[] { 15f, 25f, 35f, 50f, 70f, 100f, 160f, 200f }.Select(r => $"r{r:0} → {ceil.CornerSpeedLimit(r):0} m/s")) +
                     $"; base cap held from r {m.HardMaxLocomotionSpeed * m.HardMaxLocomotionSpeed / model.LateralAuthority(m.HardMaxLocomotionSpeed):0} m, ceiling from r {ceiling * ceiling / ceil.LateralAuthority(ceiling):0} m");

            // Airborne phase: a 100 m/s ball off a 50 m cliff with a 45° face falls 50 m in √(2h/g) and lands on the floor.
            var cliff = new List<Vector3>();
            for (float d = 0f; d <= 400f; d += 4f) cliff.Add(new Vector3(-d, 0f, 0f));
            for (float d = 4f; d <= 50f; d += 4f) cliff.Add(new Vector3(-400f - d, -d, 0f));
            for (float d = 4f; d <= 600f; d += 4f) cliff.Add(new Vector3(-450f - d, -50f, 0f));
            var drop = model.Integrate(cliff, 100f);
            float fall = Mathf.Sqrt(2f * 50f / m.Gravity);
            var fl = drop.Flights.Count > 0 ? drop.Flights[0] : null;
            float expectHorizontal = (fl?.LaunchSpeed ?? 100f) * fall, expectVy = m.Gravity * fall;   // drive holds the ball at the cap by the lip
            float horizontal = fl is null ? 0f : fl.LandingDistance - fl.LaunchDistance - (Mathf.Sqrt(2f) - 1f) * 50f;   // route distance minus the face's extra arclength
            Check("the model flies a ball off a cliff and lands it where ballistics say (±10%)",
                fl is not null && drop.Flights.Count == 1 && Mathf.Abs(horizontal - expectHorizontal) <= expectHorizontal * 0.10f
                && Mathf.Abs(fl.LandingVerticalSpeed - expectVy) <= expectVy * 0.10f && fl.LaunchDistance >= 385f && fl.LaunchDistance <= 410f,
                fl is null ? "no flight" : $"launch {fl.LaunchDistance:0} m, horizontal {horizontal:0} m (expect {expectHorizontal:0}), {fl.Seconds:0.00} s (expect {fall:0.00}), lands {fl.LandingVerticalSpeed:0} m/s down (expect {expectVy:0}), keeps {fl.LandingSpeed:0} m/s");
            // A cosine roller: a roll at 60 m/s, a launch before the apex at the cap.
            var hill = new List<Vector3>();
            for (float d = 0f; d <= 1400f; d += 4f)
            {
                float h = d >= 300f && d <= 1100f ? 40f * (1f - Mathf.Cos(Mathf.Tau * (d - 300f) / 800f)) : 0f;
                hill.Add(new Vector3(-d, h, 0f));
            }
            var slow = model.Integrate(hill, 60f, driveHeld: false);
            var fast = model.Integrate(hill, m.HardMaxLocomotionSpeed);
            Check("the model rolls the 800/80 hill at 60 m/s and launches it before the apex at the cap",
                slow.Flights.Count == 0 && fast.Flights.Count == 1 && fast.Flights[0].LaunchDistance < 700f && fast.Flights[0].LaunchDistance > 500f && fast.Flights[0].LandingDistance > 700f,
                $"slow flights {slow.Flights.Count}; fast: " + (fast.Flights.Count > 0 ? $"launch {fast.Flights[0].LaunchDistance:0} → land {fast.Flights[0].LandingDistance:0} m, {fast.Flights[0].LandingVerticalSpeed:0} m/s down" : "none"));
            Check("seconds below the base cap counts the standing start", model.Integrate(RouteSpeedModel.StraightPolyline(Vector3.Zero, Vector3.Left, 2000f, 4f)).SecondsBelow(m.HardMaxLocomotionSpeed * 0.98f) is > 5f and < 9f);
        }

    }

    private IEnumerable RunRouteSpeedModelCase()
    {
        var t = _debug.Tuning;
        var m = t.Movement;
        var model = new RouteSpeedModel(m);
        const float Tolerance = 0.05f;

        RunSpeedModelDataChecks();

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

            // --- the ceiling on the strip (08 §4 addendum, D-094): the harness stands in for full Flow by
            // raising the hard cap to base × (1 + headroom), restored exactly below. The comparison model is
            // built on a fresh MovementTuning so its base cap (steering saturation) stays the frozen one. ---
            {
                float savedCap = m.HardMaxLocomotionSpeed;
                float ceiling = savedCap * (1f + t.Flow.Headroom);
                var ceilModel = new RouteSpeedModel(new MovementTuning(), t.Flow.Headroom);
                m.HardMaxLocomotionSpeed = ceiling;

                // Turn radius: the heading rate at full lateral input is a_lat / v, so r = v / ω.
                foreach (var _ in Settle(world.SurfacePoint(ScaleStripHeightField.X(5750f), -120f, m.BallRadius + 0.4f), 0.6f)) yield return null;
                _player.LinearVelocity = Vector3.Left * ceiling;
                foreach (var _ in Frames(2)) yield return null;
                _worldDrive = Vector3.Back;                       // +Z: a quarter turn to the left of −X travel
                Vector3 h0 = FlatVel.Normalized();
                float vTurn = _player.LocomotionSpeed, arcLen = 0f;
                Vector3 prevP = _player.GlobalPosition;
                foreach (var _ in Seconds(0.4f)) { yield return null; arcLen += new Vector2(_player.GlobalPosition.X - prevP.X, _player.GlobalPosition.Z - prevP.Z).Length(); prevP = _player.GlobalPosition; }
                float turned = h0.AngleTo(FlatVel.Normalized());
                float measuredRadius = turned > 1e-3f ? arcLen / turned : float.PositiveInfinity;
                float expectRadius = ceiling * ceiling / ceilModel.LateralAuthority(ceiling);
                ReleaseAll();
                GD.Print($"[SELFTEST] ceiling turn: {vTurn:0} m/s, {Mathf.RadToDeg(turned):0.0}° over {arcLen:0} m → r {measuredRadius:0} m (model {expectRadius:0} m)");
                Check("turn radius at the ceiling matches the saturated steering envelope (±15%)",
                    vTurn > ceiling - 8f && Mathf.Abs(measuredRadius - expectRadius) <= expectRadius * 0.15f,
                    $"v={vTurn:0} r={measuredRadius:0} expect {expectRadius:0}");

                // Hills at the ceiling: every station launches; flights and landings against the model over the same centreline.
                var strip = new List<Vector3>();
                for (float sd = 1900f; sd <= 4300f; sd += 4f) strip.Add(new Vector3(ScaleStripHeightField.X(sd), world.SampleHeight(ScaleStripHeightField.X(sd), 0f), 0f));
                var stripProfile = ceilModel.Integrate(strip, ceiling);
                foreach (var _ in Settle(world.SurfacePoint(ScaleStripHeightField.X(1900f), 0f, m.BallRadius + 0.4f), 0.6f)) yield return null;
                _player.LinearVelocity = Vector3.Left * ceiling;
                _worldDrive = Vector3.Left;
                var flights = new List<(float launch, float land, float vy, float after)>();
                bool air = false; float launchS = 0f, minVy = 0f; int guard = 0;
                while (ScaleStripHeightField.S(_player.GlobalPosition.X) < 4300f && guard++ < Engine.PhysicsTicksPerSecond * 20)
                {
                    float sNow = ScaleStripHeightField.S(_player.GlobalPosition.X);
                    bool g = _player.IsGrounded;
                    if (!air && !g) { air = true; launchS = sNow; minVy = 0f; }
                    if (air) minVy = Mathf.Min(minVy, _player.Velocity.Y);
                    if (air && g) { air = false; flights.Add((launchS, sNow, -minVy, _player.LocomotionSpeed)); }
                    yield return null;
                }
                ReleaseAll();
                string real = string.Join("", flights.Select(f => $" [{f.launch:0}→{f.land:0} m, {f.vy:0} m/s down → {f.after:0}]"));
                string pred = string.Join("", stripProfile.Flights.Select(f => $" [{f.LaunchDistance + 1900f:0}→{f.LandingDistance + 1900f:0} m, {f.LandingVerticalSpeed:0} m/s down → {f.LandingSpeed:0}]"));
                GD.Print($"[SELFTEST] ceiling hills at {ceiling:0} m/s: real{real}; model{pred}");
                // Compare the flights that ended inside the window in both.
                var realDone = flights.Where(f => f.land < 4250f).ToList();
                var predDone = stripProfile.Flights.Where(f => f.LandingDistance + 1900f < 4250f).ToList();
                bool countOk = realDone.Count == predDone.Count && realDone.Count >= 1;
                // Each flight is judged on its own length and landing. Measured 2026-09-06 (D-094): the ball keeps
                // 7–9% less speed through a landing than the tangent rule and leaves the next facet a little later
                // and lower, so chained flights at the ceiling run up to 27% long in the model (the safe direction
                // for the validators); landing vertical speeds within 15%; the stage's launch crest matches within 3%.
                float worstLen = 0f, worstVy = 0f;
                for (int i = 0; countOk && i < realDone.Count; i++)
                {
                    float len = Mathf.Max(50f, realDone[i].land - realDone[i].launch);
                    worstLen = Mathf.Max(worstLen, Mathf.Abs(predDone[i].Length - len) / len);
                    worstVy = Mathf.Max(worstVy, Mathf.Abs(predDone[i].LandingVerticalSpeed - realDone[i].vy) / Mathf.Max(10f, realDone[i].vy));
                }
                Check("the model's airborne phase flies the ceiling hills as the ball does (each flight's length within 35%, landing vertical speed within 20%)",
                    countOk && worstLen <= 0.35f && worstVy <= 0.20f,
                    $"real {realDone.Count} / model {predDone.Count} flights; worst length error {worstLen:P0}, worst vertical {worstVy:P0}; real{real}; model{pred}");

                m.HardMaxLocomotionSpeed = savedCap;
                foreach (var _ in Settle(world.SurfacePoint(ScaleStripHeightField.StartX, 0f, m.BallRadius + 0.4f), 0.6f)) yield return null;
            }

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

    // ---------------- carve (03 §11; 08 §3; D-089) ----------------

    private IEnumerable RunCarveCase()
    {
        var t = _debug.Tuning;
        var cv = t.Carve;

        // (a) below the minimum speed the button does nothing
        foreach (var _ in Settle(PlatformCenter - Forward * 400f + Vector3.Up * 3f)) yield return null;
        Input.ActionPress(InputBootstrap.Carve, 1f);
        foreach (var _ in Frames(5)) yield return null;
        Check("carve does nothing below the minimum speed", !_player.IsCarving, $"speed={_player.LocomotionSpeed:0.0}");
        Input.ActionRelease(InputBootstrap.Carve);

        // (b) a hot entry: boosted run, then hold the carve and aim a quarter turn to the right
        _player.RefillBoost(t.Boost.BoostCapacity);
        _worldDrive = Forward;
        Input.ActionPress(InputBootstrap.Boost, 1f);
        foreach (var _ in Seconds(2.0f)) yield return null;
        Input.ActionRelease(InputBootstrap.Boost);
        Vector3 entryDir = FlatVel.Normalized();
        float entrySpeed = _player.LocomotionSpeed;
        Vector3 turnDir = entryDir.Rotated(Vector3.Up, -Mathf.Pi * 0.5f);     // world-fixed: right of the entry heading
        _worldDrive = turnDir;
        Input.ActionPress(InputBootstrap.Carve, 1f);
        foreach (var _ in Frames(2)) yield return null;
        Check("carve starts when held above the minimum speed while grounded", _player.IsCarving && entrySpeed > cv.MinSpeed,
            $"carving={_player.IsCarving} speed={entrySpeed:0.0}");
        int carvesBefore = _player.CarveCount;
        float flowBefore = _player.Flow, minSpeedDuring = float.MaxValue, maxOff = 0f;
        foreach (var _ in Seconds(90f / Mathf.Max(30f, cv.YawRateDegrees) + 0.05f))
        {
            minSpeedDuring = Mathf.Min(minSpeedDuring, _player.LocomotionSpeed);
            maxOff = Mathf.Max(maxOff, _player.CarveAngleDegrees);
            yield return null;
        }
        Vector3 facing = new Vector3(_player.Facing.X, 0f, _player.Facing.Z).Normalized();
        float facingTurn = Mathf.RadToDeg(entryDir.AngleTo(facing));
        float travelTurn = Mathf.RadToDeg(entryDir.AngleTo(FlatVel.Normalized()));
        float camToFacing = Forward.Dot(facing);
        GD.Print($"[SELFTEST] carve: entry {entrySpeed:0.0} m/s, facing swung {facingTurn:0}°, travel turned {travelTurn:0}° (max off {maxOff:0}°), camera·facing {camToFacing:0.00}, min speed during {minSpeedDuring:0.0}");
        Check("the facing swings toward the input at the yaw rate", facingTurn > 60f, $"{facingTurn:0}°");
        // At the D-095 yaw rate (84°/s) the hold lasts over a second, so the understeering velocity catches up
        // further than at 190°/s (measured travel 65° against facing 91°); the ratio still shows the lag.
        Check("the velocity understeers: it turns less than the facing", travelTurn < facingTurn * 0.85f, $"travel {travelTurn:0}° vs facing {facingTurn:0}°");
        Check("the camera tracks behind the facing during the carve", camToFacing > 0.6f, $"dot={camToFacing:0.00}");
        Check("the ball stays grounded through the carve", _player.IsGrounded && _player.IsCarving);
        var vfx = _player.GetNodeOrNull<PlayerVfx>("PlayerVfx");
        Check("carve debris is thrown while carving", vfx is not null && vfx.CarveDebrisActive, $"vfx={(vfx is null ? "missing" : vfx.CarveDebrisActive.ToString())}");
        Input.ActionRelease(InputBootstrap.Carve);
        foreach (var _ in Frames(2)) yield return null;
        float exitOff = Mathf.RadToDeg(FlatVel.Normalized().AngleTo(facing));
        Check("release snaps the velocity onto the facing", !_player.IsCarving && exitOff < 8f, $"off={exitOff:0.0}°");
        Check("no speed is lost through the carve", _player.LocomotionSpeed >= entrySpeed - 1f, $"entry {entrySpeed:0.0} -> exit {_player.LocomotionSpeed:0.0}");
        Check("a real carve counts and grants Flow", _player.CarveCount == carvesBefore + 1 && _player.Flow >= flowBefore + cv.FlowGain - 0.01f,
            $"count {carvesBefore} -> {_player.CarveCount}, flow {flowBefore:0.00} -> {_player.Flow:0.00}");
        Check("velocity finite after the carve", _player.Velocity.IsFinite());
        ReleaseAll();
        foreach (var _ in Seconds(0.5f)) yield return null;
    }

    // ---------------- Flow headroom (02 §8, 03 §5; 08 §3; D-088) ----------------

    private IEnumerable RunFlowHeadroomCase()
    {
        var t = _debug.Tuning;
        var m = t.Movement;
        var js = t.JumpSlam;
        var fl = t.Flow;
        float baseCap = m.HardMaxLocomotionSpeed;

        foreach (var _ in Settle(PlatformCenter - Forward * 700f + Vector3.Up * 3f)) yield return null;
        Check("Flow is zero after a recovery and the cap is the base cap", _player.Flow == 0f && Mathf.IsEqualApprox(_player.FlowCap, baseCap),
            $"flow={_player.Flow:0.00} cap={_player.FlowCap:0.0}");

        // A chain: boosted run, charged jump, slam, burst at touchdown.
        _player.RefillBoost(t.Boost.BoostCapacity);
        _worldDrive = Forward;
        Input.ActionPress(InputBootstrap.Boost, 1f);
        foreach (var _ in Seconds(1.2f)) yield return null;
        Input.ActionRelease(InputBootstrap.Boost);
        Input.ActionPress(InputBootstrap.Jump, 1f);
        foreach (var _ in Seconds(js.MaxJumpChargeSeconds + 0.05f)) yield return null;
        Input.ActionRelease(InputBootstrap.Jump);
        foreach (var _ in Frames(12)) yield return null;
        float flowAfterJump = _player.Flow;
        Check("a charged jump grants Flow at takeoff", flowAfterJump >= fl.GainChargedJump - 0.01f, $"flow={flowAfterJump:0.00}");
        Input.ActionPress(InputBootstrap.Jump, 1f);                  // slam
        foreach (var _ in Act()) yield return null;
        Input.ActionRelease(InputBootstrap.Jump);
        int landingsBefore = _landedCount, burstsBefore = _burstCount, guard = 0;
        while (_landedCount == landingsBefore && guard++ < 300) yield return null;
        foreach (var _ in Frames(1)) yield return null;
        Input.ActionPress(InputBootstrap.Jump, 1f);                  // burst at touchdown
        foreach (var _ in Act()) yield return null;
        Input.ActionRelease(InputBootstrap.Jump);
        foreach (var _ in Act()) yield return null;
        float flowChain = _player.Flow;
        Check("the chain jump → slam → burst stacks its Flow gains", _burstCount == burstsBefore + 1
            && flowChain >= fl.GainChargedJump + fl.GainSlamLanding + fl.GainBurst - 0.01f, $"bursts={_burstCount - burstsBefore} flow={flowChain:0.00}");
        float flowCap = _player.FlowCap;
        Check("Flow headroom raises the effective cap", flowCap > baseCap + 5f, $"cap {flowCap:0.0} base {baseCap:0.0} headroom {fl.Headroom:0.00}");

        // Drive and boost on: the ball rises above the base cap and never above the effective cap.
        Input.ActionPress(InputBootstrap.Boost, 1f);
        float maxSpeed = 0f, capViolations = 0f;
        foreach (var _ in Seconds(3.0f))
        {
            maxSpeed = Mathf.Max(maxSpeed, _player.LocomotionSpeed);
            if (_player.LocomotionSpeed > _player.EffectiveLocomotionCap + 0.5f) capViolations++;
            yield return null;
        }
        Input.ActionRelease(InputBootstrap.Boost);
        GD.Print($"[SELFTEST] flow headroom: flow {_player.Flow:0.00}, cap {_player.FlowCap:0.0} (base {baseCap:0.0}), max speed {maxSpeed:0.0} m/s, chain {_player.SinceFlowGain:0.0} s, impacts {_player.ImpactCount}");
        Check("with Flow the ball travels above the base cap", maxSpeed > baseCap + 10f, $"max {maxSpeed:0.0} vs base {baseCap:0.0}");
        Check("the effective cap is never exceeded", capViolations == 0f, $"{capViolations} ticks over");
        Check("no time decay while the chain is alive", _player.Flow >= flowChain - 0.001f && _player.SinceFlowGain < fl.ChainWindowSeconds,
            $"flow {flowChain:0.00} -> {_player.Flow:0.00} after {_player.SinceFlowGain:0.0} s");
        Check("a clean drive registers no impact", _player.ImpactCount == 0, $"impacts={_player.ImpactCount}");

        // Brake is the voluntary mistake: it drains Flow and the cap falls with it.
        ReleaseAll();                                   // drop the held forward key so S is a pure brake
        Input.ActionPress(InputBootstrap.MoveBack, 1f);
        foreach (var _ in Seconds(0.6f)) yield return null;
        Input.ActionRelease(InputBootstrap.MoveBack);
        Check("braking drains Flow", _player.Flow <= flowChain - fl.LossBrakePerSecond * 0.5f + 0.01f, $"{flowChain:0.00} -> {_player.Flow:0.00}");
        Check("the effective cap falls with Flow", _player.FlowCap < flowCap - 1f, $"{flowCap:0.0} -> {_player.FlowCap:0.0}");

        // Headroom 0 is the frozen baseline: the cap is the base cap whatever Flow says.
        float savedHeadroom = fl.Headroom;
        fl.Headroom = 0f;
        foreach (var _ in Frames(2)) yield return null;
        Check("with zero headroom the cap is the frozen base cap", Mathf.IsEqualApprox(_player.FlowCap, baseCap) && _player.LocomotionSpeed <= baseCap + 0.5f,
            $"cap {_player.FlowCap:0.0} speed {_player.LocomotionSpeed:0.0}");
        fl.Headroom = savedHeadroom;

        _player.RequestRecovery();
        foreach (var _ in Frames(30)) yield return null;
        Check("fall recovery ends the chain: Flow is zero", _player.Flow == 0f, $"flow={_player.Flow:0.00}");
        ReleaseAll();
        foreach (var _ in Seconds(0.5f)) yield return null;
    }

    // ---------------- Phase 2: stage generation batch (04 §4, §5A, §12; 08 §5) ----------------

    private void RunStageGenerationBatchCase()
    {
        // Gate G0 per archetype (08 §5): the same batch for every archetype Phase 3 adds.
        foreach (var archetype in new[] { TerrainArchetype.RollingHighlands, TerrainArchetype.CanyonRun, TerrainArchetype.DuneSea })
            RunStageGenerationBatchCase(archetype);
    }

    private void RunStageGenerationBatchCase(TerrainArchetype archetype)
    {
        var gen = new StageGenerator(_debug.Tuning.Movement, _debug.Tuning.Flow, _debug.Tuning.JumpSlam);
        const int Count = 100;
        string A = ArchetypeRules.Label(archetype);
        var hashes = new HashSet<ulong>();
        var fallbackReasons = new Dictionary<string, int>();
        int passed = 0, fallbacks = 0, deterministic = 0, bends = 0, committed = 0, withLines = 0, linesTotal = 0, minAnchors = int.MaxValue;
        float lenMin = float.MaxValue, lenMax = 0f, lenSum = 0f, tMin = float.MaxValue, tMax = 0f, tSum = 0f;
        float belowSum = 0f, ceilAir = 0f, widestGap = 0f; int ceilFlights = 0;
        int gaps = 0, ramps = 0, turns = 0, modulesPassed = 0, modulesTotal = 0, seedsWithGap = 0, seedsWithRamp = 0;
        int crests = 0, trains = 0, seedsWithTrain = 0, droppedShown = 0, droppedLines = 0, tubes = 0, seedsWithTube = 0, tubesPassed = 0, lids = 0, lidsPassed = 0, spirals = 0;
        float rideMax = 0f; string exampleTube = "";
        string exampleGap = "", exampleRamp = "", exampleRegen = "", exampleTrain = "";
        double msSum = 0, msMax = 0;
        string firstFailure = "";
        string tuningBefore = TuningSnapshot();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < Count; i++)
        {
            var req = new StageGenerationRequest(RunSeed: 1 + i / 9, StageIndex: i % 9, archetype);
            var def = gen.Generate(req);
            var again = gen.Generate(req);
            if (def.Report.Passed) passed++; else if (firstFailure == "") firstFailure = $"seed {req.RunSeed}/{req.StageIndex}: " + string.Join("; ", def.Report.Failures.Select(f => f.Name + " " + f.Detail));
            if (def.Report.UsedFallback)
            {
                fallbacks++;
                foreach (var c in def.Report.Checks.Where(c => c.Name.StartsWith("regeneration")))
                {
                    fallbackReasons[c.Name] = fallbackReasons.GetValueOrDefault(c.Name) + 1;
                    if (fallbacks <= 2) GD.Print($"[SELFTEST] fallback seed {req.RunSeed}/{req.StageIndex}: {c.Name} {c.Detail}");
                }
            }
            if (i == 0) GD.Print($"[SELFTEST] generation timings: " + string.Join(", ", def.Report.Timings.Select(t => $"{t.phase} {t.ms:0.00} ms")));
            if (def.DroppedLines > 0 && droppedShown++ < 2) GD.Print($"[SELFTEST] {A} seed {req.RunSeed}/{req.StageIndex} dropped optional lines: {def.DroppedDetail}");
            if (def.Hash() == again.Hash()) deterministic++;
            hashes.Add(def.Hash());
            bends += def.PrimaryRoute.Bends.Count;
            committed += def.PrimaryRoute.Bends.Count(b => b.Radius <= WorldScale.CommittedBendRadius + 1e-3f);
            float len = def.PrimaryRoute.Length, t = def.SpeedProfile.TotalTime;
            lenMin = Mathf.Min(lenMin, len); lenMax = Mathf.Max(lenMax, len); lenSum += len;
            tMin = Mathf.Min(tMin, t); tMax = Mathf.Max(tMax, t); tSum += t;
            msSum += def.Report.TotalMillis; msMax = Math.Max(msMax, def.Report.TotalMillis);
            if (def.OptionalLines.Count > 0) withLines++;
            linesTotal += def.OptionalLines.Count;
            droppedLines += def.DroppedLines;
            tubes += def.Tubes.Count;
            tubesPassed += def.Tubes.Count(x => x.Passed);
            foreach (var x in def.Tubes) rideMax = Mathf.Max(rideMax, x.MaxRideDegrees);
            lids += def.Lids.Count; lidsPassed += def.Lids.Count(x => x.Passed);
            if (def.PrimaryRoute.Spiral is not null) spirals++;
            if (def.Tubes.Count > 0) { seedsWithTube++; if (exampleTube == "" || (req.StageIndex == 0 && !exampleTube.EndsWith("/0"))) exampleTube = $"{req.RunSeed}/{req.StageIndex}"; }
            minAnchors = Mathf.Min(minAnchors, def.Checkpoints.Count);
            belowSum += def.SpeedProfile.SecondsBelow(_debug.Tuning.Movement.HardMaxLocomotionSpeed * 0.98f);
            ceilFlights += def.CeilingProfile?.Flights.Count ?? 0;
            ceilAir += def.CeilingProfile?.AirborneSeconds ?? 0f;
            widestGap = Mathf.Max(widestGap, def.WidestFlowGap);
            foreach (var mod in def.Modules)
            {
                modulesTotal++;
                if (mod.Passed) modulesPassed++;
                if (mod.Kind == ChallengeModuleKind.ModerateGap) gaps++; else if (mod.Kind == ChallengeModuleKind.LaunchRamp) ramps++; else turns++;
            }
            if (def.Modules.Any(x => x.Kind == ChallengeModuleKind.ModerateGap)) { seedsWithGap++; if (exampleGap == "") exampleGap = $"{req.RunSeed}/{req.StageIndex}"; }
            if (def.Modules.Any(x => x.Kind == ChallengeModuleKind.LaunchRamp)) { seedsWithRamp++; if (exampleRamp == "") exampleRamp = $"{req.RunSeed}/{req.StageIndex}"; }
            var crestStraights = def.PrimaryRoute.Features.Where(f => f.Kind == RouteFeatureKind.LaunchCrest).GroupBy(f => f.StartIndex).Select(g => g.Count()).ToList();
            crests += crestStraights.Sum();
            trains += crestStraights.Count(n => n >= 2);
            if (crestStraights.Any(n => n >= 2)) { seedsWithTrain++; if (exampleTrain == "") exampleTrain = $"{req.RunSeed}/{req.StageIndex}"; }
            if (def.Report.Attempts > 1 && exampleRegen == "") exampleRegen = $"{req.RunSeed}/{req.StageIndex} (attempt {def.Report.Attempts})";
            // RUSHCORE_BATCH_FAILS=1: tally what the first attempt failed on across the batch (a generation-tuning instrument).
            if (def.Report.Attempts > 1 && System.Environment.GetEnvironmentVariable("RUSHCORE_BATCH_FAILS") == "1")
            {
                var first = gen.BuildAttempt(req, 0);
                foreach (var f in first.Report.Failures)
                {
                    if (!fallbackReasons.ContainsKey("attempt 1: " + f.Name))
                        GD.Print($"[SELFTEST] {A} seed {req.RunSeed}/{req.StageIndex} attempt 1: {f.Name} {f.Detail}; features: " +
                                 string.Join(", ", first.PrimaryRoute.Features.Select(x => $"{x.Kind} {x.CentreDistance:0}→{x.FeatureEnd:0} m on {first.PrimaryRoute.Vertices[x.StartIndex].Distance:0}–{first.PrimaryRoute.Vertices[x.EndIndex].Distance:0}")) +
                                 "; bends: " + string.Join(", ", first.PrimaryRoute.Bends.Select(b => $"r{b.Radius:0} at {first.PrimaryRoute.Vertices[b.StartIndex].Distance:0}")));
                    fallbackReasons["attempt 1: " + f.Name] = fallbackReasons.GetValueOrDefault("attempt 1: " + f.Name) + 1;
                }
            }
        }
        sw.Stop();
        GD.Print($"[SELFTEST] {A} modules over the batch: {gaps} gaps ({seedsWithGap} seeds, e.g. {exampleGap}), {ramps} ramps ({seedsWithRamp} seeds, e.g. {exampleRamp}), {turns} banked turns; {modulesPassed}/{modulesTotal} pass; {crests} crests, {trains} trains of ≥ 2 ({seedsWithTrain} seeds, e.g. {exampleTrain}); regeneration e.g. {exampleRegen}");
        GD.Print($"[SELFTEST] two speeds over the batch: seconds below the base cap avg {belowSum / Count:0.0} s; ceiling flights avg {ceilFlights / (float)Count:0.0} ({ceilAir / Count:0.0} s airborne avg); widest Flow-opportunity gap {widestGap:0} m");
        GD.Print($"[SELFTEST] {A} structures over the batch: {tubes} tubes on {seedsWithTube} seeds (e.g. {exampleTube}), {tubesPassed} pass, wall ride ≤ {rideMax:0}°; {lids} lids ({lidsPassed} pass); {spirals} spiral pits");
        if (fallbackReasons.Count > 0) GD.Print($"[SELFTEST] {A} attempt failures: " + string.Join(", ", fallbackReasons.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} ×{kv.Value}")));
        GD.Print($"[SELFTEST] {A} generation batch  {Count} stages: {passed} valid, {fallbacks} fallbacks, {hashes.Count} distinct; " +
                 $"length {lenMin:0}..{lenMax:0} (avg {lenSum / Count:0}) m; base-kit time {tMin:0.0}..{tMax:0.0} (avg {tSum / Count:0.0}) s; " +
                 $"bends avg {bends / (float)Count:0.0} ({committed / (float)Count:0.0} committed); lines avg {linesTotal / (float)Count:0.0} ({withLines} seeds, {droppedLines} dropped); anchors ≥ {minAnchors}; {msSum / Count:0.00} ms avg, {msMax:0.0} ms max, {sw.ElapsedMilliseconds} ms wall");
        Check($"{A}: " + "every seed in the batch generates a valid primary route", passed == Count, $"{passed}/{Count}; first failure: {firstFailure}");
        Check($"{A}: " + "no seed needed the known-safe fallback", fallbacks == 0, $"fallbacks={fallbacks}: " + string.Join(", ", fallbackReasons.Select(kv => $"{kv.Key} ×{kv.Value}")));
        Check($"{A}: " + "same request gives the same stage hash", deterministic == Count, $"{deterministic}/{Count}");
        Check($"{A}: " + "different requests give different stages", hashes.Count >= Count - 1, $"{hashes.Count} distinct");
        // D-086 measured 95% with ridge lines alone; module straights (≈ 1 km each, D-097) leave fewer 1.3 km sections.
        // A dune sea is mostly train straights, which ridge lines avoid (D-099): its optional line is undesigned, so
        // the figure is reported and not judged until a dune lane exists.
        // A ridge needs a transition's worth of straight before and after the bend it shadows (D-100); the canyon's
        // 200–450 m straights give one to about 70% of its seeds ("occasional upper ledge", 04 §6), the Highlands' to 90%.
        float linesFloor = archetype == TerrainArchetype.CanyonRun ? 0.6f : 0.8f;
        if (archetype == TerrainArchetype.DuneSea) GD.Print($"[SELFTEST] {A}: optional lines not judged: {withLines}/{Count} seeds carry a ridge line (a dune lane is the open design)");
        else Check($"{A}: " + $"most seeds carry at least one optional line (≥ {linesFloor:P0})", withLines >= Count * linesFloor, $"{withLines}/{Count} seeds, {linesTotal / (float)Count:0.0} lines avg, {droppedLines} dropped");
        Check($"{A}: " + "every seed places progression anchors", minAnchors >= 8, $"min {minAnchors} anchors");
        Check($"{A}: " + "every challenge module in the batch passes its validator (two prices, 04 §5E)", modulesPassed == modulesTotal, $"{modulesPassed}/{modulesTotal}");
        Check($"{A}: " + "the batch exercises gaps, ramps and banked turns", gaps > 0 && ramps > 0 && turns > 0, $"{gaps} gaps, {ramps} ramps, {turns} turns");
        // Dune Sea (D-099): the archetype's identity is the crest-to-crest rhythm, so most seeds carry a train.
        if (archetype == TerrainArchetype.DuneSea)
            Check($"{A}: " + "most seeds carry a dune train of at least two crests", seedsWithTrain >= Count * 0.6f, $"{seedsWithTrain}/{Count} seeds, {trains} trains, {crests} crests");
        Check($"{A}: " + "the batch places see-through tubes and every tube passes its validators (clearance, mouths, carried profile)", tubes > 0 && tubesPassed == tubes, $"{tubes} tubes, {tubesPassed} pass, {seedsWithTube} seeds");
        if (archetype == TerrainArchetype.CanyonRun)
            Check($"{A}: " + "the batch places wall tunnels and spiral pits and every lid keeps its clearance (D-102)", lids > 0 && lidsPassed == lids && spirals > 0, $"{lids} lids ({lidsPassed} pass), {spirals} spiral pits");
        Check($"{A}: " + "route lengths sit around the 6 km target", lenMin >= WorldScale.PrimaryRouteLength * 0.9f && lenMax <= WorldScale.PrimaryRouteLength * 1.3f,
            $"{lenMin:0}..{lenMax:0} m");
        Check($"{A}: " + "base-kit travel time brackets the 60 s target", tMin >= 40f && tMax <= 90f, $"{tMin:0.0}..{tMax:0.0} s");
        Check($"{A}: " + "stage definition generation is cheap (pure data, before world sampling)", msSum / Count < 80.0, $"{msSum / Count:0.00} ms avg");
        Check($"{A}: " + "generation never writes to tuning (04 §7)", TuningSnapshot() == tuningBefore);

        // The fallback path itself must be valid: a straight axis route passes every skeleton check.
        var straight = gen.Generate(new StageGenerationRequest(RunSeed: int.MaxValue, StageIndex: 0, archetype));
        Check($"{A}: generation report carries phase timings", straight.Report.Timings.Count >= 3 && straight.Report.TotalMillis >= 0.0);
    }

    // ---------------- regression seeds (08 §11): fixed failures stay fixed ----------------

    private void RunRegressionSeedsCase()
    {
        var gen = new StageGenerator(_debug.Tuning.Movement, _debug.Tuning.Flow, _debug.Tuning.JumpSlam);
        foreach (var e in RegressionSeeds.All)
        {
            var req = new StageGenerationRequest(e.RunSeed, e.StageIndex, e.Archetype);
            var def = gen.Generate(req);
            string firstAttempt = "";
            if (def.Report.Attempts > 1)
            {
                var a = gen.BuildAttempt(req, 0);
                firstAttempt = "; attempt 1 failed: " + string.Join("; ", a.Report.Failures.Select(f => f.Name + " " + f.Detail));
            }
            GD.Print($"[SELFTEST] regression seed {e.RunSeed}/{e.StageIndex} ({e.Why}): attempts {def.Report.Attempts}, {def.PrimaryRoute.Length:0} m, " +
                     $"base-kit {def.SpeedProfile.TotalTime:0.0} s, {def.PrimaryRoute.Features.Count} features ({string.Join("/", def.Modules.Select(x => x.Kind.ToString()))}), {def.OptionalLines.Count} lines, {def.Checkpoints.Count} anchors, hash {def.Hash():X}{firstAttempt}");
            Check($"regression seed {e.RunSeed}/{e.StageIndex} generates a valid stage without fallback", def.Report.Passed && !def.Report.UsedFallback,
                string.Join("; ", def.Report.Failures.Select(f => f.Name + " " + f.Detail)));
        }
    }

    // ---------------- Phase 2: generated stage in the toy (04 §9, §16; 08 §5) ----------------

    private IEnumerable RunGeneratedStageCase()
    {
        var t = _debug.Tuning;
        var m = t.Movement;
        string bodyLab = BodySnapshot();
        int nodesLab = GetTree().GetNodeCount();
        t.World.GeneratedStage = true;
        // RUSHCORE_CELL_SIZE=8 (or 16) builds and drives the stage at that cell size: the D-092 cell
        // re-measurement knob. Everything else in the harness stays at the default 4 m.
        float stageCell = MovementToyWorld.DefaultCellSize;
        if (float.TryParse(System.Environment.GetEnvironmentVariable("RUSHCORE_CELL_SIZE"), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float envCell) && envCell >= 1f && envCell <= 16f)
            stageCell = envCell;
        t.World.CellSize = stageCell;
        // RUSHCORE_ARCHETYPE=canyon|dunes drives a Canyon Run or Dune Sea stage (G0 per archetype, 08 §5).
        string envArchetype = System.Environment.GetEnvironmentVariable("RUSHCORE_ARCHETYPE") ?? "";
        t.World.Archetype = (int)(envArchetype == "canyon" ? TerrainArchetype.CanyonRun : envArchetype == "dunes" ? TerrainArchetype.DuneSea : TerrainArchetype.RollingHighlands);
        string tuningStage = TuningSnapshot();
        _debug.RestartSameSeed();
        foreach (var _ in Frames(3)) yield return null;
        var world = _debug.World;
        var stage = world.Stage;
        Check("generated stage is the active terrain", world.IsStage && stage is not null && world.HalfX > 2900f, $"halfX={world.HalfX}");
        if (stage is null) { t.World.GeneratedStage = false; _debug.RestartSameSeed(); yield break; }
        GD.Print($"[SELFTEST] generated stage: cells {world.CellSize:0} m, build {world.BuildMillis} ms, {world.SampleCount / 1000} k samples ({world.SampleCount * 4 / 1e6f:0.0} MB heights), {world.Triangles / 1000} k tris, {world.Tiles} tiles; {world.StageSummary}");
        Check("generated stage passed its own validation", stage.Report.Passed && !stage.Report.UsedFallback,
            string.Join("; ", stage.Report.Failures.Select(f => f.Name + " " + f.Detail)));
        Check("generated stage builds within the budget", world.BuildMillis < 8000, $"{world.BuildMillis} ms");
        Check("collision and render come from the same source: route vertex heights match the world",
            Mathf.Abs(world.SampleHeight(stage.PrimaryRoute.Vertices[100].Position.X, stage.PrimaryRoute.Vertices[100].Position.Z) - stage.PrimaryRoute.Vertices[100].Position.Y) < 1.5f);

        // Corridor not blocked (08 §5): no solid prop stands inside any line's corridor.
        {
            int solids = 0, inside = 0;
            float closest = float.MaxValue;
            foreach (var shape in world.FindChildren("*", "CollisionShape3D", recursive: true, owned: false))
            {
                if (shape is not CollisionShape3D cs || cs.GetParent() is not StaticBody3D body || body.Name != "PropColliders") continue;
                solids++;
                Vector3 g = cs.GlobalPosition;
                float d = stage.HeightField!.DistanceToRoute(g.X, g.Z);
                closest = Mathf.Min(closest, d);
                if (d < StageHeightField.CorridorHalfWidth) inside++;
            }
            Check("no solid prop stands inside a route corridor", solids > 0 && inside == 0, $"{inside} of {solids} colliders inside; closest {closest:0} m from a line");
        }

        foreach (var _ in Seconds(1.0f)) yield return null;
        Check("player spawns grounded on the start pad facing the route",
            _player.IsGrounded && Forward.Dot(stage.StartFacing) > 0.98f && _player.GlobalPosition.DistanceTo(stage.StartPosition) < 12f,
            $"grounded={_player.IsGrounded} pos={_player.GlobalPosition} fwd={Forward}");

        // Drive the whole primary route: steer at the vertex ~60 m ahead of the nearest one. A smoke
        // test of one seed against the route speed model, not an agent that plays stages (04 §12).
        var verts = stage.PrimaryRoute.Vertices;
        var profile = stage.SpeedProfile;
        int ticks = 0, grounded = 0, nearest = 0, exitTick = -1, impactsBefore = _player.ImpactCount;
        int maxTicks = Engine.PhysicsTicksPerSecond * 100;
        float maxSpeed = 0f, maxOffLine = 0f, nextMark = 1000f, worstMark = 0f;
        int markTicks = 0, markGrounded = 0, markRaw = 0;
        string marks = "";
        // Ground follow (D-092): raw contact per km, and whether each launch crest was taken as physics says.
        // Every feature is a launcher (D-097): a crest at its apex, a ramp at its lip, a gap at its far rim.
        var features = stage.PrimaryRoute.Features;
        var crests = features.Where(f => f.Kind == RouteFeatureKind.LaunchCrest).ToList();
        var crestKm = new HashSet<int>();
        // A launcher's kilometre is not quiet, nor the next; nor the one before when the launch (up to half a wavelength
        // before a crest's apex) begins there.
        foreach (var f in features) { int km = (int)(f.CentreDistance / 1000f); crestKm.Add((int)((f.CentreDistance - launchBeforeOf(f)) / 1000f)); crestKm.Add(km); crestKm.Add(km + 1); }
        static float launchBeforeOf(RouteFeature f) => f.Kind == RouteFeatureKind.LaunchCrest ? f.Wavelength * 0.5f : 20f;
        var crestApexSpeed = new float[crests.Count];
        var crestLeft = new bool[crests.Count];
        var launchPoint = features.Select(f => f.Kind == RouteFeatureKind.Gap ? f.FeatureEnd : f.CentreDistance).ToArray();
        var launchBefore = features.Select(f => f.Kind == RouteFeatureKind.LaunchCrest ? f.Wavelength * 0.5f : 20f).ToArray();
        // A launcher's window closes where the next one's opens (a dune train's crests are a wavelength apart), else at 900 m.
        var launchAfter = features.Select((f, c) => c + 1 < features.Count ? Mathf.Min(900f, launchPoint[c + 1] - launchBefore[c + 1] - launchPoint[c]) : 900f).ToArray();
        var crestLaunchS = new float[features.Count];
        var crestLandS = new float[features.Count];
        var launchArmed = new bool[features.Count];   // the ball must be grounded inside the window first: a gap's dive is still airborne when the far-rim window opens
        var quietKm = new List<(int km, float raw)>();
        while (ticks < maxTicks)
        {
            ticks++;
            Vector3 p = _player.GlobalPosition;
            float best = float.MaxValue;
            for (int i = Mathf.Max(0, nearest - 5); i < Mathf.Min(verts.Count, nearest + 60); i++)
            {
                float d = new Vector2(verts[i].Position.X - p.X, verts[i].Position.Z - p.Z).LengthSquared();
                if (d < best) { best = d; nearest = i; }
            }
            maxOffLine = Mathf.Max(maxOffLine, Mathf.Sqrt(best));
            float along = verts[nearest].Distance;
            for (int c = 0; c < crests.Count; c++)
            {
                float rel = along - crests[c].CentreDistance;
                if (crestApexSpeed[c] == 0f && rel >= 0f) crestApexSpeed[c] = _player.LocomotionSpeed;
                if (rel >= 0f && rel <= crests[c].Wavelength * 0.5f + 60f && !_player.IsGrounded) crestLeft[c] = true;
            }
            for (int c = 0; c < features.Count; c++)
            {
                float rel = along - launchPoint[c];
                if (rel >= -launchBefore[c] && rel <= launchAfter[c])
                {
                    if (_player.IsGrounded && crestLaunchS[c] == 0f) launchArmed[c] = true;
                    if (!_player.IsGrounded) { if (launchArmed[c] && crestLaunchS[c] == 0f) crestLaunchS[c] = along; if (crestLaunchS[c] > 0f) crestLandS[c] = 0f; }
                    else if (crestLaunchS[c] > 0f && crestLandS[c] == 0f) crestLandS[c] = along;
                }
            }
            if (System.Environment.GetEnvironmentVariable("RUSHCORE_DRIVE_TRACE") is { } tr && float.TryParse(tr, out float trAt) && Mathf.Abs(along - trAt) < 120f)
                GD.Print($"[TRACE] {along:0} m y={p.Y:0.00} ground={world.SampleHeight(p.X, p.Z):0.00} v=({_player.Velocity.X:0.0},{_player.Velocity.Y:0.0},{_player.Velocity.Z:0.0}) grounded={_player.IsGrounded} raw={_player.IsRawGrounded}");
            if (along >= nextMark)
            {
                float ballT = ticks / (float)Engine.PhysicsTicksPerSecond, modelT = profile.TimeAt(along);
                float err = Mathf.Abs(ballT - modelT) / Mathf.Max(1f, modelT);
                worstMark = Mathf.Max(worstMark, err);
                float rawFrac = markRaw / (float)Mathf.Max(1, markTicks);
                int km = (int)(nextMark / 1000f) - 1;
                if (!crestKm.Contains(km)) quietKm.Add((km, rawFrac));
                marks += $" {nextMark:0} m: ball {ballT:0.0} s model {modelT:0.0} s ({_player.LocomotionSpeed:0} vs {profile.SpeedAt(along):0} m/s, grounded {markGrounded / (float)Mathf.Max(1, markTicks):P0}, raw {rawFrac:P0}{(crestKm.Contains(km) ? ", crest" : "")});";
                nextMark += 1000f;
                markTicks = 0; markGrounded = 0; markRaw = 0;
            }
            markTicks++;
            if (_player.IsGrounded) markGrounded++;
            if (_player.IsRawGrounded) markRaw++;
            if (nearest >= verts.Count - 2) { exitTick = ticks; break; }
            var target = verts[Mathf.Min(verts.Count - 1, nearest + 15)].Position;
            _worldDrive = new Vector3(target.X - p.X, 0f, target.Z - p.Z);
            if (_player.IsGrounded) grounded++;
            maxSpeed = Mathf.Max(maxSpeed, _player.LocomotionSpeed);
            yield return null;
        }
        int armed = world.StageCheckpointIndex;
        ReleaseAll();
        float progressed = verts[nearest].Distance;
        float groundedFrac = grounded / (float)Mathf.Max(1, ticks);
        float ballTime = exitTick > 0 ? exitTick / (float)Engine.PhysicsTicksPerSecond : float.NaN;
        float timeErr = exitTick > 0 ? Mathf.Abs(ballTime - profile.TotalTime) / profile.TotalTime : 1f;
        GD.Print($"[SELFTEST] generated stage drive: {progressed:0} of {stage.PrimaryRoute.Length:0} m in {ticks / (float)Engine.PhysicsTicksPerSecond:0.0} s " +
                 $"(model {profile.TotalTime:0.0} s, {timeErr:P1}), grounded {groundedFrac:P0}, max {maxSpeed:0.0} m/s, off-line ≤ {maxOffLine:0} m, stage clock {world.StageClock:0.0} s;{marks}");
        Check("the ball drives the whole generated route to the exit pad", exitTick > 0, $"{progressed:0} of {stage.PrimaryRoute.Length:0} m");
        Check("route speed model predicts the whole-route base-kit time within 10% (08 §5)", exitTick > 0 && timeErr <= 0.10f,
            $"ball {ballTime:0.0} s, model {profile.TotalTime:0.0} s ({timeErr:P1}); marks:{marks}");
        Check("the follower stays inside the corridor for the whole route", maxOffLine < StageHeightField.CorridorHalfWidth, $"off-line ≤ {maxOffLine:0} m");
        Check("the corridor keeps the ball grounded most of the way", groundedFrac > 0.6f, $"{groundedFrac:P0}");
        // Ground follow (D-092): away from the launch crests the ball never loses contact; at a crest it leaves
        // exactly when v² exceeds g·r (r = λ² / (2π²H)), so the follow never glues a launch and never fakes one.
        // Vacuous on a seed whose every kilometre holds a launcher (a dune train does): the batch and the other seeds cover it.
        Check("the ground follow keeps raw contact on every km without a launch crest (≥ 97%)",
            quietKm.All(q => q.raw >= 0.97f), quietKm.Count == 0 ? "no kilometre without a launcher on this seed" : string.Join(", ", quietKm.Select(q => $"km {q.km + 1}: {q.raw:P0}")));
        {
            string crestDetail = ""; bool consistent = true; int decided = 0;
            for (int c = 0; c < crests.Count; c++)
            {
                float r = crests[c].Wavelength * crests[c].Wavelength / (2f * Mathf.Pi * Mathf.Pi * Mathf.Max(0.1f, crests[c].Height));
                float ratio = crestApexSpeed[c] * crestApexSpeed[c] / (m.Gravity * r);
                string verdict = ratio > 1.25f ? (crestLeft[c] ? "launched" : "GLUED") : ratio < 0.8f ? (crestLeft[c] ? "FAKE LAUNCH" : "rolled") : "marginal";
                if (ratio > 1.25f || ratio < 0.8f) { decided++; consistent &= verdict == "launched" || verdict == "rolled"; }
                crestDetail += $" crest {c + 1} at {crests[c].CentreDistance:0} m: r {r:0} m, apex {crestApexSpeed[c]:0} m/s, v²/gr {ratio:0.00}, {verdict};";
            }
            GD.Print($"[SELFTEST] launch crests:{crestDetail}");
            // Vacuous when the seed has no crest (or only marginal ones): the batch and the regression list cover others.
            Check("launch crests stay real: the ball leaves when v² > g·r and rolls when it does not", consistent, decided == 0 ? "no crest to decide on this seed" : crestDetail);
            // The base profile's airborne phase (D-094) against the measured flight at each launcher (crest, lip, far rim).
            string flightDetail = ""; bool flightsOk = true; int compared = 0;
            for (int c = 0; c < features.Count; c++)
            {
                if (crestLaunchS[c] <= 0f || crestLandS[c] <= 0f) continue;
                float window = Mathf.Max(30f, launchBefore[c] * 2f);
                var fl = profile.Flights.Where(f => Mathf.Abs(f.LaunchDistance - crestLaunchS[c]) < window).OrderBy(f => Mathf.Abs(f.LaunchDistance - crestLaunchS[c])).FirstOrDefault();
                float len = Mathf.Max(50f, crestLandS[c] - crestLaunchS[c]);
                float err = fl is null ? 1f : Mathf.Abs(fl.LandingDistance - crestLandS[c]) / len;
                compared++;
                // A gap's free-path exit is a deflection off the pit's V that the model does not simulate (it rides the
                // wall out instead), so for a gap the ball's landing must stay on the reserved straight; crests and
                // ramps hold the model to 20% of the flight.
                float straightEnd = verts[features[c].EndIndex].Distance;
                bool ok = features[c].Kind == RouteFeatureKind.Gap ? crestLandS[c] <= straightEnd : fl is not null && err <= 0.20f;
                flightsOk &= ok;
                flightDetail += $" {features[c].Kind} {c + 1}: ball {crestLaunchS[c]:0}→{crestLandS[c]:0} m, model {(fl is null ? "no flight" : $"{fl.LaunchDistance:0}→{fl.LandingDistance:0} m, lands {fl.LandingVerticalSpeed:0} m/s down → {fl.LandingSpeed:0} m/s")} ({err:P0} of the flight{(features[c].Kind == RouteFeatureKind.Gap ? $", straight to {straightEnd:0} m" : "")}){(ok ? "" : " FAIL")};";
            }
            GD.Print($"[SELFTEST] feature flights vs model:{flightDetail}");
            Check("the route speed model's flights land within 20% of the ball's at every crest and lip, and a gap exit lands on its straight", flightsOk, compared == 0 ? "no flight on this seed" : flightDetail);
        }
        Check("velocity finite after the generated-stage drive", _player.Velocity.IsFinite());
        Check("archetype geometry left the rigid body's hidden physics untouched (04 §7)", BodySnapshot() == bodyLab, BodySnapshot());
        Check("the corridor drive registers no Flow impact (no false mistakes on clean terrain)", _player.ImpactCount == impactsBefore, $"impacts {impactsBefore} -> {_player.ImpactCount}");
        Check("building and driving a stage never writes to tuning (04 §7)", TuningSnapshot() == tuningStage);

        // Progression anchors (04 §13): the drive armed checkpoints; a fall restores to the last one, not to the ball.
        Check("every progression anchor was armed by the drive", armed == stage.Checkpoints.Count - 1 && world.StageProgressIndex > 200,
            $"anchor {armed + 1}/{stage.Checkpoints.Count}, progress index {world.StageProgressIndex}");
        Vector3 cpBefore = _player.CheckpointPosition;
        _player.TeleportTo(_player.GlobalPosition + Vector3.Down * 3000f);
        foreach (var _ in Frames(6)) yield return null;
        _player.RequestRecovery();
        foreach (var _ in Frames(30)) yield return null;
        var anchorPos = armed >= 0 ? stage.Checkpoints[armed].Position : stage.StartPosition;
        Check("fall recovery restores to the armed stage anchor",
            new Vector2(_player.GlobalPosition.X - anchorPos.X, _player.GlobalPosition.Z - anchorPos.Z).Length() < 6f && _player.IsGrounded,
            $"pos={_player.GlobalPosition} anchor={anchorPos} cp={cpBefore}");
        Check("the anchor never moved backward with the fall", world.StageCheckpointIndex == armed);

        // Optional line: drive the first ridge line from its join for a few seconds.
        if (stage.OptionalLines.Count > 0)
        {
            var line = stage.OptionalLines[0];
            var lv = line.Vertices;
            foreach (var _ in Settle(world.SurfacePoint(lv[0].Position.X, lv[0].Position.Z, m.BallRadius + 0.5f), 0.6f)) yield return null;
            _player.LinearVelocity = new Vector3(Mathf.Cos(lv[0].Heading), 0f, Mathf.Sin(lv[0].Heading)) * 60f;
            int lt = 0, lg = 0, ln = 0;
            while (lt++ < Engine.PhysicsTicksPerSecond * 10 && ln < lv.Count - 20)
            {
                Vector3 p = _player.GlobalPosition;
                float best = float.MaxValue;
                for (int i = Mathf.Max(0, ln - 5); i < Mathf.Min(lv.Count, ln + 60); i++)
                {
                    float d = new Vector2(lv[i].Position.X - p.X, lv[i].Position.Z - p.Z).LengthSquared();
                    if (d < best) { best = d; ln = i; }
                }
                var target = lv[Mathf.Min(lv.Count - 1, ln + 15)].Position;
                _worldDrive = new Vector3(target.X - p.X, 0f, target.Z - p.Z);
                if (_player.IsGrounded) lg++;
                yield return null;
            }
            ReleaseAll();
            float lineFrac = lg / (float)Mathf.Max(1, lt);
            GD.Print($"[SELFTEST] optional line drive: {lv[ln].Distance:0} m of {line.Length:0} m (ridge {line.RidgeHeight:0} m), grounded {lineFrac:P0}");
            Check("the ridge line is driveable to its rejoin", lv[ln].Distance > line.Length * 0.6f, $"{lv[ln].Distance:0} of {line.Length:0} m");
            Check("the ridge corridor keeps the ball grounded", lineFrac > 0.5f, $"{lineFrac:P0}");
        }
        else Check("stage has an optional line to drive", true, "none on this seed (the default seed carries two; a chosen seed may carry none)");

        // Tube ride (08 §5, D-101): drive into the first tube at the cap and assert the walls carry the ball through, it
        // exits along the axis with no face crossed, and the camera stays outside the shell with a clear line of sight.
        // A drive seed without a tube borrows the first seed of its archetype that has one (the node-growth block restores).
        int tubeSeed = 0;
        if (stage.Tubes.Count == 0)
        {
            var gen = new StageGenerator(t.Movement, t.Flow, t.JumpSlam);
            for (int s = 1; s <= 60 && tubeSeed == 0; s++)
                if (gen.Generate(new StageGenerationRequest(s, 0, world.Archetype)).Tubes.Count > 0) tubeSeed = s;
            if (tubeSeed > 0)
            {
                world.Regenerate(tubeSeed);
                foreach (var _ in Frames(3)) yield return null;
                stage = world.Stage!;
                verts = stage.PrimaryRoute.Vertices;
                GD.Print($"[SELFTEST] tube ride: drive seed has no tube; using seed {tubeSeed}/0 ({stage.Tubes.Count} tubes)");
            }
        }
        if (stage.Tubes.Count > 0)
        {
            var tube = stage.Tubes[0];
            var axis = tube.Axis;
            float entryD = verts[tube.JoinStart].Distance;
            int startIdx = stage.PrimaryRoute.IndexAtDistance(Mathf.Max(0f, entryD - 700f));
            foreach (var _ in Settle(verts[startIdx].Position + Vector3.Up * (m.BallRadius + 0.6f), 0.5f)) yield return null;
            var rig = (Rushcore.Camera.CameraRig)_player.CameraBasis!;
            var space = _player.GetWorld3D().DirectSpaceState;
            var ray = new PhysicsRayQueryParameters3D { CollisionMask = 1, Exclude = new Godot.Collections.Array<Rid> { _player.GetRid() } };
            int rt = 0, rn = startIdx, ak = 0, insideTicks = 0, groundedInside = 0, sightBlocked = 0, pushed = 0;
            float worstRadial = 0f, minLens = float.MaxValue, entrySpeed = 0f, exitSpeed = 0f, exitAngle = 0f, maxSpeedIn = 0f;
            bool entered = false, exited = false;
            int phase = 0;   // 0 approach on the primary, 1 aim at the mouth, 2 ride the axis
            while (rt++ < Engine.PhysicsTicksPerSecond * 45 && !exited)
            {
                Vector3 p = _player.GlobalPosition;
                if (phase == 0)
                {
                    float best = float.MaxValue;
                    for (int i = Mathf.Max(0, rn - 5); i < Mathf.Min(verts.Count, rn + 60); i++)
                    {
                        float d = new Vector2(verts[i].Position.X - p.X, verts[i].Position.Z - p.Z).LengthSquared();
                        if (d < best) { best = d; rn = i; }
                    }
                    if (verts[rn].Distance >= entryD - 160f) phase = 1;
                    else { var tg = verts[Mathf.Min(verts.Count - 1, rn + 15)].Position; _worldDrive = new Vector3(tg.X - p.X, 0f, tg.Z - p.Z); }
                }
                if (phase == 1)
                {
                    Vector3 mouth = axis[0];
                    _worldDrive = new Vector3(mouth.X - p.X, 0f, mouth.Z - p.Z);
                    if (new Vector2(mouth.X - p.X, mouth.Z - p.Z).Length() < 12f) { phase = 2; entered = true; entrySpeed = _player.LocomotionSpeed; }
                }
                if (phase == 2)
                {
                    float best = float.MaxValue;
                    for (int i = Mathf.Max(0, ak - 5); i < Mathf.Min(axis.Length, ak + 40); i++)
                    {
                        float d = axis[i].DistanceSquaredTo(p);
                        if (d < best) { best = d; ak = i; }
                    }
                    var tg = axis[Mathf.Min(axis.Length - 1, ak + 12)];
                    _worldDrive = new Vector3(tg.X - p.X, 0f, tg.Z - p.Z);
                    insideTicks++;
                    if (_player.IsGrounded) groundedInside++;
                    maxSpeedIn = Mathf.Max(maxSpeedIn, _player.LocomotionSpeed);
                    // Radial distance of the ball's centre from the axis (the tangent component removed).
                    Vector3 rel = p - axis[ak], tan = tube.TangentAt(ak);
                    rel -= tan * rel.Dot(tan);
                    if (ak > 6 && ak < axis.Length - 6) worstRadial = Mathf.Max(worstRadial, rel.Length());
                    // Camera: lens outside the shell, ball visible against the terrain layer.
                    Vector3 lens = rig.Camera.GlobalPosition;
                    int li = tube.Nearest(lens, out float ld);
                    if (li > 6 && li < axis.Length - 6) minLens = Mathf.Min(minLens, ld);
                    if (rig.TubePushedThisFrame) pushed++;
                    ray.From = lens; ray.To = p;
                    if (space.IntersectRay(ray).Count > 0) sightBlocked++;
                    if (ak >= axis.Length - 4)
                    {
                        exited = true;
                        exitSpeed = _player.LocomotionSpeed;
                        Vector3 vel = _player.Velocity;
                        exitAngle = vel.LengthSquared() > 1f ? Mathf.RadToDeg(vel.Normalized().AngleTo(tube.TangentAt(axis.Length - 1))) : 180f;
                    }
                }
                yield return null;
            }
            ReleaseAll();
            float modelExit = tube.Profile!.Speed[^1];
            if (!exited) exitSpeed = _player.LocomotionSpeed;
            GD.Print($"[SELFTEST] tube ride: entered {entered} at {entrySpeed:0} m/s, {insideTicks} ticks inside (grounded {groundedInside / (float)Mathf.Max(1, insideTicks):P0}, tube contact {(t.Movement.TubeContact ? "on" : "off")}), max {maxSpeedIn:0} m/s, " +
                     $"radial ≤ {worstRadial:0.00} m of R {tube.Radius:0}, exit {exitSpeed:0} m/s (model {modelExit:0}) at {exitAngle:0}° to the axis; lens ≥ {minLens:0.0} m from the axis, pushed {pushed} frames, sight blocked {sightBlocked} ticks; {tube.Detail}");
            Check("the ball enters the tube at the cap and is carried through to the exit", entered && exited && entrySpeed > m.HardMaxLocomotionSpeed * 0.9f, $"entered={entered} exited={exited} entry {entrySpeed:0} m/s");
            Check("the ball never crosses a tube face (its centre stays inside the radius)", exited && worstRadial <= tube.Radius - m.BallRadius + 0.5f, $"radial ≤ {worstRadial:0.00} m, R {tube.Radius:0}");
            Check("the ball exits along the tube axis", exited && exitAngle <= 20f, $"{exitAngle:0}°");
            Check("the route speed model's carried profile predicts the exit speed within 10%", exited && Mathf.Abs(exitSpeed - modelExit) / Mathf.Max(1f, modelExit) <= 0.10f, $"ball {exitSpeed:0} m/s, model {modelExit:0} m/s");
            Check("the camera stays outside the tube for the whole ride", exited && minLens >= tube.Radius + WorldScale.TubeCameraMargin - 0.2f, $"lens ≥ {minLens:0.0} m from the axis (R {tube.Radius:0} + margin {WorldScale.TubeCameraMargin:0.0})");
            Check("the camera keeps a clear line of sight to the ball against the terrain through the ride", exited && sightBlocked == 0, $"{sightBlocked} of {insideTicks} ticks blocked");
        }
        else Check("stage has a tube to ride", true, "none on this seed nor on seeds 1–60 of this archetype");

        // Wall tunnel and spiral pit (08 §5, D-102), on a Canyon Run stage: a lid holds the ball from above (a charged
        // jump under it is refused) and from below (it is a floor), the camera stays under the roof; the spiral drives
        // to the exit pad on the pit floor, and a ball dropped off a turn's inner edge lands on the turn below and
        // drives on. A drive seed without both borrows the first canyon seed that has them.
        int canyonSeed = 0;
        if (world.Archetype == TerrainArchetype.CanyonRun)
        {
            if (stage.Lids.Count == 0 || stage.PrimaryRoute.Spiral is null)
            {
                var gen = new StageGenerator(t.Movement, t.Flow, t.JumpSlam);
                for (int s = 1; s <= 80 && canyonSeed == 0; s++)
                {
                    var d = gen.Generate(new StageGenerationRequest(s, 0, TerrainArchetype.CanyonRun));
                    if (d.Lids.Count > 0 && d.PrimaryRoute.Spiral is not null) canyonSeed = s;
                }
                if (canyonSeed > 0)
                {
                    world.Regenerate(canyonSeed);
                    foreach (var _ in Frames(3)) yield return null;
                    stage = world.Stage!;
                    verts = stage.PrimaryRoute.Vertices;
                    GD.Print($"[SELFTEST] canyon structures: drive seed lacks a lid or a pit; using seed {canyonSeed}/0 ({stage.Lids.Count} lids, spiral {(stage.PrimaryRoute.Spiral is not null)})");
                }
            }
            var rig = (Rushcore.Camera.CameraRig)_player.CameraBasis!;
            if (stage.Lids.Count > 0)
            {
                var lid = stage.Lids[0];
                float lidStart = verts[lid.StartIndex].Distance, lidEnd = verts[lid.EndIndex].Distance;
                int from = stage.PrimaryRoute.IndexAtDistance(Mathf.Max(0f, lidStart - 250f));
                foreach (var _ in Settle(verts[from].Position + Vector3.Up * (m.BallRadius + 0.6f), 0.5f)) yield return null;
                int ln = from, lt = 0, underTicks = 0, lensAbove = 0, confined = 0; float ballMaxY = float.MinValue; bool jumped = false, charging = false; int chargeTicks = 0;
                while (lt++ < Engine.PhysicsTicksPerSecond * 15)
                {
                    Vector3 p = _player.GlobalPosition;
                    float best = float.MaxValue;
                    for (int i = Mathf.Max(0, ln - 5); i < Mathf.Min(verts.Count, ln + 60); i++)
                    {
                        float d = new Vector2(verts[i].Position.X - p.X, verts[i].Position.Z - p.Z).LengthSquared();
                        if (d < best) { best = d; ln = i; }
                    }
                    float along = verts[ln].Distance;
                    if (along > lidEnd + 60f) break;
                    var tg = verts[Mathf.Min(verts.Count - 1, ln + 15)].Position;
                    _worldDrive = new Vector3(tg.X - p.X, 0f, tg.Z - p.Z);
                    // Charge for half a second on the approach and release just inside the tunnel: the roof must refuse it.
                    if (!jumped && along >= lidStart - 40f && !charging) { Input.ActionPress(InputBootstrap.Jump); charging = true; }
                    if (charging && ++chargeTicks >= Engine.PhysicsTicksPerSecond / 2) { Input.ActionRelease(InputBootstrap.Jump); charging = false; jumped = true; }
                    if (lid.Covers(p.X, p.Z))
                    {
                        underTicks++;
                        ballMaxY = Mathf.Max(ballMaxY, p.Y);
                        Vector3 lens = rig.Camera.GlobalPosition;
                        if (lid.Covers(lens.X, lens.Z) && lens.Y > lid.RoofBottom) lensAbove++;
                        if (rig.LidConfinedThisFrame) confined++;
                    }
                    yield return null;
                }
                ReleaseAll();
                GD.Print($"[SELFTEST] wall tunnel: {underTicks} ticks under the roof (bottom {lid.RoofBottom:0.0} m), ball ≤ {ballMaxY:0.0} m, jumped {jumped}; lens above the roof {lensAbove} ticks, confined {confined} frames; {lid.Detail}");
                Check("a lid holds the ball from above: the charged jump under the tunnel never puts the ball above the roof", jumped && underTicks > 30 && ballMaxY <= lid.RoofBottom - m.BallRadius + 0.3f, $"ball ≤ {ballMaxY:0.0} m, roof {lid.RoofBottom:0.0} m, {underTicks} ticks under");
                Check("the camera stays inside the declared clearance through the wall tunnel", underTicks > 30 && lensAbove == 0, $"{lensAbove} of {underTicks} ticks above the roof");
                // From below: the roof is a floor.
                Vector3 onTop = new(lid.Centre.X, lid.RoofBottom + lid.Thickness + m.BallRadius + 1f, lid.Centre.Z);
                foreach (var _ in Settle(onTop, 1.0f)) yield return null;
                float restY = _player.GlobalPosition.Y, expect = lid.RoofBottom + lid.Thickness + m.BallRadius;
                Check("a lid holds the ball from below: it rests on the roof as a floor", _player.IsGrounded && Mathf.Abs(restY - expect) < 0.6f, $"y {restY:0.00} vs roof top + radius {expect:0.00}, grounded {_player.IsGrounded}");
            }
            else Check("stage has a wall tunnel to test", true, "none on this seed nor on seeds 1–80");

            if (stage.PrimaryRoute.Spiral is { } pit)
            {
                // Drive the spiral to the exit pad on the pit floor.
                int sIdx = Mathf.Max(0, pit.StartIndex - 40);
                foreach (var _ in Settle(verts[sIdx].Position + Vector3.Up * (m.BallRadius + 0.6f), 0.5f)) yield return null;
                int sn = sIdx, st = 0; float minSpeed = float.MaxValue, spMax = 0f; int sg = 0;
                while (st++ < Engine.PhysicsTicksPerSecond * 40 && sn < verts.Count - 3)
                {
                    Vector3 p = _player.GlobalPosition;
                    float best = float.MaxValue;
                    for (int i = Mathf.Max(0, sn - 5); i < Mathf.Min(verts.Count, sn + 60); i++)
                    {
                        float d = verts[i].Position.DistanceSquaredTo(p);
                        if (d < best) { best = d; sn = i; }
                    }
                    var tg = verts[Mathf.Min(verts.Count - 1, sn + 15)].Position;
                    _worldDrive = new Vector3(tg.X - p.X, 0f, tg.Z - p.Z);
                    if (st > Engine.PhysicsTicksPerSecond * 6) { minSpeed = Mathf.Min(minSpeed, _player.LocomotionSpeed); spMax = Mathf.Max(spMax, _player.LocomotionSpeed); }
                    if (_player.IsGrounded) sg++;
                    yield return null;
                }
                ReleaseAll();
                bool reachedExit = sn >= verts.Count - 3;
                GD.Print($"[SELFTEST] spiral pit: r {pit.OuterRadius:0}→{pit.InnerRadius:0}, {pit.Depth:0} m deep over {pit.Length:0} m; drove to vertex {sn}/{verts.Count - 1} in {st / (float)Engine.PhysicsTicksPerSecond:0.0} s, speed {minSpeed:0}..{spMax:0} m/s, grounded {sg / (float)Mathf.Max(1, st):P0}; exit y {_player.GlobalPosition.Y:0.0} vs entry {verts[pit.StartIndex].Position.Y:0.0}");
                Check("the spiral pit drives to the exit pad on the pit floor", reachedExit && _player.GlobalPosition.Y < verts[pit.StartIndex].Position.Y - pit.Depth * 0.8f, $"vertex {sn}/{verts.Count - 1}, y {_player.GlobalPosition.Y:0.0}");
                Check("the spiral's turns keep the ball grounded and moving", sg / (float)Mathf.Max(1, st) > 0.9f && minSpeed > 40f, $"grounded {sg / (float)Mathf.Max(1, st):P0}, min {minSpeed:0} m/s");
                // Fall off the outer turn's inner edge: find the cliff (the ground drops 50 m within the turn spacing), land on the turn below, drive on.
                int q = pit.StartIndex + (int)((pit.EndIndex - pit.StartIndex) * 0.12f);
                var ov = verts[q];
                Vector3 inward = new Vector3(-Mathf.Sin(ov.Heading), 0f, Mathf.Cos(ov.Heading))
                               * Mathf.Sign(new Vector2(pit.Centre.X - ov.Position.X, pit.Centre.Z - ov.Position.Z).Dot(new Vector2(-Mathf.Sin(ov.Heading), Mathf.Cos(ov.Heading))));
                float cliffAt = float.NaN;
                for (float lat = 80f; lat <= WorldScale.SpiralRadiusPerTurn; lat += 2f)
                {
                    Vector3 g = ov.Position + inward * lat;
                    if (world.SampleHeight(g.X, g.Z) < ov.Position.Y - 50f) { cliffAt = lat; break; }
                }
                Vector3 edge = ov.Position + inward * (float.IsNaN(cliffAt) ? 118f : cliffAt + 6f) + Vector3.Up * 3f;
                GD.Print($"[SELFTEST] spiral fall: the cliff below the outer turn's inner edge begins {cliffAt:0} m from its centreline");
                foreach (var _ in Settle(edge, 0.2f)) yield return null;
                int ft = 0; float landedY = float.NaN;
                while (ft++ < Engine.PhysicsTicksPerSecond * 6)
                {
                    if (ft > 20 && _player.IsRawGrounded) { landedY = _player.GlobalPosition.Y; break; }
                    yield return null;
                }
                float drop = ov.Position.Y - landedY;
                int fn = q, fdt = 0;
                while (fdt++ < Engine.PhysicsTicksPerSecond * 30 && fn < verts.Count - 3)
                {
                    Vector3 p = _player.GlobalPosition;
                    float best = float.MaxValue;
                    for (int i = Mathf.Max(0, fn - 5); i < Mathf.Min(verts.Count, fn + 400); i++)
                    {
                        float d = verts[i].Position.DistanceSquaredTo(p);
                        if (d < best) { best = d; fn = i; }
                    }
                    var tg = verts[Mathf.Min(verts.Count - 1, fn + 15)].Position;
                    _worldDrive = new Vector3(tg.X - p.X, 0f, tg.Z - p.Z);
                    yield return null;
                }
                ReleaseAll();
                GD.Print($"[SELFTEST] spiral fall: dropped {drop:0.0} m off the outer turn's inner edge, landed grounded {!float.IsNaN(landedY)}, then drove to vertex {fn}/{verts.Count - 1}");
                Check("a ball dropped off a spiral turn's inner edge lands on the turn below and drives on to the exit (a fall is a setback)", !float.IsNaN(landedY) && drop > 20f && fn >= verts.Count - 3, $"drop {drop:0.0} m, vertex {fn}/{verts.Count - 1}");
            }
            else Check("stage has a spiral pit to test", true, "none on this seed nor on seeds 1–80");
        }

        // Node growth (08 §10): three regenerations of the same stage leave the tree the same size.
        {
            if (tubeSeed > 0 || canyonSeed > 0) { _debug.RestartSameSeed(); }   // back to the drive seed the regenerations rebuild
            foreach (var _ in Frames(3)) yield return null;
            int nodesStage = GetTree().GetNodeCount();
            double orphansStage = Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount);
            var counts = new List<int>();
            for (int r = 0; r < 3; r++)
            {
                _debug.RestartSameSeed();
                foreach (var _ in Frames(3)) yield return null;
                counts.Add(GetTree().GetNodeCount());
            }
            double orphansAfter = Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount);
            GD.Print($"[SELFTEST] node growth: lab {nodesLab}, stage {nodesStage}, after regenerations {string.Join("/", counts)}; orphans {orphansStage} -> {orphansAfter}");
            Check("SceneTree node count is flat across three stage regenerations", counts.All(c => c == nodesStage), $"{nodesStage} -> {string.Join("/", counts)}");
            Check("no orphan nodes accumulate across stage regenerations", orphansAfter <= orphansStage, $"{orphansStage} -> {orphansAfter}");
            Check("three regenerations never write to tuning", TuningSnapshot() == tuningStage);
        }

        t.World.GeneratedStage = false;
        t.World.Archetype = 0f;
        t.World.CellSize = MovementToyWorld.DefaultCellSize;
        _debug.RestartSameSeed();
        foreach (var _ in Frames(3)) yield return null;
        Check("lab restored after the generated stage", !_debug.World.IsStage && !_debug.World.IsStrip && Mathf.IsEqualApprox(_debug.World.HalfX, MovementToyWorld.Extent * 0.5f));
        Check("lab node count returns to its pre-stage value", GetTree().GetNodeCount() == nodesLab, $"{nodesLab} -> {GetTree().GetNodeCount()}");
    }
}
