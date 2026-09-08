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
    /// <summary>Plan clearance from a tube's axis, on top of its radius: the ground a tube flies over stays bare.</summary>
    public const float TubeClearance = 10f;
    public const float SpiralClearance = 40f;
    public const float AnchorClearance = 30f;
    /// <summary>No scatter collider stands nearer a line than this; farther props keep the toy's hard edges.</summary>
    public const float ColliderClearance = 200f;

    private readonly StageDefinition _stage;
    private readonly StageHeightField _field;
    /// <summary>Plan bounding circle of each tube's axis, grown by its radius and clearance: the axis walk only
    /// runs for the few points that could be near a tube at all.</summary>
    private readonly (Vector2 Centre, float Radius)[] _tubeCircles;

    public StageScatter(StageDefinition stage)
    {
        _stage = stage;
        _field = stage.HeightField!;
        _tubeCircles = new (Vector2, float)[stage.Tubes.Count];
        for (int t = 0; t < stage.Tubes.Count; t++)
        {
            var tube = stage.Tubes[t];
            var centre = Vector2.Zero;
            foreach (var a in tube.Axis) centre += new Vector2(a.X, a.Z);
            centre /= Mathf.Max(1, tube.Axis.Length);
            float reach = 0f;
            foreach (var a in tube.Axis) reach = Mathf.Max(reach, centre.DistanceTo(new Vector2(a.X, a.Z)));
            _tubeCircles[t] = (centre, reach + tube.Radius + TubeClearance);
        }
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
    /// True where a prop may stand: outside every line, exit pad, checkpoint anchor, lid, tube and the spiral
    /// disc. A module's body and its landing run lie on their line and are inside the line keep-out already.
    /// </summary>
    public bool Clear(float x, float z)
    {
        if (DistanceToLine(x, z) <= LineClearance) return false;
        if (DistanceToExitPad(x, z) <= WorldScale.PadRadius + ExitPadClearance) return false;
        foreach (var cp in _stage.Checkpoints)
            if (Plan(cp.Position, x, z) <= AnchorClearance) return false;
        foreach (var lid in _stage.Lids)
            if (DistanceToLid(lid, x, z) <= LidClearance) return false;
        if (NearATube(x, z)) return false;
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
        if (NearATube(x, z)) return "tube axis";
        if (_stage.PrimaryRoute.Spiral is { } pit)
        {
            float sp = Plan(pit.Centre, x, z);
            if (sp <= pit.OuterRadius + SpiralClearance) return $"spiral disc at {sp:0} m";
        }
        return "";
    }

    /// <summary>True within a tube's radius and clearance of its axis, in plan. The bounding circle short-circuits
    /// the walk: most of a stage is nowhere near a tube.</summary>
    private bool NearATube(float x, float z)
    {
        for (int t = 0; t < _tubeCircles.Length; t++)
        {
            var (centre, radius) = _tubeCircles[t];
            float dx = centre.X - x, dz = centre.Y - z;
            if (dx * dx + dz * dz > radius * radius) continue;
            var tube = _stage.Tubes[t];
            float keep = tube.Radius + TubeClearance;
            foreach (var a in tube.Axis)
            {
                float ax = a.X - x, az = a.Z - z;
                if (ax * ax + az * az <= keep * keep) return true;
            }
        }
        return false;
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
