using BattleCore.Math;

namespace BattleCore.Simulation.Spatial;

internal static class SolidTerrainGeometry
{
    public static bool Intersects(
        IReadOnlyList<FixAabb> terrain,
        FixVector2 position,
        Fix64 radius) =>
        terrain.Any(blocker => blocker.IntersectsCircle(position, radius));

    public static FixVector2 ClampSegment(
        IReadOnlyList<FixAabb> terrain,
        FixVector2 from,
        FixVector2 delta,
        Fix64 radius)
    {
        if (delta == FixVector2.Zero ||
            !Intersects(terrain, from + delta, radius))
        {
            return from + delta;
        }

        if (Intersects(terrain, from, radius))
        {
            return from;
        }

        var maximumAxisDistance = Fix64.Max(
            Fix64.Abs(delta.X),
            Fix64.Abs(delta.Y));
        var sampleDistanceRaw = System.Math.Max(1, radius.RawValue / 2);
        var sampleCount = maximumAxisDistance.RawValue / sampleDistanceRaw;
        if (maximumAxisDistance.RawValue % sampleDistanceRaw != 0)
        {
            sampleCount++;
        }

        sampleCount = System.Math.Max(1, sampleCount);
        var previous = from;
        for (long sample = 1; sample <= sampleCount; sample++)
        {
            var fraction = Fix64.FromRatio(sample, sampleCount);
            var candidate = from + (delta * fraction);
            if (Intersects(terrain, candidate, radius))
            {
                return previous;
            }

            previous = candidate;
        }

        return from + delta;
    }
}
