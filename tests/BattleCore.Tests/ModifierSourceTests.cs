using BattleCore.Attributes;
using BattleCore.Math;
using BattleCore.Simulation.Commands;
using BattleCore.Simulation.Events;

namespace BattleCore.Tests;

public sealed class ModifierSourceTests
{
    [Fact]
    public void Spawn_InjectsModifierSourceIntoAttributes()
    {
        var battle = TestBattleFactory.CreateBattle();
        battle.Enqueue(new SpawnCharacterCommand(
            0,
            "hero",
            1,
            Point(2, 2),
            ModifierSources:
            [
                new ModifierSource(
                    "weapon",
                    [new ModifierContribution("attack", ModifierKind.Flat, 20)]),
            ]));

        var events = battle.Tick();

        Assert.Single(events.OfType<ModifierSourceAppliedEvent>());
        Assert.Equal(["weapon"], battle.GetCharacter(1)!.ModifierSourceIds);
        Assert.Equal(120, battle.GetAttribute(1, "attack"));
    }

    [Fact]
    public void ApplyThenRemove_SwapsWeaponWithoutTouchingOtherSources()
    {
        var battle = TestBattleFactory.CreateBattle();
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Tick();
        battle.Enqueue(new ApplyModifierSourceCommand(
            1,
            1,
            "weapon",
            [new ModifierContribution("attack", ModifierKind.Flat, 20)]));
        battle.Enqueue(new ApplyModifierSourceCommand(
            1,
            1,
            "charm",
            [new ModifierContribution("attack", ModifierKind.Flat, 5)]));
        battle.Tick();

        Assert.Equal(125, battle.GetAttribute(1, "attack"));

        battle.Enqueue(new ApplyModifierSourceCommand(
            2,
            1,
            "weapon",
            [new ModifierContribution("attack", ModifierKind.Flat, 40)]));
        var replaced = battle.Tick();

        Assert.Single(replaced.OfType<ModifierSourceRemovedEvent>());
        Assert.Single(replaced.OfType<ModifierSourceAppliedEvent>());
        Assert.Equal(145, battle.GetAttribute(1, "attack"));

        battle.Enqueue(new RemoveModifierSourceCommand(3, 1, "weapon"));
        var removed = battle.Tick();

        Assert.Single(removed.OfType<ModifierSourceRemovedEvent>());
        Assert.Equal(["charm"], battle.GetCharacter(1)!.ModifierSourceIds);
        Assert.Equal(105, battle.GetAttribute(1, "attack"));
    }

    [Fact]
    public void Apply_RejectsUnknownAttributeAndMissingRemove()
    {
        var battle = TestBattleFactory.CreateBattle();
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Tick();
        battle.Enqueue(new ApplyModifierSourceCommand(
            1,
            1,
            "weapon",
            [new ModifierContribution("missing", ModifierKind.Flat, 1)]));
        Assert.Single(battle.Tick().OfType<CommandRejectedEvent>());

        battle.Enqueue(new RemoveModifierSourceCommand(2, 1, "weapon"));
        Assert.Single(battle.Tick().OfType<CommandRejectedEvent>());
        Assert.Empty(battle.GetCharacter(1)!.ModifierSourceIds);
    }

    [Fact]
    public void MaxHpSource_DoesNotClampCurrentHp()
    {
        var battle = TestBattleFactory.CreateBattle();
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Tick();
        battle.Enqueue(new ApplyModifierSourceCommand(
            1,
            1,
            "vest",
            [new ModifierContribution(
                TestBattleFactory.Bindings.MaxHp,
                ModifierKind.Flat,
                50)]));
        battle.Tick();
        Assert.Equal(100, battle.GetCharacter(1)!.CurrentHp);
        Assert.Equal(150, battle.GetCharacter(1)!.MaxHp);

        battle.Enqueue(new RemoveModifierSourceCommand(2, 1, "vest"));
        battle.Tick();
        Assert.Equal(100, battle.GetCharacter(1)!.CurrentHp);
        Assert.Equal(100, battle.GetCharacter(1)!.MaxHp);
    }

    private static FixVector2 Point(long x, long y) =>
        new(Fix64.FromInt(x), Fix64.FromInt(y));
}
