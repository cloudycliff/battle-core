using BattleCore.Attributes;
using BattleCore.Buffs;
using BattleCore.Combat;
using BattleCore.Configuration;
using BattleCore.Math;
using BattleCore.Pickups;
using BattleCore.Simulation.Commands;
using BattleCore.Simulation.Events;

namespace BattleCore.Tests;

public sealed class PickupTests
{
    [Fact]
    public void Constructor_RejectsUnknownPickupAttribute()
    {
        var values = BaseAttributes();
        var template = new CharacterTemplate("hero", Fix64.FromRatio(1, 2), values);
        var pickup = new PickupDefinition(
            "bad",
            Fix64.FromRatio(1, 2),
            PickupKind.Instant,
            [new PickupAttributeEffect("nope", ModifierKind.Flat, 1)]);

        var error = Assert.Throws<ConfigurationException>(() =>
            new BattleConfig(
                values.Keys,
                TestBattleFactory.Bindings,
                [template],
                pickupDefinitions: [pickup]));

        Assert.Contains("nope", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InstantHeal_AppliesOnOverlapAndRemovesPickup()
    {
        var battle = CreateBattle(
            new PickupDefinition(
                "potion",
                Fix64.FromRatio(1, 2),
                PickupKind.Instant,
                [new PickupHealEffect(25)]));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2), InitialHp: 40));
        battle.Enqueue(new SpawnPickupCommand(0, "potion", Point(2, 2)));

        var events = battle.Tick();

        Assert.Single(events.OfType<PickupSpawnedEvent>());
        var picked = Assert.Single(events.OfType<PickedUpEvent>());
        Assert.Equal(PickupKind.Instant, picked.Kind);
        Assert.Equal(1, picked.CharacterId);
        Assert.Equal(25, Assert.Single(events.OfType<HealEvent>()).Amount);
        Assert.Equal(65, battle.GetCharacter(1)!.CurrentHp);
        Assert.Empty(battle.GetPickups());
    }

    [Fact]
    public void Stash_EmitsPickedUpWithoutCombatEffect()
    {
        var battle = CreateBattle(
            new PickupDefinition(
                "gem",
                Fix64.FromRatio(1, 2),
                PickupKind.Stash));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2), InitialHp: 40));
        battle.Enqueue(new SpawnPickupCommand(0, "gem", Point(2, 2)));

        var events = battle.Tick();

        Assert.Equal(PickupKind.Stash, Assert.Single(events.OfType<PickedUpEvent>()).Kind);
        Assert.Empty(events.OfType<HealEvent>());
        Assert.Equal(40, battle.GetCharacter(1)!.CurrentHp);
        Assert.Empty(battle.GetPickups());
    }

    [Fact]
    public void SameFrame_PickupIdOrderAndOnePerCharacter()
    {
        var potion = new PickupDefinition(
            "potion",
            Fix64.FromInt(2),
            PickupKind.Instant,
            [new PickupHealEffect(10)]);
        var battle = CreateBattle(potion);
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2), InitialHp: 40));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2), InitialHp: 40));
        battle.Enqueue(new SpawnPickupCommand(0, "potion", Point(2, 2)));
        battle.Enqueue(new SpawnPickupCommand(0, "potion", Point(2, 2)));

        var picked = battle.Tick().OfType<PickedUpEvent>().OrderBy(item => item.PickupId).ToArray();

        Assert.Equal(2, picked.Length);
        Assert.Equal(1, picked[0].CharacterId);
        Assert.Equal(2, picked[1].CharacterId);
        Assert.Empty(battle.GetPickups());
        Assert.Equal(50, battle.GetCharacter(1)!.CurrentHp);
        Assert.Equal(50, battle.GetCharacter(2)!.CurrentHp);
    }

    [Fact]
    public void Character_PicksOnlyOnePickupPerFrame()
    {
        var potion = new PickupDefinition(
            "potion",
            Fix64.FromInt(2),
            PickupKind.Instant,
            [new PickupHealEffect(10)]);
        var battle = CreateBattle(potion);
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2), InitialHp: 40));
        battle.Enqueue(new SpawnPickupCommand(0, "potion", Point(2, 2)));
        battle.Enqueue(new SpawnPickupCommand(0, "potion", Point(2, 2)));

        var events = battle.Tick();

        Assert.Single(events.OfType<PickedUpEvent>());
        Assert.Equal(50, battle.GetCharacter(1)!.CurrentHp);
        Assert.Single(battle.GetPickups());
    }

    [Fact]
    public void InstantLivesAndAttribute_ApplyToPicker()
    {
        var loot = new PickupDefinition(
            "heart",
            Fix64.FromRatio(1, 2),
            PickupKind.Instant,
            [
                new PickupGrantLivesEffect(2),
                new PickupAttributeEffect("move_speed", ModifierKind.Flat, 3),
            ]);
        var battle = CreateBattle(loot);
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Enqueue(new SpawnPickupCommand(0, "heart", Point(2, 2)));
        battle.Tick();
        battle.Enqueue(new MoveDirectionCommand(1, 1, Point(1, 0)));

        var moved = Assert.Single(battle.Tick().OfType<CharacterMovedEvent>());

        Assert.Equal(2, battle.GetCharacter(1)!.Lives);
        Assert.Equal(Fix64.FromRatio(22, 10), moved.To.X);
    }

    [Fact]
    public void DeadCharacter_DoesNotPickUp()
    {
        var potion = new PickupDefinition(
            "potion",
            Fix64.FromRatio(1, 2),
            PickupKind.Instant,
            [new PickupHealEffect(50)]);
        var battle = CreateBattle(potion);
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 2, Point(8, 2), InitialHp: 1));
        battle.Tick();
        battle.Enqueue(new DealDamageCommand(1, 1, 2, DamageType.True, 100, CanCrit: false));
        battle.Enqueue(new SpawnPickupCommand(1, "potion", Point(8, 2)));

        var events = battle.Tick();

        Assert.DoesNotContain(events.OfType<PickedUpEvent>(), item => item.CharacterId == 2);
        Assert.Single(battle.GetPickups());
    }

    private static BattleCore.Simulation.BattleInstance CreateBattle(PickupDefinition pickup)
    {
        var values = BaseAttributes();
        var template = new CharacterTemplate("hero", Fix64.FromRatio(1, 2), values);
        var config = new BattleConfig(
            values.Keys,
            TestBattleFactory.Bindings,
            [template],
            pickupDefinitions: [pickup]);
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
