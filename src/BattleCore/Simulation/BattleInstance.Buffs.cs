using BattleCore.Attributes;
using BattleCore.Buffs;
using BattleCore.Combat;
using BattleCore.Randomness;
using BattleCore.Simulation.Commands;
using BattleCore.Simulation.Events;

namespace BattleCore.Simulation;

public sealed partial class BattleInstance
{

    private void ApplyBuff(
        ApplyBuffCommand command,
        List<BattleEvent> events,
        CombatSourceSnapshot? snapshot = null)
    {
        if (snapshot is null &&
            !TryGetLiving(command.SourceId, command, events, out _) ||
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

        // Damageable=false means "takes neither damage nor control" (CONTEXT
        // Combat Flag); the damage path already refuses, so must control.
        if (!target.Template.Damageable &&
            definition.Effects.Any(effect => effect is ControlBuffEffect))
        {
            Reject(command, events, "Target cannot receive control effects.");
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
                    existing.CombatSnapshot = CaptureBuffCombatSnapshot(
                        command.SourceId,
                        existing.Rng,
                        snapshot) ?? existing.CombatSnapshot;
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
                    existing.CombatSnapshot = CaptureBuffCombatSnapshot(
                        command.SourceId,
                        existing.Rng,
                        snapshot) ?? existing.CombatSnapshot;
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

        var instanceId = _nextBuffId++;
        var rng = DeterministicRng.Derive(Seed, instanceId, BuffStream);
        var instance = new BuffInstance
        {
            InstanceId = instanceId,
            Definition = definition,
            SourceId = command.SourceId,
            AppliedFrame = CurrentFrame,
            ExpiryFrame = BuffExpiry(definition),
            Stacks = 1,
            Rng = rng,
            CombatSnapshot = CaptureBuffCombatSnapshot(command.SourceId, rng, snapshot),
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
            // Buffs can land on any character; skip modifiers for attributes
            // the target's template never declared instead of throwing.
            if (!target.Attributes.Contains(effect.AttributeId))
            {
                continue;
            }

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
                // A tick due on the expiry frame still fires, then the buff
                // goes: Duration == Interval means exactly one tick, not zero.
                for (var effectIndex = 0;
                     effectIndex < instance.Definition.Effects.Count &&
                     target.Buffs.Contains(instance);
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

                if (instance.ExpiryFrame <= CurrentFrame && target.Buffs.Contains(instance))
                {
                    RemoveBuffInstance(target, instance, events);
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
                    QueueCombat(
                        target.Id,
                        _nextCommandSequence++,
                        command,
                        events,
                        instance.CombatSnapshot);
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
            case PeriodicResourceBuffEffect resource:
                ChangeResource(
                    target,
                    resource.ResourceId,
                    checked(resource.Amount * instance.Stacks),
                    events,
                    command: null);
                break;
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

        // Losing the ability to move drops the current intent, mirroring how
        // a stun cancels the skill timeline: control ending must not make the
        // unit silently resume a walk nobody re-issued.
        if (!target.CanMove && target.Movement.Kind != MovementKind.None)
        {
            target.Movement = MovementIntent.None;
        }

        if (target.HasControl(ControlType.Stun) &&
            target.ActiveSkill is { } cast)
        {
            target.ActiveSkill = null;
            events.Add(new SkillEndedEvent(
                CurrentFrame,
                target.Id,
                cast.Definition.Id));
        }

        if (target.HasControl(ControlType.Stun) &&
            target.CarryingId is not null)
        {
            DropCarry(target, events);
        }
    }

    private CombatSourceSnapshot? CaptureBuffCombatSnapshot(
        long sourceId,
        DeterministicRng rng,
        CombatSourceSnapshot? provided)
    {
        if (provided is not null)
        {
            return provided.WithRng(rng);
        }

        return TryGetActingSource(sourceId) is { } source
            ? CaptureCombatSnapshot(source, rng)
            : null;
    }
}
