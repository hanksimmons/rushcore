using Godot;
using Rushcore.World;

namespace Rushcore.Generation;

/// <summary>
/// Stage height source (04 §5B–D, §6): analytic long swells whose summed crest curvature and slope
/// are budgeted so a cruising ball keeps contact (04 §8) and the corridor stays under the route grade
/// limit, bounded micro relief (04 §5C), the archetype's own relief on top (a canyon's raised side
/// terrain, a dune sea's wave), and the guaranteed corridor stamped along the primary route: level
/// across, smoothed along, banked on bends, launch crests (or dune trains riding the wave) on feature
/// straights, flat start/exit pads. Pure data; render and collision both sample it (04 §9).
/// </summary>
public sealed class StageHeightField : IHeightSource
{
    public const float CorridorHalfWidth = WorldScale.TypicalCorridorWidth * 0.5f;   // 75 m → 150 m corridor
    public const float BendExtraHalfWidth = 25f;
    public const float FalloffWidth = 120f;
    /// <summary>Along-route smoothing half-window for the corridor profile.</summary>
    public const float SmoothingHalfWindow = 150f;
    public const float BankFade = 60f;
    /// <summary>Share of the cruise crest-curvature budget the swells may use; the rest is for relief.</summary>
    private const float SwellCurvatureShare = 0.7f;
    private const float CoarseCell = 16f;
    private const int RefineSpan = 12;

    private readonly ArchetypeRules _rules;
    /// <summary>Side terrain above the corridor floor (a canyon's walls); 0 on open landscapes.</summary>
    public float WallHeight { get; }

    private readonly struct Swell
    {
        public readonly float Kx, Kz, Amp, Phase;
        public Swell(float kx, float kz, float amp, float phase) { Kx = kx; Kz = kz; Amp = amp; Phase = phase; }
    }

    private readonly Swell[] _swells;
    /// <summary>A dune sea's wave (D-099), the skeleton's; zero elsewhere.</summary>
    private readonly DuneWave _dunes;
    /// <summary>A canyon's spiral pit (D-102), the skeleton's; null elsewhere.</summary>
    private readonly SpiralPit? _spiral;
    private readonly ulong _noiseSeedA, _noiseSeedB;
    private readonly float _noiseAmpA, _noiseAmpB;

    /// <summary>One stamped line: flattened route arrays, its corridor profile, banks and a coarse nearest map.</summary>
    private sealed class StampedLine
    {
        public readonly int N;
        public readonly float[] X, Z, H, Heading, BankHeight, BankSide;
        /// <summary>Extra half-width on bends, faded in and out with the bank so the wall line never jogs at a bend's ends.</summary>
        public readonly float[] Extra;
        public readonly bool[] Bend;
        public readonly float HalfWidth;
        /// <summary>Offset lines (D-103): the side the primary lies toward (−Side) and the falloffs toward and away from it.</summary>
        private readonly float _side, _innerFalloff2, _outerFalloff2;
        public readonly int Cx, Cz;
        public readonly int[] Coarse;
        /// <summary>Vertex indices sorted by X: the coarse map scans an X window whatever order the route visits it in (D-096: no monotonic-X assumption).</summary>
        private readonly int[] _byX;
        private readonly float _wallFalloff, _insideFalloff;

