using Godot;

namespace Rushcore.Generation;

/// <summary>
/// Route-first skeleton (04 §5A): the primary route as a chain of straights and circular bends
/// from the stage entry to the exit, always progressing along +X, wandering inside the route
/// band. Radii come from the bend ladder (D-082); the corner limit of each bend comes from the
/// route speed model so the skeleton already knows its own speed shape. Some straights are
/// reserved as feature zones long enough for a launch crest and its landing. Deterministic in the seed.
/// </summary>
public sealed class RouteSkeletonBuilder
{
    // Wander shape. Headings stay inside ±MaxHeading of +X so X is monotonic and the route
    // never doubles back; the band clamp keeps Z inside the route band.
    private const float MaxHeading = Mathf.Pi / 4f;                   // 45°
    private const float BendMin = Mathf.Pi / 9f, BendMax = 7f * Mathf.Pi / 18f;   // 20°..70°
    private const float MinUsefulBend = Mathf.Pi / 18f;               // < 10° of turn is not a bend
    private const float SoftBand = 300f;                              // beyond this the next bend turns back
    /// <summary>Straights stop here; a bend can carry the route ≤ r·(1 − cos 45°) ≈ 47 m further.</summary>
    private const float HardBand = WorldScale.RouteBandHalfWidth - 120f;
    private const float FinalRunway = 200f;
    // Feature straights (04 §5A, §5E): a launch crest (approach, crest, landing run), a mandatory gap
    // (take-off runway, opening, landing zone) or a launch ramp (approach, ramp, back face, landing zone).
    private const float CrestApproach = 150f, CrestLanding = 300f;
    /// <summary>Chance a straight at the feature spacing hosts a feature; then crest / gap / ramp by the weights below.</summary>
    private const float FeatureChance = 0.6f;
    private const float CrestWeight = 0.4f, GapWeight = 0.35f;   // the rest are ramps
    /// <summary>Chance a free bend keeps the previous turn sense: longer same-sense arcs give ridge lines room (04 §2).</summary>
    private const float TurnPersistence = 0.7f;

    private readonly RouteSpeedModel _speed;
    private readonly RouteSpeedModel? _ceiling;
    private readonly float _fullTakeoff;
    private readonly float _slamInitial, _slamAccel;

    /// <summary>With a ceiling model the feature straights also hold the ceiling flight off their crest
    /// plus the landing run (two speeds, 04 §12, D-094). <paramref name="fullChargeTakeoff"/> sizes the
    /// gap and ramp straights for the base kit's full-charge flight at the cap (D-097).</summary>
    public RouteSkeletonBuilder(RouteSpeedModel speed, RouteSpeedModel? ceiling = null, float fullChargeTakeoff = 0f, float slamInitial = 0f, float slamAccel = 0f)
    {
        _speed = speed;
        _ceiling = ceiling;
        _fullTakeoff = fullChargeTakeoff;
        _slamInitial = slamInitial;
        _slamAccel = slamAccel;
    }

    /// <summary>The ceiling ball's slam landing off a launch at the given angle, from the ceiling speed.</summary>
    private float CeilingSlamRun(float launchSlope, float drop)
    {
        if (_ceiling is null) return 0f;
        float cos = 1f / Mathf.Sqrt(1f + launchSlope * launchSlope), sin = launchSlope * cos;
        return _ceiling.SlamRange(_ceiling.Cap * cos, _ceiling.Cap * sin, WorldScale.SlamReactionSeconds, _slamInitial, _slamAccel, drop) + WorldScale.LandingRunAfterFlight;
    }

    /// <summary>Straight a gap needs past its far rim: the landing zone, or the base kit's full-charge flight
    /// at the cap plus the landing run when longer (the mandatory jump stays on a straight, 04 §10).</summary>
    private float GapLandingFor() => Mathf.Max(WorldScale.LandingZoneLength,
        Mathf.Max(_speed.JumpRange(_speed.Cap, _fullTakeoff, 0f, 0f, WorldScale.LongSwellMaxSlope) + WorldScale.LandingRunAfterFlight,
                  CeilingSlamRun(WorldScale.GapExitWallMaxSlope, 0f)));

    /// <summary>Straight a ramp needs past the foot of its back face: the same rule from the lip, over the lip's drop.</summary>
    private float RampLandingFor(float slope, float lipHeight)
    {
        float cos = 1f / Mathf.Sqrt(1f + slope * slope), sin = slope * cos;
        float flight = _speed.JumpRange(_speed.Cap * cos, _fullTakeoff, lipHeight, _speed.Cap * sin, WorldScale.LongSwellMaxSlope);
        float back = lipHeight + WorldScale.RampBackFaceEase * 0.5f;
        return Mathf.Max(WorldScale.LandingZoneLength, Mathf.Max(flight - back + WorldScale.LandingRunAfterFlight, CeilingSlamRun(slope, lipHeight) - back));
    }

    /// <summary>Landing run a crest needs: the accepted 300 m, or the ceiling flight plus its run when longer.</summary>
    private float CrestLandingFor(float wavelength, float height)
    {
        if (_ceiling is null) return CrestLanding;
        // The height field trims crests to the grade limit, so the requested height is the upper bound; the
        // swell under the crest may fall away at its steepest past the apex, so the flight is sized over that.
        float flight = _ceiling.CrestFlightLength(wavelength, height, _ceiling.Cap, WorldScale.LongSwellMaxSlope);
        return Mathf.Max(CrestLanding, flight - wavelength * 0.5f + WorldScale.LandingRunAfterFlight);
    }

