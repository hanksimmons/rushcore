using Godot;

namespace Rushcore.UI;

/// <summary>
/// The speedometer (06 §12, P-015): a small racecar dial in the corner of the HUD. A 240° sweep from rest to the
/// Flow ceiling, ticks every 50 m/s, a needle, and the number in the middle. The stretch above the base cap is
/// drawn as a redline, so the dial says what Flow headroom is (D-088): everything past the red is speed that was
/// earned and can be lost.
///
/// <para>Drawn rather than composed, because a needle is a rotation and a rotation is one line of drawing code.
/// Redraws only when the shown speed changes by half a metre per second, and the numbers come from a small
/// prebuilt table, so a dial at the cap allocates nothing.</para>
/// </summary>
public partial class SpeedDial : Control
{
    private const float SweepDegrees = 240f;
    private const float StartDegrees = 150f;          // 0 at the lower left, sweeping clockwise through the top
    private const float TickStep = 50f;               // m/s between labelled ticks

    private static readonly Color Face = new(0.04f, 0.05f, 0.07f, 0.55f);
    private static readonly Color Ink = new(0.86f, 0.91f, 0.97f);
    private static readonly Color Arc = new(0.55f, 0.62f, 0.72f);
    private static readonly Color Redline = new(1.00f, 0.35f, 0.30f);
    private static readonly Color Needle = new(1.00f, 0.78f, 0.30f);

    /// <summary>Speeds as text, built once: the needle moves every frame and `int.ToString()` would allocate.</summary>
    private static readonly string[] Numbers = BuildNumbers();

    private static string[] BuildNumbers()
    {
        var n = new string[400];
        for (int i = 0; i < n.Length; i++) n[i] = i.ToString();
        return n;
    }

    private float _speed, _baseCap = 1f, _max = 1f, _shown = -1f;
    private Font? _font;
    private int _fontSize = 14;

    /// <summary>The speed the dial is drawing; the harness reads it rather than the needle's pixels.</summary>
    public float Shown => _shown;
    /// <summary>Where the redline starts: the base cap, the speed Flow headroom lets you exceed.</summary>
    public float RedlineFrom => _baseCap;
    public float FullScale => _max;

    public void SetFont(Font? font, int size)
    {
        _font = font;
        _fontSize = size;
        QueueRedraw();
    }

    /// <summary>Called each frame by the HUD; redraws only when the needle would visibly move.</summary>
    public void Show(float speed, float baseCap, float ceiling)
    {
        bool scaleMoved = !Mathf.IsEqualApprox(baseCap, _baseCap) || !Mathf.IsEqualApprox(ceiling, _max);
        _baseCap = Mathf.Max(1f, baseCap);
        _max = Mathf.Max(_baseCap, ceiling);
        _speed = Mathf.Max(0f, speed);
        if (!scaleMoved && Mathf.Abs(_speed - _shown) < 0.5f) return;
        _shown = _speed;
        QueueRedraw();
    }

    private float AngleOf(float speed) =>
        Mathf.DegToRad(StartDegrees + SweepDegrees * Mathf.Clamp(speed / _max, 0f, 1f));

    public override void _Draw()
    {
        float r = Mathf.Min(Size.X, Size.Y) * 0.5f;
        if (r <= 1f) return;
        Vector2 c = Size * 0.5f;
        float w = Mathf.Max(2f, r * 0.11f);

        DrawCircle(c, r, Face);
        // The track, then the redline over its top end: past the base cap is Flow headroom (D-088).
        DrawArc(c, r - w, AngleOf(0f), AngleOf(_max), 48, Arc, w * 0.5f);
        DrawArc(c, r - w, AngleOf(_baseCap), AngleOf(_max), 24, Redline, w * 0.5f);

        for (float s = 0f; s <= _max + 0.1f; s += TickStep)
        {
            float a = AngleOf(s);
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            DrawLine(c + dir * (r - w * 1.6f), c + dir * (r - w * 0.4f), s >= _baseCap ? Redline : Arc, Mathf.Max(1f, w * 0.22f));
        }

        // The needle, and the number it points at.
        float na = AngleOf(_shown);
        var nd = new Vector2(Mathf.Cos(na), Mathf.Sin(na));
        DrawLine(c - nd * r * 0.12f, c + nd * (r - w * 1.8f), Needle, Mathf.Max(1.5f, w * 0.3f));
        DrawCircle(c, Mathf.Max(2f, w * 0.42f), Needle);

        if (_font is null) return;
        int shown = Mathf.Clamp(Mathf.RoundToInt(_shown), 0, Numbers.Length - 1);
        string text = Numbers[shown];
        Vector2 size = _font.GetStringSize(text, HorizontalAlignment.Center, -1f, _fontSize);
        DrawString(_font, c + new Vector2(-size.X * 0.5f, size.Y * 0.30f), text, HorizontalAlignment.Left, -1f, _fontSize,
            _shown >= _baseCap ? Redline : Ink);
    }
}
