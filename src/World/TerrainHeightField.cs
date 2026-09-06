using Godot;
using Rushcore.Tuning;

namespace Rushcore.World;

/// <summary>
/// Deterministic height + colour source for the Movement Toy calibration terrain.
/// This is a movement <b>laboratory</b>, NOT the production stage generator (09 Phase 1):
/// every feature exists so a human can measure one movement property against known geometry.
///
/// <para>Coordinates: the terrain spans -512..+512 in X and Z with 4 m facets. With the
/// default 45 deg camera yaw, holding <c>W+A</c> drives exactly -X and <c>W+D</c> drives
/// exactly -Z, so the axis-aligned lanes below are drivable in a straight line.</para>
///
/// <para>Layout (all metres, heights at Terrain Amplitude = 1):</para>
/// <list type="bullet">
/// <item>Plain — dead flat y=0 for z &gt; 200, x &gt; -170. Spawn (470, 380).</item>
/// <item>Calibration lane — z=380, x 450 -&gt; -50 (500 m), flat, marked every 50 m.</item>
/// <item>Grade fan (west) — one continuous slope face whose angle varies across x:
///       8 deg at x=-450, 15 deg at x=-300, 25 deg at x=-150. Every lane starts at y=0
///       at z=150 and bottoms out at exactly y=-45, so the three lanes differ only in slope.</item>
/// <item>Uphill 12 deg climb + mesa — x 0..225, from z=130 up to y=+25.5.</item>
/// <item>Launch ramps — three off the mesa's north lip at x=40 / 112 / 184
///       (11.3 deg, 19.3 deg, 26.6 deg), dropping to a flat landing plain at y=0.</item>
/// <item>Chasms — south rim z=-220 (34 m opening, 12 m deep) and z=-380 (58 m, 20 m deep).
///       Steep south rim, 25 deg north wall so a missed jump can always be driven out of.</item>
/// <item>Bowl — centre (340, -320), radius 160, floor y=-48.</item>
/// <item>Banked hairpin — centre (330, 155), radius 145, track half-width 46, outer berm +18.</item>
/// <item>Rolling hills — everything else, three octaves with a slow amplitude modulation.</item>
/// </list>
/// </summary>
public sealed class TerrainHeightField
{
    // ------------------------------------------------------------------ layout

    /// <summary>Flat calibration plain: y = 0 for z above <see cref="PlainEdgeZ"/> and x east of <see cref="PlainEdgeX"/>.</summary>
    public const float PlainBlendZ = 110f;
    public const float PlainEdgeZ = 200f;
    public const float PlainBlendX = -300f;
    public const float PlainEdgeX = -170f;

    /// <summary>Straight measured lane on the plain. Drive it by holding W+A.</summary>
    public const float LaneZ = 380f;
    public const float LaneStartX = 450f;
    public const float LaneLength = 500f;
    public const float LaneEndX = LaneStartX - LaneLength;
    public const float LaneHalfWidth = 24f;

    /// <summary>Slope-calibration fan: constant-drop lanes of known, different gradients.</summary>
    public const float FanCrestZ = 150f;
    public const float FanDrop = 45f;
    public const float FanEase = 26f;
    public const float Grade8X = -450f;
    public const float Grade15X = -300f;
    public const float Grade25X = -150f;
    public const float FanEastFull = -110f;
    public const float FanEastFade = 10f;
    private const float Tan8 = 0.140541f;
    private const float Tan15 = 0.267949f;
    private const float Tan25 = 0.466308f;

    /// <summary>Uphill propulsion test: 12 deg climb from the plain onto a mesa.</summary>
    public const float ClimbStartZ = 130f;
    public const float ClimbSlope = 0.212557f;   // tan 12 deg
    public const float MesaHeight = 25.5f;
    private const float ClimbEase = 26f;
    private static readonly float ClimbRun = MesaHeight / ClimbSlope + ClimbEase * 0.5f;

    /// <summary>North lip of the mesa: where all three launch ramps start.</summary>
    public const float RampStartZ = -40f;
    private const float RampEase = 10f;
    public const float Ramp1X = 40f, Ramp2X = 112f, Ramp3X = 184f;
    public const float RampHalfWidth = 30f;
    private const float RampFeather = 26f;
    // slope, rise -> nominal run = rise/slope + ease/2
    private const float Ramp1Slope = 0.20f, Ramp1Rise = 12f;   // 11.3 deg
    private const float Ramp2Slope = 0.35f, Ramp2Rise = 14f;   // 19.3 deg
    private const float Ramp3Slope = 0.50f, Ramp3Rise = 15f;   // 26.6 deg

