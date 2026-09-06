using Godot;
using FileAccess = Godot.FileAccess;

namespace Rushcore.Tuning;

/// <summary>One live-editable scalar. Explicit descriptors, not reflection (07 §2).</summary>
public sealed class TuningParameter
{
    public required string Category { get; init; }
    public required string Name { get; init; }
    public required float Min { get; init; }
    public required float Max { get; init; }
    public required Func<float> Get { get; init; }
    public required Action<float> Set { get; init; }
    public float DefaultValue { get; internal set; }
    public string Key => Category + "/" + Name;
}

public sealed class TuningToggle
{
    public required string Category { get; init; }
    public required string Name { get; init; }
    public required Func<bool> Get { get; init; }
    public required Action<bool> Set { get; init; }
    public bool DefaultValue { get; internal set; }
    public string Key => Category + "/" + Name;
}

public sealed class MovementTuning
{
    public float Gravity = 39.38f;
    public float GroundDriveAcceleration = 27.99f;
    public float GroundSteeringLateralAccel = 151.25f;
    /// <summary>Steering-authority multiplier reached at the hard speed cap (lerped from 1.0
    /// at rest). Below 1 speed removes authority; above 1 speed adds it. Accepted at 1.45
    /// (V-001): turn radius still grows as v^2 / (lateralAccel * mult), which satisfies D-002.</summary>
    public float HighSpeedSteeringMultiplier = 1.45f;
    public float AirControlMultiplier = 0.308f;
    /// <summary>Linear drag coefficient: a = -k*v. Governs coasting decay, not top speed.</summary>
    public float DragCoefficient = 0.077f;
    public float HardMaxLocomotionSpeed = 148.5f;
    /// <summary>
    /// Landing on a slope at the cap makes ground-tangent speed exceed the cap by
    /// 1/cos(slope) (D-069). That excess is bled at this rate (m/s^2) instead of being
    /// clipped in one tick, so the cap reads as feel rather than a wall (03 §5).
    /// </summary>
    public float LandingCapBleed = 39.95f;
    /// <summary>dot(contactNormal, Up) required for a contact to count as ground (03 §3).</summary>
    public float MinGroundNormalDot = 0.499f;
    /// <summary>Speed bands are readability/Flow hooks only (02 §5); no physics reads them.
    /// Ladder set against the accepted cap: Rush ~1/3, Crush ~2/3, Overdrive ~95% (V-004).</summary>
    public float RushThreshold = 50f;
    public float CrushThreshold = 95f;
    public float OverdriveThreshold = 141.06f;
    public float BallRadius = 2.125f;

    /// <summary>
    /// Solver friction for the player/terrain pair (both materials). The arcade controller
    /// owns traction and resistance (03 §1), so this is kept near zero and
    /// <see cref="DragCoefficient"/> is the single tunable resistance. Not a panel value: it
    /// is part of the frozen baseline's physical setup, and the route speed model reads it
    /// as a constant slip loss (the drive re-slips the rolling ball every tick).
    /// </summary>
    public const float SurfaceFriction = 0.02f;
}

public sealed class JumpSlamTuning
{
    public float MinJumpTakeoffVerticalSpeed = 2.03f;
    public float MaxJumpTakeoffVerticalSpeed = 58.21f;
    public float MaxJumpChargeSeconds = 0.445f;
    public float ChargeReleaseGraceSeconds = 0.10f;
    /// <summary>Immediate downward velocity established on slam so it reads as instant.</summary>
    public float SlamInitialDownwardSpeed = 42.95f;
    public float SlamDownwardAcceleration = 141.1f;
    public float SlamSteeringMultiplier = 0.25f;
    /// <summary>Fraction of lateral (locomotion) velocity kept at slam start.</summary>
    public float SlamLateralRetention = 1.0f;
    /// <summary>Every slam landing is a power impact (D-077): impact-power multiplier over a plain fall.</summary>
    public float SlamImpactMultiplier = 1.35f;
    /// <summary>Landing burst (D-077): a fresh Space press within this many seconds either side
    /// of a slam touchdown fires the burst. Presses during the slam are buffered.</summary>
    public float LandingBurstWindowSeconds = 0.10f;
    /// <summary>The burst sets locomotion speed to this fraction of the hard cap along the
    /// current heading; it never slows a faster ball and never changes direction.</summary>
    public float LandingBurstSpeedFraction = 0.8f;
}

