using Godot;

namespace Rushcore.Generation;

/// <summary>
/// Lids (04 §5I, D-102): a wall tunnel is a box roof over a slot straight of the primary, spanning the corridor
/// wall to wall, its underside the family's clearance above the highest corridor point under it. Placed on plain
/// straights clear of features, tube mouths and the pit approach; the module declares its ceiling (§10 headroom).
/// </summary>
public static class LidBuilder
{
    public static List<LidDefinition> Build(RouteSkeleton primary, ulong stageSeed, ArchetypeRules rules, IReadOnlyList<TubeDefinition> tubes)
    {
        var lids = new List<LidDefinition>();
        if (rules.LidChance <= 0f) return lids;
        var rng = new SeededRandom(SeedChain.Derive(stageSeed, "lids"));
        var v = primary.Vertices;
        float lastEnd = 500f - WorldScale.OptionalLineSpacing;
        // Walk the straights between bends.
        int i = 0;
        while (i < v.Count && lids.Count < WorldScale.LidsMax)
        {
            if (v[i].Kind == RouteSegmentKind.Bend) { i++; continue; }
            int s = i;
            while (i + 1 < v.Count && v[i + 1].Kind == RouteSegmentKind.Straight) i++;
            float d0 = v[s].Distance, d1 = v[i].Distance, run = d1 - d0;
            i++;
            if (run < WorldScale.LidLengthMin + 100f) continue;
            float length = Mathf.Min(WorldScale.LidLengthMax, run - 100f);
            float centre = 0.5f * (d0 + d1);
            float from = centre - length * 0.5f, to = centre + length * 0.5f;
            if (from < lastEnd + WorldScale.OptionalLineSpacing * 0.5f) continue;
            if (to > primary.Length - 400f) break;
            if (primary.Spiral is { } pit && to > pit.ApproachDistance - 100f) break;
            if (FeatureBetween(primary, from - 50f, to + 50f) || TubeMouthBetween(primary, tubes, from - 50f, to + 50f)) continue;
            if (!rng.Chance(rules.LidChance)) continue;
            int a = primary.IndexAtDistance(from), b = primary.IndexAtDistance(to);
            float top = float.MinValue;
            for (int k = a; k <= b; k++) top = Mathf.Max(top, v[k].Position.Y);
            var mid = v[primary.IndexAtDistance(centre)];
            lids.Add(new LidDefinition
            {
                StartIndex = a, EndIndex = b,
                Centre = new Vector3(mid.Position.X, 0f, mid.Position.Z),
                Heading = mid.Heading,
                Length = length,
                Width = 2f * (StageHeightField.CorridorHalfWidth + WorldScale.WallSetback + rules.WallFalloff),
                Thickness = WorldScale.LidThickness,
                RoofBottom = top + WorldScale.LidClearance,
            });
            lastEnd = to;
        }
        return lids;
    }

    private static bool FeatureBetween(RouteSkeleton primary, float from, float to)
    {
        var v = primary.Vertices;
        foreach (var f in primary.Features)
        {
            float fs = f.Kind == RouteFeatureKind.LaunchCrest ? f.CentreDistance - f.Wavelength * 0.5f - 50f : v[f.StartIndex].Distance;
            if (from < f.ReservedEnd && to > fs) return true;
        }
        return false;
    }

    private static bool TubeMouthBetween(RouteSkeleton primary, IReadOnlyList<TubeDefinition> tubes, float from, float to)
    {
        var v = primary.Vertices;
        foreach (var t in tubes)
        {
            float ds = v[t.JoinStart].Distance, de = v[t.JoinEnd].Distance;
            if (from < ds + WorldScale.TubeMouthStraight * 2f && to > ds - WorldScale.TubeMouthStraight) return true;
            if (from < de + WorldScale.LandingZoneLength && to > de - WorldScale.TubeMouthStraight * 2f) return true;
        }
        return false;
    }
}
