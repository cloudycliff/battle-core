using BattleCore.Brains;
using BattleCore.Combat;
using BattleCore.Configuration;
using BattleCore.Math;
using BattleCore.Simulation.Commands;
using BattleCore.Simulation.Events;
using BattleCore.Skills;

namespace BattleCore.Tests;

public sealed class BrainTests
{
    [Fact]
    public void Constructor_RejectsTemplateWithUnknownBrain()
    {
        var values = HeroAttributes();
        var template = new CharacterTemplate(
            "hero",
            Fix64.FromRatio(1, 2),
            values,
            brainId: "missing");

        var error = Assert.Throws<ConfigurationException>(() =>
            new BattleConfig(values.Keys, TestBattleFactory.Bindings, [template]));

        Assert.Contains("missing", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Idle_DoesNotAcquireOrMove()
    {
        var battle = CreateBattle(new BrainDefinition("idle", BrainKind.Idle));
        SpawnFactions(battle, Point(2, 2), Point(4, 2), "idle");

        var events = battle.Tick();

        Assert.Empty(events.OfType<TargetChangedEvent>());
        Assert.Empty(events.OfType<CharacterMovedEvent>());
        Assert.Empty(events.OfType<SkillStartedEvent>());
    }

    [Fact]
    public void PlayerControlled_DoesNotRunBrain()
    {
        var battle = CreateBattle(
            new BrainDefinition("chase", BrainKind.ChaseAttack),
            strike: true);
        battle.Enqueue(new SpawnCharacterCommand(
            0,
            "hero",
            1,
            Point(2, 2),
            PlayerControlled: true,
            BrainId: "chase"));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 2, Point(3, 2)));

        var events = battle.Tick();

        Assert.Empty(events.OfType<TargetChangedEvent>());
        Assert.Empty(events.OfType<SkillStartedEvent>());
    }

    [Fact]
    public void ChaseAttack_MovesTowardNearestHostile()
    {
        var battle = CreateBattle(new BrainDefinition(
            "chase",
            BrainKind.ChaseAttack,
            aggroRadius: Fix64.FromInt(12),
            attackRange: Fix64.FromRatio(1, 2)));
        SpawnFactions(battle, Point(2, 2), Point(8, 2), "chase");

        var events = battle.Tick();

        Assert.Equal(2, Assert.Single(events.OfType<TargetChangedEvent>()).TargetId);
        var moved = Assert.Single(events.OfType<CharacterMovedEvent>());
        Assert.Equal(1, moved.CharacterId);
        Assert.True(moved.To.X > moved.From.X);
        Assert.Empty(events.OfType<SkillStartedEvent>());
    }

    [Fact]
    public void ChaseAttack_CastsHighestPriorityReadySkillInRange()
    {
        var heavy = Strike("heavy", Fix64.One);
        var light = Strike("light", Fix64.FromInt(8));
        var brain = new BrainDefinition(
            "chase",
            BrainKind.ChaseAttack,
            aggroRadius: Fix64.FromInt(12),
            attackRange: Fix64.FromInt(8));
        var battle = CreateBattle(brain, skills: [heavy, light]);
        SpawnFactions(battle, Point(2, 2), Point(6, 2), "chase");

        var started = Assert.Single(battle.Tick().OfType<SkillStartedEvent>());

        Assert.Equal(1, started.CasterId);
        Assert.Equal("light", started.SkillId);
    }

    [Fact]
    public void HoldPosition_AttacksWithoutChasing()
    {
        var brain = new BrainDefinition(
            "hold",
            BrainKind.HoldPosition,
            aggroRadius: Fix64.FromInt(12),
            attackRange: Fix64.FromRatio(1, 2));
        var battle = CreateBattle(brain, skills: [Strike("strike", Fix64.FromRatio(1, 2))]);
        SpawnFactions(battle, Point(2, 2), Point(8, 2), "hold");

        var events = battle.Tick();

        Assert.Equal(2, Assert.Single(events.OfType<TargetChangedEvent>()).TargetId);
        Assert.Empty(events.OfType<CharacterMovedEvent>());
        Assert.Empty(events.OfType<SkillStartedEvent>());
        Assert.Equal(Point(2, 2), battle.GetCharacter(1)!.Position);
    }