        public StampedLine(RouteSkeleton route, float[] profile, float sizeX, float sizeZ, ArchetypeRules rules)
        {
            _wallFalloff = rules.WallFalloff;
            _insideFalloff = rules.InsideFalloff;
            var v = route.Vertices;
            N = v.Count;
            X = new float[N]; Z = new float[N]; H = profile; Heading = new float[N];
            BankHeight = new float[N]; BankSide = new float[N]; Bend = new bool[N]; Extra = new float[N];
            HalfWidth = route.CorridorHalfWidth;
            _side = route.Side;
            _innerFalloff2 = route.InnerFalloff > 0f ? route.InnerFalloff : rules.WallFalloff;
            _outerFalloff2 = route.OuterFalloff > 0f ? route.OuterFalloff : rules.WallFalloff;
            for (int i = 0; i < N; i++)
            {
                X[i] = v[i].Position.X; Z[i] = v[i].Position.Z; Heading[i] = v[i].Heading;
                Bend[i] = v[i].Kind == RouteSegmentKind.Bend;
            }
            foreach (var b in route.Bends)
            {
                float height = Mathf.Min(WorldScale.MaxBankHeight, WorldScale.BankHeightPerRadius * b.Radius * rules.BankScale);
                float side = -Mathf.Sign(b.TurnAngle);                    // outward is opposite the turn
                float startD = v[b.StartIndex].Distance, endD = v[b.EndIndex].Distance;
                float routeEnd = v[N - 1].Distance;
                for (int i = b.StartIndex; i <= b.EndIndex; i++)
                {
                    float d = v[i].Distance;
                    float fade = Mathf.Min(Mathf.SmoothStep(0f, BankFade, d - startD), Mathf.SmoothStep(0f, BankFade, endD - d));
                    // No berm on the pads: a route ending on a bend (the pit floor, D-102) still lands on level ground.
                    fade *= Mathf.SmoothStep(WorldScale.PadRadius, WorldScale.PadRadius * 2.5f, routeEnd - d) * Mathf.SmoothStep(WorldScale.PadRadius, WorldScale.PadRadius * 2.5f, d);
                    BankHeight[i] = height * fade;
                    BankSide[i] = side;
                    Extra[i] = BendExtraHalfWidth * fade;
                }
            }

            // Coarse nearest map: each cell scans the vertices whose X lies within reach, in X order.
            _byX = Enumerable.Range(0, N).OrderBy(i => X[i]).ToArray();
            Cx = Mathf.CeilToInt(sizeX / CoarseCell) + 1;
            Cz = Mathf.CeilToInt(sizeZ / CoarseCell) + 1;
            Coarse = new int[Cx * Cz];
            float reach = HalfWidth + BendExtraHalfWidth + WorldScale.WallSetback + Mathf.Max(Mathf.Max(_wallFalloff, _insideFalloff), Mathf.Max(_innerFalloff2, _outerFalloff2)) + CoarseCell * 2f;
            for (int cz = 0; cz < Cz; cz++)
            {
                float z = cz * CoarseCell - sizeZ * 0.5f;
                for (int cx = 0; cx < Cx; cx++)
                {
                    float x = cx * CoarseCell - sizeX * 0.5f;
                    int lo = LowerBoundX(x - reach), hi = LowerBoundX(x + reach);
                    int best = -1; float bestD = float.MaxValue;
                    for (int k = lo; k < hi; k++)
                    {
                        int i = _byX[k];
                        float dx = X[i] - x, dz = Z[i] - z, dd = dx * dx + dz * dz;
                        if (dd < bestD) { bestD = dd; best = i; }
                    }
                    Coarse[cz * Cx + cx] = best;
                }
            }
        }

        private int LowerBoundX(float x)
        {
            int lo = 0, hi = N;
            while (lo < hi) { int mid = (lo + hi) >> 1; if (X[_byX[mid]] < x) lo = mid + 1; else hi = mid; }
            return lo;
        }

        public int Nearest(float x, float z, float sizeX, float sizeZ, out float distance)
        {
            int cx = Mathf.Clamp((int)((x + sizeX * 0.5f) / CoarseCell + 0.5f), 0, Cx - 1);
            int cz = Mathf.Clamp((int)((z + sizeZ * 0.5f) / CoarseCell + 0.5f), 0, Cz - 1);
            int seed = Coarse[cz * Cx + cx];
            if (seed < 0) { distance = float.MaxValue; return -1; }
            int best = -1; float bestD = float.MaxValue;
            int lo = Mathf.Max(0, seed - RefineSpan), hi = Mathf.Min(N - 1, seed + RefineSpan);
            for (int i = lo; i <= hi; i++)
            {
                float dx = X[i] - x, dz = Z[i] - z, dd = dx * dx + dz * dz;
                if (dd < bestD) { bestD = dd; best = i; }
            }
            distance = Mathf.Sqrt(bestD);
            return best;
        }

        public float Width(int i) => HalfWidth + Extra[i];

