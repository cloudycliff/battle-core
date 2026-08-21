using BattleCore.Randomness;

namespace BattleCore.Combat;

internal sealed class CombatSourceSnapshot
{
    public required long SourceId { get; init; }
    public required int FactionId { get; init; }
    public required long CritRate { get; init; }
    public required long CritDamage { get; init; }
    public required long DamageDealt { get; init; }
    public required DeterministicRng Rng { get; init; }
    public required IReadOnlyDictionary<string, long> Attributes { get; init; }

    public long ComputeBase(string? attributeId, long flat, long ratio) =>
        CombatMath.ComputeBase(flat, ReadAttribute(attributeId), ratio);

    public long ReadAttribute(string? attributeId) =>
        attributeId is null ? 0 : Attributes.GetValueOrDefault(attributeId);

    public CombatSourceSnapshot WithRng(DeterministicRng rng) =>
        new()
        {
            SourceId = SourceId,
            FactionId = FactionId,
            CritRate = CritRate,
            CritDamage = CritDamage,
            DamageDealt = DamageDealt,
            Rng = rng,
            Attributes = Attributes,
        };
}
