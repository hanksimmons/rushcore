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
    /// <summary>Ballistic phases (D-094): where the ball left the ground, where it landed, and how hard.</summary>
    public List<Flight> Flights { get; } = new();
    public float AirborneSeconds { get; internal set; }

    /// <summary>Seconds spent below a speed (segment time is charged to the arrival vertex).</summary>
    public float SecondsBelow(float speed)
    {
        float t = 0f;
        for (int i = 1; i < Count; i++)
            if (Speed[i] < speed && float.IsFinite(Time[i])) t += Time[i] - Time[i - 1];
        return t;
    }

    /// <summary>Time at which the profile first reaches a speed (the standing start), or the total time.</summary>
    public float TimeToReach(float speed)
    {
        for (int i = 0; i < Count; i++) if (Speed[i] >= speed) return Time[i];
        return TotalTime;
    }

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

/// <summary>One ballistic phase of a profile (D-094): distances are route distances.</summary>
public sealed class Flight
{
    public float LaunchDistance, LandingDistance, LaunchSpeed, Seconds;
    /// <summary>Downward speed at touchdown; above the Flow plain-landing threshold only a slam keeps Flow.</summary>
    public float LandingVerticalSpeed;
    /// <summary>Ground-tangent speed the ball keeps after touchdown (the normal component is absorbed).</summary>
    public float LandingSpeed;
    public float Length => LandingDistance - LaunchDistance;
}

/// <summary>
/// Deterministic 1D integration of the frozen movement baseline (03 §15) along a polyline,
/// base kit only: drive held, gravity times the local grade, drag, the hard cap and a
/// conservative bend loss from the steering envelope. No boost, no landing burst. Where the
/// surface curves away faster than gravity can follow (v²κ ≥ g cos θ, the same rule the
/// controller's ground follow applies, 03 §3) the ball flies: horizontal speed under air
/// control and drag, gravity on the vertical, touchdown where the path meets the polyline
/// again with the normal component absorbed (D-094).
///
/// With a headroom the model integrates the Flow ceiling (04 §12): the speed cap is
/// base × (1 + headroom) while the steering authority still saturates at the base cap.
///
/// The tick order mirrors <c>PlayerPhysics._IntegrateForces</c> so the harness can hold it
/// within 5% of the real ball (08 §5): gravity → drive → slip friction → multiplicative drag → cap.
/// </summary>
public sealed class RouteSpeedModel
{
    /// <summary>Fixed step so the profile never depends on the caller's frame rate.</summary>
    public const float TickSeconds = 1f / 60f;
    /// <summary>Vertices each side over which launch curvature is read: three cells, as the controller does.</summary>
    private const int CurvatureSpan = 3;
    /// <summary>Speed below which a driverless ball on a grade counts as stalled.</summary>
    private const float StallSpeed = 0.05f;
    private const int MaxTicks = 60 * 60 * 20;   // twenty minutes of route; a bound, not a target

    private readonly MovementTuning _m;

    private readonly float _headroom;

    public RouteSpeedModel(MovementTuning movement, float headroom = 0f)
    {
        _m = movement;
        _headroom = Mathf.Max(0f, headroom);
    }

    /// <summary>The frozen base cap: steering saturates here whatever the speed cap is.</summary>
    public float BaseCap => Mathf.Max(0.001f, _m.HardMaxLocomotionSpeed);
    /// <summary>The speed this profile is capped at: the base cap, or the Flow ceiling with a headroom.</summary>
    public float Cap => BaseCap * (1f + _headroom);
    public bool IsCeiling => _headroom > 0f;
    /// <summary>The brake sheds speed at the drive acceleration (03 §4, D-076): what a landing run can absorb.</summary>
    public float BrakeDeceleration => _m.GroundDriveAcceleration;

