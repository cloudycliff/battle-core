using BattleCore.Buffs;
using BattleCore.Combat;
using BattleCore.Configuration;
using BattleCore.Math;
using BattleCore.Simulation;
using BattleCore.Simulation.Commands;
using BattleCore.Simulation.Events;
using BattleCore.Skills;

namespace BattleCore.Tests;

public sealed class SkillTimelineTests
{
    [Fact]
    public void Cast_RunsEffectOnConfiguredFrameThenEnds()
    {
        var skill = new SkillDefinition(
            "strike",
            durationFrames: 3,
            cooldownFrames: 10,
            [
                new SkillDamageEffect(
                    1,
                    SkillTarget.ExplicitEnemy,
                    DamageType.True,
                    Flat: 25,
                    CanCrit: false),
            ]);
        var battle = SpawnPair(skill);
        battle.Enqueue(new CastSkillCommand(1, 1, "strike", TargetId: 2));

        var started = battle.Tick();
        var hit = battle.Tick();
        var ended = battle.Tick();

        Assert.Single(started.OfType<SkillStartedEvent>());
        Assert.Empty(started.OfType<DamageEvent>());
        Assert.Single(hit.OfType<SkillHitEvent>());
        Assert.Equal(25, Assert.Single(hit.OfType<DamageEvent>()).Amount);
        Assert.Equal(75, battle.GetCharacter(2)!.CurrentHp);
        Assert.Single(ended.OfType<SkillEndedEvent>());
        Assert.Null(battle.GetCharacter(1)!.ActiveSkillId);
    }

    [Fact]
    public void Cast_RejectsSkillUntilCooldownReadyFrame()
    {
        var skill = new SkillDefinition(
            "instant",
            durationFrames: 1,
            cooldownFrames: 3,
            [new SkillHealEffect(0, SkillTarget.Self, Flat: 1, CanCrit: false)]);
        var battle = SpawnPair(skill);
        battle.Enqueue(new CastSkillCommand(1, 1, "instant"));
        battle.Tick();
        battle.Enqueue(new CastSkillCommand(2, 1, "instant"));

        var rejected = battle.Tick();

        Assert.Single(rejected.OfType<CommandRejectedEvent>());
        Assert.Empty(rejected.OfType<SkillStartedEvent>());

        battle.Tick();
        battle.Enqueue(new CastSkillCommand(4, 1, "instant"));
        Assert.Single(battle.Tick().OfType<SkillStartedEvent>());
    }

    [Fact]
    public void Casting_BlocksMovementUnlessSkillAllowsIt()
    {
        var skill = new SkillDefinition(
            "rooted",
            durationFrames: 2,
            cooldownFrames: 0,
            []);
        var battle = SpawnPair(skill);
        battle.Enqueue(new MoveDirectionCommand(1, 1, Point(1, 0)));
        battle.Enqueue(new CastSkillCommand(1, 1, "rooted"));

        Assert.Empty(battle.Tick().OfType<CharacterMovedEvent>());
        Assert.Empty(battle.Tick().OfType<CharacterMovedEvent>());
        Assert.Single(battle.Tick().OfType<CharacterMovedEvent>());
    }

