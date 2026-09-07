using BattleCore.Math;
using BattleCore.Simulation.Commands;
using BattleCore.Simulation.Events;

namespace BattleCore.Tests;

public sealed class BattleInstanceTests
{
    [Fact]
    public void Tick_SpawnsThenMovesCharacterAtThirtyHertz()
    {
        var battle = TestBattleFactory.CreateBattle();
        battle.Enqueue(new SpawnCharacterCommand(
            0,
            "hero",
            1,
            Point(2, 2),
            PlayerControlled: true));

        var spawnEvents = battle.Tick();
        battle.Enqueue(new MoveDirectionCommand(1, 1, Point(1, 0)));
        var moveEvents = battle.Tick();

        Assert.Single(spawnEvents.OfType<CharacterSpawnedEvent>());
        var moved = Assert.Single(moveEvents.OfType<CharacterMovedEvent>());
        Assert.Equal(Fix64.FromRatio(21, 10), moved.To.X);
        Assert.Equal(Fix64.FromInt(2), moved.To.Y);
        Assert.Equal(2, battle.CurrentFrame);
    }

    [Fact]
    public void Movement_AllowsAlliesToOverlapButBlocksEnemies()
    {
        var allies = TestBattleFactory.CreateBattle();
        allies.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        allies.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(3, 2)));
        allies.Tick();
        allies.Enqueue(new MoveDirectionCommand(1, 1, Point(1, 0)));

        Assert.Single(allies.Tick().OfType<CharacterMovedEvent>());

        var enemies = TestBattleFactory.CreateBattle();
        enemies.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        enemies.Enqueue(new SpawnCharacterCommand(0, "hero", 2, Point(3, 2)));
        enemies.Tick();
        enemies.Enqueue(new MoveDirectionCommand(1, 1, Point(1, 0)));

        Assert.Empty(enemies.Tick().OfType<CharacterMovedEvent>());
    }

    [Fact]
    public void MoveTo_CancelsAfterBeingBlocked()
    {
        var blocker = new FixAabb(Point(3, 0), Point(4, 5));
        var battle = TestBattleFactory.CreateBattle(terrain: [blocker]);
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Tick();
        battle.Enqueue(new MoveToCommand(1, 1, Point(10, 2)));

        for (var frame = 1; frame <= 6; frame++)
        {
            battle.Tick();
        }

        var positionAfterBlock = battle.GetCharacter(1)!.Position;
        var nextEvents = battle.Tick();

        Assert.Equal(positionAfterBlock, battle.GetCharacter(1)!.Position);
        Assert.Empty(nextEvents.OfType<CharacterMovedEvent>());
    }

    [Fact]
    public void MoveTo_ContinuesAcrossFramesUntilDestination()
    {
        var battle = TestBattleFactory.CreateBattle();
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Tick();
        battle.Enqueue(new MoveToCommand(1, 1, Point(3, 2)));

        var movedFrames = 0;
        for (var frame = 1; frame <= 12; frame++)
        {
            movedFrames += battle.Tick().OfType<CharacterMovedEvent>().Count();
        }

        Assert.True(movedFrames > 1);
        Assert.Equal(Point(3, 2), battle.GetCharacter(1)!.Position);
        Assert.Empty(battle.Tick().OfType<CharacterMovedEvent>());
    }

    [Fact]
    public void NonBlockingUnit_IgnoresEnemiesButNotTerrain()
    {
        // Review round-2 N3: blocksMovement=false used to skip terrain too.
        var wall = new FixAabb(Point(3, 0), Point(4, 5));
        var walled = TestBattleFactory.CreateBattle(
            TestBattleFactory.CreateConfig(blocksMovement: false),
            terrain: [wall]);
        walled.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        walled.Tick();
        walled.Enqueue(new MoveDirectionCommand(1, 1, Point(1, 0)));
        for (var frame = 1; frame <= 30; frame++)
        {
            walled.Tick();
        }

        var stoppedX = walled.GetCharacter(1)!.Position.X;
        Assert.True(stoppedX <= Fix64.FromRatio(5, 2), $"clipped into wall at x={stoppedX}");
        Assert.True(stoppedX > Fix64.FromInt(2), "never moved at all");

        var open = TestBattleFactory.CreateBattle(
            TestBattleFactory.CreateConfig(blocksMovement: false));
        open.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        open.Enqueue(new SpawnCharacterCommand(0, "hero", 2, Point(3, 2)));
        open.Tick();
        open.Enqueue(new MoveDirectionCommand(1, 1, Point(1, 0)));

        Assert.Single(open.Tick().OfType<CharacterMovedEvent>());
    }

    [Fact]
    public void Tick_SurvivesSubResolutionMoveDirection()
    {
        var battle = TestBattleFactory.CreateBattle();
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Tick();
        battle.Enqueue(new MoveDirectionCommand(
            1,
            1,
            new FixVector2(Fix64.FromRaw(1), Fix64.Zero)));

        var events = battle.Tick();

        Assert.Empty(events.OfType<CharacterMovedEvent>());
        Assert.Equal(Point(2, 2), battle.GetCharacter(1)!.Position);
    }

    [Fact]
    public void Enqueue_DropsLateCommandAndReportsRejection()
    {
        var battle = TestBattleFactory.CreateBattle();
        battle.Tick();

        Assert.False(battle.Enqueue(new StopMovingCommand(0, 99)));

        var rejected = Assert.Single(battle.Tick().OfType<CommandRejectedEvent>());
        Assert.Contains("earlier", rejected.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Tick_EmitsTimeoutOnceWithoutStoppingSimulation()
    {
        var battle = TestBattleFactory.CreateBattle(maxFrames: 2);

        Assert.Empty(battle.Tick().OfType<BattleTimeoutEvent>());
        Assert.Single(battle.Tick().OfType<BattleTimeoutEvent>());
        Assert.Empty(battle.Tick().OfType<BattleTimeoutEvent>());
        Assert.Equal(3, battle.CurrentFrame);
    }

    [Fact]
    public void SetFacingAndTarget_AreIndependentFromMovement()
    {
        var battle = TestBattleFactory.CreateBattle();
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 2, Point(5, 2)));
        battle.Tick();
        battle.Enqueue(new SetFacingCommand(1, 1, new Facing(256)));
        battle.Enqueue(new SetTargetCommand(1, 1, 2));

        var events = battle.Tick();
        var snapshot = battle.GetCharacter(1)!;

        Assert.Single(events.OfType<FacingChangedEvent>());
        Assert.Single(events.OfType<TargetChangedEvent>());
        Assert.Equal(256, snapshot.Facing.Step);
        Assert.Equal(2, snapshot.CurrentTargetId);
        Assert.Equal(Point(2, 2), snapshot.Position);
    }

    private static FixVector2 Point(long x, long y) =>
        new(Fix64.FromInt(x), Fix64.FromInt(y));
}
