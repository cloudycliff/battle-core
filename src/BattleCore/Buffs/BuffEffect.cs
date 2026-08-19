using BattleCore.Attributes;
using BattleCore.Combat;

namespace BattleCore.Buffs;

public abstract record BuffEffect;

public sealed record AttributeBuffEffect(
    string AttributeId,
    ModifierKind Kind,
    long ValuePerStack) : BuffEffect;

public abstract record PeriodicBuffEffect(
    int IntervalFrames) : BuffEffect;

public sealed record PeriodicDamageBuffEffect(
    int IntervalFrames,
    DamageType DamageType,
    long Flat,
    string? AttributeId = null,
    long Ratio = 0,
    bool CanCrit = false) : PeriodicBuffEffect(IntervalFrames);

public sealed record PeriodicHealBuffEffect(
    int IntervalFrames,
    long Flat,
    string? AttributeId = null,
    long Ratio = 0,
    bool CanCrit = false) : PeriodicBuffEffect(IntervalFrames);

public enum ControlType
{
    Stun,
    Silence,
    Root,
    Disarm,
}

public sealed record ControlBuffEffect(ControlType Control) : BuffEffect;
