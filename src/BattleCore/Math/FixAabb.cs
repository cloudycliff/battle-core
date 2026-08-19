namespace BattleCore.Math;

public readonly record struct FixAabb
{
    public FixAabb(FixVector2 min, FixVector2 max)
    {
        if (min.X > max.X || min.Y > max.Y)
        {
            throw new ArgumentException("AABB minimum must not exceed maximum.");
        }

        Min = min;
        Max = max;
    }

    public FixVector2 Min { get; }
    public FixVector2 Max { get; }

    public bool ContainsCircle(FixVector2 center, Fix64 radius) =>
        center.X - radius >= Min.X &&
        center.X + radius <= Max.X &&
        center.Y - radius >= Min.Y &&
        center.Y + radius <= Max.Y;

    public bool IntersectsCircle(FixVector2 center, Fix64 radius)
    {
        var nearestX = Fix64.Clamp(center.X, Min.X, Max.X);
        var nearestY = Fix64.Clamp(center.Y, Min.Y, Max.Y);
        var delta = center - new FixVector2(nearestX, nearestY);
        return delta.LengthSquared < radius * radius;
    }
}
