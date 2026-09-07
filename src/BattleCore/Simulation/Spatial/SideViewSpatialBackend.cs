using BattleCore.Combat;
using BattleCore.Configuration;
using BattleCore.Math;

namespace BattleCore.Simulation.Spatial;

/// <summary>
/// Deterministic side-view movement: X is horizontal, positive Y is up.
/// Characters retain circular V1 occupancy for this first backend.
/// </summary>
internal sealed class SideViewSpatialBackend(
    MapConfig map,
    IReadOnlyDictionary<long, CharacterState> characters) : ISpatialBackend
{
    private static readonly Fix64 ContactTolerance = Fix64.FromRaw(2);
    private readonly SideViewSettings _settings = map.SideView!;

    public SpatialMode Mode => SpatialMode.SideView;

    public FixVector2 NormalizeMoveDirection(FixVector2 direction)
    {
        if (direction.X > Fix64.Zero)
        {
            return new FixVector2(Fix64.One, Fix64.Zero);
        }

        if (direction.X < Fix64.Zero)
        {
            return new FixVector2(-Fix64.One, Fix64.Zero);
        }

        return FixVector2.Zero;
    }

    public FixVector2 ClampAim(FixVector2 direction, Facing facing)
    {
        if (direction == FixVector2.Zero)
        {
            return FixVector2.Zero;
        }

        if (direction.X > Fix64.Zero)
        {
            return new FixVector2(Fix64.One, Fix64.Zero);
        }

        if (direction.X < Fix64.Zero)
        {
            return new FixVector2(-Fix64.One, Fix64.Zero);
        }

        return HorizontalFromFacing(facing);
    }

    public Facing ClampFacing(Facing facing) =>
        IsFacingLeft(facing) ? new Facing(512) : new Facing(0);

    public bool UpdatesFacingFromMovement => true;

    public Fix64 RangeDistanceSquared(FixVector2 from, FixVector2 to)
    {
        var horizontal = from.X - to.X;
        return horizontal * horizontal;
    }

    public FixAabb CreateAttackBox(
        FixVector2 origin,
        Facing facing,
        Fix64 length,
        Fix64 height) =>
        FacingAttackBox.Create(origin, ClampFacing(facing), length, height);

    private static bool IsFacingLeft(Facing facing) =>
        facing.Step > 256 && facing.Step < 768;

    private static FixVector2 HorizontalFromFacing(Facing facing) =>
        IsFacingLeft(facing)
            ? new FixVector2(-Fix64.One, Fix64.Zero)
            : new FixVector2(Fix64.One, Fix64.Zero);

    public void InitializeCharacter(CharacterState character)
    {
        character.VerticalVelocity = Fix64.Zero;
        character.Grounded = IsSupported(character.Position, character.Template.Radius);
    }

    public FixVector2 GetFrameDelta(
        CharacterState character,
        Fix64 speedPerFrame,
        bool allowMovementIntent)
    {
        var horizontal = allowMovementIntent
            ? HorizontalDelta(character, speedPerFrame)
            : Fix64.Zero;

        if (character.Grounded &&
            !IsSupported(character.Position, character.Template.Radius))
        {
            character.Grounded = false;
        }

        if (!character.Grounded)
        {
            var gravityPerFrame = _settings.Gravity /
                                  Fix64.FromInt(BattleInstance.LogicFramesPerSecond);
            character.VerticalVelocity -= gravityPerFrame;
            if (character.VerticalVelocity < -_settings.MaximumFallSpeed)
            {
                character.VerticalVelocity = -_settings.MaximumFallSpeed;
            }
        }

        var vertical = character.VerticalVelocity /
                       Fix64.FromInt(BattleInstance.LogicFramesPerSecond);
        return new FixVector2(horizontal, vertical);
    }

    public SpatialMoveResult ResolveMovement(
        CharacterState character,
        FixVector2 desiredDelta,
        SpatialDisplacementKind kind)
    {
        var slide = desiredDelta;
        if (kind == SpatialDisplacementKind.Forced)
        {
            ApplyVerticalImpulse(character, desiredDelta.Y);
            slide = new FixVector2(
                desiredDelta.X,
                character.VerticalVelocity /
                Fix64.FromInt(BattleInstance.LogicFramesPerSecond));
        }

        return Slide(character, slide);
    }

    private void ApplyVerticalImpulse(CharacterState character, Fix64 heightDelta)
    {
        if (heightDelta == Fix64.Zero)
        {
            return;
        }

        if (heightDelta < Fix64.Zero && character.Grounded)
        {
            return;
        }

        var launchSpeed = Fix64.Sqrt(
            Fix64.FromInt(2) * _settings.Gravity * Fix64.Abs(heightDelta));
        if (heightDelta > Fix64.Zero)
        {
            character.Grounded = false;
            character.VerticalVelocity += launchSpeed;
            return;
        }

        character.VerticalVelocity -= launchSpeed;
        if (character.VerticalVelocity < -_settings.MaximumFallSpeed)
        {
            character.VerticalVelocity = -_settings.MaximumFallSpeed;
        }
    }

    private SpatialMoveResult Slide(CharacterState character, FixVector2 desiredDelta)
    {
        var start = character.Position;
        var horizontalDelta = new FixVector2(desiredDelta.X, Fix64.Zero);
        var horizontalFraction = FindMovementFraction(
            character,
            start,
            horizontalDelta);
        var afterHorizontal = start + (horizontalDelta * horizontalFraction);

        var verticalDelta = new FixVector2(Fix64.Zero, desiredDelta.Y);
        var verticalFraction = FindMovementFraction(
            character,
            afterHorizontal,
            verticalDelta);
        var destination = afterHorizontal + (verticalDelta * verticalFraction);
        var blockedY = verticalFraction < Fix64.One;

        if (blockedY)
        {
            character.VerticalVelocity = Fix64.Zero;
        }

        character.Grounded = character.VerticalVelocity <= Fix64.Zero &&
                             IsSupported(destination, character.Template.Radius);

        return new SpatialMoveResult(
            destination,
            horizontalFraction < Fix64.One,
            blockedY,
            horizontalFraction < Fix64.One);
    }

    public bool HasReachedDestination(
        CharacterState character,
        FixVector2 destination) =>
        character.Position.X == destination.X;

    public bool TryJump(CharacterState character)
    {
        if (!character.Grounded)
        {
            return false;
        }

        character.Grounded = false;
        character.VerticalVelocity = _settings.JumpSpeed;
        return true;
    }

    public bool IntersectsTerrain(FixVector2 position, Fix64 radius) =>
        SolidTerrainGeometry.Intersects(map.Terrain, position, radius);

    public FixVector2 ClampProjectileMovement(
        FixVector2 from,
        FixVector2 delta,
        Fix64 radius) =>
        SolidTerrainGeometry.ClampSegment(map.Terrain, from, delta, radius);

    public bool IsPositionValid(
        FixVector2 position,
        Fix64 radius,
        int factionId,
        bool blocksMovement,
        long excludedCharacterId = 0)
    {
        if (!map.Bounds.ContainsCircle(position, radius) ||
            IntersectsTerrain(position, radius))
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

    private Fix64 HorizontalDelta(CharacterState character, Fix64 maximumDistance) =>
        character.Movement.Kind switch
        {
            MovementKind.Direction => character.Movement.Value.X * maximumDistance,
            MovementKind.Destination => Fix64.Clamp(
                character.Movement.Value.X - character.Position.X,
                -maximumDistance,
                maximumDistance),
            _ => Fix64.Zero,
        };

    private Fix64 FindMovementFraction(
        CharacterState character,
        FixVector2 start,
        FixVector2 desiredDelta)
    {
        if (desiredDelta == FixVector2.Zero)
        {
            return Fix64.One;
        }

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
            var candidate = start + (desiredDelta * fraction);
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
                candidate = start + (desiredDelta * middle);
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

    private bool IsSupported(FixVector2 position, Fix64 radius)
    {
        var bottom = position.Y - radius;
        if (Fix64.Abs(bottom - map.Bounds.Min.Y) <= ContactTolerance)
        {
            return true;
        }

        return map.Terrain.Any(blocker =>
            Fix64.Abs(bottom - blocker.Max.Y) <= ContactTolerance &&
            position.X + radius > blocker.Min.X &&
            position.X - radius < blocker.Max.X);
    }
}
