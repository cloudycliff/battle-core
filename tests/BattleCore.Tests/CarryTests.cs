using BattleCore.Buffs;
using BattleCore.Combat;
using BattleCore.Configuration;
using BattleCore.Math;
using BattleCore.Simulation.Commands;
using BattleCore.Simulation.Events;
using BattleCore.Skills;

namespace BattleCore.Tests;

public sealed class CarryTests
{
    [Fact]
    public void StartCarry_FollowsCarrierOffsetAndIgnoresCarriedMove()
    {
        var battle = CreateBattle(Grab());
        SpawnAllies(battle);
        battle.Enqueue(new CastSkillCommand(1, 1, "grab", TargetId: 2));
        Assert.Single(battle.Tick().OfType<CarryStartedEvent>());

        battle.Enqueue(new MoveDirectionCommand(2, 1, Point(1, 0)));
        battle.Enqueue(new MoveDirectionCommand(2, 2, Point(0, 1)));
        var moved = battle.Tick().OfType<CharacterMovedEvent>().ToArray();

        Assert.Equal(2, battle.GetCharacter(1)!.CarryingId);
        Assert.Equal(1, battle.GetCharacter(2)!.CarriedById);
        Assert.Contains(moved, item => item.CharacterId == 1);
        Assert.Contains(moved, item => item.CharacterId == 2);
        Assert.Equal(
            battle.GetCharacter(1)!.Position + new FixVector2(Fix64.FromInt(1), Fix64.Zero),
            battle.GetCharacter(2)!.Position);
        Assert.Equal(Fix64.FromInt(2), battle.GetCharacter(2)!.Position.Y);
    }

