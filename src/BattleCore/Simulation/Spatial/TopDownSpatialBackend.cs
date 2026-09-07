using BattleCore.Combat;
using BattleCore.Configuration;
using BattleCore.Math;

namespace BattleCore.Simulation.Spatial;

/// <summary>The original V1 top-down planar movement rules.</summary>
internal sealed class TopDownSpatialBackend(
    MapConfig map,
    IReadOnlyDictionary<long, CharacterState> characters) : ISpatialBackend
{
    public SpatialMode Mode => SpatialMode.TopDown;

    public FixVector2 NormalizeMoveDirection(FixVector2 direction) =>
        direction.NormalizeOrZero();

    public void InitializeCharacter(CharacterState character)
    {
        character.Grounded = false;
        character.VerticalVelocity = Fix64.Zero;
    }

    public FixVector2 GetFrameDelta(
        CharacterState character,
        Fix64 speedPerFrame,
        bool allowMovementIntent)
    {
        if (!allowMovementIntent)
        {
            return FixVector2.Zero;
        }

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

    public SpatialMoveResult ResolveMovement(
        CharacterState character,
        FixVector2 desiredDelta)
    {
        var fraction = FindMovementFraction(character, desiredDelta);
        return new SpatialMoveResult(
            character.Position + (desiredDelta * fraction),
            fraction < Fix64.One,
            fraction < Fix64.One,
            fraction < Fix64.One);
    }

    public bool HasReachedDestination(
        CharacterState character,
        FixVector2 destination) =>
        character.Position == destination;

    public bool TryJump(CharacterState character) => false;

    public bool IntersectsTerrain(FixVector2 position, Fix64 radius) =>
        SolidTerrainGeometry.Intersects(map.Terrain, position, radius);

    public FixVector2 ClampProjectileMovement(
        FixVector2 from,
        FixVector2 delta,
        Fix64 radius) =>
        SolidTerrainGeometry.ClampSegment(map.Terrain, from, delta, radius);

    private Fix64 FindMovementFraction(
        CharacterState character,
        FixVector2 desiredDelta)
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

    public bool IsPositionValid(
        FixVector2 position,
        Fix64 radius,
        int factionId,
        bool blocksMovement,
        long excludedCharacterId = 0)
    {
        if (!map.Bounds.ContainsCircle(position, radius))
        {
            return false;
        }

        if (IntersectsTerrain(position, radius))
        {
            return false;
        }

        if (!blocksMovement)
        {
            return true;
        }

        foreach (var other in characters.Values)
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
