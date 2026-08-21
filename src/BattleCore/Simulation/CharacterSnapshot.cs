using BattleCore.Combat;
using BattleCore.Buffs;
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
    int Lives,
    LifeState LifeState,
    long Shield,
    string? ActiveSkillId,
    IReadOnlyDictionary<string, long> CooldownReadyFrames,
    IReadOnlyList<BuffSnapshot> Buffs,
    IReadOnlySet<ControlType> Controls,
    bool PlayerControlled,
    bool Targetable,
    bool Damageable,
    bool BlocksMovement,
    bool CanBeRevived,
    long? OwnerId,
    long? CarryingId,
    long? CarriedById,
    IReadOnlyList<string> ModifierSourceIds,
    IReadOnlyDictionary<string, long> Resources);
