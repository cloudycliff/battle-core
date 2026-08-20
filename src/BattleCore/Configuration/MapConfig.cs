using BattleCore.Math;

namespace BattleCore.Configuration;

public sealed class MapConfig
{
    public MapConfig(
        FixAabb bounds,
        IEnumerable<FixAabb>? terrain = null,
        IEnumerable<SpawnPoint>? spawnPoints = null,
        IEnumerable<PlayerSpawnMarker>? playerSpawnMarkers = null)
    {
        Bounds = bounds;
        Terrain = terrain?.ToArray() ?? [];
        SpawnPoints = (spawnPoints ?? []).ToArray();
        PlayerSpawnMarkers = (playerSpawnMarkers ?? []).ToArray();

        var errors = new List<string>();

        for (var index = 0; index < Terrain.Count; index++)
        {
            var blocker = Terrain[index];
            if (!Bounds.ContainsCircle(blocker.Min, Fix64.Zero) ||
                !Bounds.ContainsCircle(blocker.Max, Fix64.Zero))
            {
                errors.Add($"Terrain at index {index} lies outside map bounds.");
            }
        }

        ValidateMarkers("Spawn point", SpawnPoints.Select(point => (point.Id, point.Position)), errors);
        ValidateMarkers(
            "Player spawn marker",
            PlayerSpawnMarkers.Select(marker => (marker.Id, marker.Position)),
            errors);

        if (errors.Count > 0)
        {
            throw new ConfigurationException(errors);
        }
    }

    public FixAabb Bounds { get; }
    public IReadOnlyList<FixAabb> Terrain { get; }
    public IReadOnlyList<SpawnPoint> SpawnPoints { get; }
    public IReadOnlyList<PlayerSpawnMarker> PlayerSpawnMarkers { get; }

    private void ValidateMarkers(
        string kind,
        IEnumerable<(string Id, FixVector2 Position)> markers,
        List<string> errors)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (id, position) in markers)
        {
            if (!seen.Add(id))
            {
                errors.Add($"{kind} '{id}' is duplicated.");
            }

            if (!Bounds.ContainsCircle(position, Fix64.Zero))
            {
                errors.Add($"{kind} '{id}' lies outside map bounds.");
            }
        }
    }
}
