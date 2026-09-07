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
        var turnSign = new float[v.Count];
        foreach (var b in primary.Bends)
            for (int i = b.StartIndex; i <= b.EndIndex; i++) turnSign[i] = Mathf.Sign(b.TurnAngle);

        // A ridge shadows a bend, or a run of up to three (04 §6): it leaves the primary on the straight before the
        // first and rejoins on the straight after the last, both transitions wholly on straights (D-100: a transition
        // through a banked bend rides the berm and launches), the plateau on the outside. The section is the family's
        // ridge length, placed anywhere the two straights allow.
        float lastEnd = 500f - WorldScale.OptionalLineSpacing;   // leave the start alone
        var bends = primary.Bends;
        float T = WorldScale.RidgeTransition, L = WorldScale.RidgeLength;
        for (int k = 0; k < bends.Count && lines.Count < WorldScale.OptionalLinesMax; k++)
        {
            float bs = v[bends[k].StartIndex].Distance;
            float ps = k == 0 ? 0f : v[bends[k - 1].EndIndex].Distance;
            bool placed = false;
            for (int extra = 0; extra <= 2 && k + extra < bends.Count && !placed; extra++)
            {
                float beLast = v[bends[k + extra].EndIndex].Distance;
                float ne = k + extra + 1 < bends.Count ? v[bends[k + extra + 1].StartIndex].Distance : primary.Length;
                float dLo = Mathf.Max(Mathf.Max(ps, beLast + T - L), lastEnd + WorldScale.OptionalLineSpacing);
                float dHi = Mathf.Min(bs - T, Mathf.Min(ne - L, primary.Length - 400f - L));
                if (dHi < dLo) continue;
                float d = 0.5f * (dLo + dHi), dEnd = d + L;
                int a = primary.IndexAtDistance(d), b = primary.IndexAtDistance(dEnd);
                float first = rng.Sign();
                float side = SideValid(primary, turnSign, a, b, first) ? first : SideValid(primary, turnSign, a, b, -first) ? -first : 0f;
                if (side == 0f || OverlapsFeature(primary, d, dEnd) || !JoinsOnStraights(primary, a, b)) continue;
                var line = BuildLine(primary, a, b, side, rng.Range(WorldScale.RidgeHeightMin, WorldScale.RidgeHeightMax));
                if (line is null) continue;
                lines.Add(line);
                lastEnd = dEnd;
                placed = true;
            }
        }
        return lines;
    }

    /// <summary>The offset line between two primary vertices, or null if it leaves the optional band.</summary>
    private static RouteSkeleton? BuildLine(RouteSkeleton primary, int a, int b, float side, float ridgeHeight)
    {
        var v = primary.Vertices;
        {
            var line = new RouteSkeleton
            {
                Kind = RouteLineKind.Ridge,
                JoinStart = a,
                JoinEnd = b,
                CorridorHalfWidth = WorldScale.MinCorridorWidth * 0.5f,
                RidgeHeight = ridgeHeight,
            };
            float sectionLength = v[b].Distance - v[a].Distance;
            for (int i = a; i <= b; i++)
            {
                float offset = WorldScale.RidgeOffset * Bump(v[i].Distance - v[a].Distance, sectionLength);
                float h = v[i].Heading;
                var p = v[i].Position + new Vector3(-Mathf.Sin(h), 0f, Mathf.Cos(h)) * (offset * side);
                if (Mathf.Abs(p.Z) > WorldScale.OptionalBandHalfWidth) return null;
                float dist = line.Vertices.Count == 0 ? 0f : line.Vertices[^1].Distance + line.Vertices[^1].Position.DistanceTo(p);
                line.Vertices.Add(new RouteVertex { Position = p, Heading = h, Radius = float.PositiveInfinity, Distance = dist, Kind = RouteSegmentKind.Straight });
            }
            // Headings from the actual offset geometry (the outside of a bend is longer than the primary arc).
            for (int i = 0; i < line.Vertices.Count - 1; i++)
            {
                var rv = line.Vertices[i];
                Vector3 dd = line.Vertices[i + 1].Position - rv.Position;
                rv.Heading = Mathf.Atan2(dd.Z, dd.X);
                line.Vertices[i] = rv;
            }
            return line;
        }
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

    /// <summary>Route length at each end of a ridge that must lie on a primary straight: the whole transition.</summary>
    private const float JoinStraightLength = WorldScale.RidgeTransition;

    /// <summary>Both transitions lie on primary straights (the ridge leaves before the bend it shadows and rejoins
    /// after it): a transition crossing a banked bend rides the berm, whose full height reaches into the falloff
    /// the transition crosses, and that bump launched the base kit into the transition's own turn (D-100).</summary>
    private static bool JoinsOnStraights(RouteSkeleton primary, int a, int b)
    {
        var v = primary.Vertices;
        for (int i = a; i < v.Count && v[i].Distance <= v[a].Distance + JoinStraightLength; i++) if (v[i].Kind == RouteSegmentKind.Bend) return false;
        for (int i = b; i >= 0 && v[i].Distance >= v[b].Distance - JoinStraightLength; i--) if (v[i].Kind == RouteSegmentKind.Bend) return false;
        return true;
    }

    /// <summary>Lateral offset envelope along a line of the given length: 0 at both joins, 1 between the transitions.</summary>
    public static float Bump(float d, float length) =>
        Mathf.SmoothStep(0f, WorldScale.RidgeTransition, d) * (1f - Mathf.SmoothStep(length - WorldScale.RidgeTransition, length, d));

    /// <summary>Ridge plateau envelope: climbs once the line has left the primary, drops back before it rejoins.
    /// Cosine ramps: their knee curvature (π² H / 2L²) is 18% under a smoothstep's, and with the family's ridge
    /// height and ramp length the knee radius clears the cap's contact radius, so the climb never launches.</summary>
    public static float Plateau(float d, float length)
    {
        float up0 = WorldScale.RidgeTransition;
        float down1 = length - WorldScale.RidgeTransition;
        return CosineStep(up0, up0 + WorldScale.RidgeRampLength, d) * (1f - CosineStep(down1 - WorldScale.RidgeRampLength, down1, d));
    }

    private static float CosineStep(float a, float b, float d)
    {
        float t = Mathf.Clamp((d - a) / (b - a), 0f, 1f);
        return 0.5f * (1f - Mathf.Cos(Mathf.Pi * t));
    }

    /// <summary>A ridge section clears every feature's body and landing run: the two corridors' falloffs overlap,
    /// and a ridge riding the plain profile beside a crest, a pit or a lip would leak the feature into its path.
    /// The rest of a feature straight (a module's approach, the landing zone beyond the run) is plain ground the
    /// primary stamps last, and a ridge may use it (D-100).</summary>
    private static bool OverlapsFeature(RouteSkeleton primary, float dStart, float dEnd)
    {
        var v = primary.Vertices;
        foreach (var f in primary.Features)
        {
            float fs = f.Kind == RouteFeatureKind.LaunchCrest ? f.CentreDistance - f.Wavelength * 0.5f - 50f : v[f.StartIndex].Distance;
            float fe = f.FeatureEnd + WorldScale.LandingRunAfterFlight;
            if (dStart < fe && dEnd > fs) return true;
        }
        return false;
    }
}
