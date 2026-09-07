using BattleCore.Combat;
using BattleCore.Configuration;
using BattleCore.Math;
using BattleCore.Simulation;
using BattleCore.Simulation.Commands;
using BattleCore.Simulation.Events;
using BattleCore.Skills;

namespace BattleCore.Tests;

public sealed class SideViewCombatDisplacementTests
{
    [Fact]
    public void HorizontalKnockback_SlidesOnTheFloor()
    {
        var battle = CreateBattle(Knockback(alongCast: true));
        SpawnPair(battle);
        battle.Enqueue(new CastSkillCommand(
            1,
            1,
            "blast",
            TargetId: 2,
            Direction: Point(1, 0)));

        battle.Tick();

        var target = battle.GetCharacter(2)!;
        Assert.True(target.Grounded);
        Assert.Equal(Fix64.FromRatio(5, 2), target.Position.Y);
        Assert.Equal(Fix64.FromInt(12), target.Position.X);
        Assert.Equal(Fix64.Zero, target.VerticalVelocity);
    }

    [Fact]
    public void UpwardKnockback_LaunchesThenLands()
    {
        var battle = CreateBattle(Knockback(alongCast: true, distance: 2));
        SpawnPair(battle);
        battle.Enqueue(new CastSkillCommand(
            1,
            1,
            "blast",
            TargetId: 2,
            Direction: Point(0, 1)));

        var launch = battle.Tick();

        Assert.Contains(launch.OfType<CharacterMovedEvent>(), e => e.CharacterId == 2);
        var airborne = battle.GetCharacter(2)!;
        Assert.False(airborne.Grounded);
        Assert.True(airborne.Position.Y > Fix64.FromRatio(5, 2));
        Assert.True(airborne.VerticalVelocity > Fix64.Zero);

        for (var frame = 2; frame <= 90; frame++)
        {
            battle.Tick();
        }

        var landed = battle.GetCharacter(2)!;
        Assert.True(landed.Grounded);
        Assert.Equal(Fix64.FromRatio(5, 2), landed.Position.Y);
        Assert.Equal(Fix64.Zero, landed.VerticalVelocity);
    }

    [Fact]
    public void UpwardKnockback_DoesNotPassThroughCeiling()
    {
        var battle = CreateBattle(
            Knockback(alongCast: true, distance: 8),
            [
                Platform(0, 0, 20, 2),
                Platform(0, 4, 20, 5),
            ]);
        SpawnPair(battle);
        battle.Enqueue(new CastSkillCommand(
            1,
            1,
            "blast",
            TargetId: 2,
            Direction: Point(0, 1)));

        var maximumY = Fix64.Zero;
        for (var frame = 1; frame <= 90; frame++)
        {
            battle.Tick();
            maximumY = Fix64.Max(maximumY, battle.GetCharacter(2)!.Position.Y);
        }

        Assert.True(maximumY <= Fix64.FromRatio(7, 2));
        Assert.True(battle.GetCharacter(2)!.Grounded);
    }

    [Fact]
    public void HorizontalDash_StillCoversConfiguredDistance()
    {
        var dash = new SkillDefinition(
            "dash",
            1,
            0,
            [new SkillDashEffect(0, Fix64.FromInt(6))]);
        var battle = CreateBattle(dash);
        battle.Enqueue(new SpawnCharacterCommand(
            0,
            "hero",
            1,
            Point(2, Fix64.FromRatio(5, 2))));
        battle.Tick();
        battle.Enqueue(new CastSkillCommand(1, 1, "dash", Direction: Point(1, 0)));

        battle.Tick();

        var character = battle.GetCharacter(1)!;
        Assert.True(character.Grounded);
        Assert.Equal(Fix64.FromInt(8), character.Position.X);
        Assert.Equal(Fix64.FromRatio(5, 2), character.Position.Y);
    }

    private static SkillDefinition Knockback(bool alongCast, long distance = 4) =>
        new(
            "blast",
            1,
            0,
            [
                new SkillKnockbackEffect(
                    0,
                    SkillTarget.ExplicitEnemy,
                    Fix64.FromInt(distance),
                    AlongCastDirection: alongCast),
            ]);

    private static BattleInstance CreateBattle(
        SkillDefinition skill,
        IReadOnlyList<FixAabb>? terrain = null)
    {
        var baseConfig = TestBattleFactory.CreateConfig();
        var template = baseConfig.CharacterTemplates["hero"];
        var config = new BattleConfig(
            baseConfig.AttributeIds,
            baseConfig.Bindings,
            [
                new CharacterTemplate(
                    template.Id,
                    template.Radius,
                    template.BaseAttributes,
                    template.Targetable,
                    template.Damageable,
                    template.BlocksMovement,
                    template.Lives,
                    [skill.Id],
                    template.BrainId,
                    template.CanBeRevived,
                    template.BaseResources),
            ],
            [skill]);
        var map = new MapConfig(
            new FixAabb(Point(0, 0), Point(20, 20)),
            terrain ?? [Platform(0, 0, 20, 2)],
            spatialMode: SpatialMode.SideView,
            sideView: new SideViewSettings(
                Gravity: Fix64.FromInt(30),
                MaximumFallSpeed: Fix64.FromInt(20),
                JumpSpeed: Fix64.FromInt(10)));
        return new BattleInstance(config, map, seed: 123, maxFrames: 1_000);
    }

    private static void SpawnPair(BattleInstance battle)
    {
        battle.Enqueue(new SpawnCharacterCommand(
            0,
            "hero",
            1,
            Point(2, Fix64.FromRatio(5, 2))));
        battle.Enqueue(new SpawnCharacterCommand(
            0,
            "hero",
            2,
            Point(8, Fix64.FromRatio(5, 2))));
        battle.Tick();
    }

    private static FixAabb Platform(long minX, long minY, long maxX, long maxY) =>
        new(Point(minX, minY), Point(maxX, maxY));

    private static FixVector2 Point(long x, long y) =>
        new(Fix64.FromInt(x), Fix64.FromInt(y));

    private static FixVector2 Point(long x, Fix64 y) =>
        new(Fix64.FromInt(x), y);
}