    [Fact]
    public void StartCarry_IgnoresOtherFactionAndSecondTarget()
    {
        var battle = CreateBattle(Grab());
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 2, Point(3, 2)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(4, 2)));
        battle.Tick();
        battle.Enqueue(new CastSkillCommand(1, 1, "grab", TargetId: 2));
        Assert.Empty(battle.Tick().OfType<CarryStartedEvent>());

        battle.Enqueue(new CastSkillCommand(2, 1, "grab", TargetId: 3));
        Assert.Single(battle.Tick().OfType<CarryStartedEvent>());
        battle.Enqueue(new CastSkillCommand(3, 1, "grab", TargetId: 3));
        Assert.Empty(battle.Tick().OfType<CarryStartedEvent>());
        Assert.Equal(3, battle.GetCharacter(1)!.CarryingId);
    }

    [Fact]
    public void DropCarry_LeavesTargetInPlace()
    {
        var battle = CreateBattle(Grab(), Drop());
        SpawnAllies(battle);
        battle.Enqueue(new CastSkillCommand(1, 1, "grab", TargetId: 2));
        battle.Tick();
        battle.Enqueue(new CastSkillCommand(2, 1, "drop"));

        Assert.Single(battle.Tick().OfType<CarryEndedEvent>());
        Assert.Null(battle.GetCharacter(1)!.CarryingId);
        Assert.Null(battle.GetCharacter(2)!.CarriedById);
        Assert.Equal(Point(3, 2), battle.GetCharacter(2)!.Position);
    }

    [Fact]
    public void ThrowCarry_DisplacesThenEnds()
    {
        var battle = CreateBattle(Grab(), Throw());
        SpawnAllies(battle);
        battle.Enqueue(new CastSkillCommand(1, 1, "grab", TargetId: 2));
        battle.Tick();
        battle.Enqueue(new CastSkillCommand(
            2,
            1,
            "throw",
            Direction: Point(1, 0)));

        var events = battle.Tick();
        var thrown = Assert.Single(events.OfType<CharacterThrownEvent>());
        Assert.Single(events.OfType<CarryEndedEvent>());
        Assert.Equal(Point(3, 2), thrown.From);
        Assert.Equal(Point(5, 2), thrown.To);
        Assert.Null(battle.GetCharacter(1)!.CarryingId);
        Assert.Equal(Point(5, 2), battle.GetCharacter(2)!.Position);
    }

    [Fact]
    public void Carry_DropsWhenCarrierIsControlledOrDies()
    {
        var stun = new BuffDefinition(
            "stun",
            durationFrames: 1,
            BuffStackingRule.Refresh,
            [new ControlBuffEffect(ControlType.Stun)]);
        var battle = CreateBattle([stun], Grab());
        SpawnAllies(battle);
        battle.Enqueue(new CastSkillCommand(1, 1, "grab", TargetId: 2));
        battle.Tick();
        battle.Enqueue(new ApplyBuffCommand(2, 1, 1, "stun"));

        Assert.Single(battle.Tick().OfType<CarryEndedEvent>());
        Assert.Null(battle.GetCharacter(2)!.CarriedById);

        battle.Tick();
        battle.Enqueue(new CastSkillCommand(4, 1, "grab", TargetId: 2));
        Assert.Single(battle.Tick().OfType<CarryStartedEvent>());
        battle.Enqueue(new DealDamageCommand(5, 2, 1, DamageType.True, 10_000, CanCrit: false));
        Assert.Single(battle.Tick().OfType<CarryEndedEvent>());
    }

    [Fact]
    public void DemoGrabThrowSkill_PicksUpThenThrowsOnLaterFrame()
    {
        var demo = new SkillDefinition(
            "grab_throw",
            durationFrames: 3,
            cooldownFrames: 0,
            [
                new SkillStartCarryEffect(0, SkillTarget.ExplicitAlly),
                new SkillThrowCarryEffect(2, SkillTarget.Self, Fix64.FromInt(2)),
            ],
            canMoveWhileCasting: true);
        var battle = CreateBattle(demo);
        SpawnAllies(battle);
        battle.Enqueue(new CastSkillCommand(
            1,
            1,
            "grab_throw",
            TargetId: 2,
            Direction: Point(1, 0)));

        Assert.Single(battle.Tick().OfType<CarryStartedEvent>());
        battle.Tick();
        var thrown = battle.Tick();
        Assert.Single(thrown.OfType<CharacterThrownEvent>());
        Assert.Single(thrown.OfType<CarryEndedEvent>());
        Assert.Equal(Point(5, 2), battle.GetCharacter(2)!.Position);
    }

    private static BattleCore.Simulation.BattleInstance CreateBattle(
        params SkillDefinition[] skills) =>
        CreateBattle([], skills);

    private static BattleCore.Simulation.BattleInstance CreateBattle(
        IReadOnlyList<BuffDefinition> buffs,
        params SkillDefinition[] skills)
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
            skillIds: skills.Select(item => item.Id));
        var config = new BattleConfig(
            values.Keys,
            TestBattleFactory.Bindings,
            [template],
            skills,
            buffs);
        return TestBattleFactory.CreateBattle(config);
    }

    private static void SpawnAllies(BattleCore.Simulation.BattleInstance battle)
    {
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(3, 2)));
        battle.Tick();
    }

    private static SkillDefinition Grab() =>
        new(
            "grab",
            durationFrames: 1,
            cooldownFrames: 0,
            [new SkillStartCarryEffect(0, SkillTarget.ExplicitAlly)]);

    private static SkillDefinition Drop() =>
        new(
            "drop",
            durationFrames: 1,
            cooldownFrames: 0,
            [new SkillDropCarryEffect(0, SkillTarget.Self)]);

    private static SkillDefinition Throw() =>
        new(
            "throw",
            durationFrames: 1,
            cooldownFrames: 0,
            [new SkillThrowCarryEffect(0, SkillTarget.Self, Fix64.FromInt(2))]);

    private static FixVector2 Point(long x, long y) =>
        new(Fix64.FromInt(x), Fix64.FromInt(y));
}
