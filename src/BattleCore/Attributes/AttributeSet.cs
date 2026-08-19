namespace BattleCore.Attributes;

public sealed class AttributeSet
{
    public const long RatioScale = 10_000;

    private readonly Dictionary<string, long> _baseValues;
    private readonly List<AttributeModifier> _modifiers = [];

    public AttributeSet(IReadOnlyDictionary<string, long> baseValues)
    {
        _baseValues = new Dictionary<string, long>(baseValues, StringComparer.Ordinal);
    }

    public long Get(string attributeId)
    {
        if (!_baseValues.TryGetValue(attributeId, out var baseValue))
        {
            throw new KeyNotFoundException($"Unknown attribute '{attributeId}'.");
        }

        var matching = _modifiers
            .Where(modifier => string.Equals(
                modifier.AttributeId,
                attributeId,
                StringComparison.Ordinal))
            .OrderBy(modifier => modifier.InstanceId);

        var value = (Int128)baseValue;

        foreach (var modifier in matching.Where(item => item.Kind == ModifierKind.Flat))
        {
            value += modifier.Value;
        }

        foreach (var modifier in matching.Where(item => item.Kind == ModifierKind.Percent))
        {
            value = value * (RatioScale + modifier.Value) / RatioScale;
        }

        foreach (var modifier in matching.Where(item => item.Kind == ModifierKind.Final))
        {
            value += modifier.Value;
        }

        return checked((long)value);
    }

    public void Add(AttributeModifier modifier)
    {
        if (!_baseValues.ContainsKey(modifier.AttributeId))
        {
            throw new ArgumentException(
                $"Unknown attribute '{modifier.AttributeId}'.",
                nameof(modifier));
        }

        if (_modifiers.Any(existing => existing.InstanceId == modifier.InstanceId))
        {
            throw new ArgumentException(
                $"Modifier instance '{modifier.InstanceId}' already exists.",
                nameof(modifier));
        }

        _modifiers.Add(modifier);
    }

    public bool Remove(long instanceId) =>
        _modifiers.RemoveAll(modifier => modifier.InstanceId == instanceId) > 0;
}
