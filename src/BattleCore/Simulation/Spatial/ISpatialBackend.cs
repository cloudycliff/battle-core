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

    void InitializeCharacter(CharacterState character);

    FixVector2 GetFrameDelta(
        CharacterState character,
        Fix64 speedPerFrame,
        bool allowMovementIntent);

    SpatialMoveResult ResolveMovement(
        CharacterState character,
        FixVector2 desiredDelta);

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

internal readonly record struct SpatialMoveResult(
    FixVector2 Destination,
    bool BlockedX,
    bool BlockedY,
    bool MovementIntentBlocked)
{
    public bool Blocked => BlockedX || BlockedY;
}
