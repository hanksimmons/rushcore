using Godot;
using Rushcore.Core;
using Rushcore.Tuning;

namespace Rushcore.Player;

public enum MovementState { Grounded, ChargingJump, Airborne, Slam }

public enum SpeedBand { Roll, Rush, Crush, Overdrive }

/// <summary>
/// Arcade-physics rolling controller (03). Real Jolt gravity/collision stays active;
/// this class shapes the body state inside <see cref="_IntegrateForces"/> only.
/// CustomIntegrator is deliberately left off (D-071).
/// </summary>
public partial class PlayerPhysics : RigidBody3D
{
    private readonly GameplayTuning _t;
    private readonly SphereShape3D _shape = new();

    // ---- input snapshot, sampled on the main thread in _PhysicsProcess ----
    private Vector2 _moveInput;
    /// <summary>cos(150°): desired input closer than 30° to straight against the heading brakes
    /// instead of turning, so plain W/S can never spin the ball (D-076).</summary>
    private const float BrakeConeDot = -0.866f;
    private bool _boostHeld;
    private bool _jumpPressedEdge, _jumpReleasedEdge, _jumpHeld;
    private bool _carveHeld;

    // ---- carve (03 §11, D-089): a held drift; facing swings, velocity understeers, release snaps ----
    private Vector3 _facing = Vector3.Forward;
    private float _carveEntrySpeed;
    private float _carveTurned;                                   // radians the facing has swung this carve

    // ---- movement state (03 §3: track only what behavior needs) ----
    private bool _rawGrounded;
    private bool _followActive;
    private float _groundStick;
    private Vector3 _groundNormal = Vector3.Up;
    private float _jumpLockout;
    private bool _isCharging;
    private float _chargeSeconds;
    private float _chargeGrace;
    private bool _slamActive;
    // ---- landing burst (D-077): timing only; no combo/rhythm state ----
    private float _sinceSlamLanding = float.PositiveInfinity;   // seconds since the last slam landing
    private float _sinceSlamPress = float.PositiveInfinity;     // seconds since a Space press during a slam
    private bool _burstArmed;                                     // slam landed; burst not yet fired or expired
    private bool _pendingBurst;
    private bool _boostActive;
    private float _boost;
    private float _defaultGravity = 9.8f;
    private float _capAllowance;
    private bool _landedThisTick;

    // ---- Flow (02 §8, 03 §5, D-088): execution quality that raises the effective cap ----
    private float _sinceFlowGain = float.PositiveInfinity;
    private float _prevLocSpeed;

    // ---- recovery ----
    private Vector3 _checkpoint;
    private float _checkpointTimer;
    private bool _pendingTeleport;
    private Vector3 _teleportPos;
    private bool _needsInterpolationReset;

    /// <summary>Solver friction for the player/terrain pair; owned by the tuning layer.</summary>
    public const float ArcadeSurfaceFriction = MovementTuning.SurfaceFriction;

    /// <summary>Ground grace used for locomotion only. Charge start still requires a raw contact.</summary>
    private const float GroundStickSeconds = 0.05f;
    /// <summary>Ground follow (03 §3, D-092): gaps inside the deadband are left to the solver; larger
    /// gaps close over this horizon as a velocity, never as a transform write.</summary>
    private const float GroundFollowDeadband = 0.03f;
    private const float GroundFollowCloseSeconds = 0.05f;
    private const float JumpLockoutSeconds = 0.08f;
    private const float CheckpointIntervalSeconds = 0.75f;

    public PlayerPhysics(GameplayTuning tuning)
    {
        _t = tuning;
        _boost = tuning.Boost.BoostCapacity;
    }

