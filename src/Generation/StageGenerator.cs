using System.Diagnostics;
using Godot;
using Rushcore.Tuning;

namespace Rushcore.Generation;

/// <summary>
/// The generation pipeline (04 §5) as far as it is built: A (route skeleton) and H (validate,
/// bounded regeneration, known-safe fallback). Pure data; no SceneTree. Later slices add the
/// archetype relief, corridor stamping, optional lines and checkpoints between A and H.
/// </summary>
public sealed class StageGenerator
{
    public const int MaxAttempts = 4;

    private readonly RouteSpeedModel _speed;
    private readonly RouteSpeedModel _ceiling;
    private readonly RouteSkeletonBuilder _routes;
    private readonly FlowTuning _flow;
    private readonly float _halfTakeoff, _fullTakeoff, _ballRadius, _slamInitial, _slamAccel;

    /// <summary>Two speeds (04 §12, D-094): the base kit decides crossability and time, the Flow ceiling
    /// (base cap × (1 + headroom)) decides safety. The jump tuning sizes the mandatory-jump envelope
    /// (04 §11, D-097). All read the frozen tuning; nothing is duplicated.</summary>
    public StageGenerator(MovementTuning movement, FlowTuning flow, JumpSlamTuning jump)
    {
        _speed = new RouteSpeedModel(movement);
        _ceiling = new RouteSpeedModel(movement, flow.Headroom);
        _halfTakeoff = Mathf.Lerp(jump.MinJumpTakeoffVerticalSpeed, jump.MaxJumpTakeoffVerticalSpeed, 0.5f);
        _fullTakeoff = jump.MaxJumpTakeoffVerticalSpeed;
        _ballRadius = movement.BallRadius;
        _slamInitial = jump.SlamInitialDownwardSpeed;
        _slamAccel = jump.SlamDownwardAcceleration;
        _routes = new RouteSkeletonBuilder(_speed, _ceiling, _fullTakeoff, _slamInitial, _slamAccel);
        _gravity = movement.Gravity;
        _flow = flow;
    }

    public RouteSpeedModel SpeedModel => _speed;
    public RouteSpeedModel CeilingModel => _ceiling;

    public StageDefinition Generate(StageGenerationRequest request)
    {
        StageDefinition? last = null;
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            last = Build(request, request.RouteSeed(attempt), attempt + 1);
            if (last.Report.Passed) return last;
        }

