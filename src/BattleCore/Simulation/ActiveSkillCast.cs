using BattleCore.Math;
using BattleCore.Skills;

namespace BattleCore.Simulation;

internal sealed class ActiveSkillCast
{
    public required SkillDefinition Definition { get; init; }
    public required long StartedFrame { get; init; }
    public required long? ExplicitTargetId { get; init; }
    public required FixVector2? TargetPoint { get; init; }
    public required FixVector2? Direction { get; init; }
    public int NextEffectIndex { get; set; }
}