    /// <summary>Centre X of launch ramp 0..2 (shallow -&gt; steep).</summary>
    public static float RampCentreX(int i) => i == 0 ? Ramp1X : i == 1 ? Ramp2X : Ramp3X;

    /// <summary>Z of the take-off lip of launch ramp 0..2.</summary>
    public static float RampLipZ(int i)
    {
        float slope = i == 0 ? Ramp1Slope : i == 1 ? Ramp2Slope : Ramp3Slope;
        float rise = i == 0 ? Ramp1Rise : i == 1 ? Ramp2Rise : Ramp3Rise;
        return RampStartZ - (rise / slope + RampEase * 0.5f);
    }

    /// <summary>Gaps cut across the landing plain. South rim is the take-off edge.</summary>
    public const float ChasmARimZ = -220f, ChasmAOpening = 34f, ChasmADepth = 12f;
    public const float ChasmBRimZ = -380f, ChasmBOpening = 58f, ChasmBDepth = 20f;

    /// <summary>Momentum bowl.</summary>
    public const float BowlX = 340f, BowlZ = -320f, BowlRadius = 160f, BowlDepth = 48f;

    /// <summary>Banked hairpin. Track floor sits at plain level so both ends tie in seamlessly.</summary>
    public const float HairpinX = 330f, HairpinZ = 155f;
    public const float HairpinRadius = 145f, HairpinHalfWidth = 46f, HairpinBank = 18f;

    // ------------------------------------------------------------------ state

    private readonly WorldTuning _t;
    private readonly float _p0, _p1, _p2, _p3, _p4, _p5, _p6, _p7;

    public TerrainHeightField(int seed, WorldTuning tuning)
    {
        _t = tuning;

        // The engineered instruments are seed-invariant on purpose: a calibration rig
        // must not change shape between runs. The seed only varies the scenery hills.
        var rng = new RandomNumberGenerator { Seed = (ulong)(uint)seed };
        _p0 = rng.Randf() * Mathf.Tau;
        _p1 = rng.Randf() * Mathf.Tau;
        _p2 = rng.Randf() * Mathf.Tau;
        _p3 = rng.Randf() * Mathf.Tau;
        _p4 = rng.Randf() * Mathf.Tau;
        _p5 = rng.Randf() * Mathf.Tau;
        _p6 = rng.Randf() * Mathf.Tau;
        _p7 = rng.Randf() * Mathf.Tau;
    }

    /// <summary>Where the player starts. Y is resolved from the terrain by the world.</summary>
    public Vector3 SpawnXZ { get; } = new(LaneStartX + 20f, 0f, LaneZ);

    /// <summary>The lane runs toward -X.</summary>
    public Vector3 SpawnFacing { get; } = Vector3.Left;

    // ------------------------------------------------------------------ height

    /// <summary>
    /// Pure, allocation-free closed-form height. Engineered features are blended by
    /// normalised weight so overlaps average smoothly instead of stepping.
    /// </summary>
    public float Sample(float x, float z)
    {
        float wsum = 0f, hsum = 0f;

        // 1. flat calibration plain
        Blend(ref wsum, ref hsum,
            Mathf.SmoothStep(PlainBlendZ, PlainEdgeZ, z) * Mathf.SmoothStep(PlainBlendX, PlainEdgeX, x),
            0f);

        // 2. grade fan: one slope face, gradient varying across x
        float fanW = (1f - Mathf.SmoothStep(FanEastFull, FanEastFade, x))
                   * (1f - Mathf.SmoothStep(FanCrestZ, FanCrestZ + 100f, z));
        if (fanW > 0f)
        {
            float s = FanSlope(x);
            Blend(ref wsum, ref hsum, fanW, -s * SmoothClamp(FanCrestZ - z, FanDrop / s + FanEase * 0.5f, FanEase));
        }

        // 3. central column: 12 deg climb -> mesa -> launch ramp -> landing plain with chasms
        Blend(ref wsum, ref hsum, LaneWeight(x, Ramp1X), CentralProfile(z, Ramp1Slope, Ramp1Rise));
        Blend(ref wsum, ref hsum, LaneWeight(x, Ramp2X), CentralProfile(z, Ramp2Slope, Ramp2Rise));
        Blend(ref wsum, ref hsum, LaneWeight(x, Ramp3X), CentralProfile(z, Ramp3Slope, Ramp3Rise));

        // 4. momentum bowl (paraboloid with a small raised rim)
        float bdx = x - BowlX, bdz = z - BowlZ;
        float br = Mathf.Sqrt(bdx * bdx + bdz * bdz);
        float bowlW = 1f - Mathf.SmoothStep(BowlRadius * 1.06f, BowlRadius * 1.45f, br);
        if (bowlW > 0f)
        {
            float u = Mathf.Min(br / BowlRadius, 1.06f);
            Blend(ref wsum, ref hsum, bowlW, BowlDepth * (u * u - 1f));
        }

        // 5. banked hairpin
        float hdx = x - HairpinX, hdz = z - HairpinZ;
        float hr = Mathf.Sqrt(hdx * hdx + hdz * hdz);
        float hd = hr - HairpinRadius;
        float hairW = (1f - Mathf.SmoothStep(HairpinHalfWidth, HairpinHalfWidth + 55f, Mathf.Abs(hd)))
                    * (1f - Mathf.SmoothStep(HairpinZ - 35f, HairpinZ + 45f, z));
        if (hairW > 0f)
        {
            float u = Mathf.Clamp((hd + HairpinHalfWidth * 0.35f) / (HairpinHalfWidth * 1.35f), 0f, 1f);
            Blend(ref wsum, ref hsum, hairW, HairpinBank * u * u);
        }

        float blend = Mathf.Min(wsum, 1f);
        float feature = wsum > 1e-4f ? hsum / wsum : 0f;
        // Amplitude scales the scenery hills only. The engineered instruments (lane, grade
        // fan, ramps, chasms, bowl, hairpin) are calibration rulers and keep their stated
        // geometry at any amplitude.
        float hills = blend >= 0.999f ? 0f : Hills(x, z) * _t.TerrainAmplitude;
        return Mathf.Lerp(hills, feature, blend) + RimWall(x, z);
    }

