using BattleCore.Attributes;
using BattleCore.Combat;
using BattleCore.Math;

namespace BattleCore.Simulation.Commands;

public abstract record BattleCommand(long Frame);

public sealed record SpawnCharacterCommand(
    long Frame,
    string TemplateId,
    int FactionId,
    FixVector2 Position,
    bool PlayerControlled = false,
    long? InitialHp = null,
    string? BrainId = null,
    long? OwnerId = null,
    IReadOnlyList<ModifierSource>? ModifierSources = null) : BattleCommand(Frame);

public sealed record SpawnPickupCommand(
    long Frame,
    string DefinitionId,
    FixVector2 Position,
    int? FactionId = null) : BattleCommand(Frame);

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

public sealed record CastSkillCommand(
    long Frame,
    long CasterId,
    string SkillId,
    long? TargetId = null,
    FixVector2? TargetPoint = null,
    FixVector2? Direction = null) : BattleCommand(Frame);

public sealed record DealDamageCommand(
    long Frame,
    long SourceId,
    long TargetId,
    DamageType DamageType,
    long Flat,
    string? AttributeId = null,
    long Ratio = 0,
    bool CanCrit = true) : BattleCommand(Frame);

public sealed record HealCommand(
    long Frame,
    long SourceId,
    long TargetId,
    long Flat,
    string? AttributeId = null,
    long Ratio = 0,
    bool CanCrit = true) : BattleCommand(Frame);

public sealed record GrantShieldCommand(
    long Frame,
    long TargetId,
    long Amount,
    int DurationFrames) : BattleCommand(Frame);

public sealed record ApplyBuffCommand(
    long Frame,
    long SourceId,
    long TargetId,
    string BuffId) : BattleCommand(Frame);

public sealed record RemoveBuffCommand(
    long Frame,
    long TargetId,
    long BuffInstanceId) : BattleCommand(Frame);

public sealed record DispelBuffsCommand(
    long Frame,
    long SourceId,
    long TargetId,
    string Tag,
    int MaxCount = 1) : BattleCommand(Frame);

public sealed record ReviveCommand(
    long Frame,
    long SourceId,
    long TargetId) : BattleCommand(Frame);

public sealed record ApplyModifierSourceCommand(
    long Frame,
    long CharacterId,
    string SourceId,
    IReadOnlyList<ModifierContribution> Modifiers) : BattleCommand(Frame);

public sealed record RemoveModifierSourceCommand(
    long Frame,
    long CharacterId,
    string SourceId) : BattleCommand(Frame);

public sealed record GrantResourceCommand(
    long Frame,
    long CharacterId,
    string ResourceId,
    long Amount) : BattleCommand(Frame);
