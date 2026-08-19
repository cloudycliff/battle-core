using System.Collections.ObjectModel;
using BattleCore.Math;

namespace BattleCore.Configuration;

public sealed class CharacterTemplate
{
    public CharacterTemplate(
        string id,
        Fix64 radius,
        IReadOnlyDictionary<string, long> baseAttributes,
        bool targetable = true,
        bool damageable = true,
        bool blocksMovement = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(baseAttributes);

        Id = id;
        Radius = radius;
        BaseAttributes = new ReadOnlyDictionary<string, long>(
            new Dictionary<string, long>(baseAttributes, StringComparer.Ordinal));
        Targetable = targetable;
        Damageable = damageable;
        BlocksMovement = blocksMovement;
    }

    public string Id { get; }
    public Fix64 Radius { get; }
    public IReadOnlyDictionary<string, long> BaseAttributes { get; }
    public bool Targetable { get; }
    public bool Damageable { get; }
    public bool BlocksMovement { get; }
}
