using BattleCore.Combat;
using BattleCore.Configuration;
using BattleCore.Math;
using BattleCore.Simulation;
using BattleCore.Simulation.Commands;
using BattleCore.Simulation.Events;
using BattleCore.Skills;

namespace BattleCore.Tests;

public sealed class SideViewAttackBoxTests
{
    [Fact]
    public void Box_HitsEnemyInFrontAndIgnoresBehind()
    {
        var battle = CreateBattle(Cleave());
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(5, FloorY)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 2, Point(8, FloorY)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 2, Point(2, FloorY)));
        battle.Tick();

        var hits = Cast(battle, 1);

        Assert.Equal(2, Assert.Single(hits).TargetId);
        Assert.Equal(90, battle.GetCharacter(2)!.CurrentHp);
        Assert.Equal(100, battle.GetCharacter(3)!.CurrentHp);
    }

    [Fact]
    public void Box_UsesCurrentFacingWhenCastHasNoDirection()
    {
        var battle = CreateBattle(Cleave());
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(8, FloorY)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 2, Point(5, FloorY)));
        battle.Tick();
        battle.Enqueue(new SetFacingCommand(1, 1, new Facing(512)));
        battle.Tick();

        var hits = Cast(battle, 2, direction: null);

        Assert.Equal(2, Assert.Single(hits).TargetId);
        Assert.Equal(90, battle.GetCharacter(2)!.CurrentHp);
    }

    [Fact]
    public void Box_IgnoresEnemyAboveTheSwipe()
    {
        var battle = CreateBattle(Cleave(height: Fix64.FromInt(2)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(4, FloorY)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 2, Point(7, 6)));
        battle.Tick();

        Assert.Empty(Cast(battle, 1));
        Assert.Equal(100, battle.GetCharacter(2)!.CurrentHp);
    }

    [Fact]
    public void Config_RejectsBoxWithoutHeight()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            Cleave(height: Fix64.Zero));

        Assert.Contains("height", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TopDownBox_ExtendsAlongFacingAxis()
    {
        var battle = CreateBattle(Cleave(), sideView: false);
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(5, 5)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 2, Point(5, 8)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 2, Point(8, 5)));
        battle.Tick();
        battle.Enqueue(new SetFacingCommand(1, 1, new Facing(256)));
        battle.Tick();

        var hits = Cast(battle, 2, direction: null);

        Assert.Equal(2, Assert.Single(hits).TargetId);
        Assert.Equal(100, battle.GetCharacter(3)!.CurrentHp);
    }

    private static SkillDefinition Cleave(Fix64? height = null) =>
        new(
            "cleave",
            1,
            0,
            [
                new SkillDamageEffect(
                    0,
                    new SkillTarget(
                        SkillTargetKind.BoxAtCaster,
                        TargetRelation.Enemies,
                        Radius: Fix64.FromInt(4),
                        Height: height ?? Fix64.FromInt(2)),
                    DamageType.True,
                    10,
                    CanCrit: false),
            ]);

    private static BattleInstance CreateBattle(
        SkillDefinition skill,
        bool sideView = true)
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
            [skill]);
        var bounds = new FixAabb(Point(0, 0), Point(20, 10));
        var map = sideView
            ? new MapConfig(
                bounds,
                spatialMode: SpatialMode.SideView,
                sideView: new SideViewSettings(
                    Gravity: Fix64.FromInt(30),
                    MaximumFallSpeed: Fix64.FromInt(20),
                    JumpSpeed: Fix64.FromInt(10)))
            : new MapConfig(bounds);
        return new BattleInstance(config, map, seed: 123, maxFrames: 1_000);
    }

    private static SkillHitEvent[] Cast(
        BattleInstance battle,
        int frame,
        FixVector2? direction = null)
    {
        battle.Enqueue(new CastSkillCommand(frame, 1, "cleave", Direction: direction));
        return battle.Tick().OfType<SkillHitEvent>().ToArray();
    }

    private static readonly Fix64 FloorY = Fix64.FromRatio(1, 2);

    private static FixVector2 Point(long x, long y) =>
        new(Fix64.FromInt(x), Fix64.FromInt(y));

    private static FixVector2 Point(long x, Fix64 y) =>
        new(Fix64.FromInt(x), y);
}
