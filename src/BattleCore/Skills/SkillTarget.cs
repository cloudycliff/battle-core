using BattleCore.Math;

namespace BattleCore.Skills;

public enum SkillTargetKind
{
    Self,
    ExplicitTarget,
    CurrentTarget,
    CircleAtPoint,
    CircleAtCaster,
    FanAtCaster,
    RingAtPoint,
}

public enum TargetRelation
{
    Self,
    Allies,
    Enemies,
    Any,
}

public sealed record SkillTarget(
    SkillTargetKind Kind,
    TargetRelation Relation,
    Fix64 Radius,
    Fix64 InnerRadius = default,
    Fix64 MinimumDot = default,
    bool CorpsesOnly = false)
{
    public static SkillTarget Self { get; } =
        new(SkillTargetKind.Self, TargetRelation.Self, Fix64.Zero);

    public static SkillTarget ExplicitEnemy { get; } =
        new(SkillTargetKind.ExplicitTarget, TargetRelation.Enemies, Fix64.Zero);

    public static SkillTarget ExplicitAlly { get; } =
        new(SkillTargetKind.ExplicitTarget, TargetRelation.Allies, Fix64.Zero);

    public static SkillTarget ExplicitDeadAlly { get; } =
        new(SkillTargetKind.ExplicitTarget, TargetRelation.Allies, Fix64.Zero, CorpsesOnly: true);

    public static SkillTarget CurrentEnemy { get; } =
        new(SkillTargetKind.CurrentTarget, TargetRelation.Enemies, Fix64.Zero);

    public static SkillTarget CurrentDeadAlly { get; } =
        new(SkillTargetKind.CurrentTarget, TargetRelation.Allies, Fix64.Zero, CorpsesOnly: true);
}
