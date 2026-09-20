using Godot;
using Rushcore.Generation;

namespace Rushcore.World;

/// <summary>
/// The far horizon (docs/13 §4.1, D-114): one flat-shaded mesh from the footprint's edge to <see cref="Radius"/> whose
/// heights are the stage's own ground (the stamp itself at the edge, so there is no seam; the relief and side terrain
/// beyond) plus a distance envelope that rises from <see cref="EnvelopeStart"/> to <see cref="EnvelopeEnd"/> into the
/// archetype's far shapes (mesas and buttes on a canyon, ridges on the highlands, a distant range beyond a dune sea,
/// peaks on the sky), so the stage sits in a bowl of foothills that become mountains. A polar grid: rays from the
/// footprint rectangle outward, rings at geometrically growing spacing (64 m at the edge), so the inner ring lies exactly
/// on the boundary and nothing cracks. No collider; never reachable. Colour is the stage's own palette; the fog does the
/// atmospheric perspective.
/// </summary>
public static class HorizonRing
{
    public const float Radius = 20000f;
    public const int Rays = 256, Rings = 48;
    /// <summary>Spacing of the first ring beyond the edge; the rest grow geometrically to reach <see cref="Radius"/>.</summary>
    public const float FirstStep = 64f;
    public const float EnvelopeStart = 2000f, EnvelopeEnd = 12000f;
    /// <summary>The far shapes' height over the side terrain at full envelope, per archetype.</summary>
    public static float EnvelopeHeight(TerrainArchetype a) => a switch
    {
        TerrainArchetype.CanyonRun => 700f,
        TerrainArchetype.DuneSea => 800f,
        TerrainArchetype.SkyTerraces => 900f,
        _ => 600f,
    };

    public readonly record struct Built(ArrayMesh Mesh, int Triangles, Vector3[] Edge, Vector3[] Vertices);

    /// <summary>The far shapes' rise at a point beyond the edge (r = distance past the footprint boundary).</summary>
    public static float Envelope(StageHeightField field, TerrainArchetype archetype, float x, float z, float r)
    {
        float start = archetype == TerrainArchetype.DuneSea ? 6000f : EnvelopeStart;   // a dune sea stays flat before its range
        float t = Mathf.SmoothStep(start, EnvelopeEnd, r);
        if (t <= 0f) return 0f;
        float n1 = field.HorizonNoise(x, z, 3000f), n2 = field.HorizonNoise(x + 7000f, z + 3000f, 1200f);
        float shape;
        switch (archetype)
        {
            case TerrainArchetype.CanyonRun:
            {
                // Mesas and buttes: the noise quantised into three flat tiers with steep sides (the wall profile's
                // silhouette repeated at scale), a little rubble on top.
                float u = Mathf.Clamp(0.5f + 0.5f * n1, 0f, 0.999f) * 3f;
                float tier = Mathf.Floor(u), frac = u - tier;
                float step = tier + Mathf.SmoothStep(0.88f, 1f, frac);
                shape = 0.35f + 0.65f * step / 3f + 0.05f * n2;
                break;
            }
            case TerrainArchetype.DuneSea:
                shape = 0.45f + 0.55f * (1f - Mathf.Abs(n1)) + 0.08f * n2;         // a ridged range beyond the sea
                break;
            case TerrainArchetype.SkyTerraces:
            {
                float ridge = 1f - Mathf.Abs(n1);
                shape = 0.3f + 0.9f * ridge * ridge + 0.1f * n2;                     // peaks
                break;
            }
            default:
                shape = 0.5f + 0.35f * n1 + 0.15f * n2;                              // rolling hills to ridges
                break;
        }
        return EnvelopeHeight(archetype) * t * shape;
    }

    public static Built Build(StageHeightField field, TerrainArchetype archetype)
    {
        float hx = field.SizeX * 0.5f, hz = field.SizeZ * 0.5f;
        // Ring spacing: FirstStep growing by g so the last ring lands on Radius.
        float g = 1.07f;
        for (int it = 0; it < 40; it++)
        {
            float sum = FirstStep * (Mathf.Pow(g, Rings) - 1f) / (g - 1f);
            g *= Mathf.Pow(Radius / sum, 1f / Rings);
        }
        var radii = new float[Rings + 1];
        float acc = 0f, step = FirstStep;
        for (int k = 1; k <= Rings; k++) { acc += step; step *= g; radii[k] = acc; }
        radii[Rings] = Radius;

        // Points: ray i at angle θ from the footprint's boundary point outward.
        var pts = new Vector3[Rays * (Rings + 1)];
        var edge = new Vector3[Rays];
        for (int i = 0; i < Rays; i++)
        {
            float th = Mathf.Tau * i / Rays;
            float cx = Mathf.Cos(th), cz = Mathf.Sin(th);
            float scale = Mathf.Min(hx / Mathf.Max(1e-4f, Mathf.Abs(cx)), hz / Mathf.Max(1e-4f, Mathf.Abs(cz)));
            float bx = cx * scale, bz = cz * scale;
            for (int k = 0; k <= Rings; k++)
            {
                float x = bx + cx * radii[k], z = bz + cz * radii[k];
                float h = field.Sample(x, z) + Envelope(field, archetype, x, z, radii[k]);
                pts[i * (Rings + 1) + k] = new Vector3(x, h, z);
            }
            edge[i] = pts[i * (Rings + 1)];
        }

        // Flat-shaded, non-indexed, like the terrain tiles (06 §4).
        int faces = Rays * Rings * 2;
        var verts = new Vector3[faces * 3]; var norms = new Vector3[faces * 3]; var cols = new Color[faces * 3];
        int w = 0;
        for (int i = 0; i < Rays; i++)
        {
            int i2 = (i + 1) % Rays;
            for (int k = 0; k < Rings; k++)
            {
                Vector3 a = pts[i * (Rings + 1) + k], b = pts[i2 * (Rings + 1) + k], c = pts[i * (Rings + 1) + k + 1], d = pts[i2 * (Rings + 1) + k + 1];
                w = Emit(field, verts, norms, cols, w, a, c, b);
                w = Emit(field, verts, norms, cols, w, b, c, d);
            }
        }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts;
        arrays[(int)Mesh.ArrayType.Normal] = norms;
        arrays[(int)Mesh.ArrayType.Color] = cols;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return new Built(mesh, faces, edge, pts);
    }

    private static int Emit(StageHeightField field, Vector3[] verts, Vector3[] norms, Color[] cols, int w, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 n = (b - a).Cross(c - a);
        n = n.LengthSquared() > 1e-12f ? n.Normalized() : Vector3.Up;
        // Godot front faces are clockwise: order the triangle so it faces the sky.
        if (n.Y < 0f) { n = -n; (b, c) = (c, b); }
        Color col = field.SampleColor((a + b + c) / 3f, n);
        verts[w] = a; norms[w] = n; cols[w] = col; w++;
        verts[w] = b; norms[w] = n; cols[w] = col; w++;
        verts[w] = c; norms[w] = n; cols[w] = col; w++;
        return w;
    }
}
