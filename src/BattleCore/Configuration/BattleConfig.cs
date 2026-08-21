using System.Collections.ObjectModel;
using BattleCore.AreaEffects;
using BattleCore.Brains;
using BattleCore.Buffs;
using BattleCore.Effects;
using BattleCore.Math;
using BattleCore.Pickups;
using BattleCore.Projectiles;
using BattleCore.Skills;

namespace BattleCore.Configuration;

public sealed class BattleConfig
{
    public BattleConfig(
        IEnumerable<string> attributeIds,
        CombatAttributeBindings bindings,
        IEnumerable<CharacterTemplate> characterTemplates,
        IEnumerable<SkillDefinition>? skillDefinitions = null,
        IEnumerable<BuffDefinition>? buffDefinitions = null,
        IEnumerable<ProjectileDefinition>? projectileDefinitions = null,
        IEnumerable<AreaEffectDefinition>? areaEffectDefinitions = null,
        IEnumerable<BrainDefinition>? brainDefinitions = null,
        IEnumerable<PickupDefinition>? pickupDefinitions = null,
        IEnumerable<ResourceDefinition>? resourceDefinitions = null,
        long defenseK = 100,
        int maximumMitigation = 8_000,
        long minimumDamage = 1,
        int corpseDurationFrames = 30,
        int reviveDelayFrames = 30)
    {
        ArgumentNullException.ThrowIfNull(attributeIds);
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(characterTemplates);

        AttributeIds = new HashSet<string>(attributeIds, StringComparer.Ordinal);
        Bindings = bindings;
        CharacterTemplates = new ReadOnlyDictionary<string, CharacterTemplate>(
            characterTemplates.ToDictionary(template => template.Id, StringComparer.Ordinal));
        SkillDefinitions = new ReadOnlyDictionary<string, SkillDefinition>(
            (skillDefinitions ?? []).ToDictionary(skill => skill.Id, StringComparer.Ordinal));
        BuffDefinitions = new ReadOnlyDictionary<string, BuffDefinition>(
            (buffDefinitions ?? []).ToDictionary(buff => buff.Id, StringComparer.Ordinal));
        ProjectileDefinitions = new ReadOnlyDictionary<string, ProjectileDefinition>(
            (projectileDefinitions ?? []).ToDictionary(item => item.Id, StringComparer.Ordinal));
        AreaEffectDefinitions = new ReadOnlyDictionary<string, AreaEffectDefinition>(
            (areaEffectDefinitions ?? []).ToDictionary(item => item.Id, StringComparer.Ordinal));
        BrainDefinitions = new ReadOnlyDictionary<string, BrainDefinition>(
            (brainDefinitions ?? []).ToDictionary(item => item.Id, StringComparer.Ordinal));
        PickupDefinitions = new ReadOnlyDictionary<string, PickupDefinition>(
            (pickupDefinitions ?? []).ToDictionary(item => item.Id, StringComparer.Ordinal));
        ResourceDefinitions = new ReadOnlyDictionary<string, ResourceDefinition>(
            (resourceDefinitions ?? []).ToDictionary(item => item.Id, StringComparer.Ordinal));
        DefenseK = defenseK;
        MaximumMitigation = maximumMitigation;
        MinimumDamage = minimumDamage;
        CorpseDurationFrames = corpseDurationFrames;
        ReviveDelayFrames = reviveDelayFrames;

        Validate();
    }

    public IReadOnlySet<string> AttributeIds { get; }
    public CombatAttributeBindings Bindings { get; }
    public IReadOnlyDictionary<string, CharacterTemplate> CharacterTemplates { get; }
    public IReadOnlyDictionary<string, SkillDefinition> SkillDefinitions { get; }
    public IReadOnlyDictionary<string, BuffDefinition> BuffDefinitions { get; }
    public IReadOnlyDictionary<string, ProjectileDefinition> ProjectileDefinitions { get; }
    public IReadOnlyDictionary<string, AreaEffectDefinition> AreaEffectDefinitions { get; }
    public IReadOnlyDictionary<string, BrainDefinition> BrainDefinitions { get; }
    public IReadOnlyDictionary<string, PickupDefinition> PickupDefinitions { get; }
    public IReadOnlyDictionary<string, ResourceDefinition> ResourceDefinitions { get; }
    public long DefenseK { get; }
    public int MaximumMitigation { get; }
    public long MinimumDamage { get; }
    public int CorpseDurationFrames { get; }
    public int ReviveDelayFrames { get; }

