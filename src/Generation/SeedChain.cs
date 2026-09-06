namespace Rushcore.Generation;

/// <summary>
/// Explicit seed chain (04 §4, 05 §9): named subsystem seeds derived from a root, and a tiny
/// platform-independent generator so gameplay randomness never touches engine or global RNG.
/// </summary>
public static class SeedChain
{
    private const ulong FnvOffset = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    /// <summary>Derives a child seed from a root, a subsystem name and an index. Pure and stable.</summary>
    public static ulong Derive(ulong root, string name, int index = 0)
    {
        ulong h = FnvOffset ^ root;
        foreach (char c in name) { h ^= c; h *= FnvPrime; }
        h ^= (ulong)(uint)index; h *= FnvPrime;
        return SplitMix64.Mix(h);
    }

    public static ulong Derive(int root, string name, int index = 0) => Derive((ulong)(uint)root, name, index);
}

/// <summary>SplitMix64: small, fast, deterministic across platforms.</summary>
internal static class SplitMix64
{
    public static ulong Mix(ulong z)
    {
        z += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}

/// <summary>Deterministic random stream for one subsystem. Value type: copy it to fork it.</summary>
public struct SeededRandom
{
    private ulong _state;

    public SeededRandom(ulong seed) => _state = seed;

    public ulong NextUInt64()
    {
        _state += 0x9E3779B97F4A7C15UL;
        return SplitMix64.Mix(_state);
    }

    /// <summary>Uniform in [0, 1).</summary>
    public float NextFloat() => (NextUInt64() >> 40) * (1f / 16777216f);
    public float Range(float min, float max) => min + (max - min) * NextFloat();
    /// <summary>Uniform integer in [0, count).</summary>
    public int Next(int count) => count <= 0 ? 0 : (int)(NextUInt64() % (ulong)count);
    public bool Chance(float probability) => NextFloat() < probability;
    public float Sign() => NextFloat() < 0.5f ? -1f : 1f;
}