        /// <summary>Lateral offset of a point from the line at vertex i, positive on the outside of a bend (or the left on a straight).</summary>
        public float Lateral(int i, float x, float z)
        {
            float lx = -Mathf.Sin(Heading[i]), lz = Mathf.Cos(Heading[i]);
            float lateral = (x - X[i]) * lx + (z - Z[i]) * lz;
            return BankSide[i] != 0f ? lateral * BankSide[i] : lateral;
        }

        /// <summary>Corridor weight at a point d metres from the line at vertex i: 1 inside the level width plus the
        /// wall setback, then falling over the archetype's falloff, the long one on the inside of a bend (blind corners).</summary>
        public float Weight(int i, float x, float z, float d)
        {
            float edge = Width(i) + WorldScale.WallSetback;
            float lateral = Lateral(i, x, z);
            // An offset line's falloffs are per side (toward the primary or away, D-103); the primary's follow the bend.
            float falloff = _side != 0f && !Bend[i] ? (lateral * _side < 0f ? _innerFalloff2 : _outerFalloff2)
                          : BankSide[i] != 0f && lateral < 0f ? _insideFalloff : _wallFalloff;
            return 1f - Mathf.SmoothStep(edge, edge + falloff, d);
        }

        /// <summary>Corridor height at a point near vertex i: the profile interpolated along the route
        /// toward whichever neighbour the point lies toward (D-093: a nearest-vertex height was a 4 m
        /// staircase that a 4 m grid resolves into flat treads and double-grade risers), level across,
        /// plus the outer-half bank.</summary>
        public float Height(int i, float x, float z)
        {
            float dx = x - X[i], dz = z - Z[i];
            int j = i + 1 < N ? i + 1 : i - 1;
            if (j == i + 1 && i > 0 && dx * (X[j] - X[i]) + dz * (Z[j] - Z[i]) < 0f) j = i - 1;
            float t = 0f;
            if (j >= 0)
            {
                float ex = X[j] - X[i], ez = Z[j] - Z[i], len2 = ex * ex + ez * ez;
                if (len2 > 1e-6f) t = Mathf.Clamp((dx * ex + dz * ez) / len2, 0f, 1f);
            }
            else j = i;
            float h = Mathf.Lerp(H[i], H[j], t);
            float bank = Mathf.Lerp(BankHeight[i], BankHeight[j], t);
            if (bank > 0f)
            {
                float lx = -Mathf.Sin(Heading[i]), lz = Mathf.Cos(Heading[i]);
                float lateral = (dx * lx + dz * lz) * (BankSide[i] != 0f ? BankSide[i] : BankSide[j]);
                h += bank * Mathf.Clamp(lateral / Width(i), 0f, 1f);
            }
            return h;
        }
    }

    private readonly StampedLine _primary;
    private readonly List<StampedLine> _lines = new();
    /// <summary>Plan positions of every exit pad (D-105): the primary's and each terminal line's; coloured like the primary's.</summary>
    private readonly List<Vector2> _exitPads = new();
    private readonly float[] _primaryProfile;
    /// <summary>The primary's smoothed profile before any feature or module stamp: optional lines ride this,
    /// so a ridge beside a gap or a ramp does not carry a copy of it (D-097).</summary>
    private float[] _primaryBase = System.Array.Empty<float>();
    private readonly RouteSkeleton _primaryRoute;

    public float SizeX => WorldScale.FootprintLength;
    public float SizeZ => WorldScale.FootprintWidth;
    public Vector3 SpawnXZ { get; }
    public Vector3 SpawnFacing { get; }
    /// <summary>Summed crest curvature of the swell layer; contact at the cap needs ≤ 1 / cruise crest radius.</summary>
    public float SwellCurvature { get; }

