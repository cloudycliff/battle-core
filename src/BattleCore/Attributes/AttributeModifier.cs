namespace BattleCore.Attributes;

public enum ModifierKind
{
    Flat,
    Percent,
    Final,
}

public readonly record struct AttributeModifier(
    long InstanceId,
    string AttributeId,
    ModifierKind Kind,
    long Value);
