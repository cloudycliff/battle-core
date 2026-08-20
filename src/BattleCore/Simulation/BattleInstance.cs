using BattleCore.AreaEffects;
using BattleCore.Configuration;
using BattleCore.Math;
using BattleCore.Projectiles;
using BattleCore.Simulation.Commands;
using BattleCore.Simulation.Events;

namespace BattleCore.Simulation;

public sealed partial class BattleInstance
{
    public const int LogicFramesPerSecond = 30;
    private const ulong CombatStream = 1;
    private const ulong AiStream = 2;

    private readonly BattleConfig _config;
    private readonly MapConfig _map;
    private readonly SpawnTable _spawnTable;
    private readonly long _maxFrames;
    private readonly SortedDictionary<long, List<ScheduledSpawn>> _scheduledSpawns = [];
    private readonly SortedDictionary<long, List<QueuedCommand>> _commands = [];
    private readonly SortedDictionary<long, CharacterState> _characters = [];
    private readonly SortedDictionary<long, ProjectileState> _projectiles = [];
    private readonly SortedDictionary<long, AreaEffectState> _areaEffects = [];
    private readonly SortedDictionary<long, PickupState> _pickups = [];
    private readonly List<BattleEvent> _deferredEvents = [];
    private readonly List<PendingCombatEffect> _pendingCombat = [];
    private readonly HashSet<int> _wipedFactions = [];

    private long _nextEntityId = 1;
    private long _nextCommandSequence;
    private long _nextShieldId = 1;
    private long _nextBuffId = 1;
    private long _nextModifierId = 1;
    private bool _timeoutEmitted;

    public BattleInstance(
        BattleConfig config,
        MapConfig map,
        ulong seed,
        long maxFrames,
        SpawnTable? spawnTable = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxFrames);

        _config = config;
        _map = map;
        _spawnTable = spawnTable ?? SpawnTable.Empty;
        _maxFrames = maxFrames;
        Seed = seed;
        ValidateSpawnSetup();
        BuildSpawnSchedule();
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

        EnqueueScheduledSpawns();
        ConsumeCommands(events);
        RunBrains(events);
        MoveCharacters(events);
        AdvanceSkills(events);
        AdvanceProjectiles(events);
        AdvanceAreaEffects(events);
        AdvanceBuffs(events);
        ResolveCombat(events);
        ResolveLife(events);
        ResolvePickups(events);

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

    public long? GetAttribute(long characterId, string attributeId) =>
        _characters.TryGetValue(characterId, out var character)
            ? character.Attributes.Get(attributeId)
            : null;

    public IReadOnlyList<CharacterSnapshot> GetCharacters() =>
        _characters.Values
            .Select(character => character.Snapshot(_config.Bindings.MaxHp))
            .ToArray();

    public IReadOnlyList<ProjectileSnapshot> GetProjectiles() =>
        _projectiles.Values
            .Select(projectile => new ProjectileSnapshot(
                projectile.Id,
                projectile.Definition.Id,
                projectile.SourceId,
                projectile.Position,
                projectile.Definition.Radius))
            .ToArray();

    public IReadOnlyList<AreaEffectSnapshot> GetAreaEffects() =>
        _areaEffects.Values
            .Select(area => new AreaEffectSnapshot(
                area.Id,
                area.Definition.Id,
                area.SourceId,
                area.Position,
                area.Definition.Radius,
                area.FollowTargetId))
            .ToArray();

    public IReadOnlyList<PickupSnapshot> GetPickups() =>
        _pickups.Values
            .Select(pickup => new PickupSnapshot(
                pickup.Id,
                pickup.Definition.Id,
                pickup.Definition.Kind,
                pickup.Position,
                pickup.Definition.Radius,
                pickup.Definition.Picker,
                pickup.FactionId))
            .ToArray();

    public IReadOnlyList<SpawnPoint> GetSpawnPoints() => _map.SpawnPoints;

    public IReadOnlyList<PlayerSpawnMarker> GetPlayerSpawnMarkers() =>
        _map.PlayerSpawnMarkers;

    private void ValidateSpawnSetup()
    {
        var errors = new List<string>();

        foreach (var entry in _spawnTable.Entries.Values)
        {
            if (!_config.CharacterTemplates.ContainsKey(entry.TemplateId))
            {
                errors.Add(
                    $"Spawn table entry '{entry.Id}' references unknown template '{entry.TemplateId}'.");
            }

            if (entry.BrainId is string brainId &&
                !_config.BrainDefinitions.ContainsKey(brainId))
            {
                errors.Add(
                    $"Spawn table entry '{entry.Id}' references unknown brain '{brainId}'.");
            }
        }

        foreach (var point in _map.SpawnPoints)
        {
            if (!_spawnTable.Entries.ContainsKey(point.TableEntryId))
            {
                errors.Add(
                    $"Spawn point '{point.Id}' references unknown table entry '{point.TableEntryId}'.");
            }
        }

        if (errors.Count > 0)
        {
            throw new ConfigurationException(errors);
        }
    }

    private void BuildSpawnSchedule()
    {
        foreach (var point in _map.SpawnPoints.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            var entry = _spawnTable.Entries[point.TableEntryId];
            if (!_scheduledSpawns.TryGetValue(entry.Frame, out var spawns))
            {
                spawns = [];
                _scheduledSpawns.Add(entry.Frame, spawns);
            }

            for (var index = 0; index < entry.Count; index++)
            {
                spawns.Add(new ScheduledSpawn(
                    entry.TemplateId,
                    entry.FactionId,
                    point.Position,
                    entry.BrainId));
            }
        }
    }

    private void EnqueueScheduledSpawns()
    {
        if (!_scheduledSpawns.Remove(CurrentFrame, out var spawns))
        {
            return;
        }

        foreach (var spawn in spawns)
        {
            Enqueue(new SpawnCharacterCommand(
                CurrentFrame,
                spawn.TemplateId,
                spawn.FactionId,
                spawn.Position,
                BrainId: spawn.BrainId));
        }
    }

    private readonly record struct ScheduledSpawn(
        string TemplateId,
        int FactionId,
        FixVector2 Position,
        string? BrainId);
}
