using BattleCore.AreaEffects;
using BattleCore.Combat;
using BattleCore.Math;

namespace BattleCore.Simulation;

internal sealed class AreaEffectState
{
    public required long Id { get; init; }
    public required AreaEffectDefinition Definition { get; init; }
    public required long SourceId { get; init; }
    public required int FactionId { get; init; }
    public required FixVector2 Position { get; set; }
    public required long? FollowTargetId { get; init; }
    public required long NextTickFrame { get; set; }
    public required long ExpiryFrame { get; init; }
    public required CombatSourceSnapshot CombatSnapshot { get; init; }
}
