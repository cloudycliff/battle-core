using BattleCore.Configuration;
using BattleCore.Math;
using BattleCore.Simulation;
using BattleCore.Simulation.Commands;
using BattleCore.Simulation.Events;

namespace BattleCore.Tests;

public sealed class SpawnTableTests
{
    [Fact]
    public void MapConfig_RejectsSpawnPointOutsideBounds()
    {
        var bounds = new FixAabb(Point(0, 0), Point(5, 5));

        var error = Assert.Throws<ConfigurationException>(() =>
            new MapConfig(
                bounds,
                spawnPoints:
                [
                    new SpawnPoint("east", Point(9, 1), "wave"),
                ]));

        Assert.Contains("east", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_RejectsUnknownTableEntry()
    {
        var error = Assert.Throws<ConfigurationException>(() =>
            CreateBattle(
                spawnPoints: [new SpawnPoint("gate", Point(4, 4), "missing")],
                spawnTable: SpawnTable.Empty));

        Assert.Contains("missing", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_RejectsUnknownTemplate()
    {
        var error = Assert.Throws<ConfigurationException>(() =>
            CreateBattle(
                spawnPoints: [new SpawnPoint("gate", Point(4, 4), "wave")],
                spawnTable: new SpawnTable(
                [
                    new SpawnTableEntry("wave", "goblin", 2, frame: 0),
                ])));

        Assert.Contains("goblin", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Tick_SpawnsTableEntryOnConfiguredFrame()
    {
        var battle = CreateBattle(
            spawnPoints: [new SpawnPoint("gate", Point(4, 4), "wave")],
            spawnTable: new SpawnTable(
            [
                new SpawnTableEntry("wave", "hero", 2, frame: 2, count: 2),
            ]));

        Assert.Empty(battle.Tick().OfType<CharacterSpawnedEvent>());
        Assert.Empty(battle.Tick().OfType<CharacterSpawnedEvent>());

        var spawned = battle.Tick().OfType<CharacterSpawnedEvent>().ToArray();

        Assert.Equal(2, spawned.Length);
        Assert.All(spawned, item =>
        {
            Assert.Equal(2, item.FactionId);
            Assert.Equal(Point(4, 4), item.Position);
            Assert.False(battle.GetCharacter(item.CharacterId)!.PlayerControlled);
        });
    }

    [Fact]
    public void Tick_SpawnsPointsInIdOrder()
    {
        var battle = CreateBattle(
            spawnPoints:
            [
                new SpawnPoint("b", Point(6, 4), "late"),
                new SpawnPoint("a", Point(3, 4), "late"),
            ],
            spawnTable: new SpawnTable(
            [
                new SpawnTableEntry("late", "hero", 2, frame: 0),
            ]));

        var spawned = battle.Tick().OfType<CharacterSpawnedEvent>().ToArray();

        Assert.Equal(Point(3, 4), spawned[0].Position);
        Assert.Equal(Point(6, 4), spawned[1].Position);
    }

    [Fact]
    public void Tick_AppliesExternalSpawnBeforeTableSpawnOnTheSameFrame()
    {
        var battle = CreateBattle(
            spawnPoints: [new SpawnPoint("gate", Point(8, 8), "wave")],
            spawnTable: new SpawnTable(
            [
                new SpawnTableEntry("wave", "hero", 2, frame: 0),
            ]));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2), PlayerControlled: true));

        var spawned = battle.Tick().OfType<CharacterSpawnedEvent>().ToArray();

        Assert.Equal(1, spawned[0].FactionId);
        Assert.True(battle.GetCharacter(1)!.PlayerControlled);
        Assert.Equal(2, spawned[1].FactionId);
        Assert.False(battle.GetCharacter(2)!.PlayerControlled);
    }

    [Fact]
    public void Query_ReturnsMapMarkersWithoutSpawningPlayers()
    {
        var marker = new PlayerSpawnMarker("p1", Point(2, 2));
        var point = new SpawnPoint("gate", Point(4, 4), "wave");
        var battle = CreateBattle(
            spawnPoints: [point],
            playerSpawnMarkers: [marker],
            spawnTable: new SpawnTable(
            [
                new SpawnTableEntry("wave", "hero", 2, frame: 5),
            ]));

        Assert.Equal(marker, Assert.Single(battle.GetPlayerSpawnMarkers()));
        Assert.Equal(point, Assert.Single(battle.GetSpawnPoints()));
        Assert.Empty(battle.Tick().OfType<CharacterSpawnedEvent>());
    }

    private static BattleInstance CreateBattle(
        IReadOnlyList<SpawnPoint>? spawnPoints = null,
        IReadOnlyList<PlayerSpawnMarker>? playerSpawnMarkers = null,
        SpawnTable? spawnTable = null)
    {
        var bounds = new FixAabb(Point(0, 0), Point(20, 20));
        return new BattleInstance(
            TestBattleFactory.CreateConfig(),
            new MapConfig(bounds, spawnPoints: spawnPoints, playerSpawnMarkers: playerSpawnMarkers),
            seed: 123,
            maxFrames: 1_000,
            spawnTable);
    }

    private static FixVector2 Point(long x, long y) =>
        new(Fix64.FromInt(x), Fix64.FromInt(y));
}
