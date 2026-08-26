using BattleCore.Math;

namespace BattleCore.Brains;

public enum BrainKind
{
    Idle,
    ChaseAttack,
    HoldPosition,
    FollowOwner,
}

public sealed class BrainDefinition
{
    public BrainDefinition(
        string id,
        BrainKind kind,
        Fix64? aggroRadius = null,
        Fix64? attackRange = null,
        Fix64? followDistance = null,
        Fix64? leashRadius = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        Id = id;
        Kind = kind;
        AggroRadius = aggroRadius ?? Fix64.FromInt(12);
        AttackRange = attackRange ?? Fix64.One;
        FollowDistance = followDistance ?? Fix64.FromInt(2);
        LeashRadius = leashRadius ?? Fix64.Zero;

        if (Kind != BrainKind.Idle)
        {
            if (AggroRadius <= Fix64.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(aggroRadius),
                    "Aggro radius must be positive.");
            }

            if (AttackRange < Fix64.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(attackRange),
                    "Attack range cannot be negative.");
            }
        }

        if (Kind == BrainKind.FollowOwner && FollowDistance < Fix64.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(followDistance),
                "Follow distance cannot be negative.");
        }

        if (LeashRadius < Fix64.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(leashRadius),
                "Leash radius cannot be negative.");
        }
    }

    public string Id { get; }
    public BrainKind Kind { get; }
    public Fix64 AggroRadius { get; }
    public Fix64 AttackRange { get; }
    public Fix64 FollowDistance { get; }
    public Fix64 LeashRadius { get; }
}
