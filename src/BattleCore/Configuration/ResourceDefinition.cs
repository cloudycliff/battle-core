namespace BattleCore.Configuration;

public sealed class ResourceDefinition
{
    public ResourceDefinition(string id, long maximum)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximum);

        Id = id;
        Maximum = maximum;
    }

    public string Id { get; }
    public long Maximum { get; }
}
