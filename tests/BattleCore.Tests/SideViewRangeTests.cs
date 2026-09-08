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
    public void SkillRange_IgnoresVerticalSeparationWithinVerticalReach()
    {
        var skill = Strike(SkillTarget.ExplicitEnemy);
        var battle = CreateBattle(skill);
        SpawnPair(battle, targetY: Fix64.FromRatio(3, 2)); // 1 m above, 0 m across

        battle.Enqueue(new CastSkillCommand(1, 1, skill.Id, TargetId: 2));

        var events = battle.Tick();

        Assert.Single(events.OfType<SkillStartedEvent>());
        Assert.Empty(events.OfType<CommandRejectedEvent>());
        Assert.Equal(90, battle.GetCharacter(2)!.CurrentHp);
    }

    [Fact]
    public void SkillRange_RejectsTargetsBeyondVerticalReach()
    {
        // A shapeless single-target strike must not reach a target standing
        // several floors up just because their X coordinates match.
        var skill = Strike(SkillTarget.ExplicitEnemy);
        var battle = CreateBattle(skill);
        SpawnPair(battle, targetY: Fix64.FromInt(6));
        battle.Enqueue(new CastSkillCommand(1, 1, skill.Id, TargetId: 2));

        var events = battle.Tick();

        Assert.Single(events.OfType<CommandRejectedEvent>());
        Assert.Empty(events.OfType<SkillStartedEvent>());
        Assert.Equal(100, battle.GetCharacter(2)!.CurrentHp);
    }

    [Fact]
    public void SkillRange_VerticalReachIsConfigurable()
    {
        var skill = Strike(SkillTarget.ExplicitEnemy);
        var battle = CreateBattle(skill, verticalReach: Fix64.FromInt(8));
        SpawnPair(battle, targetY: Fix64.FromInt(6));
        battle.Enqueue(new CastSkillCommand(1, 1, skill.Id, TargetId: 2));

        var events = battle.Tick();

        Assert.Empty(events.OfType<CommandRejectedEvent>());
        Assert.Equal(90, battle.GetCharacter(2)!.CurrentHp);
    }

    [Fact]
    public void ChaseAttack_AcquiresAndAttacksByHorizontalDistanceWithinVerticalReach()
    {
        var skill = Strike(SkillTarget.CurrentEnemy);
        var brain = new BrainDefinition(
            "chase",
            BrainKind.ChaseAttack,
            aggroRadius: Fix64.One,
            attackRange: Fix64.One);
        var battle = CreateBattle(skill, brain);
        SpawnPair(battle, brain.Id, targetY: Fix64.FromRatio(3, 2));

        var events = battle.Tick();

        Assert.Equal(2, Assert.Single(events.OfType<TargetChangedEvent>()).TargetId);
        Assert.Equal(skill.Id, Assert.Single(events.OfType<SkillStartedEvent>()).SkillId);
        Assert.Equal(90, battle.GetCharacter(2)!.CurrentHp);
    }

    [Fact]
    public void ChaseAttack_DoesNotAcquireOrSpamAtTargetsBeyondVerticalReach()
    {
        var skill = Strike(SkillTarget.CurrentEnemy);
        var brain = new BrainDefinition(
            "chase",
            BrainKind.ChaseAttack,
            aggroRadius: Fix64.FromInt(10),
            attackRange: Fix64.One);
        var battle = CreateBattle(skill, brain);
        SpawnPair(battle, brain.Id, targetY: Fix64.FromInt(6));

        var casts = 0;
        for (var frame = 0; frame < 60; frame++)
        {
            casts += battle.Tick().OfType<SkillStartedEvent>().Count();
        }

        Assert.Equal(0, casts);
        Assert.Null(battle.GetCharacter(1)!.CurrentTargetId);
        Assert.Equal(100, battle.GetCharacter(2)!.CurrentHp);
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
        bool sideView = true,
        Fix64? verticalReach = null)
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
        // Upper floor under the y = 6 spawn so a distant target stays distant.
        FixAabb[] terrain = [new(Point(0, 4), Point(4, 5))];
        var map = sideView
            ? new MapConfig(
                bounds,
                terrain,
                spatialMode: SpatialMode.SideView,
                sideView: new SideViewSettings(
                    Gravity: Fix64.FromInt(30),
                    MaximumFallSpeed: Fix64.FromInt(20),
                    JumpSpeed: Fix64.FromInt(10),
                    VerticalReach: verticalReach))
            : new MapConfig(bounds, terrain);
        return new BattleInstance(config, map, seed: 123, maxFrames: 1_000);
    }

    private static void SpawnPair(
        BattleInstance battle,
        string? brainId = null,
        Fix64? targetY = null)
    {
        battle.Enqueue(new SpawnCharacterCommand(
            0,
            "hero",
            1,
            Point(2, Fix64.FromRatio(1, 2)),
            BrainId: brainId));
        battle.Enqueue(new SpawnCharacterCommand(
            0,
            "hero",
            2,
            Point(2, targetY ?? Fix64.FromInt(6))));

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
