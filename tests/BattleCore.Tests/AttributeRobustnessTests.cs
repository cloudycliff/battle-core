using BattleCore.Attributes;
using BattleCore.Buffs;
using BattleCore.Combat;
using BattleCore.Configuration;
using BattleCore.Math;
using BattleCore.Simulation;
using BattleCore.Simulation.Commands;
using BattleCore.Simulation.Events;
using BattleCore.Skills;

namespace BattleCore.Tests;

/// <summary>
/// Review round-2 N1: attributes referenced by skills, buffs, or commands but
/// absent from a template must be rejected at load when statically knowable,
/// and must never crash the tick when they slip through dynamically.
/// </summary>
public sealed class AttributeRobustnessTests
{
    [Fact]
    public void Config_RejectsSkillReferencingAttributeMissingFromTemplate()
    {
        var skill = new SkillDefinition(
            "fireball",
            durationFrames: 2,
            cooldownFrames: 0,
            [
                new SkillDamageEffect(
                    Frame: 0,
                    SkillTarget.Self,
                    DamageType.Magical,
                    Flat: 0,
                    AttributeId: "spell_power",
                    Ratio: 10_000),
            ]);

        var exception = Assert.Throws<ConfigurationException>(() =>
            CreateConfig(declareSpellPower: true, skill: skill, templateHasSkill: true));

        Assert.Contains("spell_power", exception.Message, StringComparison.Ordinal);
        Assert.Contains("fireball", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DamageCommand_TreatsMissingSourceAttributeAsZeroInsteadOfThrowing()
    {
        var battle = CreateBattle(CreateConfig(declareSpellPower: true));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 2, Point(5, 2)));
        battle.Tick();
        battle.Enqueue(new DealDamageCommand(
            1,
            SourceId: 1,
            TargetId: 2,
            DamageType.True,
            Flat: 10,
            AttributeId: "spell_power",
            Ratio: 10_000,
            CanCrit: false));

        var events = battle.Tick();

        var damage = Assert.Single(events.OfType<DamageEvent>());
        Assert.Equal(10, damage.Amount);
        Assert.Equal(90, battle.GetCharacter(2)!.CurrentHp);
    }

    [Fact]
    public void Buff_SkipsModifierForAttributeMissingFromTarget()
    {
        var buff = new BuffDefinition(
            "arcane",
            durationFrames: 10,
            BuffStackingRule.Refresh,
            [
                new AttributeBuffEffect("spell_power", ModifierKind.Flat, 25),
                new AttributeBuffEffect("max_hp", ModifierKind.Flat, 40),
            ]);
        var battle = CreateBattle(CreateConfig(declareSpellPower: true, buff: buff));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Tick();
        battle.Enqueue(new ApplyBuffCommand(1, 1, 1, "arcane"));

        var events = battle.Tick();

        Assert.Single(events.OfType<BuffAddedEvent>());
        Assert.Null(battle.GetAttribute(1, "spell_power"));
        Assert.Equal(140, battle.GetAttribute(1, "max_hp"));
    }

    private static BattleConfig CreateConfig(
        bool declareSpellPower,
        SkillDefinition? skill = null,
        bool templateHasSkill = false,
        BuffDefinition? buff = null)
    {
        var bindings = TestBattleFactory.Bindings;
        var attributes = bindings.AllIds().ToList();
        if (declareSpellPower)
        {
            attributes.Add("spell_power");
        }

        var values = bindings.AllIds()
            .ToDictionary(id => id, _ => 0L, StringComparer.Ordinal);
        values[bindings.MaxHp] = 100;
        values[bindings.CritDamage] = 15_000;
        values[bindings.MoveSpeed] = 3;

        var template = new CharacterTemplate(
            "hero",
            Fix64.FromRatio(1, 2),
            values,
            skillIds: templateHasSkill && skill is not null ? [skill.Id] : null);

        return new BattleConfig(
            attributes,
            bindings,
            [template],
            skillDefinitions: skill is null ? null : [skill],
            buffDefinitions: buff is null ? null : [buff]);
    }

    private static BattleInstance CreateBattle(BattleConfig config)
    {
        var bounds = new FixAabb(Point(0, 0), Point(20, 20));
        return new BattleInstance(config, new MapConfig(bounds), seed: 42, maxFrames: 1_000);
    }

    private static FixVector2 Point(long x, long y) =>
        new(Fix64.FromInt(x), Fix64.FromInt(y));
}
