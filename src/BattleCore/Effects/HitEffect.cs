using BattleCore.Combat;

namespace BattleCore.Effects;

public abstract record HitEffect;

public sealed record HitDamageEffect(
    DamageType DamageType,
    long Flat,
    string? AttributeId = null,
    long Ratio = 0,
    bool CanCrit = true) : HitEffect;

public sealed record HitHealEffect(
    long Flat,
    string? AttributeId = null,
    long Ratio = 0,
    bool CanCrit = true) : HitEffect;

public sealed record HitShieldEffect(
    long Amount,
    int DurationFrames) : HitEffect;

public sealed record HitApplyBuffEffect(string BuffId) : HitEffect;