public sealed class BoostTuning
{
    public float BoostAcceleration = 88.64f;
    /// <summary>Max input influence on boost direction at zero speed; falls to 0 at the cap.</summary>
    public float BoostDirectionBlend = 0.25f;
    public float BoostCapacity = 100f;
    public float BoostDrainRate = 30f;
    public float PassiveBoostRegen = 4f;
    public float PickupRefillAmount = 35f;
}

public sealed class CameraTuning
{
    public float Distance = 26f;
    public float PitchDegrees = -34f;
    public float YawFollowDamping = 3f;
    /// <summary>Degrees per second; caps how fast the view can swing.</summary>
    public float YawMaxTurnRate = 140f;
    /// <summary>Below this flat speed the yaw holds so a resting ball never spins the view.</summary>
    public float YawFollowMinSpeed = 2.035f;
    /// <summary>After the travel heading reverses (wall bounce, backward slide) the yaw is held
    /// only while the player pushes forward against it, and never longer than this. The camera
    /// always ends up behind the direction of travel.</summary>
    public float YawReverseHoldSeconds = 1.0f;
    /// <summary>Minimum height of the lens (and, +0.5, of the focus) above the heightfield.</summary>
    public float GroundClearance = 1.5f;
    public float HeightOffset = 3.0f;
    public float LookAheadMin = 2f;
    public float LookAheadMax = 22f;
    public float FollowDamping = 4.99f;
    public float VerticalDamping = 4f;
    public float FovMin = 62f;
    public float FovMax = 78f;
    public float SpeedDistanceGain = 10f;
    public float ShakeStrength = 1.0f;
    public float ShakeDecay = 6f;
    public float ZoomMin = 0.55f;
    public float ZoomMax = 2.0f;
    public float ZoomStep = 0.08f;
    /// <summary>Sphere-cast from the focus to the camera; pull in on terrain/props.</summary>
    public bool OcclusionProbe = true;
    public float OcclusionMargin = 0.6f;
    /// <summary>How fast the camera eases back out once the line of sight clears (1/s).</summary>
    public float OcclusionRecoverSpeed = 4f;
    /// <summary>Camera far plane in metres. 8 km sees the whole scale strip; a draw-distance instrument for M1.</summary>
    public float FarPlane = 8000f;
}

public sealed class VfxTuning
{
    public float ChargeEffectStrength = 1f;
    public float JumpReleaseStrength = 1f;
    public float TrailIntensity = 1f;
    public float DustIntensity = 1f;
    public float SlamEffectStrength = 1f;
    public float ImpactEffectStrength = 1f;
    /// <summary>Landing burst sparks, boom rings and flash (D-077).</summary>
    public float BurstEffectStrength = 1f;
    public float SquashStretchStrength = 1f;
    /// <summary>Visual-only cap on the ball's spin: a real 1 m ball at 60 m/s turns 9.5
    /// rev/s, which strobes at 60 fps. Collision is unaffected.</summary>
    public float MaxVisualRollRevPerSecond = 3f;
}

/// <summary>Macro handles for the Movement Toy calibration world only (07 §5).</summary>
public sealed class WorldTuning
{
    public float TerrainAmplitude = 2.125f;
    public float TerrainWavelength = 1.005f;
    public float PropDensity = 0.19f;
    /// <summary>Gate M1: replace the lab with the 6.4 km scale-calibration strip (rebuilds the world).</summary>
    public bool CalibrationStrip = false;
    /// <summary>Phase 2: replace the lab with a generated Rolling Highlands stage from the current seed (rebuilds the world).</summary>
    public bool GeneratedStage = false;
    /// <summary>Phase 2 debug view (04 §16): draw the primary route, its bends and crests above the terrain.</summary>
    public bool RouteDebugLines = true;
    /// <summary>Metres between height samples (= facet size). Rebuilds the world when the slider settles.
    /// The M1 budget choice: 4 m is the lab default; 8 m quarters the triangle count.</summary>
    public float CellSize = 4f;
    /// <summary>Depth-fog end in metres; begin is 16% of it. A sightline instrument for M1.</summary>
    public float FogEnd = 2400f;
}

/// <summary>
/// Single source of truth for feel values. Gameplay reads these; the debug panel
/// edits the same instance (07 §7). No duplicated magic numbers elsewhere.
/// </summary>
public sealed class GameplayTuning
{
    public readonly MovementTuning Movement = new();
    public readonly JumpSlamTuning JumpSlam = new();
    public readonly BoostTuning Boost = new();
    public readonly CameraTuning Camera = new();
    public readonly VfxTuning Vfx = new();
    public readonly WorldTuning World = new();

