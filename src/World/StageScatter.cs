using Godot;
using Rushcore.Generation;

namespace Rushcore.World;

/// <summary>
/// Where cosmetic scatter may stand on a generated stage (04 §5G, T4). Pure data built from a
/// <see cref="StageDefinition"/>: the dressing asks it before placing anything and the harness asks it about
/// everything placed, so the keep-out is stated once and checked against the same rule that produced it.
///
/// <para>Scatter is scenery. Nothing here changes generation data, and the stage hash never sees it.</para>
/// </summary>
public sealed class StageScatter
{
    /// <summary>Every line's level width plus its bend extra and half its falloff: the primary's, and every
    /// optional line's (all floors, terminal lines included) since the height field measures them all.</summary>
    public const float LineClearance = StageHeightField.CorridorHalfWidth + StageHeightField.BendExtraHalfWidth + StageHeightField.FalloffWidth * 0.5f;
    /// <summary>Beyond the pad's own radius (D-105: one to three flat 60 m pads, the places a stage ends).</summary>
    public const float ExitPadClearance = 20f;
    public const float LidClearance = 20f;
    public const float SpiralClearance = 40f;
    public const float AnchorClearance = 30f;
    /// <summary>No scatter collider stands nearer a line than this; farther props keep the toy's hard edges.</summary>
    public const float ColliderClearance = 200f;

    private readonly StageDefinition _stage;
    private readonly StageHeightField _field;

    public StageScatter(StageDefinition stage)
    {
        _stage = stage;
        _field = stage.HeightField!;
    }

    /// <summary>Plan distance to the nearest line's centreline (primary or optional, every floor).</summary>
    public float DistanceToLine(float x, float z) => _field.DistanceToRoute(x, z);

    /// <summary>Plan distance to the nearest exit pad's centre; +∞ when the stage has none.</summary>
    public float DistanceToExitPad(float x, float z)
    {
        float best = float.MaxValue;
        foreach (var e in _stage.Exits) best = Mathf.Min(best, Plan(e.Position, x, z));
        return best;
    }

    /// <summary>
    /// True where a prop may stand: outside every line, exit pad, checkpoint anchor, lid and the spiral
    /// disc. A module's body and its landing run lie on their line and are inside the line keep-out already.
    /// </summary>
    public bool Clear(float x, float z) => Clear(x, z, DistanceToLine(x, z));

    /// <summary>The same with the line distance already measured (the dressing measures it once per candidate).</summary>
    public bool Clear(float x, float z, float toLine)
    {
        if (toLine <= LineClearance) return false;
        if (DistanceToExitPad(x, z) <= WorldScale.PadRadius + ExitPadClearance) return false;
        foreach (var cp in _stage.Checkpoints)
            if (Plan(cp.Position, x, z) <= AnchorClearance) return false;
        foreach (var lid in _stage.Lids)
            if (DistanceToLid(lid, x, z) <= LidClearance) return false;
        if (_stage.PrimaryRoute.Spiral is { } pit && Plan(pit.Centre, x, z) <= pit.OuterRadius + SpiralClearance) return false;
        return true;
    }

    /// <summary>Which keep-out rejects a point, with its number; empty when the point is clear. The dressing
    /// never needs it; the harness reports it, so a violation names its own cause.</summary>
    public string Why(float x, float z)
    {
        float d = DistanceToLine(x, z);
        if (d <= LineClearance) return $"line at {d:0} m (clearance {LineClearance:0} m)";
        float pad = DistanceToExitPad(x, z);
        if (pad <= WorldScale.PadRadius + ExitPadClearance) return $"exit pad at {pad:0} m";
        foreach (var cp in _stage.Checkpoints)
        {
            float c = Plan(cp.Position, x, z);
            if (c <= AnchorClearance) return $"checkpoint anchor at {c:0} m";
        }
        foreach (var lid in _stage.Lids)
        {
            float l = DistanceToLid(lid, x, z);
            if (l <= LidClearance) return $"lid at {l:0} m";
        }
        if (_stage.PrimaryRoute.Spiral is { } pit)
        {
            float sp = Plan(pit.Centre, x, z);
            if (sp <= pit.OuterRadius + SpiralClearance) return $"spiral disc at {sp:0} m";
        }
        return "";
    }

    /// <summary>Plan distance to a lid's footprint (0 under the roof).</summary>
    private static float DistanceToLid(LidDefinition lid, float x, float z)
    {
        float dx = x - lid.Centre.X, dz = z - lid.Centre.Z;
        float along = Mathf.Abs(dx * Mathf.Cos(lid.Heading) + dz * Mathf.Sin(lid.Heading)) - lid.Length * 0.5f;
        float across = Mathf.Abs(-dx * Mathf.Sin(lid.Heading) + dz * Mathf.Cos(lid.Heading)) - lid.Width * 0.5f;
        return new Vector2(Mathf.Max(0f, along), Mathf.Max(0f, across)).Length();
    }

    private static float Plan(Vector3 p, float x, float z) => new Vector2(p.X - x, p.Z - z).Length();
}
