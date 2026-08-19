using BattleCore.Configuration;
using BattleCore.Math;

namespace BattleCore.Tests;

public sealed class BattleConfigTests
{
    [Fact]
    public void Constructor_RejectsMissingBoundAttribute()
    {
        var bindings = TestBattleFactory.Bindings;
        var declared = bindings.AllIds()
            .Where(id => id != bindings.MoveSpeed)
            .ToArray();
        var template = new CharacterTemplate(
            "hero",
            Fix64.One,
            declared.ToDictionary(id => id, _ => 0L));

        var exception = Assert.Throws<ConfigurationException>(() =>
            new BattleConfig(declared, bindings, [template]));

        Assert.Contains("move_speed", exception.Message, StringComparison.Ordinal);
    }
}
