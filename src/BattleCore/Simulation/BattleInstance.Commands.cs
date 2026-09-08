using BattleCore.Attributes;
using BattleCore.Brains;
using BattleCore.Combat;
using BattleCore.Math;
using BattleCore.Randomness;
using BattleCore.Simulation.Commands;
using BattleCore.Simulation.Events;

namespace BattleCore.Simulation;

public sealed partial class BattleInstance
{

    private void ConsumeCommands(List<BattleEvent> events)
    {
        if (!_commands.Remove(CurrentFrame, out var commands))
        {
            return;
        }

        foreach (var queued in commands
                     .OrderBy(item => CommandEntityId(item.Command))
                     .ThenBy(item => item.Sequence))
        {
            ApplyCommand(queued.Command, queued.Sequence, events);
        }
    }

    private void ApplyCommand(BattleCommand command, long sequence, List<BattleEvent> events)
    {
        switch (command)
        {
            case SpawnCharacterCommand spawn:
                Spawn(spawn, events);
                break;
            case SpawnPickupCommand spawnPickup:
                SpawnPickup(spawnPickup, events);
                break;
            case MoveDirectionCommand move:
                WithLiving(move.CharacterId, command, events, character =>
                {
                    if (!character.CanMove)
                    {
                        Reject(command, events, "Character cannot move while controlled or carried.");
                        return;
                    }

                    var direction = _space.NormalizeMoveDirection(move.Direction);
                    character.Movement = MovementIntent.Direction(direction);
                    if (_space.UpdatesFacingFromMovement)
                    {
                        FaceToward(character, direction, events);
                    }
                });
                break;
            case MoveToCommand moveTo:
                WithLiving(moveTo.CharacterId, command, events, character =>
                {
                    if (!character.CanMove)
                    {
                        Reject(command, events, "Character cannot move while controlled or carried.");
                        return;
                    }

                    if (!IsInsideMap(moveTo.Destination))
                    {
                        Reject(command, events, "MoveTo destination must lie inside map bounds.");
                        return;
                    }

                    character.Movement = MovementIntent.Destination(moveTo.Destination);
                });
                break;
            case StopMovingCommand stop:
                WithLiving(stop.CharacterId, command, events, character =>
                    character.Movement = MovementIntent.None);
                break;
            case JumpCommand jump:
                WithLiving(jump.CharacterId, command, events, character =>
                {
                    var movementLocked = !character.CanMove ||
                        character.ActiveSkill is
                        {
                            Definition: { CanMoveWhileCasting: false },
                        };
                    if (movementLocked || !_space.TryJump(character))
                    {
                        Reject(command, events, "Jump requires a grounded, movable SideView character.");
                    }
                });
                break;
            case SetFacingCommand facing:
                WithLiving(facing.CharacterId, command, events, character =>
                    ApplyFacing(character, facing.Facing, events));
                break;
            case SetTargetCommand target:
                SetTarget(target, events);
                break;
            case CastSkillCommand cast:
                CastSkill(cast, events);
                break;
            case DealDamageCommand damage:
                QueueCombat(damage.TargetId, sequence, command, events);
                break;
            case HealCommand heal:
                QueueCombat(heal.TargetId, sequence, command, events);
                break;
            case GrantShieldCommand shield:
                GrantShield(shield, events);
                break;
            case ApplyBuffCommand applyBuff:
                ApplyBuff(applyBuff, events);
                break;
            case RemoveBuffCommand removeBuff:
                RemoveBuff(removeBuff, events);
                break;
            case DispelBuffsCommand dispel:
                DispelBuffs(dispel, events);
                break;
            case ReviveCommand revive:
                Revive(revive, events);
                break;
            case ApplyModifierSourceCommand applySource:
                ApplyModifierSource(applySource, events);
                break;
            case RemoveModifierSourceCommand removeSource:
                RemoveModifierSource(removeSource, events);
                break;
            case GrantResourceCommand grantResource:
                GrantResource(grantResource, events);
                break;
            default:
                Reject(command, events, "Unsupported command.");
                break;
        }
    }

    private void QueueCombat(
        long targetId,
        long sequence,
        BattleCommand command,
        List<BattleEvent> events,
        CombatSourceSnapshot? snapshot = null)
    {
        if (!_characters.ContainsKey(targetId))
        {
            Reject(command, events, $"Character '{targetId}' does not exist.");
            return;
        }

        _pendingCombat.Add(new PendingCombatEffect(sequence, targetId, command, snapshot));
    }

