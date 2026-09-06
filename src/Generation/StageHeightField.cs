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

    // Route, flattened for tight sampling loops.
    private readonly int _n;
    private readonly float[] _rx, _rz, _rh, _rheading, _bankHeight, _bankSide;
    private readonly bool[] _bend;
    private readonly float _startH, _exitH;

    // Coarse nearest-vertex map over the footprint.
    private readonly int _cx, _cz;
    private readonly int[] _coarse;

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

        // ---- route arrays ----
        var v = route.Vertices;
        _n = v.Count;
        _rx = new float[_n]; _rz = new float[_n]; _rh = new float[_n]; _rheading = new float[_n];
        _bankHeight = new float[_n]; _bankSide = new float[_n];
        _bend = new bool[_n];
        for (int i = 0; i < _n; i++)
        {
            _rx[i] = v[i].Position.X; _rz[i] = v[i].Position.Z; _rheading[i] = v[i].Heading;
            _bend[i] = v[i].Kind == RouteSegmentKind.Bend;
        }

        // ---- D: corridor profile = relief along the route, smoothed, plus crests and pads ----
        var raw = new float[_n];
        for (int i = 0; i < _n; i++) raw[i] = Relief(_rx[i], _rz[i]);
        int window = Mathf.Max(1, Mathf.RoundToInt(SmoothingHalfWindow / WorldScale.RouteSampleSpacing));
        var prefix = new float[_n + 1];
        for (int i = 0; i < _n; i++) prefix[i + 1] = prefix[i] + raw[i];
        for (int i = 0; i < _n; i++)
        {
            int lo = Mathf.Max(0, i - window), hi = Mathf.Min(_n - 1, i + window);
            _rh[i] = (prefix[hi + 1] - prefix[lo]) / (hi - lo + 1);
        }
        foreach (var f in route.Features)
        {
            if (f.Kind != RouteFeatureKind.LaunchCrest) continue;
            // Trim the crest so its slope plus the local corridor slope stays under the route grade limit.
            float localSlope = 0f;
            for (int i = f.StartIndex + 1; i <= f.EndIndex; i++)
            {
                float ds = v[i].Distance - v[i - 1].Distance;
                if (ds > 1e-3f) localSlope = Mathf.Max(localSlope, Mathf.Abs(_rh[i] - _rh[i - 1]) / ds);
            }
            float maxHeight = Mathf.Max(0f, (WorldScale.MaxRouteGrade * 0.9f - localSlope) * f.Wavelength / Mathf.Pi);
            f.Height = Mathf.Min(f.Height, maxHeight);
            for (int i = f.StartIndex; i <= f.EndIndex; i++)
            {
                float d = v[i].Distance - f.CentreDistance;
                if (Mathf.Abs(d) <= f.Wavelength * 0.5f)
                    _rh[i] += f.Height * 0.5f * (1f + Mathf.Cos(Mathf.Tau * d / f.Wavelength));
            }
        }
        _startH = _rh[0];
        _exitH = _rh[_n - 1];
        float total = v[_n - 1].Distance;
        for (int i = 0; i < _n; i++)
        {
            float d = v[i].Distance;
            float wStart = Mathf.SmoothStep(WorldScale.PadRadius, WorldScale.PadRadius * 2.5f, d);
            float wExit = Mathf.SmoothStep(WorldScale.PadRadius, WorldScale.PadRadius * 2.5f, total - d);
            _rh[i] = Mathf.Lerp(_startH, _rh[i], wStart);
            _rh[i] = Mathf.Lerp(_exitH, _rh[i], wExit);
        }

        // Banks: the outer half of a bend rises linearly with radius, faded in and out.
        foreach (var b in route.Bends)
        {
            float height = WorldScale.BankHeightPerRadius * b.Radius;
            float side = -Mathf.Sign(b.TurnAngle);                    // outward is opposite the turn
            float startD = v[b.StartIndex].Distance, endD = v[b.EndIndex].Distance;
            for (int i = b.StartIndex; i <= b.EndIndex; i++)
            {
                float d = v[i].Distance;
                float fade = Mathf.Min(Mathf.SmoothStep(0f, BankFade, d - startD), Mathf.SmoothStep(0f, BankFade, endD - d));
                _bankHeight[i] = height * fade;
                _bankSide[i] = side;
            }
        }

        SpawnXZ = new Vector3(_rx[0], 0f, _rz[0]);
        SpawnFacing = new Vector3(Mathf.Cos(_rheading[0]), 0f, Mathf.Sin(_rheading[0]));

        // ---- coarse nearest map: X is monotonic along the route, so each cell scans a window of X ----
        _cx = Mathf.CeilToInt(SizeX / CoarseCell) + 1;
        _cz = Mathf.CeilToInt(SizeZ / CoarseCell) + 1;
        _coarse = new int[_cx * _cz];
        float reach = CorridorHalfWidth + BendExtraHalfWidth + FalloffWidth + CoarseCell * 2f;
        for (int cz = 0; cz < _cz; cz++)
        {
            float z = cz * CoarseCell - SizeZ * 0.5f;
            for (int cx = 0; cx < _cx; cx++)
            {
                float x = cx * CoarseCell - SizeX * 0.5f;
                int lo = LowerBoundX(x - reach), hi = LowerBoundX(x + reach);
                int best = -1; float bestD = float.MaxValue;
                for (int i = lo; i < hi; i++)
                {
                    float dx = _rx[i] - x, dz = _rz[i] - z, dd = dx * dx + dz * dz;
                    if (dd < bestD) { bestD = dd; best = i; }
                }
                _coarse[cz * _cx + cx] = best;
            }
        }
    }

    private int LowerBoundX(float x)
    {
        int lo = 0, hi = _n;
        while (lo < hi) { int mid = (lo + hi) >> 1; if (_rx[mid] < x) lo = mid + 1; else hi = mid; }
        return lo;
    }

    /// <summary>Nearest route vertex and its distance; −1 when nothing lies within the corridor reach.</summary>
    public int Nearest(float x, float z, out float distance)
    {
        int cx = Mathf.Clamp((int)((x + SizeX * 0.5f) / CoarseCell + 0.5f), 0, _cx - 1);
        int cz = Mathf.Clamp((int)((z + SizeZ * 0.5f) / CoarseCell + 0.5f), 0, _cz - 1);
        int seed = _coarse[cz * _cx + cx];
        if (seed < 0) { distance = float.MaxValue; return -1; }
        int best = -1; float bestD = float.MaxValue;
        int lo = Mathf.Max(0, seed - RefineSpan), hi = Mathf.Min(_n - 1, seed + RefineSpan);
        for (int i = lo; i <= hi; i++)
        {
            float dx = _rx[i] - x, dz = _rz[i] - z, dd = dx * dx + dz * dz;
            if (dd < bestD) { bestD = dd; best = i; }
        }
        distance = Mathf.Sqrt(bestD);
        return best;
    }

    public float DistanceToRoute(float x, float z) { Nearest(x, z, out float d); return d; }

    /// <summary>Base landscape without the corridor: swells plus micro relief.</summary>
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

    /// <summary>Corridor weight at a point: 1 inside the corridor, 0 beyond the falloff.</summary>
    public float CorridorWeight(float x, float z)
    {
        int i = Nearest(x, z, out float d);
        if (i < 0) return 0f;
        float hw = CorridorHalfWidth + (_bend[i] ? BendExtraHalfWidth : 0f);
        return 1f - Mathf.SmoothStep(hw, hw + FalloffWidth, d);
    }

    public float Sample(float x, float z)
    {
        float relief = Relief(x, z);
        int i = Nearest(x, z, out float d);
        if (i < 0) return relief;
        float hw = CorridorHalfWidth + (_bend[i] ? BendExtraHalfWidth : 0f);
        float w = 1f - Mathf.SmoothStep(hw, hw + FalloffWidth, d);
        if (w <= 0f) return relief;

        float corridor = _rh[i];
        if (_bankHeight[i] > 0f)
        {
            // Signed lateral offset toward the outside of the bend, as a fraction of the half-width.
            float lx = -Mathf.Sin(_rheading[i]), lz = Mathf.Cos(_rheading[i]);
            float lateral = ((x - _rx[i]) * lx + (z - _rz[i]) * lz) * _bankSide[i];
            corridor += _bankHeight[i] * Mathf.Clamp(lateral / hw, 0f, 1f);
        }
        return Mathf.Lerp(relief, corridor, w);
    }

    private static readonly Color Track = new(0.50f, 0.44f, 0.36f);
    private static readonly Color StartPad = new(0.30f, 0.62f, 0.38f);
    private static readonly Color ExitPad = new(0.32f, 0.48f, 0.78f);

    public Color SampleColor(Vector3 point, Vector3 normal)
    {
        float slope = 1f - Mathf.Clamp(normal.Y, 0f, 1f);
        Color c = TerrainHeightField.BasePalette(point.Y * 0.45f, slope);
        int i = Nearest(point.X, point.Z, out float d);
        if (i >= 0)
        {
            float hw = CorridorHalfWidth + (_bend[i] ? BendExtraHalfWidth : 0f);
            float w = 1f - Mathf.SmoothStep(hw - 8f, hw + 8f, d);
            c = c.Lerp(Track, w * 0.45f);
            float dStart = point.DistanceTo(new Vector3(_rx[0], point.Y, _rz[0]));
            float dExit = point.DistanceTo(new Vector3(_rx[_n - 1], point.Y, _rz[_n - 1]));
            c = c.Lerp(StartPad, 0.7f * (1f - Mathf.SmoothStep(WorldScale.PadRadius - 10f, WorldScale.PadRadius, dStart)));
            c = c.Lerp(ExitPad, 0.7f * (1f - Mathf.SmoothStep(WorldScale.PadRadius - 10f, WorldScale.PadRadius, dExit)));
        }
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
