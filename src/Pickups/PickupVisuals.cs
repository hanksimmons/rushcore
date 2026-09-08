using Godot;
using Rushcore.Vfx;

namespace Rushcore.Pickups;

/// <summary>The pickup and affordance shapes 06 §9 asks for a consistent language across.</summary>
public enum PickupKind
{
    /// <summary>Boost: the ring the player drives through. Unchanged from the toy's existing pickup.</summary>
    Boost,
    /// <summary>Currency: a flat hexagonal coin. What the reward burst throws.</summary>
    Currency,
    /// <summary>XP / reward: a spinning tetrahedron; nothing else in the world is one.</summary>
    Reward,
    /// <summary>Item: a cube inside a thin frame.</summary>
    Item,
    /// <summary>Stage exit: a pillar pair at the pad, standing with the EXIT sign.</summary>
    Exit,
    /// <summary>High-risk shortcut: a twin chevron post, and the only reward-adjacent thing wearing hazard ink.</summary>
    Shortcut,
}

/// <summary>
/// Placeholder pickup shapes (06 §9). One shape and one material each, so the language is learned once and
/// holds across every archetype palette. Rewards are warm or cool and never red: hazard ink appears only on
/// the shortcut post, which is the one marker that is meant to say "this will cost you" (08 §9 "reward lines
/// do not read as hazards").
///
/// <para>No colliders and no collection rule live here. The boost ring's trigger stays where it always was, in
/// <c>WorldDressing</c>; the burst's collection is <see cref="RewardBurst"/>'s; everything else is a shape.</para>
/// </summary>
public partial class PickupVisual : Node3D
{
    private float _clock;
    private readonly float _phase;

    public PickupKind Kind { get; }

    private PickupVisual(PickupKind kind, float phase)
    {
        Kind = kind;
        _phase = phase;
        Name = kind.ToString();
    }

    /// <summary>
    /// Builds one pickup standing on its own origin. <paramref name="phase"/> staggers the bob so a line of
    /// them never pulses in lockstep.
    /// </summary>
    public static PickupVisual Create(PickupKind kind, float phase = 0f)
    {
        var p = new PickupVisual(kind, phase);
        switch (kind)
        {
            case PickupKind.Boost:
                // The gate is driven through, so the torus axis lies along the route: local Y is the axis, and
                // the caller aims the node. 3.1 m outer radius, matching the rings already on the lines.
                var ring = Piece(PlaceholderPalette.Halo, PlaceholderPalette.Boost, Vector3.Up * 3.6f, new Vector3(6.2f, 1.4f, 6.2f));
                ring.RotateX(Mathf.DegToRad(90f));
                p.AddChild(ring);
                p.AddChild(Piece(PlaceholderPalette.Sphere, PlaceholderPalette.Boost, Vector3.Up * 3.6f, Vector3.One * 1.8f));
                break;

            case PickupKind.Currency:
                p.AddChild(Piece(PlaceholderPalette.Coin, PlaceholderPalette.Currency, Vector3.Up * 1.2f, new Vector3(1.2f, 0.18f, 1.2f)));
                break;

            case PickupKind.Reward:
                p.AddChild(Piece(PlaceholderPalette.Tetrahedron, PlaceholderPalette.Reward, Vector3.Up * 1.4f, Vector3.One * 1.6f));
                break;

            case PickupKind.Item:
                p.AddChild(Piece(PlaceholderPalette.Box, PlaceholderPalette.PylonBody, Vector3.Up * 1.4f, Vector3.One * 1.0f));
                foreach (var edge in FrameEdges(0.8f))
                    p.AddChild(Piece(PlaceholderPalette.Box, PlaceholderPalette.Chassis, Vector3.Up * 1.4f + edge.Position, edge.Scale));
                break;

            case PickupKind.Exit:
                // A pillar pair the pad is entered between: tall enough to be the thing seen from a kilometre
                // out, which is what 06 §2 puts an exit above cosmetic detail for.
                p.AddChild(Piece(PlaceholderPalette.Cylinder, PlaceholderPalette.ExitMarker, new Vector3(-9f, 7f, 0f), new Vector3(1.4f, 14f, 1.4f)));
                p.AddChild(Piece(PlaceholderPalette.Cylinder, PlaceholderPalette.ExitMarker, new Vector3(9f, 7f, 0f), new Vector3(1.4f, 14f, 1.4f)));
                p.AddChild(Piece(PlaceholderPalette.Box, PlaceholderPalette.ExitMarker, new Vector3(0f, 13.4f, 0f), new Vector3(19.4f, 1.2f, 1.2f)));
                p.SetProcess(false);
                break;

            default:
                // Twin chevrons pointing the way in, on a post: hazard ink, deliberately, on the one line the
                // player is being warned about rather than invited onto.
                p.AddChild(Piece(PlaceholderPalette.Cylinder, PlaceholderPalette.Chassis, Vector3.Up * 2.5f, new Vector3(0.5f, 5f, 0.5f)));
                for (int i = 0; i < 2; i++)
                {
                    float y = 3.2f + i * 1.4f;
                    var left = Piece(PlaceholderPalette.Box, PlaceholderPalette.Hazard, new Vector3(-0.9f, y, 0f), new Vector3(2.0f, 0.4f, 0.18f));
                    left.RotateZ(Mathf.DegToRad(-32f));
                    var right = Piece(PlaceholderPalette.Box, PlaceholderPalette.Hazard, new Vector3(0.9f, y, 0f), new Vector3(2.0f, 0.4f, 0.18f));
                    right.RotateZ(Mathf.DegToRad(32f));
                    p.AddChild(left);
                    p.AddChild(right);
                }
                p.SetProcess(false);
                break;
        }
        return p;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _clock += dt;
        Position = Position with { Y = _restY + Mathf.Sin(_clock * 1.8f + _phase) * 0.35f };
        if (Kind is PickupKind.Reward or PickupKind.Currency or PickupKind.Item) Rotate(Vector3.Up, dt * 1.6f);
    }

    private float _restY;

    /// <summary>The bob is around wherever the caller stood it, captured once it is in the tree.</summary>
    public override void _Ready() => _restY = Position.Y;

    /// <summary>Twelve edges of a cube of the given half extent, as thin bars.</summary>
    private static IEnumerable<(Vector3 Position, Vector3 Scale)> FrameEdges(float h)
    {
        const float t = 0.09f;
        for (int a = -1; a <= 1; a += 2)
            for (int b = -1; b <= 1; b += 2)
            {
                yield return (new Vector3(0f, a * h, b * h), new Vector3(h * 2f, t, t));
                yield return (new Vector3(a * h, 0f, b * h), new Vector3(t, h * 2f, t));
                yield return (new Vector3(a * h, b * h, 0f), new Vector3(t, t, h * 2f));
            }
    }

    private static MeshInstance3D Piece(Mesh mesh, Material material, Vector3 position, Vector3 scale) => new()
    {
        Mesh = mesh,
        MaterialOverride = material,
        Position = position,
        Scale = scale,
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
    };
}
