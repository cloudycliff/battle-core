using BattleCore.Attributes;
using BattleCore.Configuration;
using BattleCore.Math;

namespace BattleCore.Simulation;

internal sealed class CharacterState
{
    public required long Id { get; init; }
    public required CharacterTemplate Template { get; init; }
    public required int FactionId { get; init; }
    public required FixVector2 Position { get; set; }
    public required bool PlayerControlled { get; init; }
    public required AttributeSet Attributes { get; init; }
    public required long CurrentHp { get; set; }

    public Facing Facing { get; set; } = new(0);
    public long? CurrentTargetId { get; set; }
    public MovementIntent Movement { get; set; } = MovementIntent.None;

    public CharacterSnapshot Snapshot(string maxHpAttributeId) =>
        new(
            Id,
            Template.Id,
            FactionId,
            Position,
            Template.Radius,
            Facing,
            CurrentTargetId,
            CurrentHp,
            Attributes.Get(maxHpAttributeId),
            PlayerControlled,
            Template.Targetable,
            Template.Damageable,
            Template.BlocksMovement);
}

internal enum MovementKind
{
    None,
    Direction,
    Destination,
}

internal readonly record struct MovementIntent(
    MovementKind Kind,
    FixVector2 Value)
{
    public static readonly MovementIntent None = new(MovementKind.None, FixVector2.Zero);

    public static MovementIntent Direction(FixVector2 value) =>
        new(MovementKind.Direction, value.NormalizeOrZero());

    public static MovementIntent Destination(FixVector2 value) =>
        new(MovementKind.Destination, value);
}