    // ---------------- public / debug state contract ----------------
    public MovementState State { get; private set; } = MovementState.Airborne;
    public bool IsGrounded { get; private set; }
    public bool IsRawGrounded => _rawGrounded;
    /// <summary>True while the analytic ground follow is carrying the ball this tick (03 §3, D-092).</summary>
    public bool GroundFollowActive => _followActive;
    /// <summary>The terrain grid the ground follow reads; null (the default) disables the follow.</summary>
    public IGroundSurface? Ground { get; set; }
    /// <summary>Structures whose walls carry the ball (tubes, D-101): the analytic shell the tube follow reads; null = none.</summary>
    public IStructureSurface? Structure { get; set; }
    /// <summary>True while the ball is inside a tube this tick (telemetry).</summary>
    public bool InTube { get; private set; }
    /// <summary>True while the tube follow is carrying the ball on the shell this tick.</summary>
    public bool TubeFollowActive { get; private set; }
    public Vector3 GroundNormal => _groundNormal;
    public Vector3 Velocity { get; private set; }
    /// <summary>Grounded: ground-tangent speed. Airborne: world-XZ speed (03 §5).</summary>
    public float LocomotionSpeed { get; private set; }
    public float VerticalSpeed => Velocity.Y;
    public float LocomotionCap => _t.Movement.HardMaxLocomotionSpeed;
    /// <summary>Flow 0..1: gained by perfect actions, lost by mistakes, never by time while a chain lives.</summary>
    public float Flow { get; private set; }
    /// <summary>Seconds since the last Flow gain (∞ before the first).</summary>
    public float SinceFlowGain => _sinceFlowGain;
    /// <summary>Base cap raised by Flow headroom: base × (1 + Flow × Headroom).</summary>
    public float FlowCap => Mathf.Max(0.001f, _t.Movement.HardMaxLocomotionSpeed) * (1f + Flow * Mathf.Max(0f, _t.Flow.Headroom));
    /// <summary>Hard impacts counted (one-tick locomotion loss above the tuned threshold).</summary>
    public int ImpactCount { get; private set; }
    public SpeedBand Band { get; private set; } = SpeedBand.Roll;
    public bool IsCharging => _isCharging;
    public float ChargeSeconds => _chargeSeconds;
    public float Charge01 => Mathf.Clamp(_chargeSeconds / Mathf.Max(0.001f, _t.JumpSlam.MaxJumpChargeSeconds), 0f, 1f);
    public float ComputedTakeoffSpeed =>
        Mathf.Lerp(_t.JumpSlam.MinJumpTakeoffVerticalSpeed, _t.JumpSlam.MaxJumpTakeoffVerticalSpeed, Charge01);
    /// <summary>True while a fresh Space press would fire the landing burst (D-077).</summary>
    public bool BurstWindowOpen => _burstArmed && _sinceSlamLanding <= _t.JumpSlam.LandingBurstWindowSeconds;
    /// <summary>Seconds left in the post-landing half of the burst window (0 when closed).</summary>
    public float BurstWindowRemaining => BurstWindowOpen ? _t.JumpSlam.LandingBurstWindowSeconds - _sinceSlamLanding : 0f;
    /// <summary>Locomotion speed established by the most recent landing burst.</summary>
    public float LastBurstSpeed { get; private set; }
    public int BurstCount { get; private set; }
    /// <summary>Cap currently enforced: base cap plus any decaying landing allowance.</summary>
    public float EffectiveLocomotionCap => FlowCap + _capAllowance;
    public bool SlamActive => _slamActive;
    /// <summary>Vertical speed established by the most recent jump release.</summary>
    public float LastTakeoffVerticalSpeed { get; private set; }
    /// <summary>Vertical speed observed at the instant the most recent slam began.</summary>
    public float LastSlamVerticalSpeed { get; private set; }
    public bool BoostActive => _boostActive;
    public float BoostAmount => _boost;
    public float Boost01 => Mathf.Clamp(_boost / Mathf.Max(0.001f, _t.Boost.BoostCapacity), 0f, 1f);
    public Vector2 InputVector => _moveInput;
    /// <summary>True while the carve button is held and the drift is live (03 §11).</summary>
    public bool IsCarving { get; private set; }
    /// <summary>Flat direction the ball faces: the travel heading, or the swung heading while carving.</summary>
    public Vector3 Facing => _facing;
    public float CarveEntrySpeed => _carveEntrySpeed;
    /// <summary>Degrees between the facing and the travel heading while carving.</summary>
    public float CarveAngleDegrees { get; private set; }
    public int CarveCount { get; private set; }
    public event Action<float>? CarveExited;
    /// <summary>Effective lateral steering acceleration applied this physics step (m/s^2).</summary>
    public float SteeringAuthority { get; private set; }
    public float ImpactPowerEstimate { get; private set; }
    public Vector3 CheckpointPosition => _checkpoint;

    public ICameraBasis? CameraBasis { get; set; }

    // ---------------- semantic events (05 §19) ----------------
    public event Action? JumpChargeStarted;
    public event Action? JumpChargeCanceled;
    public event Action<float>? Jumped;
    public event Action? Slammed;
    /// <summary>(impact speed, was a slam). Every slam landing is a power impact (D-077).</summary>
    public event Action<float, bool>? Landed;
    /// <summary>Landing burst fired; argument is the locomotion speed established.</summary>
    public event Action<float>? LandingBurst;
    public event Action<bool>? BoostActiveChanged;
    public event Action<SpeedBand>? SpeedBandChanged;
    public event Action? Recovered;

    public override void _Ready()
    {
        Name = "Player";
        _shape.Radius = _t.Movement.BallRadius;
        AddChild(new CollisionShape3D { Shape = _shape, Name = "Collider" });

        Mass = 1.0f;
        CollisionMask = 1 | Rushcore.World.MovementToyWorld.StructureLayer;   // terrain and props, plus lids and tubes (04 §9, D-101)
        ContinuousCd = true;                 // D-066: tunneling is a first-order risk
        CanSleep = false;                    // D-071
        ContactMonitor = true;               // D-067: grounded state reads direct contacts
        MaxContactsReported = 8;
        CustomIntegrator = false;            // keep standard Jolt gravity/damping
        LinearDampMode = DampMode.Replace;
        LinearDamp = 0f;                     // drag is applied explicitly below
        AngularDampMode = DampMode.Replace;
        AngularDamp = 0f;
        // The collider is a true sphere but visual roll is presentation-driven (03 §13),
        // so the simulated body itself never needs to spin.
        AxisLockAngularX = AxisLockAngularY = AxisLockAngularZ = true;
        PhysicsMaterialOverride = new PhysicsMaterial { Friction = ArcadeSurfaceFriction, Bounce = 0f };

        _defaultGravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity", 9.8f);
        // Semantic events are raised from inside _IntegrateForces and subscribers touch
        // scene state directly; that is only safe while physics runs on the main thread.
        if ((bool)ProjectSettings.GetSetting("physics/3d/run_on_separate_thread", false))
            GD.PushError("[RUSHCORE] physics/3d/run_on_separate_thread is enabled; player events assume main-thread physics.");
        _checkpoint = GlobalPosition;
    }

