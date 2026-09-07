using Godot;
using Rushcore.Core;
using Rushcore.Player;

namespace Rushcore.DebugUi;

/// <summary>
/// Unobtrusive developer readout (07 §10). Reads public player state only, never writes.
/// Two monospaced columns in the top-left corner over a translucent plate, so the centre
/// of the screen and the mouse both stay free.
/// </summary>
public partial class TelemetryOverlay : Control
{
    private const int FontSize = 12;

    private static readonly Color LabelColor = new(0.58f, 0.66f, 0.78f);
    private static readonly Color ValueColor = new(0.92f, 0.96f, 1f);

    private static readonly Color[] BandColors =
    {
        new(0.72f, 0.80f, 0.90f),   // Roll
        new(0.45f, 0.90f, 0.70f),   // Rush
        new(1.00f, 0.78f, 0.30f),   // Crush
        new(1.00f, 0.45f, 0.35f),   // Overdrive
    };

    // Row order must match AddRow() order in _Ready.
    private enum Row
    {
        Frame, Physics, State, Band, Ground, Normal, Velocity, Locomotion, Vertical,
        Charge, Takeoff, Slam, Burst, Carve, Flow, Boost, Steering, Input, Impact,
        Position, Checkpoint, Camera, Seed, Stage, Tuning, Count,
    }

    private readonly IDebugActions _debug;
    private readonly Label[] _values = new Label[(int)Row.Count];
    private GridContainer _grid = null!;
    private SpeedBand _shownBand = (SpeedBand)(-1);

    public TelemetryOverlay(IDebugActions debug) => _debug = debug;

