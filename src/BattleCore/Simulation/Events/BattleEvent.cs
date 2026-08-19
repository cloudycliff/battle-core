using BattleCore.Math;

namespace BattleCore.Simulation.Events;

public abstract record BattleEvent(long Frame);

public sealed record CharacterSpawnedEvent(
    long Frame,
    long CharacterId,
    string TemplateId,
    int FactionId,
    FixVector2 Position) : BattleEvent(Frame);

public sealed record CharacterMovedEvent(
    long Frame,
    long CharacterId,
    FixVector2 From,
    FixVector2 To) : BattleEvent(Frame);

public sealed record FacingChangedEvent(
    long Frame,
    long CharacterId,
    Facing Facing) : BattleEvent(Frame);

public sealed record TargetChangedEvent(
    long Frame,
    long CharacterId,
    long? TargetId) : BattleEvent(Frame);

public sealed record CommandRejectedEvent(
    long Frame,
    string CommandType,
    string Reason) : BattleEvent(Frame);

public sealed record BattleTimeoutEvent(long Frame) : BattleEvent(Frame);
