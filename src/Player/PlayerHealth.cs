namespace Rushcore.Player;

/// <summary>
/// The player's health as a value (Phase 4's "health", T2). A plain class with no engine dependency and no
/// gameplay authority: nothing damages it yet, and it never touches movement. The composition root owns one,
/// the HUD reads it, and the debug actions kill and heal it.
///
/// <para>Reaching zero raises <see cref="Died"/>; the composition root recovers the player and refills on arrival
/// (P-004) — a placeholder for run death, which
/// arrives with the run summary in Phase 6/7. There is no invulnerability window: that belongs with the first
/// damage source (08 §6).</para>
/// </summary>
public sealed class PlayerHealth
{
    public const float Max = 100f;

    public float Current { get; private set; } = Max;
    public float Fraction => Current / Max;
    public bool IsDead => Current <= 0f;

    /// <summary>Raised whenever the value changes, with the new value.</summary>
    public event Action<float>? Changed;
    /// <summary>Raised when the value reaches zero, before the refill. The bootstrap recovers the player on it.</summary>
    public event Action? Died;

    public void Damage(float amount)
    {
        if (amount <= 0f || IsDead) return;
        Set(Current - amount);
        if (Current > 0f) return;
        // The value stays at zero until the recovery lands, so "dead" is observable and the refill is
        // something that happens on arriving at the checkpoint rather than in the same instant.
        Died?.Invoke();
    }

    public void Heal(float amount)
    {
        if (amount <= 0f) return;
        Set(System.Math.Min(Max, Current + amount));
    }

    public void Kill() => Damage(Current);

    /// <summary>Back to full: the placeholder for what a run death will eventually decide (P-004).</summary>
    public void Refill() => Set(Max);

    private void Set(float value)
    {
        float clamped = System.Math.Clamp(value, 0f, Max);
        if (clamped == Current) return;
        Current = clamped;
        Changed?.Invoke(Current);
    }
}
