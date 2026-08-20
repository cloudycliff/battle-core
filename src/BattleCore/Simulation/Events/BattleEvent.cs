using BattleCore.Combat;
using BattleCore.Math;
using BattleCore.Pickups;

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

public sealed record SkillStartedEvent(
    long Frame,
    long CasterId,
    string SkillId,
    long CooldownReadyFrame) : BattleEvent(Frame);

public sealed record SkillHitEvent(
    long Frame,
    long CasterId,
    string SkillId,
    int EffectIndex,
    long TargetId) : BattleEvent(Frame);

public sealed record SkillEndedEvent(
    long Frame,
    long CasterId,
    string SkillId) : BattleEvent(Frame);

public sealed record CommandRejectedEvent(
    long Frame,
    string CommandType,
    string Reason) : BattleEvent(Frame);

public sealed record BattleTimeoutEvent(long Frame) : BattleEvent(Frame);

public sealed record DamageEvent(
    long Frame,
    long SourceId,
    long TargetId,
    DamageType DamageType,
    long Amount,
    long ShieldAbsorbed,
    bool WasCrit) : BattleEvent(Frame);

public sealed record HealEvent(
    long Frame,
    long SourceId,
    long TargetId,
    long Amount,
    bool WasCrit) : BattleEvent(Frame);

public sealed record ShieldChangeEvent(
    long Frame,
    long CharacterId,
    long InstanceId,
    long Amount,
    long Remaining) : BattleEvent(Frame);

public sealed record BuffAddedEvent(
    long Frame,
    long CharacterId,
    long BuffInstanceId,
    string BuffId,
    int Stacks) : BattleEvent(Frame);

public sealed record BuffStackChangedEvent(
    long Frame,
    long CharacterId,
    long BuffInstanceId,
    string BuffId,
    int Stacks) : BattleEvent(Frame);

public sealed record BuffRemovedEvent(
    long Frame,
    long CharacterId,
    long BuffInstanceId,
    string BuffId) : BattleEvent(Frame);

public sealed record ControlChangedEvent(
    long Frame,
    long CharacterId) : BattleEvent(Frame);

public sealed record DiedEvent(
    long Frame,
    long CharacterId,
    long SourceId) : BattleEvent(Frame);

public sealed record ReviveStartEvent(
    long Frame,
    long CharacterId) : BattleEvent(Frame);

public sealed record RevivedEvent(
    long Frame,
    long CharacterId) : BattleEvent(Frame);

public sealed record CorpseDespawnedEvent(
    long Frame,
    long CharacterId) : BattleEvent(Frame);

public sealed record FactionWipedEvent(
    long Frame,
    int FactionId) : BattleEvent(Frame);

public sealed record ProjectileSpawnedEvent(
    long Frame,
    long ProjectileId,
    string DefinitionId,
    long SourceId,
    FixVector2 Position) : BattleEvent(Frame);

public sealed record ProjectileMovedEvent(
    long Frame,
    long ProjectileId,
    FixVector2 From,
    FixVector2 To) : BattleEvent(Frame);

public sealed record ProjectileHitEvent(
    long Frame,
    long ProjectileId,
    long TargetId) : BattleEvent(Frame);

public sealed record ProjectileDespawnedEvent(
    long Frame,
    long ProjectileId) : BattleEvent(Frame);

public sealed record AreaEffectSpawnedEvent(
    long Frame,
    long AreaEffectId,
    string DefinitionId,
    long SourceId,
    FixVector2 Position) : BattleEvent(Frame);

public sealed record AreaEffectTickedEvent(
    long Frame,
    long AreaEffectId) : BattleEvent(Frame);

public sealed record AreaEffectDespawnedEvent(
    long Frame,
    long AreaEffectId) : BattleEvent(Frame);

public sealed record CarryStartedEvent(
    long Frame,
    long CarrierId,
    long CarriedId) : BattleEvent(Frame);

public sealed record CharacterThrownEvent(
    long Frame,
    long CarrierId,
    long CarriedId,
    FixVector2 From,
    FixVector2 To) : BattleEvent(Frame);

public sealed record CarryEndedEvent(
    long Frame,
    long CarrierId,
    long CarriedId) : BattleEvent(Frame);

public sealed record PickupSpawnedEvent(
    long Frame,
    long PickupId,
    string DefinitionId,
    FixVector2 Position) : BattleEvent(Frame);

public sealed record ModifierSourceAppliedEvent(
    long Frame,
    long CharacterId,
    string SourceId) : BattleEvent(Frame);

public sealed record ModifierSourceRemovedEvent(
    long Frame,
    long CharacterId,
    string SourceId) : BattleEvent(Frame);

public sealed record PickedUpEvent(
    long Frame,
    long PickupId,
    string DefinitionId,
    long CharacterId,
    PickupKind Kind) : BattleEvent(Frame);
