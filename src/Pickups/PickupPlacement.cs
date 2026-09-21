using Godot;
using Rushcore.Generation;
using Rushcore.Tuning;

namespace Rushcore.Pickups;

/// <summary>What the pickup field holds (docs/16 §2): experience orbs (teal, 1 XP) and cash balls (gold, 1 cash).</summary>
public enum FieldPickupKind { Orb, Cash }

public readonly record struct PlacedPickup(FieldPickupKind Kind, Vector3 Position);

/// <summary>
/// Where a stage's orbs and cash balls stand (docs/16 §2, D-119). Pure data from a <see cref="StageDefinition"/> and a
/// height query: the dressing places what this returns and the harness asks it about every point placed, so the rule
/// is stated once. Deterministic from the stage seed (a <c>pickups</c> stream, <see cref="SeedChain.Derive"/>); the
/// stage hash never sees it.
///
/// <para>Orbs lie in clusters of 3–5 along the primary every <see cref="RunTuning.OrbClusterSpacing"/>, offset laterally
/// inside the level width so a line through them is a choice rather than the racing line, and along every optional line
/// at twice the density; never inside a feature's body or its landing run, on a pad, under a lid, in a tunnel's covered
/// run or within <see cref="StartClearance"/> of the start. Cash is rarer and earned: one on each optional line's
/// plateau at its middle (a tunnel's in its covered run), one past every ramp's lip on its landing, one at the far rim
/// of every gap, and a light scatter on the primary.</para>
/// </summary>
public static class PickupPlacement
{
    public const float StartClearance = 60f;
    /// <summary>Nothing lies in the exit zone: the last stretch before the primary's pad.</summary>
    public const float ExitClearance = 300f;
    /// <summary>The margin round a feature's reserved run (its body to the end of its landing) kept clear of orbs.</summary>
    public const float FeatureMargin = 30f;
    /// <summary>Orbs keep this far from either end of a tunnel's covered run along its line (a cluster is 24 m long).</summary>
    public const float CoverMargin = 30f;
    /// <summary>Orbs rest this far above the ground (their centre), cash a little higher.</summary>
    public const float OrbRest = 1.0f, CashRest = 1.2f;
    /// <summary>A cluster's orbs lie this far apart along the line.</summary>
    public const float ClusterStep = 6f;
    /// <summary>Lateral offset of a cluster, as a fraction of the corridor's half width (inside the level width).</summary>
    public const float LateralFraction = 0.6f;

    public static List<PlacedPickup> Place(StageDefinition stage, RunTuning run, Func<float, float, float> height)
    {
        var result = new List<PlacedPickup>();
        var rng = new SeededRandom(SeedChain.Derive(stage.Request.StageSeed, "pickups"));
        float spacing = Mathf.Max(40f, run.OrbClusterSpacing);
        var primary = stage.PrimaryRoute;

        // ---- orbs on the primary ----
        for (float d = StartClearance + spacing * 0.5f; d < primary.Length - ExitClearance; d += spacing)
            Cluster(stage, primary, d, primary.CorridorHalfWidth, ref rng, height, result);

        // ---- orbs on every optional line, at twice the density, clear of its transitions ----
        foreach (var line in stage.OptionalLines)
        {
            float from = line.Transition, to = line.Length - (line.Terminal ? WorldScale.PadRadius * 2.5f + StartClearance : line.Transition);
            for (float d = from + spacing * 0.25f; d < to; d += spacing * 0.5f)
                Cluster(stage, line, d, line.CorridorHalfWidth, ref rng, height, result);
        }

        // ---- cash, earned ----
        foreach (var line in stage.OptionalLines)
        {
            if (line.Terminal) continue;   // a terminal line ends on a pad: its reward is the exit
            int i = line.IsTunnel && line.CoverStart >= 0 ? (line.CoverStart + line.CoverEnd) / 2 : line.IndexAtDistance(line.Length * 0.5f);
            var v = line.Vertices[Mathf.Clamp(i, 0, line.Vertices.Count - 1)];
            result.Add(new PlacedPickup(FieldPickupKind.Cash, new Vector3(v.Position.X, height(v.Position.X, v.Position.Z) + CashRest, v.Position.Z)));
        }
        foreach (var module in stage.Modules)
        {
            float d = module.Kind switch
            {
                ChallengeModuleKind.LaunchRamp => (module.LandingStart + module.LandingEnd) * 0.5f,
                ChallengeModuleKind.ModerateGap => module.LandingStart + 10f,
                _ => float.NaN,
            };
            if (float.IsNaN(d)) continue;
            var v = primary.Vertices[primary.IndexAtDistance(d)];
            result.Add(new PlacedPickup(FieldPickupKind.Cash, new Vector3(v.Position.X, height(v.Position.X, v.Position.Z) + CashRest, v.Position.Z)));
        }
        // ---- cash, scattered along the primary ----
        int loose = Mathf.RoundToInt(Mathf.Max(0f, run.CashScatterPerKm) * primary.Length / 1000f);
        for (int n = 0; n < loose; n++)
        {
            float d = rng.Range(StartClearance, primary.Length - ExitClearance);
            if (!ClearOnPrimary(stage, d)) continue;
            var v = primary.Vertices[primary.IndexAtDistance(d)];
            float lateral = rng.Range(-0.4f, 0.4f) * primary.CorridorHalfWidth;
            float x = v.Position.X - Mathf.Sin(v.Heading) * lateral, z = v.Position.Z + Mathf.Cos(v.Heading) * lateral;
            if (!Clear(stage, x, z)) continue;
            result.Add(new PlacedPickup(FieldPickupKind.Cash, new Vector3(x, height(x, z) + CashRest, z)));
        }
        return result;
    }

