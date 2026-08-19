namespace BattleCore.Math;

/// <summary>An angle quantized into 1024 steps per turn.</summary>
public readonly record struct Facing
{
    public const int StepsPerTurn = 1024;

    public Facing(int step) => Step = Normalize(step);

    public int Step { get; }

    private static int Normalize(int step)
    {
        var normalized = step % StepsPerTurn;
        return normalized < 0 ? normalized + StepsPerTurn : normalized;
    }
}
