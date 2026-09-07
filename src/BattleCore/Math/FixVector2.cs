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

        var length = Fix64.Sqrt(LengthSquared);

        // Sub-resolution vectors (length below 1/65536 m) square to zero in
        // Q16.16; treat them as no direction instead of dividing by zero.
        if (length.RawValue == 0)
        {
            return Zero;
        }

        return new FixVector2(X / length, Y / length);
    }

    public static Fix64 DistanceSquared(FixVector2 left, FixVector2 right) =>
        (left - right).LengthSquared;

    public static Fix64 Dot(FixVector2 left, FixVector2 right) =>
        (left.X * right.X) + (left.Y * right.Y);

    public static FixVector2 operator +(FixVector2 left, FixVector2 right) =>
        new(left.X + right.X, left.Y + right.Y);

    public static FixVector2 operator -(FixVector2 left, FixVector2 right) =>
        new(left.X - right.X, left.Y - right.Y);

    public static FixVector2 operator *(FixVector2 value, Fix64 scalar) =>
        new(value.X * scalar, value.Y * scalar);
}
