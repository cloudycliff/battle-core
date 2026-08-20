using BattleCore.Effects;
using BattleCore.Math;
using BattleCore.Skills;

namespace BattleCore.AreaEffects;

public sealed class AreaEffectDefinition
{
    public AreaEffectDefinition(
        string id,
        Fix64 radius,
        int durationFrames,
        int intervalFrames,
        TargetRelation relation,
        IEnumerable<HitEffect> hitEffects,
        bool followsTarget = false,
        bool tickOnSpawn = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(durationFrames);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(intervalFrames);
        ArgumentNullException.ThrowIfNull(hitEffects);

        if (radius <= Fix64.Zero)
        {
            throw new ArgumentException("Area effect radius must be positive.", nameof(radius));
        }

        Id = id;
        Radius = radius;
        DurationFrames = durationFrames;
        IntervalFrames = intervalFrames;
        Relation = relation;
        HitEffects = hitEffects.ToArray();
        FollowsTarget = followsTarget;
        TickOnSpawn = tickOnSpawn;
    }

    public string Id { get; }
    public Fix64 Radius { get; }
    public int DurationFrames { get; }
    public int IntervalFrames { get; }
    public TargetRelation Relation { get; }
    public IReadOnlyList<HitEffect> HitEffects { get; }
    public bool FollowsTarget { get; }
    public bool TickOnSpawn { get; }
}