    public const string CatMovement = "Movement";
    public const string CatJumpSlam = "Jump / Slam";
    public const string CatBoost = "Boost";
    public const string CatCamera = "Camera";
    public const string CatVfx = "VFX";
    public const string CatWorld = "World";

    public static readonly string[] Categories =
        { CatMovement, CatJumpSlam, CatBoost, CatCamera, CatVfx, CatWorld };

    public IReadOnlyList<TuningParameter> Parameters { get; }
    public IReadOnlyList<TuningToggle> Toggles { get; }

    /// <summary>Raised after any bulk change (reset / load) so consumers can resync.</summary>
    public event Action? BulkChanged;

    public GameplayTuning()
    {
        var p = new List<TuningParameter>();
        var t = new List<TuningToggle>();

        void F(string cat, string name, float min, float max, Func<float> get, Action<float> set)
            => p.Add(new TuningParameter { Category = cat, Name = name, Min = min, Max = max, Get = get, Set = set });
        void B(string cat, string name, Func<bool> get, Action<bool> set)
            => t.Add(new TuningToggle { Category = cat, Name = name, Get = get, Set = set });

        var m = Movement;
        F(CatMovement, "Gravity", 5f, 60f, () => m.Gravity, v => m.Gravity = v);
        F(CatMovement, "Drive Acceleration", 0f, 120f, () => m.GroundDriveAcceleration, v => m.GroundDriveAcceleration = v);
        F(CatMovement, "Steering Lateral Accel", 0f, 300f, () => m.GroundSteeringLateralAccel, v => m.GroundSteeringLateralAccel = v);
        F(CatMovement, "High-Speed Steer Mult", 0.02f, 4f, () => m.HighSpeedSteeringMultiplier, v => m.HighSpeedSteeringMultiplier = v);
        F(CatMovement, "Air Control Mult", 0f, 1f, () => m.AirControlMultiplier, v => m.AirControlMultiplier = v);
        F(CatMovement, "Drag", 0f, 1.5f, () => m.DragCoefficient, v => m.DragCoefficient = v);
        F(CatMovement, "Hard Max Locomotion Speed", 5f, 250f, () => m.HardMaxLocomotionSpeed, v => m.HardMaxLocomotionSpeed = v);
        F(CatMovement, "Landing Cap Bleed", 2f, 400f, () => m.LandingCapBleed, v => m.LandingCapBleed = v);
        F(CatMovement, "Min Ground Normal Dot", 0.1f, 0.95f, () => m.MinGroundNormalDot, v => m.MinGroundNormalDot = v);
        F(CatMovement, "Rush Threshold", 1f, 250f, () => m.RushThreshold, v => m.RushThreshold = v);
        F(CatMovement, "Crush Threshold", 1f, 250f, () => m.CrushThreshold, v => m.CrushThreshold = v);
        F(CatMovement, "Overdrive Threshold", 1f, 250f, () => m.OverdriveThreshold, v => m.OverdriveThreshold = v);
        F(CatMovement, "Ball Radius", 0.25f, 4f, () => m.BallRadius, v => m.BallRadius = v);

        var j = JumpSlam;
        F(CatJumpSlam, "Min Jump Takeoff", 1f, 40f, () => j.MinJumpTakeoffVerticalSpeed, v => j.MinJumpTakeoffVerticalSpeed = v);
        F(CatJumpSlam, "Max Jump Takeoff", 1f, 100f, () => j.MaxJumpTakeoffVerticalSpeed, v => j.MaxJumpTakeoffVerticalSpeed = v);
        F(CatJumpSlam, "Max Charge Seconds", 0.05f, 2.5f, () => j.MaxJumpChargeSeconds, v => j.MaxJumpChargeSeconds = v);
        F(CatJumpSlam, "Charge Release Grace", 0f, 0.6f, () => j.ChargeReleaseGraceSeconds, v => j.ChargeReleaseGraceSeconds = v);
        F(CatJumpSlam, "Slam Initial Speed", 0f, 90f, () => j.SlamInitialDownwardSpeed, v => j.SlamInitialDownwardSpeed = v);
        F(CatJumpSlam, "Slam Downward Accel", 0f, 250f, () => j.SlamDownwardAcceleration, v => j.SlamDownwardAcceleration = v);
        F(CatJumpSlam, "Slam Steering Mult", 0f, 1.5f, () => j.SlamSteeringMultiplier, v => j.SlamSteeringMultiplier = v);
        F(CatJumpSlam, "Slam Lateral Retention", 0.3f, 1f, () => j.SlamLateralRetention, v => j.SlamLateralRetention = v);
        F(CatJumpSlam, "Slam Impact Mult", 1f, 4f, () => j.SlamImpactMultiplier, v => j.SlamImpactMultiplier = v);
        F(CatJumpSlam, "Burst Window (s)", 0.02f, 0.5f, () => j.LandingBurstWindowSeconds, v => j.LandingBurstWindowSeconds = v);
        F(CatJumpSlam, "Burst Speed Fraction", 0f, 1f, () => j.LandingBurstSpeedFraction, v => j.LandingBurstSpeedFraction = v);

        var b = Boost;
        F(CatBoost, "Boost Acceleration", 0f, 200f, () => b.BoostAcceleration, v => b.BoostAcceleration = v);
        F(CatBoost, "Direction Blend", 0f, 1f, () => b.BoostDirectionBlend, v => b.BoostDirectionBlend = v);
        F(CatBoost, "Capacity", 10f, 400f, () => b.BoostCapacity, v => b.BoostCapacity = v);
        F(CatBoost, "Drain Rate", 0f, 150f, () => b.BoostDrainRate, v => b.BoostDrainRate = v);
        F(CatBoost, "Passive Regen", 0f, 60f, () => b.PassiveBoostRegen, v => b.PassiveBoostRegen = v);
        F(CatBoost, "Pickup Refill", 0f, 200f, () => b.PickupRefillAmount, v => b.PickupRefillAmount = v);

        var k = Camera;
        F(CatCamera, "Distance", 6f, 90f, () => k.Distance, v => k.Distance = v);
        F(CatCamera, "Pitch Degrees", -85f, -5f, () => k.PitchDegrees, v => k.PitchDegrees = v);
        F(CatCamera, "Yaw Follow Damping", 0.2f, 20f, () => k.YawFollowDamping, v => k.YawFollowDamping = v);
        F(CatCamera, "Yaw Max Turn Rate", 10f, 720f, () => k.YawMaxTurnRate, v => k.YawMaxTurnRate = v);
        F(CatCamera, "Yaw Follow Min Speed", 0f, 20f, () => k.YawFollowMinSpeed, v => k.YawFollowMinSpeed = v);
        F(CatCamera, "Yaw Reverse Hold Seconds", 0f, 3f, () => k.YawReverseHoldSeconds, v => k.YawReverseHoldSeconds = v);
        F(CatCamera, "Ground Clearance", 0.2f, 6f, () => k.GroundClearance, v => k.GroundClearance = v);
        F(CatCamera, "Height Offset", -5f, 20f, () => k.HeightOffset, v => k.HeightOffset = v);
        F(CatCamera, "Look-Ahead Min", 0f, 40f, () => k.LookAheadMin, v => k.LookAheadMin = v);
        F(CatCamera, "Look-Ahead Max", 0f, 90f, () => k.LookAheadMax, v => k.LookAheadMax = v);
        F(CatCamera, "Follow Damping", 0.5f, 30f, () => k.FollowDamping, v => k.FollowDamping = v);
        F(CatCamera, "Vertical Damping", 0.2f, 30f, () => k.VerticalDamping, v => k.VerticalDamping = v);
        F(CatCamera, "FOV Min", 30f, 110f, () => k.FovMin, v => k.FovMin = v);
        F(CatCamera, "FOV Max", 30f, 120f, () => k.FovMax, v => k.FovMax = v);
        F(CatCamera, "Speed Distance Gain", 0f, 60f, () => k.SpeedDistanceGain, v => k.SpeedDistanceGain = v);
        F(CatCamera, "Shake Strength", 0f, 3f, () => k.ShakeStrength, v => k.ShakeStrength = v);
        F(CatCamera, "Shake Decay", 0.5f, 20f, () => k.ShakeDecay, v => k.ShakeDecay = v);
        B(CatCamera, "Occlusion Probe", () => k.OcclusionProbe, v => k.OcclusionProbe = v);
        F(CatCamera, "Occlusion Margin", 0.1f, 3f, () => k.OcclusionMargin, v => k.OcclusionMargin = v);
        F(CatCamera, "Occlusion Recover Speed", 0.5f, 20f, () => k.OcclusionRecoverSpeed, v => k.OcclusionRecoverSpeed = v);
        F(CatCamera, "Far Plane (m)", 1000f, 20000f, () => k.FarPlane, v => k.FarPlane = v);

        var x = Vfx;
        F(CatVfx, "Charge Effect", 0f, 3f, () => x.ChargeEffectStrength, v => x.ChargeEffectStrength = v);
        F(CatVfx, "Jump Release Effect", 0f, 3f, () => x.JumpReleaseStrength, v => x.JumpReleaseStrength = v);
        F(CatVfx, "Trail Intensity", 0f, 3f, () => x.TrailIntensity, v => x.TrailIntensity = v);
        F(CatVfx, "Dust Intensity", 0f, 3f, () => x.DustIntensity, v => x.DustIntensity = v);
        F(CatVfx, "Slam Effect", 0f, 3f, () => x.SlamEffectStrength, v => x.SlamEffectStrength = v);
        F(CatVfx, "Impact Effect", 0f, 3f, () => x.ImpactEffectStrength, v => x.ImpactEffectStrength = v);
        F(CatVfx, "Burst Effect", 0f, 3f, () => x.BurstEffectStrength, v => x.BurstEffectStrength = v);
        F(CatVfx, "Squash / Stretch", 0f, 3f, () => x.SquashStretchStrength, v => x.SquashStretchStrength = v);
        F(CatVfx, "Max Visual Roll (rev/s)", 0.5f, 12f, () => x.MaxVisualRollRevPerSecond, v => x.MaxVisualRollRevPerSecond = v);

        var w = World;
        F(CatWorld, "Terrain Amplitude", 0.1f, 3f, () => w.TerrainAmplitude, v => w.TerrainAmplitude = v);
        F(CatWorld, "Terrain Wavelength", 0.3f, 3f, () => w.TerrainWavelength, v => w.TerrainWavelength = v);
        F(CatWorld, "Prop Density", 0f, 3f, () => w.PropDensity, v => w.PropDensity = v);
        B(CatWorld, "Calibration Strip (M1)", () => w.CalibrationStrip, v => w.CalibrationStrip = v);
        B(CatWorld, "Generated Stage (Phase 2)", () => w.GeneratedStage, v => w.GeneratedStage = v);
        B(CatWorld, "Route Debug Lines", () => w.RouteDebugLines, v => w.RouteDebugLines = v);
        F(CatWorld, "Cell Size (m)", 2f, 8f, () => w.CellSize, v => w.CellSize = v);
        F(CatWorld, "Fog End (m)", 300f, 12000f, () => w.FogEnd, v => w.FogEnd = v);

        foreach (var e in p) e.DefaultValue = e.Get();
        foreach (var e in t) e.DefaultValue = e.Get();
        Parameters = p;
        Toggles = t;
    }

