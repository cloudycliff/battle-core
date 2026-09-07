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
    public void Sqrt_MatchesPerfectSquares()
    {
        Assert.Equal(Fix64.Zero, Fix64.Sqrt(Fix64.Zero));
        Assert.Equal(Fix64.FromInt(2), Fix64.Sqrt(Fix64.FromInt(4)));
        Assert.Equal(Fix64.FromInt(3), Fix64.Sqrt(Fix64.FromInt(9)));
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
    public void NormalizeOrZero_ReturnsZeroForSubResolutionVectors()
    {
        var tiny = new FixVector2(Fix64.FromRaw(1), Fix64.Zero);
        var tinyDiagonal = new FixVector2(Fix64.FromRaw(200), Fix64.FromRaw(-200));

        Assert.Equal(FixVector2.Zero, tiny.NormalizeOrZero());
        Assert.Equal(FixVector2.Zero, tinyDiagonal.NormalizeOrZero());
    }

    [Fact]
    public void Facing_FromDirection_UsesQuarterTurnAxes()
    {
        Assert.Equal(0, Facing.FromDirection(new FixVector2(Fix64.One, Fix64.Zero)).Step);
        Assert.Equal(256, Facing.FromDirection(new FixVector2(Fix64.Zero, Fix64.One)).Step);
        Assert.Equal(512, Facing.FromDirection(new FixVector2(-Fix64.One, Fix64.Zero)).Step);
        Assert.Equal(768, Facing.FromDirection(new FixVector2(Fix64.Zero, -Fix64.One)).Step);
    }
}
