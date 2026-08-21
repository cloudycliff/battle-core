using BattleCore.Attributes;
using BattleCore.Buffs;
using BattleCore.Combat;
using BattleCore.Configuration;
using BattleCore.Math;
using BattleCore.Simulation;
using BattleCore.Simulation.Commands;
using BattleCore.Simulation.Events;
using BattleCore.Skills;

namespace BattleCore.Tests;

public sealed class BuffSystemTests
{
    [Fact]
    public void StackBuff_RebuildsAttributeModifierAndHonorsMaxStacks()
    {
        var buff = new BuffDefinition(
            "haste",
            durationFrames: 10,
            BuffStackingRule.Stack,
            [new AttributeBuffEffect("move_speed", ModifierKind.Flat, 3)],
            maxStacks: 2);
        var battle = CreateBattle(buff);
        battle.Enqueue(new ApplyBuffCommand(1, 1, 1, "haste"));
        battle.Enqueue(new ApplyBuffCommand(1, 1, 1, "haste"));
        battle.Enqueue(new ApplyBuffCommand(1, 1, 1, "haste"));
        battle.Enqueue(new MoveDirectionCommand(1, 1, Point(1, 0)));

        var events = battle.Tick();
        var moved = Assert.Single(events.OfType<CharacterMovedEvent>());

        Assert.Equal(2, battle.GetCharacter(1)!.Buffs.Single().Stacks);
        Assert.Equal(Fix64.FromRatio(23, 10), moved.To.X);
    }

    [Fact]
    public void Buff_ExpiresAndRemovesAttributeModifier()
    {
        var buff = new BuffDefinition(
            "haste",
            durationFrames: 1,
            BuffStackingRule.Refresh,
            [new AttributeBuffEffect("move_speed", ModifierKind.Flat, 3)]);
        var battle = CreateBattle(buff);
        battle.Enqueue(new ApplyBuffCommand(1, 1, 1, "haste"));

        battle.Tick();
        var expiryEvents = battle.Tick();

        Assert.Single(expiryEvents.OfType<BuffRemovedEvent>());
        Assert.Empty(battle.GetCharacter(1)!.Buffs);
    }

    [Fact]
    public void PeriodicDamage_TicksAtConfiguredInterval()
    {
        var buff = new BuffDefinition(
            "burn",
            durationFrames: 4,
            BuffStackingRule.Refresh,
            [
                new PeriodicDamageBuffEffect(
                    IntervalFrames: 1,
                    DamageType.True,
                    Flat: 7),
            ]);
        var battle = CreateBattle(buff, spawnEnemy: true);
        battle.Enqueue(new ApplyBuffCommand(1, 1, 2, "burn"));

        Assert.Empty(battle.Tick().OfType<DamageEvent>());
        Assert.Equal(7, Assert.Single(battle.Tick().OfType<DamageEvent>()).Amount);
        Assert.Equal(93, battle.GetCharacter(2)!.CurrentHp);
    }

    [Fact]
    public void PeriodicDamage_SnapshotsSourceContribution()
    {
        var buff = new BuffDefinition(
            "burn",
            durationFrames: 6,
            BuffStackingRule.Refresh,
            [
                new PeriodicDamageBuffEffect(
                    IntervalFrames: 1,
                    DamageType.True,
                    Flat: 10),
            ]);
        var battle = CreateBattle(buff, spawnEnemy: true);
        battle.Enqueue(new ApplyBuffCommand(1, 1, 2, "burn"));
        battle.Tick();
        Assert.Equal(10, Assert.Single(battle.Tick().OfType<DamageEvent>()).Amount);

        battle.Enqueue(new ApplyModifierSourceCommand(
            3,
            1,
            "amp",
            [new ModifierContribution(
                TestBattleFactory.Bindings.DamageDealt,
                ModifierKind.Flat,
                10_000)]));
        battle.Tick();
        Assert.Equal(10, Assert.Single(battle.Tick().OfType<DamageEvent>()).Amount);
        Assert.Equal(10_000, battle.GetAttribute(1, TestBattleFactory.Bindings.DamageDealt));
    }

    [Fact]
    public void Stun_BlocksMovementAndCancelsActiveSkill()
    {
        var stun = new BuffDefinition(
            "stun",
            durationFrames: 2,
            BuffStackingRule.Refresh,
            [new ControlBuffEffect(ControlType.Stun)]);
        var skill = new SkillDefinition(
            "long_cast",
            durationFrames: 5,
            cooldownFrames: 0,
            []);
        var battle = CreateBattle(stun, skill);
        battle.Enqueue(new CastSkillCommand(1, 1, "long_cast"));
        battle.Enqueue(new ApplyBuffCommand(1, 1, 1, "stun"));
        battle.Enqueue(new MoveDirectionCommand(1, 1, Point(1, 0)));

        var events = battle.Tick();

        Assert.Single(events.OfType<SkillStartedEvent>());
        Assert.Single(events.OfType<SkillEndedEvent>());
        Assert.Single(events.OfType<ControlChangedEvent>());
        Assert.Empty(events.OfType<CharacterMovedEvent>());
        Assert.Contains(ControlType.Stun, battle.GetCharacter(1)!.Controls);
    }

