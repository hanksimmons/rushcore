using Godot;

namespace Rushcore.Generation;

/// <summary>
/// See-through tubes as optional lines (04 §5I, D-101): a tube leaves the primary through a ground mouth at the
/// corridor's edge, climbs to its cruise height at a lateral offset (like a ridge line, but in the air: the walls
/// carry the ball, so the path may bend tighter than the bend ladder), and descends to a flared exit mouth on the
/// primary's edge with a straight landing zone beyond it. The axis follows the primary's own geometry, offset by a
/// lateral envelope and lifted by a height envelope, so both mouths land exactly where the line is. The cruise clears
/// every point of ground under it by the camera clearance, so the lens always has room outside the shell.
/// </summary>
public static class TubeBuilder
{
    public static List<TubeDefinition> Build(RouteSkeleton primary, StageHeightField field, ulong stageSeed, ArchetypeRules rules, IReadOnlyList<RouteSkeleton> lines)
    {
        var tubes = new List<TubeDefinition>();
        if (rules.TubeChance <= 0f) return tubes;
        var rng = new SeededRandom(SeedChain.Derive(stageSeed, "tubes"));
        float M = WorldScale.TubeMouthStraight;
        float d = 600f;
        while (tubes.Count < WorldScale.TubesMax)
        {
            // The section length depends on the cruise height (a higher cruise climbs longer), so the site is judged
            // with the shortest section and the tube built with its own; a longer one that no longer fits is skipped.
            float L = SectionLength(WorldScale.TubeClearance);
            float dEnd = d + L;
            if (dEnd + WorldScale.LandingZoneLength >= primary.Length - 400f) break;
            bool clear = StraightBetween(primary, d - M, d + M) && StraightBetween(primary, dEnd - M, dEnd + WorldScale.LandingZoneLength)
                      && !FeatureBetween(primary, d - M, d + M) && !FeatureBetween(primary, dEnd - M, dEnd + WorldScale.LandingZoneLength)
                      && !LineOverlaps(primary, lines, tubes, d - M, dEnd + WorldScale.LandingZoneLength);
            if (!clear) { d += 100f; continue; }
            if (!rng.Chance(rules.TubeChance)) { d += 300f; continue; }
            var tube = Make(primary, field, lines, tubes, d, rng.Sign());
            if (tube is null) { d += 100f; continue; }
            tubes.Add(tube);
            d = primary.Vertices[tube.JoinEnd].Distance + WorldScale.OptionalLineSpacing;
        }
        return tubes;
    }

    /// <summary>Climb length for a cruise height: the ramp length, or longer to keep the pitch under the limit.</summary>
    public static float RampLength(float cruise) => Mathf.Max(WorldScale.TubeRampLength, cruise / WorldScale.TubeMaxPitch);
    public static float SectionLength(float cruise) => 2f * RampLength(cruise) + 2f * WorldScale.TubeSwingLength + WorldScale.TubeCruiseLength;

    /// <summary>Height envelope over a section: cosine climb over the ramp, cruise, cosine descent over the last ramp.</summary>
    public static float HeightEnvelope(float d, float span, float ramp)
    {
        return CosineStep(0f, ramp, d) * (1f - CosineStep(span - ramp, span, d));
    }

    /// <summary>Lateral envelope: still at the mouth offset through the climb, swinging out over the swing length, back before the descent.</summary>
    public static float LateralEnvelope(float d, float span, float ramp)
    {
        float s = WorldScale.TubeSwingLength;
        return CosineStep(ramp, ramp + s, d) * (1f - CosineStep(span - ramp - s, span - ramp, d));
    }

    private static bool StraightBetween(RouteSkeleton primary, float from, float to)
    {
        var v = primary.Vertices;
        for (int i = primary.IndexAtDistance(from); i < v.Count && v[i].Distance <= to; i++)
            if (v[i].Kind == RouteSegmentKind.Bend) return false;
        return true;
    }

    private static bool FeatureBetween(RouteSkeleton primary, float from, float to)
    {
        var v = primary.Vertices;
        foreach (var f in primary.Features)
        {
            float fs = f.Kind == RouteFeatureKind.LaunchCrest ? f.CentreDistance - f.Wavelength * 0.5f - 50f : v[f.StartIndex].Distance;
            float fe = f.FeatureEnd + WorldScale.LandingRunAfterFlight;
            if (from < fe && to > fs) return true;
        }
        return false;
    }

    private static bool LineOverlaps(RouteSkeleton primary, IReadOnlyList<RouteSkeleton> lines, List<TubeDefinition> tubes, float from, float to)
    {
        var v = primary.Vertices;
        foreach (var l in lines) if (from < v[l.JoinEnd].Distance && to > v[l.JoinStart].Distance) return true;
        foreach (var t in tubes) if (from < v[t.JoinEnd].Distance + WorldScale.LandingZoneLength && to > v[t.JoinStart].Distance - WorldScale.TubeMouthStraight) return true;
        return false;
    }

