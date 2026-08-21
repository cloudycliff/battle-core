using BattleCore.Attributes;
using BattleCore.Math;

namespace BattleCore.Pickups;

public enum PickupKind
{
    Instant,
    Stash,
}

public enum PickupPickerKind
{
    Any,
    PlayerControlled,
    SameFaction,
}

public abstract record PickupEffect;

public sealed record PickupHealEffect(
    long Flat,
    string? AttributeId = null,
    long Ratio = 0) : PickupEffect;

public sealed record PickupGrantLivesEffect(int Amount) : PickupEffect;

public sealed record PickupAttributeEffect(
    string AttributeId,
    ModifierKind Kind,
    long Value) : PickupEffect;

public sealed record PickupApplyBuffEffect(string BuffId) : PickupEffect;

public sealed record PickupShieldEffect(
    long Amount,
    int DurationFrames) : PickupEffect;

public sealed record PickupGrantResourceEffect(
    string ResourceId,
    long Amount) : PickupEffect;

public sealed class PickupDefinition
{
    public PickupDefinition(
        string id,
        Fix64 radius,
        PickupKind kind,
        IEnumerable<PickupEffect>? effects = null,
        PickupPickerKind picker = PickupPickerKind.Any)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        if (radius <= Fix64.Zero)
        {
            throw new ArgumentException("Pickup radius must be positive.", nameof(radius));
        }

        Id = id;
        Radius = radius;
        Kind = kind;
        Picker = picker;
        Effects = (effects ?? []).ToArray();

        if (Kind == PickupKind.Stash && Effects.Count > 0)
        {
            throw new ArgumentException(
                "Stash pickups cannot include in-core effects.",
                nameof(effects));
        }

        foreach (var effect in Effects)
        {
            if (effect is PickupGrantLivesEffect lives && lives.Amount <= 0)
            {
                throw new ArgumentException("Granted lives must be positive.");
            }

            if (effect is PickupShieldEffect shield &&
                (shield.Amount <= 0 || shield.DurationFrames < 0))
            {
                throw new ArgumentException(
                    "Pickup shield amount must be positive and duration non-negative.");
            }

            if (effect is PickupGrantResourceEffect resource &&
                (string.IsNullOrWhiteSpace(resource.ResourceId) || resource.Amount == 0))
            {
                throw new ArgumentException(
                    "Pickup resource grants require a resource id and a non-zero amount.");
            }
        }
    }

    public string Id { get; }
    public Fix64 Radius { get; }
    public PickupKind Kind { get; }
    public PickupPickerKind Picker { get; }
    public IReadOnlyList<PickupEffect> Effects { get; }
}
