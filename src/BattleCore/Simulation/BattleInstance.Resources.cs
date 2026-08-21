using BattleCore.Simulation.Commands;
using BattleCore.Simulation.Events;

namespace BattleCore.Simulation;

public sealed partial class BattleInstance
{
    private void GrantResource(GrantResourceCommand command, List<BattleEvent> events)
    {
        WithLiving(command.CharacterId, command, events, character =>
            ChangeResource(character, command.ResourceId, command.Amount, events, command));
    }

    private bool TrySpendResources(
        CharacterState character,
        IReadOnlyDictionary<string, long> costs,
        List<BattleEvent> events,
        BattleCommand command)
    {
        foreach (var (resourceId, amount) in costs)
        {
            if (character.Resources.GetValueOrDefault(resourceId) < amount)
            {
                Reject(command, events, $"Not enough '{resourceId}' to cast.");
                return false;
            }
        }

        foreach (var (resourceId, amount) in costs)
        {
            ChangeResource(character, resourceId, -amount, events, command);
        }

        return true;
    }

    private void ChangeResource(
        CharacterState character,
        string resourceId,
        long delta,
        List<BattleEvent> events,
        BattleCommand? command)
    {
        if (!_config.ResourceDefinitions.TryGetValue(resourceId, out var definition))
        {
            if (command is not null)
            {
                Reject(command, events, $"Unknown resource '{resourceId}'.");
            }

            return;
        }

        var current = character.Resources.GetValueOrDefault(resourceId);
        var next = current + delta;
        if (next < 0)
        {
            next = 0;
        }

        if (next > definition.Maximum)
        {
            next = definition.Maximum;
        }

        if (next == current)
        {
            return;
        }

        character.Resources[resourceId] = next;
        events.Add(new ResourceChangedEvent(
            CurrentFrame,
            character.Id,
            resourceId,
            next - current,
            next));
    }
}
