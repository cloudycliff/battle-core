using BattleCore.Combat;
using BattleCore.Math;
using BattleCore.Simulation.Events;
using BattleCore.Simulation.Spatial;

namespace BattleCore.Simulation;

public sealed partial class BattleInstance
{

    private void MoveCharacters(List<BattleEvent> events)
    {
        foreach (var character in _characters.Values)
        {
            if (!character.CanAct || character.CarriedById is not null)
            {
                continue;
            }

            var allowMovementIntent = character.CanMove &&
                character.ActiveSkill is not
                {
                    Definition: { CanMoveWhileCasting: false },
                };

            // Slows can push the modified value below zero; a negative speed
            // must mean "cannot move", not "walks backwards" (TopDown) or an
            // inverted clamp range (SideView).
            var speedPerFrame = Fix64.FromRatio(
                System.Math.Max(0, character.Attributes.Get(_config.Bindings.MoveSpeed)),
                LogicFramesPerSecond);
            var desiredDelta = _space.GetFrameDelta(
                character,
                speedPerFrame,
                allowMovementIntent);
            if (desiredDelta == FixVector2.Zero)
            {
                continue;
            }

            var from = character.Position;
            var movement = _space.ResolveMovement(
                character,
                desiredDelta,
                SpatialDisplacementKind.Locomotion);
            var destination = movement.Destination;
            if (destination == from)
            {
                if (character.Movement.Kind == MovementKind.Destination)
                {
                    character.Movement = MovementIntent.None;
                    character.LeashReturnBlocked |= character.ReturningToLeash;
                }

                continue;
            }

            character.Position = destination;
            events.Add(new CharacterMovedEvent(
                CurrentFrame,
                character.Id,
                from,
                character.Position));

            if (_space.UpdatesFacingFromMovement)
            {
                FaceToward(character, destination - from, events);
            }

            if (character.Movement.Kind == MovementKind.Destination)
            {
                var reached = _space.HasReachedDestination(
                    character,
                    character.Movement.Value);
                if (reached || movement.MovementIntentBlocked)
                {
                    character.Movement = MovementIntent.None;
                    character.LeashReturnBlocked |=
                        movement.MovementIntentBlocked && character.ReturningToLeash;
                }
            }
        }

        FollowCarries(events);
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

    private bool IsPositionValid(
        FixVector2 position,
        Fix64 radius,
        int factionId,
        bool blocksMovement,
        long excludedCharacterId = 0)
        => _space.IsPositionValid(
            position,
            radius,
            factionId,
            blocksMovement,
            excludedCharacterId);
}
