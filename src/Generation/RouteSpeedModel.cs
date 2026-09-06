using Godot;
using Rushcore.Tuning;

namespace Rushcore.Generation;

/// <summary>
/// Speed profile of the base movement kit along a route polyline (04 §12, D-081).
/// One entry per polyline vertex: cumulative 3D arclength, arrival speed, arrival time and
/// the corner speed limit that applied there. Pure data; no scene dependency.
/// </summary>
public sealed class RouteSpeedProfile
{
    public float[] Distance { get; }
    public float[] Speed { get; }
    public float[] Time { get; }
    /// <summary>Speed the steering envelope allows through each vertex (infinity on straights).</summary>
    public float[] CornerLimit { get; }
    /// <summary>True if the ball came to rest before the end (uphill with no drive).</summary>
    public bool Stalled { get; internal set; }
    public int StallVertex { get; internal set; } = -1;
    public float MaxSpeed { get; internal set; }

    public RouteSpeedProfile(int vertices)
    {
        Distance = new float[vertices];
        Speed = new float[vertices];
        Time = new float[vertices];
        CornerLimit = new float[vertices];
    }

    public int Count => Distance.Length;
    public float TotalLength => Distance[^1];
    public float TotalTime => Time[^1];

    public float SpeedAt(float s) => Interpolate(Speed, s);
    public float TimeAt(float s) => Interpolate(Time, s);

    private float Interpolate(float[] values, float s)
    {
        if (s <= Distance[0]) return values[0];
        if (s >= Distance[^1]) return values[^1];
        int hi = Array.BinarySearch(Distance, s);
        if (hi >= 0) return values[hi];
        hi = ~hi;
        int lo = hi - 1;
        float span = Distance[hi] - Distance[lo];
        float f = span > 0f ? (s - Distance[lo]) / span : 0f;
        return Mathf.Lerp(values[lo], values[hi], f);
    }

    /// <summary>FNV-1a over the speed and time arrays; equal profiles hash equal (G0 determinism).</summary>
    public ulong Hash()
    {
        ulong h = 14695981039346656037UL;
        void Mix(float f)
        {
            uint bits = (uint)BitConverter.SingleToInt32Bits(f);
            for (int i = 0; i < 4; i++) { h ^= (bits >> (8 * i)) & 0xFF; h *= 1099511628211UL; }
        }
        for (int i = 0; i < Count; i++) { Mix(Speed[i]); Mix(Time[i]); }
        return h;
    }
}

/// <summary>
/// Deterministic 1D integration of the frozen movement baseline (03 §15) along a polyline,
/// base kit only: drive held, gravity times the local grade, drag, the hard cap and a
/// conservative bend loss from the steering envelope. No boost, no landing burst, no
/// airborne phases (a crest that launches the ball is classified with
/// <see cref="CrestIsLaunch"/>; the ground profile stays conservative through it).
///
/// The tick order mirrors <c>PlayerPhysics._IntegrateForces</c> so the harness can hold it
/// within 5% of the real ball (08 §5): gravity → drive → slip friction → multiplicative drag → cap.
/// </summary>
public sealed class RouteSpeedModel
{
    /// <summary>Fixed step so the profile never depends on the caller's frame rate.</summary>
    public const float TickSeconds = 1f / 60f;
    /// <summary>Speed below which a driverless ball on a grade counts as stalled.</summary>
    private const float StallSpeed = 0.05f;
    private const int MaxTicks = 60 * 60 * 20;   // twenty minutes of route; a bound, not a target

    private readonly MovementTuning _m;

    public RouteSpeedModel(MovementTuning movement) => _m = movement;

    public float Cap => Mathf.Max(0.001f, _m.HardMaxLocomotionSpeed);

    /// <summary>Lateral steering authority at speed v (03 §15, V-001).</summary>
    public float LateralAuthority(float v) =>
        _m.GroundSteeringLateralAccel * Mathf.Lerp(1f, _m.HighSpeedSteeringMultiplier, Mathf.Clamp(v / Cap, 0f, 1f));

    /// <summary>Steady turn radius at speed v: v² / a_lat(v).</summary>
    public float TurnRadius(float v) => v * v / Mathf.Max(0.001f, LateralAuthority(v));

    /// <summary>
    /// Fastest speed the steering envelope holds through a bend of the given radius
    /// (the inverse of <see cref="TurnRadius"/>, closed form), clamped to the cap.
    /// </summary>
    public float CornerSpeedLimit(float radius)
    {
        if (radius <= 0f) return 0f;
        if (float.IsInfinity(radius)) return Cap;
        float a0 = _m.GroundSteeringLateralAccel;
        float k = (_m.HighSpeedSteeringMultiplier - 1f) / Cap;
        // v² = r·a0·(1 + k·v)  →  v² − r·a0·k·v − r·a0 = 0
        float b = radius * a0 * k;
        float c = radius * a0;
        float v = 0.5f * (b + Mathf.Sqrt(b * b + 4f * c));
        return Mathf.Min(v, Cap);
    }

    /// <summary>Smallest crest radius that keeps a ball at speed v on the ground: v² / g (04 §8).</summary>
    public float CrestLaunchRadius(float v) => v * v / Mathf.Max(0.001f, _m.Gravity);

    public bool CrestIsLaunch(float crestRadius, float v) => crestRadius < CrestLaunchRadius(v);

