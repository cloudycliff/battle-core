namespace BattleCore.Buffs;

public enum BuffStackingRule
{
    Unique,
    Refresh,
    Stack,
    Independent,
}

public sealed class BuffDefinition
{
    public BuffDefinition(
        string id,
        int durationFrames,
        BuffStackingRule stackingRule,
        IEnumerable<BuffEffect> effects,
        int maxStacks = 1,
        IEnumerable<string>? tags = null,
        IEnumerable<string>? immunityTags = null,
        IEnumerable<string>? replacesTags = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentOutOfRangeException.ThrowIfNegative(durationFrames);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxStacks);
        ArgumentNullException.ThrowIfNull(effects);

        if (stackingRule != BuffStackingRule.Stack && maxStacks != 1)
        {
            throw new ArgumentException(
                "Only Stack buffs may have more than one stack.",
                nameof(maxStacks));
        }

        Id = id;
        DurationFrames = durationFrames;
        StackingRule = stackingRule;
        MaxStacks = maxStacks;
        Effects = effects.ToArray();
        Tags = (tags ?? []).ToHashSet(StringComparer.Ordinal);
        ImmunityTags = (immunityTags ?? []).ToHashSet(StringComparer.Ordinal);
        ReplacesTags = (replacesTags ?? []).ToHashSet(StringComparer.Ordinal);

        foreach (var periodic in Effects.OfType<PeriodicBuffEffect>())
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(periodic.IntervalFrames);
            if (periodic is PeriodicResourceBuffEffect resource && resource.Amount == 0)
            {
                throw new ArgumentException("Periodic resource amount cannot be zero.", nameof(effects));
            }
        }

        foreach (var trigger in Effects.OfType<CombatTriggerBuffEffect>())
        {
            ArgumentOutOfRangeException.ThrowIfNegative(trigger.CooldownFrames);
            ArgumentNullException.ThrowIfNull(trigger.Actions);
            if (trigger.Actions.Count == 0)
            {
                throw new ArgumentException("Combat trigger must declare at least one action.", nameof(effects));
            }

            foreach (var action in trigger.Actions)
            {
                switch (action)
                {
                    case TriggerHealAction heal when heal.Flat == 0 && heal.Ratio == 0:
                        throw new ArgumentException(
                            "Trigger heal must have a non-zero flat or ratio.",
                            nameof(effects));
                    case TriggerGrantResourceAction resource when resource.Amount == 0:
                        throw new ArgumentException(
                            "Trigger resource amount cannot be zero.",
                            nameof(effects));
                    case TriggerGrantShieldAction shield when shield.Amount <= 0 || shield.DurationFrames < 0:
                        throw new ArgumentException(
                            "Trigger shield amount must be positive and duration non-negative.",
                            nameof(effects));
                    case TriggerApplyBuffAction apply when string.IsNullOrWhiteSpace(apply.BuffId):
                        throw new ArgumentException("Trigger buff id must be set.", nameof(effects));
                    case TriggerGrantResourceAction resource when string.IsNullOrWhiteSpace(resource.ResourceId):
                        throw new ArgumentException("Trigger resource id must be set.", nameof(effects));
                }
            }
        }
    }

    public string Id { get; }
    public int DurationFrames { get; }
    public BuffStackingRule StackingRule { get; }
    public int MaxStacks { get; }
    public IReadOnlyList<BuffEffect> Effects { get; }
    public IReadOnlySet<string> Tags { get; }
    public IReadOnlySet<string> ImmunityTags { get; }
    public IReadOnlySet<string> ReplacesTags { get; }
}