    public override void _PhysicsProcess(double delta)
    {
        _moveInput = InputBootstrap.ReadMoveVector();
        _boostHeld = Input.IsActionPressed(InputBootstrap.Boost);
        _carveHeld = Input.IsActionPressed(InputBootstrap.Carve);
        _jumpHeld = Input.IsActionPressed(InputBootstrap.Jump);
        if (Input.IsActionJustPressed(InputBootstrap.Jump)) _jumpPressedEdge = true;
        if (Input.IsActionJustReleased(InputBootstrap.Jump)) _jumpReleasedEdge = true;

        // Live tunables that live on engine objects rather than in our integration math.
        GravityScale = _t.Movement.Gravity / Mathf.Max(0.001f, _defaultGravity);
        if (!Mathf.IsEqualApprox(_shape.Radius, _t.Movement.BallRadius))
            _shape.Radius = _t.Movement.BallRadius;
    }

    public override void _Process(double delta)
    {
        if (_needsInterpolationReset)
        {
            _needsInterpolationReset = false;
            ResetPhysicsInterpolation();   // D-068: no old->new streak after a teleport
            Recovered?.Invoke();
        }
    }

    // ---------------- commands ----------------
    public void RefillBoost(float amount) =>
        _boost = Mathf.Clamp(_boost + amount, 0f, _t.Boost.BoostCapacity);

    public void SetCheckpoint(Vector3 position) => _checkpoint = position;
    /// <summary>Toy behaviour: re-anchor to the ball every 0.75 s of ground contact. A generated
    /// stage turns this off and supplies its own progression anchors (04 §13).</summary>
    public bool AutoCheckpoint { get; set; } = true;

    public void RequestRecovery() => TeleportTo(_checkpoint);

    public void TeleportTo(Vector3 position)
    {
        _teleportPos = position;
        _pendingTeleport = true;
    }

    // ---------------- physics ----------------
    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        float dt = (float)state.Step;
        if (dt <= 0f) return;

        if (_pendingTeleport)
        {
            ApplyTeleport(state);
            return;
        }

        Vector3 v = state.LinearVelocity;
        if (!v.IsFinite())
        {
            GD.PushError("[RUSHCORE] Non-finite player velocity; recovering.");
            _pendingTeleport = true;
            _teleportPos = _checkpoint;
            ApplyTeleport(state);
            return;
        }

        float preVerticalSpeed = v.Y;
        bool wasGrounded = IsGrounded;

        _landedThisTick = false;
        _sinceSlamLanding += dt;
        _sinceSlamPress += dt;
        UpdateGroundState(state, dt, ref v);
        HandleLanding(wasGrounded, preVerticalSpeed);
        UpdateJumpInput(dt, ref v);

        // ---- locomotion plane: ground tangent when grounded, world XZ when airborne ----
        Vector3 planeNormal = IsGrounded ? _groundNormal : Vector3.Up;
        float vN = v.Dot(planeNormal);
        Vector3 vT = v - planeNormal * vN;
        float speed = vT.Length();

        var m = _t.Movement;
        var f = _t.Flow;
        float cap = Mathf.Max(0.001f, m.HardMaxLocomotionSpeed);
        // Steering authority saturates at the base cap: the frozen curve (03 §4) is untouched by headroom.
        float speed01 = Mathf.Clamp(speed / cap, 0f, 1f);

        // Flow mistakes read from the ball, not from a subsystem: a hard impact sheds locomotion
        // speed in one tick between two grounded (or two airborne) ticks; brake and idle are below.
        _sinceFlowGain += dt;
        if (wasGrounded == IsGrounded && _prevLocSpeed - speed > f.ImpactSpeedLoss)
        {
            ImpactCount++;
            LoseFlow(f.LossImpact);
        }

        // Landing converts world-horizontal speed into ground-tangent speed, which is
        // larger by 1/cos(slope). Clipping that in one tick reads as hitting a wall, so
        // the excess becomes a short-lived allowance that bleeds away (03 §5, D-069).
        float flowCap = FlowCap;
        if (_landedThisTick && speed > flowCap) _capAllowance = Mathf.Max(_capAllowance, speed - flowCap);
        _capAllowance = Mathf.Max(0f, _capAllowance - m.LandingCapBleed * dt);

        Vector3 curDir = speed > 0.5f ? vT / speed : Vector3.Zero;
        // S / stick-back is a brake, never a reverse drive (D-076): it has no direction of
        // its own, so the travel heading, and therefore the chase camera, cannot flip from it.
        Vector2 driveInput = new(_moveInput.X, Mathf.Max(0f, _moveInput.Y));
        float brake01 = Mathf.Clamp(-_moveInput.Y, 0f, 1f);
        Vector3 desiredDir = ComputeDesiredDirection(planeNormal, curDir, driveInput);
        float inputMag = Mathf.Min(driveInput.Length(), 1f);

