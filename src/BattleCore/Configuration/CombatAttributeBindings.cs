namespace BattleCore.Configuration;

public sealed record CombatAttributeBindings(
    string MaxHp,
    string PhysicalDefense,
    string MagicalDefense,
    string CritRate,
    string CritDamage,
    string DamageDealt,
    string DamageTaken,
    string MoveSpeed)
{
    public IEnumerable<string> AllIds()
    {
        yield return MaxHp;
        yield return PhysicalDefense;
        yield return MagicalDefense;
        yield return CritRate;
        yield return CritDamage;
        yield return DamageDealt;
        yield return DamageTaken;
        yield return MoveSpeed;
    }
}