    /// <summary>Crest radius of a cosine hill of wavelength λ and height H: λ² / (2π²H).</summary>
    public static float CosineCrestRadius(float wavelength, float height) =>
        height <= 0f ? float.PositiveInfinity : wavelength * wavelength / (2f * Mathf.Pi * Mathf.Pi * height);

    /// <summary>
    /// Integrates the base kit along the polyline. Arrival speed at each vertex is clamped
    /// to that vertex's corner limit: the player is assumed to brake to what the bend allows,
    /// which under-predicts speed after bends, never over-predicts it.
    /// </summary>
    public RouteSpeedProfile Integrate(IReadOnlyList<Vector3> polyline, float entrySpeed = 0f, bool driveHeld = true)
    {
        int n = polyline.Count;
        if (n < 2) throw new ArgumentException("A route needs at least two vertices.", nameof(polyline));

        var profile = new RouteSpeedProfile(n);
        var segLen = new float[n - 1];
        var sinGrade = new float[n - 1];   // positive downhill
        var flatDir = new Vector2[n - 1];
        for (int i = 0; i < n - 1; i++)
        {
            Vector3 d = polyline[i + 1] - polyline[i];
            float len = d.Length();
            segLen[i] = len;
            sinGrade[i] = len > 1e-6f ? -d.Y / len : 0f;
            Vector2 flat = new(d.X, d.Z);
            flatDir[i] = flat.LengthSquared() > 1e-12f ? flat.Normalized() : Vector2.Zero;
            profile.Distance[i + 1] = profile.Distance[i] + len;
        }

        profile.CornerLimit[0] = float.PositiveInfinity;
        profile.CornerLimit[n - 1] = float.PositiveInfinity;
        for (int i = 1; i < n - 1; i++)
        {
            float turn = flatDir[i - 1] == Vector2.Zero || flatDir[i] == Vector2.Zero ? 0f : flatDir[i - 1].AngleTo(flatDir[i]);
            turn = Mathf.Abs(turn);
            if (turn < 1e-3f) { profile.CornerLimit[i] = float.PositiveInfinity; continue; }
            float radius = 0.5f * (segLen[i - 1] + segLen[i]) / turn;
            profile.CornerLimit[i] = CornerSpeedLimit(radius);
        }

        float cap = Cap;
        float g = _m.Gravity;
        float drive = driveHeld ? _m.GroundDriveAcceleration : 0f;
        // The solver friction is tiny but the controller re-slips the ball every tick, so it
        // acts as a constant rolling loss of μ·g·cosθ while moving (measured on the runway).
        float slip = MovementTuning.SurfaceFriction * g;
        float dragKeep = Mathf.Max(0f, 1f - _m.DragCoefficient * TickSeconds);

        float v = Mathf.Clamp(entrySpeed, 0f, cap);
        float t = 0f, segPos = 0f;
        int seg = 0;
        profile.Speed[0] = v;
        profile.Time[0] = 0f;
        profile.MaxSpeed = v;

        for (int tick = 0; tick < MaxTicks && seg < n - 1; tick++)
        {
            float cosGrade = Mathf.Sqrt(Mathf.Max(0f, 1f - sinGrade[seg] * sinGrade[seg]));
            float loss = v > 0f ? slip * cosGrade : 0f;
            float vNew = v + (g * sinGrade[seg] + drive - loss) * TickSeconds;
            vNew = Mathf.Max(0f, vNew) * dragKeep;
            vNew = Mathf.Min(vNew, cap);
            float advance = vNew * TickSeconds;
            t += TickSeconds;

            if (advance <= 0f && vNew < StallSpeed)
            {
                profile.Stalled = true;
                profile.StallVertex = seg;
                break;
            }

            float remaining = segLen[seg] - segPos;
            while (advance >= remaining && seg < n - 1)
            {
                float fraction = advance > 0f ? remaining / advance : 1f;
                int vi = seg + 1;
                float arrival = Mathf.Lerp(v, vNew, fraction);
                arrival = Mathf.Min(arrival, profile.CornerLimit[vi]);
                profile.Speed[vi] = arrival;
                profile.Time[vi] = t - TickSeconds * (1f - fraction);
                vNew = Mathf.Min(vNew, profile.CornerLimit[vi]);
                advance -= remaining;
                seg++;
                segPos = 0f;
                if (seg < n - 1) remaining = segLen[seg];
            }
            segPos += advance;
            v = vNew;
            profile.MaxSpeed = Mathf.Max(profile.MaxSpeed, v);
        }

        if (profile.Stalled || seg < n - 1)
        {
            // Unreached vertices: speed 0, infinite time, so a validator reads them as failures.
            for (int i = seg + 1; i < n; i++)
            {
                profile.Speed[i] = 0f;
                profile.Time[i] = float.PositiveInfinity;
            }
            if (!profile.Stalled) { profile.Stalled = true; profile.StallVertex = seg; }
        }
        return profile;
    }

    /// <summary>Straight, flat polyline of the given length at the given vertex spacing.</summary>
    public static Vector3[] StraightPolyline(Vector3 start, Vector3 direction, float length, float spacing = 1f)
    {
        direction = new Vector3(direction.X, 0f, direction.Z).Normalized();
        int count = Mathf.Max(2, Mathf.CeilToInt(length / spacing) + 1);
        var pts = new Vector3[count];
        for (int i = 0; i < count; i++) pts[i] = start + direction * Mathf.Min(length, i * spacing);
        return pts;
    }
}
