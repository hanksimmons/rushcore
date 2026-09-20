using Godot;

namespace Rushcore.Generation;

/// <summary>
/// The tunnel's cross-section (docs/13 §2.1, D-113), one shape for every tunnel: a slot of <see cref="WorldScale.TunnelHalfWidth"/>
/// half-width whose walls are the wall profile with a 6 m fillet and a 78° face (<see cref="WallProfile"/> with
/// <see cref="Radius"/>), an arch springing from the walls at <see cref="WorldScale.TunnelSpringHeight"/> as the circular
/// arc tangent to the face there (a horseshoe: the surface from the floor over the fillet, the face and the arch is
/// tangent-continuous, so a ball riding up the wall runs onto the arch and drops off where the ceiling rule ends the ride,
/// with no crease to hit), and over it the cap: the ground as it would be without the tunnel. The floor and the walls are
/// the heightfield and its wall shell (the ball rides them under the ground and wall-ride rules); the arch, the cap and the
/// portal faces are one structure, the tunnel roof (<see cref="StageHeightField.RoofStrips"/>). Pure numbers; the height
/// field, the world, the camera and the dressing all read the same shape here.
/// </summary>
public static class TunnelProfile
{
    public const float HalfWidth = WorldScale.TunnelHalfWidth;
    public const float Radius = WorldScale.TunnelFootRadius;
    public static readonly float FaceTan = Mathf.Tan(Mathf.DegToRad(WorldScale.TunnelFaceDegrees));
    public const float Spring = WorldScale.TunnelSpringHeight;
    /// <summary>The face's angle from the horizontal, in radians.</summary>
    private static readonly float FaceAngle = Mathf.DegToRad(WorldScale.TunnelFaceDegrees);

    /// <summary>Lateral distance beyond the corridor's edge at which the wall reaches the spring height.</summary>
    public static readonly float SpringU = WallProfile.LateralAtHeight(Spring, 0f, FaceTan, Radius);
    /// <summary>The spring point's distance from the centreline.</summary>
    public static readonly float SpringLateral = HalfWidth + SpringU;
    /// <summary>The arch's radius: the arc tangent to the face at both spring points has its centre on the centreline along the
    /// face's inward normal, R = SpringLateral / sin(face).</summary>
    public static readonly float ArchRadius = SpringLateral / Mathf.Sin(FaceAngle);
    /// <summary>Height of the arc's centre above the floor.</summary>
    public static readonly float ArchCentre = Spring + ArchRadius * Mathf.Cos(FaceAngle);
    /// <summary>The crown's height above the floor (about 25 m at the shipped numbers).</summary>
    public static readonly float Crown = ArchCentre + ArchRadius;
    /// <summary>Rock over the floor a tunnel needs before it is covered: the crown plus the cap.</summary>
    public static readonly float PortalDepth = Crown + WorldScale.TunnelCapThickness;
    /// <summary>Half the arc's angle, from the crown to a spring point: past the horizontal by the face's lean (a horseshoe).</summary>
    public static readonly float ArchHalfAngle = Mathf.Pi - FaceAngle;

    /// <summary>The arch's underside height above the floor at a lateral distance from the centreline (clamped to the span; the
    /// upper branch of the horseshoe).</summary>
    public static float ArchHeight(float lateral)
    {
        float l = Mathf.Min(Mathf.Abs(lateral), SpringLateral);
        return ArchCentre + Mathf.Sqrt(Mathf.Max(0f, ArchRadius * ArchRadius - l * l));
    }

    /// <summary>The arch's stations as angles from the crown (negative = one side), every <see cref="WorldScale.TunnelArchStepDegrees"/>
    /// with the spring points exact.</summary>
    public static float[] ArchAngles()
    {
        float step = Mathf.DegToRad(WorldScale.TunnelArchStepDegrees);
        int n = Mathf.Max(1, Mathf.CeilToInt(ArchHalfAngle / step));
        var a = new float[2 * n + 1];
        for (int k = -n; k <= n; k++) a[k + n] = Mathf.Clamp(k * step, -ArchHalfAngle, ArchHalfAngle);
        return a;
    }

    /// <summary>Lateral distance from the centreline at which the trench wall reaches a height above the floor.</summary>
    public static float WallLateral(float height) => HalfWidth + WallProfile.LateralAtHeight(height, 0f, FaceTan, Radius);

    /// <summary>How far from the centreline the cap runs over a trench of the given depth: the trench's lip plus the cap margin.</summary>
    public static float CapLateral(float depth) => WallLateral(Mathf.Max(0f, depth) + WorldScale.WallLipEase) + WorldScale.TunnelCapMargin;

    /// <summary>Inside a tunnel the rock darkens toward the middle (06 §3): the share of the lit colour kept at a distance from the nearer portal.</summary>
    public static float Shade(float fromPortal) => Mathf.Lerp(1f, WorldScale.TunnelShadeFloor, Mathf.SmoothStep(0f, WorldScale.TunnelShadeDepth, fromPortal));
}