    /// <summary>Raise <see cref="BulkChanged"/> after an external edit (e.g. a debug hotkey).</summary>
    public void NotifyChanged() => BulkChanged?.Invoke();

    public void ResetAll()
    {
        foreach (var e in Parameters) e.Set(e.DefaultValue);
        foreach (var e in Toggles) e.Set(e.DefaultValue);
        BulkChanged?.Invoke();
    }

    public void ResetCategory(string category)
    {
        foreach (var e in Parameters) if (e.Category == category) e.Set(e.DefaultValue);
        foreach (var e in Toggles) if (e.Category == category) e.Set(e.DefaultValue);
        BulkChanged?.Invoke();
    }

    public const string OverridePath = "user://tuning_override_v1.json";
    private const float ModifiedEpsilon = 1e-4f;

    public static bool IsModified(TuningParameter p) => Mathf.Abs(p.Get() - p.DefaultValue) > ModifiedEpsilon;
    public static bool IsModified(TuningToggle t) => t.Get() != t.DefaultValue;

    /// <summary>How many values currently differ from the compiled defaults.</summary>
    public int OverrideCount
    {
        get
        {
            int n = 0;
            foreach (var e in Parameters) if (IsModified(e)) n++;
            foreach (var e in Toggles) if (IsModified(e)) n++;
            return n;
        }
    }

