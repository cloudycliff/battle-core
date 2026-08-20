using BattleCore.Math;
using BattleCore.Pickups;

namespace BattleCore.Simulation;

internal sealed class PickupState
{
    public required long Id { get; init; }
    public required PickupDefinition Definition { get; init; }
    public required FixVector2 Position { get; init; }
}

public sealed record PickupSnapshot(
    long Id,
    string DefinitionId,
    PickupKind Kind,
    FixVector2 Position,
    Fix64 Radius);
