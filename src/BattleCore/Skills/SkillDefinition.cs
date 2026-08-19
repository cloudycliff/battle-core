using BattleCore.Math;

namespace BattleCore.Skills;

public enum SkillCategory
{
    Spell,
    Weapon,
    Unrestricted,
}

public sealed class SkillDefinition
{
    public SkillDefinition(
        string id,
        int durationFrames,
        int cooldownFrames,
        IEnumerable<SkillEffect> effects,
        bool canMoveWhileCasting = false,
        SkillCategory category = SkillCategory.Spell)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(durationFrames);
        ArgumentOutOfRangeException.ThrowIfNegative(cooldownFrames);
        ArgumentNullException.ThrowIfNull(effects);

        Id = id;
        DurationFrames = durationFrames;
        CooldownFrames = cooldownFrames;
        Effects = effects
            .Select((effect, index) => new IndexedEffect(index, effect))
            .OrderBy(item => item.Effect.Frame)
            .ThenBy(item => item.Index)
            .Select(item => item.Effect)
            .ToArray();
        CanMoveWhileCasting = canMoveWhileCasting;
        Category = category;

        ValidateEffects();
    }

    public string Id { get; }
    public int DurationFrames { get; }
    public int CooldownFrames { get; }
    public IReadOnlyList<SkillEffect> Effects { get; }
    public bool CanMoveWhileCasting { get; }
    public SkillCategory Category { get; }

    private void ValidateEffects()
    {
        foreach (var effect in Effects)
        {
            if (effect.Frame < 0 || effect.Frame >= DurationFrames)
            {
                throw new ArgumentException(
                    $"Skill '{Id}' effect frame {effect.Frame} must be within [0, {DurationFrames - 1}].");
            }

            if (effect.Target.Kind is
                    SkillTargetKind.CircleAtPoint or
                    SkillTargetKind.CircleAtCaster or
                    SkillTargetKind.FanAtCaster or
                    SkillTargetKind.RingAtPoint &&
                effect.Target.Radius <= Fix64.Zero)
            {
                throw new ArgumentException(
                    $"Skill '{Id}' area target radius must be positive.");
            }

            if (effect.Target.Kind == SkillTargetKind.RingAtPoint &&
                (effect.Target.InnerRadius < Fix64.Zero ||
                 effect.Target.InnerRadius >= effect.Target.Radius))
            {
                throw new ArgumentException(
                    $"Skill '{Id}' ring inner radius must be non-negative and below its outer radius.");
            }

            if (effect.Target.Kind == SkillTargetKind.FanAtCaster &&
                (effect.Target.MinimumDot < -Fix64.One ||
                 effect.Target.MinimumDot > Fix64.One))
            {
                throw new ArgumentException(
                    $"Skill '{Id}' fan minimum dot must be within [-1, 1].");
            }

            if (effect is SkillShieldEffect shield &&
                (shield.Amount <= 0 || shield.DurationFrames < 0))
            {
                throw new ArgumentException(
                    $"Skill '{Id}' shield amount must be positive and duration non-negative.");
            }
        }
    }

    private readonly record struct IndexedEffect(int Index, SkillEffect Effect);
}
