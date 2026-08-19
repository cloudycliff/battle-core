namespace BattleCore.Randomness;

public sealed class DeterministicRng
{
    private ulong _state;

    public DeterministicRng(ulong seed)
    {
        _state = Mix(seed, 1);
        if (_state == 0)
        {
            _state = 0x9E3779B97F4A7C15;
        }
    }

    public int NextInt(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        return (int)(NextUInt64() % (ulong)count);
    }

    public static DeterministicRng Derive(ulong battleSeed, long entityId, ulong stream)
    {
        return new DeterministicRng(Mix(Mix(battleSeed, (ulong)entityId), stream));
    }

    private ulong NextUInt64()
    {
        unchecked
        {
            var x = _state;
            x ^= x >> 12;
            x ^= x << 25;
            x ^= x >> 27;
            _state = x;
            return x * 0x2545F4914F6CDD1DUL;
        }
    }

    private static ulong Mix(ulong value, ulong salt)
    {
        unchecked
        {
            value += salt * 0x9E3779B97F4A7C15;
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EB;
            return value ^ (value >> 31);
        }
    }
}
