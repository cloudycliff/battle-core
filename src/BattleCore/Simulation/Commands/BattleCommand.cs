using BattleCore.Math;

namespace BattleCore.Simulation.Commands;

public abstract record BattleCommand(long Frame);

public sealed record SpawnCharacterCommand(
    long Frame,
    string TemplateId,
    int FactionId,
    FixVector2 Position,
    bool PlayerControlled = false,
    long? InitialHp = null) : BattleCommand(Frame);

public sealed record MoveDirectionCommand(
    long Frame,
    long CharacterId,
    FixVector2 Direction) : BattleCommand(Frame);

public sealed record MoveToCommand(
    long Frame,
    long CharacterId,
    FixVector2 Destination) : BattleCommand(Frame);

public sealed record StopMovingCommand(
    long Frame,
    long CharacterId) : BattleCommand(Frame);

public sealed record SetFacingCommand(
    long Frame,
    long CharacterId,
    Facing Facing) : BattleCommand(Frame);

public sealed record SetTargetCommand(
    long Frame,
    long CharacterId,
    long? TargetId) : BattleCommand(Frame);
