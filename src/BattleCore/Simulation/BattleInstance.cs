using BattleCore.Attributes;
using BattleCore.Buffs;
using BattleCore.Combat;
using BattleCore.Configuration;
using BattleCore.Math;
using BattleCore.Randomness;
using BattleCore.Simulation.Commands;
using BattleCore.Simulation.Events;
using BattleCore.Skills;

namespace BattleCore.Simulation;

public sealed class BattleInstance
{
    public const int LogicFramesPerSecond = 30;
    private const ulong CombatStream = 1;
    private const ulong AiStream = 2;

    private readonly BattleConfig _config;
    private readonly MapConfig _map;
    private readonly long _maxFrames;
    private readonly SortedDictionary<long, List<QueuedCommand>> _commands = [];
    private readonly SortedDictionary<long, CharacterState> _characters = [];
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
        AdvanceSkills(events);
        AdvanceBuffs(events);
        ResolveCombat(events);
        ResolveLife(events);

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
            case MoveDirectionCommand move:
                WithLiving(move.CharacterId, command, events, character =>
                    character.Movement = MovementIntent.Direction(move.Direction));
                break;
            case MoveToCommand moveTo:
                WithLiving(moveTo.CharacterId, command, events, character =>
                    character.Movement = MovementIntent.Destination(moveTo.Destination));
                break;
            case StopMovingCommand stop:
                WithLiving(stop.CharacterId, command, events, character =>
                    character.Movement = MovementIntent.None);
                break;
            case SetFacingCommand facing:
                WithLiving(facing.CharacterId, command, events, character =>
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
            default:
                Reject(command, events, "Unsupported command.");
                break;
        }
    }

    private void QueueCombat(
        long targetId,
        long sequence,
        BattleCommand command,
        List<BattleEvent> events)
    {
        if (!_characters.ContainsKey(targetId))
        {
            Reject(command, events, $"Character '{targetId}' does not exist.");
            return;
        }

        _pendingCombat.Add(new PendingCombatEffect(sequence, targetId, command));
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
        WithLiving(command.CharacterId, command, events, character =>
        {
            if (command.TargetId is long targetId &&
                (!_characters.TryGetValue(targetId, out var target) || !target.IsSelectable))
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

    private void CastSkill(CastSkillCommand command, List<BattleEvent> events)
    {
        if (!TryGetLiving(command.CasterId, command, events, out var caster))
        {
            return;
        }

        if (!_config.SkillDefinitions.TryGetValue(command.SkillId, out var skill) ||
            !caster.Template.SkillIds.Contains(command.SkillId, StringComparer.Ordinal))
        {
            Reject(command, events, $"Character does not own skill '{command.SkillId}'.");
            return;
        }

        if (caster.HasControl(ControlType.Stun) ||
            (skill.Category == SkillCategory.Spell &&
             caster.HasControl(ControlType.Silence)) ||
            (skill.Category == SkillCategory.Weapon &&
             caster.HasControl(ControlType.Disarm)))
        {
            Reject(command, events, "A control effect prevents casting this skill.");
            return;
        }

        if (caster.ActiveSkill is not null)
        {
            Reject(command, events, "Character is already casting a skill.");
            return;
        }

        if (caster.CooldownReadyFrames.TryGetValue(skill.Id, out var readyFrame) &&
            CurrentFrame < readyFrame)
        {
            Reject(command, events, $"Skill '{skill.Id}' is on cooldown until frame {readyFrame}.");
            return;
        }

        if (skill.Effects.Any(effect =>
                effect.Target.Kind == SkillTargetKind.ExplicitTarget) &&
            (command.TargetId is not long targetId ||
             !_characters.TryGetValue(targetId, out var target) ||
             !target.IsSelectable))
        {
            Reject(command, events, "Skill requires an available explicit target.");
            return;
        }

        if (skill.Effects.Any(effect =>
                effect.Target.Kind is
                    SkillTargetKind.CircleAtPoint or
                    SkillTargetKind.RingAtPoint) &&
            (command.TargetPoint is not FixVector2 point ||
             !_map.Bounds.ContainsCircle(point, Fix64.Zero)))
        {
            Reject(command, events, "Skill requires a target point inside map bounds.");
            return;
        }

        if (skill.Effects.Any(effect =>
                effect.Target.Kind == SkillTargetKind.FanAtCaster) &&
            (command.Direction is not FixVector2 direction ||
             direction == FixVector2.Zero))
        {
            Reject(command, events, "Skill requires a non-zero direction.");
            return;
        }

        var cooldownReadyFrame = CurrentFrame + skill.CooldownFrames;
        caster.CooldownReadyFrames[skill.Id] = cooldownReadyFrame;
        caster.ActiveSkill = new ActiveSkillCast
        {
            Definition = skill,
            StartedFrame = CurrentFrame,
            ExplicitTargetId = command.TargetId,
            TargetPoint = command.TargetPoint,
            Direction = command.Direction?.NormalizeOrZero(),
        };
        events.Add(new SkillStartedEvent(
            CurrentFrame,
            caster.Id,
            skill.Id,
            cooldownReadyFrame));
    }

    private void AdvanceSkills(List<BattleEvent> events)
    {
        foreach (var caster in _characters.Values)
        {
            var cast = caster.ActiveSkill;
            if (cast is null || !caster.CanAct)
            {
                continue;
            }

            var elapsed = checked((int)(CurrentFrame - cast.StartedFrame));
            while (cast.NextEffectIndex < cast.Definition.Effects.Count)
            {
                var effectIndex = cast.NextEffectIndex;
                var effect = cast.Definition.Effects[effectIndex];
                if (effect.Frame > elapsed)
                {
                    break;
                }

                cast.NextEffectIndex++;
                if (effect.Frame == elapsed)
                {
                    ExecuteSkillEffect(caster, cast, effect, effectIndex, events);
                }
            }

            if (elapsed < cast.Definition.DurationFrames - 1)
            {
                continue;
            }

            caster.ActiveSkill = null;
            events.Add(new SkillEndedEvent(
                CurrentFrame,
                caster.Id,
                cast.Definition.Id));
        }
    }

    private void ExecuteSkillEffect(
        CharacterState caster,
        ActiveSkillCast cast,
        SkillEffect effect,
        int effectIndex,
        List<BattleEvent> events)
    {
        foreach (var target in ResolveSkillTargets(caster, cast, effect.Target))
        {
            events.Add(new SkillHitEvent(
                CurrentFrame,
                caster.Id,
                cast.Definition.Id,
                effectIndex,
                target.Id));

            switch (effect)
            {
                case SkillDamageEffect damage:
                    {
                        var command = new DealDamageCommand(
                            CurrentFrame,
                            caster.Id,
                            target.Id,
                            damage.DamageType,
                            damage.Flat,
                            damage.AttributeId,
                            damage.Ratio,
                            damage.CanCrit);
                        QueueCombat(
                            target.Id,
                            _nextCommandSequence++,
                            command,
                            events);
                        break;
                    }
                case SkillHealEffect heal:
                    {
                        var command = new HealCommand(
                            CurrentFrame,
                            caster.Id,
                            target.Id,
                            heal.Flat,
                            heal.AttributeId,
                            heal.Ratio,
                            heal.CanCrit);
                        QueueCombat(
                            target.Id,
                            _nextCommandSequence++,
                            command,
                            events);
                        break;
                    }
                case SkillShieldEffect shield:
                    GrantShield(
                        new GrantShieldCommand(
                            CurrentFrame,
                            target.Id,
                            shield.Amount,
                            shield.DurationFrames),
                        events);
                    break;
                case SkillApplyBuffEffect buff:
                    ApplyBuff(
                        new ApplyBuffCommand(
                            CurrentFrame,
                            caster.Id,
                            target.Id,
                            buff.BuffId),
                        events);
                    break;
            }
        }
    }

    private IReadOnlyList<CharacterState> ResolveSkillTargets(
        CharacterState caster,
        ActiveSkillCast cast,
        SkillTarget target)
    {
        IEnumerable<CharacterState> candidates = target.Kind switch
        {
            SkillTargetKind.Self => [caster],
            SkillTargetKind.ExplicitTarget => ResolveOne(cast.ExplicitTargetId),
            SkillTargetKind.CurrentTarget => ResolveOne(caster.CurrentTargetId),
            SkillTargetKind.CircleAtPoint => ResolveCircle(cast.TargetPoint, target.Radius),
            SkillTargetKind.CircleAtCaster => ResolveCircle(
                caster.Position,
                target.Radius),
            SkillTargetKind.FanAtCaster => ResolveFan(
                caster,
                cast.Direction,
                target.Radius,
                target.MinimumDot),
            SkillTargetKind.RingAtPoint => ResolveRing(
                cast.TargetPoint,
                target.InnerRadius,
                target.Radius),
            _ => [],
        };

        return candidates
            .Where(candidate => IsRelationMatch(caster, candidate, target.Relation))
            .OrderBy(candidate => candidate.Id)
            .ToArray();
    }

    private IEnumerable<CharacterState> ResolveOne(long? characterId)
    {
        if (characterId is long id &&
            _characters.TryGetValue(id, out var character) &&
            character.IsSelectable)
        {
            yield return character;
        }
    }

    private IEnumerable<CharacterState> ResolveCircle(
        FixVector2? point,
        Fix64 radius)
    {
        if (point is not FixVector2 center)
        {
            yield break;
        }

        foreach (var character in _characters.Values)
        {
            if (!character.IsSelectable)
            {
                continue;
            }

            var hitRadius = radius + character.Template.Radius;
            if (FixVector2.DistanceSquared(center, character.Position) <=
                hitRadius * hitRadius)
            {
                yield return character;
            }
        }
    }

    private IEnumerable<CharacterState> ResolveFan(
        CharacterState caster,
        FixVector2? direction,
        Fix64 radius,
        Fix64 minimumDot)
    {
        if (direction is not FixVector2 facing || facing == FixVector2.Zero)
        {
            yield break;
        }

        foreach (var character in ResolveCircle(caster.Position, radius))
        {
            var offset = character.Position - caster.Position;
            if (offset == FixVector2.Zero)
            {
                yield return character;
                continue;
            }

            if (FixVector2.Dot(offset.NormalizeOrZero(), facing) >= minimumDot)
            {
                yield return character;
            }
        }
    }

    private IEnumerable<CharacterState> ResolveRing(
        FixVector2? point,
        Fix64 innerRadius,
        Fix64 outerRadius)
    {
        if (point is not FixVector2 center)
        {
            yield break;
        }

        foreach (var character in ResolveCircle(center, outerRadius))
        {
            var innerExclusionRadius = Fix64.Max(
                Fix64.Zero,
                innerRadius - character.Template.Radius);
            if (FixVector2.DistanceSquared(center, character.Position) >=
                innerExclusionRadius * innerExclusionRadius)
            {
                yield return character;
            }
        }
    }

    private static bool IsRelationMatch(
        CharacterState caster,
        CharacterState target,
        TargetRelation relation) =>
        relation switch
        {
            TargetRelation.Self => caster.Id == target.Id,
            TargetRelation.Allies => caster.FactionId == target.FactionId,
            TargetRelation.Enemies => caster.FactionId != target.FactionId,
            TargetRelation.Any => true,
            _ => false,
        };

    private void ApplyBuff(ApplyBuffCommand command, List<BattleEvent> events)
    {
        if (!TryGetLiving(command.SourceId, command, events, out _) ||
            !TryGetCharacter(command.TargetId, command, events, out var target))
        {
            return;
        }

        if (!target.CanAct)
        {
            Reject(command, events, "Target cannot receive buffs.");
            return;
        }

        if (!_config.BuffDefinitions.TryGetValue(command.BuffId, out var definition))
        {
            Reject(command, events, $"Unknown buff '{command.BuffId}'.");
            return;
        }

        var immunityTags = target.Buffs
            .SelectMany(buff => buff.Definition.ImmunityTags)
            .ToHashSet(StringComparer.Ordinal);
        if (definition.Tags.Overlaps(immunityTags))
        {
            Reject(command, events, $"Target is immune to buff '{definition.Id}'.");
            return;
        }

        if (definition.ReplacesTags.Count > 0)
        {
            foreach (var replaced in target.Buffs
                         .Where(buff => buff.Definition.Tags.Overlaps(
                             definition.ReplacesTags))
                         .OrderBy(buff => buff.InstanceId)
                         .ToArray())
            {
                RemoveBuffInstance(target, replaced, events);
            }
        }

        var controlsBefore = target.Controls;
        var existing = target.Buffs
            .Where(buff => buff.Definition.Id == definition.Id)
            .OrderBy(buff => buff.InstanceId)
            .FirstOrDefault();

        if (existing is not null &&
            definition.StackingRule != BuffStackingRule.Independent)
        {
            switch (definition.StackingRule)
            {
                case BuffStackingRule.Unique:
                    return;
                case BuffStackingRule.Refresh:
                    existing.ExpiryFrame = BuffExpiry(definition);
                    events.Add(new BuffStackChangedEvent(
                        CurrentFrame,
                        target.Id,
                        existing.InstanceId,
                        definition.Id,
                        existing.Stacks));
                    break;
                case BuffStackingRule.Stack:
                    existing.Stacks = System.Math.Min(
                        definition.MaxStacks,
                        existing.Stacks + 1);
                    existing.ExpiryFrame = BuffExpiry(definition);
                    RefreshBuffModifiers(target, existing);
                    events.Add(new BuffStackChangedEvent(
                        CurrentFrame,
                        target.Id,
                        existing.InstanceId,
                        definition.Id,
                        existing.Stacks));
                    break;
            }

            HandleControlChange(target, controlsBefore, events);
            return;
        }

        var instance = new BuffInstance
        {
            InstanceId = _nextBuffId++,
            Definition = definition,
            SourceId = command.SourceId,
            AppliedFrame = CurrentFrame,
            ExpiryFrame = BuffExpiry(definition),
            Stacks = 1,
        };

        for (var index = 0; index < definition.Effects.Count; index++)
        {
            if (definition.Effects[index] is PeriodicBuffEffect periodic)
            {
                instance.NextPeriodicFrames[index] =
                    CurrentFrame + periodic.IntervalFrames;
            }
        }

        target.Buffs.Add(instance);
        RefreshBuffModifiers(target, instance);
        events.Add(new BuffAddedEvent(
            CurrentFrame,
            target.Id,
            instance.InstanceId,
            definition.Id,
            instance.Stacks));
        HandleControlChange(target, controlsBefore, events);
    }

    private void RemoveBuff(RemoveBuffCommand command, List<BattleEvent> events)
    {
        if (!TryGetCharacter(command.TargetId, command, events, out var target))
        {
            return;
        }

        var instance = target.Buffs.FirstOrDefault(
            buff => buff.InstanceId == command.BuffInstanceId);
        if (instance is null)
        {
            Reject(command, events, $"Buff instance '{command.BuffInstanceId}' does not exist.");
            return;
        }

        RemoveBuffInstance(target, instance, events);
    }

    private void DispelBuffs(DispelBuffsCommand command, List<BattleEvent> events)
    {
        if (!TryGetLiving(command.SourceId, command, events, out _) ||
            !TryGetCharacter(command.TargetId, command, events, out var target))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(command.Tag) || command.MaxCount <= 0)
        {
            Reject(command, events, "Dispel requires a tag and a positive max count.");
            return;
        }

        var matches = target.Buffs
            .Where(buff => buff.Definition.Tags.Contains(command.Tag))
            .OrderBy(buff => buff.InstanceId)
            .Take(command.MaxCount)
            .ToArray();
        foreach (var instance in matches)
        {
            RemoveBuffInstance(target, instance, events);
        }
    }

    private long BuffExpiry(BuffDefinition definition) =>
        definition.DurationFrames == 0
            ? long.MaxValue
            : CurrentFrame + definition.DurationFrames;

    private void RefreshBuffModifiers(CharacterState target, BuffInstance instance)
    {
        foreach (var modifierId in instance.ModifierIds)
        {
            target.Attributes.Remove(modifierId);
        }

        instance.ModifierIds.Clear();
        foreach (var effect in instance.Definition.Effects.OfType<AttributeBuffEffect>())
        {
            var modifierId = _nextModifierId++;
            target.Attributes.Add(new AttributeModifier(
                modifierId,
                effect.AttributeId,
                effect.Kind,
                checked(effect.ValuePerStack * instance.Stacks)));
            instance.ModifierIds.Add(modifierId);
        }
    }

    private void AdvanceBuffs(List<BattleEvent> events)
    {
        foreach (var target in _characters.Values)
        {
            foreach (var instance in target.Buffs
                         .OrderBy(buff => buff.InstanceId)
                         .ToArray())
            {
                if (instance.ExpiryFrame <= CurrentFrame)
                {
                    RemoveBuffInstance(target, instance, events);
                    continue;
                }

                for (var effectIndex = 0;
                     effectIndex < instance.Definition.Effects.Count;
                     effectIndex++)
                {
                    if (instance.Definition.Effects[effectIndex] is not
                        PeriodicBuffEffect periodic ||
                        instance.NextPeriodicFrames[effectIndex] > CurrentFrame)
                    {
                        continue;
                    }

                    ExecutePeriodicEffect(
                        target,
                        instance,
                        periodic,
                        events);
                    instance.NextPeriodicFrames[effectIndex] =
                        CurrentFrame + periodic.IntervalFrames;
                }
            }
        }
    }

    private void ExecutePeriodicEffect(
        CharacterState target,
        BuffInstance instance,
        PeriodicBuffEffect effect,
        List<BattleEvent> events)
    {
        switch (effect)
        {
            case PeriodicDamageBuffEffect damage:
                {
                    var command = new DealDamageCommand(
                        CurrentFrame,
                        instance.SourceId,
                        target.Id,
                        damage.DamageType,
                        checked(damage.Flat * instance.Stacks),
                        damage.AttributeId,
                        checked(damage.Ratio * instance.Stacks),
                        damage.CanCrit);
                    QueueCombat(target.Id, _nextCommandSequence++, command, events);
                    break;
                }
            case PeriodicHealBuffEffect heal:
                {
                    var command = new HealCommand(
                        CurrentFrame,
                        instance.SourceId,
                        target.Id,
                        checked(heal.Flat * instance.Stacks),
                        heal.AttributeId,
                        checked(heal.Ratio * instance.Stacks),
                        heal.CanCrit);
                    QueueCombat(target.Id, _nextCommandSequence++, command, events);
                    break;
                }
        }
    }

    private void RemoveBuffInstance(
        CharacterState target,
        BuffInstance instance,
        List<BattleEvent> events)
    {
        var controlsBefore = target.Controls;
        foreach (var modifierId in instance.ModifierIds)
        {
            target.Attributes.Remove(modifierId);
        }

        target.Buffs.Remove(instance);
        events.Add(new BuffRemovedEvent(
            CurrentFrame,
            target.Id,
            instance.InstanceId,
            instance.Definition.Id));
        HandleControlChange(target, controlsBefore, events);
    }

    private void HandleControlChange(
        CharacterState target,
        IReadOnlySet<ControlType> before,
        List<BattleEvent> events)
    {
        if (before.SetEquals(target.Controls))
        {
            return;
        }

        events.Add(new ControlChangedEvent(CurrentFrame, target.Id));
        if (target.HasControl(ControlType.Stun) &&
            target.ActiveSkill is { } cast)
        {
            target.ActiveSkill = null;
            events.Add(new SkillEndedEvent(
                CurrentFrame,
                target.Id,
                cast.Definition.Id));
        }
    }

    private void GrantShield(GrantShieldCommand command, List<BattleEvent> events)
    {
        WithLiving(command.TargetId, command, events, character =>
        {
            if (command.Amount <= 0)
            {
                Reject(command, events, "Shield amount must be positive.");
                return;
            }

            var instanceId = _nextShieldId++;
            var expiry = command.DurationFrames <= 0
                ? long.MaxValue
                : CurrentFrame + command.DurationFrames;
            character.Shields.Add(new ShieldLayer
            {
                InstanceId = instanceId,
                Amount = command.Amount,
                ExpiryFrame = expiry,
            });
            events.Add(new ShieldChangeEvent(
                CurrentFrame,
                character.Id,
                instanceId,
                command.Amount,
                character.ShieldTotal));
        });
    }

    private void Revive(ReviveCommand command, List<BattleEvent> events)
    {
        if (!TryGetLiving(command.SourceId, command, events, out var source))
        {
            return;
        }

        if (!_characters.TryGetValue(command.TargetId, out var target))
        {
            Reject(command, events, $"Character '{command.TargetId}' does not exist.");
            return;
        }

        if (target.LifeState != LifeState.Dead || source.FactionId != target.FactionId)
        {
            Reject(command, events, "Revive requires a dead ally in the corpse window.");
            return;
        }

        StandUp(target, events);
    }

    private void MoveCharacters(List<BattleEvent> events)
    {
        foreach (var character in _characters.Values)
        {
            if (!character.CanMove)
            {
                continue;
            }

            if (character.ActiveSkill is
                {
                    Definition: { CanMoveWhileCasting: false },
                })
            {
                continue;
            }

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

    private void ResolveCombat(List<BattleEvent> events)
    {
        ExpireShields(events);

        foreach (var pending in _pendingCombat
                     .OrderBy(item => item.TargetId)
                     .ThenBy(item => item.Sequence))
        {
            switch (pending.Command)
            {
                case DealDamageCommand damage:
                    ApplyDamage(damage, events);
                    break;
                case HealCommand heal:
                    ApplyHeal(heal, events);
                    break;
            }
        }

        _pendingCombat.Clear();
    }

    private void ApplyDamage(DealDamageCommand command, List<BattleEvent> events)
    {
        if (!TryGetLiving(command.SourceId, command, events, out var source) ||
            !TryGetCharacter(command.TargetId, command, events, out var target))
        {
            return;
        }

        if (!target.Template.Damageable || !target.CanAct)
        {
            Reject(command, events, "Target cannot receive damage.");
            return;
        }

        var hit = ComputeBase(source, command.AttributeId, command.Flat, command.Ratio);
        var wasCrit = false;
        if (command.CanCrit)
        {
            var critRate = System.Math.Clamp(
                source.Attributes.Get(_config.Bindings.CritRate),
                0,
                AttributeSet.RatioScale);
            wasCrit = source.CombatRng.NextInt((int)AttributeSet.RatioScale) < critRate;
            if (wasCrit)
            {
                hit = hit * source.Attributes.Get(_config.Bindings.CritDamage) /
                      AttributeSet.RatioScale;
            }
        }

        if (command.DamageType != DamageType.True)
        {
            var defenseId = command.DamageType == DamageType.Physical
                ? _config.Bindings.PhysicalDefense
                : _config.Bindings.MagicalDefense;
            hit = CombatMath.ApplyMitigation(
                hit,
                target.Attributes.Get(defenseId),
                _config.DefenseK,
                _config.MaximumMitigation);
        }

        hit = CombatMath.ScaleByRate(hit, source.Attributes.Get(_config.Bindings.DamageDealt));
        hit = CombatMath.ScaleByRate(hit, target.Attributes.Get(_config.Bindings.DamageTaken));
        if (hit < 0)
        {
            hit = 0;
        }

        if (hit == 0 && CombatMath.ComputeBase(
                command.Flat,
                ReadAttribute(source, command.AttributeId),
                command.Ratio) > 0)
        {
            hit = _config.MinimumDamage;
        }

        var shieldAbsorbed = AbsorbShield(target, hit, events);
        var hpDamage = hit - shieldAbsorbed;
        if (hpDamage > 0)
        {
            target.CurrentHp -= hpDamage;
        }

        events.Add(new DamageEvent(
            CurrentFrame,
            source.Id,
            target.Id,
            command.DamageType,
            hpDamage,
            shieldAbsorbed,
            wasCrit));

        if (target.CurrentHp <= 0)
        {
            Kill(target, source.Id, events);
        }
    }

    private void ApplyHeal(HealCommand command, List<BattleEvent> events)
    {
        if (!TryGetLiving(command.SourceId, command, events, out var source) ||
            !TryGetCharacter(command.TargetId, command, events, out var target))
        {
            return;
        }

        if (!target.CanAct)
        {
            Reject(command, events, "Target cannot receive healing.");
            return;
        }

        var amount = ComputeBase(source, command.AttributeId, command.Flat, command.Ratio);
        var wasCrit = false;
        if (command.CanCrit)
        {
            var critRate = System.Math.Clamp(
                source.Attributes.Get(_config.Bindings.CritRate),
                0,
                AttributeSet.RatioScale);
            wasCrit = source.CombatRng.NextInt((int)AttributeSet.RatioScale) < critRate;
            if (wasCrit)
            {
                amount = amount * source.Attributes.Get(_config.Bindings.CritDamage) /
                         AttributeSet.RatioScale;
            }
        }

        if (amount <= 0)
        {
            return;
        }

        target.CurrentHp += amount;
        events.Add(new HealEvent(
            CurrentFrame,
            source.Id,
            target.Id,
            amount,
            wasCrit));
    }

    private long ComputeBase(
        CharacterState source,
        string? attributeId,
        long flat,
        long ratio) =>
        CombatMath.ComputeBase(flat, ReadAttribute(source, attributeId), ratio);

    private static long ReadAttribute(CharacterState source, string? attributeId) =>
        attributeId is null ? 0 : source.Attributes.Get(attributeId);

    private void ExpireShields(List<BattleEvent> events)
    {
        foreach (var character in _characters.Values)
        {
            for (var index = character.Shields.Count - 1; index >= 0; index--)
            {
                var layer = character.Shields[index];
                if (layer.ExpiryFrame > CurrentFrame && layer.Amount > 0)
                {
                    continue;
                }

                character.Shields.RemoveAt(index);
                events.Add(new ShieldChangeEvent(
                    CurrentFrame,
                    character.Id,
                    layer.InstanceId,
                    -layer.Amount,
                    character.ShieldTotal));
            }
        }
    }

    private long AbsorbShield(CharacterState target, long hit, List<BattleEvent> events)
    {
        if (hit <= 0 || target.Shields.Count == 0)
        {
            return 0;
        }

        var remaining = hit;
        var absorbed = 0L;
        foreach (var layer in target.Shields
                     .OrderBy(item => item.ExpiryFrame)
                     .ThenBy(item => item.InstanceId)
                     .ToArray())
        {
            if (remaining <= 0)
            {
                break;
            }

            var taken = System.Math.Min(layer.Amount, remaining);
            layer.Amount -= taken;
            remaining -= taken;
            absorbed += taken;
            events.Add(new ShieldChangeEvent(
                CurrentFrame,
                target.Id,
                layer.InstanceId,
                -taken,
                target.ShieldTotal));
        }

        target.Shields.RemoveAll(layer => layer.Amount <= 0);
        return absorbed;
    }

    private void Kill(CharacterState target, long sourceId, List<BattleEvent> events)
    {
        foreach (var buff in target.Buffs.ToArray())
        {
            RemoveBuffInstance(target, buff, events);
        }

        if (target.ActiveSkill is { } cast)
        {
            target.ActiveSkill = null;
            events.Add(new SkillEndedEvent(
                CurrentFrame,
                target.Id,
                cast.Definition.Id));
        }

        target.CurrentHp = 0;
        target.Movement = MovementIntent.None;
        target.CurrentTargetId = null;
        target.Shields.Clear();

        if (target.Lives > 0)
        {
            target.Lives--;
            target.LifeState = LifeState.Reviving;
            target.ReviveAtFrame = CurrentFrame + _config.ReviveDelayFrames;
            target.DespawnAtFrame = null;
            events.Add(new DiedEvent(CurrentFrame, target.Id, sourceId));
            events.Add(new ReviveStartEvent(CurrentFrame, target.Id));
            return;
        }

        target.LifeState = LifeState.Dead;
        target.ReviveAtFrame = null;
        target.DespawnAtFrame = CurrentFrame + _config.CorpseDurationFrames;
        events.Add(new DiedEvent(CurrentFrame, target.Id, sourceId));
        MaybeEmitFactionWiped(target.FactionId, events);
    }

    private void StandUp(CharacterState character, List<BattleEvent> events)
    {
        character.LifeState = LifeState.Alive;
        character.ReviveAtFrame = null;
        character.DespawnAtFrame = null;
        character.CurrentHp = character.Attributes.Get(_config.Bindings.MaxHp);
        events.Add(new RevivedEvent(CurrentFrame, character.Id));
    }

    private void ResolveLife(List<BattleEvent> events)
    {
        foreach (var character in _characters.Values.ToArray())
        {
            if (character.LifeState == LifeState.Reviving &&
                character.ReviveAtFrame is long reviveAt &&
                CurrentFrame >= reviveAt)
            {
                StandUp(character, events);
            }

            if (character.LifeState == LifeState.Dead &&
                character.DespawnAtFrame is long despawnAt &&
                CurrentFrame >= despawnAt)
            {
                _characters.Remove(character.Id);
                events.Add(new CorpseDespawnedEvent(CurrentFrame, character.Id));
                MaybeEmitFactionWiped(character.FactionId, events);
            }
        }

        foreach (var character in _characters.Values)
        {
            if (character.CurrentTargetId is long targetId &&
                (!_characters.TryGetValue(targetId, out var target) || !target.IsSelectable))
            {
                character.CurrentTargetId = null;
                events.Add(new TargetChangedEvent(CurrentFrame, character.Id, null));
            }
        }
    }

    private void MaybeEmitFactionWiped(int factionId, List<BattleEvent> events)
    {
        if (_wipedFactions.Contains(factionId))
        {
            return;
        }

        if (_characters.Values.Any(character =>
                character.FactionId == factionId && character.LifeState != LifeState.Dead))
        {
            return;
        }

        _wipedFactions.Add(factionId);
        events.Add(new FactionWipedEvent(CurrentFrame, factionId));
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
                other.LifeState == LifeState.Dead ||
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
            CastSkillCommand value => value.CasterId,
            DealDamageCommand value => value.SourceId,
            HealCommand value => value.SourceId,
            GrantShieldCommand value => value.TargetId,
            ApplyBuffCommand value => value.SourceId,
            RemoveBuffCommand value => value.TargetId,
            DispelBuffsCommand value => value.SourceId,
            ReviveCommand value => value.SourceId,
            _ => long.MinValue,
        };

    private readonly record struct QueuedCommand(long Sequence, BattleCommand Command);

    private readonly record struct PendingCombatEffect(
        long Sequence,
        long TargetId,
        BattleCommand Command);
}
