using BattleCore.Math;

namespace BattleCore.Configuration;

public sealed class SpawnPoint
{
    public SpawnPoint(string id, FixVector2 position, string tableEntryId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableEntryId);

        Id = id;
        Position = position;
        TableEntryId = tableEntryId;
    }

    public string Id { get; }
    public FixVector2 Position { get; }
    public string TableEntryId { get; }
}

public sealed class PlayerSpawnMarker
{
    public PlayerSpawnMarker(string id, FixVector2 position)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        Id = id;
        Position = position;
    }

    public string Id { get; }
    public FixVector2 Position { get; }
}

public sealed class SpawnTableEntry
{
    public SpawnTableEntry(
        string id,
        string templateId,
        int factionId,
        long frame,
        int count = 1,
        string? brainId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(templateId);
        ArgumentOutOfRangeException.ThrowIfNegative(frame);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        Id = id;
        TemplateId = templateId;
        FactionId = factionId;
        Frame = frame;
        Count = count;
        BrainId = brainId;
    }

    public string Id { get; }
    public string TemplateId { get; }
    public int FactionId { get; }
    public long Frame { get; }
    public int Count { get; }
    public string? BrainId { get; }
}

public sealed class SpawnTable
{
    public SpawnTable(IEnumerable<SpawnTableEntry>? entries = null)
    {
        var list = (entries ?? []).ToArray();
        var duplicates = list
            .GroupBy(entry => entry.Id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicates.Length > 0)
        {
            throw new ConfigurationException(
                duplicates.Select(id => $"Duplicate spawn table entry '{id}'.").ToArray());
        }

        Entries = list.ToDictionary(entry => entry.Id, StringComparer.Ordinal);
    }

    public IReadOnlyDictionary<string, SpawnTableEntry> Entries { get; }

    public static SpawnTable Empty { get; } = new();
}
