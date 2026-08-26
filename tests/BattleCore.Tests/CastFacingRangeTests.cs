using BattleCore.Combat;
using BattleCore.Configuration;
using BattleCore.Math;
using BattleCore.Simulation;
using BattleCore.Simulation.Commands;
using BattleCore.Simulation.Events;
using BattleCore.Skills;

namespace BattleCore.Tests;

public sealed class CastFacingRangeTests
{
    [Fact]
    public void DirectedCast_UpdatesFacingOnAcceptAndDoesNotRollBack()
    {
        var skill = Strike();
        var battle = SpawnPair(skill);
        battle.Enqueue(new SetFacingCommand(1, 1, new Facing(256)));
        battle.Tick();
        battle.Enqueue(new CastSkillCommand(2, 1, skill.Id, TargetId: 2));

        var started = battle.Tick();

        Assert.Equal(0, Assert.Single(started.OfType<FacingChangedEvent>()).Facing.Step);
        Assert.Equal(0, battle.GetCharacter(1)!.Facing.Step);
        Assert.Single(started.OfType<SkillStartedEvent>());
    }

    [Fact]
    public void SelfCast_LeavesFacingUnchanged()
    {
        var skill = new SkillDefinition(
            "heal",
            1,
            0,
            [new SkillHealEffect(0, SkillTarget.Self, Flat: 1, CanCrit: false)]);
        var battle = SpawnPair(skill);
        battle.Enqueue(new SetFacingCommand(1, 1, new Facing(256)));
        battle.Tick();
        battle.Enqueue(new CastSkillCommand(2, 1, skill.Id));

        var events = battle.Tick();

        Assert.Empty(events.OfType<FacingChangedEvent>());
        Assert.Equal(256, battle.GetCharacter(1)!.Facing.Step);
    }

    [Fact]
    public void PositiveRange_RejectsFarExplicitTargetWithoutSpendingCooldown()
    {
        var skill = Strike(range: Fix64.FromInt(2));
        var battle = SpawnPair(skill, resourceCost: true);
        battle.Enqueue(new CastSkillCommand(1, 1, skill.Id, TargetId: 2));

        var events = battle.Tick();

        Assert.Single(events.OfType<CommandRejectedEvent>());
        Assert.Empty(events.OfType<SkillStartedEvent>());
        Assert.Empty(events.OfType<FacingChangedEvent>());
        Assert.Equal(5, battle.GetCharacter(1)!.Resources["mana"]);
        Assert.False(battle.GetCharacter(1)!.CooldownReadyFrames.ContainsKey(skill.Id));
    }

    [Fact]
    public void PositiveRange_AcceptsTargetWithinRangePlusRadius()
    {
        var skill = Strike(range: Fix64.FromInt(6));
        var battle = SpawnPair(skill);
        battle.Enqueue(new CastSkillCommand(1, 1, skill.Id, TargetId: 2));

        Assert.Single(battle.Tick().OfType<SkillStartedEvent>());
        Assert.Equal(90, battle.GetCharacter(2)!.CurrentHp);
    }

    [Fact]
    public void DirectionDash_DoesNotCheckHorizontalRange()
    {
        var skill = new SkillDefinition(
            "dash",
            1,
            0,
            [new SkillDashEffect(0, Fix64.FromInt(6))],
            range: Fix64.One);
        var battle = SpawnPair(skill);
        battle.Enqueue(new CastSkillCommand(1, 1, skill.Id, Direction: Point(0, 1)));

        Assert.Single(battle.Tick().OfType<SkillStartedEvent>());
        Assert.Equal(Point(2, 8), battle.GetCharacter(1)!.Position);
    }

    private static SkillDefinition Strike(Fix64? range = null) =>
        new(
            "strike",
            durationFrames: 1,
            cooldownFrames: 10,
            [
                new SkillDamageEffect(
                    0,
                    SkillTarget.ExplicitEnemy,
                    DamageType.True,
                    Flat: 10,
                    CanCrit: false),
            ],
            range: range ?? Fix64.Zero);

    private static BattleInstance SpawnPair(SkillDefinition skill, bool resourceCost = false)
    {
        var values = TestBattleFactory.Bindings.AllIds()
            .Concat(["attack"])
            .ToDictionary(id => id, _ => 0L, StringComparer.Ordinal);
        values[TestBattleFactory.Bindings.MaxHp] = 100;
        values[TestBattleFactory.Bindings.CritDamage] = 15_000;
        values[TestBattleFactory.Bindings.MoveSpeed] = 3;
        values["attack"] = 100;
        var costs = resourceCost
            ? new Dictionary<string, long>(StringComparer.Ordinal) { ["mana"] = 3 }
            : null;
        if (resourceCost)
        {
            skill = new SkillDefinition(
                skill.Id,
                skill.DurationFrames,
                skill.CooldownFrames,
                skill.Effects,
                resourceCosts: costs,
                range: skill.Range);
        }

        var template = new CharacterTemplate(
            "hero",
            Fix64.FromRatio(1, 2),
            values,
            skillIds: [skill.Id],
            baseResources: resourceCost
                ? new Dictionary<string, long>(StringComparer.Ordinal) { ["mana"] = 5 }
                : null);
        var config = new BattleConfig(
            values.Keys,
            TestBattleFactory.Bindings,
            [template],
            [skill],
            resourceDefinitions: resourceCost ? [new ResourceDefinition("mana", 10)] : null);
        var battle = TestBattleFactory.CreateBattle(config);
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 2, Point(8, 2)));
        battle.Tick();
        return battle;
    }

    private static FixVector2 Point(long x, long y) =>
        new(Fix64.FromInt(x), Fix64.FromInt(y));
}
