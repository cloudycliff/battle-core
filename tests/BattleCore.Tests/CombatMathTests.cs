using BattleCore.Combat;

namespace BattleCore.Tests;

public sealed class CombatMathTests
{
    [Fact]
    public void ComputeBase_AddsFlatAndScaledAttribute()
    {
        Assert.Equal(290, CombatMath.ComputeBase(50, 200, 12_000));
    }

    [Fact]
    public void ApplyMitigation_UsesDiminishingFormula()
    {
        Assert.Equal(500, CombatMath.ApplyMitigation(1_000, 100, 100, 8_000));
        Assert.Equal(1_000, CombatMath.ApplyMitigation(1_000, 0, 100, 8_000));
        Assert.Equal(200, CombatMath.ApplyMitigation(1_000, 900, 100, 8_000));
    }
}
