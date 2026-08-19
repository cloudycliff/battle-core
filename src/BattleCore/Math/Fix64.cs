using System.Globalization;

namespace BattleCore.Math;

/// <summary>Signed Q16.16 fixed-point value.</summary>
public readonly struct Fix64 :
    IComparable<Fix64>,
    IEquatable<Fix64>
{
    public const int FractionalBits = 16;
    public const long Scale = 1L << FractionalBits;

    public static readonly Fix64 Zero = new(0);
    public static readonly Fix64 One = new(Scale);

    private Fix64(long rawValue) => RawValue = rawValue;

    public long RawValue { get; }

    public static Fix64 FromRaw(long rawValue) => new(rawValue);

    public static Fix64 FromInt(long value) =>
        new(checked(value * Scale));

    public static Fix64 FromRatio(long numerator, long denominator)
    {
        ArgumentOutOfRangeException.ThrowIfZero(denominator);
        var raw = (Int128)numerator * Scale / denominator;
        return new(checked((long)raw));
    }

    public long ToInt64() => RawValue / Scale;

    public static Fix64 Abs(Fix64 value) =>
        value.RawValue >= 0 ? value : new(checked(-value.RawValue));

    public static Fix64 Min(Fix64 left, Fix64 right) =>
        left <= right ? left : right;

    public static Fix64 Max(Fix64 left, Fix64 right) =>
        left >= right ? left : right;

    public static Fix64 Clamp(Fix64 value, Fix64 min, Fix64 max)
    {
        if (min > max)
        {
            throw new ArgumentException("Minimum cannot exceed maximum.");
        }

        return Min(Max(value, min), max);
    }

    public int CompareTo(Fix64 other) => RawValue.CompareTo(other.RawValue);

    public bool Equals(Fix64 other) => RawValue == other.RawValue;

    public override bool Equals(object? obj) => obj is Fix64 other && Equals(other);

    public override int GetHashCode() => RawValue.GetHashCode();

    public override string ToString() =>
        ((decimal)RawValue / Scale).ToString(CultureInfo.InvariantCulture);

    public static Fix64 operator +(Fix64 left, Fix64 right) =>
        new(checked(left.RawValue + right.RawValue));

    public static Fix64 operator -(Fix64 left, Fix64 right) =>
        new(checked(left.RawValue - right.RawValue));

    public static Fix64 operator -(Fix64 value) =>
        new(checked(-value.RawValue));

    public static Fix64 operator *(Fix64 left, Fix64 right)
    {
        var raw = (Int128)left.RawValue * right.RawValue / Scale;
        return new(checked((long)raw));
    }

    public static Fix64 operator /(Fix64 left, Fix64 right)
    {
        ArgumentOutOfRangeException.ThrowIfZero(right.RawValue);
        var raw = (Int128)left.RawValue * Scale / right.RawValue;
        return new(checked((long)raw));
    }

    public static bool operator ==(Fix64 left, Fix64 right) => left.Equals(right);
    public static bool operator !=(Fix64 left, Fix64 right) => !left.Equals(right);
    public static bool operator <(Fix64 left, Fix64 right) => left.RawValue < right.RawValue;
    public static bool operator <=(Fix64 left, Fix64 right) => left.RawValue <= right.RawValue;
    public static bool operator >(Fix64 left, Fix64 right) => left.RawValue > right.RawValue;
    public static bool operator >=(Fix64 left, Fix64 right) => left.RawValue >= right.RawValue;
}
