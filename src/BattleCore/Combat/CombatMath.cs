using BattleCore.Attributes;

namespace BattleCore.Combat;

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

        var mitigation = defense * AttributeSet.RatioScale / (defense + defenseK);
        if (mitigation > maximumMitigation)
        {
            mitigation = maximumMitigation;
        }

        return hit * (AttributeSet.RatioScale - mitigation) / AttributeSet.RatioScale;
    }

    public static long ScaleByRate(long value, long rate) =>
        value * (AttributeSet.RatioScale + rate) / AttributeSet.RatioScale;
}