    public static bool OverrideFileExists(string? path = null) => FileAccess.FileExists(path ?? OverridePath);

    /// <summary>
    /// Writes only the values that differ from compiled defaults (05 §17: simple versioned
    /// JSON under user://). The file is therefore a diff of developer intent, and a fresh
    /// build with new defaults is not pinned to stale copies of unchanged values.
    /// </summary>
    public bool SaveOverride(string? path = null)
    {
        path ??= OverridePath;
        var values = new Godot.Collections.Dictionary();
        foreach (var e in Parameters) if (IsModified(e)) values[e.Key] = e.Get();
        foreach (var e in Toggles) if (IsModified(e)) values[e.Key] = e.Get();
        var dict = new Godot.Collections.Dictionary { { "version", 1 }, { "values", values } };

        using var f = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (f is null)
        {
            GD.PushWarning($"[RUSHCORE] Could not write {path}: {FileAccess.GetOpenError()}");
            return false;
        }
        f.StoreString(Json.Stringify(dict, "  "));
        GD.Print($"[RUSHCORE] Saved tuning override ({values.Count} values differ from defaults).");
        return true;
    }

    // ---------------- named presets: stash and compare variations ----------------

    public const string PresetDir = "user://tuning_presets";

    /// <summary>File-safe name: letters, digits, '-' and '_' only; at most 40 characters.</summary>
    public static string SanitizePresetName(string raw)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char ch in raw.Trim())
        {
            if (sb.Length >= 40) break;
            sb.Append(char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' ? ch : '_');
        }
        return sb.ToString().Trim('_');
    }

    public static string PresetPath(string name) => $"{PresetDir}/{name}.json";
    public static bool PresetExists(string name) => FileAccess.FileExists(PresetPath(name));

    public static List<string> ListPresets()
    {
        var names = new List<string>();
        using var dir = DirAccess.Open(PresetDir);
        if (dir is null) return names;
        foreach (string file in dir.GetFiles())
            if (file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                names.Add(file[..^5]);
        names.Sort(StringComparer.OrdinalIgnoreCase);
        return names;
    }

    public const string BundledPresetDir = "res://tuning/presets";

    /// <summary>
    /// Copies presets shipped in the repo into user:// on first sight so they appear in
    /// the panel. Never overwrites a preset the developer has since edited or saved.
    /// Returns how many were installed.
    /// </summary>
    public static int InstallBundledPresets()
    {
        using var bundled = DirAccess.Open(BundledPresetDir);
        if (bundled is null) return 0;
        using var user = DirAccess.Open("user://");
        if (user is null) return 0;
        if (!user.DirExists("tuning_presets") && user.MakeDirRecursive("tuning_presets") != Error.Ok) return 0;

        int installed = 0;
        foreach (string file in bundled.GetFiles())
        {
            if (!file.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
            string name = file[..^5];
            if (PresetExists(name)) continue;
            string text = FileAccess.GetFileAsString($"{BundledPresetDir}/{file}");
            using var f = FileAccess.Open(PresetPath(name), FileAccess.ModeFlags.Write);
            if (f is null) continue;
            f.StoreString(text);
            installed++;
        }
        if (installed > 0) GD.Print($"[RUSHCORE] Installed {installed} bundled tuning preset(s).");
        return installed;
    }

    public bool SavePreset(string name)
    {
        using var user = DirAccess.Open("user://");
        if (user is null) return false;
        if (!user.DirExists("tuning_presets") && user.MakeDirRecursive("tuning_presets") != Error.Ok) return false;
        return SaveOverride(PresetPath(name));
    }

    public int LoadPreset(string name) => LoadOverride(PresetPath(name));

    public static bool DeletePreset(string name)
    {
        if (!PresetExists(name)) return false;
        return DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(PresetPath(name))) == Error.Ok;
    }

    /// <summary>Applies the override on top of compiled defaults. Returns the number of values applied, or -1 if no file.</summary>
    public int LoadOverride(string? path = null)
    {
        path ??= OverridePath;
        if (!FileAccess.FileExists(path)) return -1;
        using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (f is null) return -1;
        if (Json.ParseString(f.GetAsText()).Obj is not Godot.Collections.Dictionary root) return -1;
        if (!root.ContainsKey("values") || root["values"].Obj is not Godot.Collections.Dictionary values) return -1;

        // Start from defaults so a diff-only file fully defines the resulting state.
        foreach (var e in Parameters) e.Set(e.DefaultValue);
        foreach (var e in Toggles) e.Set(e.DefaultValue);

        int applied = 0;
        foreach (var e in Parameters)
            if (values.TryGetValue(e.Key, out var v)) { e.Set(Mathf.Clamp((float)v, e.Min, e.Max)); applied++; }
        foreach (var e in Toggles)
            if (values.TryGetValue(e.Key, out var v)) { e.Set((bool)v); applied++; }

        BulkChanged?.Invoke();
        GD.Print($"[RUSHCORE] Loaded tuning override: {applied} values applied; {OverrideCount} differ from compiled defaults.");
        return applied;
    }
}