    /// <summary>Lateral steering authority at speed v (03 §15, V-001).</summary>
    public float LateralAuthority(float v) =>
        _m.GroundSteeringLateralAccel * Mathf.Lerp(1f, _m.HighSpeedSteeringMultiplier, Mathf.Clamp(v / BaseCap, 0f, 1f));

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
        float k = (_m.HighSpeedSteeringMultiplier - 1f) / BaseCap;
        // v² = r·a0·(1 + k·v)  →  v² − r·a0·k·v − r·a0 = 0
        float b = radius * a0 * k;
        float c = radius * a0;
        float v = 0.5f * (b + Mathf.Sqrt(b * b + 4f * c));
        // Above the base cap the authority is saturated (03 §5): v² = r · a0 · mult.
        if (v > BaseCap) v = Mathf.Sqrt(radius * a0 * _m.HighSpeedSteeringMultiplier);
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
    /// <summary>Flights shorter than this are the launch test firing a vertex early on a surface the
    /// ball has not actually left; they are dropped and the ball keeps rolling.</summary>
    private const float MinFlightSeconds = 0.05f;

    /// <param name="chainFromBaseCap">Ceiling safety case (04 §12): once the base kit could have reached
    /// the base cap, assume a full chain and hold the ceiling wherever the bends allow; bends still
    /// clamp to their corner limits and the ceiling returns right after them.</param>
    public RouteSpeedProfile Integrate(IReadOnlyList<Vector3> polyline, float entrySpeed = 0f, bool driveHeld = true, bool chainFromBaseCap = false)
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

        // Launch curvature per vertex (D-094): second difference of height over ±3 vertices, as the
        // controller's ground follow reads it; negative = convex. Zero near the ends.
        var launchDemand = new float[n];   // −κ · (1 + s²)^(3/2)... folded: κ signed, with cos θ for the gravity side
        var cosSlope = new float[n];
        for (int i = 0; i < n; i++)
        {
            cosSlope[i] = 1f;
            if (i < CurvatureSpan || i + CurvatureSpan >= n) continue;
            float span = 0.5f * (profile.Distance[i + CurvatureSpan] - profile.Distance[i - CurvatureSpan]);
            if (span <= 1e-3f) continue;
            float yF = polyline[i + CurvatureSpan].Y, y0 = polyline[i].Y, yB = polyline[i - CurvatureSpan].Y;
            float second = (yF - 2f * y0 + yB) / (span * span);
            float slope = (yF - yB) / (2f * span);
            float slope2 = 1f + slope * slope;
            launchDemand[i] = second / (slope2 * Mathf.Sqrt(slope2));
            cosSlope[i] = 1f / Mathf.Sqrt(slope2);
        }

        float cap = Cap;
        float g = _m.Gravity;
        float drive = driveHeld ? _m.GroundDriveAcceleration : 0f;
        float airDrive = drive * _m.AirControlMultiplier;
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

        // Flight state: horizontal speed v; the vertical is closed-form from the launch (vy0, t0, y0), so
        // every vertex crossing, even several inside one tick, reads the exact ballistic height.
        Flight? flight = null;
        float vy0 = 0f, launchY = 0f, launchT = 0f, vy = 0f;
        bool chained = false, landed = false;

