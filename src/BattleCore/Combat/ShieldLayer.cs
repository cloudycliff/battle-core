namespace BattleCore.Combat;

internal sealed class ShieldLayer
{
    public required long InstanceId { get; init; }
    public required long Amount { get; set; }
    public required long ExpiryFrame { get; init; }
}
