using BattleCore.Attributes;
using BattleCore.Combat;
using BattleCore.Math;
using BattleCore.Simulation.Commands;
using BattleCore.Simulation.Events;

namespace BattleCore.Tests;

public sealed class CombatPipelineTests
{
    [Fact]
    public void Damage_AppliesMitigationThenHitsHp()
    {
        var battle = SpawnPair();
        battle.Enqueue(new DealDamageCommand(
            1,
            SourceId: 1,
            TargetId: 2,
            DamageType.Physical,
            Flat: 100,
            CanCrit: false));

        var damage = Assert.Single(battle.Tick().OfType<DamageEvent>());

        Assert.Equal(50, damage.Amount);
        Assert.False(damage.WasCrit);
        Assert.Equal(50, battle.GetCharacter(2)!.CurrentHp);
    }

    [Fact]
    public void TrueDamage_SkipsDefense()
    {
        var battle = SpawnPair();
        battle.Enqueue(new DealDamageCommand(
            1,
            1,
            2,
            DamageType.True,
            40,
            CanCrit: false));

        Assert.Equal(40, Assert.Single(battle.Tick().OfType<DamageEvent>()).Amount);
        Assert.Equal(60, battle.GetCharacter(2)!.CurrentHp);
    }

    [Fact]
    public void Heal_CanExceedMaxHpAndSkipsDefense()
    {
        var battle = SpawnPair();
        battle.Enqueue(new HealCommand(1, 1, 1, Flat: 50, CanCrit: false));

        var heal = Assert.Single(battle.Tick().OfType<HealEvent>());

        Assert.Equal(50, heal.Amount);
        Assert.Equal(150, battle.GetCharacter(1)!.CurrentHp);
    }

    [Fact]
    public void Shield_AbsorbsBeforeHpAndExpiresByInstanceOrder()
    {
        var battle = SpawnPair();
        battle.Enqueue(new GrantShieldCommand(1, 2, 30, DurationFrames: 10));
        battle.Enqueue(new DealDamageCommand(1, 1, 2, DamageType.True, 40, CanCrit: false));

        var events = battle.Tick();
        var damage = Assert.Single(events.OfType<DamageEvent>());

        Assert.Equal(30, damage.ShieldAbsorbed);
        Assert.Equal(10, damage.Amount);
        Assert.Equal(90, battle.GetCharacter(2)!.CurrentHp);
        Assert.Equal(0, battle.GetCharacter(2)!.Shield);
    }

    [Fact]
    public void LethalDamage_LeavesCorpseThenDespawns()
    {
        var battle = TestBattleFactory.CreateBattle(
            TestBattleFactory.CreateConfig(corpseDurationFrames: 1));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 2, Point(8, 2)));
        battle.Tick();
        battle.Enqueue(new DealDamageCommand(1, 1, 2, DamageType.True, 200, CanCrit: false));

        var deathEvents = battle.Tick();
        Assert.Single(deathEvents.OfType<DiedEvent>());
        Assert.Single(deathEvents.OfType<FactionWipedEvent>());
        Assert.Equal(LifeState.Dead, battle.GetCharacter(2)!.LifeState);

        var despawnEvents = battle.Tick();
        Assert.Single(despawnEvents.OfType<CorpseDespawnedEvent>());
        Assert.Null(battle.GetCharacter(2));
    }

    [Fact]
    public void Lives_StartReviveCountdownInsteadOfCorpse()
    {
        var battle = TestBattleFactory.CreateBattle(
            TestBattleFactory.CreateConfig(lives: 1, reviveDelayFrames: 1));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 2, Point(8, 2)));
        battle.Tick();
        battle.Enqueue(new DealDamageCommand(1, 1, 2, DamageType.True, 200, CanCrit: false));

        var downed = battle.Tick();
        Assert.Single(downed.OfType<ReviveStartEvent>());
        Assert.Equal(LifeState.Reviving, battle.GetCharacter(2)!.LifeState);
        Assert.Equal(0, battle.GetCharacter(2)!.Lives);

        var revived = battle.Tick();
        Assert.Single(revived.OfType<RevivedEvent>());
        Assert.Equal(LifeState.Alive, battle.GetCharacter(2)!.LifeState);
        Assert.Equal(100, battle.GetCharacter(2)!.CurrentHp);
    }

    [Fact]
    public void ReviveCommand_RaisesDeadAllyInCorpseWindow()
    {
        var battle = TestBattleFactory.CreateBattle(
            TestBattleFactory.CreateConfig(
                corpseDurationFrames: 10,
                canBeRevived: true));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(8, 2)));
        battle.Tick();
        battle.Enqueue(new DealDamageCommand(1, 1, 2, DamageType.True, 200, CanCrit: false));
        battle.Tick();
        battle.Enqueue(new ReviveCommand(2, 1, 2));

        Assert.Single(battle.Tick().OfType<RevivedEvent>());
        Assert.Equal(LifeState.Alive, battle.GetCharacter(2)!.LifeState);
    }

    [Fact]
    public void Heal_UsesSharedBaseFormula()
    {
        var battle = SpawnPair();
        battle.Enqueue(new HealCommand(
            1,
            1,
            2,
            Flat: 10,
            AttributeId: "attack",
            Ratio: 5_000,
            CanCrit: false));

        Assert.Equal(60, Assert.Single(battle.Tick().OfType<HealEvent>()).Amount);
        Assert.Equal(160, battle.GetCharacter(2)!.CurrentHp);
    }

    private static Simulation.BattleInstance SpawnPair()
    {
        var values = TestBattleFactory.Bindings.AllIds()
            .Concat(["attack"])
            .ToDictionary(id => id, _ => 0L, StringComparer.Ordinal);
        values[TestBattleFactory.Bindings.MaxHp] = 100;
        values[TestBattleFactory.Bindings.PhysicalDefense] = 100;
        values[TestBattleFactory.Bindings.CritDamage] = 15_000;
        values[TestBattleFactory.Bindings.MoveSpeed] = 3;
        values["attack"] = 100;

        var template = new Configuration.CharacterTemplate(
            "hero",
            Fix64.FromRatio(1, 2),
            values);
        var config = new Configuration.BattleConfig(
            values.Keys,
            TestBattleFactory.Bindings,
            [template]);
        var battle = TestBattleFactory.CreateBattle(config);
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 2, Point(8, 2)));
        battle.Tick();
        return battle;
    }

    private static FixVector2 Point(long x, long y) =>
        new(Fix64.FromInt(x), Fix64.FromInt(y));
}