        for (int tick = 0; tick < MaxTicks && seg < n - 1; tick++)
        {
            float vNew, advance;
            if (flight is null)
            {
                float cosGrade = Mathf.Sqrt(Mathf.Max(0f, 1f - sinGrade[seg] * sinGrade[seg]));
                float loss = v > 0f ? slip * cosGrade : 0f;
                vNew = v + (g * sinGrade[seg] + drive - loss) * TickSeconds;
                vNew = Mathf.Max(0f, vNew) * dragKeep;
                if (chainFromBaseCap && IsCeiling && (chained || vNew >= BaseCap)) { chained = true; vNew = cap; }
                vNew = Mathf.Min(vNew, cap);
                advance = vNew * TickSeconds;
            }
            else
            {
                vNew = Mathf.Min((v + airDrive * TickSeconds) * dragKeep, cap);
                // Horizontal advance projected onto the segment the ball is flying over.
                float cosGrade = Mathf.Sqrt(Mathf.Max(0.05f, 1f - sinGrade[seg] * sinGrade[seg]));
                advance = vNew * TickSeconds / cosGrade;
            }
            t += TickSeconds;

            if (flight is null && advance <= 0f && vNew < StallSpeed)
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
                float arrivalTime = t - TickSeconds * (1f - fraction);
                if (flight is null)
                {
                    arrival = Mathf.Min(arrival, profile.CornerLimit[vi]);
                    vNew = Mathf.Min(vNew, profile.CornerLimit[vi]);
                    // Launch test at the vertex: the surface curves away faster than gravity follows.
                    if (launchDemand[vi] < 0f && arrival * arrival * -launchDemand[vi] >= g * cosSlope[vi])
                    {
                        float sinUp = -sinGrade[seg];                // the segment just travelled sets the launch angle
                        float cosUp = Mathf.Sqrt(Mathf.Max(0f, 1f - sinUp * sinUp));
                        flight = new Flight { LaunchDistance = profile.Distance[vi], LaunchSpeed = arrival };
                        vy0 = arrival * sinUp;
                        vNew = arrival * cosUp;
                        arrival = vNew;
                        launchY = polyline[vi].Y;
                        launchT = arrivalTime;
                    }
                }
                else
                {
                    float tau = Mathf.Max(0f, arrivalTime - launchT);
                    vy = vy0 - g * tau;
                    if (launchY + vy0 * tau - 0.5f * g * tau * tau <= polyline[vi].Y)
                    {
                        landed = true;
                        // Touchdown: keep the component along the landing segment, absorb the rest.
                        int landSeg = Mathf.Min(vi, n - 2);
                        float cosGrade = Mathf.Sqrt(Mathf.Max(0f, 1f - sinGrade[landSeg] * sinGrade[landSeg]));
                        float tangent = arrival * cosGrade - vy * sinGrade[landSeg];
                        flight.LandingDistance = profile.Distance[vi];
                        flight.Seconds = tau;
                        flight.LandingVerticalSpeed = Mathf.Max(0f, -vy);
                        flight.LandingSpeed = Mathf.Clamp(tangent, 0f, cap);
                    }
                }
                if (landed && flight is not null)
                {
                    landed = false;
                    if (flight.Seconds >= MinFlightSeconds)
                    {
                        profile.Flights.Add(flight);
                        profile.AirborneSeconds += flight.Seconds;
                    }
                    else flight.LandingSpeed = Mathf.Min(flight.LaunchSpeed, cap);   // never left the surface: nothing absorbed
                    arrival = Mathf.Min(flight.LandingSpeed, profile.CornerLimit[vi]);
                    vNew = arrival;
                    flight = null;
                }
                profile.Speed[vi] = arrival;
                profile.Time[vi] = arrivalTime;
                advance -= remaining;
                seg++;
                segPos = 0f;
                if (seg < n - 1) remaining = segLen[seg];
            }
            segPos += advance;
            v = vNew;
            profile.MaxSpeed = Mathf.Max(profile.MaxSpeed, v);
        }
        if (flight is not null)
        {
            // Still airborne at the exit: land it there so the report can see it.
            float tau = Mathf.Max(0f, t - launchT);
            flight.LandingDistance = profile.Distance[n - 1];
            flight.Seconds = tau;
            flight.LandingVerticalSpeed = Mathf.Max(0f, -(vy0 - g * tau));
            flight.LandingSpeed = v;
            profile.Flights.Add(flight);
            profile.AirborneSeconds += flight.Seconds;
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

    /// <summary>
    /// Flight off a cosine crest of wavelength λ and height H on flat ground, entered at a speed and
    /// integrated with this model (D-094): route distance from the apex to the touchdown, 0 when the
    /// crest is rolled. The skeleton builder sizes feature straights with the ceiling model's answer.
    /// </summary>
    /// <param name="descentAfter">Grade (tan) the ground falls away at past the crest: the swell under a
    /// crest can descend, and a flight over falling ground is far longer than over flat.</param>
    public float CrestFlightLength(float wavelength, float height, float entrySpeed, float descentAfter = 0f)
    {
        const float lead = 200f, tail = 3000f;
        var poly = new List<Vector3>();
        for (float d = 0f; d <= lead + wavelength + tail; d += WorldScale.RouteSampleSpacing)
        {
            float h = d >= lead && d <= lead + wavelength ? 0.5f * height * (1f - Mathf.Cos(Mathf.Tau * (d - lead) / wavelength)) : 0f;
            if (d > lead + wavelength) h -= descentAfter * (d - lead - wavelength);
            poly.Add(new Vector3(d, h, 0f));
        }
        var profile = Integrate(poly, entrySpeed);
        float apex = lead + wavelength * 0.5f, best = 0f;
        foreach (var f in profile.Flights)
            if (f.LaunchDistance <= apex + wavelength * 0.5f && f.LandingDistance > apex) best = Mathf.Max(best, f.LandingDistance - apex);
        return best;
    }

    /// <summary>
    /// Flight length of a jump from a lip (D-097): the release sets the vertical to
    /// max(<paramref name="lipVertical"/>, <paramref name="verticalTakeoff"/>) (03 §6: v.Y = max(current, takeoff)),
    /// the horizontal runs under air drive and drag as in the airborne phase, and the flight ends once the
    /// ball is <paramref name="drop"/> below the lip and falling. Route distance over flat ground past the lip.
    /// </summary>
    /// <param name="descentAfter">Grade (tan) the ground may fall away at past the landing zone: the swell under a
    /// module straight descends at most this much, and a flight over falling ground is longer.</param>
    public float JumpRange(float horizontalSpeed, float verticalTakeoff, float drop = 0f, float lipVertical = 0f, float descentAfter = 0f)
    {
        float g = _m.Gravity;
        float airDrive = _m.GroundDriveAcceleration * _m.AirControlMultiplier;
        float dragKeep = Mathf.Max(0f, 1f - _m.DragCoefficient * TickSeconds);
        float v = Mathf.Clamp(horizontalSpeed, 0f, Cap), vy = Mathf.Max(lipVertical, verticalTakeoff);
        if (drop < 0f && vy * vy / (2f * g) < -drop) return 0f;   // a rise the apex never reaches: unreachable
        float x = 0f, y = 0f;
        for (int i = 0; i < MaxTicks; i++)
        {
            v = Mathf.Min((v + airDrive * TickSeconds) * dragKeep, Cap);
            vy -= g * TickSeconds;
            x += v * TickSeconds;
            y += vy * TickSeconds;
            if (vy < 0f && y <= -drop - descentAfter * x) break;
        }
        return x;
    }

    /// <summary>
    /// Where a launched ball lands if the player slams after <paramref name="reactionSeconds"/> of flight
    /// (03 §8: the vertical becomes min(current, −slam initial) and then falls at the slam acceleration plus
    /// gravity; the horizontal keeps flying). The safe landing a module's launch relies on at the ceiling
    /// (D-097): route distance from the launch, flat ground assumed past it.
    /// </summary>
    public float SlamRange(float horizontalSpeed, float verticalLaunch, float reactionSeconds, float slamInitial, float slamAccel, float drop = 0f)
    {
        float g = _m.Gravity;
        float airDrive = _m.GroundDriveAcceleration * _m.AirControlMultiplier;
        float dragKeep = Mathf.Max(0f, 1f - _m.DragCoefficient * TickSeconds);
        float v = Mathf.Clamp(horizontalSpeed, 0f, Cap), vy = verticalLaunch;
        float x = 0f, y = 0f, t = 0f;
        bool slamming = false;
        for (int i = 0; i < MaxTicks; i++)
        {
            t += TickSeconds;
            v = Mathf.Min((v + airDrive * TickSeconds) * dragKeep, Cap);
            if (!slamming && t >= reactionSeconds) { slamming = true; vy = Mathf.Min(vy, -slamInitial); }
            vy -= (g + (slamming ? slamAccel : 0f)) * TickSeconds;
            x += v * TickSeconds;
            y += vy * TickSeconds;
            if (vy < 0f && y <= -drop) break;
        }
        return x;
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
