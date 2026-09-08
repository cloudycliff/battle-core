using BattleCore.Attributes;
using BattleCore.Brains;
using BattleCore.Buffs;
using BattleCore.Combat;
using BattleCore.Configuration;
using BattleCore.Math;
using BattleCore.Randomness;

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
    public required DeterministicRng CombatRng { get; init; }
    public required DeterministicRng AiRng { get; init; }
    public required int Lives { get; set; }
    public BrainDefinition? Brain { get; init; }
    public long? OwnerId { get; init; }
    public long? CarryingId { get; set; }
    public long? CarriedById { get; set; }
    public FixVector2 CarryOffset { get; set; }
    public Fix64 VerticalVelocity { get; set; }
    public bool Grounded { get; set; }

    public Facing Facing { get; set; } = new(0);
    public FixVector2 SpawnOrigin { get; set; }
    public bool ReturningToLeash { get; set; }
    public bool LeashReturnBlocked { get; set; }
    public long? CurrentTargetId { get; set; }
    public MovementIntent Movement { get; set; } = MovementIntent.None;
    public LifeState LifeState { get; set; } = LifeState.Alive;
    public long? ReviveAtFrame { get; set; }
    public long? DespawnAtFrame { get; set; }
    public List<ShieldLayer> Shields { get; } = [];
    public ActiveSkillCast? ActiveSkill { get; set; }
    public Dictionary<string, long> CooldownReadyFrames { get; } =
        new(StringComparer.Ordinal);
    public List<BuffInstance> Buffs { get; } = [];
    public Dictionary<string, List<long>> ModifierSources { get; } =
        new(StringComparer.Ordinal);
    public Dictionary<string, long> Resources { get; } =
        new(StringComparer.Ordinal);

    public bool CanAct => LifeState == LifeState.Alive;

    public bool CanMove =>
        CanAct &&
        CarriedById is null &&
        !HasControl(ControlType.Stun) &&
        !HasControl(ControlType.Root);

    public bool IsSelectable =>
        LifeState == LifeState.Alive &&
        Template.Targetable &&
        !HasUntargetable;

    public bool HasImmuneToDamage =>
        Buffs.Any(buff => buff.Definition.Effects.OfType<ImmuneToDamageBuffEffect>().Any());

    public bool HasUntargetable =>
        Buffs.Any(buff => buff.Definition.Effects.OfType<UntargetableBuffEffect>().Any());

    public bool CanBeRevived =>
        Template.CanBeRevived &&
        !Buffs.Any(buff => buff.PreventsRevive);

    public bool IsReviveTarget =>
        LifeState == LifeState.Dead && CanBeRevived;

    public long ShieldTotal => Shields.Sum(layer => layer.Amount);

    public bool HasControl(ControlType control) =>
        Buffs.Any(buff => buff.Definition.Effects
            .OfType<ControlBuffEffect>()
            .Any(effect => effect.Control == control));

    public IReadOnlySet<ControlType> Controls =>
        Buffs.SelectMany(buff => buff.Definition.Effects)
            .OfType<ControlBuffEffect>()
            .Select(effect => effect.Control)
            .ToHashSet();

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
            Lives,
            LifeState,
            ShieldTotal,
            ActiveSkill?.Definition.Id,
            new Dictionary<string, long>(CooldownReadyFrames, StringComparer.Ordinal),
            Buffs.OrderBy(buff => buff.InstanceId)
                .Select(buff => new BuffSnapshot(
                    buff.InstanceId,
                    buff.Definition.Id,
                    buff.Stacks,
                    buff.ExpiryFrame))
                .ToArray(),
            Controls,
            PlayerControlled,
            IsSelectable,
            Template.Damageable && CanAct && !HasImmuneToDamage,
            Template.BlocksMovement,
            CanBeRevived,
            OwnerId,
            CarryingId,
            CarriedById,
            ModifierSources.Keys.ToArray(),
            new Dictionary<string, long>(Resources, StringComparer.Ordinal),
            Grounded,
            VerticalVelocity);
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
        new(MovementKind.Direction, value);

    public static MovementIntent Destination(FixVector2 value) =>
        new(MovementKind.Destination, value);
}
