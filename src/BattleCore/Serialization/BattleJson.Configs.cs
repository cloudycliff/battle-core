using System.Text.Json;
using BattleCore.AreaEffects;
using BattleCore.Attributes;
using BattleCore.Brains;
using BattleCore.Buffs;
using BattleCore.Configuration;
using BattleCore.Effects;
using BattleCore.Math;
using BattleCore.Pickups;
using BattleCore.Projectiles;
using BattleCore.Skills;

namespace BattleCore.Serialization;

public static partial class BattleJson
{
    private static BattleConfig? ReadBattleConfig(JsonElement root, string path, JsonParse parse)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            parse.Fail($"{path} must be an object.");
            return null;
        }

        var attributeIds = parse.RequireArray(
            root,
            "attributeIds",
            path,
            (element, itemPath) =>
            {
                if (element.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(element.GetString()))
                {
                    parse.Fail($"{itemPath} must be a non-empty string.");
                    return string.Empty;
                }

                return element.GetString()!;
            });
        var bindings = ReadBindings(parse.Require(root, "bindings", path), $"{path}.bindings", parse);
        var templates = parse.RequireArray(
            root,
            "characterTemplates",
            path,
            (element, itemPath) => ReadTemplate(element, itemPath, parse));
        var skills = parse.OptionalArray(
            root,
            "skills",
            path,
            (element, itemPath) => ReadSkill(element, itemPath, parse));
        var buffs = parse.OptionalArray(
            root,
            "buffs",
            path,
            (element, itemPath) => ReadBuff(element, itemPath, parse));
        var projectiles = parse.OptionalArray(
            root,
            "projectiles",
            path,
            (element, itemPath) => ReadProjectile(element, itemPath, parse));
        var areas = parse.OptionalArray(
            root,
            "areaEffects",
            path,
            (element, itemPath) => ReadAreaEffect(element, itemPath, parse));
        var brains = parse.OptionalArray(
            root,
            "brains",
            path,
            (element, itemPath) => ReadBrain(element, itemPath, parse));
        var pickups = parse.OptionalArray(
            root,
            "pickups",
            path,
            (element, itemPath) => ReadPickup(element, itemPath, parse));
        var resources = parse.OptionalArray(
            root,
            "resources",
            path,
            (element, itemPath) => ReadResource(element, itemPath, parse));
        var defenseK = parse.OptionalInt64(root, "defenseK", path, 100);
        var maximumMitigation = parse.OptionalInt32(root, "maximumMitigation", path, 8_000);
        var minimumDamage = parse.OptionalInt64(root, "minimumDamage", path, 1);
        var corpseDurationFrames = parse.OptionalInt32(root, "corpseDurationFrames", path, 30);
        var reviveDelayFrames = parse.OptionalInt32(root, "reviveDelayFrames", path, 30);

        if (parse.HasErrors)
        {
            return null;
        }

        try
        {
            return new BattleConfig(
                attributeIds,
                bindings!,
                templates.OfType<CharacterTemplate>(),
                skills.OfType<SkillDefinition>(),
                buffs.OfType<BuffDefinition>(),
                projectiles.OfType<ProjectileDefinition>(),
                areas.OfType<AreaEffectDefinition>(),
                brains.OfType<BrainDefinition>(),
                pickups.OfType<PickupDefinition>(),
                resources.OfType<ResourceDefinition>(),
                defenseK,
                maximumMitigation,
                minimumDamage,
                corpseDurationFrames,
                reviveDelayFrames);
        }
        catch (ConfigurationException exception)
        {
            foreach (var error in exception.Errors)
            {
                parse.Fail($"{path}: {error}");
            }

            return null;
        }
        catch (ArgumentException exception)
        {
            parse.Fail($"{path}: {exception.Message}");
            return null;
        }
    }

    private static CombatAttributeBindings? ReadBindings(JsonElement root, string path, JsonParse parse)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            parse.Fail($"{path} must be an object.");
            return null;
        }

        return new CombatAttributeBindings(
            parse.RequireString(root, "maxHp", path),
            parse.RequireString(root, "physicalDefense", path),
            parse.RequireString(root, "magicalDefense", path),
            parse.RequireString(root, "critRate", path),
            parse.RequireString(root, "critDamage", path),
            parse.RequireString(root, "damageDealt", path),
            parse.RequireString(root, "damageTaken", path),
            parse.RequireString(root, "moveSpeed", path));
    }

    private static CharacterTemplate? ReadTemplate(JsonElement root, string path, JsonParse parse)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            parse.Fail($"{path} must be an object.");
            return null;
        }

        try
        {
            return new CharacterTemplate(
                parse.RequireString(root, "id", path),
                parse.RequireFix64(root, "radius", path),
                parse.OptionalLongMap(root, "baseAttributes", path),
                parse.OptionalBool(root, "targetable", path, true),
                parse.OptionalBool(root, "damageable", path, true),
                parse.OptionalBool(root, "blocksMovement", path, true),
                parse.OptionalInt32(root, "lives", path, 0),
                parse.OptionalStringArray(root, "skillIds", path),
                parse.OptionalString(root, "brainId", path),
                parse.OptionalBool(root, "canBeRevived", path, false),
                parse.OptionalLongMap(root, "baseResources", path));
        }
        catch (ArgumentException exception)
        {
            parse.Fail($"{path}: {exception.Message}");
            return null;
        }
    }

    private static SkillDefinition? ReadSkill(JsonElement root, string path, JsonParse parse)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            parse.Fail($"{path} must be an object.");
            return null;
        }

        var effects = parse.RequireArray(
            root,
            "effects",
            path,
            (element, itemPath) => ReadSkillEffect(element, itemPath, parse));
        if (parse.HasErrors)
        {
            return null;
        }

        try
        {
            return new SkillDefinition(
                parse.RequireString(root, "id", path),
                parse.RequireInt32(root, "durationFrames", path),
                parse.RequireInt32(root, "cooldownFrames", path),
                effects.OfType<SkillEffect>(),
                parse.OptionalBool(root, "canMoveWhileCasting", path, false),
                parse.OptionalEnum(root, "category", path, SkillCategory.Spell),
                parse.OptionalLongMap(root, "resourceCosts", path),
                parse.OptionalFix64(root, "range", path, Fix64.Zero));
        }
        catch (ArgumentException exception)
        {
            parse.Fail($"{path}: {exception.Message}");
            return null;
        }
    }

    private static BuffDefinition? ReadBuff(JsonElement root, string path, JsonParse parse)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            parse.Fail($"{path} must be an object.");
            return null;
        }

        var effects = parse.RequireArray(
            root,
            "effects",
            path,
            (element, itemPath) => ReadBuffEffect(element, itemPath, parse));
        if (parse.HasErrors)
        {
            return null;
        }

        try
        {
            return new BuffDefinition(
                parse.RequireString(root, "id", path),
                parse.RequireInt32(root, "durationFrames", path),
                parse.RequireEnum<BuffStackingRule>(root, "stackingRule", path),
                effects.OfType<BuffEffect>(),
                parse.OptionalInt32(root, "maxStacks", path, 1),
                parse.OptionalStringArray(root, "tags", path),
                parse.OptionalStringArray(root, "immunityTags", path),
                parse.OptionalStringArray(root, "replacesTags", path));
        }
        catch (ArgumentException exception)
        {
            parse.Fail($"{path}: {exception.Message}");
            return null;
        }
    }

    private static ProjectileDefinition? ReadProjectile(JsonElement root, string path, JsonParse parse)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            parse.Fail($"{path} must be an object.");
            return null;
        }

        try
        {
            return new ProjectileDefinition(
                parse.RequireString(root, "id", path),
                parse.RequireFix64(root, "speed", path),
                parse.RequireFix64(root, "radius", path),
                parse.RequireInt32(root, "lifetimeFrames", path),
                parse.RequireInt32(root, "pierceCount", path),
                parse.RequireEnum<TargetRelation>(root, "relation", path),
                parse.RequireArray(
                    root,
                    "hitEffects",
                    path,
                    (element, itemPath) => ReadHitEffect(element, itemPath, parse)).OfType<HitEffect>(),
                parse.OptionalBool(root, "collidesWithTerrain", path, true));
        }
        catch (ArgumentException exception)
        {
            parse.Fail($"{path}: {exception.Message}");
            return null;
        }
    }

    private static AreaEffectDefinition? ReadAreaEffect(JsonElement root, string path, JsonParse parse)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            parse.Fail($"{path} must be an object.");
            return null;
        }

        try
        {
            return new AreaEffectDefinition(
                parse.RequireString(root, "id", path),
                parse.RequireFix64(root, "radius", path),
                parse.RequireInt32(root, "durationFrames", path),
                parse.RequireInt32(root, "intervalFrames", path),
                parse.RequireEnum<TargetRelation>(root, "relation", path),
                parse.RequireArray(
                    root,
                    "hitEffects",
                    path,
                    (element, itemPath) => ReadHitEffect(element, itemPath, parse)).OfType<HitEffect>(),
                parse.OptionalBool(root, "followsTarget", path, false),
                parse.OptionalBool(root, "tickOnSpawn", path, true));
        }
        catch (ArgumentException exception)
        {
            parse.Fail($"{path}: {exception.Message}");
            return null;
        }
    }

    private static BrainDefinition? ReadBrain(JsonElement root, string path, JsonParse parse)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            parse.Fail($"{path} must be an object.");
            return null;
        }

        try
        {
            return new BrainDefinition(
                parse.RequireString(root, "id", path),
                parse.RequireEnum<BrainKind>(root, "kind", path),
                parse.TryGet(root, "aggroRadius", out _)
                    ? parse.RequireFix64(root, "aggroRadius", path)
                    : null,
                parse.TryGet(root, "attackRange", out _)
                    ? parse.RequireFix64(root, "attackRange", path)
                    : null,
                parse.TryGet(root, "followDistance", out _)
                    ? parse.RequireFix64(root, "followDistance", path)
                    : null,
                parse.TryGet(root, "leashRadius", out _)
                    ? parse.RequireFix64(root, "leashRadius", path)
                    : null);
        }
        catch (ArgumentException exception)
        {
            parse.Fail($"{path}: {exception.Message}");
            return null;
        }
    }

    private static PickupDefinition? ReadPickup(JsonElement root, string path, JsonParse parse)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            parse.Fail($"{path} must be an object.");
            return null;
        }

        try
        {
            return new PickupDefinition(
                parse.RequireString(root, "id", path),
                parse.RequireFix64(root, "radius", path),
                parse.RequireEnum<PickupKind>(root, "kind", path),
                parse.OptionalArray(
                    root,
                    "effects",
                    path,
                    (element, itemPath) => ReadPickupEffect(element, itemPath, parse)).OfType<PickupEffect>(),
                parse.OptionalEnum(root, "picker", path, PickupPickerKind.Any));
        }
        catch (ArgumentException exception)
        {
            parse.Fail($"{path}: {exception.Message}");
            return null;
        }
    }

    private static ResourceDefinition? ReadResource(JsonElement root, string path, JsonParse parse)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            parse.Fail($"{path} must be an object.");
            return null;
        }

        try
        {
            return new ResourceDefinition(
                parse.RequireString(root, "id", path),
                parse.RequireInt64(root, "maximum", path));
        }
        catch (ArgumentException exception)
        {
            parse.Fail($"{path}: {exception.Message}");
            return null;
        }
    }

    private static MapConfig? ReadMapConfig(JsonElement root, string path, JsonParse parse)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            parse.Fail($"{path} must be an object.");
            return null;
        }

        var bounds = parse.RequireAabb(root, "bounds", path);
        var terrain = parse.OptionalArray(
            root,
            "terrain",
            path,
            (element, itemPath) => parse.ReadAabbElement(element, itemPath));
        var spawnPoints = parse.OptionalArray(
            root,
            "spawnPoints",
            path,
            (element, itemPath) => ReadSpawnPoint(element, itemPath, parse));
        var playerMarkers = parse.OptionalArray(
            root,
            "playerSpawnMarkers",
            path,
            (element, itemPath) => ReadPlayerMarker(element, itemPath, parse));

        if (parse.HasErrors)
        {
            return null;
        }

        try
        {
            return new MapConfig(
                bounds,
                terrain,
                spawnPoints.OfType<SpawnPoint>(),
                playerMarkers.OfType<PlayerSpawnMarker>());
        }
        catch (ConfigurationException exception)
        {
            foreach (var error in exception.Errors)
            {
                parse.Fail($"{path}: {error}");
            }

            return null;
        }
        catch (ArgumentException exception)
        {
            parse.Fail($"{path}: {exception.Message}");
            return null;
        }
    }

    private static SpawnPoint? ReadSpawnPoint(JsonElement root, string path, JsonParse parse)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            parse.Fail($"{path} must be an object.");
            return null;
        }

        try
        {
            return new SpawnPoint(
                parse.RequireString(root, "id", path),
                parse.RequireVector(root, "position", path),
                parse.RequireString(root, "tableEntryId", path));
        }
        catch (ArgumentException exception)
        {
            parse.Fail($"{path}: {exception.Message}");
            return null;
        }
    }

    private static PlayerSpawnMarker? ReadPlayerMarker(JsonElement root, string path, JsonParse parse)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            parse.Fail($"{path} must be an object.");
            return null;
        }

        try
        {
            return new PlayerSpawnMarker(
                parse.RequireString(root, "id", path),
                parse.RequireVector(root, "position", path));
        }
        catch (ArgumentException exception)
        {
            parse.Fail($"{path}: {exception.Message}");
            return null;
        }
    }

    private static SpawnTable? ReadSpawnTable(JsonElement root, string path, JsonParse parse)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            var entries = parse.ReadArray(
                root,
                path,
                (element, itemPath) => ReadSpawnEntry(element, itemPath, parse));
            if (parse.HasErrors)
            {
                return null;
            }

            try
            {
                return new SpawnTable(entries.OfType<SpawnTableEntry>());
            }
            catch (ConfigurationException exception)
            {
                foreach (var error in exception.Errors)
                {
                    parse.Fail($"{path}: {error}");
                }

                return null;
            }
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            parse.Fail($"{path} must be an object or array of entries.");
            return null;
        }

        var listed = parse.OptionalArray(
            root,
            "entries",
            path,
            (element, itemPath) => ReadSpawnEntry(element, itemPath, parse));
        if (parse.HasErrors)
        {
            return null;
        }

        try
        {
            return new SpawnTable(listed.OfType<SpawnTableEntry>());
        }
        catch (ConfigurationException exception)
        {
            foreach (var error in exception.Errors)
            {
                parse.Fail($"{path}: {error}");
            }

            return null;
        }
    }

    private static SpawnTableEntry? ReadSpawnEntry(JsonElement root, string path, JsonParse parse)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            parse.Fail($"{path} must be an object.");
            return null;
        }

        try
        {
            return new SpawnTableEntry(
                parse.RequireString(root, "id", path),
                parse.RequireString(root, "templateId", path),
                parse.RequireInt32(root, "factionId", path),
                parse.RequireInt64(root, "frame", path),
                parse.OptionalInt32(root, "count", path, 1),
                parse.OptionalString(root, "brainId", path));
        }
        catch (ArgumentException exception)
        {
            parse.Fail($"{path}: {exception.Message}");
            return null;
        }
    }
}
