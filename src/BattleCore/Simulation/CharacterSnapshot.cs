using BattleCore.Math;

namespace BattleCore.Simulation;

public sealed record CharacterSnapshot(
    long Id,
    string TemplateId,
    int FactionId,
    FixVector2 Position,
    Fix64 Radius,
    Facing Facing,
    long? CurrentTargetId,
    long CurrentHp,
    long MaxHp,
    bool PlayerControlled,
    bool Targetable,
    bool Damageable,
    bool BlocksMovement);
