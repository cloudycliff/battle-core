using BattleCore.Buffs;
using BattleCore.Combat;
using BattleCore.Configuration;
using BattleCore.Math;
using BattleCore.Pickups;
using BattleCore.Simulation;
using BattleCore.Simulation.Commands;
using BattleCore.Simulation.Events;
using BattleCore.Skills;

namespace BattleCore.Tests;

public sealed class ResourcePoolTests
{
    [Fact]
    public void CastSkill_SpendsResourcesAtomicallyAndRejectsIfShort()
    {
        var battle = CreateBattle(boltCost: 40);
        SpawnCaster(battle);

        battle.Enqueue(new CastSkillCommand(1, 1, "bolt"));
        var rejected = battle.Tick();
        Assert.Contains(rejected.OfType<CommandRejectedEvent>(), item =>
            item.Reason.Contains("mana", StringComparison.Ordinal));
        Assert.Empty(rejected.OfType<SkillStartedEvent>());
        Assert.Equal(30, battle.GetResource(1, "mana"));

        battle.Enqueue(new GrantResourceCommand(2, 1, "mana", 20));
        var granted = battle.Tick();
        Assert.Equal(50, battle.GetResource(1, "mana"));
        Assert.Equal(20, Assert.Single(granted.OfType<ResourceChangedEvent>()).Amount);

        battle.Enqueue(new CastSkillCommand(3, 1, "bolt"));
        var cast = battle.Tick();
        Assert.Single(cast.OfType<SkillStartedEvent>());
        Assert.Equal(10, battle.GetResource(1, "mana"));
    }

    [Fact]
    public void Resources_ClampToMaximumAndSurviveDeath()
    {
        var battle = CreateBattle(boltCost: 1, lives: 1);
        SpawnCaster(battle);
        battle.Enqueue(new GrantResourceCommand(1, 1, "mana", 10_000));
        battle.Tick();
        Assert.Equal(80, battle.GetResource(1, "mana"));

        battle.Enqueue(new DealDamageCommand(2, 1, 1, DamageType.True, 10_000, CanCrit: false));
        battle.Tick();
        Assert.Equal(LifeState.Reviving, battle.GetCharacter(1)!.LifeState);
        Assert.Equal(80, battle.GetResource(1, "mana"));
    }

    [Fact]
    public void PickupAndPeriodicBuff_GrantResource()
    {
        var regen = new BuffDefinition(
            "regen",
            durationFrames: 3,
            BuffStackingRule.Refresh,
            [new PeriodicResourceBuffEffect(1, "mana", 5)]);
        var pickup = new PickupDefinition(
            "vial",
            Fix64.FromRatio(1, 2),
            PickupKind.Instant,
            [new PickupGrantResourceEffect("mana", 7)]);
        var battle = CreateBattle(boltCost: 1, buffs: [regen], pickups: [pickup]);
        SpawnCaster(battle);
        battle.Enqueue(new SpawnPickupCommand(1, "vial", Point(2, 2)));
        battle.Tick();
        Assert.Equal(37, battle.GetResource(1, "mana"));

        battle.Enqueue(new ApplyBuffCommand(2, 1, 1, "regen"));
        Assert.Equal(37, battle.GetResource(1, "mana"));
        battle.Tick();
        battle.Tick();
        Assert.Equal(42, battle.GetResource(1, "mana"));
    }

    private static BattleInstance CreateBattle(
        long boltCost,
        int lives = 0,
        IReadOnlyList<BuffDefinition>? buffs = null,
        IReadOnlyList<PickupDefinition>? pickups = null)
    {
        var values = BaseAttributes();
        var skill = new SkillDefinition(
            "bolt",
            1,
            0,
            [new SkillDamageEffect(0, SkillTarget.Self, DamageType.True, 1, CanCrit: false)],
            resourceCosts: new Dictionary<string, long>(StringComparer.Ordinal)
            {
                ["mana"] = boltCost,
            });
        var template = new CharacterTemplate(
            "hero",
            Fix64.FromRatio(1, 2),
            values,
            lives: lives,
            skillIds: [skill.Id],
            baseResources: new Dictionary<string, long>(StringComparer.Ordinal)
            {
                ["mana"] = 30,
            });
        var config = new BattleConfig(
            values.Keys,
            TestBattleFactory.Bindings,
            [template],
            [skill],
            buffDefinitions: buffs,
            pickupDefinitions: pickups,
            resourceDefinitions: [new ResourceDefinition("mana", 80)]);
        return TestBattleFactory.CreateBattle(config);
    }

    private static void SpawnCaster(BattleInstance battle)
    {
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Tick();
    }

    private static Dictionary<string, long> BaseAttributes()
    {
        var values = TestBattleFactory.Bindings.AllIds()
            .Concat(["attack"])
            .ToDictionary(id => id, _ => 0L, StringComparer.Ordinal);
        values[TestBattleFactory.Bindings.MaxHp] = 100;
        values[TestBattleFactory.Bindings.CritDamage] = 15_000;
        values[TestBattleFactory.Bindings.MoveSpeed] = 3;
        values["attack"] = 100;
        return values;
    }

    private static FixVector2 Point(long x, long y) =>
        new(Fix64.FromInt(x), Fix64.FromInt(y));
}
