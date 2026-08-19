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
