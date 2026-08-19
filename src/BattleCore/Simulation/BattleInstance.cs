using BattleCore.Attributes;
using BattleCore.Configuration;
using BattleCore.Math;
using BattleCore.Simulation.Commands;
using BattleCore.Simulation.Events;

namespace BattleCore.Simulation;

public sealed class BattleInstance
{
    public const int LogicFramesPerSecond = 30;

    private readonly BattleConfig _config;
    private readonly MapConfig _map;
    private readonly long _maxFrames;
    private readonly SortedDictionary<long, List<QueuedCommand>> _commands = [];
    private readonly SortedDictionary<long, CharacterState> _characters = [];
    private readonly List<BattleEvent> _deferredEvents = [];

    private long _nextEntityId = 1;
    private long _nextCommandSequence;
    private bool _timeoutEmitted;

    public BattleInstance(
        BattleConfig config,
        MapConfig map,
        ulong seed,
        long maxFrames)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxFrames);

        _config = config;
        _map = map;
        _maxFrames = maxFrames;
        Seed = seed;
    }

    public ulong Seed { get; }
    public long CurrentFrame { get; private set; }

    public bool Enqueue(BattleCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Frame < CurrentFrame)
        {
            _deferredEvents.Add(new CommandRejectedEvent(
                CurrentFrame,
                command.GetType().Name,
                $"Command frame {command.Frame} is earlier than current frame {CurrentFrame}."));
            return false;
        }

        if (!_commands.TryGetValue(command.Frame, out var commands))
        {
            commands = [];
            _commands.Add(command.Frame, commands);
        }

        commands.Add(new QueuedCommand(_nextCommandSequence++, command));
        return true;
    }

    public IReadOnlyList<BattleEvent> Tick()
    {
        var events = new List<BattleEvent>(_deferredEvents);
        _deferredEvents.Clear();

        ConsumeCommands(events);
        MoveCharacters(events);

        if (!_timeoutEmitted && CurrentFrame + 1 >= _maxFrames)
        {
            _timeoutEmitted = true;
            events.Add(new BattleTimeoutEvent(CurrentFrame));
        }

        CurrentFrame++;
        return events;
    }

    public CharacterSnapshot? GetCharacter(long characterId) =>
        _characters.TryGetValue(characterId, out var character)
            ? character.Snapshot(_config.Bindings.MaxHp)
            : null;

    public IReadOnlyList<CharacterSnapshot> GetCharacters() =>
        _characters.Values
            .Select(character => character.Snapshot(_config.Bindings.MaxHp))
            .ToArray();

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
            ApplyCommand(queued.Command, events);
        }
    }

    private void ApplyCommand(BattleCommand command, List<BattleEvent> events)
    {
        switch (command)
        {
            case SpawnCharacterCommand spawn:
                Spawn(spawn, events);
                break;
            case MoveDirectionCommand move:
                WithCharacter(move.CharacterId, command, events, character =>
                    character.Movement = MovementIntent.Direction(move.Direction));
                break;
            case MoveToCommand moveTo:
                WithCharacter(moveTo.CharacterId, command, events, character =>
                    character.Movement = MovementIntent.Destination(moveTo.Destination));
                break;
            case StopMovingCommand stop:
                WithCharacter(stop.CharacterId, command, events, character =>
                    character.Movement = MovementIntent.None);
                break;
            case SetFacingCommand facing:
                WithCharacter(facing.CharacterId, command, events, character =>
                {
                    if (character.Facing == facing.Facing)
                    {
                        return;
                    }

                    character.Facing = facing.Facing;
                    events.Add(new FacingChangedEvent(
                        CurrentFrame,
                        character.Id,
                        character.Facing));
                });
                break;
            case SetTargetCommand target:
                SetTarget(target, events);
                break;
            default:
                Reject(command, events, "Unsupported command.");
                break;
        }
    }

    private void Spawn(SpawnCharacterCommand command, List<BattleEvent> events)
    {
        if (!_config.CharacterTemplates.TryGetValue(command.TemplateId, out var template))
        {
            Reject(command, events, $"Unknown character template '{command.TemplateId}'.");
            return;
        }

        if (!IsPositionValid(command.Position, template.Radius, command.FactionId, template.BlocksMovement))
        {
            Reject(command, events, "Spawn position is blocked or outside map bounds.");
            return;
        }

        var attributes = new AttributeSet(template.BaseAttributes);
        var maxHp = attributes.Get(_config.Bindings.MaxHp);
        var character = new CharacterState
        {
            Id = _nextEntityId++,
            Template = template,
            FactionId = command.FactionId,
            Position = command.Position,
            PlayerControlled = command.PlayerControlled,
            Attributes = attributes,
            CurrentHp = command.InitialHp ?? maxHp,
        };

        _characters.Add(character.Id, character);
        events.Add(new CharacterSpawnedEvent(
            CurrentFrame,
            character.Id,
            template.Id,
            character.FactionId,
            character.Position));
    }

    private void SetTarget(SetTargetCommand command, List<BattleEvent> events)
    {
        WithCharacter(command.CharacterId, command, events, character =>
        {
            if (command.TargetId is long targetId &&
                (!_characters.TryGetValue(targetId, out var target) || !target.Template.Targetable))
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

    private void MoveCharacters(List<BattleEvent> events)
    {
        foreach (var character in _characters.Values)
        {
            var desiredDelta = GetDesiredDelta(character);
            if (desiredDelta == FixVector2.Zero)
            {
                continue;
            }

            var from = character.Position;
            var fullDestination = from + desiredDelta;
            var movementFraction = FindMovementFraction(character, desiredDelta);

            var destination = from + (desiredDelta * movementFraction);
            if (movementFraction == Fix64.Zero || destination == from)
            {
                if (character.Movement.Kind == MovementKind.Destination)
                {
                    character.Movement = MovementIntent.None;
                }

                continue;
            }

            character.Position = destination;
            events.Add(new CharacterMovedEvent(
                CurrentFrame,
                character.Id,
                from,
                character.Position));

            if (character.Movement.Kind == MovementKind.Destination)
            {
                var reached = character.Position == fullDestination;
                var blocked = movementFraction < Fix64.One;
                if (reached || blocked)
                {
                    character.Movement = MovementIntent.None;
                }
            }
        }
    }

    private FixVector2 GetDesiredDelta(CharacterState character)
    {
        var speedPerFrame = Fix64.FromRatio(
            character.Attributes.Get(_config.Bindings.MoveSpeed),
            LogicFramesPerSecond);

        return character.Movement.Kind switch
        {
            MovementKind.Direction => character.Movement.Value * speedPerFrame,
            MovementKind.Destination => DeltaToward(
                character.Position,
                character.Movement.Value,
                speedPerFrame),
            _ => FixVector2.Zero,
        };
    }

    private static FixVector2 DeltaToward(
        FixVector2 origin,
        FixVector2 destination,
        Fix64 maximumDistance)
    {
        var offset = destination - origin;
        if (offset == FixVector2.Zero)
        {
            return FixVector2.Zero;
        }

        if (offset.LengthSquared <= maximumDistance * maximumDistance)
        {
            return offset;
        }

        return offset.NormalizeOrZero() * maximumDistance;
    }

    private Fix64 FindMovementFraction(CharacterState character, FixVector2 desiredDelta)
    {
        if (IsPositionValid(
                character.Position + desiredDelta,
                character.Template.Radius,
                character.FactionId,
                character.Template.BlocksMovement,
                character.Id))
        {
            return Fix64.One;
        }

        var low = Fix64.Zero;
        var high = Fix64.One;
        var two = Fix64.FromInt(2);

        for (var iteration = 0; iteration < 16; iteration++)
        {
            var middle = (low + high) / two;
            var candidate = character.Position + (desiredDelta * middle);
            if (IsPositionValid(
                    candidate,
                    character.Template.Radius,
                    character.FactionId,
                    character.Template.BlocksMovement,
                    character.Id))
            {
                low = middle;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

    private bool IsPositionValid(
        FixVector2 position,
        Fix64 radius,
        int factionId,
        bool blocksMovement,
        long excludedCharacterId = 0)
    {
        if (!_map.Bounds.ContainsCircle(position, radius))
        {
            return false;
        }

        if (blocksMovement && _map.Terrain.Any(blocker => blocker.IntersectsCircle(position, radius)))
        {
            return false;
        }

        if (!blocksMovement)
        {
            return true;
        }

        foreach (var other in _characters.Values)
        {
            if (other.Id == excludedCharacterId ||
                !other.Template.BlocksMovement ||
                other.FactionId == factionId)
            {
                continue;
            }

            var combinedRadius = radius + other.Template.Radius;
            if (FixVector2.DistanceSquared(position, other.Position) <
                combinedRadius * combinedRadius)
            {
                return false;
            }
        }

        return true;
    }

    private void WithCharacter(
        long characterId,
        BattleCommand command,
        List<BattleEvent> events,
        Action<CharacterState> action)
    {
        if (!_characters.TryGetValue(characterId, out var character))
        {
            Reject(command, events, $"Character '{characterId}' does not exist.");
            return;
        }

        action(character);
    }

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
            SetFacingCommand value => value.CharacterId,
            SetTargetCommand value => value.CharacterId,
            _ => long.MinValue,
        };

    private readonly record struct QueuedCommand(long Sequence, BattleCommand Command);
}
