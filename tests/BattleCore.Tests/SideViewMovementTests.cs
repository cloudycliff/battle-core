using BattleCore.Configuration;
using BattleCore.Math;
using BattleCore.Simulation;
using BattleCore.Simulation.Commands;
using BattleCore.Simulation.Events;
using BattleCore.Skills;

namespace BattleCore.Tests;

public sealed class SideViewMovementTests
{
    [Fact]
    public void GroundedMovement_UsesHorizontalInputOnly()
    {
        var battle = CreateBattle([Platform(0, 0, 20, 2)]);
        Spawn(battle, Point(2, Fix64.FromRatio(5, 2)));
        battle.Enqueue(new MoveDirectionCommand(1, 1, Point(1, 1)));

        var events = battle.Tick();

        var moved = Assert.Single(events.OfType<CharacterMovedEvent>());
        Assert.Equal(Fix64.FromRatio(21, 10), moved.To.X);
        Assert.Equal(Fix64.FromRatio(5, 2), moved.To.Y);
        Assert.True(battle.GetCharacter(1)!.Grounded);
    }

    [Fact]
    public void AirborneCharacter_FallsAndLandsOnPlatformTop()
    {
        var battle = CreateBattle([Platform(0, 0, 20, 2)]);
        Spawn(battle, Point(2, 8));

        for (var frame = 1; frame <= 90; frame++)
        {
            battle.Tick();
        }

        var character = battle.GetCharacter(1)!;
        Assert.True(character.Grounded);
        Assert.Equal(Fix64.FromRatio(5, 2), character.Position.Y);
        Assert.Equal(Fix64.Zero, character.VerticalVelocity);
    }

    [Fact]
    public void JumpCommand_LaunchesOnlyFromGroundAndReturnsToGround()
    {
        var battle = CreateBattle([Platform(0, 0, 20, 2)]);
        Spawn(battle, Point(2, Fix64.FromRatio(5, 2)));
        battle.Enqueue(new JumpCommand(1, 1));

        var launch = battle.Tick();

        Assert.Empty(launch.OfType<CommandRejectedEvent>());
        Assert.True(battle.GetCharacter(1)!.Position.Y > Fix64.FromRatio(5, 2));
        Assert.False(battle.GetCharacter(1)!.Grounded);

        battle.Enqueue(new JumpCommand(2, 1));
        Assert.Single(battle.Tick().OfType<CommandRejectedEvent>());

        for (var frame = 3; frame <= 90; frame++)
        {
            battle.Tick();
        }

        Assert.True(battle.GetCharacter(1)!.Grounded);
        Assert.Equal(Fix64.FromRatio(5, 2), battle.GetCharacter(1)!.Position.Y);
    }

    [Fact]
    public void Jump_DoesNotPassThroughCeiling()
    {
        var battle = CreateBattle(
        [
            Platform(0, 0, 20, 2),
            Platform(0, 4, 20, 5),
        ]);
        Spawn(battle, Point(2, Fix64.FromRatio(5, 2)));
        battle.Enqueue(new JumpCommand(1, 1));

        var maximumY = Fix64.Zero;
        for (var frame = 1; frame <= 90; frame++)
        {
            battle.Tick();
            maximumY = Fix64.Max(maximumY, battle.GetCharacter(1)!.Position.Y);
        }

        Assert.True(maximumY <= Fix64.FromRatio(7, 2));
        Assert.True(battle.GetCharacter(1)!.Grounded);
    }

    [Fact]
    public void Gravity_ContinuesWhileSkillLocksControlledMovement()
    {
        var lockedSkill = new SkillDefinition(
            "locked",
            durationFrames: 60,
            cooldownFrames: 0,
            effects: []);
        var battle = CreateBattle([Platform(0, 0, 20, 2)], lockedSkill);
        Spawn(battle, Point(2, 8));
        battle.Enqueue(new CastSkillCommand(1, 1, lockedSkill.Id));

        battle.Tick();

        Assert.Equal(lockedSkill.Id, battle.GetCharacter(1)!.ActiveSkillId);
        Assert.True(battle.GetCharacter(1)!.Position.Y < Fix64.FromInt(8));
    }

    [Fact]
    public void Landing_DoesNotCancelHorizontalMoveTo()
    {
        var battle = CreateBattle([Platform(0, 0, 20, 2)]);
        Spawn(battle, Point(2, 8));
        battle.Enqueue(new MoveToCommand(1, 1, Point(5, 2)));

        for (var frame = 1; frame <= 40; frame++)
        {
            battle.Tick();
        }

        var character = battle.GetCharacter(1)!;
        Assert.True(character.Grounded);
        Assert.Equal(Fix64.FromInt(5), character.Position.X);
        Assert.Equal(Fix64.FromRatio(5, 2), character.Position.Y);
    }

    [Fact]
    public void TopDownBattle_RejectsJumpWithoutChangingPosition()
    {
        var battle = TestBattleFactory.CreateBattle();
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Tick();
        battle.Enqueue(new JumpCommand(1, 1));

        var events = battle.Tick();

        Assert.Single(events.OfType<CommandRejectedEvent>());
        Assert.Equal(Point(2, 2), battle.GetCharacter(1)!.Position);
    }

    private static BattleInstance CreateBattle(
        IReadOnlyList<FixAabb> terrain,
        SkillDefinition? skill = null)
    {
        var baseConfig = TestBattleFactory.CreateConfig();
        var template = baseConfig.CharacterTemplates["hero"];
        var config = skill is null
            ? baseConfig
            : new BattleConfig(
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
            terrain,
            spatialMode: SpatialMode.SideView,
            sideView: new SideViewSettings(
                Gravity: Fix64.FromInt(30),
                MaximumFallSpeed: Fix64.FromInt(20),
                JumpSpeed: Fix64.FromInt(10)));
        return new BattleInstance(
            config,
            map,
            seed: 123,
            maxFrames: 1_000);
    }

    private static void Spawn(BattleInstance battle, FixVector2 position)
    {
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, position));
        Assert.Single(battle.Tick().OfType<CharacterSpawnedEvent>());
    }

    private static FixAabb Platform(long minX, long minY, long maxX, long maxY) =>
        new(Point(minX, minY), Point(maxX, maxY));

    private static FixVector2 Point(long x, long y) =>
        new(Fix64.FromInt(x), Fix64.FromInt(y));

    private static FixVector2 Point(long x, Fix64 y) =>
        new(Fix64.FromInt(x), y);
}
