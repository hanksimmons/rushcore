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

    private StageDefinition Build(StageGenerationRequest request, ulong routeSeed, int attempt, bool straight = false)
    {
        var report = new ValidationReport { Attempts = attempt };
        var sw = Stopwatch.StartNew();

        RouteSkeleton route = straight ? StraightRoute() : _routes.Build(routeSeed);
        report.Timings.Add(("route skeleton", sw.Elapsed.TotalMilliseconds)); sw.Restart();

        RouteSpeedProfile profile = _speed.Integrate(route.Polyline());
        report.Timings.Add(("speed profile", sw.Elapsed.TotalMilliseconds)); sw.Restart();

        Validate(route, profile, report);
        report.Timings.Add(("validation", sw.Elapsed.TotalMilliseconds));

        return new StageDefinition(request, route, profile, report) { RouteSeedUsed = routeSeed };
    }

    /// <summary>Mandatory and secondary checks that apply to a skeleton (04 §12); more join with each slice.</summary>
    private void Validate(RouteSkeleton route, RouteSpeedProfile profile, ValidationReport report)
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
