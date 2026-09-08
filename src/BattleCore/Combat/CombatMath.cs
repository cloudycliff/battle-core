using BattleCore.Attributes;

namespace BattleCore.Combat;

/// <summary>
/// Damage pipeline arithmetic. Every step is <c>checked</c>: a configuration
/// that overflows Int64 must fail loudly, never wrap into a silently wrong
/// number (ADR 0001 / 0006).
/// </summary>
public static class CombatMath
{
    public static long ComputeBase(long flat, long attributeValue, long ratio) =>
        checked(flat + (attributeValue * ratio / AttributeSet.RatioScale));

    public static long ApplyMitigation(long hit, long defense, long defenseK, int maximumMitigation)
    {
        if (hit <= 0 || defense <= 0)
        {
            return hit;
        }

        var mitigation = checked(defense * AttributeSet.RatioScale / (defense + defenseK));
        if (mitigation > maximumMitigation)
        {
            mitigation = maximumMitigation;
        }

        return checked(hit * (AttributeSet.RatioScale - mitigation) / AttributeSet.RatioScale);
    }

    public static long ScaleByRate(long value, long rate) =>
        checked(value * (AttributeSet.RatioScale + rate) / AttributeSet.RatioScale);
}