    private void Spawn(SpawnCharacterCommand command, List<BattleEvent> events)
    {
        if (!_config.CharacterTemplates.TryGetValue(command.TemplateId, out var template))
        {
            Reject(command, events, $"Unknown character template '{command.TemplateId}'.");
            return;
        }

        if (!IsInsideMap(command.Position) ||
            !IsPositionValid(command.Position, template.Radius, command.FactionId, template.BlocksMovement))
        {
            Reject(command, events, "Spawn position is blocked or outside map bounds.");
            return;
        }

        var attributes = new AttributeSet(template.BaseAttributes);
        var maxHp = attributes.Get(_config.Bindings.MaxHp);
        var brainId = command.BrainId ?? template.BrainId;
        BrainDefinition? brain = null;
        if (brainId is not null)
        {
            if (!_config.BrainDefinitions.TryGetValue(brainId, out brain))
            {
                Reject(command, events, $"Unknown brain '{brainId}'.");
                return;
            }
        }

        if (command.OwnerId is long ownerId && !_characters.ContainsKey(ownerId))
        {
            Reject(command, events, $"Owner '{ownerId}' does not exist.");
            return;
        }

        if (!TryValidateModifierSources(command.ModifierSources, command, events))
        {
            return;
        }

        var id = _nextEntityId++;
        var character = new CharacterState
        {
            Id = id,
            Template = template,
            FactionId = command.FactionId,
            Position = command.Position,
            PlayerControlled = command.PlayerControlled,
            Attributes = attributes,
            CurrentHp = command.InitialHp ?? maxHp,
            CombatRng = DeterministicRng.Derive(Seed, id, CombatStream),
            AiRng = DeterministicRng.Derive(Seed, id, AiStream),
            Lives = template.Lives,
            Brain = brain,
            OwnerId = command.OwnerId,
            SpawnOrigin = command.Position,
        };
        _space.InitializeCharacter(character);
        foreach (var resource in _config.ResourceDefinitions.Values)
        {
            character.Resources[resource.Id] =
                template.BaseResources.GetValueOrDefault(resource.Id);
        }

        _characters.Add(character.Id, character);
        events.Add(new CharacterSpawnedEvent(
            CurrentFrame,
            character.Id,
            template.Id,
            character.FactionId,
            character.Position));
        AttachSpawnModifierSources(character, command.ModifierSources, events);
    }

    private void SetTarget(SetTargetCommand command, List<BattleEvent> events)
    {
        WithLiving(command.CharacterId, command, events, character =>
        {
            if (command.TargetId is long targetId &&
                (!_characters.TryGetValue(targetId, out var target) ||
                 (!target.IsSelectable && !target.IsReviveTarget)))
            {
                Reject(command, events, $"Target '{targetId}' is unavailable.");
                return;
            }

            if (character.CurrentTargetId == command.TargetId)
            {
                return;
            }

            character.CurrentTargetId = command.TargetId;
            events.Add(new TargetChangedEvent(
                CurrentFrame,
                character.Id,
                character.CurrentTargetId));
        });
    }
    private void WithLiving(
        long characterId,
        BattleCommand command,
        List<BattleEvent> events,
        Action<CharacterState> action)
    {
        if (!TryGetLiving(characterId, command, events, out var character))
        {
            return;
        }

        action(character);
    }

    private bool TryGetLiving(
        long characterId,
        BattleCommand command,
        List<BattleEvent> events,
        out CharacterState character)
    {
        if (!TryGetCharacter(characterId, command, events, out character))
        {
            return false;
        }

        if (!character.CanAct)
        {
            Reject(command, events, $"Character '{characterId}' cannot act.");
            character = null!;
            return false;
        }

        return true;
    }

    private bool TryGetCharacter(
        long characterId,
        BattleCommand command,
        List<BattleEvent> events,
        out CharacterState character)
    {
        if (_characters.TryGetValue(characterId, out character!))
        {
            return true;
        }

        Reject(command, events, $"Character '{characterId}' does not exist.");
        return false;
    }

    private void FaceToward(
        CharacterState character,
        FixVector2 direction,
        List<BattleEvent> events)
    {
        var aim = _space.ClampAim(direction, character.Facing);
        if (aim == FixVector2.Zero)
        {
            return;
        }

        ApplyFacing(character, Facing.FromDirection(aim), events);
    }

    private void ApplyFacing(
        CharacterState character,
        Facing facing,
        List<BattleEvent> events)
    {
        facing = _space.ClampFacing(facing);
        if (character.Facing == facing)
        {
            return;
        }

        character.Facing = facing;
        events.Add(new FacingChangedEvent(CurrentFrame, character.Id, facing));
    }

    /// <summary>
    /// Host-supplied points must lie inside the map. Besides being the only
    /// meaningful destination, this bounds every distance the core squares
    /// afterwards so fixed-point math cannot overflow on absurd input.
    /// </summary>
    private bool IsInsideMap(FixVector2 point) =>
        _map.Bounds.ContainsCircle(point, Fix64.Zero);

    private void Reject(BattleCommand command, List<BattleEvent> events, string reason) =>
        events.Add(new CommandRejectedEvent(
            CurrentFrame,
            command.GetType().Name,
            reason));

    private static long CommandEntityId(BattleCommand command) =>
        command switch
        {
            MoveDirectionCommand value => value.CharacterId,
            MoveToCommand value => value.CharacterId,
            StopMovingCommand value => value.CharacterId,
            JumpCommand value => value.CharacterId,
            SetFacingCommand value => value.CharacterId,
            SetTargetCommand value => value.CharacterId,
            CastSkillCommand value => value.CasterId,
            DealDamageCommand value => value.SourceId,
            HealCommand value => value.SourceId,
            GrantShieldCommand value => value.TargetId,
            ApplyBuffCommand value => value.SourceId,
            RemoveBuffCommand value => value.TargetId,
            DispelBuffsCommand value => value.SourceId,
            ReviveCommand value => value.SourceId,
            ApplyModifierSourceCommand value => value.CharacterId,
            RemoveModifierSourceCommand value => value.CharacterId,
            GrantResourceCommand value => value.CharacterId,
            _ => long.MinValue,
        };

    private readonly record struct QueuedCommand(long Sequence, BattleCommand Command);

    private readonly record struct PendingCombatEffect(
        long Sequence,
        long TargetId,
        BattleCommand Command,
        CombatSourceSnapshot? Snapshot);
}
