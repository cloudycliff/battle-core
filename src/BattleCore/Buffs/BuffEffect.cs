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

public sealed record PeriodicResourceBuffEffect(
    int IntervalFrames,
    string ResourceId,
    long Amount) : PeriodicBuffEffect(IntervalFrames);

public enum ControlType
{
    Stun,
    Silence,
    Root,
    Disarm,
}

public sealed record ControlBuffEffect(ControlType Control) : BuffEffect;

public sealed record PreventReviveBuffEffect : BuffEffect;

public sealed record ImmuneToDamageBuffEffect : BuffEffect;

public sealed record UntargetableBuffEffect : BuffEffect;

public enum CombatTriggerKind
{
    DealDamage,
    TakeDamage,
    Kill,
    SkillStart,
}

public enum CombatTriggerTarget
{
    Owner,
    Other,
}

public sealed record CombatTriggerBuffEffect(
    CombatTriggerKind Kind,
    IReadOnlyList<CombatTriggerAction> Actions,
    bool RequireHitLanded = false,
    bool RequireCrit = false,
    DamageType? DamageType = null,
    int CooldownFrames = 0) : BuffEffect;

public abstract record CombatTriggerAction(CombatTriggerTarget Target);

public sealed record TriggerHealAction(
    CombatTriggerTarget Target,
    long Flat,
    string? AttributeId = null,
    long Ratio = 0,
    bool CanCrit = false) : CombatTriggerAction(Target);

public sealed record TriggerGrantResourceAction(
    CombatTriggerTarget Target,
    string ResourceId,
    long Amount) : CombatTriggerAction(Target);

public sealed record TriggerApplyBuffAction(
    CombatTriggerTarget Target,
    string BuffId) : CombatTriggerAction(Target);

public sealed record TriggerGrantShieldAction(
    CombatTriggerTarget Target,
    long Amount,
    int DurationFrames) : CombatTriggerAction(Target);
