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
    // Wander shape. Headings stay inside ±MaxHeading of +X (the archetype's, 45° for the family) so X is
    // monotonic and the route never doubles back; the band clamp keeps Z inside the route band.
    private const float BendMin = Mathf.Pi / 9f, BendMax = 7f * Mathf.Pi / 18f;   // 20°..70°
    private const float MinUsefulBend = Mathf.Pi / 18f;               // < 10° of turn is not a bend
    private const float SoftBand = 300f;                              // beyond this the next bend turns back
    /// <summary>Straights stop here; a bend can carry the route ≤ r·(1 − cos 45°) ≈ 47 m further.</summary>
    private const float HardBand = WorldScale.RouteBandHalfWidth - 120f;
    private const float FinalRunway = 200f;
    // Feature straights (04 §5A, §5E): a launch crest (approach, crest, landing run), a mandatory gap
    // (take-off runway, opening, landing zone), a launch ramp (approach, ramp, back face, landing zone) or,
    // on a dune sea, a train of crests on the wave (approach, N crests, landing run). The feature spacing,
    // chance and mix are the archetype's (ArchetypeRules).
    private const float CrestApproach = 150f, CrestLanding = 300f;
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
    private float GapLandingFor(float descentAfter) => Mathf.Max(WorldScale.LandingZoneLength,
        Mathf.Max(_speed.JumpRange(_speed.Cap, _fullTakeoff, 0f, 0f, descentAfter) + WorldScale.LandingRunAfterFlight,
                  CeilingSlamRun(WorldScale.GapExitWallMaxSlope, 0f)));

    /// <summary>Straight a ramp needs past the foot of its back face: the same rule from the lip, over the lip's drop.</summary>
    private float RampLandingFor(float slope, float lipHeight, float descentAfter)
    {
        float cos = 1f / Mathf.Sqrt(1f + slope * slope), sin = slope * cos;
        float flight = _speed.JumpRange(_speed.Cap * cos, _fullTakeoff, lipHeight, _speed.Cap * sin, descentAfter);
        float back = lipHeight + WorldScale.RampBackFaceEase * 0.5f;
        return Mathf.Max(WorldScale.LandingZoneLength, Mathf.Max(flight - back + WorldScale.LandingRunAfterFlight, CeilingSlamRun(slope, lipHeight) - back));
    }

    /// <summary>Landing run a crest (or the last crest of a train) needs past its span: the accepted 300 m, the
    /// ceiling's roll-off flight, the base kit's full-charge jump from the apex at the cap (the crest's paid path,
    /// D-100) or the ceiling's slam landing off that jump, whichever runs longest, plus the landing run.</summary>
    private float CrestLandingFor(float wavelength, float height, float descentAfter, int crests = 1)
    {
        float half = wavelength * 0.5f;
        // The height field trims crests to the grade limit, so the requested height is the upper bound; the
        // swell under the crest may fall away at its steepest past the apex, so the flight is sized over that.
        float paid = _speed.JumpRange(_speed.Cap, _fullTakeoff, height, 0f, descentAfter);
        float landing = Mathf.Max(CrestLanding, paid - half + WorldScale.LandingRunAfterFlight);
        if (_ceiling is null) return landing;
        // The ceiling's roll-off is integrated over the train itself: the landing from the previous crest moves the launch point.
        float rollOff = _ceiling.CrestFlightLength(wavelength, height, _ceiling.Cap, descentAfter, crests);
        float slam = _ceiling.SlamRange(_ceiling.Cap, _fullTakeoff, WorldScale.SlamReactionSeconds, _slamInitial, _slamAccel, height);
        return Mathf.Max(landing, Mathf.Max(rollOff, slam) - half + WorldScale.LandingRunAfterFlight);
    }

    public RouteSkeleton Build(ulong routeSeed, ArchetypeRules? rules = null)
    {
        rules ??= ArchetypeRules.RollingHighlands;
        float StraightMin = rules.StraightMin, StraightMax = rules.StraightMax, MaxHeading = rules.MaxHeading;
        var rng = new SeededRandom(routeSeed);
        var route = new RouteSkeleton();
        float entryX = -WorldScale.FootprintLength * 0.5f + WorldScale.EntryMargin;
        float exitX = WorldScale.FootprintLength * 0.5f - WorldScale.ExitMargin;

        var pos = new Vector2(entryX, 0f);
        float heading = 0f;
        float lastCrestX = entryX;
        float lastSign = 0f;
        AddVertex(route, pos, heading, float.PositiveInfinity, RouteSegmentKind.Straight);

        // Dune Sea (D-099): one wave for the whole stage, drawn here so the trains sit on its crests and the
        // height field raises the same wave in the relief (gameplay structure before noise, 04 §5).
        if (rules.HasDunes)
        {
            float wavelength = rng.Range(WorldScale.DuneWavelengthMin, WorldScale.DuneWavelengthMax);
            route.Dunes = new DuneWave(wavelength, wavelength * rng.Range(WorldScale.DuneHeightRatioMin, WorldScale.DuneHeightRatioMax),
                                       rng.Range(-WorldScale.DuneWaveAngleMax, WorldScale.DuneWaveAngleMax), rng.Range(0f, Mathf.Tau));
        }

        // Leave room for the closing bend (≤ r·sin 45°) and a final straight before the exit; a spiral pit finale
        // (D-102) needs its approach S, a straight the disc cannot cross, and the disc itself instead.
        bool spiral = rules.SpiralChance > 0f && rng.Chance(rules.SpiralChance);
        float R0 = WorldScale.SpiralOuterRadius;
        float closeX = spiral ? exitX - (2f * R0 + SpiralApproach + R0 + 60f)
                              : exitX - (WorldScale.CruiseBendRadius * Mathf.Sin(MaxHeading) + FinalRunway);

        while (true)
        {
            // A feature straight hosts a crest (or a dune train), a gap or a ramp: approach + feature + landing (04 §10: no bend in the flight).
            bool wantFeature = pos.X - lastCrestX >= rules.FeatureSpacing && rng.Chance(rules.FeatureChance);
            float kindRoll = rng.NextFloat();
            RouteFeatureKind kind = kindRoll < rules.CrestWeight ? RouteFeatureKind.LaunchCrest
                                  : kindRoll < rules.CrestWeight + rules.GapWeight ? RouteFeatureKind.Gap : RouteFeatureKind.LaunchRamp;
            float wavelength = rng.Range(WorldScale.LaunchCrestWavelengthMin, WorldScale.LaunchCrestWavelengthMax);
            float crestHeight = wavelength * rng.Range(0.08f, 0.12f);   // requested; the height field trims it to the grade limit
            float opening = rng.Range(WorldScale.MandatoryGapMin, WorldScale.MandatoryGapMax);
            float depth = rng.Range(WorldScale.MandatoryGapDepthMin, WorldScale.MandatoryGapDepthMax);
            float slope = rng.Chance(0.5f) ? WorldScale.RouteRampSlope : WorldScale.ModuleRampSlope;
            float rampRun = WorldScale.RampRise / slope + WorldScale.RampEase * 0.5f;
            float lipHeight = slope * rampRun;
            // Dune train: N crests of the stage's wave, the first on the wave's first crest past the approach, the
            // straight only where it runs near the wave's travel direction (the corridor is level across).
            int trainCrests = 0;
            float trainFirst = 0f;
            if (rules.HasDunes && kind == RouteFeatureKind.LaunchCrest)
            {
                float across = Mathf.Cos(heading - route.Dunes.Angle);
                if (across >= Mathf.Cos(WorldScale.DuneTrainAlignment))
                {
                    trainCrests = rules.TrainCrestsMin + rng.Next(rules.TrainCrestsMax - rules.TrainCrestsMin + 1);
                    wavelength = route.Dunes.Wavelength / across;
                    crestHeight = route.Dunes.Height;
                    // Wave phase along this straight advances by 2π·across/λ per metre: the first crest at or past the approach.
                    float phase0 = Mathf.Tau * route.Dunes.Along(pos.X, pos.Y) / route.Dunes.Wavelength + route.Dunes.Phase;
                    float toCrest = Mathf.PosMod(-phase0, Mathf.Tau) / Mathf.Tau * wavelength;   // route distance to the next wave crest
                    float minFirst = CrestApproach + wavelength * 0.5f;
                    trainFirst = toCrest + Mathf.Ceil(Mathf.Max(0f, minFirst - toCrest) / wavelength) * wavelength;
                }
                else kind = rng.Chance(0.5f) ? RouteFeatureKind.Gap : RouteFeatureKind.LaunchRamp;
            }
            float featureLanding = !wantFeature ? CrestLanding
                                 : kind == RouteFeatureKind.LaunchCrest ? CrestLandingFor(wavelength, crestHeight, rules.SwellMaxSlope, Mathf.Max(1, trainCrests))
                                 : kind == RouteFeatureKind.Gap ? GapLandingFor(rules.SwellMaxSlope) : RampLandingFor(slope, lipHeight, rules.SwellMaxSlope);
            float featureBody = kind == RouteFeatureKind.LaunchCrest ? (trainCrests > 0 ? trainFirst + (trainCrests - 0.5f) * wavelength : CrestApproach + wavelength)
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

            // A clipped train keeps as many crests as still fit (one is a dune, not a train, but still on the wave).
            if (wantFeature && trainCrests > 0 && length < featureBody + featureLanding - 1e-3f)
            {
                trainCrests = Mathf.Min(trainCrests, Mathf.FloorToInt((length - trainFirst - featureLanding) / wavelength + 0.5f));
                if (trainCrests > 0)
                {
                    featureLanding = CrestLandingFor(wavelength, crestHeight, rules.SwellMaxSlope, trainCrests);
                    featureBody = trainFirst + (trainCrests - 0.5f) * wavelength;
                    length = featureBody + featureLanding;
                    if (length > room) { trainCrests = 0; }   // the shorter train's own landing no longer fits either
                }
            }
            bool feature = wantFeature && length >= featureBody + featureLanding - 1e-3f;
            // A feature that no longer fits (the band or the closing point clipped its straight) leaves a plain
            // straight, never a featureless run of the feature's length: nothing to chain on it (04 §12).
            if (wantFeature && !feature) length = Mathf.Min(length, StraightMax);
            int startIndex = route.Vertices.Count - 1;
            float startDistance = route.Vertices[^1].Distance;
            pos = EmitStraight(route, pos, heading, length);
            if (feature)
            {
                var f = new RouteFeature { Kind = kind, StartIndex = startIndex, EndIndex = route.Vertices.Count - 1 };
                switch (kind)
                {
                    case RouteFeatureKind.LaunchCrest:
                        f.CentreDistance = startDistance + (trainCrests > 0 ? trainFirst : CrestApproach + wavelength * 0.5f);
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
                // The rest of a train: one feature per crest, all on this straight, a wavelength apart, in route order.
                for (int c = 1; c < trainCrests; c++)
                    route.Features.Add(new RouteFeature
                    {
                        Kind = kind, StartIndex = startIndex, EndIndex = f.EndIndex,
                        CentreDistance = f.CentreDistance + c * wavelength, Wavelength = wavelength, Height = crestHeight,
                    });
                lastCrestX = pos.X;
            }
            if (pos.X >= closeX - 1e-3f) break;

            float radius = PickRadius(ref rng, rules);
            // On a dune sea a bend that has carried the route off the wave's direction turns back toward it,
            // so the next straight can ride the wave (the bends are the swales, the straights the dunes).
            float offWave = rules.HasDunes ? heading - route.Dunes.Angle : 0f;
            float sign = Mathf.Abs(pos.Y) > SoftBand ? -Mathf.Sign(pos.Y)
                       : Mathf.Abs(heading) >= MaxHeading - 1e-3f ? -Mathf.Sign(heading)
                       : Mathf.Abs(offWave) > WorldScale.DuneTrainAlignment ? -Mathf.Sign(offWave)
                       : lastSign != 0f && rng.Chance(TurnPersistence) ? lastSign
                       : rng.Sign();
            lastSign = sign;
            float turn = rng.Range(BendMin, BendMax);
            // Near the band edge the bend must end heading back toward the axis, not merely less outward.
            if (Mathf.Abs(pos.Y) > SoftBand && Mathf.Sign(heading) == -sign) turn = Mathf.Max(turn, Mathf.Abs(heading) + BendMin);
            float target = Mathf.Clamp(heading + sign * turn, -MaxHeading, MaxHeading);
            // A turn the heading limit clips below a useful bend turns the other way instead: skipping it
            // would run this straight into the next with no bend between them (a chain gap, 04 §12).
            if (Mathf.Abs(target - heading) < MinUsefulBend)
            {
                sign = -sign;
                lastSign = sign;
                target = Mathf.Clamp(heading + sign * turn, -MaxHeading, MaxHeading);
            }
            turn = Mathf.Abs(target - heading);
            if (turn < MinUsefulBend) continue;
            pos = EmitBend(route, pos, ref heading, radius, sign, turn);
            if (pos.X >= closeX) break;
        }

        // Close: bend back to +X with the cruise radius, then run straight into the exit.
        if (Mathf.Abs(heading) > 1e-3f)
            pos = EmitBend(route, pos, ref heading, WorldScale.CruiseBendRadius, -Mathf.Sign(heading), Mathf.Abs(heading));
        heading = 0f;
        if (spiral) { EmitSpiral(route, pos, ref rng); return route; }
        if (exitX - pos.X > 0f) pos = EmitStraight(route, pos, heading, exitX - pos.X);
        return route;
    }

    /// <summary>X room for the S that carries the route to the spiral's entry side (two r 200 quarter arcs) plus the straight before the disc.</summary>
    private const float SpiralApproach = 400f + WorldScale.SpiralOuterRadius;

    /// <summary>
    /// Spiral pit finale (04 §6, D-102): an S moves the route to the side of the band opposite the pit's centre, a
    /// straight as long as the outer radius keeps the disc behind the entry clear of the route, then one full turn
    /// of quarter arcs with shrinking radius descends into the pit; the route ends on the pit floor. Headings run
    /// unbounded through the turn (D-096): only the footprint constrains the plan.
    /// </summary>
    private void EmitSpiral(RouteSkeleton route, Vector2 pos, ref SeededRandom rng)
    {
        float R0 = WorldScale.SpiralOuterRadius;
        float heading = 0f;
        // The centre sits R0 to the side of the entry; keep it within ~100 m of the axis so the disc stays inside the band.
        float s = pos.Y >= 0f ? -1f : 1f;                        // turn toward the axis
        float targetZ = -s * (R0 - 100f);                        // entry side: the centre (target + s·R0) lands 100 m past the axis
        float dz = targetZ - pos.Y;
        if (Mathf.Abs(dz) > 1f)
        {
            // An S of two equal arcs: lateral = 2r(1 − cos θ); r grows with the move so θ never exceeds 90°.
            float r = Mathf.Max(WorldScale.CruiseBendRadius, Mathf.Abs(dz) * 0.5f);
            float theta = Mathf.Acos(Mathf.Clamp(1f - Mathf.Abs(dz) / (2f * r), -1f, 1f));
            float sgn = Mathf.Sign(dz);
            pos = EmitBend(route, pos, ref heading, r, sgn, theta);
            pos = EmitBend(route, pos, ref heading, r, -sgn, theta);
            heading = 0f;
        }
        float approachDistance = route.Vertices[^1].Distance;
        pos = EmitStraight(route, pos, heading, R0);
        var pit = new SpiralPit
        {
            ApproachDistance = approachDistance,
            Centre = new Vector3(pos.X, 0f, pos.Y + s * R0),
            OuterRadius = R0,
            StartIndex = route.Vertices.Count - 1,
            StartDistance = route.Vertices[^1].Distance,
            Depth = WorldScale.SpiralDepth,
        };
        // Quarter arcs of shrinking radius: each arc starts where the last ended, so the shrink accumulates over the
        // arcs and the turn's radial spacing is the whole shed by the last quarter (two level widths, two setbacks
        // and the cliff face between a turn and the one below it, docs/11 §7c).
        int quarters = Mathf.Max(2, Mathf.RoundToInt(WorldScale.SpiralTurns * 4f));
        float r0 = R0, dr = WorldScale.SpiralTurns * WorldScale.SpiralRadiusPerTurn / (quarters - 1);
        for (int q = 0; q < quarters; q++)
        {
            float radius = r0 - dr * q;
            pos = EmitBend(route, pos, ref heading, radius, s, Mathf.Pi / 2f);
        }
        pit.InnerRadius = r0 - dr * (quarters - 1);
        pit.EndIndex = route.Vertices.Count - 1;
        pit.Length = route.Vertices[^1].Distance - pit.StartDistance;
        route.Spiral = pit;
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
