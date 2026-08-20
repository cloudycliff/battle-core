using BattleCore.Combat;
using BattleCore.Math;

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

public sealed record SkillSpawnProjectileEffect(
    int Frame,
    SkillTarget Target,
    string ProjectileId)
    : SkillEffect(Frame, Target);

public sealed record SkillSpawnAreaEffectEffect(
    int Frame,
    SkillTarget Target,
    string AreaEffectId)
    : SkillEffect(Frame, Target);

public sealed record SkillSpawnCharacterEffect(
    int Frame,
    SkillTarget Target,
    string TemplateId,
    int Count = 1,
    string? BrainId = null,
    bool BindOwner = true)
    : SkillEffect(Frame, Target);

public sealed record SkillStartCarryEffect(
    int Frame,
    SkillTarget Target)
    : SkillEffect(Frame, Target);

public sealed record SkillReviveEffect(
    int Frame,
    SkillTarget Target)
    : SkillEffect(Frame, Target);

public sealed record SkillDropCarryEffect(
    int Frame,
    SkillTarget Target)
    : SkillEffect(Frame, Target);

public sealed record SkillThrowCarryEffect(
    int Frame,
    SkillTarget Target,
    Fix64 Distance)
    : SkillEffect(Frame, Target);
