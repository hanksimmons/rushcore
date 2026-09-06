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
    private readonly RouteSkeletonBuilder _routes;

    public StageGenerator(MovementTuning movement)
    {
        _speed = new RouteSpeedModel(movement);
        _routes = new RouteSkeletonBuilder(_speed);
        _gravity = movement.Gravity;
    }

    public RouteSpeedModel SpeedModel => _speed;

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
        for (int i = 0; i < route.Vertices.Count; i++)
        {
            var v = route.Vertices[i];
            v.Position.Y = field.Sample(v.Position.X, v.Position.Z);
            route.Vertices[i] = v;
        }
        report.Timings.Add(("relief + corridor", sw.Elapsed.TotalMilliseconds)); sw.Restart();

        RouteSpeedProfile profile = _speed.Integrate(route.Polyline());
        report.Timings.Add(("speed profile", sw.Elapsed.TotalMilliseconds)); sw.Restart();

        Validate(route, profile, field, report);
        report.Timings.Add(("validation", sw.Elapsed.TotalMilliseconds));

        return new StageDefinition(request, route, profile, report) { RouteSeedUsed = routeSeed, HeightField = field };
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
            maxGap = Mathf.Max(maxGap, v[i].Position.DistanceTo(v[i - 1].Position));
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
        float maxGrade = 0f, maxDelta = 0f, prevGrade = 0f;
        for (int i = 1; i < v.Count; i++)
        {
            float ds = v[i].Distance - v[i - 1].Distance;
            if (ds <= 1e-3f) continue;
            float grade = (v[i].Position.Y - v[i - 1].Position.Y) / ds;
            maxGrade = Mathf.Max(maxGrade, Mathf.Abs(grade));
            if (i > 1) maxDelta = Mathf.Max(maxDelta, Mathf.Abs(grade - prevGrade));
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
            // Flight to fall the crest height on the far side at the arrival speed (04 §10).
            f.LandingDistance = f.IsLaunch ? speed * Mathf.Sqrt(2f * f.Height / Mathf.Max(0.001f, _gravity)) : 0f;
            float straightEnd = v[f.EndIndex].Distance;
            bool ok = f.CentreDistance + f.Wavelength * 0.5f + f.LandingDistance <= straightEnd + 1e-3f;
            crestsClear &= ok;
            crestDetail += $" [{f.Wavelength:0}/{f.Height:0} at {f.CentreDistance:0} m: {speed:0} m/s {(f.IsLaunch ? "launch" : "roll")}, landing {f.LandingDistance:0} m{(ok ? "" : " OVERRUNS")}]";
        }
        report.Add("launch crests keep a straight landing run", crestsClear, $"{route.Features.Count} crests{crestDetail}");

        float startFlat = PadFlatness(field, v[0].Position), exitFlat = PadFlatness(field, v[^1].Position);
        report.Add("start and exit pads are flat", startFlat < 1.0f && exitFlat < 1.0f, $"height spread start {startFlat:0.00} m, exit {exitFlat:0.00} m");
    }

    private readonly float _gravity;

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
