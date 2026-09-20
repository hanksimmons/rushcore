using System.Collections.Generic;
using Godot;

namespace Rushcore.Generation;

/// <summary>
/// The authored wall profile (04 §5D, D-109): a walled archetype's wall is not a blend but a shape, the height above
/// the corridor as a function of the lateral distance <c>u</c> beyond the profile's origin. A circular fillet of
/// <see cref="FootRadius"/> starts tangent to the ground it leaves (level, or a berm's slope <c>s0</c> so a bend's
/// bank flows into its wall with no lip) and turns up to the face angle, then a plane at that angle carries on until
/// the stamp rounds it into the side terrain over <see cref="WorldScale.WallLipEase"/> metres of height. The face is
/// 72° on straights and the outsides of bends and eases to 30° on the inside of a bend, where a steep wall would hide
/// the read horizon (04 §10). Along the route the profile is constant, so the collider's facets reproduce the face
/// exactly (a plane through grid samples is that plane) and the fillet's chords sit a sagitta off the arc; the
/// controller reads the same profile through <see cref="StageHeightField.WallSurface"/>, so a wall ride never depends
/// on the facets.
/// </summary>
public static class WallProfile
{
    public const float FootRadius = WorldScale.WallFootRadius;
    public static readonly float FaceTan = Mathf.Tan(Mathf.DegToRad(WorldScale.WallFaceDegrees));
    public static readonly float InsideFaceTan = Mathf.Tan(Mathf.DegToRad(WorldScale.WallInsideFaceDegrees));

    /// <summary>The fillet's end: lateral extent and height, for a foot slope s0 and a face slope faceTan.</summary>
    private static void FilletEnd(float s0, float faceTan, out float uEnd, out float hEnd, out float phi0)
    {
        phi0 = Mathf.Atan(s0);
        float theta = Mathf.Atan(faceTan);
        uEnd = FootRadius * (Mathf.Sin(theta) - Mathf.Sin(phi0));
        hEnd = FootRadius * (Mathf.Cos(phi0) - Mathf.Cos(theta));
    }

    /// <summary>Height above the origin at lateral distance u (0 for u ≤ 0).</summary>
    public static float Height(float u, float s0 = 0f, float? faceTan = null)
    {
        float ft = faceTan ?? FaceTan;
        if (u <= 0f) return 0f;
        FilletEnd(s0, ft, out float uEnd, out float hEnd, out float phi0);
        if (u < uEnd)
        {
            float phi = Mathf.Asin(Mathf.Clamp(u / FootRadius + Mathf.Sin(phi0), -1f, 1f));
            return FootRadius * (Mathf.Cos(phi0) - Mathf.Cos(phi));
        }
        return hEnd + (u - uEnd) * ft;
    }

    /// <summary>dHeight/du.</summary>
    public static float Slope(float u, float s0 = 0f, float? faceTan = null)
    {
        float ft = faceTan ?? FaceTan;
        if (u <= 0f) return s0;
        FilletEnd(s0, ft, out float uEnd, out _, out float phi0);
        if (u < uEnd)
        {
            float phi = Mathf.Asin(Mathf.Clamp(u / FootRadius + Mathf.Sin(phi0), -1f, 1f));
            return Mathf.Tan(Mathf.Min(phi, Mathf.DegToRad(89f)));
        }
        return ft;
    }

    /// <summary>Signed curvature across the profile: the fillet is concave (positive), the face flat.</summary>
    public static float Curvature(float u, float s0 = 0f, float? faceTan = null)
    {
        if (u <= 0f) return 0f;
        FilletEnd(s0, faceTan ?? FaceTan, out float uEnd, out _, out _);
        return u < uEnd ? 1f / FootRadius : 0f;
    }

    /// <summary>Lateral distance at which the profile reaches a height (the inverse of <see cref="Height"/>).</summary>
    public static float LateralAtHeight(float h, float s0 = 0f, float? faceTan = null)
    {
        float ft = faceTan ?? FaceTan;
        if (h <= 0f) return 0f;
        FilletEnd(s0, ft, out float uEnd, out float hEnd, out float phi0);
        if (h < hEnd)
        {
            // h = R(cos φ0 − cos φ) → φ, then u = R(sin φ − sin φ0).
            float cosPhi = Mathf.Clamp(Mathf.Cos(phi0) - h / FootRadius, -1f, 1f);
            float phi = Mathf.Acos(cosPhi);
            return FootRadius * (Mathf.Sin(phi) - Mathf.Sin(phi0));
        }
        return uEnd + (h - hEnd) / ft;
    }

    /// <summary>The shell's chord sagitta on the fillet (D-111): a step of <see cref="WorldScale.WallShellStepDegrees"/> round
    /// the 30 m arc puts each flat facet's middle this far inside the true surface.</summary>
    public static readonly float ShellSagitta = FootRadius * (1f - Mathf.Cos(Mathf.DegToRad(WorldScale.WallShellStepDegrees) * 0.5f));
    /// <summary>Where the analytic wall follow holds the ball above the true surface: twice the shell's sagitta, so the
    /// facets never touch it (the tube follow's margin, T7).</summary>
    public static readonly float ShellRest = ShellSagitta * 2f;

    /// <summary>The shell's canonical lateral stations (D-111): the level foot inside the edge, the fillet by angle, then
    /// the face by lateral step out to <paramref name="uMax"/>.</summary>
    public static float[] ShellStations(float uMax)
    {
        var u = new List<float> { -WorldScale.WallShellFoot, -WorldScale.WallShellFoot * 0.5f, 0f };
        for (float deg = WorldScale.WallShellStepDegrees; deg <= WorldScale.WallFaceDegrees + 1e-3f; deg += WorldScale.WallShellStepDegrees)
            u.Add(FootRadius * Mathf.Sin(Mathf.DegToRad(deg)));
        for (float f = u[^1] + WorldScale.WallShellFaceStep; f < uMax; f += WorldScale.WallShellFaceStep) u.Add(f);
        u.Add(uMax);
        return u.ToArray();
    }
}
