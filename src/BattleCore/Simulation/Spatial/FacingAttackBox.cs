using BattleCore.Math;

namespace BattleCore.Simulation.Spatial;

/// <summary>
/// Axis-aligned swipe in front of the caster. Length extends along facing;
/// height is the full perpendicular thickness, centered on the origin.
/// </summary>
internal static class FacingAttackBox
{
    public static FixAabb Create(
        FixVector2 origin,
        Facing facing,
        Fix64 length,
        Fix64 height)
    {
        var half = height / Fix64.FromInt(2);
        var step = facing.Step;
        if (step < 128 || step >= 896)
        {
            return new FixAabb(
                new FixVector2(origin.X, origin.Y - half),
                new FixVector2(origin.X + length, origin.Y + half));
        }

        if (step < 384)
        {
            return new FixAabb(
                new FixVector2(origin.X - half, origin.Y),
                new FixVector2(origin.X + half, origin.Y + length));
        }

        if (step < 640)
        {
            return new FixAabb(
                new FixVector2(origin.X - length, origin.Y - half),
                new FixVector2(origin.X, origin.Y + half));
        }

        return new FixAabb(
            new FixVector2(origin.X - half, origin.Y - length),
            new FixVector2(origin.X + half, origin.Y));
    }
}
