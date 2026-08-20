using BattleCore.Effects;
using BattleCore.Math;
using BattleCore.Skills;

namespace BattleCore.Projectiles;

public sealed class ProjectileDefinition
{
    public ProjectileDefinition(
        string id,
        Fix64 speed,
        Fix64 radius,
        int lifetimeFrames,
        int pierceCount,
        TargetRelation relation,
        IEnumerable<HitEffect> hitEffects)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lifetimeFrames);
        ArgumentOutOfRangeException.ThrowIfNegative(pierceCount);
        ArgumentNullException.ThrowIfNull(hitEffects);

        if (speed <= Fix64.Zero)
        {
            throw new ArgumentException("Projectile speed must be positive.", nameof(speed));
        }

        if (radius <= Fix64.Zero)
        {
            throw new ArgumentException("Projectile radius must be positive.", nameof(radius));
        }

        Id = id;
        Speed = speed;
        Radius = radius;
        LifetimeFrames = lifetimeFrames;
        PierceCount = pierceCount;
        Relation = relation;
        HitEffects = hitEffects.ToArray();
    }

    public string Id { get; }
    public Fix64 Speed { get; }
    public Fix64 Radius { get; }
    public int LifetimeFrames { get; }
    public int PierceCount { get; }
    public TargetRelation Relation { get; }
    public IReadOnlyList<HitEffect> HitEffects { get; }
}
