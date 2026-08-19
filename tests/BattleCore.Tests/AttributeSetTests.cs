using BattleCore.Attributes;

namespace BattleCore.Tests;

public sealed class AttributeSetTests
{
    [Fact]
    public void Get_AppliesFlatThenOrderedPercentThenFinal()
    {
        var attributes = new AttributeSet(
            new Dictionary<string, long> { ["attack"] = 100 });
        attributes.Add(new AttributeModifier(4, "attack", ModifierKind.Final, 7));
        attributes.Add(new AttributeModifier(2, "attack", ModifierKind.Percent, 5_000));
        attributes.Add(new AttributeModifier(3, "attack", ModifierKind.Percent, 2_000));
        attributes.Add(new AttributeModifier(1, "attack", ModifierKind.Flat, 20));

        Assert.Equal(223, attributes.Get("attack"));
    }

    [Fact]
    public void Remove_RecalculatesValue()
    {
        var attributes = new AttributeSet(
            new Dictionary<string, long> { ["attack"] = 100 });
        attributes.Add(new AttributeModifier(1, "attack", ModifierKind.Flat, 20));

        Assert.True(attributes.Remove(1));
        Assert.Equal(100, attributes.Get("attack"));
    }
}
