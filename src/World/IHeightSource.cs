using Godot;

namespace Rushcore.World;

/// <summary>
/// One logical height (and colour) source for both render and collision (D-061).
/// The world samples it on a regular grid; everything else is derived.
/// </summary>
public interface IHeightSource
{
    /// <summary>World-space extents in metres, centred on the origin.</summary>
    float SizeX { get; }
    float SizeZ { get; }
    Vector3 SpawnXZ { get; }
    Vector3 SpawnFacing { get; }
    float Sample(float x, float z);
    Color SampleColor(Vector3 point, Vector3 normal);
}