    public override void _Ready()
    {
        Name = "TelemetryOverlay";
        SetAnchorsPreset(LayoutPreset.TopLeft);
        MouseFilter = MouseFilterEnum.Ignore;
        ProcessMode = ProcessModeEnum.Always;   // keep reading while the tuning panel pauses the tree

        var plate = new PanelContainer
        {
            Position = new Vector2(10f, 10f),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        var bg = new StyleBoxFlat
        {
            BgColor = new Color(0.04f, 0.05f, 0.07f, 0.62f),
            ContentMarginLeft = 8,
            ContentMarginRight = 10,
            ContentMarginTop = 6,
            ContentMarginBottom = 6,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
        };
        plate.AddThemeStyleboxOverride("panel", bg);
        AddChild(plate);

        _grid = new GridContainer { Columns = 2, MouseFilter = MouseFilterEnum.Ignore };
        _grid.AddThemeConstantOverride("h_separation", 10);
        _grid.AddThemeConstantOverride("v_separation", 1);
        plate.AddChild(_grid);

        AddRow(Row.Frame, "frame");
        AddRow(Row.Physics, "physics");
        AddRow(Row.State, "state");
        AddRow(Row.Band, "band");
        AddRow(Row.Ground, "grounded");
        AddRow(Row.Normal, "gnormal");
        AddRow(Row.Velocity, "velocity");
        AddRow(Row.Locomotion, "locomotion");
        AddRow(Row.Vertical, "vertical");
        AddRow(Row.Charge, "charge");
        AddRow(Row.Takeoff, "takeoff");
        AddRow(Row.Slam, "slam");
        AddRow(Row.Burst, "burst");
        AddRow(Row.Carve, "carve");
        AddRow(Row.Flow, "flow");
        AddRow(Row.Boost, "boost");
        AddRow(Row.Steering, "steering");
        AddRow(Row.Input, "input");
        AddRow(Row.Impact, "impact");
        AddRow(Row.Position, "position");
        AddRow(Row.Checkpoint, "checkpoint");
        AddRow(Row.Camera, "camera");
        AddRow(Row.Seed, "seed");
        AddRow(Row.Stage, "stage");
        AddRow(Row.Tuning, "tuning");
    }

    private void AddRow(Row row, string caption)
    {
        var font = MonoFont;

        var name = new Label { Text = caption, MouseFilter = MouseFilterEnum.Ignore };
        Style(name, font, LabelColor);
        _grid.AddChild(name);

        var value = new Label { Text = "-", MouseFilter = MouseFilterEnum.Ignore };
        Style(value, font, ValueColor);
        _grid.AddChild(value);

        _values[(int)row] = value;
    }

    private static void Style(Label label, Font? font, Color color)
    {
        if (font is not null) label.AddThemeFontOverride("font", font);
        label.AddThemeFontSizeOverride("font_size", FontSize);
        label.AddThemeColorOverride("font_color", color);
    }

    private Font? _monoFont;

    /// <summary>Numbers must not jitter while reading them at speed.</summary>
    private Font MonoFont => _monoFont ??= new SystemFont
    {
        FontNames = new[] { "Menlo", "Monaco", "SF Mono", "Consolas", "DejaVu Sans Mono", "monospace" },
    };

    public override void _Process(double delta)
    {
        if (!Visible) return;

        var p = _debug.Player;
        var v = p.Velocity;
        var n = p.GroundNormal;
        var pos = p.GlobalPosition;
        var cp = p.CheckpointPosition;
        var input = p.InputVector;

        Set(Row.Frame, $"{Engine.GetFramesPerSecond()} fps   {delta * 1000d:0.0} ms");
        Set(Row.Physics, $"{Engine.PhysicsTicksPerSecond} Hz");
        Set(Row.State, p.State.ToString());
        Set(Row.Band, p.Band.ToString());
        Set(Row.Ground, $"{OnOff(p.IsGrounded)}   raw {OnOff(p.IsRawGrounded)}   follow {OnOff(p.GroundFollowActive)}");
        Set(Row.Normal, $"{n.X,6:0.00} {n.Y,6:0.00} {n.Z,6:0.00}");
        Set(Row.Velocity, $"{v.X,7:0.0} {v.Y,7:0.0} {v.Z,7:0.0}   |v| {v.Length():0.0}");
        Set(Row.Locomotion, $"{p.LocomotionSpeed,6:0.0} / {p.EffectiveLocomotionCap:0.0} m/s   (base {p.LocomotionCap:0.0})");
        Set(Row.Vertical, $"{p.VerticalSpeed,6:0.0} m/s");
        Set(Row.Charge, p.IsCharging
            ? $"{p.ChargeSeconds:0.00}s  ({p.Charge01:0.00})"
            : $"idle   ({p.Charge01:0.00})");
        Set(Row.Takeoff, $"{p.ComputedTakeoffSpeed:0.0} m/s");
        if (p.CameraBasis is Rushcore.Camera.CameraRig rig)
            Set(Row.Camera, $"dist {rig.CurrentDistance:0.0}  occl {rig.OcclusionFraction:0.00}  lookahead {rig.CurrentLookAhead:0.0}{(rig.ReverseHoldActive ? "  REV-HOLD" : "")}");
        Set(Row.Slam, $"{(p.SlamActive ? "ACTIVE" : "idle")}   impact x{_debug.Tuning.JumpSlam.SlamImpactMultiplier:0.00}");
        Set(Row.Burst, p.BurstWindowOpen
            ? $"WINDOW OPEN  {p.BurstWindowRemaining * 1000f:0} ms left"
            : $"idle   ±{_debug.Tuning.JumpSlam.LandingBurstWindowSeconds * 1000f:0} ms   count {p.BurstCount}  last {p.LastBurstSpeed:0.0} m/s");
        Set(Row.Carve, p.IsCarving
            ? $"CARVING  facing {p.CarveAngleDegrees:0}° off travel   entry {p.CarveEntrySpeed:0.0} m/s"
            : $"idle (Alt / LB)   count {p.CarveCount}");
        Set(Row.Flow, $"{p.Flow,5:0.00}   cap {p.FlowCap:0.0} m/s (headroom {_debug.Tuning.Flow.Headroom:P0})   " +
                      $"chain {(float.IsPositiveInfinity(p.SinceFlowGain) ? "—" : $"{p.SinceFlowGain:0.0} s")}   impacts {p.ImpactCount}");
        Set(Row.Boost, $"{p.BoostAmount,6:0.0} ({p.Boost01:0.00}) {(p.BoostActive ? "FIRING" : "")}");
        Set(Row.Steering, $"{p.SteeringAuthority:0.0} m/s2");
        Set(Row.Input, $"{input.X,6:0.00} {input.Y,6:0.00}");
        Set(Row.Impact, $"{p.ImpactPowerEstimate:0.0}");
        Set(Row.Position, $"{pos.X,7:0.0} {pos.Y,7:0.0} {pos.Z,7:0.0}");
        Set(Row.Checkpoint, $"{cp.X,7:0.0} {cp.Y,7:0.0} {cp.Z,7:0.0}");
        Set(Row.Seed, _debug.World.IsStage ? $"{_debug.SeedText}   {_debug.World.StageSummary}" : _debug.SeedText);
        var w = _debug.World;
        Set(Row.Stage, w.IsStage && w.Stage is { } st
            ? $"clock {w.StageClock,6:0.0} s   progress {st.PrimaryRoute.Vertices[w.StageProgressIndex].Distance,5:0} / {st.PrimaryRoute.Length:0} m   anchor {w.StageCheckpointIndex + 1}/{st.Checkpoints.Count}" +
              (w.StageExitTime > 0f ? $"   EXIT {w.StageExitTime:0.0} s" : "") +
              (st.Report.Passed ? "" : "   INVALID: " + string.Join("; ", st.Report.Failures.Select(f => f.Name)))
            : "-");
        int overrides = _debug.Tuning.OverrideCount;
        Set(Row.Tuning, overrides == 0 ? "compiled defaults" : $"OVERRIDE ({overrides} {(overrides == 1 ? "value differs" : "values differ")})");

        if (p.Band != _shownBand)
        {
            _shownBand = p.Band;
            var color = BandColors[Mathf.Clamp((int)_shownBand, 0, BandColors.Length - 1)];
            _values[(int)Row.Band].AddThemeColorOverride("font_color", color);
            _values[(int)Row.Locomotion].AddThemeColorOverride("font_color", color);
        }
    }

    private void Set(Row row, string text) => _values[(int)row].Text = text;

    private static string OnOff(bool value) => value ? "on" : "off";

    private static string YesNo(bool value) => value ? "yes" : "no";
}