    [Fact]
    public void CircleTarget_ResolvesOnlyMatchingRelationsInEntityOrder()
    {
        var circle = new SkillTarget(
            SkillTargetKind.CircleAtPoint,
            TargetRelation.Enemies,
            Fix64.FromInt(1));
        var skill = new SkillDefinition(
            "blast",
            durationFrames: 1,
            cooldownFrames: 0,
            [
                new SkillDamageEffect(
                    0,
                    circle,
                    DamageType.True,
                    Flat: 10,
                    CanCrit: false),
            ]);
        var battle = CreateBattle(skill);
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(4, 2)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 2, Point(5, 2)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 2, Point(8, 2)));
        battle.Tick();
        battle.Enqueue(new CastSkillCommand(
            1,
            1,
            "blast",
            TargetPoint: Point(5, 2)));

        var events = battle.Tick();

        var hit = Assert.Single(events.OfType<SkillHitEvent>());
        Assert.Equal(3, hit.TargetId);
        Assert.Equal(90, battle.GetCharacter(3)!.CurrentHp);
        Assert.Equal(100, battle.GetCharacter(2)!.CurrentHp);
        Assert.Equal(100, battle.GetCharacter(4)!.CurrentHp);
    }

    [Fact]
    public void Config_RejectsUnknownSkillOnCharacter()
    {
        var values = BaseAttributes();
        var template = new CharacterTemplate(
            "hero",
            Fix64.FromRatio(1, 2),
            values,
            skillIds: ["missing"]);

        var error = Assert.Throws<ConfigurationException>(() =>
            new BattleConfig(values.Keys, TestBattleFactory.Bindings, [template]));

        Assert.Contains("missing", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FanTarget_UsesCastDirectionAndMinimumDot()
    {
        var fan = new SkillTarget(
            SkillTargetKind.FanAtCaster,
            TargetRelation.Enemies,
            Radius: Fix64.FromInt(3),
            MinimumDot: Fix64.FromRatio(1, 2));
        var skill = new SkillDefinition(
            "cleave",
            1,
            0,
            [new SkillDamageEffect(0, fan, DamageType.True, 10, CanCrit: false)]);
        var battle = CreateBattle(skill);
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(5, 5)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 2, Point(7, 5)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 2, Point(3, 5)));
        battle.Tick();
        battle.Enqueue(new CastSkillCommand(
            1,
            1,
            "cleave",
            Direction: Point(1, 0)));

        var hits = battle.Tick().OfType<SkillHitEvent>().ToArray();

        Assert.Single(hits);
        Assert.Equal(2, hits[0].TargetId);
        Assert.Equal(90, battle.GetCharacter(2)!.CurrentHp);
        Assert.Equal(100, battle.GetCharacter(3)!.CurrentHp);
    }

    [Fact]
    public void SelfStun_CancelsRemainingSameFrameEffectsWithoutDuplicateEnd()
    {
        // Review 2026-08-28 #4: a mid-frame stun used to leave the loop's
        // local cast running (later effects still fired) and the timeline end
        // emitted a second SkillEndedEvent.
        var stun = new BuffDefinition(
            "stun",
            durationFrames: 5,
            BuffStackingRule.Refresh,
            [new ControlBuffEffect(ControlType.Stun)]);
        var skill = new SkillDefinition(
            "reckless",
            durationFrames: 1,
            cooldownFrames: 0,
            [
                new SkillApplyBuffEffect(0, SkillTarget.Self, "stun"),
                new SkillDamageEffect(
                    0,
                    SkillTarget.Self,
                    DamageType.True,
                    Flat: 25,
                    CanCrit: false),
            ]);
        var battle = CreateBattle(skill, [stun]);
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Tick();
        battle.Enqueue(new CastSkillCommand(1, 1, "reckless"));

        var events = battle.Tick();

        Assert.Single(events.OfType<BuffAddedEvent>());
        Assert.Single(events.OfType<SkillEndedEvent>());
        Assert.Empty(events.OfType<DamageEvent>());
        Assert.Equal(100, battle.GetCharacter(1)!.CurrentHp);
    }

    [Fact]
    public void RingTarget_LeavesInnerHole()
    {
        var ring = new SkillTarget(
            SkillTargetKind.RingAtPoint,
            TargetRelation.Enemies,
            Radius: Fix64.FromInt(3),
            InnerRadius: Fix64.FromInt(2));
        var skill = new SkillDefinition(
            "nova",
            1,
            0,
            [new SkillDamageEffect(0, ring, DamageType.True, 10, CanCrit: false)]);
        var battle = CreateBattle(skill);
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 2, Point(6, 5)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 2, Point(7, 5)));
        battle.Tick();
        battle.Enqueue(new CastSkillCommand(
            1,
            1,
            "nova",
            TargetPoint: Point(5, 5)));

        var hits = battle.Tick().OfType<SkillHitEvent>().ToArray();

        Assert.Single(hits);
        Assert.Equal(3, hits[0].TargetId);
        Assert.Equal(100, battle.GetCharacter(2)!.CurrentHp);
        Assert.Equal(90, battle.GetCharacter(3)!.CurrentHp);
    }

    private static BattleInstance SpawnPair(SkillDefinition skill)
    {
        var battle = CreateBattle(skill);
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 2, Point(8, 2)));
        battle.Tick();
        return battle;
    }

    private static BattleInstance CreateBattle(
        SkillDefinition skill,
        IReadOnlyList<BuffDefinition>? buffs = null)
    {
        var values = BaseAttributes();
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
            buffDefinitions: buffs);
        return TestBattleFactory.CreateBattle(config);
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
