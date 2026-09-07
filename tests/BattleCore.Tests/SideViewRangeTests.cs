using BattleCore.Brains;
using BattleCore.Combat;
using BattleCore.Configuration;
using BattleCore.Math;
using BattleCore.Simulation;
using BattleCore.Simulation.Commands;
using BattleCore.Simulation.Events;
using BattleCore.Skills;

namespace BattleCore.Tests;

public sealed class SideViewRangeTests
{
    [Fact]
    public void SkillRange_IgnoresVerticalSeparation()
    {
        var skill = Strike(SkillTarget.ExplicitEnemy);
        var battle = CreateBattle(skill);
        SpawnPair(battle);
        battle.Enqueue(new CastSkillCommand(1, 1, skill.Id, TargetId: 2));

        var events = battle.Tick();

        Assert.Single(events.OfType<SkillStartedEvent>());
        Assert.Empty(events.OfType<CommandRejectedEvent>());
        Assert.Equal(90, battle.GetCharacter(2)!.CurrentHp);
    }

    [Fact]
    public void ChaseAttack_AcquiresAndAttacksByHorizontalDistance()
    {
        var skill = Strike(SkillTarget.CurrentEnemy);
        var brain = new BrainDefinition(
            "chase",
            BrainKind.ChaseAttack,
            aggroRadius: Fix64.One,
            attackRange: Fix64.One);
        var battle = CreateBattle(skill, brain);
        SpawnPair(battle, brain.Id);

        var events = battle.Tick();

        Assert.Equal(2, Assert.Single(events.OfType<TargetChangedEvent>()).TargetId);
        Assert.Equal(skill.Id, Assert.Single(events.OfType<SkillStartedEvent>()).SkillId);
        Assert.Equal(90, battle.GetCharacter(2)!.CurrentHp);
    }

    [Fact]
    public void TopDownSkillRange_StillIncludesVerticalSeparation()
    {
        var skill = Strike(SkillTarget.ExplicitEnemy);
        var battle = CreateBattle(skill, sideView: false);
        SpawnPair(battle);
        battle.Enqueue(new CastSkillCommand(1, 1, skill.Id, TargetId: 2));

        var events = battle.Tick();

        Assert.Single(events.OfType<CommandRejectedEvent>());
        Assert.Empty(events.OfType<SkillStartedEvent>());
        Assert.Equal(100, battle.GetCharacter(2)!.CurrentHp);
    }

    private static SkillDefinition Strike(SkillTarget target) =>
        new(
            "strike",
            durationFrames: 1,
            cooldownFrames: 10,
            [
                new SkillDamageEffect(
                    0,
                    target,
                    DamageType.True,
                    Flat: 10,
                    CanCrit: false),
            ],
            range: Fix64.One);

    private static BattleInstance CreateBattle(
        SkillDefinition skill,
        BrainDefinition? brain = null,
        bool sideView = true)
    {
        var values = TestBattleFactory.Bindings.AllIds()
            .Concat(["attack"])
            .ToDictionary(id => id, _ => 0L, StringComparer.Ordinal);
        values[TestBattleFactory.Bindings.MaxHp] = 100;
        values[TestBattleFactory.Bindings.CritDamage] = 15_000;
        values[TestBattleFactory.Bindings.MoveSpeed] = 3;
        values["attack"] = 100;

        var template = new CharacterTemplate(
            "hero",
            Fix64.FromRatio(1, 2),
            values,
            skillIds: [skill.Id]);
        var config = new BattleConfig(
            values.Keys,
            TestBattleFactory.Bindings,
            [template],
            [skill],
            brainDefinitions: brain is null ? null : [brain]);
        var bounds = new FixAabb(Point(0, 0), Point(20, 10));
        var map = sideView
            ? new MapConfig(
                bounds,
                spatialMode: SpatialMode.SideView,
                sideView: new SideViewSettings(
                    Gravity: Fix64.FromInt(30),
                    MaximumFallSpeed: Fix64.FromInt(20),
                    JumpSpeed: Fix64.FromInt(10)))
            : new MapConfig(bounds);
        return new BattleInstance(config, map, seed: 123, maxFrames: 1_000);
    }

    private static void SpawnPair(BattleInstance battle, string? brainId = null)
    {
        battle.Enqueue(new SpawnCharacterCommand(
            0,
            "hero",
            1,
            Point(2, Fix64.FromRatio(1, 2)),
            BrainId: brainId));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 2, Point(2, 6)));

        if (brainId is null)
        {
            battle.Tick();
        }
    }

    private static FixVector2 Point(long x, long y) =>
        new(Fix64.FromInt(x), Fix64.FromInt(y));

    private static FixVector2 Point(long x, Fix64 y) =>
        new(Fix64.FromInt(x), y);
}