    [Fact]
    public void Silence_BlocksSpellButNotUnrestrictedSkill()
    {
        var silence = new BuffDefinition(
            "silence",
            durationFrames: 5,
            BuffStackingRule.Refresh,
            [new ControlBuffEffect(ControlType.Silence)]);
        var spell = new SkillDefinition("spell", 1, 0, []);
        var unrestricted = new SkillDefinition(
            "item",
            1,
            0,
            [],
            category: SkillCategory.Unrestricted);
        var battle = CreateBattle(silence, [spell, unrestricted]);
        battle.Enqueue(new ApplyBuffCommand(1, 1, 1, "silence"));
        battle.Tick();
        battle.Enqueue(new CastSkillCommand(2, 1, "spell"));

        Assert.Single(battle.Tick().OfType<CommandRejectedEvent>());

        battle.Enqueue(new CastSkillCommand(3, 1, "item"));
        Assert.Single(battle.Tick().OfType<SkillStartedEvent>());
    }

    [Fact]
    public void SkillEffect_AppliesConfiguredBuff()
    {
        var buff = new BuffDefinition(
            "mark",
            durationFrames: 10,
            BuffStackingRule.Unique,
            []);
        var skill = new SkillDefinition(
            "mark_skill",
            1,
            0,
            [
                new SkillApplyBuffEffect(
                    0,
                    SkillTarget.ExplicitEnemy,
                    "mark"),
            ]);
        var battle = CreateBattle(buff, skill, spawnEnemy: true);
        battle.Enqueue(new CastSkillCommand(1, 1, "mark_skill", TargetId: 2));

        var added = Assert.Single(battle.Tick().OfType<BuffAddedEvent>());

        Assert.Equal(2, added.CharacterId);
        Assert.Equal("mark", battle.GetCharacter(2)!.Buffs.Single().BuffId);
    }

    [Fact]
    public void ImmunityTag_RejectsMatchingBuff()
    {
        var immunity = new BuffDefinition(
            "unstoppable",
            10,
            BuffStackingRule.Unique,
            [],
            immunityTags: ["control"]);
        var stun = new BuffDefinition(
            "stun",
            10,
            BuffStackingRule.Refresh,
            [new ControlBuffEffect(ControlType.Stun)],
            tags: ["control"]);
        var battle = CreateBattle([immunity, stun]);
        battle.Enqueue(new ApplyBuffCommand(1, 1, 1, "unstoppable"));
        battle.Enqueue(new ApplyBuffCommand(1, 1, 1, "stun"));

        var events = battle.Tick();

        Assert.Single(events.OfType<CommandRejectedEvent>());
        Assert.DoesNotContain(ControlType.Stun, battle.GetCharacter(1)!.Controls);
        Assert.Equal("unstoppable", battle.GetCharacter(1)!.Buffs.Single().BuffId);
    }

    [Fact]
    public void ReplacesTags_RemovesOlderMutuallyExclusiveBuff()
    {
        var weak = new BuffDefinition(
            "weak_stance",
            10,
            BuffStackingRule.Unique,
            [],
            tags: ["stance"]);
        var strong = new BuffDefinition(
            "strong_stance",
            10,
            BuffStackingRule.Unique,
            [],
            tags: ["stance"],
            replacesTags: ["stance"]);
        var battle = CreateBattle([weak, strong]);
        battle.Enqueue(new ApplyBuffCommand(1, 1, 1, "weak_stance"));
        battle.Tick();
        battle.Enqueue(new ApplyBuffCommand(2, 1, 1, "strong_stance"));

        var events = battle.Tick();

        Assert.Single(events.OfType<BuffRemovedEvent>());
        Assert.Equal("strong_stance", battle.GetCharacter(1)!.Buffs.Single().BuffId);
    }

    [Fact]
    public void Dispel_RemovesMatchingBuffsByInstanceIdUpToLimit()
    {
        var first = new BuffDefinition(
            "first",
            10,
            BuffStackingRule.Independent,
            [],
            tags: ["debuff"]);
        var second = new BuffDefinition(
            "second",
            10,
            BuffStackingRule.Independent,
            [],
            tags: ["debuff"]);
        var battle = CreateBattle([first, second]);
        battle.Enqueue(new ApplyBuffCommand(1, 1, 1, "first"));
        battle.Enqueue(new ApplyBuffCommand(1, 1, 1, "second"));
        battle.Tick();
        battle.Enqueue(new DispelBuffsCommand(2, 1, 1, "debuff", MaxCount: 1));

        var removed = Assert.Single(battle.Tick().OfType<BuffRemovedEvent>());

        Assert.Equal("first", removed.BuffId);
        Assert.Equal("second", battle.GetCharacter(1)!.Buffs.Single().BuffId);
    }

    private static BattleInstance CreateBattle(
        BuffDefinition buff,
        SkillDefinition? skill = null,
        bool spawnEnemy = false) =>
        CreateBattle(buff, skill is null ? [] : [skill], spawnEnemy);

    private static BattleInstance CreateBattle(
        BuffDefinition buff,
        IReadOnlyList<SkillDefinition> skills,
        bool spawnEnemy = false)
        => CreateBattle([buff], skills, spawnEnemy);

    private static BattleInstance CreateBattle(
        IReadOnlyList<BuffDefinition> buffs,
        IReadOnlyList<SkillDefinition>? skills = null,
        bool spawnEnemy = false)
    {
        skills ??= [];
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
        var battle = TestBattleFactory.CreateBattle(config);
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        if (spawnEnemy)
        {
            battle.Enqueue(new SpawnCharacterCommand(0, "hero", 2, Point(8, 2)));
        }

        battle.Tick();
        return battle;
    }

    private static FixVector2 Point(long x, long y) =>
        new(Fix64.FromInt(x), Fix64.FromInt(y));
}