        // ---- landing burst (D-077, D-088): multiply the current speed along the current heading ----
        // Direction is never rewritten and the ball is never slowed, so the burst reads as a
        // seamless surge out of the landing. The burst's own Flow gain lands first, so the
        // multiplied speed is limited by the raised effective cap it just earned.
        if (_pendingBurst)
        {
            _pendingBurst = false;
            AddFlow(f.GainBurst);
            float limit = FlowCap + _capAllowance;
            if (curDir != Vector3.Zero && speed > 0.5f)
            {
                float target = Mathf.Min(speed * Mathf.Max(1f, _t.JumpSlam.LandingBurstMultiplier), Mathf.Max(speed, limit));
                vT = curDir * target;
                speed = target;
                speed01 = Mathf.Clamp(speed / cap, 0f, 1f);
            }
            LastBurstSpeed = speed;
            BurstCount++;
            LandingBurst?.Invoke(LastBurstSpeed);
        }
        float effectiveCap = FlowCap + _capAllowance;

        // ---- authority ----
        float lateral = m.GroundSteeringLateralAccel * Mathf.Lerp(1f, m.HighSpeedSteeringMultiplier, speed01);
        float drive = m.GroundDriveAcceleration;
        if (!IsGrounded)
        {
            lateral *= m.AirControlMultiplier;
            drive *= m.AirControlMultiplier;
        }
        if (_slamActive) lateral *= _t.JumpSlam.SlamSteeringMultiplier;
        // Charge locks line-control authority only; drive/gravity/boost are untouched (03 §4).
        if (_isCharging) lateral = 0f;
        SteeringAuthority = lateral;

        // ---- carve (03 §11, D-089): held drift; the exit is the one place velocity is re-aimed ----
        bool carveExitThisTick = UpdateCarve(dt, planeNormal, desiredDir, lateral, ref curDir, ref vT, ref speed);

        // ---- steer: rotate the travel direction, never rewrite it ----
        if (!IsCarving && !carveExitThisTick && speed > 0.5f && desiredDir != Vector3.Zero && lateral > 0f)
        {
            float angle = curDir.AngleTo(desiredDir);
            if (angle > 1e-5f)
            {
                // a_lat = v * omega  =>  turnRadius = v^2 / a_lat
                float maxAngle = lateral * dt / speed;
                Vector3 axis = curDir.Cross(desiredDir);
                // Input within the brake cone of straight against the heading has no turn
                // side: it only brakes (negative-alignment drive below), it is never a U-turn
                // whose direction float noise would pick. Clear lateral intent (W+A/D) makes
                // the side unambiguous and the normal hairpin applies.
                if (curDir.Dot(desiredDir) > BrakeConeDot && axis.LengthSquared() >= 1e-6f)
                {
                    curDir = curDir.Rotated(axis.Normalized(), Mathf.Min(maxAngle, angle));
                    vT = curDir * speed;
                }
            }
        }

        // ---- longitudinal drive (negative alignment still brakes on sharp turns) ----
        if (inputMag > 0.01f && desiredDir != Vector3.Zero)
        {
            if (speed > 0.5f)
            {
                // While carving the stick aims the facing, not the drive: drive stays along travel.
                float align = IsCarving ? 1f : curDir.Dot(desiredDir);
                vT += curDir * (drive * inputMag * align * dt);
            }
            else
            {
                vT += desiredDir * (drive * inputMag * dt);
            }
        }

        // ---- brake: shed speed along the current heading, never through zero ----
        if (brake01 > 0.01f && speed > 0.5f)
        {
            float shed = Mathf.Min(speed, drive * brake01 * dt);
            vT -= curDir * shed;
            LoseFlow(f.LossBrakePerSecond * brake01 * dt);   // braking is the one voluntary mistake
        }
        if (_sinceFlowGain > f.ChainWindowSeconds) LoseFlow(f.IdleDecayPerSecond * dt);

        ApplyBoost(ref vT, planeNormal, curDir, desiredDir, speed01, dt);

        // ---- drag ----
        float dragK = m.DragCoefficient;
        vT *= Mathf.Max(0f, 1f - dragK * dt);

        // ---- slam commits downward; vertical is never touched by the locomotion cap ----
        if (_slamActive && !IsGrounded)
        {
            vN -= _t.JumpSlam.SlamDownwardAcceleration * dt;
        }

        // ---- hard locomotion cap: clamp magnitude only, never rotate (03 §5, D-069) ----
        float locSpeed = vT.Length();
        if (locSpeed > effectiveCap)
        {
            vT *= effectiveCap / locSpeed;
            locSpeed = effectiveCap;
        }

        v = vT + planeNormal * vN;
        state.LinearVelocity = v;

        // ---- published state ----
        Velocity = v;
        LocomotionSpeed = locSpeed;
        State = _isCharging ? MovementState.ChargingJump
              : _slamActive ? MovementState.Slam
              : IsGrounded ? MovementState.Grounded
              : MovementState.Airborne;
        ImpactPowerEstimate = v.Length() * (_slamActive ? _t.JumpSlam.SlamImpactMultiplier : 1f);
        UpdateSpeedBand(locSpeed);
        UpdateBoostMeter(dt);
        UpdateCheckpoint(state, dt);
        _prevLocSpeed = locSpeed;

