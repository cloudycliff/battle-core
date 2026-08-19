using BattleCore.Combat;

namespace BattleCore.Skills;

public abstract record SkillEffect(
    int Frame,
    SkillTarget Target);

public sealed record SkillDamageEffect(
    int Frame,
    SkillTarget Target,
    DamageType DamageType,
    long Flat,
    string? AttributeId = null,
    long Ratio = 0,
    bool CanCrit = true)
    : SkillEffect(Frame, Target);

public sealed record SkillHealEffect(
    int Frame,
    SkillTarget Target,
    long Flat,
    string? AttributeId = null,
    long Ratio = 0,
    bool CanCrit = true)
    : SkillEffect(Frame, Target);

public sealed record SkillShieldEffect(
    int Frame,
    SkillTarget Target,
    long Amount,
    int DurationFrames)
    : SkillEffect(Frame, Target);

public sealed record SkillApplyBuffEffect(
    int Frame,
    SkillTarget Target,
    string BuffId)
    : SkillEffect(Frame, Target);
