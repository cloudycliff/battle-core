using BattleCore.Math;

namespace BattleCore.Simulation;

public sealed record ProjectileSnapshot(
    long Id,
    string DefinitionId,
    long SourceId,
    FixVector2 Position,
    Fix64 Radius);

public sealed record AreaEffectSnapshot(
    long Id,
    string DefinitionId,
    long SourceId,
    FixVector2 Position,
    Fix64 Radius,
    long? FollowTargetId);
