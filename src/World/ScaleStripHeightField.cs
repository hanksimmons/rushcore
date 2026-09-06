using Godot;
using Rushcore.Tuning;

namespace Rushcore.World;

/// <summary>
/// Gate M1 scale-calibration strip (08 §4, 09 Phase 1B). A 6.4 km straight run of
/// stations, each a feature at a stated size, so world scale is measured at the accepted
/// cap instead of estimated. Seed-invariant and amplitude-invariant: it is a ruler.
///
/// <para>Runs toward -X from the spawn at x = +3100. Station distance s = StartX - x.</para>
/// <list type="bullet">
/// <item>s 0–1000 runway: flat, 100 m posts, 500 m gantries; 0→cap, braking, burst surge.</item>
/// <item>s 1000–2000 corridor widths: 300 / 150 / 75 / 40 m, 250 m each, 30 m walls.</item>
/// <item>s 2000–4200 hills: wavelength/height 100/10, 200/20, 400/40, 800/80 m.</item>
/// <item>s 4200–5080 gaps: 40 / 80 / 160 m openings, 150 m runways, 25° exit walls.</item>
/// <item>s 5080–5700 ramps: three lanes (z −180 / 0 / +180) 11 / 19 / 27°, 20 m lips, landing.</item>
/// <item>s 5700–6300 turn pad: flat, painted rings r 80 / 160 / 240 m.</item>
/// </list>
/// </summary>
public sealed class ScaleStripHeightField : IHeightSource
{
    public const float StartX = 3100f;
    public const float HalfLength = 3200f;
    public const float HalfWidth = 320f;
    public const float LaneHalfWidth = 30f;

    // Stations (s = StartX - x).
    public const float RunwayEnd = 1000f;
    public const float CorridorEnd = 2000f;
    public const float HillsEnd = 4200f;
    public const float GapsEnd = 5080f;
    public const float RampsStart = 5200f;
    public const float RampsEnd = 5700f;
    public const float PadEnd = 6300f;

    public static readonly float[] CorridorWidths = { 300f, 150f, 75f, 40f };
    public const float CorridorWallHeight = 30f;

    public static readonly float[] HillWavelengths = { 100f, 200f, 400f, 800f };
    public static readonly float[] HillHeights = { 10f, 20f, 40f, 80f };
    public static readonly float[] HillStationLengths = { 200f, 400f, 800f, 800f };

    /// <summary>Take-off rim s, opening, depth. Runways of 150 m between.</summary>
    public static readonly float[] GapRimS = { 4350f, 4540f, 4770f };
    public static readonly float[] GapOpenings = { 40f, 80f, 160f };
    public static readonly float[] GapDepths = { 12f, 20f, 30f };

    public static readonly float[] RampLaneZ = { -180f, 0f, 180f };
    public static readonly float[] RampSlopes = { 0.20f, 0.35f, 0.50f };   // 11.3 / 19.3 / 26.6 deg
    public const float RampRise = 20f;
    public const float RampHalfWidth = 40f;
    private const float RampFeather = 30f;
    private const float RampEase = 12f;

    public const float PadCentreS = 6000f;
    public static readonly float[] PadRingRadii = { 80f, 160f, 240f };

    public const float RimStart = 296f;
    public const float RimHeight = 55f;
    private const float Tan25 = 0.466308f;

    public ScaleStripHeightField(WorldTuning _) { }

    public float SizeX => HalfLength * 2f;
    public float SizeZ => HalfWidth * 2f;
    public Vector3 SpawnXZ { get; } = new(StartX + 10f, 0f, 0f);
    public Vector3 SpawnFacing { get; } = Vector3.Left;

    public static float S(float x) => StartX - x;
    public static float X(float s) => StartX - s;

    /// <summary>Take-off lip s of ramp lane 0..2.</summary>
    public static float RampLipS(int i) => RampsStart + RampRise / RampSlopes[i] + RampEase * 0.5f;

    // ------------------------------------------------------------------ height

    public float Sample(float x, float z)
    {
        float s = S(x);
        float h = 0f;

        // Corridor walls: piecewise half-width, blended over 30 m at each boundary.
        if (s > RunwayEnd - 40f && s < CorridorEnd + 40f)
        {
            float hw = CorridorHalfWidth(s);
            float az = Mathf.Abs(z);
            float wall = Mathf.SmoothStep(hw, hw + 16f, az);
            // Fade the walls in and out along the strip so the runway and hills tie in flat.
            float along = Mathf.SmoothStep(RunwayEnd - 40f, RunwayEnd, s) * (1f - Mathf.SmoothStep(CorridorEnd, CorridorEnd + 40f, s));
            h += CorridorWallHeight * wall * along;
        }

        // Hills: each station starts and ends at zero height with zero slope (C1).
        if (s >= CorridorEnd && s < HillsEnd)
        {
            float s0 = CorridorEnd;
            for (int i = 0; i < HillWavelengths.Length; i++)
            {
                float s1 = s0 + HillStationLengths[i];
                if (s < s1)
                {
                    h += 0.5f * HillHeights[i] * (1f - Mathf.Cos(Mathf.Tau * (s - s0) / HillWavelengths[i]));
                    break;
                }
                s0 = s1;
            }
        }

        // Gaps: steep take-off rim, floor, 25 deg exit wall (drivable out, like the lab).
        for (int i = 0; i < GapRimS.Length; i++)
            h -= GapDepths[i] * ChasmFactor(s, GapRimS[i], GapOpenings[i], GapDepths[i]);

        // Ramps: three lanes; between lanes the ground stays flat.
        if (s > RampsStart - 20f && s < RampsEnd)
        {
            for (int i = 0; i < RampLaneZ.Length; i++)
            {
                float w = 1f - Mathf.SmoothStep(RampHalfWidth, RampHalfWidth + RampFeather, Mathf.Abs(z - RampLaneZ[i]));
                if (w <= 0f) continue;
                float run = RampRise / RampSlopes[i] + RampEase * 0.5f;
                float lipS = RampsStart + run;
                float p = RampSlopes[i] * SmoothClamp(s - RampsStart, run, RampEase);
                p -= SmoothClamp(s - lipS, RampRise + 9f, 18f);      // 45 deg back face to y = 0
                h += w * p;
            }
        }

        return h + Rim(x, z);
    }

