using BattleCore.Buffs;
using BattleCore.Combat;
using BattleCore.Configuration;
using BattleCore.Math;
using BattleCore.Simulation;
using BattleCore.Simulation.Commands;
using BattleCore.Simulation.Events;
using BattleCore.Skills;

namespace BattleCore.Tests;

public sealed class SkillReviveTests
{
    [Fact]
    public void ReviveSkill_RaisesDeadAllyInCorpseWindow()
    {
        var battle = CreateBattle(ReviveSkill());
        KillAlly(battle);

        battle.Enqueue(new CastSkillCommand(2, 1, "rez", TargetId: 2));
        var events = battle.Tick();

        Assert.Single(events.OfType<SkillHitEvent>());
        Assert.Single(events.OfType<RevivedEvent>());
        Assert.Equal(LifeState.Alive, battle.GetCharacter(2)!.LifeState);
        Assert.Equal(100, battle.GetCharacter(2)!.CurrentHp);
    }

    [Fact]
    public void ReviveSkill_RejectsLivingEnemyAndRevivingTargets()
    {
        var battle = CreateBattle(ReviveSkill(), lives: 1, reviveDelayFrames: 5);
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(8, 2)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 2, Point(12, 2)));
        battle.Tick();
        battle.Enqueue(new CastSkillCommand(1, 1, "rez", TargetId: 2));
        Assert.Single(battle.Tick().OfType<CommandRejectedEvent>());

        battle.Enqueue(new DealDamageCommand(2, 1, 2, DamageType.True, 200, CanCrit: false));
        battle.Tick();
        Assert.Equal(LifeState.Reviving, battle.GetCharacter(2)!.LifeState);
        battle.Enqueue(new CastSkillCommand(3, 1, "rez", TargetId: 2));
        Assert.Single(battle.Tick().OfType<CommandRejectedEvent>());

        battle.Enqueue(new DealDamageCommand(4, 1, 3, DamageType.True, 200, CanCrit: false));
        battle.Tick();
        battle.Enqueue(new CastSkillCommand(5, 1, "rez", TargetId: 3));
        Assert.Single(battle.Tick().OfType<CommandRejectedEvent>());
    }

    [Fact]
    public void DamageSkill_StillSkipsCorpses()
    {
        var strike = new SkillDefinition(
            "strike",
            durationFrames: 1,
            cooldownFrames: 0,
            [
                new SkillDamageEffect(
                    0,
                    SkillTarget.ExplicitEnemy,
                    DamageType.True,
                    10,
                    CanCrit: false),
            ]);
        var battle = CreateBattle(strike);
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 2, Point(8, 2)));
        battle.Tick();
        battle.Enqueue(new DealDamageCommand(1, 1, 2, DamageType.True, 200, CanCrit: false));
        battle.Tick();
        battle.Enqueue(new CastSkillCommand(2, 1, "strike", TargetId: 2));

        Assert.Single(battle.Tick().OfType<CommandRejectedEvent>());
        Assert.Equal(LifeState.Dead, battle.GetCharacter(2)!.LifeState);
    }

    [Fact]
    public void ReviveSkill_RejectsCorpseNotMarkedReviveable()
    {
        var battle = CreateBattle(ReviveSkill(), canBeRevived: false);
        KillAlly(battle);
        battle.Enqueue(new CastSkillCommand(2, 1, "rez", TargetId: 2));

        Assert.Single(battle.Tick().OfType<CommandRejectedEvent>());
        Assert.Equal(LifeState.Dead, battle.GetCharacter(2)!.LifeState);
    }

    [Fact]
    public void CurrentDeadAlly_KeepsCorpseSoftLock()
    {
        var battle = CreateBattle(
            new SkillDefinition(
                "rez",
                durationFrames: 1,
                cooldownFrames: 0,
                [new SkillReviveEffect(0, SkillTarget.CurrentDeadAlly)]));
        KillAlly(battle);
        battle.Enqueue(new SetTargetCommand(2, 1, 2));
        battle.Enqueue(new CastSkillCommand(2, 1, "rez"));

        var events = battle.Tick();
        Assert.Single(events.OfType<TargetChangedEvent>());
        Assert.Single(events.OfType<RevivedEvent>());
    }

    [Fact]
    public void CircleCorpseRevive_HitsDeadAlliesOnly()
    {
        var circle = new SkillTarget(
            SkillTargetKind.CircleAtPoint,
            TargetRelation.Allies,
            Fix64.FromInt(4),
            CorpsesOnly: true);
        var battle = CreateBattle(
            new SkillDefinition(
                "mass_rez",
                durationFrames: 1,
                cooldownFrames: 0,
                [new SkillReviveEffect(0, circle)]));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(4, 2)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(16, 2)));
        battle.Tick();
        battle.Enqueue(new DealDamageCommand(1, 1, 2, DamageType.True, 200, CanCrit: false));
        battle.Enqueue(new DealDamageCommand(1, 1, 3, DamageType.True, 200, CanCrit: false));
        battle.Tick();
        battle.Enqueue(new CastSkillCommand(2, 1, "mass_rez", TargetPoint: Point(4, 2)));

        var revived = battle.Tick().OfType<RevivedEvent>().ToArray();
        Assert.Single(revived);
        Assert.Equal(2, revived[0].CharacterId);
        Assert.Equal(LifeState.Alive, battle.GetCharacter(2)!.LifeState);
        Assert.Equal(LifeState.Dead, battle.GetCharacter(3)!.LifeState);
    }

    [Fact]
    public void PreventReviveBuff_BlocksReviveWithoutDespawningPersistentCorpse()
    {
        var preventRevive = new BuffDefinition(
            "prevent_revive",
            durationFrames: 0,
            BuffStackingRule.Unique,
            [new PreventReviveBuffEffect()]);
        var battle = CreateBattle(
            ReviveSkill(),
            corpseDurationFrames: 1,
            buffDefinitions: [preventRevive]);
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(8, 2)));
        battle.Tick();
        battle.Enqueue(new ApplyBuffCommand(1, 1, 2, preventRevive.Id));
        battle.Tick();
        var buffId = Assert.Single(battle.GetCharacter(2)!.Buffs).InstanceId;
        battle.Enqueue(new DealDamageCommand(2, 1, 2, DamageType.True, 200, CanCrit: false));
        battle.Tick();
        battle.Enqueue(new CastSkillCommand(3, 1, "rez", TargetId: 2));

        Assert.Single(battle.Tick().OfType<CommandRejectedEvent>());
        Assert.Equal(LifeState.Dead, battle.GetCharacter(2)!.LifeState);

        battle.Enqueue(new RemoveBuffCommand(4, 2, buffId));
        battle.Tick();
        battle.Enqueue(new CastSkillCommand(5, 1, "rez", TargetId: 2));

        Assert.Single(battle.Tick().OfType<RevivedEvent>());
    }

    [Fact]
    public void PreventReviveBuff_DelaysLivesCountdownStandUpUntilBuffEnds()
    {
        // Review round-2 N2: the Lives revive path used to ignore
        // PreventRevive entirely and stand the character up on schedule.
        var preventRevive = new BuffDefinition(
            "seal",
            durationFrames: 6,
            BuffStackingRule.Unique,
            [new PreventReviveBuffEffect()]);
        var battle = CreateBattle(
            ReviveSkill(),
            lives: 1,
            reviveDelayFrames: 2,
            buffDefinitions: [preventRevive]);
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(8, 2)));
        battle.Tick();
        battle.Enqueue(new ApplyBuffCommand(1, 1, 2, preventRevive.Id));
        battle.Tick();
        battle.Enqueue(new DealDamageCommand(2, 1, 2, DamageType.True, 200, CanCrit: false));
        battle.Tick();
        Assert.Equal(LifeState.Reviving, battle.GetCharacter(2)!.LifeState);

        var revived = new List<long>();
        for (var frame = 3; frame <= 6; frame++)
        {
            revived.AddRange(battle.Tick()
                .OfType<RevivedEvent>()
                .Select(item => item.CharacterId));
            if (frame < 6)
            {
                Assert.Equal(LifeState.Reviving, battle.GetCharacter(2)!.LifeState);
            }
        }

        Assert.Empty(revived);

        var standUpEvents = battle.Tick();

        Assert.Single(standUpEvents.OfType<RevivedEvent>());
        Assert.Equal(LifeState.Alive, battle.GetCharacter(2)!.LifeState);
        Assert.Equal(0, battle.GetCharacter(2)!.Lives);
    }

    private static BattleInstance CreateBattle(
        SkillDefinition skill,
        int lives = 0,
        int reviveDelayFrames = 30,
        int corpseDurationFrames = 10,
        bool canBeRevived = true,
        IEnumerable<BuffDefinition>? buffDefinitions = null)
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
            lives: lives,
            skillIds: [skill.Id],
            canBeRevived: canBeRevived);
        var config = new BattleConfig(
            values.Keys,
            TestBattleFactory.Bindings,
            [template],
            [skill],
            buffDefinitions,
            corpseDurationFrames: corpseDurationFrames,
            reviveDelayFrames: reviveDelayFrames);
        return TestBattleFactory.CreateBattle(config);
    }

    private static void KillAlly(BattleInstance battle)
    {
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(8, 2)));
        battle.Tick();
        battle.Enqueue(new DealDamageCommand(1, 1, 2, DamageType.True, 200, CanCrit: false));
        battle.Tick();
    }

    private static SkillDefinition ReviveSkill() =>
        new(
            "rez",
            durationFrames: 1,
            cooldownFrames: 0,
            [new SkillReviveEffect(0, SkillTarget.ExplicitDeadAlly)]);

    private static FixVector2 Point(long x, long y) =>
        new(Fix64.FromInt(x), Fix64.FromInt(y));
}
