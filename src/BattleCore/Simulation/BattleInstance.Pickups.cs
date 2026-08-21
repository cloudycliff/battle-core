using BattleCore.Attributes;
using BattleCore.Math;
using BattleCore.Pickups;
using BattleCore.Simulation.Commands;
using BattleCore.Simulation.Events;

namespace BattleCore.Simulation;

public sealed partial class BattleInstance
{
    private void SpawnPickup(SpawnPickupCommand command, List<BattleEvent> events)
    {
        if (!_config.PickupDefinitions.TryGetValue(command.DefinitionId, out var definition))
        {
            Reject(command, events, $"Unknown pickup '{command.DefinitionId}'.");
            return;
        }

        if (!_map.Bounds.ContainsCircle(command.Position, definition.Radius))
        {
            Reject(command, events, "Pickup position is outside map bounds.");
            return;
        }

        if (definition.Picker == PickupPickerKind.SameFaction &&
            command.FactionId is null)
        {
            Reject(command, events, "Same-faction pickups require a faction id.");
            return;
        }

        var pickup = new PickupState
        {
            Id = _nextEntityId++,
            Definition = definition,
            Position = command.Position,
            FactionId = command.FactionId,
        };
        _pickups.Add(pickup.Id, pickup);
        events.Add(new PickupSpawnedEvent(
            CurrentFrame,
            pickup.Id,
            definition.Id,
            pickup.Position));
    }

    private void ResolvePickups(List<BattleEvent> events)
    {
        var pickedCharacters = new HashSet<long>();
        foreach (var pickup in _pickups.Values.ToArray())
        {
            CharacterState? winner = null;
            foreach (var character in _characters.Values)
            {
                if (!character.CanAct ||
                    pickedCharacters.Contains(character.Id) ||
                    !CanPickUp(pickup, character) ||
                    !IsInPickupRange(pickup, character))
                {
                    continue;
                }

                if (winner is null || character.Id < winner.Id)
                {
                    winner = character;
                }
            }

            if (winner is null)
            {
                continue;
            }

            pickedCharacters.Add(winner.Id);
            ApplyPickup(winner, pickup, events);
        }
    }

    private static bool CanPickUp(PickupState pickup, CharacterState character) =>
        pickup.Definition.Picker switch
        {
            PickupPickerKind.Any => true,
            PickupPickerKind.PlayerControlled => character.PlayerControlled,
            PickupPickerKind.SameFaction =>
                pickup.FactionId is int factionId &&
                character.FactionId == factionId,
            _ => false,
        };

    private static bool IsInPickupRange(PickupState pickup, CharacterState character)
    {
        var reach = pickup.Definition.Radius + character.Template.Radius;
        return FixVector2.DistanceSquared(pickup.Position, character.Position) <=
               reach * reach;
    }

    private void ApplyPickup(
        CharacterState character,
        PickupState pickup,
        List<BattleEvent> events)
    {
        _pickups.Remove(pickup.Id);
        events.Add(new PickedUpEvent(
            CurrentFrame,
            pickup.Id,
            pickup.Definition.Id,
            character.Id,
            pickup.Definition.Kind));

        if (pickup.Definition.Kind != PickupKind.Instant)
        {
            return;
        }

        foreach (var effect in pickup.Definition.Effects)
        {
            switch (effect)
            {
                case PickupHealEffect heal:
                    ApplyHeal(
                        new HealCommand(
                            CurrentFrame,
                            character.Id,
                            character.Id,
                            heal.Flat,
                            heal.AttributeId,
                            heal.Ratio,
                            CanCrit: false),
                        events);
                    break;
                case PickupGrantLivesEffect lives:
                    character.Lives += lives.Amount;
                    break;
                case PickupAttributeEffect attribute:
                    character.Attributes.Add(new AttributeModifier(
                        _nextModifierId++,
                        attribute.AttributeId,
                        attribute.Kind,
                        attribute.Value));
                    break;
                case PickupApplyBuffEffect buff:
                    ApplyBuff(
                        new ApplyBuffCommand(
                            CurrentFrame,
                            character.Id,
                            character.Id,
                            buff.BuffId),
                        events);
                    break;
                case PickupShieldEffect shield:
                    GrantShield(
                        new GrantShieldCommand(
                            CurrentFrame,
                            character.Id,
                            shield.Amount,
                            shield.DurationFrames),
                        events);
                    break;
                case PickupGrantResourceEffect resource:
                    ChangeResource(
                        character,
                        resource.ResourceId,
                        resource.Amount,
                        events,
                        command: null);
                    break;
            }
        }
    }
}
