using System.Collections.ObjectModel;
using BattleCore.Buffs;
using BattleCore.Math;
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
        }

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
}
