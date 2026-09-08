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

public sealed record HitShieldEffect : HitEffect
{
    /// <summary>
    /// Same bounds as skill, buff, and pickup shields: a positive amount and a
    /// non-negative duration (0 = permanent). Negative durations used to slip
    /// through and become permanent shields at runtime.
    /// </summary>
    public HitShieldEffect(long Amount, int DurationFrames)
    {
        if (Amount <= 0 || DurationFrames < 0)
        {
            throw new ArgumentException(
                "Hit shield amount must be positive and duration non-negative.");
        }

        this.Amount = Amount;
        this.DurationFrames = DurationFrames;
    }

    public long Amount { get; }
    public int DurationFrames { get; }
}

public sealed record HitApplyBuffEffect(string BuffId) : HitEffect;
