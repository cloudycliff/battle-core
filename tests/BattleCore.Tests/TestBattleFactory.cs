using BattleCore.Configuration;
using BattleCore.Math;
using BattleCore.Simulation;

namespace BattleCore.Tests;

internal static class TestBattleFactory
{
    public static readonly CombatAttributeBindings Bindings = new(
        "max_hp",
        "physical_defense",
        "magical_defense",
        "crit_rate",
        "crit_damage",
        "damage_dealt",
        "damage_taken",
        "move_speed");

    public static BattleConfig CreateConfig(
        Fix64? radius = null,
        bool blocksMovement = true,
        int lives = 0,
        int corpseDurationFrames = 30,
        int reviveDelayFrames = 2,
        bool canBeRevived = false)
    {
        var attributes = Bindings.AllIds().Concat(["attack"]).ToArray();
        var values = attributes.ToDictionary(id => id, _ => 0L, StringComparer.Ordinal);
        values[Bindings.MaxHp] = 100;
        values[Bindings.CritDamage] = 15_000;
        values[Bindings.MoveSpeed] = 3;
        values["attack"] = 100;

        var template = new CharacterTemplate(
            "hero",
            radius ?? Fix64.FromRatio(1, 2),
            values,
            blocksMovement: blocksMovement,
            lives: lives,
            canBeRevived: canBeRevived);

        return new BattleConfig(
            attributes,
            Bindings,
            [template],
            corpseDurationFrames: corpseDurationFrames,
            reviveDelayFrames: reviveDelayFrames);
    }

    public static BattleInstance CreateBattle(
        BattleConfig? config = null,
        IEnumerable<FixAabb>? terrain = null,
        long maxFrames = 1_000)
    {
        var bounds = new FixAabb(
            new FixVector2(Fix64.Zero, Fix64.Zero),
            new FixVector2(Fix64.FromInt(20), Fix64.FromInt(20)));

        return new BattleInstance(
            config ?? CreateConfig(),
            new MapConfig(bounds, terrain),
            seed: 123,
            maxFrames);
    }
}
