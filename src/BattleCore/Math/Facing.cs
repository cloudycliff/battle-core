namespace BattleCore.Math;

/// <summary>An angle quantized into 1024 steps per turn. Step 0 faces +X, increasing counterclockwise.</summary>
public readonly record struct Facing
{
    public const int StepsPerTurn = 1024;

    public Facing(int step) => Step = Normalize(step);

    public int Step { get; }

    public static Facing FromDirection(FixVector2 direction)
    {
        var x = direction.X.RawValue;
        var y = direction.Y.RawValue;
        if (x == 0 && y == 0)
        {
            return new Facing(0);
        }

        var ax = x < 0 ? -x : x;
        var ay = y < 0 ? -y : y;
        var offset = ax + ay == 0 ? 0 : (int)(ay * 256L / (ax + ay));

        var step = x >= 0 && y >= 0
            ? offset
            : x < 0 && y >= 0
                ? 512 - offset
                : x < 0 && y < 0
                    ? 512 + offset
                    : 1024 - offset;
        return new Facing(step);
    }

    private static int Normalize(int step)
    {
        var normalized = step % StepsPerTurn;
        return normalized < 0 ? normalized + StepsPerTurn : normalized;
    }
}
