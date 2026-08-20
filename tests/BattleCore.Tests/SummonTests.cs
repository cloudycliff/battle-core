using BattleCore.Brains;
using BattleCore.Configuration;
using BattleCore.Math;
using BattleCore.Simulation.Commands;
using BattleCore.Simulation.Events;
using BattleCore.Skills;

namespace BattleCore.Tests;

public sealed class SummonTests
{
    [Fact]
    public void Constructor_RejectsUnknownSummonTemplate()
    {
        var values = BaseAttributes();
        var summon = SummonSkill("missing");
        var template = new CharacterTemplate(
            "hero",
            Fix64.FromRatio(1, 2),
            values,
            skillIds: [summon.Id]);

        var error = Assert.Throws<ConfigurationException>(() =>
            new BattleConfig(
                values.Keys,
                TestBattleFactory.Bindings,
                [template],
                [summon]));

        Assert.Contains("missing", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SummonSkill_SpawnsAlliedCharacterBoundToCaster()
    {
        var battle = CreateSummonBattle();
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2), PlayerControlled: true));
        battle.Tick();
        battle.Enqueue(new CastSkillCommand(1, 1, "summon"));

        var events = battle.Tick();
        var spawned = Assert.Single(
            events.OfType<CharacterSpawnedEvent>(),
            item => item.TemplateId == "pet");

        Assert.Equal(1, spawned.FactionId);
        Assert.Equal(Point(2, 2), spawned.Position);
        var pet = battle.GetCharacter(spawned.CharacterId)!;
        Assert.Equal(1, pet.OwnerId);
        Assert.False(pet.PlayerControlled);
        Assert.Equal("pet", pet.TemplateId);
        Assert.Single(events.OfType<SkillHitEvent>());
    }

    [Fact]
    public void SummonSkill_SpawnsConfiguredCount()
    {
        var battle = CreateSummonBattle(count: 3);
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Tick();
        battle.Enqueue(new CastSkillCommand(1, 1, "summon"));

        var pets = battle.Tick()
            .OfType<CharacterSpawnedEvent>()
            .Where(item => item.TemplateId == "pet")
            .ToArray();

        Assert.Equal(3, pets.Length);
        Assert.Equal(4, battle.GetCharacters().Count);
    }

    [Fact]
    public void SummonSkill_SpawnsAtTargetPoint()
    {
        var target = new SkillTarget(
            SkillTargetKind.CircleAtPoint,
            TargetRelation.Self,
            Fix64.One);
        var battle = CreateSummonBattle(target: target);
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Tick();
        battle.Enqueue(new CastSkillCommand(1, 1, "summon", TargetPoint: Point(6, 4)));

        var spawned = Assert.Single(
            battle.Tick().OfType<CharacterSpawnedEvent>(),
            item => item.TemplateId == "pet");

        Assert.Equal(Point(6, 4), spawned.Position);
    }

    [Fact]
    public void SummonSkill_SkipsWhenPointIsBlocked()
    {
        var target = new SkillTarget(
            SkillTargetKind.CircleAtPoint,
            TargetRelation.Self,
            Fix64.One);
        var terrain = new FixAabb(Point(5, 0), Point(8, 8));
        var battle = CreateSummonBattle(target: target, terrain: [terrain]);
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Tick();
        battle.Enqueue(new CastSkillCommand(1, 1, "summon", TargetPoint: Point(6, 4)));

        Assert.DoesNotContain(
            battle.Tick().OfType<CharacterSpawnedEvent>(),
            item => item.TemplateId == "pet");
        Assert.Single(battle.GetCharacters());
    }

    [Fact]
    public void SummonedFollowOwner_MovesTowardCaster()
    {
        var follow = new BrainDefinition(
            "follow",
            BrainKind.FollowOwner,
            aggroRadius: Fix64.FromInt(3),
            followDistance: Fix64.FromInt(2));
        var target = new SkillTarget(
            SkillTargetKind.CircleAtPoint,
            TargetRelation.Self,
            Fix64.One);
        var battle = CreateSummonBattle(
            target: target,
            summonBrainId: "follow",
            brain: follow);
        battle.Enqueue(new SpawnCharacterCommand(
            0,
            "hero",
            1,
            Point(2, 2),
            PlayerControlled: true));
        battle.Tick();
        battle.Enqueue(new CastSkillCommand(1, 1, "summon", TargetPoint: Point(10, 2)));
        battle.Tick();

        var petId = battle.GetCharacters().Single(item => item.TemplateId == "pet").Id;
        var before = battle.GetCharacter(petId)!.Position.X;
        battle.Tick();

        Assert.True(battle.GetCharacter(petId)!.Position.X < before);
    }

    private static BattleCore.Simulation.BattleInstance CreateSummonBattle(
        int count = 1,
        SkillTarget? target = null,
        string? summonBrainId = null,
        BrainDefinition? brain = null,
        IEnumerable<FixAabb>? terrain = null)
    {
        var values = BaseAttributes();
        var summon = SummonSkill("pet", count, target, summonBrainId);
        var hero = new CharacterTemplate(
            "hero",
            Fix64.FromRatio(1, 2),
            values,
            skillIds: [summon.Id]);
        var pet = new CharacterTemplate(
            "pet",
            Fix64.FromRatio(1, 2),
            values);
        var brains = brain is null ? Array.Empty<BrainDefinition>() : [brain];
        var config = new BattleConfig(
            values.Keys,
            TestBattleFactory.Bindings,
            [hero, pet],
            [summon],
            brainDefinitions: brains);
        return TestBattleFactory.CreateBattle(config, terrain);
    }

    private static SkillDefinition SummonSkill(
        string templateId,
        int count = 1,
        SkillTarget? target = null,
        string? brainId = null) =>
        new(
            "summon",
            durationFrames: 1,
            cooldownFrames: 0,
            [
                new SkillSpawnCharacterEffect(
                    0,
                    target ?? SkillTarget.Self,
                    templateId,
                    count,
                    brainId),
            ]);

    private static Dictionary<string, long> BaseAttributes()
    {
        var values = TestBattleFactory.Bindings.AllIds()
            .Concat(["attack"])
            .ToDictionary(id => id, _ => 0L, StringComparer.Ordinal);
        values[TestBattleFactory.Bindings.MaxHp] = 100;
        values[TestBattleFactory.Bindings.CritDamage] = 15_000;
        values[TestBattleFactory.Bindings.MoveSpeed] = 3;
        values["attack"] = 100;
        return values;
    }

    private static FixVector2 Point(long x, long y) =>
        new(Fix64.FromInt(x), Fix64.FromInt(y));
}
