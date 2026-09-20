using Godot;
using Rushcore.Generation;

namespace Rushcore.World;

/// <summary>
/// The wall shell's geometry (D-111): a <see cref="WallShellStrip"/> (the stamp sampled along a line's lateral rays at the
/// shell's stations) as one indexed mesh with smooth normals and terrain colours, and the same triangles for an
/// outward-facing concave collider (backface collision is on, so winding does not matter to physics). Faces with a
/// collapsed or invalid corner are skipped: past a vertex's lip the stations fold onto its last point, and on the inside
/// of a bend the rays stop short of the centre.
/// </summary>
public static class WallShellMesh
{
    public readonly record struct Built(ArrayMesh Mesh, Vector3[] CollisionTriangles);

    public static Built Build(WallShellStrip strip)
    {
        int n = strip.Vertices, k = strip.Stations;
        var idx = new System.Collections.Generic.List<int>((n - 1) * (k - 1) * 6);
        var tris = new System.Collections.Generic.List<Vector3>((n - 1) * (k - 1) * 6);
        for (int i = 0; i + 1 < n; i++)
        {
            for (int j = 0; j + 1 < k; j++)
            {
                int a = i * k + j, b = i * k + j + 1, c = (i + 1) * k + j, d = (i + 1) * k + j + 1;
                if (!strip.Valid[a] || !strip.Valid[b] || !strip.Valid[c] || !strip.Valid[d]) continue;
                AddFace(idx, tris, strip, a, c, b);
                AddFace(idx, tris, strip, b, c, d);
            }
        }
        var mesh = new ArrayMesh();
        if (idx.Count > 0)
        {
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = strip.Points;
            arrays[(int)Mesh.ArrayType.Normal] = strip.Normals;
            arrays[(int)Mesh.ArrayType.Color] = strip.Colors;
            arrays[(int)Mesh.ArrayType.Index] = idx.ToArray();
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        }
        return new Built(mesh, tris.ToArray());
    }

    private static void AddFace(System.Collections.Generic.List<int> idx, System.Collections.Generic.List<Vector3> tris, WallShellStrip s, int a, int b, int c)
    {
        Vector3 faceN = (s.Points[b] - s.Points[a]).Cross(s.Points[c] - s.Points[a]);
        if (faceN.LengthSquared() < 1e-4f) return;                    // a collapsed station: no face
        // Godot front faces are clockwise: order each triangle so its face normal points along the surface normal.
        Vector3 outward = s.Normals[a] + s.Normals[b] + s.Normals[c];
        if (faceN.Dot(outward) < 0f) (b, c) = (c, b);
        idx.Add(a); idx.Add(b); idx.Add(c);
        tris.Add(s.Points[a]); tris.Add(s.Points[b]); tris.Add(s.Points[c]);
    }
}
