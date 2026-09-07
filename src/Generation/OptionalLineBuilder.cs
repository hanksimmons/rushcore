using Godot;

namespace Rushcore.Generation;

/// <summary>
/// Optional lines (04 §2, §6 "valley safety vs ridge shortcuts", §11): ridge lines that shadow a
/// primary section through one of its bends on the outside, offset laterally, and rise onto a
/// plateau before dropping back to the primary. Distinct by elevation and exposure, same base
/// kit; distance shortcuts are weak under the frozen baseline because the primary is already
/// nearly straight, so rewards and Phase 3 modules carry the risk/reward later. Deterministic.
/// </summary>
public static class OptionalLineBuilder
{

    public static List<RouteSkeleton> Build(RouteSkeleton primary, ulong stageSeed)
    {
        var rng = new SeededRandom(SeedChain.Derive(stageSeed, "optional"));
        var lines = new List<RouteSkeleton>();
        var v = primary.Vertices;
        float length = WorldScale.RidgeLength;
        float d = 500f;   // leave the start alone
        var turnSign = new float[v.Count];
        foreach (var b in primary.Bends)
            for (int i = b.StartIndex; i <= b.EndIndex; i++) turnSign[i] = Mathf.Sign(b.TurnAngle);

        // Scan the route for sections whose bends all turn the same way, so one side is the
        // outside of every bend the ridge shadows and the offset line never self-intersects.
        while (lines.Count < WorldScale.OptionalLinesMax && d + length < primary.Length - 400f)
        {
            float dEnd = d + length;
            int a = primary.IndexAtDistance(d), b = primary.IndexAtDistance(dEnd);
            float first = rng.Sign();
            float side = SideValid(primary, turnSign, a, b, first) ? first : SideValid(primary, turnSign, a, b, -first) ? -first : 0f;
            if (side == 0f || OverlapsFeature(primary, d, dEnd)) { d += 100f; continue; }
            var line = new RouteSkeleton
            {
                Kind = RouteLineKind.Ridge,
                JoinStart = a,
                JoinEnd = b,
                CorridorHalfWidth = WorldScale.MinCorridorWidth * 0.5f,
                RidgeHeight = rng.Range(WorldScale.RidgeHeightMin, WorldScale.RidgeHeightMax),
            };
            float sectionLength = v[b].Distance - v[a].Distance;
            bool inBand = true;
            for (int i = a; i <= b; i++)
            {
                float offset = WorldScale.RidgeOffset * Bump(v[i].Distance - v[a].Distance, sectionLength);
                float h = v[i].Heading;
                var p = v[i].Position + new Vector3(-Mathf.Sin(h), 0f, Mathf.Cos(h)) * (offset * side);
                if (Mathf.Abs(p.Z) > WorldScale.OptionalBandHalfWidth) { inBand = false; break; }
                float dist = line.Vertices.Count == 0 ? 0f : line.Vertices[^1].Distance + line.Vertices[^1].Position.DistanceTo(p);
                line.Vertices.Add(new RouteVertex { Position = p, Heading = h, Radius = float.PositiveInfinity, Distance = dist, Kind = RouteSegmentKind.Straight });
            }
            if (!inBand) { d += 100f; continue; }
            // Headings from the actual offset geometry (the outside of a bend is longer than the primary arc).
            for (int i = 0; i < line.Vertices.Count - 1; i++)
            {
                var rv = line.Vertices[i];
                Vector3 dd = line.Vertices[i + 1].Position - rv.Position;
                rv.Heading = Mathf.Atan2(dd.Z, dd.X);
                line.Vertices[i] = rv;
            }
            lines.Add(line);
            d = dEnd + WorldScale.OptionalLineSpacing;
        }
        return lines;
    }

    /// <summary>
    /// A side is valid when the offset line never crosses a bend's centre: on the outside of a bend
    /// anything goes; on the inside the offset at that point must leave the inside margin of radius.
    /// </summary>
    private static bool SideValid(RouteSkeleton primary, float[] turnSign, int a, int b, float side)
    {
        var v = primary.Vertices;
        float sectionLength = v[b].Distance - v[a].Distance;
        for (int i = a; i <= b; i++)
        {
            if (turnSign[i] == 0f) continue;
            bool inside = side == turnSign[i];
            if (!inside) continue;
            float offset = WorldScale.RidgeOffset * Bump(v[i].Distance - v[a].Distance, sectionLength);
            if (offset > v[i].Radius - WorldScale.InsideOffsetMargin) return false;
        }
        return true;
    }

    /// <summary>Lateral offset envelope along a line of the given length: 0 at both joins, 1 between the transitions.</summary>
    public static float Bump(float d, float length) =>
        Mathf.SmoothStep(0f, WorldScale.RidgeTransition, d) * (1f - Mathf.SmoothStep(length - WorldScale.RidgeTransition, length, d));

    /// <summary>Ridge plateau envelope: climbs once the line has left the primary, drops back before it rejoins.</summary>
    public static float Plateau(float d, float length)
    {
        float up0 = WorldScale.RidgeTransition;
        float down1 = length - WorldScale.RidgeTransition;
        return Mathf.SmoothStep(up0, up0 + WorldScale.RidgeRampLength, d) * (1f - Mathf.SmoothStep(down1 - WorldScale.RidgeRampLength, down1, d));
    }

    /// <summary>A ridge's joins and S-transitions (where it still meets the primary's stamp) must clear every
    /// feature; its plateau may shadow a module straight from 200 m away, riding the pre-module profile (D-097).</summary>
    private static bool OverlapsFeature(RouteSkeleton primary, float dStart, float dEnd)
    {
        var v = primary.Vertices;
        foreach (var f in primary.Features)
        {
            float fs = v[f.StartIndex].Distance, fe = v[f.EndIndex].Distance;
            if (dStart < fe && dStart + WorldScale.RidgeTransition + WorldScale.RidgeRampLength > fs) return true;
            if (dEnd - WorldScale.RidgeTransition - WorldScale.RidgeRampLength < fe && dEnd > fs) return true;
        }
        return false;
    }
}
