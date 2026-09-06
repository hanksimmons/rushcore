using Godot;

namespace Rushcore.Generation;

/// <summary>
/// Route-first skeleton (04 §5A): the primary route as a chain of straights and circular bends
/// from the stage entry to the exit, always progressing along +X, wandering inside the route
/// band. Radii come from the bend ladder (D-082); the corner limit of each bend comes from the
/// route speed model so the skeleton already knows its own speed shape. Deterministic in the seed.
/// </summary>
public sealed class RouteSkeletonBuilder
{
    // Wander shape. Headings stay inside ±MaxHeading of +X so X is monotonic and the route
    // never doubles back; the band clamp keeps Z inside the route band.
    private const float MaxHeading = Mathf.Pi / 4f;                   // 45°
    private const float StraightMin = 250f, StraightMax = 600f;
    private const float BendMin = Mathf.Pi / 9f, BendMax = 7f * Mathf.Pi / 18f;   // 20°..70°
    private const float MinUsefulBend = Mathf.Pi / 18f;               // < 10° of turn is not a bend
    private const float SoftBand = 300f;                              // beyond this the next bend turns back
    /// <summary>Straights stop here; a bend can carry the route ≤ r·(1 − cos 45°) ≈ 47 m further.</summary>
    private const float HardBand = WorldScale.RouteBandHalfWidth - 120f;
    private const float FinalRunway = 200f;

    private readonly RouteSpeedModel _speed;

    public RouteSkeletonBuilder(RouteSpeedModel speed) => _speed = speed;

    public RouteSkeleton Build(ulong routeSeed)
    {
        var rng = new SeededRandom(routeSeed);
        var route = new RouteSkeleton();
        float entryX = -WorldScale.FootprintLength * 0.5f + WorldScale.EntryMargin;
        float exitX = WorldScale.FootprintLength * 0.5f - WorldScale.ExitMargin;

        var pos = new Vector2(entryX, 0f);
        float heading = 0f;
        AddVertex(route, pos, heading, float.PositiveInfinity, RouteSegmentKind.Straight);

        // Leave room for the closing bend (≤ r·sin 45°) and a final straight before the exit.
        float closeX = exitX - (WorldScale.CruiseBendRadius * Mathf.Sin(MaxHeading) + FinalRunway);

        while (true)
        {
            float length = rng.Range(StraightMin, StraightMax);
            float sin = Mathf.Sin(heading);
            if (Mathf.Abs(sin) > 1e-4f)
            {
                // Shorten a straight that would leave the band; the next bend turns back.
                float limit = (HardBand * Mathf.Sign(sin) - pos.Y) / sin;
                if (limit < length) length = Mathf.Max(StraightMin * 0.5f, limit);
            }
            // Never run past the closing point: the closing bend and the exit runway need the room.
            float room = (closeX - pos.X) / Mathf.Cos(heading);
            if (room <= WorldScale.RouteSampleSpacing) break;
            if (length > room) length = room;
            pos = EmitStraight(route, pos, heading, length);
            if (pos.X >= closeX - 1e-3f) break;

            float radius = PickRadius(ref rng);
            float sign = Mathf.Abs(pos.Y) > SoftBand ? -Mathf.Sign(pos.Y)
                       : Mathf.Abs(heading) >= MaxHeading - 1e-3f ? -Mathf.Sign(heading)
                       : rng.Sign();
            float turn = rng.Range(BendMin, BendMax);
            // Near the band edge the bend must end heading back toward the axis, not merely less outward.
            if (Mathf.Abs(pos.Y) > SoftBand && Mathf.Sign(heading) == -sign) turn = Mathf.Max(turn, Mathf.Abs(heading) + BendMin);
            float target = Mathf.Clamp(heading + sign * turn, -MaxHeading, MaxHeading);
            turn = Mathf.Abs(target - heading);
            if (turn < MinUsefulBend) continue;
            pos = EmitBend(route, pos, ref heading, radius, sign, turn);
            if (pos.X >= closeX) break;
        }

        // Close: bend back to +X with the cruise radius, then run straight into the exit.
        if (Mathf.Abs(heading) > 1e-3f)
            pos = EmitBend(route, pos, ref heading, WorldScale.CruiseBendRadius, -Mathf.Sign(heading), Mathf.Abs(heading));
        heading = 0f;
        if (exitX - pos.X > 0f) pos = EmitStraight(route, pos, heading, exitX - pos.X);
        return route;
    }

    private static float PickRadius(ref SeededRandom rng)
    {
        float r = rng.NextFloat();
        return r < 0.50f ? WorldScale.CruiseBendRadius
             : r < 0.85f ? WorldScale.FastBendRadius
             : WorldScale.CommittedBendRadius;
    }

    private static Vector2 EmitStraight(RouteSkeleton route, Vector2 from, float heading, float length)
    {
        var dir = new Vector2(Mathf.Cos(heading), Mathf.Sin(heading));
        int steps = Mathf.Max(1, Mathf.CeilToInt(length / WorldScale.RouteSampleSpacing));
        for (int i = 1; i <= steps; i++)
        {
            float d = Mathf.Min(length, i * WorldScale.RouteSampleSpacing);
            AddVertex(route, from + dir * d, heading, float.PositiveInfinity, RouteSegmentKind.Straight);
        }
        return from + dir * length;
    }

    /// <summary>Circular arc of the given radius turning by <paramref name="turn"/> radians toward +Z (sign +1) or −Z (−1).</summary>
    private Vector2 EmitBend(RouteSkeleton route, Vector2 from, ref float heading, float radius, float sign, float turn)
    {
        var bend = new RouteBend
        {
            StartIndex = route.Vertices.Count - 1,
            Radius = radius,
            TurnAngle = sign * turn,
            CornerLimit = _speed.CornerSpeedLimit(radius),
        };
        Vector2 Left(float h) => new(-Mathf.Sin(h), Mathf.Cos(h));
        Vector2 centre = from + Left(heading) * radius * sign;
        float arcLength = radius * turn;
        int steps = Mathf.Max(1, Mathf.CeilToInt(arcLength / WorldScale.RouteSampleSpacing));
        Vector2 last = from;
        for (int i = 1; i <= steps; i++)
        {
            float a = turn * Mathf.Min(1f, i / (float)steps);
            float h = heading + sign * a;
            last = centre - Left(h) * radius * sign;
            AddVertex(route, last, h, radius, RouteSegmentKind.Bend);
        }
        heading += sign * turn;
        bend.EndIndex = route.Vertices.Count - 1;
        route.Bends.Add(bend);
        return last;
    }

    private static void AddVertex(RouteSkeleton route, Vector2 xz, float heading, float radius, RouteSegmentKind kind)
    {
        var p = new Vector3(xz.X, 0f, xz.Y);
        float dist = route.Vertices.Count == 0 ? 0f : route.Vertices[^1].Distance + route.Vertices[^1].Position.DistanceTo(p);
        route.Vertices.Add(new RouteVertex { Position = p, Heading = heading, Radius = radius, Distance = dist, Kind = kind });
    }
}
