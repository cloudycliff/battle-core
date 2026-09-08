using BattleCore.Configuration;
using BattleCore.Math;

namespace BattleCore.Simulation.Spatial;

/// <summary>
/// Owns mode-specific movement and occupancy rules. Combat systems use this
/// boundary instead of interpreting map axes or terrain directly.
/// </summary>
internal interface ISpatialBackend
{
    SpatialMode Mode { get; }

    FixVector2 NormalizeMoveDirection(FixVector2 direction);

    /// <summary>
    /// SideView keeps only ±X; a purely vertical aim keeps the current facing.
    /// TopDown returns the normalized vector unchanged.
    /// </summary>
    FixVector2 ClampAim(FixVector2 direction, Facing facing);

    Facing ClampFacing(Facing facing);

    bool UpdatesFacingFromMovement { get; }

    /// <summary>
    /// Measures gameplay distance for leash, owner-follow, and nearest-target
    /// ordering. Occupancy and hit geometry retain their own checks.
    /// </summary>
    Fix64 RangeDistanceSquared(FixVector2 from, FixVector2 to);

    /// <summary>
    /// Whether <paramref name="to"/> is a reachable target for casts and Brain
    /// aggro/attack decisions at the given reach. SideView additionally
    /// requires the vertical gap to stay within the map's vertical reach, so
    /// shapeless single-target skills cannot hit through floors.
    /// </summary>
    bool IsWithinReach(FixVector2 from, FixVector2 to, Fix64 reach);

    FixAabb CreateAttackBox(
        FixVector2 origin,
        Facing facing,
        Fix64 length,
        Fix64 height);

    void InitializeCharacter(CharacterState character);

    FixVector2 GetFrameDelta(
        CharacterState character,
        Fix64 speedPerFrame,
        bool allowMovementIntent);

    SpatialMoveResult ResolveMovement(
        CharacterState character,
        FixVector2 desiredDelta,
        SpatialDisplacementKind kind);

    bool HasReachedDestination(
        CharacterState character,
        FixVector2 destination);

    bool TryJump(CharacterState character);

    bool IntersectsTerrain(FixVector2 position, Fix64 radius);

    FixVector2 ClampProjectileMovement(
        FixVector2 from,
        FixVector2 delta,
        Fix64 radius);

    bool IsPositionValid(
        FixVector2 position,
        Fix64 radius,
        int factionId,
        bool blocksMovement,
        long excludedCharacterId = 0);
}

internal enum SpatialDisplacementKind
{
    Locomotion,
    Forced,
}

internal readonly record struct SpatialMoveResult(
    FixVector2 Destination,
    bool BlockedX,
    bool BlockedY,
    bool MovementIntentBlocked)
{
    public bool Blocked => BlockedX || BlockedY;
}
