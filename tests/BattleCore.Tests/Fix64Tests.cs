using BattleCore.Math;

namespace BattleCore.Tests;

public sealed class Fix64Tests
{
    [Fact]
    public void Arithmetic_UsesQ16Point16WithoutFloatingPoint()
    {
        var oneAndHalf = Fix64.FromRatio(3, 2);
        var two = Fix64.FromInt(2);

        Assert.Equal(Fix64.FromInt(3), oneAndHalf * two);
        Assert.Equal(Fix64.FromRatio(3, 4), oneAndHalf / two);
    }

    [Fact]
    public void NormalizeOrZero_ProducesUnitLength()
    {
        var vector = new FixVector2(Fix64.FromInt(3), Fix64.FromInt(4));

        var normalized = vector.NormalizeOrZero();

        Assert.InRange(
            normalized.LengthSquared.RawValue,
            Fix64.One.RawValue - 4,
            Fix64.One.RawValue + 4);
    }

    [Fact]
    public void Facing_WrapsIntoOneTurn()
    {
        Assert.Equal(1, new Facing(1025).Step);
        Assert.Equal(1023, new Facing(-1).Step);
    }
}
