using BattleCore.Combat;
using BattleCore.Math;
using BattleCore.Simulation.Events;

namespace BattleCore.Simulation;

public sealed partial class BattleInstance
{

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

        FollowCarries(events);
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
        var maximumAxisDistance = Fix64.Max(
            Fix64.Abs(desiredDelta.X),
            Fix64.Abs(desiredDelta.Y));
        var sampleDistanceRaw = System.Math.Max(
            1,
            character.Template.Radius.RawValue / 2);
        var sampleCount = maximumAxisDistance.RawValue / sampleDistanceRaw;
        if (maximumAxisDistance.RawValue % sampleDistanceRaw != 0)
        {
            sampleCount++;
        }

        sampleCount = System.Math.Max(1, sampleCount);
        var previous = Fix64.Zero;
        var two = Fix64.FromInt(2);
        for (long sample = 1; sample <= sampleCount; sample++)
        {
            var fraction = Fix64.FromRatio(sample, sampleCount);
            var candidate = character.Position + (desiredDelta * fraction);
            if (IsPositionValid(
                    candidate,
                    character.Template.Radius,
                    character.FactionId,
                    character.Template.BlocksMovement,
                    character.Id))
            {
                previous = fraction;
                continue;
            }

            var low = previous;
            var high = fraction;
            for (var iteration = 0; iteration < 16; iteration++)
            {
                var middle = (low + high) / two;
                candidate = character.Position + (desiredDelta * middle);
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

        return Fix64.One;
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

        // Terrain blocks every unit; the blocksMovement flag only opts a unit
        // out of unit-vs-unit blocking (CONTEXT: Movement Blocking).
        if (_map.Terrain.Any(blocker => blocker.IntersectsCircle(position, radius)))
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
                other.CarriedById is not null ||
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
}