    public StageHeightField(RouteSkeleton route, ulong stageSeed, ArchetypeRules? rules = null)
    {
        _rules = rules ?? ArchetypeRules.RollingHighlands;
        _dunes = route.Dunes;
        _spiral = route.Spiral;
        var rng = new SeededRandom(SeedChain.Derive(stageSeed, "relief"));
        WallHeight = _rules.WallHeightMax > 0f ? rng.Range(_rules.WallHeightMin, _rules.WallHeightMax) : 0f;

        // ---- B: long swells, budgeted so the base landscape never launches a cruising ball ----
        _swells = new Swell[3];
        float curvature = 0f, slope = 0f;
        for (int i = 0; i < _swells.Length; i++)
        {
            float angle = rng.Range(0f, Mathf.Pi);
            float wavelength = rng.Range(WorldScale.LongSwellWavelengthMin, WorldScale.LongSwellWavelengthMax);
            float amp = 0.5f * rng.Range(WorldScale.LongSwellHeightMin, WorldScale.LongSwellHeightMax);
            float k = Mathf.Tau / wavelength;
            _swells[i] = new Swell(k * Mathf.Cos(angle), k * Mathf.Sin(angle), amp, rng.Range(0f, Mathf.Tau));
            curvature += amp * k * k;
            slope += amp * k;
        }
        // Two budgets: crest curvature (contact at the cap) and slope (corridor grade with a crest on top).
        float scale = Mathf.Min(1f, Mathf.Min(SwellCurvatureShare / WorldScale.CruiseCrestRadius / curvature, _rules.SwellMaxSlope / slope));
        if (scale < 1f)
        {
            for (int i = 0; i < _swells.Length; i++)
                _swells[i] = new Swell(_swells[i].Kx, _swells[i].Kz, _swells[i].Amp * scale, _swells[i].Phase);
            curvature *= scale;
        }
        SwellCurvature = curvature;

        // ---- C: micro relief inside the remaining curvature budget (value noise peaks ~1.5× a cosine's) ----
        _noiseSeedA = SeedChain.Derive(stageSeed, "noise", 0);
        _noiseSeedB = SeedChain.Derive(stageSeed, "noise", 1);
        float remaining = (1f - SwellCurvatureShare) / WorldScale.CruiseCrestRadius;
        _noiseAmpA = 0.6f * remaining / (1.5f * Mathf.Pow(Mathf.Tau / 400f, 2f));   // ≈ 0.9 m at λ 400
        _noiseAmpB = 0.4f * remaining / (1.5f * Mathf.Pow(Mathf.Tau / 120f, 2f));   // ≈ 0.05 m at λ 120

        // ---- D: corridor profile = base relief along the route, smoothed, plus crests and pads. On a dune sea
        // the wave is left out here and put back by the trains' crest stamps, which sit on its crests: a train
        // straight rides the dunes exactly, every other section is the swale between them (a bend on the wave
        // would launch the ceiling ball inside it).
        _primaryRoute = route;
        var v = route.Vertices;
        int n = v.Count;
        var raw = new float[n];
        for (int i = 0; i < n; i++) raw[i] = Base(v[i].Position.X, v[i].Position.Z);
        var rh = new float[n];
        int window = Mathf.Max(1, Mathf.RoundToInt(SmoothingHalfWindow / WorldScale.RouteSampleSpacing));
        var prefix = new float[n + 1];
        for (int i = 0; i < n; i++) prefix[i + 1] = prefix[i] + raw[i];
        for (int i = 0; i < n; i++)
        {
            int lo = Mathf.Max(0, i - window), hi = Mathf.Min(n - 1, i + window);
            rh[i] = (prefix[hi + 1] - prefix[lo]) / (hi - lo + 1);
        }
        // Modules (04 §5E, D-097) are stamped on the corridor profile as it slopes (≤ 0.18 by the swell
        // budget): a level module would need eases whose convexity launches a ball before the rim, so
        // the validator reads the real rim heights instead. A gap's exit wall is the gentlest the
        // opening allows (never over the family's 25°), because a ball riding it out leaves like a ramp.
        _primaryBase = (float[])rh.Clone();
        foreach (var f in route.Features)
        {
            if (f.Kind == RouteFeatureKind.LaunchCrest) continue;
            if (f.Kind == RouteFeatureKind.Gap)
                f.Depth = Mathf.Min(f.Depth, (f.Opening - WorldScale.GapRimFace) * WorldScale.GapExitWallMaxSlope);
            for (int i = f.StartIndex; i <= f.EndIndex; i++)
            {
                float rel = v[i].Distance - f.CentreDistance;
                if (f.Kind == RouteFeatureKind.Gap)
                    rh[i] -= f.Depth * Chasm(rel, f.Opening, f.Depth);
                else
                {
                    float run = f.Rise / f.Slope;   // lip height = slope × run, so the ramp meets the lip exactly
                    rh[i] += f.Slope * SmoothClamp(rel + run, run, WorldScale.RampEase) - SmoothClamp(rel, f.Rise, WorldScale.RampBackFaceEase);
                }
            }
        }
        // Crests read the slope under them from the profile before any crest: the crests of a dune train share
        // one straight, and one crest's face is not the ground the next one stands on.
        var preCrest = (float[])rh.Clone();
        foreach (var f in route.Features)
        {
            if (f.Kind != RouteFeatureKind.LaunchCrest) continue;
            // Trim the crest so its slope plus the local corridor slope stays under the route grade limit.
            float localSlope = 0f;
            for (int i = f.StartIndex + 1; i <= f.EndIndex; i++)
            {
                float ds = v[i].Distance - v[i - 1].Distance;
                if (ds > 1e-3f) localSlope = Mathf.Max(localSlope, Mathf.Abs(preCrest[i] - preCrest[i - 1]) / ds);
            }
            float maxHeight = Mathf.Max(0f, (WorldScale.MaxRouteGrade * 0.9f - localSlope) * f.Wavelength / Mathf.Pi);
            f.Height = Mathf.Min(f.Height, maxHeight);
            for (int i = f.StartIndex; i <= f.EndIndex; i++)
            {
                float d = v[i].Distance - f.CentreDistance;
                if (Mathf.Abs(d) <= f.Wavelength * 0.5f)
                    rh[i] += f.Height * 0.5f * (1f + Mathf.Cos(Mathf.Tau * d / f.Wavelength));
            }
        }
        // Spiral pit (D-102): over the straight into the pit the corridor blends from the relief to the entry level, then
        // descends the pit's depth as a perfect helix (a swell under two kilometres of bends would launch the ceiling
        // ball inside them); its turns are terraces cut into the cone the pit surface carries (Sample), the exit pad on
        // the pit floor.
        if (_spiral is { } pit)
        {
            pit.EntryHeight = rh[pit.StartIndex];
            for (int i = 0; i < n; i++)
            {
                float d = v[i].Distance;
                float wIn = Mathf.SmoothStep(pit.ApproachDistance, pit.StartDistance, d);
                float descent = pit.Depth * Mathf.SmoothStep(0f, 1f, Mathf.Clamp((d - pit.StartDistance) / Mathf.Max(1f, pit.Length), 0f, 1f));
                rh[i] = Mathf.Lerp(rh[i], pit.EntryHeight, wIn) - descent;
            }
        }
        float startH = rh[0], exitH = rh[n - 1];
        float total = v[n - 1].Distance;
        for (int i = 0; i < n; i++)
        {
            float d = v[i].Distance;
            float wStart = Mathf.SmoothStep(WorldScale.PadRadius, WorldScale.PadRadius * 2.5f, d);
            float wExit = Mathf.SmoothStep(WorldScale.PadRadius, WorldScale.PadRadius * 2.5f, total - d);
            rh[i] = Mathf.Lerp(startH, rh[i], wStart);
            rh[i] = Mathf.Lerp(exitH, rh[i], wExit);
        }
        _primaryProfile = rh;
        route.Side = 0f;   // the primary has no "toward the primary" side
        _primary = new StampedLine(route, rh, SizeX, SizeZ, _rules);
        _lines.Add(_primary);
        _exitPads.Add(new Vector2(v[n - 1].Position.X, v[n - 1].Position.Z));

        SpawnXZ = new Vector3(v[0].Position.X, 0f, v[0].Position.Z);
        SpawnFacing = new Vector3(Mathf.Cos(v[0].Heading), 0f, Mathf.Sin(v[0].Heading));
    }

