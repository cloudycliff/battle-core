namespace BattleCore.Configuration;

public sealed class ConfigurationException : Exception
{
    public ConfigurationException(IReadOnlyList<string> errors)
        : base($"Battle configuration is invalid:{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", errors)}")
    {
        Errors = errors.ToArray();
    }

    public IReadOnlyList<string> Errors { get; }
}
