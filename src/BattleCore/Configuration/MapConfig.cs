using BattleCore.Math;

namespace BattleCore.Configuration;

public sealed class MapConfig
{
    public MapConfig(FixAabb bounds, IEnumerable<FixAabb>? terrain = null)
    {
        Bounds = bounds;
        Terrain = terrain?.ToArray() ?? [];

        for (var index = 0; index < Terrain.Count; index++)
        {
            var blocker = Terrain[index];
            if (!Bounds.ContainsCircle(blocker.Min, Fix64.Zero) ||
                !Bounds.ContainsCircle(blocker.Max, Fix64.Zero))
            {
                throw new ConfigurationException(
                    [$"Terrain at index {index} lies outside map bounds."]);
            }
        }
    }

    public FixAabb Bounds { get; }
    public IReadOnlyList<FixAabb> Terrain { get; }
}
