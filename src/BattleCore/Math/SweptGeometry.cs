namespace BattleCore.Math;

/// <summary>
/// Continuous-collision helpers for shapes moving along a straight segment
/// within one logic frame. All math is fixed-point and deterministic.
/// </summary>
public static class SweptGeometry
{
    /// <summary>
    /// Whether a circle moving from <paramref name="from"/> to
    /// <paramref name="to"/> comes strictly closer than
    /// <paramref name="combinedRadius"/> to <paramref name="center"/> at any
    /// point of the segment. Fold both radii into
    /// <paramref name="combinedRadius"/> for circle-vs-circle sweeps.
    /// </summary>
    public static bool CircleSweepHitsCircle(
        FixVector2 from,
        FixVector2 to,
        FixVector2 center,
        Fix64 combinedRadius)
    {
        var nearest = from + ((to - from) * ClosestParameter(from, to - from, center));
        return (center - nearest).LengthSquared < combinedRadius * combinedRadius;
    }

    /// <summary>
    /// Parameter t in [0, 1] where the segment `from + delta * t` is closest
    /// to <paramref name="point"/>. Returns 0 for a zero-length segment.
    /// </summary>
    public static Fix64 ClosestParameter(
        FixVector2 from,
        FixVector2 delta,
        FixVector2 point)
    {
        var numerator = FixVector2.Dot(point - from, delta);
        if (numerator <= Fix64.Zero)
        {
            return Fix64.Zero;
        }

        var denominator = delta.LengthSquared;
        return numerator >= denominator ? Fix64.One : numerator / denominator;
    }
}
