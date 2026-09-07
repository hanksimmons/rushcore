using Godot;
using Rushcore.Generation;

namespace Rushcore.World;

/// <summary>
/// The tube's geometry (04 §9, D-096/D-101): a ring of <see cref="Sides"/> vertices swept along the axis with a
/// parallel-transported frame, the radius flared at the mouths, as (1) a see-through shell mesh with outward
/// normals, (2) opaque rib bands every rib spacing, and (3) the same triangles for an inward-facing concave
/// collider (backface collision is on, so winding does not matter to physics).
/// </summary>
public static class TubeMesh
{
    public const int Sides = 10;
    private const float RibWidth = 0.6f, RibLift = 0.25f;

    public readonly record struct Built(ArrayMesh Shell, ArrayMesh Ribs, Vector3[] CollisionTriangles);

    /// <summary>The ring frame at every axis sample: the parallel-transported "up" (ring vertex 0) and its binormal.
    /// Public so the harness can read the facet phase of a ball inside the tube (T7).</summary>
    public static (Vector3 n, Vector3 b)[] Frames(TubeDefinition tube)
    {
        var axis = tube.Axis;
        int n = axis.Length;
        var frames = new (Vector3 n, Vector3 b)[n];
        Vector3 prevN = Vector3.Up;
        for (int i = 0; i < n; i++)
        {
            Vector3 t = tube.TangentAt(i);
            Vector3 nn = prevN - t * prevN.Dot(t);
            if (nn.LengthSquared() < 1e-6f) nn = Mathf.Abs(t.Y) < 0.9f ? Vector3.Up - t * t.Y : Vector3.Right - t * t.X;
            nn = nn.Normalized();
            frames[i] = (nn, t.Cross(nn).Normalized());
            prevN = nn;
        }
        return frames;
    }

    public static Built Build(TubeDefinition tube)
    {
        var axis = tube.Axis;
        int n = axis.Length;
        var frames = Frames(tube);
        float flareLen = tube.Radius * 2f;
        float Radius(float along, float fromEnd)
        {
            float f = Mathf.Max(1f - along / flareLen, 1f - fromEnd / flareLen);
            return tube.Radius * (1f + (WorldScale.TubeMouthFlare - 1f) * Mathf.Clamp(f, 0f, 1f));
        }
        var along = new float[n];
        for (int i = 1; i < n; i++) along[i] = along[i - 1] + axis[i].DistanceTo(axis[i - 1]);
        float total = along[n - 1];

        // Shell: indexed ring strip.
        var verts = new Vector3[n * Sides];
        var norms = new Vector3[n * Sides];
        for (int i = 0; i < n; i++)
        {
            float r = Radius(along[i], total - along[i]);
            for (int s = 0; s < Sides; s++)
            {
                float a = Mathf.Tau * s / Sides;
                Vector3 radial = frames[i].n * Mathf.Cos(a) + frames[i].b * Mathf.Sin(a);
                verts[i * Sides + s] = axis[i] + radial * r;
                norms[i * Sides + s] = radial;
            }
        }
        var idx = new List<int>((n - 1) * Sides * 6);
        var tris = new List<Vector3>((n - 1) * Sides * 6);
        for (int i = 0; i + 1 < n; i++)
        {
            for (int s = 0; s < Sides; s++)
            {
                int s1 = (s + 1) % Sides;
                int a = i * Sides + s, b = i * Sides + s1, c = (i + 1) * Sides + s, d = (i + 1) * Sides + s1;
                // Godot front faces are clockwise: order each quad so its face normal points outward (along the radial).
                AddFace(idx, tris, verts, norms, a, c, b);
                AddFace(idx, tris, verts, norms, b, c, d);
            }
        }
        var shell = new ArrayMesh();
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts;
        arrays[(int)Mesh.ArrayType.Normal] = norms;
        arrays[(int)Mesh.ArrayType.Index] = idx.ToArray();
        shell.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

        // Ribs: a short opaque band just outside the shell every rib spacing (06 §3: the motion cue at speed).
        var rv = new List<Vector3>();
        var rn = new List<Vector3>();
        for (float at = WorldScale.TubeRibSpacing * 0.5f; at < total - WorldScale.TubeRibSpacing * 0.25f; at += WorldScale.TubeRibSpacing)
        {
            int i = IndexAt(along, at);
            float r = Radius(along[i], total - along[i]) + RibLift;
            Vector3 t = tube.TangentAt(i);
            for (int s = 0; s < Sides; s++)
            {
                int s1 = (s + 1) % Sides;
                Vector3 ra = frames[i].n * Mathf.Cos(Mathf.Tau * s / Sides) + frames[i].b * Mathf.Sin(Mathf.Tau * s / Sides);
                Vector3 rb = frames[i].n * Mathf.Cos(Mathf.Tau * s1 / Sides) + frames[i].b * Mathf.Sin(Mathf.Tau * s1 / Sides);
                Vector3 p0 = axis[i] + ra * r - t * RibWidth * 0.5f, p1 = axis[i] + rb * r - t * RibWidth * 0.5f;
                Vector3 p2 = axis[i] + ra * r + t * RibWidth * 0.5f, p3 = axis[i] + rb * r + t * RibWidth * 0.5f;
                Vector3 nrm = (ra + rb).Normalized();
                AddQuad(rv, rn, p0, p2, p1, p3, nrm);
            }
        }
        var ribs = new ArrayMesh();
        if (rv.Count > 0)
        {
            var ra2 = new Godot.Collections.Array();
            ra2.Resize((int)Mesh.ArrayType.Max);
            ra2[(int)Mesh.ArrayType.Vertex] = rv.ToArray();
            ra2[(int)Mesh.ArrayType.Normal] = rn.ToArray();
            ribs.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, ra2);
        }
        return new Built(shell, ribs, tris.ToArray());
    }

    private static void AddFace(List<int> idx, List<Vector3> tris, Vector3[] v, Vector3[] n, int a, int b, int c)
    {
        Vector3 faceN = (v[b] - v[a]).Cross(v[c] - v[a]);
        Vector3 outward = n[a] + n[b] + n[c];
        if (faceN.Dot(outward) < 0f) (b, c) = (c, b);
        idx.Add(a); idx.Add(b); idx.Add(c);
        tris.Add(v[a]); tris.Add(v[b]); tris.Add(v[c]);
    }

    private static void AddQuad(List<Vector3> v, List<Vector3> n, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 nrm)
    {
        void Tri(Vector3 a, Vector3 b, Vector3 c)
        {
            if ((b - a).Cross(c - a).Dot(nrm) < 0f) (b, c) = (c, b);
            v.Add(a); v.Add(b); v.Add(c); n.Add(nrm); n.Add(nrm); n.Add(nrm);
        }
        Tri(p0, p1, p2);
        Tri(p1, p3, p2);
    }

    private static int IndexAt(float[] along, float at)
    {
        int lo = 0, hi = along.Length - 1;
        while (lo < hi) { int mid = (lo + hi) >> 1; if (along[mid] < at) lo = mid + 1; else hi = mid; }
        return lo;
    }
}