    /// <summary>Corridor centreline height of the primary route at a vertex.</summary>
    public float PrimaryHeight(int index) => _primaryProfile[index];
    public ArchetypeRules Rules => _rules;

    /// <summary>
    /// Stamps an optional line. A ridge line rides the primary's profile between its joins, raised
    /// onto a plateau by its ridge height, so both ends meet the primary exactly.
    /// </summary>
    public void AddLine(RouteSkeleton line)
    {
        var v = line.Vertices;
        var pv = _primaryRoute.Vertices;
        var profile = new float[v.Count];
        // The plateau envelope runs in the primary's distance, as the offset envelope does: the line's own distance
        // is longer around the outside of the bend it shadows, and in it the descent slid into the return transition,
        // where the primary's falloff blend squeezed it into a launch (D-100).
        float span = Mathf.Max(1f, pv[line.JoinEnd].Distance - pv[line.JoinStart].Distance);
        for (int i = 0; i < v.Count; i++)
        {
            int pi = Mathf.Clamp(line.JoinStart + i, 0, _primaryProfile.Length - 1);
            float pd = pv[pi].Distance - pv[line.JoinStart].Distance;
            profile[i] = _primaryBase[pi] + line.RidgeHeight * (line.Terminal ? OptionalLineBuilder.TerminalPlateau(pd, line.Transition, line.RampLength)
                                                                                : OptionalLineBuilder.Plateau(pd, span, line.Transition, line.RampLength));
        }
        if (line.Terminal)
        {
            // A terminal line (D-105) ends on its own exit pad: level over the pad's reach, as the primary's is.
            float end = v[^1].Distance, padH = profile[^1];
            for (int i = 0; i < v.Count; i++)
                profile[i] = Mathf.Lerp(padH, profile[i], Mathf.SmoothStep(WorldScale.PadRadius, WorldScale.PadRadius * 2.5f, end - v[i].Distance));
            _exitPads.Add(new Vector2(v[^1].Position.X, v[^1].Position.Z));
        }
        // Full strength from the first vertex: inside the S-transition the ridge's height is the primary's own
        // (the section avoids every feature and the plateau begins after the transition), so the overlap is
        // seamless, and a fade would instead blend the ridge toward the side terrain a canyon raises.
        // A terrace's falloffs are its own (D-103): a cliff toward the floor below, a drain-grade slope outward.
        var stamped = new StampedLine(line, profile, SizeX, SizeZ, _rules);
        _lines.Add(stamped);
    }