    public RouteSkeleton Build(ulong routeSeed, ArchetypeRules? rules = null)
    {
        rules ??= ArchetypeRules.RollingHighlands;
        float StraightMin = rules.StraightMin, StraightMax = rules.StraightMax;
        var rng = new SeededRandom(routeSeed);
        var route = new RouteSkeleton();
        float entryX = -WorldScale.FootprintLength * 0.5f + WorldScale.EntryMargin;
        float exitX = WorldScale.FootprintLength * 0.5f - WorldScale.ExitMargin;

        var pos = new Vector2(entryX, 0f);
        float heading = 0f;
        float lastCrestX = entryX;
        float lastSign = 0f;
        AddVertex(route, pos, heading, float.PositiveInfinity, RouteSegmentKind.Straight);

        // Leave room for the closing bend (≤ r·sin 45°) and a final straight before the exit.
        float closeX = exitX - (WorldScale.CruiseBendRadius * Mathf.Sin(MaxHeading) + FinalRunway);

        while (true)
        {
            // A feature straight hosts a crest, a gap or a ramp: approach + feature + landing (04 §10: no bend in the flight).
            bool wantFeature = pos.X - lastCrestX >= WorldScale.LaunchCrestSpacing && rng.Chance(FeatureChance);
            float kindRoll = rng.NextFloat();
            RouteFeatureKind kind = kindRoll < CrestWeight ? RouteFeatureKind.LaunchCrest
                                  : kindRoll < CrestWeight + GapWeight ? RouteFeatureKind.Gap : RouteFeatureKind.LaunchRamp;
            float wavelength = rng.Range(WorldScale.LaunchCrestWavelengthMin, WorldScale.LaunchCrestWavelengthMax);
            float crestHeight = wavelength * rng.Range(0.08f, 0.12f);   // requested; the height field trims it to the grade limit
            float opening = rng.Range(WorldScale.MandatoryGapMin, WorldScale.MandatoryGapMax);
            float depth = rng.Range(WorldScale.MandatoryGapDepthMin, WorldScale.MandatoryGapDepthMax);
            float slope = rng.Chance(0.5f) ? WorldScale.RouteRampSlope : WorldScale.ModuleRampSlope;
            float rampRun = WorldScale.RampRise / slope + WorldScale.RampEase * 0.5f;
            float lipHeight = slope * rampRun;
            float featureLanding = !wantFeature ? CrestLanding
                                 : kind == RouteFeatureKind.LaunchCrest ? CrestLandingFor(wavelength, crestHeight)
                                 : kind == RouteFeatureKind.Gap ? GapLandingFor() : RampLandingFor(slope, lipHeight);
            float featureBody = kind == RouteFeatureKind.LaunchCrest ? CrestApproach + wavelength
                              : kind == RouteFeatureKind.Gap ? WorldScale.TakeoffRunwayLength + opening
                              : WorldScale.RampApproachLength + rampRun + lipHeight + WorldScale.RampBackFaceEase * 0.5f;
            float length = wantFeature ? featureBody + featureLanding : rng.Range(StraightMin, StraightMax);
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

            bool feature = wantFeature && length >= featureBody + featureLanding - 1e-3f;
            int startIndex = route.Vertices.Count - 1;
            float startDistance = route.Vertices[^1].Distance;
            pos = EmitStraight(route, pos, heading, length);
            if (feature)
            {
                var f = new RouteFeature { Kind = kind, StartIndex = startIndex, EndIndex = route.Vertices.Count - 1 };
                switch (kind)
                {
                    case RouteFeatureKind.LaunchCrest:
                        f.CentreDistance = startDistance + CrestApproach + wavelength * 0.5f;
                        f.Wavelength = wavelength; f.Height = crestHeight;
                        break;
                    case RouteFeatureKind.Gap:
                        f.CentreDistance = startDistance + WorldScale.TakeoffRunwayLength;
                        f.Opening = opening; f.Depth = depth; f.LandingDistance = featureLanding;
                        break;
                    default:
                        f.CentreDistance = startDistance + WorldScale.RampApproachLength + rampRun;
                        f.Slope = slope; f.Rise = lipHeight; f.LandingDistance = featureLanding;
                        break;
                }
                route.Features.Add(f);
                lastCrestX = pos.X;
            }
            if (pos.X >= closeX - 1e-3f) break;

            float radius = PickRadius(ref rng, rules);
            float sign = Mathf.Abs(pos.Y) > SoftBand ? -Mathf.Sign(pos.Y)
                       : Mathf.Abs(heading) >= MaxHeading - 1e-3f ? -Mathf.Sign(heading)
                       : lastSign != 0f && rng.Chance(TurnPersistence) ? lastSign
                       : rng.Sign();
            lastSign = sign;
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

    private static float PickRadius(ref SeededRandom rng, ArchetypeRules rules)
    {
        float r = rng.NextFloat();
        return r < rules.CruiseWeight ? WorldScale.CruiseBendRadius
             : r < rules.CruiseWeight + rules.FastWeight ? WorldScale.FastBendRadius
             : WorldScale.CommittedBendRadius;
    }

    private static Vector2 EmitStraight(RouteSkeleton route, Vector2 from, float heading, float length)
    {
        var dir = new Vector2(Mathf.Cos(heading), Mathf.Sin(heading));
        // Uniform spacing: a fractional last step would make the per-vertex corridor profile
        // read as a cliff over a few centimetres.
        int steps = Mathf.Max(1, Mathf.RoundToInt(length / WorldScale.RouteSampleSpacing));
        for (int i = 1; i <= steps; i++)
            AddVertex(route, from + dir * (length * i / steps), heading, float.PositiveInfinity, RouteSegmentKind.Straight);
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
        int steps = Mathf.Max(1, Mathf.RoundToInt(arcLength / WorldScale.RouteSampleSpacing));
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