    /// <summary>World boundary: a rim rising toward the heightfield edge so the lab reads
    /// as bounded instead of ending in sky. Steeper than the ground-normal limit, so the
    /// ball climbs a little and rolls back rather than falling to the kill plane.</summary>
    public const float RimStart = 482f;
    public const float RimHeight = 55f;
    private static float RimWall(float x, float z)
    {
        float edge = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
        float t = Mathf.SmoothStep(RimStart, 512f, edge);
        return RimHeight * t * t;
    }

    /// <summary>Gradient of the fan at a given x. Flat-topped at each marked lane.</summary>
    public static float FanSlope(float x)
    {
        if (x <= Grade8X) return Tan8;
        if (x <= Grade15X) return Mathf.Lerp(Tan8, Tan15, Mathf.SmoothStep(Grade8X, Grade15X, x));
        if (x <= Grade25X) return Mathf.Lerp(Tan15, Tan25, Mathf.SmoothStep(Grade15X, Grade25X, x));
        return Tan25;
    }

    /// <summary>Longitudinal profile shared by the three ramp columns; only the ramp differs.</summary>
    private static float CentralProfile(float z, float rampSlope, float rampRise)
    {
        float rampRun = rampRise / rampSlope + RampEase * 0.5f;
        float lipZ = RampStartZ - rampRun;
        float lipY = MesaHeight + rampRise;

        float h = ClimbSlope * SmoothClamp(ClimbStartZ - z, ClimbRun, ClimbEase);
        h += rampSlope * SmoothClamp(RampStartZ - z, rampRun, RampEase);
        h -= SmoothClamp(lipZ - z, lipY + 9f, 18f);          // 45 deg back face down to y = 0
        h -= ChasmADepth * ChasmFactor(z, ChasmARimZ, ChasmAOpening, ChasmADepth);
        h -= ChasmBDepth * ChasmFactor(z, ChasmBRimZ, ChasmBOpening, ChasmBDepth);
        return h;
    }

    /// <summary>0 outside the gap, 1 on its floor. Steep south rim, ~25 deg north wall.</summary>
    private static float ChasmFactor(float z, float southRim, float opening, float depth)
    {
        float northLip = southRim - opening;
        float rampRun = depth / Tan25;
        float a = (southRim - z) / 8f;
        float b = (z - northLip) / rampRun;
        return Smooth01(Mathf.Clamp(Mathf.Min(a, b), 0f, 1f));
    }

    private static float LaneWeight(float x, float centre)
        => 1f - Mathf.SmoothStep(RampHalfWidth, RampHalfWidth + RampFeather, Mathf.Abs(x - centre));

    private float Hills(float x, float z)
    {
        float s = 1f / Mathf.Max(0.05f, _t.TerrainWavelength);
        float h = 9.0f * Mathf.Sin(x * 0.0331f * s + _p0) * Mathf.Cos(z * 0.0299f * s + _p1)
                + 4.2f * Mathf.Sin(x * 0.0714f * s + _p2) * Mathf.Sin(z * 0.0827f * s + _p3)
                + 1.7f * Mathf.Sin(x * 0.1530f * s + _p4) * Mathf.Cos(z * 0.1337f * s + _p5);
        float mod = 0.55f + 0.45f * Mathf.Sin(x * 0.0041f + _p6) * Mathf.Cos(z * 0.0036f + _p7);
        return h * mod;
    }