    /// <summary>Nearest primary-route vertex and its distance; −1 when nothing lies within the corridor reach.</summary>
    public int Nearest(float x, float z, out float distance) => _primary.Nearest(x, z, SizeX, SizeZ, out distance);

    /// <summary>Distance to the nearest stamped line (primary or optional).</summary>
    public float DistanceToRoute(float x, float z)
    {
        float best = float.MaxValue;
        foreach (var line in _lines)
        {
            line.Nearest(x, z, SizeX, SizeZ, out float d);
            best = Mathf.Min(best, d);
        }
        return best;
    }

    /// <summary>Base landscape without the corridors: swells plus micro relief, plus a dune sea's wave. A canyon's
    /// side terrain sits <see cref="WallHeight"/> above this; the corridor profile is cut from the floor relief, so
    /// the channel floor follows the valleys and the walls are the difference.</summary>
    public float Relief(float x, float z) => Base(x, z) + _dunes.At(x, z);

    /// <summary>The wave of a dune sea at a point (0 elsewhere).</summary>
    public float DuneHeight(float x, float z) => _dunes.At(x, z);

    /// <summary>Swells plus micro relief: what the corridor profile is smoothed from.</summary>
    private float Base(float x, float z)
    {
        float h = 0f;
        for (int i = 0; i < _swells.Length; i++)
        {
            ref readonly var s = ref _swells[i];
            h += s.Amp * Mathf.Cos(s.Kx * x + s.Kz * z + s.Phase);
        }
        h += _noiseAmpA * ValueNoise(x / 400f, z / 400f, _noiseSeedA);
        h += _noiseAmpB * ValueNoise(x / 120f, z / 120f, _noiseSeedB);
        return h;
    }