    [Fact]
    public void HoldPosition_CastsWhenAlreadyInRange()
    {
        var brain = new BrainDefinition(
            "hold",
            BrainKind.HoldPosition,
            aggroRadius: Fix64.FromInt(12),
            attackRange: Fix64.FromInt(8));
        var battle = CreateBattle(brain, strike: true);
        SpawnFactions(battle, Point(2, 2), Point(3, 2), "hold");

        Assert.Equal("strike", Assert.Single(battle.Tick().OfType<SkillStartedEvent>()).SkillId);
        Assert.Equal(Point(2, 2), battle.GetCharacter(1)!.Position);
    }

    [Fact]
    public void FollowOwner_ClosesToFollowDistanceThenStops()
    {
        var brain = new BrainDefinition(
            "follow",
            BrainKind.FollowOwner,
            aggroRadius: Fix64.FromInt(3),
            followDistance: Fix64.FromInt(2));
        var battle = CreateBattle(brain);
        battle.Enqueue(new SpawnCharacterCommand(
            0,
            "hero",
            1,
            Point(2, 2),
            PlayerControlled: true));
        battle.Enqueue(new SpawnCharacterCommand(
            0,
            "hero",
            1,
            Point(10, 2),
            BrainId: "follow",
            OwnerId: 1));

        battle.Tick();
        Assert.True(battle.GetCharacter(2)!.Position.X < Fix64.FromInt(10));

        for (var i = 0; i < 120; i++)
        {
            battle.Tick();
        }

        var pet = battle.GetCharacter(2)!;
        var owner = battle.GetCharacter(1)!;
        var distanceSquared = FixVector2.DistanceSquared(pet.Position, owner.Position);
        var leash = Fix64.FromInt(2) + pet.Radius;
        Assert.True(distanceSquared <= leash * leash);
        Assert.Empty(battle.Tick().OfType<CharacterMovedEvent>());
    }

    [Fact]
    public void FollowOwner_SwitchesToChaseWhenHostileIsInAggro()
    {
        var brain = new BrainDefinition(
            "follow",
            BrainKind.FollowOwner,
            aggroRadius: Fix64.FromInt(12),
            attackRange: Fix64.FromInt(8),
            followDistance: Fix64.FromInt(2));
        var battle = CreateBattle(brain, strike: true);
        battle.Enqueue(new SpawnCharacterCommand(
            0,
            "hero",
            1,
            Point(2, 2),
            PlayerControlled: true));
        battle.Enqueue(new SpawnCharacterCommand(
            0,
            "hero",
            1,
            Point(3, 2),
            BrainId: "follow",
            OwnerId: 1));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 2, Point(4, 2)));

        var started = Assert.Single(battle.Tick().OfType<SkillStartedEvent>());
        Assert.Equal(2, started.CasterId);
        Assert.Equal("strike", started.SkillId);
    }

    private static BattleCore.Simulation.BattleInstance CreateBattle(
        BrainDefinition brain,
        bool strike = false,
        IReadOnlyList<SkillDefinition>? skills = null)
    {
        skills ??= strike ? [Strike("strike", Fix64.FromInt(8))] : [];
        var values = HeroAttributes();
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
            brainDefinitions: [brain]);
        return TestBattleFactory.CreateBattle(config);
    }

    private static void SpawnFactions(
        BattleCore.Simulation.BattleInstance battle,
        FixVector2 self,
        FixVector2 enemy,
        string brainId)
    {
        battle.Enqueue(new SpawnCharacterCommand(
            0,
            "hero",
            1,
            self,
            BrainId: brainId));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 2, enemy));
    }

    private static SkillDefinition Strike(string id, Fix64 radius) =>
        new(
            id,
            durationFrames: 1,
            cooldownFrames: 10,
            [
                new SkillDamageEffect(
                    0,
                    new SkillTarget(SkillTargetKind.CurrentTarget, TargetRelation.Enemies, radius),
                    DamageType.True,
                    Flat: 10,
                    CanCrit: false),
            ]);

    private static Dictionary<string, long> HeroAttributes()
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
