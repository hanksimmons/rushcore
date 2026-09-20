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
        /// <summary>A bend's fade in 0..1 at each vertex (the bank's own), read by the wall profile (D-109).</summary>
        public readonly float[] Fade;
        /// <summary>How far (0..1) the wall on a bend's inside has eased toward the inside face at each vertex, over
        /// <see cref="WorldScale.WallInsideFaceFade"/> of route before and after the bend, and which raw lateral sign
        /// that inside is (+1 left, −1 right; 0 = none in reach) (D-109).</summary>
        public readonly float[] InsideEase, InsideSign;
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
        private readonly float _sizeX, _sizeZ;
        /// <summary>Walled archetypes (D-109): the wall beyond the edge is the authored profile, not a blend. A tunnel's trench is profiled everywhere (D-113).</summary>
        private readonly bool _profiled;
        public bool Profiled => _profiled;
        /// <summary>A tunnel line (docs/13, D-113): a narrow slot cut into the finished ground with its own profile numbers.</summary>
        public readonly bool Tunnel;
        public readonly RouteSkeleton Route;
        /// <summary>The line's wall profile (D-113): the fillet radius, the face slope, the setback beyond the level width and the sink
        /// under its shell (the canyon's, or a tunnel's).</summary>
        public readonly float Radius, FaceTan, Setback, ShellSink;

        public StampedLine(RouteSkeleton route, float[] profile, float sizeX, float sizeZ, ArchetypeRules rules)
        {
            _wallFalloff = rules.WallFalloff;
            _insideFalloff = rules.InsideFalloff;
            _sizeX = sizeX; _sizeZ = sizeZ;
            Route = route;
            Tunnel = route.IsTunnel;
            _profiled = rules.WallHeightMax > 0f || Tunnel;
            Radius = Tunnel ? TunnelProfile.Radius : WallProfile.FootRadius;
            FaceTan = Tunnel ? TunnelProfile.FaceTan : WallProfile.FaceTan;
            Setback = Tunnel ? 0f : WorldScale.WallSetback;
            ShellSink = Tunnel ? WorldScale.TunnelShellSink : WorldScale.WallShellSink;
            var v = route.Vertices;
            N = v.Count;
            X = new float[N]; Z = new float[N]; H = profile; Heading = new float[N];
            BankHeight = new float[N]; BankSide = new float[N]; Bend = new bool[N]; Extra = new float[N]; Fade = new float[N];
            InsideEase = new float[N]; InsideSign = new float[N];
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
                    Fade[i] = fade;
                    Extra[i] = BendExtraHalfWidth * fade;
                }
                // The inside face eases in along the straight before the bend and out after it (D-109).
                float insideSign = Mathf.Sign(b.TurnAngle);
                int i0 = route.IndexAtDistance(Mathf.Max(0f, startD - WorldScale.WallInsideFaceFade));
                int i1 = route.IndexAtDistance(Mathf.Min(routeEnd, endD + WorldScale.WallInsideFaceFade));
                for (int i = i0; i <= i1 && i < N; i++)
                {
                    float d = v[i].Distance;
                    float ease = d < startD ? Mathf.SmoothStep(0f, WorldScale.WallInsideFaceFade, d - (startD - WorldScale.WallInsideFaceFade))
                               : d > endD ? Mathf.SmoothStep(0f, WorldScale.WallInsideFaceFade, (endD + WorldScale.WallInsideFaceFade) - d)
                               : 1f;
                    if (ease > InsideEase[i]) { InsideEase[i] = ease; InsideSign[i] = insideSign; }
                    // On a walled archetype the bend's extra half-width eases over the same run (D-109): faded with the bank
                    // over 60 m it jogged the wall line 25 m outward at a bend's entry, a 29° recession no wall ride could
                    // follow. Open landscapes keep the bank's fade (their hashes are pinned).
                    if (_profiled) Extra[i] = Mathf.Max(Extra[i], BendExtraHalfWidth * ease);
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
            // A tunnel's wall stands 15–25 m from its line, where the distance to the nearest vertex ripples 0.1 m every
            // 4 m (2²/2d) and the 78° face turns that into half a metre of height between the shell's stations (sampled at
            // the vertices, where the two distances agree) and the analytic wall the follow reads; the ball was punted off
            // the shell at every station. A tunnel line therefore measures to the polyline itself (D-113). The canyon's
            // walls stand 83 m out, where the ripple is 2 cm, and keep the vertex distance (and their hashes).
            distance = Tunnel && best >= 0 ? FootDistance(best, x, z) : Mathf.Sqrt(bestD);
            return best;
        }

        /// <summary>The point of the polyline nearest to (x, z) beside vertex i: on the segment toward the neighbour the point lies
        /// toward for a tunnel line, the vertex itself otherwise.</summary>
        public void Foot(int i, float x, float z, out float fx, out float fz)
        {
            if (!Tunnel) { fx = X[i]; fz = Z[i]; return; }
            Toward(i, x, z, out int j, out float t);
            fx = Mathf.Lerp(X[i], X[j], t); fz = Mathf.Lerp(Z[i], Z[j], t);
        }

        private float FootDistance(int i, float x, float z)
        {
            Foot(i, x, z, out float fx, out float fz);
            return Mathf.Sqrt((x - fx) * (x - fx) + (z - fz) * (z - fz));
        }

        public float Width(int i) => HalfWidth + Extra[i];

        /// <summary>Lateral offset of a point from the line at vertex i, positive on the outside of a bend (or the left on a straight).</summary>
        public float Lateral(int i, float x, float z)
        {
            float lx = -Mathf.Sin(Heading[i]), lz = Mathf.Cos(Heading[i]);
            float lateral = (x - X[i]) * lx + (z - Z[i]) * lz;
            return BankSide[i] != 0f ? lateral * BankSide[i] : lateral;
        }

        /// <summary>Whether the authored wall profile applies at a point beside vertex i, and its lateral distance u
        /// beyond the edge (D-109): every wall of a walled archetype except the inside of a bend, which keeps the long
        /// blend so the inside wall never hides the read horizon (04 §10).</summary>
        public bool ProfileAt(int i, float x, float z, float d, out float u, out float footSlope, out float faceTan)
        {
            u = d - (Width(i) + Setback);
            footSlope = 0f; faceTan = FaceTan;
            if (!_profiled) return false;
            // Every per-vertex quantity the wall's position depends on is read interpolated toward the neighbour the point
            // lies toward (D-111): as a step function of the nearest vertex the wall jogged 0.56 m every 4 m wherever a
            // bend's extra width eased in, and the follow read that as the wall jumping tick to tick.
            Toward(i, x, z, out int j, out float t);
            float width = HalfWidth + Mathf.Lerp(Extra[i], Extra[j], t);
            u = d - (width + Setback);
            // The inside of a bend, and the run into and out of it: the face eases to the blind-corner angle over the
            // inside fade, so the wall is one continuous surface along the route that a ride steers along.
            float rawLateral = -Mathf.Sin(Heading[i]) * (x - X[i]) + Mathf.Cos(Heading[i]) * (z - Z[i]);
            float insideSign = InsideSign[i] != 0f ? InsideSign[i] : InsideSign[j];
            float ease = Mathf.Lerp(InsideSign[i] == insideSign ? InsideEase[i] : 0f, InsideSign[j] == insideSign ? InsideEase[j] : 0f, t);
            if (ease > 0f && rawLateral * insideSign > 0f)
            {
                faceTan = Mathf.Lerp(FaceTan, WallProfile.InsideFaceTan, ease);
                return true;
            }
            float bankSide = BankSide[i] != 0f ? BankSide[i] : BankSide[j];
            if (bankSide == 0f) return true;
            float lateral = rawLateral * bankSide;
            if (lateral < 0f) return true;
            // The outside of a bend: the berm's slope carries straight into the fillet from the berm's top, with no
            // setback and no lip (a berm that flattened before the wall was a launch at the cap).
            float bank = Mathf.Lerp(BankSide[i] == bankSide ? BankHeight[i] : 0f, BankSide[j] == bankSide ? BankHeight[j] : 0f, t);
            float fade = Mathf.Lerp(BankSide[i] == bankSide ? Fade[i] : 0f, BankSide[j] == bankSide ? Fade[j] : 0f, t);
            if (bank > 0f && width > 1f)
            {
                u = d - (width + Setback * (1f - fade));   // the setback goes with the bank's fade, no step
                footSlope = bank / width;
            }
            return true;
        }

        /// <summary>The inside-of-bend reach of a vertex's lateral ray on a side (+1 left, −1 right): a metre short of the
        /// centre of curvature, where the rays would cross; unbounded on a straight or on the outside (D-111).</summary>
        /// <summary>Whether a point lies beyond the line's end nearest to it (behind the first vertex or past the last),
        /// where the nearest-vertex rule would still claim it for that vertex's wall band (D-111: the band stops at the
        /// line's ends, as the shell's strips do; a ridge's band otherwise sank the primary's floor for a hundred metres
        /// behind the ridge's start with no shell over it, and the ball drove under the shell's first ray and was
        /// popped up through it).</summary>
        public bool BeyondEnd(int i, float x, float z)
        {
            if (i != 0 && i != N - 1) return false;
            float along = (x - X[i]) * Mathf.Cos(Heading[i]) + (z - Z[i]) * Mathf.Sin(Heading[i]);
            return i == 0 ? along < 0f : along > 0f;
        }

        public float Reach(int i, float sideSign)
        {
            int ia = Mathf.Max(0, i - 1), ib = Mathf.Min(N - 1, i + 1);
            float ds = Mathf.Sqrt((X[ib] - X[ia]) * (X[ib] - X[ia]) + (Z[ib] - Z[ia]) * (Z[ib] - Z[ia]));
            float turn = ds > 1e-3f ? Mathf.AngleDifference(Heading[ia], Heading[ib]) / ds : 0f;
            return turn * sideSign > 1e-5f ? 1f / (turn * sideSign) - 1f : float.MaxValue;
        }

        /// <summary>Corridor weight at a point d metres from the line at vertex i: 1 inside the level width plus the
        /// wall setback, then falling over the archetype's falloff, the long one on the inside of a bend (blind corners).
        /// On a walled archetype (D-109) the fall is the wall profile instead: the weight is the share of the rise to
        /// the side terrain (<paramref name="top"/> metres above the corridor here) the profile has not yet made, so
        /// the stamp lays the fillet, the face and the rounded lip exactly.</summary>
        public float Weight(int i, float x, float z, float d, float top)
        {
            if (top > 0f && ProfileAt(i, x, z, d, out float u, out float s0, out float ft))
            {
                if (u <= 0f) return 1f;
                // The lip alone is rounded (SoftMax0 on the remaining rise); the foot is already tangent to the ground it
                // leaves, and a two-ended clamp would lift the wall a metre at the corridor edge (a step the ball hits,
                // and a stamp weight the wall-clearance validator refuses).
                float ease = Mathf.Min(WorldScale.WallLipEase, top * 0.9f);
                float rise = top - SoftMax0(top - WallProfile.Height(u, s0, ft, Radius), ease);
                return 1f - Mathf.Clamp(rise / top, 0f, 1f);
            }
            float edge = Width(i) + Setback;
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
            Toward(i, x, z, out int j, out float t);
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

        /// <summary>Any per-vertex profile interpolated along the route at a point's projection beside vertex i, no bank.</summary>
        public float ProfileHeight(float[] profile, int i, float x, float z)
        {
            Toward(i, x, z, out int j, out float t);
            return Mathf.Lerp(profile[i], profile[j], t);
        }

        /// <summary>The neighbour a point beside vertex i lies toward, and how far along toward it (0..1): the per-vertex
        /// arrays are read interpolated along the route (D-093 for the floor height; D-111 for the wall's width, berm fade
        /// and inside ease, whose nearest-vertex steps were a 0.56 m lateral staircase in the wall every 4 m where a bend's
        /// extra width eased in, 1.7 m of height on the face, that the follow read as the wall jumping tick to tick).</summary>
        private void Toward(int i, float x, float z, out int j, out float t)
        {
            float dx = x - X[i], dz = z - Z[i];
            j = i + 1 < N ? i + 1 : i - 1;
            if (j == i + 1 && i > 0 && dx * (X[j] - X[i]) + dz * (Z[j] - Z[i]) < 0f) j = i - 1;
            t = 0f;
            if (j >= 0)
            {
                float ex = X[j] - X[i], ez = Z[j] - Z[i], len2 = ex * ex + ez * ez;
                if (len2 > 1e-6f) t = Mathf.Clamp((dx * ex + dz * ez) / len2, 0f, 1f);
            }
            else j = i;
        }
    }

    private readonly StampedLine _primary;
    private readonly List<StampedLine> _lines = new();
    /// <summary>Tunnel lines (docs/13, D-113) are stamped after everything else, as cuts into the finished ground.</summary>
    private readonly List<StampedLine> _tunnels = new();
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
    /// <summary>The primary's base profile at a vertex (before features): what an optional line's floor is measured from.</summary>
    public float PrimaryBaseHeight(int index) => _primaryBase[index];

    public ArchetypeRules Rules => _rules;

    /// <summary>
    /// Stamps an optional line. A ridge line rides the primary's profile between its joins, raised
    /// onto a plateau by its ridge height, so both ends meet the primary exactly. A tunnel line (docs/13) is stamped only
    /// when the ground roofs it for at least <see cref="WorldScale.TunnelCoveredMin"/>: returns false, stamping nothing, for
    /// a tunnel the ground does not cover (a dive under too shallow a swell), so the generator drops the line.
    /// </summary>
    public bool AddLine(RouteSkeleton line)
    {
        var v = line.Vertices;
        var pv = _primaryRoute.Vertices;
        var profile = new float[v.Count];
        // The ramps are anchored in the primary's distance (the climb begins once the line has left the primary's falloff, the
        // descent ends before the return transition, D-100) but shaped in the line's own distance (D-117): the line is longer
        // round the outside of a bend, and a ramp shaped in the primary's distance kinked wherever a bend began or ended under it.
        var (up0, down1) = OptionalLineBuilder.RampAnchors(_primaryRoute, line);
        for (int i = 0; i < v.Count; i++)
        {
            int pi = Mathf.Clamp(line.JoinStart + i, 0, _primaryProfile.Length - 1);
            profile[i] = _primaryBase[pi] + line.RidgeHeight * (line.Terminal ? OptionalLineBuilder.TerminalEnvelope(up0, line.RampLength, v[i].Distance)
                                                                                : OptionalLineBuilder.RampEnvelope(up0, down1, line.RampLength, v[i].Distance));
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
        if (line.IsTunnel)
        {
            // A tunnel's floor is the primary's base along the line (the canyon floor continued into the rock, meeting the primary's
            // exactly through the mouth: the S's lie on straights, where the base along the line is the base at the projection) less
            // a dive's descent (D-116). The covered run: the longest contiguous stretch where the ground without any tunnel stands the portal depth
            // above the floor; its ends are the portals.
            int n = line.Vertices.Count, bestStart = -1, bestLen = 0, runStart = -1;
            for (int i = 0; i <= n; i++)
            {
                bool covered = i < n && SampleWithoutTunnels(stamped.X[i], stamped.Z[i]) - stamped.Height(i, stamped.X[i], stamped.Z[i]) >= TunnelProfile.PortalDepth;
                if (covered) { if (runStart < 0) runStart = i; }
                else if (runStart >= 0) { if (i - runStart > bestLen) { bestLen = i - runStart; bestStart = runStart; } runStart = -1; }
            }
            bool enough = bestStart >= 0 && bestLen >= 2 && line.Vertices[bestStart + bestLen - 1].Distance - line.Vertices[bestStart].Distance >= WorldScale.TunnelCoveredMin;
            line.CoverStart = enough ? bestStart : -1;
            line.CoverEnd = enough ? bestStart + bestLen - 1 : -1;
            if (!enough) return false;
            _tunnels.Add(stamped);
        }
        _lines.Add(stamped);
        return true;
    }

    /// <summary>Instrument: a tunnel line's own floor at a point (the primary's base at the projection less the dive envelope) and its
    /// cut weight there, for tracing the stamped floor against the planned one.</summary>
    public (float floor, float weight, float without) TunnelFloorAt(RouteSkeleton line, float x, float z)
    {
        foreach (var t in _tunnels)
        {
            if (!ReferenceEquals(t.Route, line)) continue;
            int i = t.Nearest(x, z, SizeX, SizeZ, out float d);
            float without = SampleWithoutTunnels(x, z);
            if (i < 0) return (float.NaN, 0f, without);
            float f = t.Height(i, x, z);
            float top = without - f;
            return (f, top > 0f ? t.Weight(i, x, z, d, top) * TunnelGuard(x, z) : 0f, without);
        }
        return (float.NaN, 0f, float.NaN);
    }

    /// <summary>The tunnel lines (docs/13) with a covered run, and their stamps.</summary>
    public IEnumerable<RouteSkeleton> Tunnels => _tunnels.Where(t => t.Route.CoverStart >= 0).Select(t => t.Route);

    /// <summary>A tunnel cuts nothing inside the primary's level width and setback (the primary's corridor stays exact); its trench
    /// fades in over the first metres of the fillet's foot (docs/13, D-113).</summary>
    private float TunnelGuard(float x, float z)
    {
        int j = _primary.Nearest(x, z, SizeX, SizeZ, out float d);
        if (j < 0) return 1f;
        return Mathf.SmoothStep(0f, WorldScale.TunnelMouthFade, d - (_primary.Width(j) + _primary.Setback));
    }

    /// <summary>The tunnels' cut at a point (docs/13 §2.1): the strongest tunnel corridor weight there, given the ground without
    /// tunnels; 0 away from every tunnel. The point's floor and the cutting line come back for the stamp. Where the ground lies
    /// below the line's floor (a dive's S and ramp start beside a swell's low side, D-116) the trench is a fill instead, blended
    /// over the archetype's falloff: the floor is the planned floor from the fork on (a floor that rode the lower ground and then
    /// dropped into the cut was a convex kink that launched the base kit). A portal's floor is never above the canyon's ground.</summary>
    private float TunnelCut(float x, float z, float hWithout, out float floor)
    {
        float best = 0f; floor = hWithout;
        if (_tunnels.Count == 0) return 0f;
        foreach (var t in _tunnels)
        {
            int i = t.Nearest(x, z, SizeX, SizeZ, out float d);
            if (i < 0 || d > 80f || t.BeyondEnd(i, x, z)) continue;
            float f = t.Height(i, x, z);
            float top = hWithout - f;
            float w = t.Weight(i, x, z, d, top) * TunnelGuard(x, z);
            if (w > best) { best = w; floor = f; }
        }
        return best;
    }

    /// <summary>Whether a tunnel's cut reaches a point at all (the other lines' shells have a hole there, and the grid under it
    /// is not sunk by them: the tunnel's own shell and sink take over).</summary>
    private bool InTunnelCut(float x, float z)
    {
        if (_tunnels.Count == 0) return false;
        foreach (var t in _tunnels)
        {
            t.Nearest(x, z, SizeX, SizeZ, out float d);
            if (d <= 80f) return TunnelCut(x, z, SampleWithoutTunnels(x, z), out _) > 0f;
        }
        return false;
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

    /// <summary>Seeded value noise in [−1, 1] at a wavelength, from the stage's own noise stream: the far horizon's shapes (docs/13 §4, D-114).</summary>
    public float HorizonNoise(float x, float z, float wavelength) => ValueNoise(x / wavelength, z / wavelength, _noiseSeedA);

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
        return i < 0 ? 0f : _primary.Weight(i, x, z, d, SideHeight(x, z) - _primary.Height(i, x, z));
    }

    /// <summary>A line's wall-profile surface height at a point (corridor height plus the profile's rise, before the lip
    /// rounding); false where the point is inside the corridor or off the profile. Used to measure the surface's tilt
    /// along the route for the analytic wall's normal.</summary>
    private float ProfileHeightAt(StampedLine line, float x, float z, float side, out bool ok)
    {
        ok = false;
        int i = line.Nearest(x, z, SizeX, SizeZ, out float d);
        if (i < 0 || d < 1e-3f) return 0f;
        if (!line.ProfileAt(i, x, z, d, out float u, out float s0, out float ft) || u <= 0f) return 0f;
        float hc = line.Height(i, x, z);
        float rise = WallProfile.Height(u, s0, ft, line.Radius);
        if (side - hc <= 0f) return 0f;
        ok = true;
        return hc + rise;
    }

    /// <summary>The side terrain a corridor is cut into: relief plus the wall height, or the pit surface inside a spiral's rim.</summary>
    public float SideHeight(float x, float z)
    {
        float pitH = PitSurface(x, z);
        return float.IsNaN(pitH) ? Relief(x, z) + WallHeight : pitH;
    }

    /// <summary>
    /// The analytic wall under a point (D-109): on a walled archetype, the
    /// nearest stamped wall's surface normal (pointing off the wall, toward the corridor and up), the perpendicular gap
    /// from the ball's rest height on it, and the profile's curvature across (the fillet's, or 0 on the face). False
    /// inside a corridor, on a bend's inside, at the rounded lip (real physics launches the ball there), where the side
    /// terrain is not above the corridor, and on every archetype without walls.
    /// </summary>
    public bool WallSurface(Vector3 p, float ballRadius, out Vector3 normal, out float gap, out float curvature)
    {
        normal = Vector3.Up; gap = float.MaxValue; curvature = 0f;
        if (WallHeight <= 0f && _tunnels.Count == 0) return false;   // a dive's trench is a wall on any archetype (D-116)
        float side = SideHeight(p.X, p.Z), sideTunnel = _tunnels.Count > 0 ? SampleWithoutTunnels(p.X, p.Z) : side;
        // A line's wall is real only where the stamped ground is that wall: the stamps blend in order (Sample), so
        // beside a ledge cut into the wall, or inside another corridor, the ground is a mix or another line's floor, and
        // the follow reads the grid there instead. Without this the query reported a ledge's face inside the primary's
        // corridor and the follow lifted the ball up it.
        float stamped = Sample(p.X, p.Z);
        bool found = false;
        foreach (var line in _lines)
        {
            int i = line.Nearest(p.X, p.Z, SizeX, SizeZ, out float d);
            if (i < 0 || d < 1e-3f) continue;
            // A line's wall stands beside the line, never beyond its ends (D-111): 40 m before an optional line joined the
            // primary, its end vertex was the nearest to a point on the primary's floor, the distance to it ran along the
            // route, its fillet's foot agreed with the floor within 0.3 m, and the "wall" reported leaned forward like a
            // ramp; the carry turned 148 m/s of forward speed into a 99 m/s launch. The outward direction must also be
            // across the line's heading, not along it.
            if (line.BeyondEnd(i, p.X, p.Z)) continue;
            line.Foot(i, p.X, p.Z, out float footX, out float footZ);
            float exRaw = (p.X - footX) / d, ezRaw = (p.Z - footZ) / d;
            if (Mathf.Abs(exRaw * Mathf.Cos(line.Heading[i]) + ezRaw * Mathf.Sin(line.Heading[i])) > 0.5f) continue;
            if (!line.ProfileAt(i, p.X, p.Z, d, out float u, out float s0, out float ft) || u <= 0f) continue;
            float hc = line.Height(i, p.X, p.Z);
            float top = (line.Tunnel ? sideTunnel : side) - hc;   // a tunnel's wall rises to whatever ground stands over the trench
            float rise = WallProfile.Height(u, s0, ft, line.Radius);
            if (top <= 0f || rise > top - WorldScale.WallLipEase) continue;
            if (Mathf.Abs(stamped - (hc + rise)) > 0.3f) continue;
            // The foot's first metres are ground the grid follow already reads well; the analytic wall begins where the
            // fillet has a slope to hold (about 8°), so a flat stretch that merely agrees in height (a fork, a ledge's
            // start) never counts as a wall.
            float s = WallProfile.Slope(u, s0, ft, line.Radius);
            if (s < 0.15f) continue;
            // The surface also tilts along the route: the corridor's grade, and the wall's own origin and face moving
            // with a bend's extra width, berm and inside ease. The follow holds the ball to the surface's true normal,
            // so that tilt is measured on the same profile function four metres either way along the heading; without it
            // a wall receding at 7° along the route slid out from under a ride at 16 m/s and the follow lost the ball.
            float tx = Mathf.Cos(line.Heading[i]), tz = Mathf.Sin(line.Heading[i]);
            const float delta = 4f;
            float sideAlong = line.Tunnel ? sideTunnel : side;
            float hAhead = ProfileHeightAt(line, p.X + tx * delta, p.Z + tz * delta, sideAlong, out bool okA);
            float hBehind = ProfileHeightAt(line, p.X - tx * delta, p.Z - tz * delta, sideAlong, out bool okB);
            float along = okA && okB ? (hAhead - hBehind) / (2f * delta) : 0f;
            float ex = exRaw, ez = ezRaw;
            var n = new Vector3(-(ex * s + tx * along), 1f, -(ez * s + tz * along)).Normalized();
            float g = (p.Y - (hc + rise)) * n.Y - ballRadius;
            if (Mathf.Abs(g) < Mathf.Abs(gap)) { gap = g; normal = n; curvature = WallProfile.Curvature(u, s0, ft, line.Radius); found = true; }
        }
        return found;
    }

    /// <summary>The sink under the wall shell (D-111): how far below the stamp the collided heightfield sits at a point.
    /// Inside a profiled line's wall band, from the level foot <see cref="WorldScale.WallShellFoot"/> metres inside the
    /// edge to <see cref="WorldScale.WallShellPastLip"/> metres beyond the lip, the grid is sunk by
    /// the line's shell sink (<see cref="WorldScale.WallShellSink"/>; a tunnel's is deeper, its fillet being smaller), fading in over
    /// three metres from the corridor's edge (one cell inside the foot, so no cell under the flat foot is bent) and out over the
    /// last four so the shell and the grid coincide at both edges. Zero elsewhere, and on every unwalled archetype.</summary>
    public float Sink(float x, float z)
    {
        if ((WallHeight <= 0f && _tunnels.Count == 0) || !float.IsNaN(PitSurface(x, z))) return 0f;
        float best = 0f, side = float.NaN;
        // Inside a tunnel's cut the other lines' shells have a hole (docs/13 §2.1), so their sink stops there too; the tunnel's
        // own band (guarded out of the primary's level width, like its cut) sinks the trench's walls.
        bool inCut = InTunnelCut(x, z);
        foreach (var line in _lines)
        {
            if (inCut && !line.Tunnel) continue;
            int i = line.Nearest(x, z, SizeX, SizeZ, out float d);
            if (i < 0 || line.BeyondEnd(i, x, z) || !line.ProfileAt(i, x, z, d, out float u, out float s0, out float ft) || u <= -WorldScale.WallShellFoot) continue;
            float rawLateral = -Mathf.Sin(line.Heading[i]) * (x - line.X[i]) + Mathf.Cos(line.Heading[i]) * (z - line.Z[i]);
            if (d > line.Reach(i, Mathf.Sign(rawLateral))) continue;      // past the ray's reach on a bend's inside: no shell there
            if (float.IsNaN(side)) side = SideHeight(x, z);
            float top = (line.Tunnel ? SampleWithoutTunnels(x, z) : side) - line.Height(i, x, z);
            if (top <= 0f) continue;
            float uLip = WallProfile.LateralAtHeight(top, s0, ft, line.Radius);
            // The sink begins one cell inside the shell's foot, at the corridor's edge (D-113): the grid is bilinear over 4 m cells,
            // so a sunk node bends the collider for a whole cell around it, and a fade that began at the foot itself dipped the
            // floor before the shell started and stood the shell's edge a metre proud of it (a launch ramp at the cap on the
            // first tunnel drive, with the tunnel's 3 m sink). Under the flat foot the grid and the shell coincide instead.
            float s = Mathf.SmoothStep(-WorldScale.WallShellFoot + WorldScale.CellSize, -WorldScale.WallShellFoot + WorldScale.CellSize + 3f, u) * (1f - Mathf.SmoothStep(uLip + WorldScale.WallShellPastLip - 4f, uLip + WorldScale.WallShellPastLip, u));
            if (line.Tunnel) s *= TunnelGuard(x, z);
            best = Mathf.Max(best, s * line.ShellSink);
        }
        return best;
    }

    /// <summary>The wall shells (D-111): for every profiled line and each side, the stamp sampled along the line's lateral
    /// rays at the shell's stations, as a grid of points (vertex-major) with smooth normals and the terrain colour. The
    /// shell is the stamp itself, sampled finely where it curves, so it agrees with the analytic wall the follow reads
    /// and with the grid that lies sunk beneath it. On the inside of a bend the rays are cut short of the bend's centre,
    /// where they would cross; stations past a vertex's own lip collapse onto its last point (skipped as faces).</summary>
    public List<WallShellStrip> ShellStrips()
    {
        var strips = new List<WallShellStrip>();
        foreach (var line in _lines)
        {
            if (!line.Profiled) continue;   // every line of a walled archetype; a tunnel's trench on any archetype (D-116)
            int n = line.N;
            foreach (float sideSign in new[] { 1f, -1f })
            {
                // Each vertex's edge, lip and reach along its ray.
                var edge = new float[n]; var uEnd = new float[n]; var reach = new float[n];
                float uMax = 0f;
                for (int i = 0; i < n; i++)
                {
                    float lx = -Mathf.Sin(line.Heading[i]) * sideSign, lz = Mathf.Cos(line.Heading[i]) * sideSign;
                    float dProbe = line.Width(i) + WorldScale.WallSetback;
                    float px = line.X[i] + lx * dProbe, pz = line.Z[i] + lz * dProbe;
                    line.ProfileAt(i, px, pz, dProbe, out float u0, out _, out _);
                    edge[i] = dProbe - u0;
                    float fx = line.X[i] + lx * (edge[i] + 40f), fz = line.Z[i] + lz * (edge[i] + 40f);
                    line.ProfileAt(i, fx, fz, edge[i] + 40f, out _, out float s0, out float ft);
                    float top = (line.Tunnel ? SampleWithoutTunnels(fx, fz) : SideHeight(fx, fz)) - line.Height(i, fx, fz);
                    uEnd[i] = top > 0f ? WallProfile.LateralAtHeight(top, s0, ft, line.Radius) + WorldScale.WallShellPastLip : 0f;
                    // A tunnel's walls stop at the arch's spring line under the roof (docs/13 §2.1); the open cut before the
                    // portal keeps the whole trench wall.
                    if (line.Tunnel && line.Route.Covered(i)) uEnd[i] = Mathf.Min(uEnd[i], TunnelProfile.SpringU);
                    // The inside of a bend: the ray stops a metre short of the centre of curvature.
                    reach[i] = line.Reach(i, sideSign);
                    uMax = Mathf.Max(uMax, uEnd[i]);
                }
                if (uMax <= 0f) continue;
                float[] stations = WallProfile.ShellStations(uMax, line.Radius, line.Tunnel ? WorldScale.TunnelFaceDegrees : null);
                int k = stations.Length;
                // The fillet's stations are absolute (the arc is the same at every vertex); the face's are proportional to each
                // vertex's own lip (D-116: the face is planar, so any station on it is exact, and absolute stations folded past a
                // lower lip made a sawtooth along every lip whose height changed vertex to vertex).
                float uFillet = line.Radius * Mathf.Sin(Mathf.DegToRad(line.Tunnel ? WorldScale.TunnelFaceDegrees : WorldScale.WallFaceDegrees));
                float StationAt(int vi, int sj)
                {
                    float st = stations[sj], top = uEnd[vi];
                    if (top <= 0f) return Mathf.Min(st, 0f);
                    if (top < uFillet) return st <= 0f ? st : st * top / uFillet;
                    if (st <= uFillet || uMax - uFillet < 1e-3f) return Mathf.Min(st, top);
                    return uFillet + (top - uFillet) * (st - uFillet) / (uMax - uFillet);
                }
                var pts = new Vector3[n * k];
                var valid = new bool[n * k];
                // A vertex per task (docs/13 §3.3: the field is pure once generated; the four canyon strips took 0.3 s on one core).
                System.Threading.Tasks.Parallel.For(0, n, i =>
                {
                    float lx = -Mathf.Sin(line.Heading[i]) * sideSign, lz = Mathf.Cos(line.Heading[i]) * sideSign;
                    for (int j = 0; j < k; j++)
                    {
                        float u = StationAt(i, j);
                        float d = Mathf.Min(edge[i] + u, reach[i]);
                        float px = line.X[i] + lx * d, pz = line.Z[i] + lz * d;
                        pts[i * k + j] = new Vector3(px, Sample(px, pz), pz);
                        // A station is the wall of vertex i only while vertex i is still the nearest: past a bend's centre a
                        // straight's ray runs on into the far side of the bend (across its exit corridor on a hairpin), and a
                        // strip joining those points to the clamped neighbour laid a ramp across the corridor.
                        int owner = line.Nearest(px, pz, SizeX, SizeZ, out float nd);
                        bool owned = owner == i || nd >= d - 0.5f;
                        // The hole (docs/13 §2.7): no station of another line stands inside a tunnel's cut; a tunnel's own band
                        // exists only where its cut does (beyond the primary's level width).
                        bool tunnelOk = line.Tunnel ? TunnelGuard(px, pz) > 0f : !InTunnelCut(px, pz);
                        valid[i * k + j] = owned && tunnelOk && uEnd[i] > 0f && Mathf.Abs(px) <= SizeX * 0.5f && Mathf.Abs(pz) <= SizeZ * 0.5f && float.IsNaN(PitSurface(px, pz));
                    }
                });
                var norms = new Vector3[n * k];
                var cols = new Color[n * k];
                System.Threading.Tasks.Parallel.For(0, n, i =>
                {
                    for (int j = 0; j < k; j++)
                    {
                        int ia = Mathf.Max(0, i - 1), ib = Mathf.Min(n - 1, i + 1), ja = Mathf.Max(0, j - 1), jb = Mathf.Min(k - 1, j + 1);
                        Vector3 along = pts[ib * k + j] - pts[ia * k + j];
                        Vector3 across = pts[i * k + jb] - pts[i * k + ja];
                        Vector3 nrm = along.Cross(across);
                        if (nrm.LengthSquared() < 1e-8f) nrm = Vector3.Up;
                        nrm = nrm.Normalized();
                        if (nrm.Y < 0f) nrm = -nrm;
                        norms[i * k + j] = nrm;
                        Color col = SampleColor(pts[i * k + j], nrm);
                        if (line.Tunnel && line.Route.Covered(i)) col = col.Darkened(1f - TunnelProfile.Shade(PortalDistance(line.Route, i)));
                        cols[i * k + j] = col;
                    }
                });
                strips.Add(new WallShellStrip(n, k, pts, norms, cols, valid));
            }
        }
        return strips;
    }

    /// <summary>Route distance from a covered vertex to the nearer portal.</summary>
    private static float PortalDistance(RouteSkeleton line, int i)
    {
        var v = line.Vertices;
        return Mathf.Min(v[i].Distance - v[line.CoverStart].Distance, v[line.CoverEnd].Distance - v[i].Distance);
    }

    /// <summary>
    /// The tunnel roofs (docs/13 §2.1, D-113): for every covered tunnel, strips built like the wall shells and collided like
    /// them: the <b>arch</b> (the underside, one arc per covered vertex from spring point to spring point), the <b>cap</b>
    /// (the ground without the tunnel over the trench's footprint, so the surface continues over the tunnel and is drivable),
    /// a <b>portal face</b> at each end (the rock between the trench's walls, the arch and the cap, in the portal's plane) and
    /// its <b>rim</b> (a rock lip round the arch, so the opening reads against the wall from far off). Normals are set by the
    /// surface's side: the arch faces into the tunnel, the cap up, a face out of the tunnel.
    /// </summary>
    public List<WallShellStrip> RoofStrips()
    {
        var strips = new List<WallShellStrip>();
        foreach (var t in _tunnels)
        {
            var line = t.Route;
            if (line.CoverStart < 0) continue;
            int n = line.CoverEnd - line.CoverStart + 1;
            float[] angles = TunnelProfile.ArchAngles();
            int ka = angles.Length;
            // Per covered vertex: the centre, the lateral direction, the floor and the cap's reach.
            var cx = new float[n]; var cz = new float[n]; var lx = new float[n]; var lz = new float[n]; var floor = new float[n]; var capReach = new float[n];
            float capMax = 0f;
            for (int q = 0; q < n; q++)
            {
                int i = line.CoverStart + q;
                cx[q] = t.X[i]; cz[q] = t.Z[i]; lx[q] = -Mathf.Sin(t.Heading[i]); lz[q] = Mathf.Cos(t.Heading[i]);
                floor[q] = t.Height(i, cx[q], cz[q]);
                capReach[q] = TunnelProfile.CapLateral(SampleWithoutTunnels(cx[q], cz[q]) - floor[q]);
                capMax = Mathf.Max(capMax, capReach[q]);
            }
            bool Inside(float px, float pz) => Mathf.Abs(px) <= SizeX * 0.5f && Mathf.Abs(pz) <= SizeZ * 0.5f;

            // The arch.
            {
                var pts = new Vector3[n * ka]; var nrm = new Vector3[n * ka]; var col = new Color[n * ka]; var ok = new bool[n * ka];
                for (int q = 0; q < n; q++)
                {
                    float shade = TunnelProfile.Shade(PortalDistance(line, line.CoverStart + q));
                    for (int j = 0; j < ka; j++)
                    {
                        float l = TunnelProfile.ArchRadius * Mathf.Sin(angles[j]);
                        float h = TunnelProfile.ArchCentre + TunnelProfile.ArchRadius * Mathf.Cos(angles[j]);
                        var p = new Vector3(cx[q] + lx[q] * l, floor[q] + h, cz[q] + lz[q] * l);
                        // Into the tunnel: from the surface toward the arc's centre.
                        var toCentre = new Vector3(cx[q], floor[q] + TunnelProfile.ArchCentre, cz[q]) - p;
                        pts[q * ka + j] = p; nrm[q * ka + j] = toCentre.Normalized();
                        col[q * ka + j] = SampleColor(p, Vector3.Up).Darkened(1f - shade);
                        ok[q * ka + j] = Inside(p.X, p.Z);
                    }
                }
                strips.Add(new WallShellStrip(n, ka, pts, nrm, col, ok));
            }
            // The cap: stations across the trench's footprint every 8 m and at both edges, at the ground without the tunnel.
            {
                int kc = Mathf.Max(3, Mathf.CeilToInt(2f * capMax / 8f) + 1);
                var pts = new Vector3[n * kc]; var nrm = new Vector3[n * kc]; var col = new Color[n * kc]; var ok = new bool[n * kc];
                for (int q = 0; q < n; q++)
                    for (int j = 0; j < kc; j++)
                    {
                        float l = Mathf.Lerp(-capReach[q], capReach[q], j / (float)(kc - 1));
                        float px = cx[q] + lx[q] * l, pz = cz[q] + lz[q] * l;
                        pts[q * kc + j] = new Vector3(px, SampleWithoutTunnels(px, pz), pz);
                        ok[q * kc + j] = Inside(px, pz);
                    }
                for (int q = 0; q < n; q++)
                    for (int j = 0; j < kc; j++)
                    {
                        int qa = Mathf.Max(0, q - 1), qb = Mathf.Min(n - 1, q + 1), ja = Mathf.Max(0, j - 1), jb = Mathf.Min(kc - 1, j + 1);
                        Vector3 g = (pts[qb * kc + j] - pts[qa * kc + j]).Cross(pts[q * kc + jb] - pts[q * kc + ja]);
                        g = g.LengthSquared() < 1e-8f ? Vector3.Up : g.Normalized();
                        if (g.Y < 0f) g = -g;
                        nrm[q * kc + j] = g;
                        col[q * kc + j] = SampleColor(pts[q * kc + j], g);
                    }
                strips.Add(new WallShellStrip(n, kc, pts, nrm, col, ok));
            }
            // The portal faces and their rims, one at each end.
            foreach (int end in new[] { 0, n - 1 })
            {
                int i = line.CoverStart + end;
                float outward = end == 0 ? -1f : 1f;   // along the heading, out of the tunnel
                float hx = Mathf.Cos(t.Heading[i]) * outward, hz = Mathf.Sin(t.Heading[i]) * outward;
                var faceN = new Vector3(hx, 0f, hz);
                // Stations across the face: the trench wall from the cap's edge down to the spring on each side, the arch between.
                var lat = new List<float>(); var bottom = new List<float>();
                int wallStations = 6;
                for (int j = 0; j <= wallStations; j++)
                {
                    float l = Mathf.Lerp(-capReach[end], -TunnelProfile.SpringLateral, j / (float)wallStations);
                    lat.Add(l); bottom.Add(float.NaN);   // NaN: the trench wall (the stamp) at that lateral
                }
                for (int j = 1; j < ka - 1; j++)
                {
                    lat.Add(TunnelProfile.ArchRadius * Mathf.Sin(angles[j]));
                    bottom.Add(floor[end] + TunnelProfile.ArchCentre + TunnelProfile.ArchRadius * Mathf.Cos(angles[j]));
                }
                for (int j = 0; j <= wallStations; j++)
                {
                    float l = Mathf.Lerp(TunnelProfile.SpringLateral, capReach[end], j / (float)wallStations);
                    lat.Add(l); bottom.Add(float.NaN);
                }
                int kf = lat.Count;
                var pts = new Vector3[2 * kf]; var nrm = new Vector3[2 * kf]; var col = new Color[2 * kf]; var ok = new bool[2 * kf];
                for (int j = 0; j < kf; j++)
                {
                    float px = cx[end] + lx[end] * lat[j], pz = cz[end] + lz[end] * lat[j];
                    float lo = float.IsNaN(bottom[j]) ? Sample(px, pz) : bottom[j];
                    float hi = SampleWithoutTunnels(px, pz);
                    pts[j] = new Vector3(px, lo, pz); pts[kf + j] = new Vector3(px, Mathf.Max(lo, hi), pz);
                    nrm[j] = faceN; nrm[kf + j] = faceN;
                    col[j] = SampleColor(pts[j], faceN); col[kf + j] = SampleColor(pts[kf + j], faceN);
                    ok[j] = ok[kf + j] = Inside(px, pz);
                }
                strips.Add(new WallShellStrip(2, kf, pts, nrm, col, ok));
                // The rim: a rectangular band round the arch, one rim size proud of the face and one rim size thick.
                float rs = WorldScale.TunnelRimSize;
                var rp = new Vector3[4 * ka]; var rn = new Vector3[4 * ka]; var rc = new Color[4 * ka]; var ro = new bool[4 * ka];
                var centre = new Vector3(cx[end], floor[end] + TunnelProfile.ArchCentre, cz[end]);
                for (int j = 0; j < ka; j++)
                {
                    float sn = Mathf.Sin(angles[j]), cs = Mathf.Cos(angles[j]);
                    Vector3 radial = new Vector3(lx[end] * sn, cs, lz[end] * sn);          // from the arc's centre outward
                    Vector3 inner = centre + radial * TunnelProfile.ArchRadius, outer = centre + radial * (TunnelProfile.ArchRadius + rs);
                    Vector3 proud = faceN * rs;
                    rp[0 * ka + j] = inner; rp[1 * ka + j] = inner + proud; rp[2 * ka + j] = outer + proud; rp[3 * ka + j] = outer;
                    rn[0 * ka + j] = -radial; rn[1 * ka + j] = (faceN - radial).Normalized(); rn[2 * ka + j] = (faceN + radial).Normalized(); rn[3 * ka + j] = radial;
                    for (int r = 0; r < 4; r++) { rc[r * ka + j] = SampleColor(rp[r * ka + j], Vector3.Up).Darkened(0.15f); ro[r * ka + j] = Inside(rp[r * ka + j].X, rp[r * ka + j].Z); }
                }
                strips.Add(new WallShellStrip(4, ka, rp, rn, rc, ro));
            }
        }
        return strips;
    }

    /// <summary>
    /// The roof over a plan position (docs/13 §2.5): the arch's underside height at the point's lateral offset inside a
    /// covered tunnel; NaN elsewhere. Within <see cref="WorldScale.TunnelCameraEase"/> before a portal (or after) the answer
    /// rises linearly away from the arch, so a confinement reading it eases in and out.
    /// </summary>
    public float ArchOver(float x, float z)
    {
        foreach (var t in _tunnels)
        {
            var line = t.Route;
            if (line.CoverStart < 0) continue;
            int i = t.Nearest(x, z, SizeX, SizeZ, out float d);
            if (i < 0 || d > TunnelProfile.SpringLateral + 2f) continue;
            float lateral = -Mathf.Sin(t.Heading[i]) * (x - t.X[i]) + Mathf.Cos(t.Heading[i]) * (z - t.Z[i]);
            if (Mathf.Abs(lateral) > TunnelProfile.SpringLateral) continue;
            var v = line.Vertices;
            float along = (x - t.X[i]) * Mathf.Cos(t.Heading[i]) + (z - t.Z[i]) * Mathf.Sin(t.Heading[i]);
            float dist = v[i].Distance + along;
            float outside = Mathf.Max(v[line.CoverStart].Distance - dist, dist - v[line.CoverEnd].Distance);
            if (outside > WorldScale.TunnelCameraEase) continue;
            float arch = t.Height(i, x, z) + TunnelProfile.ArchHeight(lateral);
            return outside <= 0f ? arch : arch + outside / WorldScale.TunnelCameraEase * 40f;
        }
        return float.NaN;
    }

    /// <summary>
    /// The cap over a plan position (docs/13 §2.1): true under a covered tunnel's roof footprint, with the cap's top (the
    /// ground without the tunnel). A height query given the ball's own height answers the cap where the ball is above the
    /// cap's underside less a ball, the trench otherwise.
    /// </summary>
    public bool CapOver(float x, float z, out float capTop)
    {
        capTop = 0f;
        foreach (var t in _tunnels)
        {
            var line = t.Route;
            if (line.CoverStart < 0) continue;
            int i = t.Nearest(x, z, SizeX, SizeZ, out float d);
            if (i < 0 || !line.Covered(i) || d > 60f) continue;
            float lateral = -Mathf.Sin(t.Heading[i]) * (x - t.X[i]) + Mathf.Cos(t.Heading[i]) * (z - t.Z[i]);
            float hWithout = SampleWithoutTunnels(x, z);
            if (Mathf.Abs(lateral) > TunnelProfile.CapLateral(hWithout - t.Height(i, x, z))) continue;
            capTop = hWithout;
            return true;
        }
        return false;
    }

    /// <summary>Combined corridor weight at a point: 1 inside any corridor, 0 beyond every falloff.</summary>
    public float CorridorWeight(float x, float z)
    {
        float w = 0f;
        float side = SideHeight(x, z);
        foreach (var line in _lines)
        {
            int i = line.Nearest(x, z, SizeX, SizeZ, out float d);
            if (i < 0) continue;
            w = Mathf.Max(w, line.Weight(i, x, z, d, side - line.Height(i, x, z)));
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

    /// <summary>The ground with every tunnel's trench cut into it (docs/13 §2.1): <see cref="SampleWithoutTunnels"/>, then each
    /// tunnel lowers it toward its floor by its corridor weight where the ground stands above the floor. The roof's cap is the
    /// ground without the tunnel, so the two agree wherever the trench has faded.</summary>
    public float Sample(float x, float z)
    {
        float h = SampleWithoutTunnels(x, z);
        if (_tunnels.Count == 0) return h;
        float w = TunnelCut(x, z, h, out float floor);
        return w > 0f ? Mathf.Lerp(h, floor, w) : h;
    }

    /// <summary>The ground as it would be with no tunnel: the relief, the side terrain, the pit, every other line's stamp.</summary>
    public float SampleWithoutTunnels(float x, float z)
    {
        float h = Relief(x, z) + WallHeight;
        float pitH = PitSurface(x, z);
        if (!float.IsNaN(pitH)) h = pitH;
        // Optional lines stamp first and the primary last, so inside the primary corridor the primary's
        // profile is exact (a ridge's transition beside it must never kink the centreline: a metre over a
        // cell launches a ceiling ball) while a ridge's own centreline is its profile wherever the primary
        // has faded. Their heights agree where both are full, so the overlap is continuous.
        float wOpt = 0f;
        for (int k = _lines.Count - 1; k >= 0; k--)
        {
            var line = _lines[k];
            if (line.Tunnel) continue;
            int i = line.Nearest(x, z, SizeX, SizeZ, out float d);
            if (i < 0) continue;
            float lineH = line.Height(i, x, z);
            float w = line.Weight(i, x, z, d, h - lineH);   // the wall rises from this line to whatever stands beyond it
            if (w <= 0f) continue;
            if (ReferenceEquals(line, _primary))
            {
                // The primary's corridor is exact; its wall zone (D-109, wide now) yields to an optional line's corridor
                // where a ledge's ramp crosses it, so the ramp keeps its shape instead of being pulled toward the floor.
                if (line.ProfileAt(i, x, z, d, out float u, out _, out _) && u > 0f) w *= 1f - wOpt;   // walled archetypes only
            }
            else wOpt = Mathf.Max(wOpt, w);
            h = Mathf.Lerp(h, lineH, w);
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
            // Strata (D-109): the rock is in the tint, not the mesh. Bands of height every 14 m on steep faces, the
            // deeper ones darker, so a smooth face still reads as layered stone at speed.
            float band = 0.5f + 0.5f * Mathf.Sin(point.Y * (Mathf.Tau / 14f));
            float strata = Mathf.SmoothStep(0.3f, 0.7f, slope) * Mathf.SmoothStep(0.05f, 0.3f, above) * (0.35f * band + 0.15f * (1f - above));
            c = c.Lerp(CanyonRock.Darkened(0.35f), strata);
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

/// <summary>One wall shell (D-111): a vertex-major grid of <c>Vertices × Stations</c> points along a line's side, with
/// smooth normals and terrain colours; a point marked invalid lies outside the footprint or on a vertex with no wall.</summary>
public readonly record struct WallShellStrip(int Vertices, int Stations, Vector3[] Points, Vector3[] Normals, Color[] Colors, bool[] Valid);