    private static void Blend(ref float wsum, ref float hsum, float w, float h)
    {
        if (w <= 0f) return;
        wsum += w;
        hsum += w * h;
    }

    private static float Smooth01(float t) => t * t * (3f - 2f * t);

    /// <summary>C1 smooth max(v, 0). Exact outside a +/- e/2 window around zero.</summary>
    private static float SoftMax0(float v, float e)
    {
        float t = v + e * 0.5f;
        if (t <= 0f) return 0f;
        return t < e ? t * t / (2f * e) : t - e * 0.5f;
    }

    /// <summary>
    /// C1 clamp of <paramref name="v"/> into [0, hi] with slope exactly 1 in the middle:
    /// the measured gradient stays exact while crest and runout are rounded over
    /// <paramref name="e"/> metres so a rolling ball is never kicked by a corner.
    /// </summary>
    private static float SmoothClamp(float v, float hi, float e)
    {
        e = Mathf.Min(e, hi * 0.9f);
        if (e <= 1e-4f) return Mathf.Clamp(v, 0f, hi);
        return hi - SoftMax0(hi - SoftMax0(v, e), e);
    }

    // ------------------------------------------------------------------ colour

    // Constrained N64-ish palette: height ramp, slope overlay, two painted surfaces.
    private static readonly Color Basin = new(0.24f, 0.31f, 0.39f);
    private static readonly Color Low = new(0.24f, 0.40f, 0.28f);
    private static readonly Color Mid = new(0.39f, 0.55f, 0.30f);
    private static readonly Color High = new(0.72f, 0.70f, 0.49f);
    private static readonly Color Rock = new(0.52f, 0.42f, 0.33f);
    private static readonly Color Cliff = new(0.33f, 0.30f, 0.31f);
    private static readonly Color LaneSurface = new(0.19f, 0.22f, 0.27f);
    private static readonly Color LaneSurfaceAlt = new(0.27f, 0.31f, 0.37f);
    private static readonly Color TrackSurface = new(0.47f, 0.29f, 0.25f);

    public Color SampleColor(Vector3 point, Vector3 normal)
    {
        float y = point.Y;
        float slope = 1f - Mathf.Clamp(normal.Y, 0f, 1f);

        Color c = Basin;
        c = c.Lerp(Low, Mathf.SmoothStep(-42f, -14f, y));
        c = c.Lerp(Mid, Mathf.SmoothStep(-10f, 6f, y));
        c = c.Lerp(High, Mathf.SmoothStep(14f, 34f, y));

        // Slope tint doubles as the gradient read-out across the calibration fan.
        c = c.Lerp(Rock, Mathf.SmoothStep(0.01f, 0.30f, slope));
        c = c.Lerp(Cliff, Mathf.SmoothStep(0.40f, 0.75f, slope));

        // Painted calibration lane.
        float lane = (1f - Mathf.SmoothStep(LaneHalfWidth - 6f, LaneHalfWidth + 6f, Mathf.Abs(point.Z - LaneZ)))
                   * (1f - Mathf.SmoothStep(LaneStartX + 10f, LaneStartX + 30f, point.X))
                   * Mathf.SmoothStep(LaneEndX - 30f, LaneEndX - 10f, point.X);
        // 10 m bands: a uniform dark strip gives no ground-motion cue at 60 m/s (06 §2).
        float band = Mathf.PosMod(Mathf.Floor(point.X / 10f), 2f);
        c = c.Lerp(band < 1f ? LaneSurface : LaneSurfaceAlt, lane * 0.9f);

        // Painted hairpin track.
        float hdx = point.X - HairpinX, hdz = point.Z - HairpinZ;
        float hd = Mathf.Abs(Mathf.Sqrt(hdx * hdx + hdz * hdz) - HairpinRadius);
        float track = (1f - Mathf.SmoothStep(HairpinHalfWidth - 10f, HairpinHalfWidth + 4f, hd))
                    * (1f - Mathf.SmoothStep(HairpinZ - 30f, HairpinZ + 30f, point.Z));
        c = c.Lerp(TrackSurface, track * 0.6f);

        // Cheap per-facet value jitter so the flat-shaded triangles read individually.
        float n = Mathf.Sin(point.X * 12.9898f + point.Z * 78.233f) * 43758.5453f;
        n -= Mathf.Floor(n);
        float k = 0.94f + 0.12f * n;
        return new Color(c.R * k, c.G * k, c.B * k);
    }
}