    /// <summary>The primary's own stamp weight at a point: 1 wherever its profile is the ground.</summary>
    public float PrimaryWeight(float x, float z)
    {
        int i = _primary.Nearest(x, z, SizeX, SizeZ, out float d);
        return i < 0 ? 0f : _primary.Weight(i, x, z, d);
    }

    /// <summary>Combined corridor weight at a point: 1 inside any corridor, 0 beyond every falloff.</summary>
    public float CorridorWeight(float x, float z)
    {
        float w = 0f;
        foreach (var line in _lines)
        {
            int i = line.Nearest(x, z, SizeX, SizeZ, out float d);
            if (i < 0) continue;
            w = Mathf.Max(w, line.Weight(i, x, z, d));
        }
        return w;
    }

    /// <summary>The spiral pit's surface (D-102) at a point inside its rim, or NaN outside: the entry level out to the
    /// outer turn, then a cone down to the pit floor at the inner radius; the rim itself is a cliff to the side terrain.</summary>
    public float PitSurface(float x, float z)
    {
        if (_spiral is not { } pit) return float.NaN;
        float r = new Vector2(x - pit.Centre.X, z - pit.Centre.Z).Length();
        float rim = pit.OuterRadius + CorridorHalfWidth + BendExtraHalfWidth + WorldScale.WallSetback + 2f;
        if (r >= rim) return float.NaN;
        float cone = pit.Depth * Mathf.Clamp((pit.OuterRadius - r) / Mathf.Max(1f, pit.OuterRadius - pit.InnerRadius), 0f, 1f);
        return pit.EntryHeight - cone;
    }

    public float Sample(float x, float z)
    {
        float h = Relief(x, z) + WallHeight;
        float pitH = PitSurface(x, z);
        if (!float.IsNaN(pitH)) h = pitH;
        // Optional lines stamp first and the primary last, so inside the primary corridor the primary's
        // profile is exact (a ridge's transition beside it must never kink the centreline: a metre over a
        // cell launches a ceiling ball) while a ridge's own centreline is its profile wherever the primary
        // has faded. Their heights agree where both are full, so the overlap is continuous.
        for (int k = _lines.Count - 1; k >= 0; k--)
        {
            var line = _lines[k];
            int i = line.Nearest(x, z, SizeX, SizeZ, out float d);
            if (i < 0) continue;
            float w = line.Weight(i, x, z, d);
            if (w <= 0f) continue;
            h = Mathf.Lerp(h, line.Height(i, x, z), w);
        }
        return h;
    }

    private static readonly Color Track = new(0.50f, 0.44f, 0.36f);
    private static readonly Color CanyonRock = new(0.62f, 0.36f, 0.24f);
    private static readonly Color CanyonRim = new(0.80f, 0.60f, 0.40f);
    private static readonly Color SandTrough = new(0.72f, 0.56f, 0.34f);
    private static readonly Color SandCrest = new(0.93f, 0.80f, 0.52f);
    private static readonly Color SandLee = new(0.58f, 0.42f, 0.26f);
    private static readonly Color StartPad = new(0.30f, 0.62f, 0.38f);
    private static readonly Color ExitPad = new(0.32f, 0.48f, 0.78f);