    /// <summary>One cluster of 3–5 orbs along a line at a distance, offset to one side, if its ground is clear.</summary>
    private static void Cluster(StageDefinition stage, RouteSkeleton line, float d, float halfWidth, ref SeededRandom rng,
                                Func<float, float, float> height, List<PlacedPickup> result)
    {
        int count = 3 + rng.Next(3);
        float lateral = rng.Range(-LateralFraction, LateralFraction) * halfWidth;
        bool primary = line.Kind == RouteLineKind.Primary;
        if (primary && !ClearOnPrimary(stage, d)) return;
        if (line.IsTunnel && line.CoverStart >= 0
            && d >= line.Vertices[line.CoverStart].Distance - CoverMargin && d <= line.Vertices[line.CoverEnd].Distance + CoverMargin) return;
        for (int k = 0; k < count; k++)
        {
            float dk = d + (k - (count - 1) * 0.5f) * ClusterStep;
            int i = line.IndexAtDistance(dk);
            if (i < 0 || i >= line.Vertices.Count) continue;
            if (line.IsTunnel && line.Covered(i)) return;   // never in a covered run (docs/16 §2)
            var v = line.Vertices[i];
            float x = v.Position.X - Mathf.Sin(v.Heading) * lateral, z = v.Position.Z + Mathf.Cos(v.Heading) * lateral;
            if (!Clear(stage, x, z)) return;
            result.Add(new PlacedPickup(FieldPickupKind.Orb, new Vector3(x, height(x, z) + OrbRest, z)));
        }
    }

    /// <summary>True where a primary distance lies outside every feature's reserved run (its body and landing) plus the margin.</summary>
    public static bool ClearOnPrimary(StageDefinition stage, float d)
    {
        var v = stage.PrimaryRoute.Vertices;
        foreach (var f in stage.PrimaryRoute.Features)
        {
            float start = v[Mathf.Clamp(f.StartIndex, 0, v.Count - 1)].Distance - FeatureMargin;
            if (d >= start && d <= f.ReservedEnd + FeatureMargin) return false;
        }
        if (stage.PrimaryRoute.Spiral is { } pit && d >= pit.StartDistance - FeatureMargin && d <= pit.StartDistance + pit.Length + FeatureMargin) return false;
        return true;
    }

    /// <summary>True where a point is off every pad (start and exits) and under no lid.</summary>
    public static bool Clear(StageDefinition stage, float x, float z)
    {
        if (Plan(stage.StartPosition, x, z) <= WorldScale.PadRadius + StartClearance) return false;
        foreach (var e in stage.Exits) if (Plan(e.Position, x, z) <= WorldScale.PadRadius) return false;
        foreach (var lid in stage.Lids) if (lid.Covers(x, z)) return false;
        return true;
    }

    /// <summary>Why a placed point breaks the rule, or empty (the harness's report).</summary>
    public static string Why(StageDefinition stage, Vector3 p)
    {
        if (Plan(stage.StartPosition, p.X, p.Z) <= WorldScale.PadRadius + StartClearance) return "within the start clearance";
        foreach (var e in stage.Exits) if (Plan(e.Position, p.X, p.Z) <= WorldScale.PadRadius) return $"on exit pad {e.Label}";
        foreach (var lid in stage.Lids) if (lid.Covers(p.X, p.Z)) return "under a lid";
        return "";
    }

    private static float Plan(Vector3 a, float x, float z) => new Vector2(a.X - x, a.Z - z).Length();
}
