using BattleCore.Math;
using BattleCore.Simulation.Events;
using BattleCore.Skills;

namespace BattleCore.Simulation;

public sealed partial class BattleInstance
{
    private void TryStartCarry(
        CharacterState carrier,
        CharacterState target,
        List<BattleEvent> events)
    {
        if (carrier.CarryingId is not null ||
            carrier.CarriedById is not null ||
            carrier.Id == target.Id ||
            carrier.FactionId != target.FactionId ||
            !target.CanAct ||
            target.CarriedById is not null ||
            target.CarryingId is not null)
        {
            return;
        }

        carrier.CarryingId = target.Id;
        target.CarriedById = carrier.Id;
        target.CarryOffset = target.Position - carrier.Position;
        target.Movement = MovementIntent.None;
        events.Add(new CarryStartedEvent(CurrentFrame, carrier.Id, target.Id));
    }

    private void DropCarry(CharacterState carrier, List<BattleEvent> events)
    {
        if (carrier.CarryingId is not long carriedId ||
            !_characters.TryGetValue(carriedId, out var carried))
        {
            ClearCarry(carrier, null);
            return;
        }

        RelocateIfInvalid(carried, carrier);
        EndCarry(carrier, carried, events);
    }

    private void ThrowCarry(
        CharacterState carrier,
        ActiveSkillCast cast,
        SkillThrowCarryEffect effect,
        List<BattleEvent> events)
    {
        if (carrier.CarryingId is not long carriedId ||
            !_characters.TryGetValue(carriedId, out var carried))
        {
            ClearCarry(carrier, null);
            return;
        }

        var from = carried.Position;
        var direction = ResolveThrowDirection(carrier, carried, cast);
        if (direction != FixVector2.Zero)
        {
            var desiredDelta = direction * effect.Distance;
            var fraction = FindMovementFraction(carried, desiredDelta);
            var destination = from + (desiredDelta * fraction);
            if (destination != from)
            {
                carried.Position = destination;
            }
        }

        events.Add(new CharacterThrownEvent(
            CurrentFrame,
            carrier.Id,
            carried.Id,
            from,
            carried.Position));
        RelocateIfInvalid(carried, carrier);
        EndCarry(carrier, carried, events);
    }

    private static FixVector2 ResolveThrowDirection(
        CharacterState carrier,
        CharacterState carried,
        ActiveSkillCast cast)
    {
        if (cast.Direction is { } directed && directed != FixVector2.Zero)
        {
            return directed;
        }

        if (cast.TargetPoint is { } point)
        {
            return (point - carried.Position).NormalizeOrZero();
        }

        return (carried.Position - carrier.Position).NormalizeOrZero();
    }

    private void FollowCarries(List<BattleEvent> events)
    {
        foreach (var carrier in _characters.Values)
        {
            if (carrier.CarryingId is not long carriedId)
            {
                continue;
            }

            if (!_characters.TryGetValue(carriedId, out var carried) ||
                !carried.CanAct)
            {
                ClearCarry(carrier, carried);
                if (carried is not null)
                {
                    events.Add(new CarryEndedEvent(CurrentFrame, carrier.Id, carried.Id));
                }

                continue;
            }

            var destination = carrier.Position + carried.CarryOffset;
            if (!_map.Bounds.ContainsCircle(destination, carried.Template.Radius))
            {
                destination = carrier.Position;
            }

            carried.Movement = MovementIntent.None;
            if (destination == carried.Position)
            {
                continue;
            }

            var from = carried.Position;
            carried.Position = destination;
            events.Add(new CharacterMovedEvent(
                CurrentFrame,
                carried.Id,
                from,
                carried.Position));
        }
    }

    private void EndCarry(
        CharacterState carrier,
        CharacterState carried,
        List<BattleEvent> events)
    {
        if (carrier.CarryingId != carried.Id && carried.CarriedById != carrier.Id)
        {
            return;
        }

        ClearCarry(carrier, carried);
        events.Add(new CarryEndedEvent(CurrentFrame, carrier.Id, carried.Id));
    }

    private static void ClearCarry(CharacterState carrier, CharacterState? carried)
    {
        carrier.CarryingId = null;
        if (carried is null)
        {
            return;
        }

        carried.CarriedById = null;
        carried.CarryOffset = FixVector2.Zero;
        carried.Movement = MovementIntent.None;
    }

    private void ReleaseCarryInvolving(CharacterState character, List<BattleEvent> events)
    {
        if (character.CarryingId is long carriedId &&
            _characters.TryGetValue(carriedId, out var carried))
        {
            RelocateIfInvalid(carried, character);
            EndCarry(character, carried, events);
        }

        if (character.CarriedById is long carrierId &&
            _characters.TryGetValue(carrierId, out var carrier))
        {
            RelocateIfInvalid(character, carrier);
            EndCarry(carrier, character, events);
        }
        else
        {
            character.CarriedById = null;
            character.CarryingId = null;
        }
    }

    private void RelocateIfInvalid(CharacterState carried, CharacterState carrier)
    {
        if (IsPositionValid(
                carried.Position,
                carried.Template.Radius,
                carried.FactionId,
                carried.Template.BlocksMovement,
                carried.Id))
        {
            return;
        }

        carried.Position = carrier.Position;
    }
}