    public Color SampleColor(Vector3 point, Vector3 normal)
    {
        float slope = 1f - Mathf.Clamp(normal.Y, 0f, 1f);
        Color c = TerrainHeightField.BasePalette(point.Y * 0.45f, slope);
        if (_dunes.Exists)
        {
            // Dune palette (06 §3): sand from the troughs to the pale crests, the faces that lean away from the
            // wave's travel darker, so the crest lines read at a glance and the wave's direction with them.
            float up = Mathf.Clamp(_dunes.At(point.X, point.Z) / _dunes.Height, 0f, 1f);
            float lean = normal.X * Mathf.Cos(_dunes.Angle) + normal.Z * Mathf.Sin(_dunes.Angle);
            c = SandTrough.Lerp(SandCrest, Mathf.SmoothStep(0.1f, 0.9f, up));
            c = c.Lerp(SandLee, Mathf.SmoothStep(0.02f, 0.2f, -lean) * 0.7f);
            c = c.Lerp(TerrainHeightField.BasePalette(-20f, slope), Mathf.SmoothStep(0.25f, 0.5f, slope) * 0.5f);
        }
        if (WallHeight > 0f)
        {
            // Canyon palette (06 §3): red rock on the walls, a paler rim on the side terrain, the floor unchanged.
            float above = Mathf.Clamp((point.Y - Relief(point.X, point.Z)) / WallHeight, 0f, 1f);
            c = c.Lerp(CanyonRock, Mathf.SmoothStep(0.05f, 0.4f, above) * (0.55f + 0.45f * Mathf.SmoothStep(0.2f, 0.6f, slope)));
            c = c.Lerp(CanyonRim, Mathf.SmoothStep(0.85f, 1f, above) * (1f - slope) * 0.6f);
        }
        float track = 0f;
        foreach (var line in _lines)
        {
            int i = line.Nearest(point.X, point.Z, SizeX, SizeZ, out float d);
            if (i < 0) continue;
            float hw = line.Width(i);
            track = Mathf.Max(track, 1f - Mathf.SmoothStep(hw - 8f, hw + 8f, d));
        }
        c = c.Lerp(Track, track * 0.45f);
        float dStart = point.DistanceTo(new Vector3(_primary.X[0], point.Y, _primary.Z[0]));
        c = c.Lerp(StartPad, 0.7f * (1f - Mathf.SmoothStep(WorldScale.PadRadius - 10f, WorldScale.PadRadius, dStart)));
        foreach (var pad in _exitPads)
        {
            float dExit = new Vector2(point.X - pad.X, point.Z - pad.Y).Length();
            c = c.Lerp(ExitPad, 0.7f * (1f - Mathf.SmoothStep(WorldScale.PadRadius - 10f, WorldScale.PadRadius, dExit)));
        }
        return TerrainHeightField.FacetJitter(c, point);
    }

    /// <summary>0 outside the gap, 1 at its deepest: a steep take-off rim over <see cref="WorldScale.GapRimFace"/> metres,
    /// then an exit wall rising over the rest of the opening (drivable out: the free path never stops).</summary>
    private static float Chasm(float d, float opening, float depth)
    {
        float exitRun = Mathf.Max(1f, opening - WorldScale.GapRimFace);
        float a = d / WorldScale.GapRimFace;
        float b = (opening - d) / exitRun;
        float t = Mathf.Clamp(Mathf.Min(a, b), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    /// <summary>C1 smooth max(v, 0), exact outside a ± e/2 window around zero.</summary>
    private static float SoftMax0(float v, float e)
    {
        float t = v + e * 0.5f;
        if (t <= 0f) return 0f;
        return t < e ? t * t / (2f * e) : t - e * 0.5f;
    }

    /// <summary>C1 clamp of v into [0, hi] with slope exactly 1 in the middle: the ramp face keeps its stated
    /// gradient while the foot, the lip and the back face are rounded over e metres (the lab's ramps).</summary>
    private static float SmoothClamp(float v, float hi, float e)
    {
        e = Mathf.Min(e, hi * 0.9f);
        if (e <= 1e-4f) return Mathf.Clamp(v, 0f, hi);
        return hi - SoftMax0(hi - SoftMax0(v, e), e);
    }

    /// <summary>Seeded lattice value noise in [−1, 1] with smoothstep interpolation.</summary>
    private static float ValueNoise(float x, float z, ulong seed)
    {
        int x0 = Mathf.FloorToInt(x), z0 = Mathf.FloorToInt(z);
        float tx = Mathf.SmoothStep(0f, 1f, x - x0), tz = Mathf.SmoothStep(0f, 1f, z - z0);
        float a = Lattice(x0, z0, seed), b = Lattice(x0 + 1, z0, seed);
        float c = Lattice(x0, z0 + 1, seed), d = Lattice(x0 + 1, z0 + 1, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz);
    }

    private static float Lattice(int x, int z, ulong seed)
    {
        ulong h = SplitMix64.Mix(seed ^ ((ulong)(uint)x * 0x9E3779B97F4A7C15UL) ^ ((ulong)(uint)z * 0xC2B2AE3D27D4EB4FUL));
        return ((h >> 40) * (1f / 16777216f)) * 2f - 1f;
    }
}
