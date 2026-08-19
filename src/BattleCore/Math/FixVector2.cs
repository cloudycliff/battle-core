namespace BattleCore.Math;

public readonly record struct FixVector2(Fix64 X, Fix64 Y)
{
    public static readonly FixVector2 Zero = new(Fix64.Zero, Fix64.Zero);

    public Fix64 LengthSquared => (X * X) + (Y * Y);

    public FixVector2 NormalizeOrZero()
    {
        if (this == Zero)
        {
            return Zero;
        }

        var length = Fix64.FromRaw(IntegerSquareRoot(
            checked((ulong)LengthSquared.RawValue << Fix64.FractionalBits)));

        return new FixVector2(X / length, Y / length);
    }

    public static Fix64 DistanceSquared(FixVector2 left, FixVector2 right) =>
        (left - right).LengthSquared;

    public static FixVector2 operator +(FixVector2 left, FixVector2 right) =>
        new(left.X + right.X, left.Y + right.Y);

    public static FixVector2 operator -(FixVector2 left, FixVector2 right) =>
        new(left.X - right.X, left.Y - right.Y);

    public static FixVector2 operator *(FixVector2 value, Fix64 scalar) =>
        new(value.X * scalar, value.Y * scalar);

    private static long IntegerSquareRoot(ulong value)
    {
        ulong result = 0;
        ulong bit = 1UL << 62;

        while (bit > value)
        {
            bit >>= 2;
        }

        while (bit != 0)
        {
            if (value >= result + bit)
            {
                value -= result + bit;
                result = (result >> 1) + bit;
            }
            else
            {
                result >>= 1;
            }

            bit >>= 2;
        }

        return checked((long)result);
    }
}