        _jumpPressedEdge = false;
        _jumpReleasedEdge = false;
    }

    private void ApplyTeleport(PhysicsDirectBodyState3D state)
    {
        _pendingTeleport = false;
        Transform3D tr = state.Transform;
        tr.Origin = _teleportPos;
        tr.Basis = Basis.Identity;
        state.Transform = tr;
        state.LinearVelocity = Vector3.Zero;
        state.AngularVelocity = Vector3.Zero;

        Velocity = Vector3.Zero;
        LocomotionSpeed = 0f;
        _burstArmed = false;
        _pendingBurst = false;
        _sinceSlamLanding = float.PositiveInfinity;
        _sinceSlamPress = float.PositiveInfinity;
        _isCharging = false;
        _chargeSeconds = 0f;
        _chargeGrace = 0f;
        _slamActive = false;
        _jumpLockout = 0f;
        _groundStick = 0f;
        _capAllowance = 0f;
        IsCarving = false;
        _carveTurned = 0f;
        CarveAngleDegrees = 0f;
        Flow = 0f;                                   // recovery / teleport ends the chain (02 §8)
        _sinceFlowGain = float.PositiveInfinity;
        _prevLocSpeed = 0f;
        _rawGrounded = false;
        _followActive = false;
        IsGrounded = false;
        _groundNormal = Vector3.Up;
        State = MovementState.Airborne;
        _needsInterpolationReset = true;
    }

    private void UpdateGroundState(PhysicsDirectBodyState3D state, float dt, ref Vector3 v)
    {
        if (_jumpLockout > 0f) _jumpLockout -= dt;

        Vector3 sum = Vector3.Zero;
        int hits = 0;
        int contacts = state.GetContactCount();
        // Tube contact (V-015 → D-101): inside a tube the walls carry the ball, so any contact is ground; the
        // normal it reports is the wall's, and drive and charge read from it.
        InTube = Structure is not null && Structure.Nearest(state.Transform.Origin, out _, out _, out float tubeRadius, out float tubeDist) && tubeDist <= tubeRadius * 2f;
        float minDot = InTube && _t.Movement.TubeContact ? -1f : _t.Movement.MinGroundNormalDot;
        for (int i = 0; i < contacts; i++)
        {
            Vector3 n = state.GetContactLocalNormal(i);
            if (n.Dot(Vector3.Up) >= minDot)
            {
                sum += n;
                hits++;
            }
        }

        // The analytic follow reads the same terrain grid the collider is built from, so a facet
        // edge that would hop the ball for a few ticks reads as continuous ground instead (D-092).
        _followActive = TryGroundFollow(state, dt, ref v, out Vector3 followNormal);
        Vector3 tubeNormal = Vector3.Up;
        TubeFollowActive = !_followActive && TryTubeFollow(state, dt, ref v, out tubeNormal);
        _rawGrounded = (hits > 0 || _followActive || TubeFollowActive) && _jumpLockout <= 0f;
        if (_rawGrounded)
        {
            _groundNormal = _followActive ? followNormal : TubeFollowActive ? tubeNormal : sum.Normalized();     // stable representative normal (03 §3)
            _groundStick = GroundStickSeconds;
        }
        else
        {
            if (_jumpLockout > 0f) _groundStick = 0f;
            else _groundStick -= dt;
            if (_groundStick <= 0f) _groundNormal = Vector3.Up;
        }

        IsGrounded = _rawGrounded || _groundStick > 0f;
    }

    /// <summary>
    /// Analytic ground follow (03 §3, D-092). The collider is flat facets; at speed every convex
    /// facet edge launches a real sphere for a few ticks, so contact flickers and a held charge
    /// cancels. The fix reads the terrain grid under the ball: where the surface could physically
    /// carry the ball (its curvature demand v²κ along travel is below the gravity available) and
    /// the ball is within the snap distance, the outward velocity component is removed, the gap is
    /// closed as a bounded velocity, and the ball counts as grounded with the grid's smooth normal.
    /// Where v²κ ≥ g nothing happens, so launches stay real. Never during jump lockout or a slam,
    /// never on a slope steeper than the ground limit, never against a falling ball (the solver
    /// lands that). A velocity rule only: no transform is written.
    /// </summary>
    private bool TryGroundFollow(PhysicsDirectBodyState3D state, float dt, ref Vector3 v, out Vector3 normal)
    {
        normal = Vector3.Up;
        var m = _t.Movement;
        if (Ground is not { } g || !m.GroundFollow || _jumpLockout > 0f || _slamActive) return false;
        Vector3 c = state.Transform.Origin;
        if (!g.Contains(c.X, c.Z)) return false;

        // Sample along and across the travel direction. The normal spans one cell each side and the
        // curvature three, so a single facet kink averages out while the underlying bend remains.
        float cell = Mathf.Max(0.5f, g.CellSize);
        Vector3 flat = new(v.X, 0f, v.Z);
        Vector3 d = flat.LengthSquared() > 1e-4f ? flat.Normalized() : Vector3.Forward;
        Vector3 q = new(-d.Z, 0f, d.X);
        float h0 = g.Height(c.X, c.Z);
        float hf = g.Height(c.X + d.X * cell, c.Z + d.Z * cell), hb = g.Height(c.X - d.X * cell, c.Z - d.Z * cell);
        float hl = g.Height(c.X + q.X * cell, c.Z + q.Z * cell), hr = g.Height(c.X - q.X * cell, c.Z - q.Z * cell);
        float sd = (hf - hb) / (2f * cell), sq = (hl - hr) / (2f * cell);
        Vector3 n = new Vector3(-(sd * d.X + sq * q.X), 1f, -(sd * d.Z + sq * q.Z)).Normalized();
        if (n.Y < m.MinGroundNormalDot) return false;

        float gap = (c.Y - h0) * n.Y - m.BallRadius;              // perpendicular distance from the resting height
        if (Mathf.Abs(gap) > m.GroundFollowSnapDistance) return false;
        float vN = v.Dot(n);
        if (vN < -m.GroundFollowSnapDistance / dt) return false;   // arriving faster than one snap per tick: a landing

        // Contact possibility: a surface curving away demands v²κ of centripetal acceleration; gravity supplies g·n.Y.
        float span = 3f * cell;
        float hF = g.Height(c.X + d.X * span, c.Z + d.Z * span), hB = g.Height(c.X - d.X * span, c.Z - d.Z * span);
        float second = (hF - 2f * h0 + hB) / (span * span);
        float slope2 = 1f + sd * sd;
        float kappa = second / (slope2 * Mathf.Sqrt(slope2));       // signed curvature along travel; negative = convex
        Vector3 vT = v - n * vN;
        if (kappa < 0f && vT.LengthSquared() * -kappa >= m.Gravity * n.Y) return false;

        float excess = Mathf.Max(0f, Mathf.Abs(gap) - GroundFollowDeadband) * Mathf.Sign(gap);
        float target = -excess / GroundFollowCloseSeconds;         // toward the surface; zero inside the deadband
        vN = gap >= 0f ? Mathf.Min(vN, target) : Mathf.Max(vN, target);
        v = vT + n * vN;
        normal = n;
        return true;
    }

    /// <summary>
    /// Tube follow (D-101, the tube twin of the ground follow): the shell's collider is a ring of flat facets and
    /// a ball at speed hops every facet edge, so inside a tube the controller reads the analytic shell (axis and
    /// radius) instead: within the snap distance of the wall the outward-moving component away from the wall is
    /// removed, the gap closes as a bounded velocity, and the ball counts as grounded with the wall's normal. A
    /// concave wall can always carry the ball, so there is no curvature test. Never during jump lockout or a slam,
    /// never against a ball arriving at the wall faster than one snap per tick (the solver lands that).
    /// </summary>
    private bool TryTubeFollow(PhysicsDirectBodyState3D state, float dt, ref Vector3 v, out Vector3 normal)
    {
        normal = Vector3.Up;
        var m = _t.Movement;
        if (Structure is null || !m.TubeContact || _jumpLockout > 0f || _slamActive) return false;
        Vector3 c = state.Transform.Origin;
        if (!Structure.Nearest(c, out Vector3 axisPoint, out Vector3 tangent, out float radius, out _)) return false;
        Vector3 rel = c - axisPoint;
        rel -= tangent * rel.Dot(tangent);
        float dist = rel.Length();
        if (dist < 1e-3f) return false;
        Vector3 outward = rel / dist;
        float gap = radius - m.BallRadius - dist;                   // > 0: inside, off the wall; < 0: pressed into it
        if (Mathf.Abs(gap) > m.GroundFollowSnapDistance) return false;
        float vOut = v.Dot(outward);
        if (vOut > m.GroundFollowSnapDistance / dt) return false;   // flying into the wall: a landing
        float excess = Mathf.Max(0f, Mathf.Abs(gap) - GroundFollowDeadband) * Mathf.Sign(gap);
        float target = excess / GroundFollowCloseSeconds;           // toward the wall (outward) when off it
        vOut = gap >= 0f ? Mathf.Max(vOut, target) : Mathf.Min(vOut, target);
        v = v - outward * v.Dot(outward) + outward * vOut;
        normal = -outward;
        return true;
    }

    private void HandleLanding(bool wasGrounded, float preVerticalSpeed)
    {
        if (wasGrounded || !IsGrounded) return;

        _landedThisTick = true;
        bool wasSlam = _slamActive;
        _slamActive = false;

        // Landing burst (D-077): every slam landing opens a short window. A press buffered
        // during the slam counts if it was inside the same window before touchdown.
        _burstArmed = wasSlam;
        _sinceSlamLanding = wasSlam ? 0f : float.PositiveInfinity;
        if (wasSlam && _sinceSlamPress <= _t.JumpSlam.LandingBurstWindowSeconds) FireBurst();
        _sinceSlamPress = float.PositiveInfinity;

        // Flow (02 §8): a slam landing is a perfect action; a hard landing without one is a mistake.
        if (wasSlam) AddFlow(_t.Flow.GainSlamLanding);
        else if (Mathf.Abs(preVerticalSpeed) >= _t.Flow.PlainLandingSpeed) LoseFlow(_t.Flow.LossPlainLanding);

        Landed?.Invoke(Mathf.Abs(preVerticalSpeed), wasSlam);
    }

    private void FireBurst()
    {
        _burstArmed = false;
        _pendingBurst = true;
    }

    /// <summary>
    /// Carve (03 §11, D-089). Start: button held, grounded, above the minimum speed. While held:
    /// the facing swings toward the input at the yaw rate; the velocity keeps only the understeer
    /// fraction of its steering authority, aimed at the facing, so the ball slides wide while the
    /// "model" already looks at the exit. Exit (release, or ground lost): velocity is re-aimed
    /// along the facing at no less than the entry speed. Nothing else in the tick changes, so the
    /// cap, drag and Flow rules apply as always. Returns true on the exit tick.
    /// </summary>
    private bool UpdateCarve(float dt, Vector3 planeNormal, Vector3 desiredDir, float lateral,
                             ref Vector3 curDir, ref Vector3 vT, ref float speed)
    {
        var c = _t.Carve;
        if (!IsCarving)
        {
            _facing = curDir != Vector3.Zero ? curDir : _facing;
            CarveAngleDegrees = 0f;
            if (_carveHeld && IsGrounded && !_isCharging && speed >= c.MinSpeed && curDir != Vector3.Zero)
            {
                IsCarving = true;
                _carveEntrySpeed = speed;
                _carveTurned = 0f;
                _facing = curDir;
            }
            return false;
        }

        // Keep the facing in the current locomotion plane.
        _facing -= planeNormal * _facing.Dot(planeNormal);
        _facing = _facing.LengthSquared() > 1e-6f ? _facing.Normalized() : curDir;

        bool exit = !_carveHeld || !IsGrounded || speed < 0.5f;
        if (!exit)
        {
            if (desiredDir != Vector3.Zero)
            {
                float angle = _facing.AngleTo(desiredDir);
                Vector3 axis = _facing.Cross(desiredDir);
                if (angle > 1e-4f && axis.LengthSquared() > 1e-8f)
                {
                    float step = Mathf.Min(angle, Mathf.DegToRad(Mathf.Max(0f, c.YawRateDegrees)) * dt);
                    _facing = _facing.Rotated(axis.Normalized(), step).Normalized();
                    _carveTurned += step;
                }
            }
            // Understeer: the velocity bites toward the facing with a fraction of normal authority.
            float bite = lateral * Mathf.Clamp(c.Understeer, 0f, 1f);
            float toFacing = curDir.AngleTo(_facing);
            Vector3 vAxis = curDir.Cross(_facing);
            if (toFacing > 1e-4f && bite > 0f && vAxis.LengthSquared() > 1e-8f)
            {
                curDir = curDir.Rotated(vAxis.Normalized(), Mathf.Min(toFacing, bite * dt / Mathf.Max(0.5f, speed)));
                vT = curDir * speed;
            }
            CarveAngleDegrees = Mathf.RadToDeg(curDir.AngleTo(_facing));
            return false;
        }

        // Exit: traction returns along the facing; no speed is lost (03 §11).
        IsCarving = false;
        float exitSpeed = Mathf.Max(speed, _carveEntrySpeed);
        curDir = _facing;
        vT = curDir * exitSpeed;
        speed = exitSpeed;
        CarveAngleDegrees = 0f;
        CarveCount++;
        if (Mathf.RadToDeg(_carveTurned) >= c.FlowGainMinDegrees) AddFlow(c.FlowGain);
        CarveExited?.Invoke(Mathf.RadToDeg(_carveTurned));
        return true;
    }

    private void AddFlow(float amount)
    {
        if (amount <= 0f) return;
        Flow = Mathf.Min(1f, Flow + amount);
        _sinceFlowGain = 0f;
    }

    private void LoseFlow(float amount)
    {
        if (amount <= 0f) return;
        Flow = Mathf.Max(0f, Flow - amount);
    }

    private void UpdateJumpInput(float dt, ref Vector3 v)
    {
        var js = _t.JumpSlam;

        if (_isCharging)
        {
            _chargeSeconds = Mathf.Min(_chargeSeconds + dt, js.MaxJumpChargeSeconds);   // clamps, never auto-jumps

            if (!_rawGrounded)
            {
                _chargeGrace += dt;
                if (_chargeGrace > js.ChargeReleaseGraceSeconds)
                {
                    _isCharging = false;
                    _chargeSeconds = 0f;
                    _chargeGrace = 0f;
                    JumpChargeCanceled?.Invoke();
                }
            }
            else
            {
                _chargeGrace = 0f;
            }
        }

        if (_burstArmed && _sinceSlamLanding > js.LandingBurstWindowSeconds) _burstArmed = false;

        if (_jumpPressedEdge)
        {
            if (_burstArmed && _sinceSlamLanding <= js.LandingBurstWindowSeconds)
            {
                // Landing burst (D-077): the press is consumed; it never starts a charge.
                FireBurst();
            }
            else if (_rawGrounded && !_isCharging)
            {
                // Charge begins only from a real ground contact; there is deliberately no
                // airborne coyote charge, which keeps the airborne press unambiguously slam.
                _isCharging = true;
                _chargeSeconds = 0f;
                _chargeGrace = 0f;
                JumpChargeStarted?.Invoke();
            }
            else if (!IsGrounded && !_isCharging && !_slamActive)
            {
                StartSlam(ref v);
            }
            else if (_slamActive && !IsGrounded)
            {
                // Early burst press: remembered so a press just before touchdown still counts.
                _sinceSlamPress = 0f;
            }
        }

        // Release is edge-driven, but the held-state fallback means a jump can never be
        // stranded if press and release land inside the same physics tick.
        if (_isCharging && (_jumpReleasedEdge || !_jumpHeld))
        {
            float charge01 = Charge01;
            float takeoff = Mathf.Lerp(js.MinJumpTakeoffVerticalSpeed, js.MaxJumpTakeoffVerticalSpeed, charge01);
            // Preserve all useful horizontal momentum; only establish upward velocity (D-012).
            v.Y = Mathf.Max(v.Y, takeoff);
            LastTakeoffVerticalSpeed = v.Y;
            if (charge01 >= 0.5f) AddFlow(_t.Flow.GainChargedJump);   // a large, risky jump (02 §8)

            _isCharging = false;
            _chargeSeconds = 0f;
            _chargeGrace = 0f;
            _slamActive = false;
            _jumpLockout = JumpLockoutSeconds;
            _rawGrounded = false;
            IsGrounded = false;
            _groundStick = 0f;
            _groundNormal = Vector3.Up;
            Jumped?.Invoke(charge01);
        }
    }

    private void StartSlam(ref Vector3 v)
    {
        var js = _t.JumpSlam;
        LastSlamVerticalSpeed = v.Y;
        _slamActive = true;

        v.X *= js.SlamLateralRetention;
        v.Z *= js.SlamLateralRetention;
        v.Y = Mathf.Min(v.Y, -js.SlamInitialDownwardSpeed);

        Slammed?.Invoke();
    }

    private Vector3 ComputeDesiredDirection(Vector3 planeNormal, Vector3 curDir, Vector2 driveInput)
    {
        // While charging, propulsion follows the existing travel heading instead of
        // letting WASD redirect the ball (03 §4).
        if (_isCharging) return curDir;

        Vector3 fwd = CameraBasis?.FlatForward ?? Vector3.Forward;
        Vector3 right = CameraBasis?.FlatRight ?? Vector3.Right;
        Vector3 desired = right * driveInput.X + fwd * driveInput.Y;
        desired -= planeNormal * desired.Dot(planeNormal);
        return desired.LengthSquared() > 1e-6f ? desired.Normalized() : Vector3.Zero;
    }

    private void ApplyBoost(ref Vector3 vT, Vector3 planeNormal, Vector3 curDir, Vector3 desiredDir, float speed01, float dt)
    {
        var b = _t.Boost;
        bool wantBoost = _boostHeld && _boost > 0f;
        if (wantBoost != _boostActive)
        {
            _boostActive = wantBoost;
            BoostActiveChanged?.Invoke(wantBoost);
        }
        if (!wantBoost) return;

        Vector3 travelDir = curDir != Vector3.Zero ? curDir : desiredDir;
        if (travelDir == Vector3.Zero) return;

        // Current trajectory dominates increasingly with speed (D-019).
        float blend = b.BoostDirectionBlend * (1f - speed01);
        if (!IsGrounded) blend *= _t.Movement.AirControlMultiplier;
        if (_isCharging) blend = 0f;

        Vector3 dir = desiredDir != Vector3.Zero ? travelDir.Lerp(desiredDir, blend) : travelDir;
        dir -= planeNormal * dir.Dot(planeNormal);
        if (dir.LengthSquared() < 1e-6f) return;

        vT += dir.Normalized() * (b.BoostAcceleration * dt);
    }

    private void UpdateBoostMeter(float dt)
    {
        var b = _t.Boost;
        if (_boostActive) _boost = Mathf.Max(0f, _boost - b.BoostDrainRate * dt);
        else _boost = Mathf.Min(b.BoostCapacity, _boost + b.PassiveBoostRegen * dt);
    }

    private void UpdateSpeedBand(float locomotionSpeed)
    {
        var m = _t.Movement;
        SpeedBand band = locomotionSpeed >= m.OverdriveThreshold ? SpeedBand.Overdrive
                       : locomotionSpeed >= m.CrushThreshold ? SpeedBand.Crush
                       : locomotionSpeed >= m.RushThreshold ? SpeedBand.Rush
                       : SpeedBand.Roll;
        if (band == Band) return;
        Band = band;
        SpeedBandChanged?.Invoke(band);
    }

    private void UpdateCheckpoint(PhysicsDirectBodyState3D state, float dt)
    {
        if (!AutoCheckpoint) return;
        _checkpointTimer -= dt;
        if (_checkpointTimer > 0f) return;
        _checkpointTimer = CheckpointIntervalSeconds;
        if (_rawGrounded && !_slamActive)
            _checkpoint = state.Transform.Origin + Vector3.Up * (_t.Movement.BallRadius + 0.5f);
    }
}

/// <summary>Minimal camera-basis contract consumed by camera-relative steering.</summary>
public interface ICameraBasis
{
    Vector3 FlatForward { get; }
    Vector3 FlatRight { get; }
}

/// <summary>The terrain grid the analytic ground follow reads (03 §3, D-092): the same samples the
/// collider and the render mesh are built from, so the one height source stays one.</summary>
/// <summary>A structure whose walls carry the ball (a tube): the nearest shell axis point to a position, its tangent and radius.</summary>
public interface IStructureSurface
{
    /// <summary>False when the position is outside every structure's bounds.</summary>
    bool Nearest(Vector3 position, out Vector3 axisPoint, out Vector3 tangent, out float radius, out float distance);
}

public interface IGroundSurface
{
    float CellSize { get; }
    bool Contains(float x, float z);
    float Height(float x, float z);
}
