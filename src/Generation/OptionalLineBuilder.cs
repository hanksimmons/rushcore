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

    /// <summary>Geometry of one kind of offset line: a ridge (D-100) or a terrace floor (D-103).</summary>
    private readonly record struct LineShape(RouteLineKind Kind, int Floor, float Offset, float Transition, float Ramp, float HeightMin, float HeightMax)
    {
        public float Length => 2f * Transition + 2f * Ramp + 80f;
        /// <summary>A terminal line (D-105): the leaving S, the climb, the widening, a level run and the pad's flat.</summary>
        public float TerminalLength(float spread) => Transition + Ramp + spread + WorldScale.ExitLineRun + WorldScale.PadRadius * 2.5f;
    }

    private static readonly LineShape Ridge = new(RouteLineKind.Ridge, 1, WorldScale.RidgeOffset, WorldScale.RidgeTransition, WorldScale.RidgeRampLength, WorldScale.RidgeHeightMin, WorldScale.RidgeHeightMax);
    private static readonly LineShape Floor2 = new(RouteLineKind.Terrace, 2, WorldScale.Floor2Offset, WorldScale.Floor2Transition, WorldScale.Floor2Ramp, WorldScale.FloorStep, WorldScale.FloorStep);
    private static readonly LineShape Floor3 = new(RouteLineKind.Terrace, 3, WorldScale.Floor3Offset, WorldScale.Floor3Transition, WorldScale.Floor3Ramp, 2f * WorldScale.FloorStep, 2f * WorldScale.FloorStep);

    public static List<RouteSkeleton> Build(RouteSkeleton primary, ulong stageSeed, ArchetypeRules? rules = null)
    {
        rules ??= ArchetypeRules.RollingHighlands;
        var rng = new SeededRandom(SeedChain.Derive(stageSeed, "optional"));
        var lines = new List<RouteSkeleton>();
        var v = primary.Vertices;
        var turnSign = TurnSigns(primary);
        // Sky Terraces (D-103): the lines are terrace floors: a section carries floor 2 and, where its longer climb fits,
        // floor 3 stacked beyond it (floor 2's outer edge is then the cliff up to floor 3).
        int floorsWanted = rules.Floors;

        // A line shadows a bend, or a run of up to three (04 §6): it leaves the primary on the straight before the
        // first and rejoins on the straight after the last, both transitions wholly on straights (D-100: a transition
        // through a banked bend rides the berm and launches, and D-103 found the S's own curvature adding to a bend's
        // into a corner the cap cannot hold), the plateau on the outside; the ramps lie outside the transitions.
        float lastEnd = 500f - WorldScale.OptionalLineSpacing;   // leave the start alone
        var bends = primary.Bends;
        float stop = primary.Spiral is { } pit ? pit.ApproachDistance - 100f : primary.Length - 400f;
        // Terminal lines (D-105) claim their spans first, from their own stream; rejoining lines fill around them on the
        // same side (the other side is free: the stamps never meet, and a bend keeps a line on its outside anyway).
        // On Sky Terraces the floors come first (a floor-3 stack needs the sections terminals would take): its terminal
        // terraces fill around them instead.
        var exitRng = new SeededRandom(SeedChain.Derive(stageSeed, "exits"));
        float terminalStop = primary.Spiral is not null ? stop : primary.Length - 200f;
        var terminals = rules.Floors >= 2 ? new List<RouteSkeleton>() : PlaceTerminals(primary, rules, exitRng, turnSign, terminalStop, lines);
        bool Occupied(float from, float to, float side) => terminals.Any(t => t.Side == side && from < v[t.JoinEnd].Distance + 100f && to > v[t.JoinStart].Distance - 100f);
        for (int k = 0; k < bends.Count && lines.Count < WorldScale.OptionalLinesMax; k++)
        {
            float bs = v[bends[k].StartIndex].Distance;
            float ps = k == 0 ? 0f : v[bends[k - 1].EndIndex].Distance;
            bool placed = false;
            // The tallest stack first (a floor-3 section carries its floor 2), then a lone floor 2, or the ridge.
            for (int tier = floorsWanted >= 3 ? 3 : floorsWanted >= 2 ? 2 : 1; tier >= 1 && !placed; tier--)
            for (int extra = 0; extra <= 2 && k + extra < bends.Count && !placed; extra++)
            {
                if (floorsWanted >= 2 && tier == 1) break;
                var shape = tier == 3 ? Floor3 : tier == 2 ? Floor2 : Ridge;
                float T = shape.Transition, L = shape.Length;
                float beLast = v[bends[k + extra].EndIndex].Distance;
                float ne = k + extra + 1 < bends.Count ? v[bends[k + extra + 1].StartIndex].Distance : primary.Length;
                float dLo = Mathf.Max(Mathf.Max(ps, beLast + T - L), lastEnd + WorldScale.OptionalLineSpacing);
                float dHi = Mathf.Min(bs - T, Mathf.Min(ne - L, stop - L));
                if (dHi < dLo) continue;
                float d = 0.5f * (dLo + dHi), dEnd = d + L;
                int a = primary.IndexAtDistance(d), b = primary.IndexAtDistance(dEnd);
                // A terrace prefers the side toward the axis (its long falloff needs the room); a ridge picks at random.
                float first = shape.Kind == RouteLineKind.Terrace ? -Mathf.Sign(v[a].Position.Z + v[b].Position.Z + 1e-3f) : rng.Sign();
                float Offset(int i) => shape.Offset * Bump(v[i].Distance - v[a].Distance, v[b].Distance - v[a].Distance, shape.Transition);
                float side = SideValid(primary, turnSign, a, b, first, Offset) ? first : SideValid(primary, turnSign, a, b, -first, Offset) ? -first : 0f;
                if (side == 0f || OverlapsFeature(primary, d, dEnd) || !JoinsOnStraights(primary, a, b, T) || Occupied(d, dEnd, side)) continue;
                var line = BuildLine(primary, a, b, side, shape, rng.Range(shape.HeightMin, shape.HeightMax));
                if (line is null) continue;
                if (tier == 3)
                {
                    // Floor 2 under floor 3 on the same section: its outer edge is the cliff up to floor 3.
                    var lower = BuildLine(primary, a, b, side, Floor2, WorldScale.FloorStep);
                    if (lower is null) continue;
                    lower.OuterFalloff = WorldScale.TerraceCliffFalloff;
                    lines.Add(lower);
                }
                lines.Add(line);
                lastEnd = dEnd;
                placed = true;
            }
        }
        if (rules.Floors >= 2) terminals = PlaceTerminals(primary, rules, exitRng, turnSign, terminalStop, lines);
        lines.AddRange(terminals);
        return lines;
    }

    /// <summary>
    /// Branching exits (D-105): up to <see cref="WorldScale.ExitLinesMax"/> terminal lines in the last
    /// <see cref="WorldScale.ExitZoneLength"/> of the primary. Each leaves like a ridge (its S on a straight, its climb clear
    /// of every feature), widens to <see cref="WorldScale.ExitLineOffset"/> once on the plateau, runs level and ends on its
    /// own exit pad. The two lie on opposite sides, so their forks may overlap in route distance (the stamps never meet);
    /// their forks stay <see cref="WorldScale.ExitForkSpacing"/> apart so each reads as its own choice. The pad must sit inside
    /// the footprint and, on a pit stage, clear of the disc. Entry is the drive-in ramp: choosing an exit is steering.
    /// </summary>
    private static List<RouteSkeleton> PlaceTerminals(RouteSkeleton primary, ArchetypeRules rules, SeededRandom rng, float[] turnSign, float stop, IReadOnlyList<RouteSkeleton> existing)
    {
        var lines = new List<RouteSkeleton>();
        var v = primary.Vertices;
        bool Taken(float from, float to, float side) => existing.Any(o => o.Side == side && from < v[o.JoinEnd].Distance + 100f && to > v[o.JoinStart].Distance - 100f);
        // A sky exit is a floor-2 terrace ending on a pad (its cliff drains onto the primary); elsewhere a ridge.
        var shape = rules.Floors >= 2 ? Floor2 : Ridge;
        float spread = rules.ExitSpread;
        float T = shape.Transition, L = shape.TerminalLength(spread);
        // Only the fork must be clear of features: past the S the line's path is outside the primary's stamp, so no
        // feature can leak into it (the D-100 rule for a whole rejoining section guards its return S as well).
        float clear = T + 50f;
        // The zone ends where the terminals must (before a pit's approach on a pit stage), so a pit stage's exits fork
        // before the set-piece: taking one skips the finale.
        float zoneStart = Mathf.Max(500f, stop - WorldScale.ExitZoneLength);
        float side = rng.Sign();
        var forks = new List<float>();
        for (int n = 0; n < WorldScale.ExitLinesMax; n++, side = -side)
        {
            RouteSkeleton? best = null;
            int tried = 0;
            for (float d = zoneStart; d + L <= stop && best is null; d += 50f)
            {
                tried++;
                if (forks.Any(f => Mathf.Abs(f - d) < WorldScale.ExitForkSpacing)) { Tally("fork spacing"); continue; }
                int a = primary.IndexAtDistance(d), b = primary.IndexAtDistance(d + L);
                if (b >= v.Count - 1) { Tally("end"); continue; }
                if (OverlapsFeature(primary, d, d + clear)) { Tally("feature"); continue; }
                if (Taken(d, d + L, side)) { Tally("line on that side"); continue; }
                if (!LeavesOnStraight(primary, a, T)) { Tally("bend at fork"); continue; }
                float Offset(int i) => TerminalOffset(v[i].Distance - v[a].Distance, shape, spread);
                if (!SideValid(primary, turnSign, a, b, side, Offset)) { Tally("inside of a bend"); continue; }
                var line = BuildLine(primary, a, b, side, shape, rng.Range(shape.HeightMin, shape.HeightMax), spread);
                if (line is null) { Tally("band"); continue; }
                if (!PadClear(primary, line)) { Tally("pad"); continue; }
                best = line;
                forks.Add(d);
            }
            if (tried == 0) Tally("no window");
            if (best is null) continue;
            lines.Add(best);
        }
        return lines;
    }

    public static readonly Dictionary<string, int> TerminalTally = new();
    private static void Tally(string reason) { if (System.Environment.GetEnvironmentVariable("RUSHCORE_EXIT_TRACE") == "1") TerminalTally[reason] = TerminalTally.GetValueOrDefault(reason) + 1; }

    /// <summary>The exit pad lies inside the footprint with its radius to spare and, on a pit stage, outside the disc.</summary>
    private static bool PadClear(RouteSkeleton primary, RouteSkeleton line)
    {
        var e = line.Vertices[^1].Position;
        if (Mathf.Abs(e.X) > WorldScale.FootprintLength * 0.5f - WorldScale.PadRadius || Mathf.Abs(e.Z) > WorldScale.FootprintWidth * 0.5f - WorldScale.PadRadius) return false;
        if (primary.Spiral is { } pit)
            foreach (var lv in line.Vertices)
                if (new Vector2(lv.Position.X - pit.Centre.X, lv.Position.Z - pit.Centre.Z).Length() < pit.OuterRadius + WorldScale.PadRadius) return false;
        return true;
    }

    /// <summary>The leaving transition lies on a primary straight (the first half of <see cref="JoinsOnStraights"/>).</summary>
    private static bool LeavesOnStraight(RouteSkeleton primary, int a, float transition)
    {
        var v = primary.Vertices;
        for (int i = a; i < v.Count && v[i].Distance <= v[a].Distance + transition; i++) if (v[i].Kind == RouteSegmentKind.Bend) return false;
        return true;
    }

    /// <summary>Terminal offset envelope: the ridge's S out to its offset, then the widening S to the exit offset after the climb.</summary>
    private static float TerminalOffset(float d, LineShape shape, float spread) =>
        shape.Offset * Mathf.SmoothStep(0f, shape.Transition, d)
        + (spread > 0f ? (WorldScale.ExitLineOffset - shape.Offset) * Mathf.SmoothStep(shape.Transition + shape.Ramp, shape.Transition + shape.Ramp + spread, d) : 0f);

    /// <summary>Terminal plateau envelope: the climb once the line has left the primary, then level to the pad.</summary>
    public static float TerminalPlateau(float d, float transition, float ramp) => CosineStep(transition, transition + ramp, d);

    /// <summary>The offset line between two primary vertices, or null if it leaves the optional band (a terrace's long
    /// falloff counts toward the band).</summary>
    /// <param name="terminalSpread">Non-negative for a terminal line (D-105): its widening beyond the shape's offset; negative for a rejoining line.</param>
    private static RouteSkeleton? BuildLine(RouteSkeleton primary, int a, int b, float side, LineShape shape, float height, float terminalSpread = -1f)
    {
        bool terminal = terminalSpread >= 0f;
        var v = primary.Vertices;
        {
            var line = new RouteSkeleton
            {
                Kind = shape.Kind,
                Floor = shape.Floor,
                JoinStart = a,
                JoinEnd = b,
                Terminal = terminal,
                CorridorHalfWidth = WorldScale.MinCorridorWidth * 0.5f,
                RidgeHeight = height,
                Offset = shape.Offset, Transition = shape.Transition, RampLength = shape.Ramp,
                Side = side,
                InnerFalloff = shape.Kind == RouteLineKind.Terrace ? WorldScale.TerraceCliffFalloff : 0f,
                OuterFalloff = shape.Kind == RouteLineKind.Terrace ? WorldScale.TerraceOuterFalloff(height) : 0f,
            };
            float sectionLength = v[b].Distance - v[a].Distance;
            // A terrace's own falloff may run into the scenery margin; its level width may not.
            float bandRoom = shape.Kind == RouteLineKind.Terrace ? WorldScale.MinCorridorWidth * 0.5f + WorldScale.WallSetback + 100f : 0f;
            for (int i = a; i <= b; i++)
            {
                float offset = terminal ? TerminalOffset(v[i].Distance - v[a].Distance, shape, terminalSpread)
                                        : shape.Offset * Bump(v[i].Distance - v[a].Distance, sectionLength, shape.Transition);
                float h = v[i].Heading;
                var p = v[i].Position + new Vector3(-Mathf.Sin(h), 0f, Mathf.Cos(h)) * (offset * side);
                if (Mathf.Abs(p.Z) + bandRoom > WorldScale.OptionalBandHalfWidth) return null;
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
    /// <summary>The same for any offset envelope (a tube's, D-101), given the offset at each primary vertex.</summary>
    internal static bool SideValid(RouteSkeleton primary, float[] turnSign, int a, int b, float side, Func<int, float> offsetAt)
    {
        var v = primary.Vertices;
        for (int i = a; i <= b; i++)
        {
            if (turnSign[i] == 0f) continue;
            bool inside = side == turnSign[i];
            if (!inside) continue;
            if (offsetAt(i) > v[i].Radius - WorldScale.InsideOffsetMargin) return false;
        }
        return true;
    }

    /// <summary>Turn sense per primary vertex: ±1 inside a bend, 0 on straights.</summary>
    internal static float[] TurnSigns(RouteSkeleton primary)
    {
        var turnSign = new float[primary.Vertices.Count];
        foreach (var b in primary.Bends)
            for (int i = b.StartIndex; i <= b.EndIndex; i++) turnSign[i] = Mathf.Sign(b.TurnAngle);
        return turnSign;
    }

    /// <summary>Both transitions lie on primary straights (the line leaves before the bend it shadows and rejoins
    /// after it): a transition crossing a banked bend rides the berm, whose full height reaches into the falloff
    /// the transition crosses, and that bump launched the base kit into the transition's own turn (D-100).</summary>
    private static bool JoinsOnStraights(RouteSkeleton primary, int a, int b, float transition)
    {
        var v = primary.Vertices;
        for (int i = a; i < v.Count && v[i].Distance <= v[a].Distance + transition; i++) if (v[i].Kind == RouteSegmentKind.Bend) return false;
        for (int i = b; i >= 0 && v[i].Distance >= v[b].Distance - transition; i--) if (v[i].Kind == RouteSegmentKind.Bend) return false;
        return true;
    }

    // ---------------------------------------------------------------- T5 measurement (read-only)

    /// <summary>
    /// T5 instrument, read-only: could a floor 3 branch off this floor-2 section under the shipped
    /// <see cref="Floor3"/> numbers, taking off after the floor 2's leaving transition rather than sharing its span?
    /// That is D-103's named candidate, and it matters more since D-105 made the sections contested.
    ///
    /// <para>Asks exactly the three questions the placement loop asks: is there room on the primary after the floor-2
    /// transition for a floor-3 length, do both of its own transitions lie on straights, is the side valid, and does
    /// it clear every feature. Builds nothing and decides nothing — <paramref name="why"/> names the first question
    /// that said no.</para>
    /// </summary>
    internal static bool Floor3CouldBranch(RouteSkeleton primary, RouteSkeleton floor2, out string why, out float roomMetres)
    {
        why = "";
        var v = primary.Vertices;
        float stop = primary.Spiral is { } pit ? pit.ApproachDistance - 100f : primary.Length - 400f;
        float d = v[floor2.JoinStart].Distance + floor2.Transition;
        float L = Floor3.Length;
        roomMetres = stop - d;
        if (roomMetres < L) { why = $"room {roomMetres:0} m < floor-3 length {L:0} m"; return false; }

        int a = primary.IndexAtDistance(d), b = primary.IndexAtDistance(d + L);
        if (b >= v.Count - 1) { why = "runs past the last vertex"; return false; }
        float Offset(int i) => Floor3.Offset * Bump(v[i].Distance - v[a].Distance, v[b].Distance - v[a].Distance, Floor3.Transition);
        if (!JoinsOnStraights(primary, a, b, Floor3.Transition)) { why = "a transition crosses a bend"; return false; }
        if (!SideValid(primary, TurnSigns(primary), a, b, floor2.Side, Offset)) { why = "inside of a bend on the floor 2's side"; return false; }
        if (OverlapsFeature(primary, d, d + L)) { why = "overlaps a feature"; return false; }
        return true;
    }

    /// <summary>T5 instrument, read-only: the shipped floor-3 shape's length, for the room table.</summary>
    internal static float Floor3Length => Floor3.Length;

    /// <summary>Lateral offset envelope along a line of the given length: 0 at both joins, 1 between the transitions.</summary>
    public static float Bump(float d, float length) => Bump(d, length, WorldScale.RidgeTransition);
    public static float Bump(float d, float length, float transition) =>
        Mathf.SmoothStep(0f, transition, d) * (1f - Mathf.SmoothStep(length - transition, length, d));

    /// <summary>Plateau envelope: climbs once the line has left the primary, drops back before it rejoins.
    /// Cosine ramps: their knee curvature (π² H / 2L²) is 18% under a smoothstep's, and with each line's height and
    /// ramp length the knee radius clears the cap's contact radius, so the climb never launches.</summary>
    public static float Plateau(float d, float length, float transition, float ramp)
    {
        float up0 = transition;
        float down1 = length - transition;
        return CosineStep(up0, up0 + ramp, d) * (1f - CosineStep(down1 - ramp, down1, d));
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
