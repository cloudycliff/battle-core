namespace BattleCore.Attributes;

public readonly record struct ModifierContribution(
    string AttributeId,
    ModifierKind Kind,
    long Value);

public sealed record ModifierSource(
    string SourceId,
    IReadOnlyList<ModifierContribution> Modifiers);
