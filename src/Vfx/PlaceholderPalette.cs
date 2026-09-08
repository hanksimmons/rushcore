using Godot;

namespace Rushcore.Vfx;

/// <summary>
/// Every shared material and mesh the placeholder enemies, the pickups and the world VFX draw with
/// (T3; 06 §8 enemy language, §9 pickup shapes, §17 retro restraint).
///
/// <para>One resource per role, created once for the process. An instance never owns a material, so a
/// showcase row of a dozen bodies costs a dozen draws off twelve materials rather than a dozen new
/// resources (05 §15: no pooling beyond what a burst routinely needs). The palette is deliberately
/// small: role is carried by silhouette first and colour second (06 §3 "do not rely only on hue"),
/// and hazard tones appear only on the Bulwark's band and the shortcut marker so a reward can never
/// be mistaken for a threat (06 §9, 08 §9).</para>
/// </summary>
public static class PlaceholderPalette
{
    // ---------------- materials (twelve, and the showcase row draws with exactly these) ----------------

    /// <summary>Heavy structural metal: the Bulwark's block, the Strider's legs, the Shooter's barrel, the item frame.</summary>
    public static readonly StandardMaterial3D Chassis = Flat(new Color(0.21f, 0.23f, 0.27f));
    /// <summary>Pale cool plastic: the Pylon's body and the item cube. Reads as inert, i.e. safe to ram.</summary>
    public static readonly StandardMaterial3D PylonBody = Flat(new Color(0.74f, 0.80f, 0.84f));
    /// <summary>The Pylon's lit cap: the one bright thing on a low-resistance silhouette.</summary>
    public static readonly StandardMaterial3D PylonCap = Glow(new Color(0.42f, 0.92f, 1.00f), 1.3f);
    /// <summary>Hazard: the Bulwark's chevron band and the high-risk shortcut post, and nothing else (06 §9).</summary>
    public static readonly StandardMaterial3D Hazard = Glow(new Color(0.92f, 0.28f, 0.14f), 0.5f);
    /// <summary>The Strider's long body: a colour of its own so the crossing shape separates from the ground.</summary>
    public static readonly StandardMaterial3D StriderBody = Flat(new Color(0.38f, 0.47f, 0.40f));
    /// <summary>The Shooter's core sphere.</summary>
    public static readonly StandardMaterial3D ShooterCore = Flat(new Color(0.36f, 0.28f, 0.52f));
    /// <summary>Where the Shooter is aiming: the orientation ring and the projectile.</summary>
    public static readonly StandardMaterial3D ShooterAim = Glow(new Color(1.00f, 0.74f, 0.24f), 1.2f);
    /// <summary>The elite treatment, and only it: the halo ring and the crown (02 §7 "one constrained modifier").</summary>
    public static readonly StandardMaterial3D Elite = Glow(new Color(1.00f, 0.86f, 0.42f), 1.6f);
    /// <summary>Currency: warm, flat, never red.</summary>
    public static readonly StandardMaterial3D Currency = Glow(new Color(1.00f, 0.80f, 0.30f), 0.7f);
    /// <summary>XP / reward: cool, so it never sits in the hazard family.</summary>
    public static readonly StandardMaterial3D Reward = Glow(new Color(0.36f, 0.86f, 0.98f), 0.9f);
    /// <summary>The exit marker's pillars and lintel.</summary>
    public static readonly StandardMaterial3D ExitMarker = Glow(new Color(0.62f, 1.00f, 0.72f), 0.9f);
    /// <summary>Boost keeps the existing ring's tone (06 §9: the shape language does not move).</summary>
    public static readonly StandardMaterial3D Boost = Glow(new Color(0.30f, 0.90f, 0.72f), 1.1f);

    // ---------------- meshes (unit-sized; every visual scales one of these) ----------------

    public static readonly BoxMesh Box = new() { Size = Vector3.One };
    /// <summary>Unit cylinder: legs, posts, the barrel's collar.</summary>
    public static readonly CylinderMesh Cylinder = new() { TopRadius = 0.5f, BottomRadius = 0.5f, Height = 1f, RadialSegments = 8, Rings = 1 };
    /// <summary>A flat hexagonal coin: currency, at any size.</summary>
    public static readonly CylinderMesh Coin = new() { TopRadius = 0.5f, BottomRadius = 0.5f, Height = 1f, RadialSegments = 6, Rings = 1 };
    /// <summary>A cone, nose along +Y: the Shooter's barrel.</summary>
    public static readonly CylinderMesh Cone = new() { TopRadius = 0.02f, BottomRadius = 0.5f, Height = 1f, RadialSegments = 8, Rings = 1 };
    /// <summary>A triangular prism standing on its base: the Pylon.</summary>
    public static readonly PrismMesh Prism = new() { Size = Vector3.One };
    public static readonly SphereMesh Sphere = new() { Radius = 0.5f, Height = 1f, RadialSegments = 10, Rings = 6 };
    /// <summary>A fat ring: the elite halo and the Shooter's orientation ring.</summary>
    public static readonly TorusMesh Halo = new() { InnerRadius = 0.40f, OuterRadius = 0.5f, Rings = 16, RingSegments = 6 };
    /// <summary>A thin ring: the VFX one-shots expand this.</summary>
    public static readonly TorusMesh ThinRing = new() { InnerRadius = 0.45f, OuterRadius = 0.5f, Rings = 24, RingSegments = 6 };
    /// <summary>A regular tetrahedron in a unit box: the XP/reward shape, which nothing else in the world is.</summary>
    public static readonly ArrayMesh Tetrahedron = BuildTetrahedron();

    static PlaceholderPalette()
    {
        // The tetrahedron's triangles are wound off the cube's corners rather than to a winding rule,
        // so both faces are drawn; nothing ever sees the inside of a spinning pickup anyway.
        Reward.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
    }

    // ---------------- helpers ----------------

    /// <summary>Flat lambert, no specular: the same rule the terrain and props are drawn under (06 §4).</summary>
    public static StandardMaterial3D Flat(Color albedo) => new()
    {
        AlbedoColor = albedo,
        DiffuseMode = BaseMaterial3D.DiffuseModeEnum.Lambert,
        SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled,
        Roughness = 1f,
        Metallic = 0f,
    };

    public static StandardMaterial3D Glow(Color albedo, float energy)
    {
        var m = Flat(albedo);
        m.EmissionEnabled = true;
        m.Emission = albedo;
        m.EmissionEnergyMultiplier = energy;
        return m;
    }

    /// <summary>
    /// Four vertices of a cube's alternating corners are a regular tetrahedron. Faces carry their own
    /// outward normal so the flat-shaded read is clean; culling is left on the material rather than
    /// argued about here, since nothing ever sees the inside of a spinning pickup.
    /// </summary>
    private static ArrayMesh BuildTetrahedron()
    {
        const float s = 0.5f;
        Vector3 a = new(s, s, s), b = new(s, -s, -s), c = new(-s, s, -s), d = new(-s, -s, s);
        (Vector3, Vector3, Vector3)[] faces = { (a, b, c), (a, c, d), (a, d, b), (b, d, c) };

        var verts = new List<Vector3>(12);
        var norms = new List<Vector3>(12);
        foreach (var (p, q, r) in faces)
        {
            Vector3 n = (q - p).Cross(r - p).Normalized();
            if (n.Dot((p + q + r) / 3f) < 0f) n = -n;      // outward from the centroid at the origin
            verts.Add(p); verts.Add(q); verts.Add(r);
            norms.Add(n); norms.Add(n); norms.Add(n);
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = norms.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }
}
