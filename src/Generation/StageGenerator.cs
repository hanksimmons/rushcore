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

        RouteSkeleton route = straight ? StraightRoute() : _routes.Build(routeSeed);
        report.Timings.Add(("route skeleton", sw.Elapsed.TotalMilliseconds)); sw.Restart();

        // B–D: archetype relief and the stamped corridor become the one logical height source;
        // the route polyline is then lifted onto it so every later check sees real geometry.
        var field = new StageHeightField(route, request.StageSeed);
        var optional = straight ? new List<RouteSkeleton>() : OptionalLineBuilder.Build(route, request.StageSeed);
        foreach (var line in optional) field.AddLine(line);
        Lift(route, field);
        foreach (var line in optional) Lift(line, field);
        report.Timings.Add(("relief + corridors", sw.Elapsed.TotalMilliseconds)); sw.Restart();

        RouteSpeedProfile profile = _speed.Integrate(route.Polyline());
        // The ceiling profile: a standing start, then a full chain held wherever the bends allow (04 §12).
        RouteSpeedProfile ceiling = _ceiling.Integrate(route.Polyline(), chainFromBaseCap: true);
        var def = new StageDefinition(request, route, profile, report) { RouteSeedUsed = routeSeed, HeightField = field, CeilingProfile = ceiling };
        foreach (var line in optional)
        {
            def.OptionalLines.Add(line);
            def.OptionalProfiles.Add(_speed.Integrate(line.Polyline(), profile.Speed[line.JoinStart]));
        }
        report.Timings.Add(("speed profiles", sw.Elapsed.TotalMilliseconds)); sw.Restart();

        PlaceCheckpoints(def);
        report.Timings.Add(("checkpoints", sw.Elapsed.TotalMilliseconds)); sw.Restart();

        Validate(route, profile, field, report);
        def.WidestFlowGap = ValidateTwoSpeeds(route, profile, ceiling, report);
        ValidateModules(def, report);
        ValidateOptionalLines(def, report);
        ValidateCheckpoints(def, report);
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
        for (float d = WorldScale.CheckpointSpacing; d < route.Length - WorldScale.PadRadius * 2f; d += WorldScale.CheckpointSpacing)
        {
            float place = d;
            foreach (var f in route.Features)
            {
                float fs = v[f.StartIndex].Distance, fe = f.ReservedEnd;
                if (place >= fs && place <= fe) place = fe + 20f;
            }
            if (place >= route.Length - WorldScale.PadRadius * 2f) break;
            int i = route.IndexAtDistance(place);
            // Level ground across the corridor: never anchor on a banked bend; step past it.
            foreach (var b in route.Bends)
                if (i >= b.StartIndex && i <= b.EndIndex) { i = Mathf.Min(v.Count - 1, b.EndIndex + 4); break; }
            if (v[i].Distance >= route.Length - WorldScale.PadRadius * 2f) break;
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
            bool ok = !prof.Stalled && maxGrade <= WorldScale.MaxRouteGrade && rise >= 15f && joinStart < 2f && joinEnd < 2f;
            allOk &= ok;
            detail += $" [{line.Kind} {v[0].Distance:0}→{v[^1].Distance:0} m of {line.Length:0}, rise {rise:0} m, grade {maxGrade:0.00}, exit {prof.Speed[^1]:0} m/s{(ok ? "" : " FAIL")}]";
        }
        report.Add("optional lines are traversable, joined and distinct", allOk, $"{def.OptionalLines.Count} lines{detail}");
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
    private void Validate(RouteSkeleton route, RouteSpeedProfile profile, StageHeightField field, ValidationReport report)
    {
        var v = route.Vertices;
        float entryX = -WorldScale.FootprintLength * 0.5f + WorldScale.EntryMargin;
        float exitX = WorldScale.FootprintLength * 0.5f - WorldScale.ExitMargin;

        report.Add("start and exit exist", v.Count >= 2
            && Mathf.Abs(v[0].Position.X - entryX) < 1f && Mathf.Abs(v[^1].Position.X - exitX) < 1f,
            $"start x={v[0].Position.X:0} exit x={v[^1].Position.X:0}");

        float maxGap = 0f, maxBand = 0f;
        bool monotonic = true;
        for (int i = 1; i < v.Count; i++)
        {
            // Plan spacing: a module's rim face is a real drop between two samples, not a break in the route.
            maxGap = Mathf.Max(maxGap, new Vector2(v[i].Position.X - v[i - 1].Position.X, v[i].Position.Z - v[i - 1].Position.Z).Length());
            if (v[i].Position.X < v[i - 1].Position.X - 1e-3f) monotonic = false;
        }
        foreach (var p in v) maxBand = Mathf.Max(maxBand, Mathf.Abs(p.Position.Z));
        report.Add("primary route is continuous", maxGap <= WorldScale.RouteSampleSpacing * 1.5f, $"max vertex gap {maxGap:0.00} m");
        report.Add("route always progresses toward the exit", monotonic);
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
                if (f.Kind != RouteFeatureKind.LaunchCrest && d >= f.CentreDistance - WorldScale.RouteSampleSpacing * 2f && d <= f.FeatureEnd + WorldScale.RouteSampleSpacing * 2f)
                    return true;
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
            crestsClear &= ok;
            crestDetail += $" [{f.Wavelength:0}/{f.Height:0} at {f.CentreDistance:0} m: {speed:0} m/s {(f.IsLaunch ? "launch" : "roll")}, landing {f.LandingDistance:0} m{(ok ? "" : " OVERRUNS")}]";
        }
        report.Add("launch crests keep a straight landing run", crestsClear, $"{route.Features.Count(f => f.Kind == RouteFeatureKind.LaunchCrest)} crests{crestDetail}");

        float startFlat = PadFlatness(field, v[0].Position), exitFlat = PadFlatness(field, v[^1].Position);
        report.Add("start and exit pads are flat", startFlat < 1.0f && exitFlat < 1.0f, $"height spread start {startFlat:0.00} m, exit {exitFlat:0.00} m");
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

    /// <summary>Metres either side of a module's launch point within which a flight is the module's own.</summary>
    private const float ModuleLaunchWindow = 12f;

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
