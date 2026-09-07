using BattleCore.Configuration;
using BattleCore.Math;
using BattleCore.Serialization;
using BattleCore.Simulation.Commands;
using BattleCore.Simulation.Events;
using BattleCore.Skills;

namespace BattleCore.Tests;

public sealed class BattleJsonTests
{
    [Fact]
    public void LoadBundle_CreatesRunnableBattleFromSample()
    {
        var json = File.ReadAllText(SamplePath("arena.bundle.json"));
        var battle = BattleJson.CreateBattle(json);

        var marker = Assert.Single(battle.GetPlayerSpawnMarkers());
        Assert.Equal("player", marker.Id);

        battle.Enqueue(new SpawnCharacterCommand(
            0,
            "hero",
            1,
            marker.Position,
            PlayerControlled: true));
        Assert.Single(battle.Tick().OfType<CharacterSpawnedEvent>());
        Assert.Empty(battle.Tick().OfType<CharacterSpawnedEvent>());

        var spawned = Assert.Single(battle.Tick().OfType<CharacterSpawnedEvent>());
        Assert.Equal("goblin", spawned.TemplateId);
        Assert.Equal(2, spawned.FactionId);
        Assert.False(battle.GetCharacter(spawned.CharacterId)!.PlayerControlled);
    }

    [Fact]
    public void LoadBattleConfig_ReadsFix64RatioAndEnums()
    {
        var bundle = BattleJson.LoadBundle(File.ReadAllText(SamplePath("arena.bundle.json")));
        var hero = bundle.Battle.CharacterTemplates["hero"];
        Assert.Equal(SpatialMode.TopDown, bundle.Map.SpatialMode);
        Assert.Equal(Fix64.FromRatio(1, 2), hero.Radius);
        Assert.Equal("strike", Assert.Single(hero.SkillIds));
        Assert.Equal(
            SkillTargetKind.CurrentTarget,
            bundle.Battle.SkillDefinitions["strike"].Effects.OfType<SkillDamageEffect>().Single().Target.Kind);
        Assert.Equal(8, bundle.Battle.SkillDefinitions["strike"].Range.ToInt64());
    }

    [Fact]
    public void LoadSideViewSample_SpawnsGroundedOnTheFloor()
    {
        var json = File.ReadAllText(SamplePath("sideview.bundle.json"));
        var bundle = BattleJson.LoadBundle(json);
        var battle = BattleJson.CreateBattle(json);

        Assert.Equal(SpatialMode.SideView, bundle.Map.SpatialMode);
        Assert.Equal(SpatialMode.SideView, battle.SpatialMode);
        Assert.Empty(bundle.Map.Terrain);

        var marker = Assert.Single(battle.GetPlayerSpawnMarkers());
        Assert.Equal(Fix64.FromRatio(1, 2), marker.Position.Y);

        battle.Enqueue(new SpawnCharacterCommand(
            0,
            "hero",
            1,
            marker.Position,
            PlayerControlled: true));
        Assert.Single(battle.Tick().OfType<CharacterSpawnedEvent>());

        var player = battle.GetCharacter(1)!;
        Assert.True(player.Grounded);
        Assert.Equal(Fix64.FromRatio(1, 2), player.Position.Y);
        Assert.Empty(battle.Tick().OfType<CharacterSpawnedEvent>());

        var spawned = Assert.Single(battle.Tick().OfType<CharacterSpawnedEvent>());
        Assert.Equal("bandit", spawned.TemplateId);
        Assert.True(battle.GetCharacter(spawned.CharacterId)!.Grounded);
    }

    [Fact]
    public void LoadBundle_ReadsSideViewModeAndCreatesBattle()
    {
        var json = File.ReadAllText(SamplePath("arena.bundle.json"))
            .Replace(
                "\"map\": {",
                """
                "map": {
                  "spatialMode": "SideView",
                  "sideView": {
                    "gravity": 30,
                    "maximumFallSpeed": 20,
                    "jumpSpeed": 10
                  },
                """,
                StringComparison.Ordinal);

        var bundle = BattleJson.LoadBundle(json);

        Assert.Equal(SpatialMode.SideView, bundle.Map.SpatialMode);
        Assert.Equal(SpatialMode.SideView, bundle.CreateBattle().SpatialMode);
    }

    [Fact]
    public void LoadBattleConfig_RejectsJsonFloatingPointForFix64()
    {
        var error = Assert.Throws<ConfigurationException>(() =>
            BattleJson.LoadBattleConfig(
                """
                {
                  "attributeIds": ["max_hp"],
                  "bindings": {
                    "maxHp": "max_hp",
                    "physicalDefense": "max_hp",
                    "magicalDefense": "max_hp",
                    "critRate": "max_hp",
                    "critDamage": "max_hp",
                    "damageDealt": "max_hp",
                    "damageTaken": "max_hp",
                    "moveSpeed": "max_hp"
                  },
                  "characterTemplates": [
                    { "id": "hero", "radius": 0.5, "baseAttributes": { "max_hp": 1 } }
                  ]
                }
                """));

        Assert.Contains("floating-point", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadBattleConfig_RejectsUnknownSkillEffectKind()
    {
        var json = File.ReadAllText(SamplePath("arena.bundle.json"));
        json = json.Replace("\"kind\": \"damage\"", "\"kind\": \"explode\"", StringComparison.Ordinal);

        var error = Assert.Throws<ConfigurationException>(() => BattleJson.LoadBundle(json));
        Assert.Contains("explode", error.Message, StringComparison.Ordinal);
    }

    private static string SamplePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "samples", name);
}