        // Known-safe fallback (04 §5H): the straight route down the axis, flagged as such.
        var fallback = Build(request, SeedChain.Derive(request.StageSeed, "fallback"), MaxAttempts, straight: true);
        fallback.Report.UsedFallback = true;
        foreach (var f in last!.Report.Failures)
            fallback.Report.Add("regeneration exhausted: " + f.Name, fallback.Report.Passed, f.Detail);
        return fallback;
    }

    /// <summary>One attempt without regeneration or fallback: seed tooling and tests inspect failures with it.</summary>
    public StageDefinition BuildAttempt(StageGenerationRequest request, int attempt) => Build(request, request.RouteSeed(attempt), attempt + 1);

    private StageDefinition Build(StageGenerationRequest request, ulong routeSeed, int attempt, bool straight = false)
    {
        var report = new ValidationReport { Attempts = attempt };
        var sw = Stopwatch.StartNew();

        var rules = ArchetypeRules.For(request.Archetype);
        RouteSkeleton route = straight ? StraightRoute() : _routes.Build(routeSeed, rules);
        report.Timings.Add(("route skeleton", sw.Elapsed.TotalMilliseconds)); sw.Restart();

        // B–D: archetype relief and the stamped corridor become the one logical height source;
        // the route polyline is then lifted onto it so every later check sees real geometry.
        var field = new StageHeightField(route, request.StageSeed, rules);
        var optional = straight ? new List<RouteSkeleton>() : OptionalLineBuilder.Build(route, request.StageSeed, rules);
        foreach (var line in optional) field.AddLine(line);
        Lift(route, field);
        foreach (var line in optional) Lift(line, field);
        report.Timings.Add(("relief + corridors", sw.Elapsed.TotalMilliseconds)); sw.Restart();

        RouteSpeedProfile profile = _speed.Integrate(route.Polyline());
        // The ceiling profile: a standing start, then a full chain held wherever the bends allow (04 §12).
        RouteSpeedProfile ceiling = _ceiling.Integrate(route.Polyline(), chainFromBaseCap: true);
        // An optional line whose base-kit flight cannot hold the corner it lands into is dropped, not the stage
        // (D-100): the lines are optional, and the field is rebuilt once without it. Ridge lines are spaced, so
        // the remaining lines' geometry does not change.
        var dropped = new List<string>();
        // A terrace whose drain fails goes too, with the other floor of its section (they share it).
        var badSections = new HashSet<int>();
        foreach (var line in optional)
            if (line.Kind == RouteLineKind.Terrace && !TerraceDrains(line, field, route, optional).ok) badSections.Add(line.JoinStart);
        for (int k = optional.Count - 1; k >= 0; k--)
            if (optional[k].Kind == RouteLineKind.Terrace && badSections.Contains(optional[k].JoinStart))
            {
                dropped.Add($"floor {optional[k].Floor} at {route.Vertices[optional[k].JoinStart].Distance:0} m: {TerraceDrains(optional[k], field, route, optional).detail}");
                optional.RemoveAt(k);
            }
        for (int k = optional.Count - 1; k >= 0; k--)
        {
            var line = optional[k];
            var baseProfile = _speed.Integrate(line.Polyline(), profile.Speed[line.JoinStart]);
            var verdict = FlightsHoldCorners(baseProfile, line.Polyline(), _speed);
            if (!verdict.ok)
            {
                string trace = "";
                if (System.Environment.GetEnvironmentVariable("RUSHCORE_LINE_TRACE") == "1" && baseProfile.Flights.Count > 0)
                {
                    float at = baseProfile.Flights[0].LaunchDistance;
                    var lv = line.Vertices;
                    for (int i = 0; i < lv.Count; i++) if (Mathf.Abs(lv[i].Distance - at) <= 40f) trace += $" {lv[i].Distance:0}:{lv[i].Position.Y:0.00}";
                }
                dropped.Add($"line {k + 1} at {route.Vertices[line.JoinStart].Distance:0} m: {verdict.detail}{trace}"); optional.RemoveAt(k);
            }
        }
        if (dropped.Count > 0)
        {
            field = new StageHeightField(route, request.StageSeed, rules);
            foreach (var line in optional) field.AddLine(line);
            Lift(route, field);
            foreach (var line in optional) Lift(line, field);
            profile = _speed.Integrate(route.Polyline());
            ceiling = _ceiling.Integrate(route.Polyline(), chainFromBaseCap: true);
        }
        var def = new StageDefinition(request, route, profile, report) { RouteSeedUsed = routeSeed, HeightField = field, CeilingProfile = ceiling };
        foreach (var line in optional)
        {
            def.OptionalLines.Add(line);
            def.OptionalProfiles.Add(_speed.Integrate(line.Polyline(), profile.Speed[line.JoinStart]));
            def.OptionalCeilingProfiles.Add(_ceiling.Integrate(line.Polyline(), ceiling.Speed[line.JoinStart], chainFromBaseCap: true));
        }
        def.DroppedLines = dropped.Count;
        def.DroppedDetail = string.Join("; ", dropped);
        // Tubes (04 §5I, D-101) branch off the lifted primary; their carried profiles start at the join's arrival speed.
        if (!straight)
            foreach (var tube in TubeBuilder.Build(route, field, request.StageSeed, rules, optional))
            {
                tube.Profile = _speed.Integrate(tube.Axis, profile.Speed[tube.JoinStart], carried: true);
                tube.CeilingProfile = _ceiling.Integrate(tube.Axis, ceiling.Speed[tube.JoinStart], chainFromBaseCap: true, carried: true);
                def.Tubes.Add(tube);
            }
        if (!straight) def.Lids.AddRange(LidBuilder.Build(route, request.StageSeed, rules, def.Tubes, optional));
        // Exits (02 §4, D-105): the primary's pad is A; each terminal line's pad follows in route order of its fork.
        def.Exits.Add(new StageExit(0, "A", route.Exit, route.Vertices[^1].Heading, -1));
        foreach (int k in Enumerable.Range(0, optional.Count).Where(k => optional[k].Terminal).OrderBy(k => optional[k].JoinStart))
        {
            var e = optional[k].Vertices[^1];
            def.Exits.Add(new StageExit(def.Exits.Count, ((char)('A' + def.Exits.Count)).ToString(), e.Position, e.Heading, k));
        }
        report.Timings.Add(("speed profiles", sw.Elapsed.TotalMilliseconds)); sw.Restart();

        PlaceCheckpoints(def);
        report.Timings.Add(("checkpoints", sw.Elapsed.TotalMilliseconds)); sw.Restart();

        Validate(route, profile, ceiling, field, report);
        def.WidestFlowGap = ValidateTwoSpeeds(route, profile, ceiling, report);
        ValidateModules(def, report);
        ValidateOptionalLines(def, report);
        ValidateTubes(def, report);
        ValidateStructuresAndHeadroom(def, report);
        ValidateCheckpoints(def, report);
        ValidateExits(def, report);
        report.Timings.Add(("validation", sw.Elapsed.TotalMilliseconds));
        return def;
    }

    /// <summary>Puts a line's vertices on the stamped surface so every later check sees real geometry.</summary>
    private static void Lift(RouteSkeleton line, StageHeightField field)
    {
        for (int i = 0; i < line.Vertices.Count; i++)
        {
            var v = line.Vertices[i];
            v.Position.Y = field.Sample(v.Position.X, v.Position.Z);
            line.Vertices[i] = v;
        }
    }

    /// <summary>
    /// Recovery anchors (04 §13): every checkpoint spacing along the primary, pushed past any launch
    /// crest and its landing run so a restore is never placed just before or inside a flight.
    /// </summary>
    private static void PlaceCheckpoints(StageDefinition def)
    {
        var route = def.PrimaryRoute;
        var v = route.Vertices;
        float ballRadius = 2.125f;   // restore height uses the accepted ball radius (03 §15); the runtime re-floors it
        float lastPlaced = float.NegativeInfinity;
        for (float d = WorldScale.CheckpointSpacing; d < route.Length - WorldScale.PadRadius * 2f; d += WorldScale.CheckpointSpacing)
        {
            float place = d;
            foreach (var f in route.Features)
            {
                // A module owns its whole straight; a crest owns its own span, so the approach before it and the
                // troughs of a dune train (a restore at rest between two crests, D-099) keep their anchors.
                float fs = f.Kind == RouteFeatureKind.LaunchCrest ? f.CentreDistance - f.Wavelength * 0.5f + 40f : v[f.StartIndex].Distance;
                float fe = f.ReservedEnd;
                if (place >= fs && place <= fe) place = fe + 20f;
            }
            if (place >= route.Length - WorldScale.PadRadius * 2f) break;
            int i = route.IndexAtDistance(place);
            // Level ground across the corridor: never anchor on a banked bend; step past it.
            foreach (var b in route.Bends)
                if (i >= b.StartIndex && i <= b.EndIndex) { i = Mathf.Min(v.Count - 1, b.EndIndex + 4); break; }
            if (v[i].Distance >= route.Length - WorldScale.PadRadius * 2f) break;
            // Every spacing pushed past one long feature straight would land on the same vertex: one anchor there.
            if (v[i].Distance < lastPlaced + WorldScale.CheckpointSpacing * 0.5f) continue;
            lastPlaced = v[i].Distance;
            def.Checkpoints.Add(new Checkpoint
            {
                Position = v[i].Position + Vector3.Up * (ballRadius + 0.6f),
                Heading = v[i].Heading,
                PrimaryIndex = i,
                Distance = v[i].Distance,
            });
        }
    }

    private void ValidateOptionalLines(StageDefinition def, ValidationReport report)
    {
        var primary = def.PrimaryRoute.Vertices;
        bool allOk = true;
        string detail = "";
        for (int k = 0; k < def.OptionalLines.Count; k++)
        {
            var line = def.OptionalLines[k];
            var prof = def.OptionalProfiles[k];
            var v = line.Vertices;
            float maxGrade = 0f, rise = 0f;
            for (int i = 1; i < v.Count; i++)
            {
                float ds = v[i].Distance - v[i - 1].Distance;
                if (ds > 1e-3f) maxGrade = Mathf.Max(maxGrade, Mathf.Abs(v[i].Position.Y - v[i - 1].Position.Y) / ds);
                int pi = Mathf.Clamp(line.JoinStart + i, 0, primary.Count - 1);
                rise = Mathf.Max(rise, v[i].Position.Y - primary[pi].Position.Y);
            }
            float joinStart = Mathf.Abs(v[0].Position.Y - primary[line.JoinStart].Position.Y);
            float joinEnd = Mathf.Abs(v[^1].Position.Y - primary[line.JoinEnd].Position.Y);
            // A terminal line (D-105) ends on its own pad: level over the pad's reach instead of meeting the primary.
            float padGrade = 0f;
            if (line.Terminal)
                for (int i = v.Count - 1; i > 0 && v[^1].Distance - v[i - 1].Distance <= WorldScale.PadRadius; i--)
                    padGrade = Mathf.Max(padGrade, Mathf.Abs(v[i].Position.Y - v[i - 1].Position.Y) / Mathf.Max(1e-3f, v[i].Distance - v[i - 1].Distance));
            bool ends = line.Terminal ? padGrade <= 0.03f : joinEnd < 2f;
            bool ok = !prof.Stalled && maxGrade <= WorldScale.MaxRouteGrade && rise >= 15f && joinStart < 2f && ends;
            allOk &= ok;
            detail += $" [{line.Kind}{(line.Terminal ? " → exit" : "")} {v[0].Distance:0}→{v[^1].Distance:0} m of {line.Length:0}, rise {rise:0} m, grade {maxGrade:0.00}, exit {prof.Speed[^1]:0} m/s{(ok ? "" : " FAIL")}]";
        }
        report.Add("optional lines are traversable, joined and distinct", allOk, $"{def.OptionalLines.Count} lines{detail}");
        // Flights on an optional line are judged on its own geometry (it has no bend list): no flight drifts through a
        // turn, and a landing holds the corner limits inside its run. The base kit is enforced (a failing line was
        // dropped before this report); the ceiling is reported, as a module's no-slam flight is (04 §5E, D-100).
        string ceilingDetail = ""; int ceilingFaults = 0;
        for (int k = 0; k < def.OptionalLines.Count; k++)
        {
            var c = FlightsHoldCorners(def.OptionalCeilingProfiles[k], def.OptionalLines[k].Polyline(), _ceiling);
            if (!c.ok) ceilingFaults++;
            ceilingDetail += $" [line {k + 1}: {c.detail}]";
        }
        report.Add("optional lines' flights hold their corners at the base kit (a line that cannot is dropped)", true, $"{def.DroppedLines} dropped{(def.DroppedLines > 0 ? ": " + def.DroppedDetail : "")}");

        // Drains (04 §10, D-103): reported here; a terrace whose drain fails was dropped before this report, like a line
        // whose flight cannot hold a corner (D-100).
        var field = def.HeightField!;
        string drainDetail = ""; int terraces = 0;
        for (int k = 0; k < def.OptionalLines.Count; k++)
        {
            var line = def.OptionalLines[k];
            if (line.Kind != RouteLineKind.Terrace) continue;
            terraces++;
            var verdict = TerraceDrains(line, field, def.PrimaryRoute, def.OptionalLines);
            drainDetail += $" [{verdict.detail}]";
        }
        report.Add("terrace edges drain: every cliff foot lands on the floor below and the outer slopes stay inside the route grade (04 §10)", true, $"{terraces} terraces{drainDetail}");
        report.Note("optional lines at the ceiling", $"{ceilingFaults} of {def.OptionalLines.Count} lines fly into a corner they cannot hold (the paid line's risk){ceilingDetail}");
    }

    /// <summary>
    /// Terrace drains (04 §10, D-103): the inner edge is a cliff whose foot lands on the floor below (the primary's flank
    /// or floor 2), at most a few metres above that floor's profile; from the foot to the floor below the ground never
    /// steps up more than relief noise; the outer edge (when it is not the cliff up to floor 3) descends into the relief
    /// at or under the route grade. Read on lateral cuts every 100 m of plateau.
    /// </summary>
    private static (bool ok, string detail) TerraceDrains(RouteSkeleton line, StageHeightField field, RouteSkeleton primaryRoute, IReadOnlyList<RouteSkeleton> lines)
    {
        var primary = primaryRoute.Vertices;
        var lv = line.Vertices;
        float worstFoot = 0f, worstOut = 0f, riseIn = 0f;
        RouteSkeleton? below = line.Floor == 3 ? lines.FirstOrDefault(o => o.Floor == 2 && o.JoinStart == line.JoinStart) : null;
        for (int i = 0; i < lv.Count; i += 25)
        {
            float d = lv[i].Distance;
            if (d < line.Transition + line.RampLength || d > line.Length - (line.Terminal ? WorldScale.PadRadius * 2.5f : line.Transition + line.RampLength)) continue;
            float lx = -Mathf.Sin(lv[i].Heading), lz = Mathf.Cos(lv[i].Heading);
            float inward = -line.Side;
            float edge = line.CorridorHalfWidth + WorldScale.WallSetback + WorldScale.TerraceCliffFalloff + 6f;
            float floorY = below is not null ? below.Vertices[Mathf.Min(below.Vertices.Count - 1, i)].Position.Y : primary[Mathf.Min(primary.Count - 1, line.JoinStart + i)].Position.Y;
            float foot = field.Sample(lv[i].Position.X + lx * inward * edge, lv[i].Position.Z + lz * inward * edge);
            worstFoot = Mathf.Max(worstFoot, foot - floorY);
            float prev = foot;
            float reach = below is not null ? line.Offset - below.Offset - edge : line.Offset - edge;
            for (float s2 = edge + 4f; s2 <= reach; s2 += 4f)
            {
                float h = field.Sample(lv[i].Position.X + lx * inward * s2, lv[i].Position.Z + lz * inward * s2);
                riseIn = Mathf.Max(riseIn, h - prev);
                prev = h;
            }
            if (line.OuterFalloff >= WorldScale.TerraceCliffFalloff + 1f)
            {
                float oe = line.CorridorHalfWidth + WorldScale.WallSetback;
                prev = field.Sample(lv[i].Position.X - lx * inward * oe, lv[i].Position.Z - lz * inward * oe);
                for (float s2 = oe + 4f; s2 <= oe + line.OuterFalloff + 40f; s2 += 4f)
                {
                    float h = field.Sample(lv[i].Position.X - lx * inward * s2, lv[i].Position.Z - lz * inward * s2);
                    worstOut = Mathf.Max(worstOut, (prev - h) / 4f);
                    prev = h;
                }
            }
        }
        bool ok = worstFoot <= 15f && riseIn <= 4f && worstOut <= WorldScale.MaxRouteGrade + 0.02f;
        return (ok, $"floor {line.Floor} at {primary[line.JoinStart].Distance:0} m, {line.RidgeHeight:0} m up: cliff foot at most {worstFoot:0.0} m above the floor below, step up {riseIn:0.0} m on the way down, outer grade ≤ {worstOut:0.00}{(ok ? "" : " FAIL")}");
    }

    /// <summary>
    /// Tubes (04 §5I, §10, §12; D-101): the axis keeps the camera clearance above the ground along the cruise and never
    /// dips below its own radius over the ground; a ground mouth sits inside the line's level width and the exit lands
    /// on a straight (by construction, reported); the carried profile never stalls; the graph stays acyclic (every tube
    /// rejoins further along its line). The steepest wall ride the base kit asks of the tube is reported (V-015).
    /// </summary>
    private void ValidateTubes(StageDefinition def, ValidationReport report)
    {
        var field = def.HeightField!;
        var v = def.PrimaryRoute.Vertices;
        bool allOk = true, acyclic = true;
        string detail = "";
        foreach (var t in def.Tubes)
        {
            float worstCruise = float.MaxValue, worstAny = float.MaxValue;
            float span = v[t.JoinEnd].Distance - v[t.JoinStart].Distance, ramp = TubeBuilder.RampLength(t.CruiseHeight);
            float along = 0f;
            for (int i = 0; i < t.Axis.Length; i++)
            {
                if (i > 0) along += t.Axis[i].DistanceTo(t.Axis[i - 1]);
                float clearance = t.Axis[i].Y - field.Sample(t.Axis[i].X, t.Axis[i].Z);
                worstAny = Mathf.Min(worstAny, clearance);
                // The envelope runs in the primary's distance; the axis is a little longer, so read it by fraction.
                if (TubeBuilder.HeightEnvelope(along / Mathf.Max(1f, t.Length) * span, span, ramp) > 0.9f) worstCruise = Mathf.Min(worstCruise, clearance);
            }
            // Wall ride (docs/11 §7d): tan φ = v² κ / g with the axis's plan curvature over ±3 points.
            float ride = 0f;
            var p = t.Profile!;
            for (int i = 3; i + 3 < t.Axis.Length; i++)
            {
                Vector2 a = new(t.Axis[i - 3].X, t.Axis[i - 3].Z), b = new(t.Axis[i].X, t.Axis[i].Z), c = new(t.Axis[i + 3].X, t.Axis[i + 3].Z);
                Vector2 ab = b - a, bc = c - b;
                if (ab.LengthSquared() < 1e-6f || bc.LengthSquared() < 1e-6f) continue;
                float turn = Mathf.Abs(ab.AngleTo(bc)), len = 0.5f * (ab.Length() + bc.Length());
                float kappa = turn / Mathf.Max(1e-3f, len);
                float speed = p.Speed[Mathf.Min(i, p.Count - 1)];
                ride = Mathf.Max(ride, Mathf.RadToDeg(Mathf.Atan(speed * speed * kappa / _gravity)));
            }
            t.MaxRideDegrees = ride;
            float mouthEdge = WorldScale.TubeMouthOffset + t.Radius * WorldScale.TubeMouthFlare;
            bool mouthsOk = mouthEdge <= StageHeightField.CorridorHalfWidth + WorldScale.WallSetback;
            // The builder guaranteed the clearance on its own samples; the resampled axis reads it within a few metres.
            bool clearOk = worstCruise >= t.Radius + WorldScale.TubeClearance - 5f && worstAny >= t.Radius - 3f;
            bool profileOk = !p.Stalled;
            if (t.JoinEnd <= t.JoinStart) acyclic = false;
            t.Passed = mouthsOk && clearOk && profileOk;
            allOk &= t.Passed;
            t.Detail = $"{t.Length:0} m from {v[t.JoinStart].Distance:0} m, cruise {t.CruiseHeight:0} m up, clearance ≥ {worstCruise:0} m (min {worstAny:0.0}), " +
                       $"entry {p.Speed[0]:0} → exit {p.Speed[^1]:0} m/s (ceiling {t.CeilingProfile!.Speed[^1]:0}), wall ride ≤ {ride:0}°";
            detail += $" [{t.Detail}{(t.Passed ? "" : clearOk ? mouthsOk ? " STALLS" : " MOUTH OUTSIDE THE LEVEL WIDTH" : " AXIS TOO LOW")}]";
        }
        report.Add("tube axes keep the camera clearance above the ground and their mouths sit on the line (04 §10)", allOk, $"{def.Tubes.Count} tubes{detail}");
        report.Add("the line graph is acyclic in route distance (every tube and line rejoins further along)", acyclic && def.OptionalLines.All(l => l.JoinEnd > l.JoinStart));
    }

    /// <summary>
    /// Lids and headroom (04 §10, D-102): every lid keeps the family's clearance over the corridor under it and spans the
    /// slot; nothing else stands within the full-charge apex above the primary's centreline (a tube axis near the
    /// centreline below the apex would be an undeclared ceiling), so only lids declare a ceiling.
    /// </summary>
    private void ValidateStructuresAndHeadroom(StageDefinition def, ValidationReport report)
    {
        var v = def.PrimaryRoute.Vertices;
        bool lidsOk = true; string lidDetail = "";
        foreach (var lid in def.Lids)
        {
            float clearance = float.MaxValue;
            for (int i = lid.StartIndex; i <= lid.EndIndex; i++) clearance = Mathf.Min(clearance, lid.RoofBottom - v[i].Position.Y);
            lid.Clearance = clearance;
            lid.Passed = clearance >= WorldScale.LidClearance - 0.5f && lid.Width >= WorldScale.MinCorridorWidth;
            lidsOk &= lid.Passed;
            lid.Detail = $"{lid.Length:0} m from {v[lid.StartIndex].Distance:0} m, roof {lid.RoofBottom:0} m, clearance {clearance:0.0} m, {lid.Width:0} m wide";
            lidDetail += $" [{lid.Detail}{(lid.Passed ? "" : " LOW")}]";
        }
        report.Add("lids keep the declared clearance over the corridor and span the slot (04 §10)", lidsOk, $"{def.Lids.Count} lids{lidDetail}");

        float apex = _fullTakeoff * _fullTakeoff / (2f * _gravity) + 2f * _ballRadius;
        float worst = float.MaxValue; string where = "";
        foreach (var t in def.Tubes)
            foreach (var a in t.Axis)
            {
                int i = def.PrimaryRoute.IndexAtDistance(0f);   // nearest by plan distance over the tube's join span
                float best = float.MaxValue; int bi = t.JoinStart;
                for (int k = t.JoinStart; k <= t.JoinEnd; k += 2)
                {
                    float d = new Vector2(v[k].Position.X - a.X, v[k].Position.Z - a.Z).LengthSquared();
                    if (d < best) { best = d; bi = k; }
                }
                float lateral = Mathf.Sqrt(best), above = a.Y - t.Radius - v[bi].Position.Y;
                if (lateral < 20f + t.Radius && above < apex && above < worst) { worst = above; where = $"tube (joins {v[t.JoinStart].Distance:0}–{v[t.JoinEnd].Distance:0} m, side {t.Side:+0;-0}, cruise {t.CruiseHeight:0}) axis {above:0} m above the centreline, {lateral:0.0} m off it in plan, at {v[bi].Distance:0} m; axis ({a.X:0},{a.Y:0},{a.Z:0}) vertex ({v[bi].Position.X:0},{v[bi].Position.Y:0},{v[bi].Position.Z:0}) heading {Mathf.RadToDeg(v[bi].Heading):0}°"; }
            }
        report.Add("headroom: nothing but a declared lid stands within the jump apex above the primary's centreline (04 §10)", worst == float.MaxValue,
            worst == float.MaxValue ? $"apex {apex:0} m clear; {def.Lids.Count} declared ceilings" : where);
    }

    /// <summary>Geometric flight check for any polyline (D-100): a flight's straight path may not drift more than the
    /// tolerance off a path that turns under it (drift ≈ length × turn / 2), and the corner limits inside the landing
    /// run must hold the landing speed after what the brake sheds over the run.</summary>
    private static (bool ok, string detail) FlightsHoldCorners(RouteSpeedProfile p, Vector3[] poly, RouteSpeedModel model)
    {
        bool clear = true; string d = "";
        foreach (var f in p.Flights)
        {
            int i0 = IndexAt(p, f.LaunchDistance), i1 = IndexAt(p, f.LandingDistance);
            float turn = 0f;
            for (int i = Mathf.Max(1, i0); i <= Mathf.Min(i1, poly.Length - 2); i++)
            {
                Vector2 a = new(poly[i].X - poly[i - 1].X, poly[i].Z - poly[i - 1].Z), b = new(poly[i + 1].X - poly[i].X, poly[i + 1].Z - poly[i].Z);
                if (a.LengthSquared() > 1e-6f && b.LengthSquared() > 1e-6f) turn += Mathf.Abs(a.AngleTo(b));
            }
            string fault = "";
            if (f.Length * turn * 0.5f > WorldScale.FlightDriftTolerance) fault = $" FLIES THROUGH A {Mathf.RadToDeg(turn):0}° TURN";
            else
            {
                float runEnd = f.LandingDistance + WorldScale.LandingRunAfterFlight;
                for (int i = i1; i < p.Count && p.Distance[i] <= runEnd; i++)
                {
                    float limit = p.CornerLimit[i];
                    if (float.IsInfinity(limit)) continue;
                    float shed = model.BrakeDeceleration * Mathf.Max(0f, p.Distance[i] - f.LandingDistance) / Mathf.Max(1f, f.LandingSpeed);
                    if (f.LandingSpeed - shed > limit + 1f) { fault = $" LANDS TOO FAST FOR THE CORNER AT {p.Distance[i]:0} m ({limit:0} m/s after braking {shed:0})"; break; }
                }
            }
            clear &= fault == "";
            d += $" [{f.LaunchDistance:0}→{f.LandingDistance:0} m at {f.LaunchSpeed:0} m/s, lands {f.LandingVerticalSpeed:0} m/s down → {f.LandingSpeed:0} m/s{fault}]";
        }
        return (clear, $"{p.Flights.Count} flights{d}");
    }

    private static int IndexAt(RouteSpeedProfile p, float distance)
    {
        int i = Array.BinarySearch(p.Distance, distance);
        return Mathf.Clamp(i >= 0 ? i : ~i, 0, p.Count - 1);
    }

    /// <summary>Exits (02 §4, 04 §12, D-105): at least the primary's; every pad inside the footprint with its radius to spare,
    /// level across and along, and any two pads apart by <see cref="WorldScale.ExitSeparationMin"/>.</summary>
    private static void ValidateExits(StageDefinition def, ValidationReport report)
    {
        var field = def.HeightField!;
        bool ok = def.Exits.Count >= 1 && def.Exits[0].IsPrimary;
        float worstSpread = 0f, minApart = float.MaxValue;
        string detail = "";
        foreach (var e in def.Exits)
        {
            bool inside = Mathf.Abs(e.Position.X) <= WorldScale.FootprintLength * 0.5f - WorldScale.PadRadius + 1f && Mathf.Abs(e.Position.Z) <= WorldScale.FootprintWidth * 0.5f - WorldScale.PadRadius;
            float lx = -Mathf.Sin(e.Heading), lz = Mathf.Cos(e.Heading), fx = Mathf.Cos(e.Heading), fz = Mathf.Sin(e.Heading);
            float h0 = field.Sample(e.Position.X, e.Position.Z), spread = 0f;
            foreach (var (dx, dz) in new[] { (lx * 30f, lz * 30f), (-lx * 30f, -lz * 30f), (fx * 40f, fz * 40f), (-fx * 40f, -fz * 40f) })
                spread = Mathf.Max(spread, Mathf.Abs(field.Sample(e.Position.X + dx, e.Position.Z + dz) - h0));
            worstSpread = Mathf.Max(worstSpread, spread);
            foreach (var o in def.Exits)
                if (o.Index < e.Index) minApart = Mathf.Min(minApart, new Vector2(o.Position.X - e.Position.X, o.Position.Z - e.Position.Z).Length());
            bool thisOk = inside && spread <= 3f;
            ok &= thisOk;
            detail += $" [{e.Label}{(e.IsPrimary ? " primary" : $" line {e.LineIndex + 1}")} at ({e.Position.X:0}, {e.Position.Z:0}) y {e.Position.Y:0}, spread {spread:0.0} m{(thisOk ? "" : " FAIL")}]";
        }
        if (def.Exits.Count > 1 && minApart < WorldScale.ExitSeparationMin) ok = false;
        report.Add("exits are distinct, inside the footprint and on level pads (D-105)", ok, $"{def.Exits.Count} exits{detail}{(def.Exits.Count > 1 ? $", nearest pair {minApart:0} m apart" : "")}");
    }

    private static void ValidateCheckpoints(StageDefinition def, ValidationReport report)
    {
        var field = def.HeightField!;
        bool clear = true;
        float worst = 0f;
        foreach (var c in def.Checkpoints)
        {
            // Stable, level ground across the corridor at the anchor (04 §13): heights ±30 m either side.
            float lx = -Mathf.Sin(c.Heading), lz = Mathf.Cos(c.Heading);
            float h0 = field.Sample(c.Position.X, c.Position.Z);
            float spread = Mathf.Max(Mathf.Abs(field.Sample(c.Position.X + lx * 30f, c.Position.Z + lz * 30f) - h0),
                                     Mathf.Abs(field.Sample(c.Position.X - lx * 30f, c.Position.Z - lz * 30f) - h0));
            worst = Mathf.Max(worst, spread);
            if (spread > 3f) clear = false;
        }
        report.Add("checkpoints exist along the progression", def.Checkpoints.Count >= 8, $"{def.Checkpoints.Count} anchors");
        report.Add("checkpoints have clearance", clear, $"worst lateral height spread {worst:0.00} m");
    }

    /// <summary>Mandatory and secondary checks that apply to a skeleton (04 §12); more join with each slice.</summary>
    private void Validate(RouteSkeleton route, RouteSpeedProfile profile, RouteSpeedProfile ceilingProfile, StageHeightField field, ValidationReport report)
    {
        var v = route.Vertices;
        float entryX = -WorldScale.FootprintLength * 0.5f + WorldScale.EntryMargin;
        float exitX = WorldScale.FootprintLength * 0.5f - WorldScale.ExitMargin;

        // A spiral pit finale (D-102) ends on the pit floor, wherever the turn leaves it; the footprint check bounds it.
        bool exitOk = route.Spiral is { } spx ? new Vector2(v[^1].Position.X - spx.Centre.X, v[^1].Position.Z - spx.Centre.Z).Length() <= spx.OuterRadius : Mathf.Abs(v[^1].Position.X - exitX) < 1f;
        report.Add("start and exit exist", v.Count >= 2 && Mathf.Abs(v[0].Position.X - entryX) < 1f && exitOk,
            $"start x={v[0].Position.X:0} exit x={v[^1].Position.X:0}{(route.Spiral is { } sp ? $" on the floor of a spiral pit at ({sp.Centre.X:0}, {sp.Centre.Z:0}), r {sp.OuterRadius:0}→{sp.InnerRadius:0}, {sp.Depth:0} m deep" : "")}");

        float maxGap = 0f, maxBand = 0f;
        for (int i = 1; i < v.Count; i++)
        {
            // Plan spacing: a module's rim face is a real drop between two samples, not a break in the route.
            maxGap = Mathf.Max(maxGap, new Vector2(v[i].Position.X - v[i - 1].Position.X, v[i].Position.Z - v[i - 1].Position.Z).Length());
        }
        foreach (var p in v) maxBand = Mathf.Max(maxBand, Mathf.Abs(p.Position.Z));
        report.Add("primary route is continuous", maxGap <= WorldScale.RouteSampleSpacing * 1.5f, $"max vertex gap {maxGap:0.00} m");
        // Progress is route distance and headings are unbounded (D-096, D-102): the plan constraint is the footprint alone.
        bool inFootprint = v.All(p => Mathf.Abs(p.Position.X) <= WorldScale.FootprintLength * 0.5f && Mathf.Abs(p.Position.Z) <= WorldScale.FootprintWidth * 0.5f);
        report.Add("route stays inside the footprint", inFootprint);
        report.Add("route stays inside the route band", maxBand <= WorldScale.RouteBandHalfWidth, $"max |z| {maxBand:0} m");

        float minRadius = route.Bends.Count == 0 ? float.PositiveInfinity : route.Bends.Min(b => b.Radius);
        report.Add("no bend tighter than the mandatory minimum", minRadius >= WorldScale.CommittedBendRadius - 1e-3f,
            $"min radius {minRadius:0} m over {route.Bends.Count} bends");

        report.Add("route length in the calibrated range",
            route.Length >= WorldScale.PrimaryRouteLength * 0.9f && route.Length <= WorldScale.PrimaryRouteLength * 1.3f,
            $"{route.Length:0} m");

        report.Add("route is traversable by the base kit", !profile.Stalled, profile.Stalled ? $"stalled at vertex {profile.StallVertex}" : "");
        report.Add("base-kit travel time near the stage target",
            profile.TotalTime >= WorldScale.TargetBaseKitSeconds * 0.7f && profile.TotalTime <= WorldScale.TargetBaseKitSeconds * 1.5f,
            $"{profile.TotalTime:0.0} s (target {WorldScale.TargetBaseKitSeconds:0} s)");

        // ---- geometry checks on the stamped corridor (04 §10, §12) ----
        // A module's own faces (a gap's rim and exit wall, a ramp's back face) exceed the route limits on
        // purpose and are judged by the module validator instead (04 §10, "when their validator understands the exception").
        bool InsideModuleFace(float d)
        {
            foreach (var f in route.Features)
            {
                if (f.Kind == RouteFeatureKind.LaunchCrest) continue;
                float start = f.CentreDistance - (f.Kind == RouteFeatureKind.LaunchRamp ? f.Rise / f.Slope + WorldScale.RampEase : 0f);
                if (d >= start - WorldScale.RouteSampleSpacing * 2f && d <= f.FeatureEnd + WorldScale.RouteSampleSpacing * 2f) return true;
            }
            return false;
        }
        float maxGrade = 0f, maxDelta = 0f, prevGrade = 0f;
        for (int i = 1; i < v.Count; i++)
        {
            float ds = v[i].Distance - v[i - 1].Distance;
            if (ds <= 1e-3f) continue;
            float grade = (v[i].Position.Y - v[i - 1].Position.Y) / ds;
            bool skip = InsideModuleFace(v[i].Distance);
            if (!skip) maxGrade = Mathf.Max(maxGrade, Mathf.Abs(grade));
            if (i > 1 && !skip && !InsideModuleFace(v[i - 1].Distance)) maxDelta = Mathf.Max(maxDelta, Mathf.Abs(grade - prevGrade));
            prevGrade = grade;
        }
        report.Add("corridor grade within the route limit", maxGrade <= WorldScale.MaxRouteGrade, $"max grade {maxGrade:0.000} (limit {WorldScale.MaxRouteGrade:0.00})");
        report.Add("no abrupt grade change along the corridor", maxDelta <= WorldScale.MaxGradeDeltaPerSample, $"max Δgrade/sample {maxDelta:0.000}");

        report.Add("base relief keeps contact at the cap", field.SwellCurvature <= 1f / WorldScale.CruiseCrestRadius + 1e-6f,
            $"swell crest radius ≥ {1f / Mathf.Max(1e-6f, field.SwellCurvature):0} m");
        if (route.Dunes.Exists)
        {
            // Dune Sea (D-099): a train's crests are the wave's own, so the corridor there is the relief; its
            // profile must meet the relief wave at every crest apex (a trimmed crest would leave a seam).
            float worstSeam = 0f; int crests = 0, trains = 0; int lastStraight = -1;
            foreach (var f in route.Features)
            {
                if (f.Kind != RouteFeatureKind.LaunchCrest) continue;
                crests++;
                if (f.StartIndex != lastStraight) { trains++; lastStraight = f.StartIndex; }
                var p = v[route.IndexAtDistance(f.CentreDistance)].Position;
                worstSeam = Mathf.Max(worstSeam, Mathf.Abs(f.Height - field.DuneHeight(p.X, p.Z)));
            }
            report.Add("dune trains ride the stage's wave (crest heights meet the relief wave at every apex)", worstSeam <= 2f,
                $"wave λ {route.Dunes.Wavelength:0} m / H {route.Dunes.Height:0} m at {Mathf.RadToDeg(route.Dunes.Angle):+0;-0}°; {trains} trains, {crests} crests, worst seam {worstSeam:0.00} m");
        }

        bool crestsClear = true;
        string crestDetail = "";
        foreach (var f in route.Features)
        {
            if (f.Kind != RouteFeatureKind.LaunchCrest) continue;
            float speed = profile.SpeedAt(f.CentreDistance);
            float crestRadius = RouteSpeedModel.CosineCrestRadius(f.Wavelength, f.Height);
            f.IsLaunch = _speed.CrestIsLaunch(crestRadius, speed);
            // Flight to fall the crest height on the far side at the arrival speed (04 §10), or the
            // model's own flight off this crest when it integrated one (D-094), whichever lands later.
            f.LandingDistance = f.IsLaunch ? speed * Mathf.Sqrt(2f * f.Height / Mathf.Max(0.001f, _gravity)) : 0f;
            float crestStart = f.CentreDistance - f.Wavelength * 0.5f;
            foreach (var fl in profile.Flights)
                if (fl.LaunchDistance >= crestStart && fl.LaunchDistance <= f.CentreDistance + f.Wavelength * 0.5f)
                    f.LandingDistance = Mathf.Max(f.LandingDistance, fl.LandingDistance - f.CentreDistance - f.Wavelength * 0.5f);
            float straightEnd = v[f.EndIndex].Distance;
            bool ok = f.CentreDistance + f.Wavelength * 0.5f + f.LandingDistance <= straightEnd + 1e-3f;
            // The crest's paid path (04 §5E, D-100): a full charge released at the apex at the arrival speed must land
            // on the straight with its run; at the ceiling the slam landing off that jump must (the two-price rule).
            float paid = _speed.JumpRange(speed, _fullTakeoff, f.Height, 0f, 0f);
            float ceilingSlam = _ceiling.SlamRange(ceilingProfile.SpeedAt(f.CentreDistance), _fullTakeoff, WorldScale.SlamReactionSeconds, _slamInitial, _slamAccel, f.Height);
            bool paidOk = f.CentreDistance + paid + WorldScale.LandingRunAfterFlight <= straightEnd + 1e-3f
                       && f.CentreDistance + ceilingSlam + WorldScale.LandingRunAfterFlight <= straightEnd + 1e-3f;
            crestsClear &= ok && paidOk;
            crestDetail += $" [{f.Wavelength:0}/{f.Height:0} at {f.CentreDistance:0} m: {speed:0} m/s {(f.IsLaunch ? "launch" : "roll")}, landing {f.LandingDistance:0} m{(ok ? "" : " OVERRUNS")}; full charge {paid:0} m, ceiling slam {ceilingSlam:0} m on {straightEnd - f.CentreDistance:0} m{(paidOk ? "" : " PAID PATH OVERRUNS")}]";
        }
        report.Add("launch crests keep a straight landing run for the roll-off and the charged jump (two prices)", crestsClear, $"{route.Features.Count(f => f.Kind == RouteFeatureKind.LaunchCrest)} crests{crestDetail}");

        // Wall clearance (04 §10, D-096): out to the level width plus the setback the primary's stamp is still
        // complete on both sides (its weight 1), so no wall face, and no side terrain, stands where the ground
        // follow's lateral samples or a wide line would meet it. Inside a bend tighter than the reach the
        // sample would cross the bend's own centre, so only the outside is read there.
        float worstWeight = 1f;
        var bendAt = new RouteBend?[v.Count];
        foreach (var b in route.Bends) for (int i = b.StartIndex; i <= b.EndIndex; i++) bendAt[i] = b;
        float reach = StageHeightField.CorridorHalfWidth + WorldScale.WallSetback;
        for (int i = 0; i < v.Count; i += 5)
        {
            float lx = -Mathf.Sin(v[i].Heading), lz = Mathf.Cos(v[i].Heading);
            foreach (float side in new[] { 1f, -1f })
            {
                if (bendAt[i] is { } bend && side * Mathf.Sign(bend.TurnAngle) > 0f && bend.Radius < reach + 10f) continue;
                worstWeight = Mathf.Min(worstWeight, field.PrimaryWeight(v[i].Position.X + lx * reach * side, v[i].Position.Z + lz * reach * side));
            }
        }
        report.Add("wall faces stay outside the corridor's level width plus the setback", worstWeight >= 0.99f, $"lowest stamp weight at {reach:0} m {worstWeight:0.000} (walls {field.WallHeight:0} m)");

        float startFlat = PadFlatness(field, v[0].Position), exitFlat = PadFlatness(field, v[^1].Position);
        report.Add("start and exit pads are flat", startFlat < 1.0f && exitFlat < 1.0f, $"height spread start {startFlat:0.00} m, exit {exitFlat:0.00} m");
    }

    // ---------------------------------------------------------------- T5 instruments (read-only)

    /// <summary>Where the wall-clearance probe was worst on a stage, and what a fine re-sample says about it (T5).</summary>
    public readonly record struct WallProbeReading(
        float CoarseWeight, int VertexIndex, float Side, float BendRadius,
        float InnerWorst, float SubWidthMetres, float RiseMetres, float NearestLineMetres, string Profile)
    {
        /// <summary>
        /// The discriminator is the ground, not the stamp: a wall face standing in the reach lifts the terrain well
        /// above the route, while the falloff of a bend's own bank leaves it nearly level. Three metres is a fifth of
        /// the shortest archetype wall and far above any bank's shoulder.
        /// </summary>
        public bool IsIntrusion => RiseMetres > 3f;
    }

    /// <summary>
    /// T5 instrument, read-only: re-runs the shipped wall-clearance probe (04 §10, D-096) over a stage, finds the
    /// worst sample, then re-samples across the route at 1 m from the corridor centre to well past the reach.
    ///
    /// <para>The shipped check takes one sample per five vertices at exactly the level-width reach and fails the stage
    /// when the primary's stamp weight there is under 0.99. It is suspected of reading the falloff of a bend's own
    /// bank as a wall. Weight alone cannot settle that — a stamp always fades outward, so sampling inward finds full
    /// weight whatever is out there — so this reads the <em>terrain</em> across the same window: <c>RiseMetres</c> is
    /// how far the ground climbs above the route inside it. A wall face lifts it metres; a bank's shoulder does not.
    /// Decides nothing and changes nothing: the verdict stays with the shipped check.</para>
    /// </summary>
    public static WallProbeReading MeasureWallProbe(StageDefinition def)
    {
        var route = def.PrimaryRoute;
        var v = route.Vertices;
        var field = def.HeightField!;
        var bendAt = new RouteBend?[v.Count];
        foreach (var b in route.Bends) for (int i = b.StartIndex; i <= b.EndIndex; i++) bendAt[i] = b;
        float reach = StageHeightField.CorridorHalfWidth + WorldScale.WallSetback;

        float worst = 1f;
        int worstIndex = 0;
        float worstSide = 1f;
        for (int i = 0; i < v.Count; i += 5)
        {
            float lx0 = -Mathf.Sin(v[i].Heading), lz0 = Mathf.Cos(v[i].Heading);
            foreach (float side in new[] { 1f, -1f })
            {
                if (bendAt[i] is { } bend && side * Mathf.Sign(bend.TurnAngle) > 0f && bend.Radius < reach + 10f) continue;
                float w = field.PrimaryWeight(v[i].Position.X + lx0 * reach * side, v[i].Position.Z + lz0 * reach * side);
                if (w >= worst) continue;
                worst = w; worstIndex = i; worstSide = side;
            }
        }

        var wv = v[worstIndex];
        float lx = -Mathf.Sin(wv.Heading) * worstSide, lz = Mathf.Cos(wv.Heading) * worstSide;
        float innerWorst = 1f, subWidth = 0f, rise = 0f;
        var profile = new System.Text.StringBuilder();
        for (float s2 = 0f; s2 <= reach + 40f + 1e-3f; s2 += 1f)
        {
            float w = field.PrimaryWeight(wv.Position.X + lx * s2, wv.Position.Z + lz * s2);
            float h = field.Sample(wv.Position.X + lx * s2, wv.Position.Z + lz * s2);
            if (s2 <= reach - 12f) innerWorst = Mathf.Min(innerWorst, w);
            if (s2 >= reach - 12f && s2 <= reach + 12f && w < 0.99f) subWidth += 1f;
            if (s2 >= reach - 12f) rise = Mathf.Max(rise, h - wv.Position.Y);
            if (Mathf.PosMod(s2, 20f) < 0.5f) profile.Append($" {s2:0}m:w{w:0.00}/h{h - wv.Position.Y:+0;-0;0}");
        }
        // What else is claiming the ground there? An optional line's own corridor blends the primary's weight down
        // just as a wall does, and it leaves the terrain level while doing it.
        Vector3 probe = wv.Position + new Vector3(lx, 0f, lz) * reach;
        float nearestLine = float.MaxValue;
        foreach (var line in def.OptionalLines)
            foreach (var lv in line.Vertices)
                nearestLine = Mathf.Min(nearestLine, new Vector2(lv.Position.X - probe.X, lv.Position.Z - probe.Z).Length());

        return new WallProbeReading(worst, worstIndex, worstSide, bendAt[worstIndex]?.Radius ?? 0f,
                                    innerWorst, subWidth, rise,
                                    nearestLine == float.MaxValue ? -1f : nearestLine, profile.ToString());
    }

    /// <summary>Where a tube's worst wall ride comes from, in the builder's own terms (T5).</summary>
    public readonly record struct TubeRideReading(
        float MaxDegrees, string Phase, float AlongMetres, float SpanMetres,
        float LateralOffset, float TightestBendRadius, float SpeedAtMax);

    /// <summary>
    /// T5 instrument, read-only: recomputes the wall-ride profile the tube validator reports (docs/11 §7d,
    /// tan φ = v²κ/g over the axis's plan curvature) and says *where* the maximum sits — which term of
    /// <see cref="TubeBuilder"/>'s envelope is running there (climb, swing out, cruise, swing back, descent), how far
    /// out the lateral offset has swung, and the tightest primary bend the section overlaps.
    ///
    /// <para>T7 and T8 already settled the ride's feel and the axis-reference error; this answers only the question
    /// they left: which part of the builder produces the angle.</para>
    /// </summary>
    public static TubeRideReading MeasureTubeRide(StageDefinition def, TubeDefinition t, float gravity)
    {
        var v = def.PrimaryRoute.Vertices;
        float span = v[t.JoinEnd].Distance - v[t.JoinStart].Distance;
        float ramp = TubeBuilder.RampLength(t.CruiseHeight);
        float swing = WorldScale.TubeSwingLength;
        var p = t.Profile!;

        float best = 0f, bestAlong = 0f, bestSpeed = 0f;
        float along = 0f;
        for (int i = 1; i < t.Axis.Length; i++)
        {
            along += t.Axis[i].DistanceTo(t.Axis[i - 1]);
            if (i < 3 || i + 3 >= t.Axis.Length) continue;
            Vector2 a = new(t.Axis[i - 3].X, t.Axis[i - 3].Z), b = new(t.Axis[i].X, t.Axis[i].Z), c = new(t.Axis[i + 3].X, t.Axis[i + 3].Z);
            Vector2 ab = b - a, bc = c - b;
            if (ab.LengthSquared() < 1e-6f || bc.LengthSquared() < 1e-6f) continue;
            float turn = Mathf.Abs(ab.AngleTo(bc)), len = 0.5f * (ab.Length() + bc.Length());
            float speed = p.Speed[Mathf.Min(i, p.Count - 1)];
            float deg = Mathf.RadToDeg(Mathf.Atan(speed * speed * (turn / Mathf.Max(1e-3f, len)) / gravity));
            if (deg <= best) continue;
            best = deg; bestAlong = along; bestSpeed = speed;
        }

        // The envelope runs in the primary's distance; the axis is a little longer, so read it by fraction.
        float d = bestAlong / Mathf.Max(1f, t.Length) * span;
        string phase = d < ramp ? "climb"
                     : d < ramp + swing ? "swing out"
                     : d < span - ramp - swing ? "cruise"
                     : d < span - ramp ? "swing back"
                     : "descent";
        float offset = WorldScale.TubeMouthOffset
                     + (WorldScale.TubeLateralOffset - WorldScale.TubeMouthOffset) * TubeBuilder.LateralEnvelope(d, span, ramp);

        float tightest = 0f;
        foreach (var bend in def.PrimaryRoute.Bends)
        {
            if (bend.EndIndex < t.JoinStart || bend.StartIndex > t.JoinEnd) continue;
            if (tightest == 0f || bend.Radius < tightest) tightest = bend.Radius;
        }
        return new TubeRideReading(best, phase, d, span, offset, tightest, bestSpeed);
    }

    private readonly float _gravity;

    /// <summary>
    /// Two speeds (04 §12, D-094). The base-kit profile carries the figure every report must show
    /// (seconds below the base cap). Safety reads the ceiling profile: no bend may sit inside a flight
    /// or its landing run at either speed, and the primary must offer a chainable line (consecutive
    /// Flow opportunities never further apart than the chain window at the ceiling speed).
    /// </summary>
    private float ValidateTwoSpeeds(RouteSkeleton route, RouteSpeedProfile profile, RouteSpeedProfile ceiling, ValidationReport report)
    {
        var v = route.Vertices;
        float baseCap = _speed.Cap, ceilingSpeed = _ceiling.Cap;

        float below = profile.SecondsBelow(baseCap * 0.98f), start = profile.TimeToReach(baseCap * 0.98f);
        report.Note("seconds below the base cap",
            $"{below:0.0} s of {profile.TotalTime:0.0} s ({start:0.0} s standing start); {profile.Flights.Count} flights, {profile.AirborneSeconds:0.0} s airborne");

        // A flying ball goes straight: it may not leave the ground inside a bend nor fly across a bend's
        // start. It may land into a bend's approach only if that bend holds the speed it lands with,
        // because the landing run is too short to shed any.
        (bool ok, string detail) FlightsClear(RouteSpeedProfile p, RouteSpeedModel model)
        {
            bool clear = true; string d = "";
            foreach (var f in p.Flights)
            {
                // A module's own launch (its lip, its far rim) is the module validator's business (04 §10): the
                // base kit's flight must land on the reserved straight, the ceiling's slam landing must.
                bool moduleOwned = false;
                foreach (var feat in route.Features)
                    if (feat.Kind != RouteFeatureKind.LaunchCrest && (Mathf.Abs(f.LaunchDistance - feat.FeatureEnd) <= ModuleLaunchWindow || Mathf.Abs(f.LaunchDistance - feat.CentreDistance) <= ModuleLaunchWindow))
                        moduleOwned = true;
                float runEnd = f.LandingDistance + WorldScale.LandingRunAfterFlight;
                string fault = "";
                if (moduleOwned) { d += $" [{f.LaunchDistance:0}→{f.LandingDistance:0} m module launch, {f.Seconds:0.0} s, lands {f.LandingVerticalSpeed:0} m/s down → {f.LandingSpeed:0} m/s]"; continue; }
                foreach (var b in route.Bends)
                {
                    float bs = v[b.StartIndex].Distance, be = v[b.EndIndex].Distance;
                    // A straight flight of length L inside an arc of radius r drifts L² / 2r off the line; a
                    // short skim stays in the corridor, a real flight does not.
                    float inBend = Mathf.Max(0f, Mathf.Min(f.LandingDistance, be) - Mathf.Max(f.LaunchDistance, bs));
                    bool drifts = inBend * inBend / (2f * b.Radius) > WorldScale.FlightDriftTolerance;
                    if (drifts && f.LaunchDistance >= bs && f.LaunchDistance <= be) { fault = " LAUNCHES IN A BEND"; break; }
                    if (drifts && f.LaunchDistance < bs && f.LandingDistance > bs) { fault = " FLIES ACROSS A BEND"; break; }
                    // Between touchdown and the bend the brake can shed drive × (run / speed); the rest must fit the bend.
                    float limit = model.CornerSpeedLimit(b.Radius);
                    float shed = model.BrakeDeceleration * Mathf.Max(0f, bs - f.LandingDistance) / Mathf.Max(1f, f.LandingSpeed);
                    if (bs > f.LandingDistance && bs <= runEnd && f.LandingSpeed - shed > limit + 1f) { fault = $" LANDS TOO FAST FOR THE r{b.Radius:0} BEND ({limit:0} m/s after braking {shed:0})"; break; }
                }
                clear &= fault == "";
                d += $" [{f.LaunchDistance:0}→{f.LandingDistance:0} m at {f.LaunchSpeed:0} m/s, {f.Seconds:0.0} s, lands {f.LandingVerticalSpeed:0} m/s down → {f.LandingSpeed:0} m/s{fault}]";
            }
            return (clear, $"{p.Flights.Count} flights, {p.AirborneSeconds:0.0} s airborne{d}");
        }
        var baseFlights = FlightsClear(profile, _speed);
        var ceilFlights = FlightsClear(ceiling, _ceiling);
        report.Add("flights stay clear of bends at the base cap (none launched in or across a bend; landings hold the next bend)", baseFlights.ok, baseFlights.detail);
        report.Add("flights stay clear of bends at the ceiling (none launched in or across a bend; landings hold the next bend)", ceilFlights.ok, $"{ceilingSpeed:0} m/s: {ceilFlights.detail}");
        int hardLandings = ceiling.Flights.Count(f => f.LandingVerticalSpeed >= _flow.PlainLandingSpeed);
        report.Note("ceiling profile", $"{ceilingSpeed:0} m/s: {ceiling.TotalTime:0.0} s, max {ceiling.MaxSpeed:0} m/s, " +
            $"{ceiling.Flights.Count} flights ({hardLandings} land above the {_flow.PlainLandingSpeed:0} m/s plain-landing loss: slam or lose Flow)");

        // Chainable line: crest apexes, module features (a charged jump at a rim or a lip) and real bends
        // (the carve) are the Flow opportunities the primary offers.
        var opportunities = new List<float>();
        foreach (var f in route.Features) opportunities.Add(f.CentreDistance);
        foreach (var b in route.Bends) if (Mathf.Abs(Mathf.RadToDeg(b.TurnAngle)) >= WorldScale.FlowBendMinDegrees) opportunities.Add(v[b.StartIndex].Distance);
        opportunities.Sort();
        float window = _flow.ChainWindowSeconds * ceilingSpeed, maxGap = 0f;
        for (int i = 1; i < opportunities.Count; i++) maxGap = Mathf.Max(maxGap, opportunities[i] - opportunities[i - 1]);
        // The known-safe straight fallback has neither bends nor crests: nothing to chain, nothing to fail.
        bool chainable = route.Bends.Count == 0 && route.Features.Count == 0 || opportunities.Count >= 2 && maxGap <= window;
        report.Add("a chainable line exists on the primary (Flow opportunities inside the chain window at the ceiling)", chainable,
            $"{opportunities.Count} opportunities, widest gap {maxGap:0} m (window {_flow.ChainWindowSeconds:0} s × {ceilingSpeed:0} m/s = {window:0} m)");
        return maxGap;
    }

    /// <summary>
    /// Challenge modules (04 §5E, D-097): every gap, ramp and committed bend on the primary is a module
    /// with the seven fields, read from the two profiles. Two prices: the free path (drive through the pit
    /// and up its exit wall, fly off the lip uncharged, take the bend at its corner limit) never stops and
    /// never drops the model below the free-path fraction of the base cap; the paid path (a half-charged
    /// jump across a mandatory gap, a full charge off the lip, a carve through the bend) grants Flow and
    /// must land inside the straight the skeleton reserved. Mandatory gaps obey 04 §11: crossable
    /// half-charged at the model's arrival speed, never needing boost or the burst.
    /// </summary>
    private void ValidateModules(StageDefinition def, ValidationReport report)
    {
        var route = def.PrimaryRoute;
        var v = route.Vertices;
        var profile = def.SpeedProfile;
        var ceiling = def.CeilingProfile!;
        float freeFloor = WorldScale.FreePathSpeedFraction * _speed.BaseCap;
        bool allPass = true, gapsCross = true;
        int gaps = 0, ramps = 0, bends = 0;
        string detail = "";

        foreach (var f in route.Features)
        {
            if (f.Kind == RouteFeatureKind.LaunchCrest) continue;
            float straightEnd = v[f.EndIndex].Distance;
            var mod = new ChallengeModule
            {
                Required = true,
                Distance = f.CentreDistance,
                EntrySpeed = profile.SpeedAt(f.CentreDistance),
                CeilingEntrySpeed = ceiling.SpeedAt(f.CentreDistance),
                LandingStart = f.FeatureEnd,
                LandingEnd = f.FeatureEnd + WorldScale.LandingZoneLength,
                FlowDistance = f.CentreDistance,
            };
            mod.FreeExitSpeed = profile.SpeedAt(mod.LandingEnd);
            bool freeOk = !float.IsInfinity(profile.TimeAt(mod.LandingEnd)) && mod.FreeExitSpeed >= freeFloor;
            bool paidOk, envelopeOk = true;
            float ceilingRange;
            if (f.Kind == RouteFeatureKind.Gap)
            {
                gaps++;
                mod.Kind = ChallengeModuleKind.ModerateGap;
                mod.Geometry = $"gap {f.Opening:0} m × {f.Depth:0} m deep, 25° exit wall";
                mod.Entrance = $"{WorldScale.TakeoffRunwayLength:0} m flat runway";
                // The rims sit on the sloped corridor profile: the jump must reach the far rim's real height.
                float rise = v[route.IndexAtDistance(f.FeatureEnd)].Position.Y - v[route.IndexAtDistance(f.CentreDistance)].Position.Y;
                mod.PaidRange = _speed.JumpRange(mod.EntrySpeed, _halfTakeoff, -rise);
                envelopeOk = mod.PaidRange >= f.Opening + 2f * _ballRadius + WorldScale.MandatoryGapMargin;
                float fullRange = _speed.JumpRange(mod.EntrySpeed, _fullTakeoff, -rise);
                paidOk = f.CentreDistance + fullRange + WorldScale.LandingRunAfterFlight <= straightEnd + 1e-3f;
                ceilingRange = _ceiling.JumpRange(mod.CeilingEntrySpeed, _fullTakeoff, -rise);
                // The free path rides out of the pit and leaves the far rim like a ramp: that flight is the module's
                // own; the base kit's must land on the straight, the ceiling's must land there with a slam.
                float exitSlope = f.Depth / Mathf.Max(1f, f.Opening - WorldScale.GapRimFace);
                var freeFlight = ModuleFlight(profile, f.FeatureEnd);
                float ceilingSlam = f.FeatureEnd + SlamRun(mod.CeilingEntrySpeed, exitSlope, 0f);
                freeOk &= (freeFlight is null || freeFlight.LandingDistance <= straightEnd) && ceilingSlam <= straightEnd + 1e-3f;
                mod.Detail = $"entry {mod.EntrySpeed:0} m/s, far rim {rise:+0;-0} m: half charge flies {mod.PaidRange:0} m (needs {f.Opening + 2f * _ballRadius + WorldScale.MandatoryGapMargin:0}), full {fullRange:0} m on a {straightEnd - f.CentreDistance:0} m straight; free path {(freeFlight is null ? "rolls out" : $"leaves the {Mathf.RadToDeg(Mathf.Atan(exitSlope)):0}° exit wall for {freeFlight.Length:0} m")} and exits at {mod.FreeExitSpeed:0} m/s; ceiling entry {mod.CeilingEntrySpeed:0} m/s: full charge {ceilingRange:0} m, slam landing at +{ceilingSlam - f.FeatureEnd:0} m";
            }
            else
            {
                ramps++;
                mod.Kind = ChallengeModuleKind.LaunchRamp;
                float deg = Mathf.RadToDeg(Mathf.Atan(f.Slope));
                mod.Geometry = $"ramp {deg:0}°, lip {f.Rise:0} m, 45° back face";
                mod.Entrance = $"{WorldScale.RampApproachLength:0} m flat approach";
                float cos = 1f / Mathf.Sqrt(1f + f.Slope * f.Slope), sin = f.Slope * cos;
                mod.PaidRange = _speed.JumpRange(mod.EntrySpeed * cos, _fullTakeoff, f.Rise, mod.EntrySpeed * sin);
                paidOk = f.CentreDistance + mod.PaidRange + WorldScale.LandingRunAfterFlight <= straightEnd + 1e-3f;
                ceilingRange = _ceiling.JumpRange(mod.CeilingEntrySpeed * cos, _fullTakeoff, f.Rise, mod.CeilingEntrySpeed * sin);
                // The free path is the uncharged launch the model integrates itself; it must exist and land on the straight.
                Flight? free = ModuleFlight(profile, f.CentreDistance);
                float ceilingSlam = f.CentreDistance + SlamRun(mod.CeilingEntrySpeed, f.Slope, f.Rise);
                freeOk &= free is not null && free.LandingDistance <= straightEnd && ceilingSlam <= straightEnd + 1e-3f;
                mod.Detail = $"entry {mod.EntrySpeed:0} m/s: uncharged flight {(free is null ? "none" : $"{free.Length:0} m, lands {free.LandingVerticalSpeed:0} m/s down")}, full charge {mod.PaidRange:0} m on a {straightEnd - f.CentreDistance:0} m straight; free path exits at {mod.FreeExitSpeed:0} m/s; ceiling entry {mod.CeilingEntrySpeed:0} m/s: full charge {ceilingRange:0} m, slam landing at +{ceilingSlam - f.CentreDistance:0} m";
            }
            mod.Passed = freeOk && paidOk && envelopeOk;
            allPass &= mod.Passed;
            gapsCross &= envelopeOk;
            detail += $" [{mod.Kind} at {mod.Distance:0} m: {mod.Detail}{(mod.Passed ? "" : freeOk ? paidOk ? " NOT CROSSABLE HALF-CHARGED" : " PAID FLIGHT OVERRUNS THE STRAIGHT" : " FREE PATH FAILS")}]";
            def.Modules.Add(mod);
        }

        float prevBendEnd = 0f;
        foreach (var b in route.Bends)
        {
            float start = v[b.StartIndex].Distance;
            if (b.Radius <= WorldScale.CommittedBendRadius + 1e-3f)
            {
                bends++;
                var mod = new ChallengeModule
                {
                    Kind = ChallengeModuleKind.BankedTurn,
                    Required = true,
                    Distance = start,
                    Geometry = $"r{b.Radius:0} bend of {Mathf.Abs(Mathf.RadToDeg(b.TurnAngle)):0}°, bank {WorldScale.BankHeightPerRadius * b.Radius:0.0} m",
                    Entrance = $"{start - prevBendEnd:0} m straight before it",
                    EntrySpeed = profile.SpeedAt(start),
                    CeilingEntrySpeed = ceiling.SpeedAt(start),
                    LandingStart = v[b.EndIndex].Distance,
                    LandingEnd = v[b.EndIndex].Distance,
                    FreeExitSpeed = b.CornerLimit,
                    PaidRange = 0f,
                    FlowDistance = start,
                };
                mod.Passed = b.CornerLimit >= freeFloor && start - prevBendEnd >= WorldScale.BankedTurnApproach - 1e-3f;
                mod.Detail = $"corner limit {b.CornerLimit:0} m/s (free floor {freeFloor:0}), {start - prevBendEnd:0} m approach";
                allPass &= mod.Passed;
                detail += $" [{mod.Kind} at {mod.Distance:0} m: {mod.Detail}{(mod.Passed ? "" : " FAIL")}]";
                def.Modules.Add(mod);
            }
            prevBendEnd = v[b.EndIndex].Distance;
        }

        report.Add("challenge modules pass their validators (two prices: the free path never stops or drops below 2/3 of the base cap; the paid path lands on its straight)",
            allPass, $"{gaps} gaps, {ramps} ramps, {bends} banked turns{detail}");
        report.Add("mandatory jumps fit the base capability envelope (half charge at the model's arrival speed, no boost, 04 §11)",
            gapsCross, $"{gaps} mandatory gaps");
    }

    /// <summary>The profile's flight launched at a module's own launch point (a lip, a far rim), if any.</summary>
    private static Flight? ModuleFlight(RouteSpeedProfile profile, float launchDistance)
    {
        Flight? best = null;
        foreach (var fl in profile.Flights)
            if (fl.LaunchDistance >= launchDistance - ModuleLaunchWindow && fl.LaunchDistance <= launchDistance + ModuleLaunchWindow) best = fl;
        return best;
    }

    /// <summary>Route distance a slam lands a ceiling launch at the given slope, after the reaction time.</summary>
    private float SlamRun(float speed, float launchSlope, float drop)
    {
        float cos = 1f / Mathf.Sqrt(1f + launchSlope * launchSlope), sin = launchSlope * cos;
        return _ceiling.SlamRange(speed * cos, speed * sin, WorldScale.SlamReactionSeconds, _slamInitial, _slamAccel, drop) + WorldScale.LandingRunAfterFlight;
    }

    /// <summary>Metres either side of a module's launch point within which a flight is the module's own: the lip
    /// ease (12 m) plus the three-cell span the launch curvature is read over, so a launch the model fires a few
    /// cells past the lip is still the ramp's (D-100; at 12 m one ramp in twenty reported no free flight).</summary>
    private const float ModuleLaunchWindow = WorldScale.RampEase + WorldScale.RouteSampleSpacing * 3f;

    private static float PadFlatness(StageHeightField field, Vector3 centre)
    {
        float lo = float.MaxValue, hi = float.MinValue;
        for (int a = 0; a < 8; a++)
        {
            float ang = a * Mathf.Tau / 8f;
            float h = field.Sample(centre.X + Mathf.Cos(ang) * 30f, centre.Z + Mathf.Sin(ang) * 30f);
            lo = Mathf.Min(lo, h); hi = Mathf.Max(hi, h);
        }
        return hi - lo;
    }

    private static RouteSkeleton StraightRoute()
    {
        var route = new RouteSkeleton();
        float entryX = -WorldScale.FootprintLength * 0.5f + WorldScale.EntryMargin;
        float exitX = WorldScale.FootprintLength * 0.5f - WorldScale.ExitMargin;
        int steps = Mathf.CeilToInt((exitX - entryX) / WorldScale.RouteSampleSpacing);
        for (int i = 0; i <= steps; i++)
        {
            float x = Mathf.Min(exitX, entryX + i * WorldScale.RouteSampleSpacing);
            var p = new Vector3(x, 0f, 0f);
            float dist = route.Vertices.Count == 0 ? 0f : route.Vertices[^1].Distance + route.Vertices[^1].Position.DistanceTo(p);
            route.Vertices.Add(new RouteVertex { Position = p, Heading = 0f, Radius = float.PositiveInfinity, Distance = dist, Kind = RouteSegmentKind.Straight });
        }
        return route;
    }
}
