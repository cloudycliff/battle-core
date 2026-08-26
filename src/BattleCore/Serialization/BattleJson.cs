using System.Text.Json;
using BattleCore.Configuration;
using BattleCore.Simulation;

namespace BattleCore.Serialization;

/// <summary>
/// Loads validated in-memory configs from JSON. Spatial values are integers, n/d, or Q16.16 raw — never IEEE floats.
/// </summary>
public static partial class BattleJson
{
    public static BattleConfig LoadBattleConfig(string json)
    {
        using var document = JsonParse.ParseDocument(json);
        var parse = new JsonParse();
        var config = ReadBattleConfig(document.RootElement, "$", parse);
        parse.ThrowIfErrors();
        return config!;
    }

    public static MapConfig LoadMapConfig(string json)
    {
        using var document = JsonParse.ParseDocument(json);
        var parse = new JsonParse();
        var config = ReadMapConfig(document.RootElement, "$", parse);
        parse.ThrowIfErrors();
        return config!;
    }

    public static SpawnTable LoadSpawnTable(string json)
    {
        using var document = JsonParse.ParseDocument(json);
        var parse = new JsonParse();
        var table = ReadSpawnTable(document.RootElement, "$", parse);
        parse.ThrowIfErrors();
        return table!;
    }

    public static BattleJsonBundle LoadBundle(string json)
    {
        using var document = JsonParse.ParseDocument(json);
        var parse = new JsonParse();
        var root = document.RootElement;
        var battle = ReadBattleConfig(parse.Require(root, "battle", "$"), "$.battle", parse);
        var map = ReadMapConfig(parse.Require(root, "map", "$"), "$.map", parse);
        var spawn = parse.TryGet(root, "spawnTable", out var spawnElement)
            ? ReadSpawnTable(spawnElement, "$.spawnTable", parse)
            : SpawnTable.Empty;
        var seed = parse.RequireUInt64(root, "seed", "$");
        var maxFrames = parse.RequireInt64(root, "maxFrames", "$");
        parse.ThrowIfErrors();
        return new BattleJsonBundle(battle!, map!, spawn!, seed, maxFrames);
    }

    public static BattleInstance CreateBattle(string bundleJson)
    {
        var bundle = LoadBundle(bundleJson);
        return bundle.CreateBattle();
    }
}

public sealed record BattleJsonBundle(
    BattleConfig Battle,
    MapConfig Map,
    SpawnTable SpawnTable,
    ulong Seed,
    long MaxFrames)
{
    public BattleInstance CreateBattle() =>
        new(Battle, Map, Seed, MaxFrames, SpawnTable);
}