    private static float CosineStep(float a, float b, float d)
    {
        float t = Mathf.Clamp((d - a) / Mathf.Max(1e-3f, b - a), 0f, 1f);
        return 0.5f * (1f - Mathf.Cos(Mathf.Pi * t));
    }

    /// <summary>The tube from a section start, or null when its axis cannot clear the ground (it would pass through
    /// terrain mass, 04 §5I) or its cruise-sized section no longer fits the site.</summary>
    private static TubeDefinition? Make(RouteSkeleton primary, StageHeightField field, IReadOnlyList<RouteSkeleton> lines, List<TubeDefinition> tubes, float d, float side)
    {
        var v = primary.Vertices;
        float R = WorldScale.TubeRadius, M = WorldScale.TubeMouthStraight;
        // Two passes: the cruise height is read from the ground under the path, and the path's length depends on it.
        float cruise = WorldScale.TubeClearance;
        for (int pass = 0; pass < 2; pass++)
        {
            float ramp = RampLength(cruise), L = SectionLength(cruise);
            float dEnd = d + L;
            if (dEnd + WorldScale.LandingZoneLength >= primary.Length - 400f) return null;
            if (pass > 0 && (!StraightBetween(primary, dEnd - M, dEnd + WorldScale.LandingZoneLength) || FeatureBetween(primary, dEnd - M, dEnd + WorldScale.LandingZoneLength)
                          || LineOverlaps(primary, lines, tubes, d - M, dEnd + WorldScale.LandingZoneLength))) return null;
            int a = primary.IndexAtDistance(d), b = primary.IndexAtDistance(dEnd);
            float span = v[b].Distance - v[a].Distance;
            // The swing must not cross a bend's own centre: the outside of every bend in the section, or an inside with room.
            float Offset(int i) => WorldScale.TubeMouthOffset + (WorldScale.TubeLateralOffset - WorldScale.TubeMouthOffset) * LateralEnvelope(v[i].Distance - v[a].Distance, span, ramp);
            var turnSign = OptionalLineBuilder.TurnSigns(primary);
            if (!OptionalLineBuilder.SideValid(primary, turnSign, a, b, side, Offset))
            {
                if (!OptionalLineBuilder.SideValid(primary, turnSign, a, b, -side, Offset)) return null;
                side = -side;
            }
            int n = b - a + 1;
            var raw = new Vector3[n];
            float needed = WorldScale.TubeClearance;
            bool ok = true;
            for (int i = 0; i < n; i++)
            {
                var pv = v[a + i];
                float dist = pv.Distance - v[a].Distance;
                float offset = WorldScale.TubeMouthOffset + (WorldScale.TubeLateralOffset - WorldScale.TubeMouthOffset) * LateralEnvelope(dist, span, ramp) * side;
                Vector3 p = pv.Position + new Vector3(-Mathf.Sin(pv.Heading), 0f, Mathf.Cos(pv.Heading)) * offset;
                if (Mathf.Abs(p.Z) > WorldScale.OptionalBandHalfWidth) return null;
                float e = HeightEnvelope(dist, span, ramp);
                float y = pv.Position.Y + R + cruise * e;
                raw[i] = new Vector3(p.X, y, p.Z);
                float ground = field.Sample(p.X, p.Z);
                // Ground under the path relative to the corridor: the cruise must clear its highest point by the clearance.
                needed = Mathf.Max(needed, ground - pv.Position.Y + WorldScale.TubeClearance);
                // Nowhere may the floor of the tube sink below the ground (inside the climb the mouth offset keeps it over the level corridor).
                if (y - ground < R - 1f) ok = false;
            }
            if (pass == 0) { cruise = needed; continue; }
            if (!ok || needed > cruise + 1f) return null;
            var axis = Resample(raw, WorldScale.RouteSampleSpacing);
            float length = 0f;
            for (int i = 1; i < axis.Length; i++) length += axis[i].DistanceTo(axis[i - 1]);
            var tube = new TubeDefinition { Axis = axis, Radius = R, JoinStart = a, JoinEnd = b, Side = side, CruiseHeight = cruise, Length = length };
            var bounds = new Aabb(axis[0], Vector3.Zero);
            foreach (var p in axis) bounds = bounds.Expand(p);
            tube.Bounds = bounds.Grow(R * WorldScale.TubeMouthFlare + WorldScale.TubeCameraMargin);
            return tube;
        }
        return null;
    }

    /// <summary>Uniformly spaced copy of a polyline (the primary's offset points spread around the outside of bends).</summary>
    private static Vector3[] Resample(Vector3[] points, float spacing)
    {
        var result = new List<Vector3> { points[0] };
        float carry = 0f;
        for (int i = 1; i < points.Length; i++)
        {
            Vector3 from = points[i - 1], to = points[i];
            float seg = from.DistanceTo(to);
            float t = spacing - carry;
            while (t <= seg)
            {
                result.Add(from.Lerp(to, t / seg));
                t += spacing;
            }
            carry = seg - (t - spacing);
        }
        if (result[^1].DistanceTo(points[^1]) > spacing * 0.25f) result.Add(points[^1]);
        return result.ToArray();
    }
}
