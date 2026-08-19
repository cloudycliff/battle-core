using System.Collections.ObjectModel;
using BattleCore.Math;

namespace BattleCore.Configuration;

public sealed class BattleConfig
{
    public BattleConfig(
        IEnumerable<string> attributeIds,
        CombatAttributeBindings bindings,
        IEnumerable<CharacterTemplate> characterTemplates,
        long defenseK = 100,
        int maximumMitigation = 8_000,
        long minimumDamage = 1)
    {
        ArgumentNullException.ThrowIfNull(attributeIds);
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(characterTemplates);

        AttributeIds = new HashSet<string>(attributeIds, StringComparer.Ordinal);
        Bindings = bindings;
        CharacterTemplates = new ReadOnlyDictionary<string, CharacterTemplate>(
            characterTemplates.ToDictionary(template => template.Id, StringComparer.Ordinal));
        DefenseK = defenseK;
        MaximumMitigation = maximumMitigation;
        MinimumDamage = minimumDamage;

        Validate();
    }

    public IReadOnlySet<string> AttributeIds { get; }
    public CombatAttributeBindings Bindings { get; }
    public IReadOnlyDictionary<string, CharacterTemplate> CharacterTemplates { get; }
    public long DefenseK { get; }
    public int MaximumMitigation { get; }
    public long MinimumDamage { get; }

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

        if (errors.Count > 0)
        {
            throw new ConfigurationException(errors);
        }
    }
}