    public static float CorridorHalfWidth(float s)
    {
        float seg = (CorridorEnd - RunwayEnd) / CorridorWidths.Length;
        float u = (s - RunwayEnd) / seg;
        int i = Mathf.Clamp((int)Mathf.Floor(u), 0, CorridorWidths.Length - 1);
        float hw = CorridorWidths[i] * 0.5f;
        if (i + 1 < CorridorWidths.Length)
        {
            float t = Mathf.SmoothStep(i + 1f - 30f / seg, i + 1f, u);
            hw = Mathf.Lerp(hw, CorridorWidths[i + 1] * 0.5f, t);
        }
        return hw;
    }

    private static float Rim(float x, float z)
    {
        float tz = Mathf.SmoothStep(RimStart, HalfWidth, Mathf.Abs(z));
        float tx = Mathf.SmoothStep(HalfLength - 24f, HalfLength, Mathf.Abs(x));
        float t = Mathf.Max(tz, tx);
        return RimHeight * t * t;
    }

    /// <summary>0 outside the gap, 1 on its floor. Steep take-off rim, ~25 deg exit wall.</summary>
    private static float ChasmFactor(float s, float rimS, float opening, float depth)
    {
        float exitS = rimS + opening;
        float exitRun = depth / Tan25;
        float a = (s - rimS) / 8f;
        float b = (exitS - s) / exitRun;
        return Smooth01(Mathf.Clamp(Mathf.Min(a, b), 0f, 1f));
    }

    private static float Smooth01(float t) => t * t * (3f - 2f * t);

    private static float SoftMax0(float v, float e)
    {
        float t = v + e * 0.5f;
        if (t <= 0f) return 0f;
        return t < e ? t * t / (2f * e) : t - e * 0.5f;
    }

    private static float SmoothClamp(float v, float hi, float e)
    {
        e = Mathf.Min(e, hi * 0.9f);
        if (e <= 1e-4f) return Mathf.Clamp(v, 0f, hi);
        return hi - SoftMax0(hi - SoftMax0(v, e), e);
    }

    // ------------------------------------------------------------------ colour

    private static readonly Color StripeColor = new(0.12f, 0.12f, 0.14f);
    private static readonly Color RingColor = new(0.85f, 0.55f, 0.18f);

    /// <summary>Station start distances, for the painted stripes and the signs.</summary>
    public static readonly float[] StationS = { 0f, RunwayEnd, 1250f, 1500f, 1750f, CorridorEnd, 2200f, 2600f, 3400f, HillsEnd, GapsEnd, RampsEnd };

    public Color SampleColor(Vector3 point, Vector3 normal)
    {
        float slope = 1f - Mathf.Clamp(normal.Y, 0f, 1f);
        Color c = TerrainHeightField.BasePalette(point.Y, slope);
        float s = S(point.X);

        // Centre lane, 10 m bands, everywhere except the hills (their crests are the read).
        bool laneSection = s < CorridorEnd || (s >= HillsEnd && s < RampsEnd);
        if (laneSection)
        {
            float lane = 1f - Mathf.SmoothStep(LaneHalfWidth - 4f, LaneHalfWidth + 4f, Mathf.Abs(point.Z));
            float band = Mathf.PosMod(Mathf.Floor(s / 10f), 2f);
            c = c.Lerp(band < 1f ? TerrainHeightField.LaneSurface : TerrainHeightField.LaneSurfaceAlt, lane * 0.9f);
        }

        // 100 m cross stripes on the runway/pad, station boundaries everywhere.
        float mod100 = Mathf.PosMod(s, 100f);
        if ((s < RunwayEnd || s >= RampsEnd) && mod100 < 3f) c = c.Lerp(StripeColor, 0.7f);
        foreach (float st in StationS)
            if (Mathf.Abs(s - st) < 3f) c = c.Lerp(StripeColor, 0.9f);

        // Turn-pad rings.
        if (s >= RampsEnd)
        {
            float r = new Vector2(point.X - X(PadCentreS), point.Z).Length();
            foreach (float ring in PadRingRadii)
                if (Mathf.Abs(r - ring) < 2.5f) c = c.Lerp(RingColor, 0.85f);
        }

        return TerrainHeightField.FacetJitter(c, point);
    }
}
