using BattleCore.Attributes;
using BattleCore.Simulation.Commands;
using BattleCore.Simulation.Events;

namespace BattleCore.Simulation;

public sealed partial class BattleInstance
{
    private void ApplyModifierSource(
        ApplyModifierSourceCommand command,
        List<BattleEvent> events)
    {
        if (!TryGetCharacter(command.CharacterId, command, events, out var character))
        {
            return;
        }

        if (!TryValidateModifierSource(
                command.SourceId,
                command.Modifiers,
                command,
                events))
        {
            return;
        }

        ReplaceModifierSource(character, command.SourceId, command.Modifiers, events);
    }

    private void RemoveModifierSource(
        RemoveModifierSourceCommand command,
        List<BattleEvent> events)
    {
        if (!TryGetCharacter(command.CharacterId, command, events, out var character))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(command.SourceId))
        {
            Reject(command, events, "Modifier source id is required.");
            return;
        }

        if (!TryRemoveModifierSource(character, command.SourceId, events))
        {
            Reject(
                command,
                events,
                $"Modifier source '{command.SourceId}' is not attached.");
        }
    }

    private bool TryValidateModifierSources(
        IReadOnlyList<ModifierSource>? sources,
        BattleCommand command,
        List<BattleEvent> events)
    {
        if (sources is null || sources.Count == 0)
        {
            return true;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            if (!seen.Add(source.SourceId))
            {
                Reject(
                    command,
                    events,
                    $"Duplicate modifier source '{source.SourceId}'.");
                return false;
            }

            if (!TryValidateModifierSource(
                    source.SourceId,
                    source.Modifiers,
                    command,
                    events))
            {
                return false;
            }
        }

        return true;
    }

    private bool TryValidateModifierSource(
        string sourceId,
        IReadOnlyList<ModifierContribution> modifiers,
        BattleCommand command,
        List<BattleEvent> events)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            Reject(command, events, "Modifier source id is required.");
            return false;
        }

        if (modifiers is null || modifiers.Count == 0)
        {
            Reject(command, events, "Modifier source must include at least one contribution.");
            return false;
        }

        foreach (var contribution in modifiers)
        {
            if (string.IsNullOrWhiteSpace(contribution.AttributeId) ||
                !_config.AttributeIds.Contains(contribution.AttributeId))
            {
                Reject(
                    command,
                    events,
                    $"Unknown attribute '{contribution.AttributeId}'.");
                return false;
            }
        }

        return true;
    }

    private void AttachSpawnModifierSources(
        CharacterState character,
        IReadOnlyList<ModifierSource>? sources,
        List<BattleEvent> events)
    {
        if (sources is null)
        {
            return;
        }

        foreach (var source in sources)
        {
            ReplaceModifierSource(character, source.SourceId, source.Modifiers, events);
        }
    }

    private void ReplaceModifierSource(
        CharacterState character,
        string sourceId,
        IReadOnlyList<ModifierContribution> modifiers,
        List<BattleEvent> events)
    {
        TryRemoveModifierSource(character, sourceId, events);

        var instanceIds = new List<long>(modifiers.Count);
        foreach (var contribution in modifiers)
        {
            var instanceId = _nextModifierId++;
            character.Attributes.Add(new AttributeModifier(
                instanceId,
                contribution.AttributeId,
                contribution.Kind,
                contribution.Value));
            instanceIds.Add(instanceId);
        }

        character.ModifierSources[sourceId] = instanceIds;
        events.Add(new ModifierSourceAppliedEvent(
            CurrentFrame,
            character.Id,
            sourceId));
    }

    private bool TryRemoveModifierSource(
        CharacterState character,
        string sourceId,
        List<BattleEvent> events)
    {
        if (!character.ModifierSources.Remove(sourceId, out var instanceIds))
        {
            return false;
        }

        foreach (var instanceId in instanceIds)
        {
            character.Attributes.Remove(instanceId);
        }

        events.Add(new ModifierSourceRemovedEvent(
            CurrentFrame,
            character.Id,
            sourceId));
        return true;
    }
}
