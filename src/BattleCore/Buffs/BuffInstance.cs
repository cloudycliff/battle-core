namespace BattleCore.Buffs;

internal sealed class BuffInstance
{
    public required long InstanceId { get; init; }
    public required BuffDefinition Definition { get; init; }
    public required long SourceId { get; init; }
    public required long AppliedFrame { get; init; }
    public required long ExpiryFrame { get; set; }
    public required int Stacks { get; set; }

    public bool PreventsRevive =>
        Definition.Effects.OfType<PreventReviveBuffEffect>().Any();

    public List<long> ModifierIds { get; } = [];
    public Dictionary<int, long> NextPeriodicFrames { get; } = [];
}
