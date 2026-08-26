using System.Text.Json;
using BattleCore.Attributes;
using BattleCore.Buffs;
using BattleCore.Combat;
using BattleCore.Effects;
using BattleCore.Math;
using BattleCore.Pickups;
using BattleCore.Skills;

namespace BattleCore.Serialization;

public static partial class BattleJson
{
    private static SkillEffect? ReadSkillEffect(JsonElement root, string path, JsonParse parse)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            parse.Fail($"{path} must be an object.");
            return null;
        }

        var kind = parse.RequireString(root, "kind", path);
        var frame = parse.RequireInt32(root, "frame", path);
        var needsTarget = kind is not ("dash" or "dropCarry" or "throwCarry");
        var target = kind == "dash"
            ? SkillTarget.Self
            : ReadSkillTarget(root, path, parse, required: needsTarget);
        if (needsTarget && target is null)
        {
            return null;
        }

        try
        {
            return kind switch
            {
                "damage" => new SkillDamageEffect(
                    frame,
                    target!,
                    parse.RequireEnum<DamageType>(root, "damageType", path),
                    parse.RequireInt64(root, "flat", path),
                    parse.OptionalString(root, "attributeId", path),
                    parse.OptionalInt64(root, "ratio", path, 0),
                    parse.OptionalBool(root, "canCrit", path, true)),
                "heal" => new SkillHealEffect(
                    frame,
                    target!,
                    parse.RequireInt64(root, "flat", path),
                    parse.OptionalString(root, "attributeId", path),
                    parse.OptionalInt64(root, "ratio", path, 0),
                    parse.OptionalBool(root, "canCrit", path, true)),
                "shield" => new SkillShieldEffect(
                    frame,
                    target!,
                    parse.RequireInt64(root, "amount", path),
                    parse.RequireInt32(root, "durationFrames", path)),
                "applyBuff" => new SkillApplyBuffEffect(
                    frame,
                    target!,
                    parse.RequireString(root, "buffId", path)),
                "spawnProjectile" => new SkillSpawnProjectileEffect(
                    frame,
                    target!,
                    parse.RequireString(root, "projectileId", path)),
                "spawnAreaEffect" => new SkillSpawnAreaEffectEffect(
                    frame,
                    target!,
                    parse.RequireString(root, "areaEffectId", path)),
                "spawnCharacter" => new SkillSpawnCharacterEffect(
                    frame,
                    target!,
                    parse.RequireString(root, "templateId", path),
                    parse.OptionalInt32(root, "count", path, 1),
                    parse.OptionalString(root, "brainId", path),
                    parse.OptionalBool(root, "bindOwner", path, true)),
                "startCarry" => new SkillStartCarryEffect(frame, target!),
                "revive" => new SkillReviveEffect(frame, target!),
                "dropCarry" => new SkillDropCarryEffect(
                    frame,
                    target ?? SkillTarget.Self),
                "throwCarry" => new SkillThrowCarryEffect(
                    frame,
                    target ?? SkillTarget.Self,
                    parse.RequireFix64(root, "distance", path)),
                "dash" => new SkillDashEffect(frame, parse.RequireFix64(root, "distance", path)),
                "knockback" => new SkillKnockbackEffect(
                    frame,
                    target!,
                    parse.RequireFix64(root, "distance", path),
                    parse.OptionalBool(root, "alongCastDirection", path, false)),
                "pull" => new SkillPullEffect(
                    frame,
                    target!,
                    parse.RequireFix64(root, "distance", path)),
                _ => Unknown<SkillEffect>(parse, path, kind),
            };
        }
        catch (ArgumentException exception)
        {
            parse.Fail($"{path}: {exception.Message}");
            return null;
        }
    }

    private static SkillTarget? ReadSkillTarget(
        JsonElement parent,
        string path,
        JsonParse parse,
        bool required)
    {
        if (!parse.TryGet(parent, "target", out var value))
        {
            if (required)
            {
                parse.Fail($"{path}.target is required.");
            }

            return null;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            parse.Fail($"{path}.target must be an object.");
            return null;
        }

        var targetPath = $"{path}.target";
        return new SkillTarget(
            parse.RequireEnum<SkillTargetKind>(value, "kind", targetPath),
            parse.RequireEnum<TargetRelation>(value, "relation", targetPath),
            parse.OptionalFix64(value, "radius", targetPath, Fix64.Zero),
            parse.OptionalFix64(value, "innerRadius", targetPath, Fix64.Zero),
            parse.OptionalFix64(value, "minimumDot", targetPath, Fix64.Zero),
            parse.OptionalBool(value, "corpsesOnly", targetPath, false));
    }

    private static BuffEffect? ReadBuffEffect(JsonElement root, string path, JsonParse parse)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            parse.Fail($"{path} must be an object.");
            return null;
        }

        var kind = parse.RequireString(root, "kind", path);
        try
        {
            return kind switch
            {
                "attribute" => new AttributeBuffEffect(
                    parse.RequireString(root, "attributeId", path),
                    parse.RequireEnum<ModifierKind>(root, "modifierKind", path),
                    parse.RequireInt64(root, "valuePerStack", path)),
                "periodicDamage" => new PeriodicDamageBuffEffect(
                    parse.RequireInt32(root, "intervalFrames", path),
                    parse.RequireEnum<DamageType>(root, "damageType", path),
                    parse.RequireInt64(root, "flat", path),
                    parse.OptionalString(root, "attributeId", path),
                    parse.OptionalInt64(root, "ratio", path, 0),
                    parse.OptionalBool(root, "canCrit", path, false)),
                "periodicHeal" => new PeriodicHealBuffEffect(
                    parse.RequireInt32(root, "intervalFrames", path),
                    parse.RequireInt64(root, "flat", path),
                    parse.OptionalString(root, "attributeId", path),
                    parse.OptionalInt64(root, "ratio", path, 0),
                    parse.OptionalBool(root, "canCrit", path, false)),
                "periodicResource" => new PeriodicResourceBuffEffect(
                    parse.RequireInt32(root, "intervalFrames", path),
                    parse.RequireString(root, "resourceId", path),
                    parse.RequireInt64(root, "amount", path)),
                "control" => new ControlBuffEffect(parse.RequireEnum<ControlType>(root, "control", path)),
                "preventRevive" => new PreventReviveBuffEffect(),
                "immuneToDamage" => new ImmuneToDamageBuffEffect(),
                "untargetable" => new UntargetableBuffEffect(),
                "combatTrigger" => ReadCombatTrigger(root, path, parse),
                _ => Unknown<BuffEffect>(parse, path, kind),
            };
        }
        catch (ArgumentException exception)
        {
            parse.Fail($"{path}: {exception.Message}");
            return null;
        }
    }

    private static CombatTriggerBuffEffect? ReadCombatTrigger(
        JsonElement root,
        string path,
        JsonParse parse)
    {
        var actions = parse.RequireArray(
            root,
            "actions",
            path,
            (element, itemPath) => ReadTriggerAction(element, itemPath, parse));
        if (parse.HasErrors)
        {
            return null;
        }

        DamageType? damageType = parse.TryGet(root, "damageType", out _)
            ? parse.RequireEnum<DamageType>(root, "damageType", path)
            : null;

        return new CombatTriggerBuffEffect(
            parse.RequireEnum<CombatTriggerKind>(root, "trigger", path),
            actions.OfType<CombatTriggerAction>().ToArray(),
            parse.OptionalBool(root, "requireHitLanded", path, false),
            parse.OptionalBool(root, "requireCrit", path, false),
            damageType,
            parse.OptionalInt32(root, "cooldownFrames", path, 0));
    }

    private static CombatTriggerAction? ReadTriggerAction(JsonElement root, string path, JsonParse parse)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            parse.Fail($"{path} must be an object.");
            return null;
        }

        var kind = parse.RequireString(root, "kind", path);
        var target = parse.RequireEnum<CombatTriggerTarget>(root, "target", path);
        return kind switch
        {
            "heal" => new TriggerHealAction(
                target,
                parse.RequireInt64(root, "flat", path),
                parse.OptionalString(root, "attributeId", path),
                parse.OptionalInt64(root, "ratio", path, 0),
                parse.OptionalBool(root, "canCrit", path, false)),
            "grantResource" => new TriggerGrantResourceAction(
                target,
                parse.RequireString(root, "resourceId", path),
                parse.RequireInt64(root, "amount", path)),
            "applyBuff" => new TriggerApplyBuffAction(
                target,
                parse.RequireString(root, "buffId", path)),
            "grantShield" => new TriggerGrantShieldAction(
                target,
                parse.RequireInt64(root, "amount", path),
                parse.RequireInt32(root, "durationFrames", path)),
            _ => Unknown<CombatTriggerAction>(parse, path, kind),
        };
    }

    private static HitEffect? ReadHitEffect(JsonElement root, string path, JsonParse parse)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            parse.Fail($"{path} must be an object.");
            return null;
        }

        var kind = parse.RequireString(root, "kind", path);
        return kind switch
        {
            "damage" => new HitDamageEffect(
                parse.RequireEnum<DamageType>(root, "damageType", path),
                parse.RequireInt64(root, "flat", path),
                parse.OptionalString(root, "attributeId", path),
                parse.OptionalInt64(root, "ratio", path, 0),
                parse.OptionalBool(root, "canCrit", path, true)),
            "heal" => new HitHealEffect(
                parse.RequireInt64(root, "flat", path),
                parse.OptionalString(root, "attributeId", path),
                parse.OptionalInt64(root, "ratio", path, 0),
                parse.OptionalBool(root, "canCrit", path, true)),
            "shield" => new HitShieldEffect(
                parse.RequireInt64(root, "amount", path),
                parse.RequireInt32(root, "durationFrames", path)),
            "applyBuff" => new HitApplyBuffEffect(parse.RequireString(root, "buffId", path)),
            _ => Unknown<HitEffect>(parse, path, kind),
        };
    }

    private static PickupEffect? ReadPickupEffect(JsonElement root, string path, JsonParse parse)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            parse.Fail($"{path} must be an object.");
            return null;
        }

        var kind = parse.RequireString(root, "kind", path);
        return kind switch
        {
            "heal" => new PickupHealEffect(
                parse.RequireInt64(root, "flat", path),
                parse.OptionalString(root, "attributeId", path),
                parse.OptionalInt64(root, "ratio", path, 0)),
            "grantLives" => new PickupGrantLivesEffect(parse.RequireInt32(root, "amount", path)),
            "attribute" => new PickupAttributeEffect(
                parse.RequireString(root, "attributeId", path),
                parse.RequireEnum<ModifierKind>(root, "modifierKind", path),
                parse.RequireInt64(root, "value", path)),
            "applyBuff" => new PickupApplyBuffEffect(parse.RequireString(root, "buffId", path)),
            "shield" => new PickupShieldEffect(
                parse.RequireInt64(root, "amount", path),
                parse.RequireInt32(root, "durationFrames", path)),
            "grantResource" => new PickupGrantResourceEffect(
                parse.RequireString(root, "resourceId", path),
                parse.RequireInt64(root, "amount", path)),
            _ => Unknown<PickupEffect>(parse, path, kind),
        };
    }

    private static T? Unknown<T>(JsonParse parse, string path, string kind)
        where T : class
    {
        parse.Fail($"{path}.kind '{kind}' is not recognized.");
        return null;
    }
}
