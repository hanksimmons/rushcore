using Godot;
using Rushcore.World;

namespace Rushcore.Generation;

/// <summary>
/// Rolling Highlands height source (04 §5B–D, §6): analytic long swells whose summed crest
/// curvature and slope are budgeted so a cruising ball keeps contact (04 §8) and the corridor
/// stays under the route grade limit, bounded micro relief (04 §5C), and the guaranteed corridor
/// stamped along the primary route: level across, smoothed along, banked on bends, launch
/// crests on feature straights, flat start/exit pads. Pure data; render and collision both
/// sample it (04 §9).
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

    private readonly struct Swell
    {
        public readonly float Kx, Kz, Amp, Phase;
        public Swell(float kx, float kz, float amp, float phase) { Kx = kx; Kz = kz; Amp = amp; Phase = phase; }
    }

    private readonly Swell[] _swells;
    private readonly ulong _noiseSeedA, _noiseSeedB;
    private readonly float _noiseAmpA, _noiseAmpB;

    /// <summary>One stamped line: flattened route arrays, its corridor profile, banks and a coarse nearest map.</summary>
    private sealed class StampedLine
    {
        public readonly int N;
        public readonly float[] X, Z, H, Heading, BankHeight, BankSide;
        /// <summary>Stamp strength per vertex: 1 for the primary; optional lines fade in as they leave the primary corridor.</summary>
        public readonly float[] Strength;
        public readonly bool[] Bend;
        public readonly float HalfWidth;
        public readonly int Cx, Cz;
        public readonly int[] Coarse;

        public StampedLine(RouteSkeleton route, float[] profile, float sizeX, float sizeZ)
        {
            var v = route.Vertices;
            N = v.Count;
            X = new float[N]; Z = new float[N]; H = profile; Heading = new float[N];
            BankHeight = new float[N]; BankSide = new float[N]; Bend = new bool[N];
            Strength = new float[N];
            Array.Fill(Strength, 1f);
            HalfWidth = route.CorridorHalfWidth;
            for (int i = 0; i < N; i++)
            {
                X[i] = v[i].Position.X; Z[i] = v[i].Position.Z; Heading[i] = v[i].Heading;
                Bend[i] = v[i].Kind == RouteSegmentKind.Bend;
            }
            foreach (var b in route.Bends)
            {
                float height = WorldScale.BankHeightPerRadius * b.Radius;
                float side = -Mathf.Sign(b.TurnAngle);                    // outward is opposite the turn
                float startD = v[b.StartIndex].Distance, endD = v[b.EndIndex].Distance;
                for (int i = b.StartIndex; i <= b.EndIndex; i++)
                {
                    float d = v[i].Distance;
                    float fade = Mathf.Min(Mathf.SmoothStep(0f, BankFade, d - startD), Mathf.SmoothStep(0f, BankFade, endD - d));
                    BankHeight[i] = height * fade;
                    BankSide[i] = side;
                }
            }

            // Coarse nearest map: X is monotonic along every line, so each cell scans a window of X.
            Cx = Mathf.CeilToInt(sizeX / CoarseCell) + 1;
            Cz = Mathf.CeilToInt(sizeZ / CoarseCell) + 1;
            Coarse = new int[Cx * Cz];
            float reach = HalfWidth + BendExtraHalfWidth + FalloffWidth + CoarseCell * 2f;
            for (int cz = 0; cz < Cz; cz++)
            {
                float z = cz * CoarseCell - sizeZ * 0.5f;
                for (int cx = 0; cx < Cx; cx++)
                {
                    float x = cx * CoarseCell - sizeX * 0.5f;
                    int lo = LowerBoundX(x - reach), hi = LowerBoundX(x + reach);
                    int best = -1; float bestD = float.MaxValue;
                    for (int i = lo; i < hi; i++)
                    {
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
            while (lo < hi) { int mid = (lo + hi) >> 1; if (X[mid] < x) lo = mid + 1; else hi = mid; }
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

        public float Width(int i) => HalfWidth + (Bend[i] ? BendExtraHalfWidth : 0f);

        /// <summary>Corridor height at a point near vertex i: level across, plus the outer-half bank.</summary>
        public float Height(int i, float x, float z)
        {
            float h = H[i];
            if (BankHeight[i] > 0f)
            {
                float lx = -Mathf.Sin(Heading[i]), lz = Mathf.Cos(Heading[i]);
                float lateral = ((x - X[i]) * lx + (z - Z[i]) * lz) * BankSide[i];
                h += BankHeight[i] * Mathf.Clamp(lateral / Width(i), 0f, 1f);
            }
            return h;
        }
    }

    private readonly StampedLine _primary;
    private readonly List<StampedLine> _lines = new();
    private readonly float[] _primaryProfile;
    private readonly RouteSkeleton _primaryRoute;

    public float SizeX => WorldScale.FootprintLength;
    public float SizeZ => WorldScale.FootprintWidth;
    public Vector3 SpawnXZ { get; }
    public Vector3 SpawnFacing { get; }
    /// <summary>Summed crest curvature of the swell layer; contact at the cap needs ≤ 1 / cruise crest radius.</summary>
    public float SwellCurvature { get; }

    public StageHeightField(RouteSkeleton route, ulong stageSeed)
    {
        var rng = new SeededRandom(SeedChain.Derive(stageSeed, "relief"));

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
        float scale = Mathf.Min(1f, Mathf.Min(SwellCurvatureShare / WorldScale.CruiseCrestRadius / curvature, WorldScale.LongSwellMaxSlope / slope));
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

        // ---- D: corridor profile = relief along the route, smoothed, plus crests and pads ----
        _primaryRoute = route;
        var v = route.Vertices;
        int n = v.Count;
        var raw = new float[n];
        for (int i = 0; i < n; i++) raw[i] = Relief(v[i].Position.X, v[i].Position.Z);
        var rh = new float[n];
        int window = Mathf.Max(1, Mathf.RoundToInt(SmoothingHalfWindow / WorldScale.RouteSampleSpacing));
        var prefix = new float[n + 1];
        for (int i = 0; i < n; i++) prefix[i + 1] = prefix[i] + raw[i];
        for (int i = 0; i < n; i++)
        {
            int lo = Mathf.Max(0, i - window), hi = Mathf.Min(n - 1, i + window);
            rh[i] = (prefix[hi + 1] - prefix[lo]) / (hi - lo + 1);
        }
        foreach (var f in route.Features)
        {
            if (f.Kind != RouteFeatureKind.LaunchCrest) continue;
            // Trim the crest so its slope plus the local corridor slope stays under the route grade limit.
            float localSlope = 0f;
            for (int i = f.StartIndex + 1; i <= f.EndIndex; i++)
            {
                float ds = v[i].Distance - v[i - 1].Distance;
                if (ds > 1e-3f) localSlope = Mathf.Max(localSlope, Mathf.Abs(rh[i] - rh[i - 1]) / ds);
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
        _primary = new StampedLine(route, rh, SizeX, SizeZ);
        _lines.Add(_primary);

        SpawnXZ = new Vector3(v[0].Position.X, 0f, v[0].Position.Z);
        SpawnFacing = new Vector3(Mathf.Cos(v[0].Heading), 0f, Mathf.Sin(v[0].Heading));
    }

    /// <summary>Corridor centreline height of the primary route at a vertex.</summary>
    public float PrimaryHeight(int index) => _primaryProfile[index];

    /// <summary>
    /// Stamps an optional line. A ridge line rides the primary's profile between its joins, raised
    /// onto a plateau by its ridge height, so both ends meet the primary exactly.
    /// </summary>
    public void AddLine(RouteSkeleton line)
    {
        var v = line.Vertices;
        var profile = new float[v.Count];
        float span = Mathf.Max(1f, v[^1].Distance);
        for (int i = 0; i < v.Count; i++)
        {
            int pi = Mathf.Clamp(line.JoinStart + i, 0, _primaryProfile.Length - 1);
            profile[i] = _primaryProfile[pi] + line.RidgeHeight * OptionalLineBuilder.Plateau(v[i].Distance, span);
        }
        var stamped = new StampedLine(line, profile, SizeX, SizeZ);
        for (int i = 0; i < v.Count; i++)
        {
            _primary.Nearest(v[i].Position.X, v[i].Position.Z, SizeX, SizeZ, out float away);
            stamped.Strength[i] = Mathf.SmoothStep(WorldScale.OptionalStampFadeStart, WorldScale.OptionalStampFadeEnd, away);
        }
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

    /// <summary>Base landscape without the corridors: swells plus micro relief.</summary>
    public float Relief(float x, float z)
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

    /// <summary>Combined corridor weight at a point: 1 inside any corridor, 0 beyond every falloff.</summary>
    public float CorridorWeight(float x, float z)
    {
        float w = 0f;
        foreach (var line in _lines)
        {
            int i = line.Nearest(x, z, SizeX, SizeZ, out float d);
            if (i < 0) continue;
            float hw = line.Width(i);
            w = Mathf.Max(w, 1f - Mathf.SmoothStep(hw, hw + FalloffWidth, d));
        }
        return w;
    }

    public float Sample(float x, float z)
    {
        float h = Relief(x, z);
        // Lines stamp in order (primary first); an optional line's profile meets the primary at its
        // joins, so where their falloffs overlap the blend is continuous.
        foreach (var line in _lines)
        {
            int i = line.Nearest(x, z, SizeX, SizeZ, out float d);
            if (i < 0) continue;
            float hw = line.Width(i);
            float w = (1f - Mathf.SmoothStep(hw, hw + FalloffWidth, d)) * line.Strength[i];
            if (w <= 0f) continue;
            h = Mathf.Lerp(h, line.Height(i, x, z), w);
        }
        return h;
    }

    private static readonly Color Track = new(0.50f, 0.44f, 0.36f);
    private static readonly Color StartPad = new(0.30f, 0.62f, 0.38f);
    private static readonly Color ExitPad = new(0.32f, 0.48f, 0.78f);

    public Color SampleColor(Vector3 point, Vector3 normal)
    {
        float slope = 1f - Mathf.Clamp(normal.Y, 0f, 1f);
        Color c = TerrainHeightField.BasePalette(point.Y * 0.45f, slope);
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
        float dExit = point.DistanceTo(new Vector3(_primary.X[_primary.N - 1], point.Y, _primary.Z[_primary.N - 1]));
        c = c.Lerp(StartPad, 0.7f * (1f - Mathf.SmoothStep(WorldScale.PadRadius - 10f, WorldScale.PadRadius, dStart)));
        c = c.Lerp(ExitPad, 0.7f * (1f - Mathf.SmoothStep(WorldScale.PadRadius - 10f, WorldScale.PadRadius, dExit)));
        return TerrainHeightField.FacetJitter(c, point);
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