    private void Validate()
    {
        var errors = new List<string>();

        if (AttributeIds.Count == 0)
        {
            errors.Add("At least one attribute must be declared.");
        }

        foreach (var bindingId in Bindings.AllIds())
        {
            if (!AttributeIds.Contains(bindingId))
            {
                errors.Add($"Combat binding references undeclared attribute '{bindingId}'.");
            }
        }

        if (CharacterTemplates.Count == 0)
        {
            errors.Add("At least one character template is required.");
        }

        foreach (var template in CharacterTemplates.Values)
        {
            if (template.Radius <= Fix64.Zero)
            {
                errors.Add($"Character '{template.Id}' radius must be positive.");
            }

            foreach (var attributeId in template.BaseAttributes.Keys)
            {
                if (!AttributeIds.Contains(attributeId))
                {
                    errors.Add(
                        $"Character '{template.Id}' references undeclared attribute '{attributeId}'.");
                }
            }

            foreach (var bindingId in Bindings.AllIds())
            {
                if (!template.BaseAttributes.ContainsKey(bindingId))
                {
                    errors.Add(
                        $"Character '{template.Id}' is missing bound attribute '{bindingId}'.");
                }
            }

            foreach (var skillId in template.SkillIds)
            {
                if (!SkillDefinitions.ContainsKey(skillId))
                {
                    errors.Add(
                        $"Character '{template.Id}' references unknown skill '{skillId}'.");
                }
            }

            if (template.BrainId is string brainId &&
                !BrainDefinitions.ContainsKey(brainId))
            {
                errors.Add(
                    $"Character '{template.Id}' references unknown brain '{brainId}'.");
            }

            foreach (var resourceId in template.BaseResources.Keys)
            {
                if (!ResourceDefinitions.ContainsKey(resourceId))
                {
                    errors.Add(
                        $"Character '{template.Id}' references undeclared resource '{resourceId}'.");
                }
            }

            foreach (var (resourceId, amount) in template.BaseResources)
            {
                if (amount < 0)
                {
                    errors.Add(
                        $"Character '{template.Id}' resource '{resourceId}' cannot start negative.");
                }
                else if (ResourceDefinitions.TryGetValue(resourceId, out var definition) &&
                         amount > definition.Maximum)
                {
                    errors.Add(
                        $"Character '{template.Id}' resource '{resourceId}' starts above maximum {definition.Maximum}.");
                }
            }
        }

        foreach (var skill in SkillDefinitions.Values)
        {
            foreach (var attributeId in skill.Effects
                         .Select(GetEffectAttributeId)
                         .Where(attributeId => attributeId is not null))
            {
                if (!AttributeIds.Contains(attributeId!))
                {
                    errors.Add(
                        $"Skill '{skill.Id}' references undeclared attribute '{attributeId}'.");
                }
            }

            foreach (var buffId in skill.Effects
                         .OfType<SkillApplyBuffEffect>()
                         .Select(effect => effect.BuffId))
            {
                if (!BuffDefinitions.ContainsKey(buffId))
                {
                    errors.Add($"Skill '{skill.Id}' references unknown buff '{buffId}'.");
                }
            }

            foreach (var projectileId in skill.Effects
                         .OfType<SkillSpawnProjectileEffect>()
                         .Select(effect => effect.ProjectileId))
            {
                if (!ProjectileDefinitions.ContainsKey(projectileId))
                {
                    errors.Add($"Skill '{skill.Id}' references unknown projectile '{projectileId}'.");
                }
            }

            foreach (var areaEffectId in skill.Effects
                         .OfType<SkillSpawnAreaEffectEffect>()
                         .Select(effect => effect.AreaEffectId))
            {
                if (!AreaEffectDefinitions.ContainsKey(areaEffectId))
                {
                    errors.Add($"Skill '{skill.Id}' references unknown area effect '{areaEffectId}'.");
                }
            }

            foreach (var summon in skill.Effects.OfType<SkillSpawnCharacterEffect>())
            {
                if (!CharacterTemplates.ContainsKey(summon.TemplateId))
                {
                    errors.Add(
                        $"Skill '{skill.Id}' references unknown character template '{summon.TemplateId}'.");
                }

                if (summon.BrainId is string brainId &&
                    !BrainDefinitions.ContainsKey(brainId))
                {
                    errors.Add($"Skill '{skill.Id}' references unknown brain '{brainId}'.");
                }
            }

            foreach (var (resourceId, amount) in skill.ResourceCosts)
            {
                if (!ResourceDefinitions.ContainsKey(resourceId))
                {
                    errors.Add($"Skill '{skill.Id}' references undeclared resource '{resourceId}'.");
                }

                if (amount <= 0)
                {
                    errors.Add($"Skill '{skill.Id}' resource cost '{resourceId}' must be positive.");
                }
            }
        }

        ValidateHitCatalog("Projectile", ProjectileDefinitions.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.HitEffects,
            StringComparer.Ordinal), errors);
        ValidateHitCatalog("AreaEffect", AreaEffectDefinitions.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.HitEffects,
            StringComparer.Ordinal), errors);

        foreach (var buff in BuffDefinitions.Values)
        {
            foreach (var attributeId in buff.Effects
                         .Select(GetBuffAttributeId)
                         .Where(attributeId => attributeId is not null))
            {
                if (!AttributeIds.Contains(attributeId!))
                {
                    errors.Add(
                        $"Buff '{buff.Id}' references undeclared attribute '{attributeId}'.");
                }
            }

            foreach (var resourceId in buff.Effects
                         .OfType<PeriodicResourceBuffEffect>()
                         .Select(effect => effect.ResourceId))
            {
                if (!ResourceDefinitions.ContainsKey(resourceId))
                {
                    errors.Add($"Buff '{buff.Id}' references undeclared resource '{resourceId}'.");
                }
            }
        }

        foreach (var pickup in PickupDefinitions.Values)
        {
            foreach (var attributeId in pickup.Effects
                         .Select(GetPickupAttributeId)
                         .Where(attributeId => attributeId is not null))
            {
                if (!AttributeIds.Contains(attributeId!))
                {
                    errors.Add(
                        $"Pickup '{pickup.Id}' references undeclared attribute '{attributeId}'.");
                }
            }

            foreach (var buffId in pickup.Effects
                         .OfType<PickupApplyBuffEffect>()
                         .Select(effect => effect.BuffId))
            {
                if (!BuffDefinitions.ContainsKey(buffId))
                {
                    errors.Add($"Pickup '{pickup.Id}' references unknown buff '{buffId}'.");
                }
            }

            foreach (var resourceId in pickup.Effects
                         .OfType<PickupGrantResourceEffect>()
                         .Select(effect => effect.ResourceId))
            {
                if (!ResourceDefinitions.ContainsKey(resourceId))
                {
                    errors.Add($"Pickup '{pickup.Id}' references undeclared resource '{resourceId}'.");
                }
            }
        }

        if (DefenseK <= 0)
        {
            errors.Add("Defense K must be positive.");
        }

        if (MaximumMitigation is < 0 or > 10_000)
        {
            errors.Add("Maximum mitigation must be between 0 and 10000.");
        }

        if (MinimumDamage < 0)
        {
            errors.Add("Minimum damage cannot be negative.");
        }

        if (CorpseDurationFrames < 0)
        {
            errors.Add("Corpse duration cannot be negative.");
        }

        if (ReviveDelayFrames < 0)
        {
            errors.Add("Revive delay cannot be negative.");
        }

        if (errors.Count > 0)
        {
            throw new ConfigurationException(errors);
        }
    }

    private static string? GetEffectAttributeId(SkillEffect effect) =>
        effect switch
        {
            SkillDamageEffect damage => damage.AttributeId,
            SkillHealEffect heal => heal.AttributeId,
            _ => null,
        };

    private static string? GetBuffAttributeId(BuffEffect effect) =>
        effect switch
        {
            AttributeBuffEffect attribute => attribute.AttributeId,
            PeriodicDamageBuffEffect damage => damage.AttributeId,
            PeriodicHealBuffEffect heal => heal.AttributeId,
            _ => null,
        };

    private void ValidateHitCatalog(
        string kind,
        IReadOnlyDictionary<string, IReadOnlyList<HitEffect>> catalog,
        List<string> errors)
    {
        foreach (var (id, hitEffects) in catalog)
        {
            foreach (var attributeId in hitEffects
                         .Select(GetHitAttributeId)
                         .Where(attributeId => attributeId is not null))
            {
                if (!AttributeIds.Contains(attributeId!))
                {
                    errors.Add($"{kind} '{id}' references undeclared attribute '{attributeId}'.");
                }
            }

            foreach (var buffId in hitEffects
                         .OfType<HitApplyBuffEffect>()
                         .Select(effect => effect.BuffId))
            {
                if (!BuffDefinitions.ContainsKey(buffId))
                {
                    errors.Add($"{kind} '{id}' references unknown buff '{buffId}'.");
                }
            }
        }
    }

    private static string? GetPickupAttributeId(PickupEffect effect) =>
        effect switch
        {
            PickupHealEffect heal => heal.AttributeId,
            PickupAttributeEffect attribute => attribute.AttributeId,
            _ => null,
        };

    private static string? GetHitAttributeId(HitEffect effect) =>
        effect switch
        {
            HitDamageEffect damage => damage.AttributeId,
            HitHealEffect heal => heal.AttributeId,
            _ => null,
        };
}
