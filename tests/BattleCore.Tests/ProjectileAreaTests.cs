using BattleCore.AreaEffects;
using BattleCore.Combat;
using BattleCore.Configuration;
using BattleCore.Effects;
using BattleCore.Math;
using BattleCore.Projectiles;
using BattleCore.Simulation;
using BattleCore.Simulation.Commands;
using BattleCore.Simulation.Events;
using BattleCore.Skills;

namespace BattleCore.Tests;

public sealed class ProjectileAreaTests
{
    [Fact]
    public void Projectile_HitsEnemyThenDespawns()
    {
        var projectile = LinearBolt(pierce: 0);
        var skill = FireSkill(projectile.Id);
        var battle = CreateBattle(skill, [projectile]);
        SpawnPair(battle);
        battle.Enqueue(new CastSkillCommand(1, 1, skill.Id, Direction: Point(1, 0)));

        ProjectileHitEvent? hit = null;
        for (var i = 0; i < 10; i++)
        {
            var events = battle.Tick();
            hit = events.OfType<ProjectileHitEvent>().FirstOrDefault();
            if (hit is not null)
            {
                Assert.Single(events.OfType<ProjectileDespawnedEvent>());
                break;
            }
        }

        Assert.NotNull(hit);
        Assert.Equal(2, hit.TargetId);
        Assert.Equal(80, battle.GetCharacter(2)!.CurrentHp);
        Assert.Empty(battle.GetProjectiles());
    }

    [Fact]
    public void Projectile_PiercesUntilCountExhausted()
    {
        var projectile = LinearBolt(pierce: 1);
        var skill = FireSkill(projectile.Id);
        var battle = CreateBattle(skill, [projectile]);
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 2, Point(5, 2)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 2, Point(8, 2)));
        battle.Tick();
        battle.Enqueue(new CastSkillCommand(1, 1, skill.Id, Direction: Point(1, 0)));

        var hits = new List<long>();
        for (var i = 0; i < 12; i++)
        {
            hits.AddRange(battle.Tick().OfType<ProjectileHitEvent>().Select(item => item.TargetId));
        }

        Assert.Equal([2L, 3L], hits);
        Assert.Equal(80, battle.GetCharacter(2)!.CurrentHp);
        Assert.Equal(80, battle.GetCharacter(3)!.CurrentHp);
        Assert.Empty(battle.GetProjectiles());
    }

    [Fact]
    public void AreaEffect_TicksThenExpires()
    {
        var area = new AreaEffectDefinition(
            "burn_zone",
            Fix64.FromInt(2),
            durationFrames: 2,
            intervalFrames: 1,
            TargetRelation.Enemies,
            [new HitDamageEffect(DamageType.True, 10, CanCrit: false)]);
        var skill = new SkillDefinition(
            "drop",
            1,
            0,
            [
                new SkillSpawnAreaEffectEffect(
                    0,
                    new SkillTarget(
                        SkillTargetKind.CircleAtPoint,
                        TargetRelation.Enemies,
                        Fix64.Zero),
                    area.Id),
            ]);
        var battle = CreateBattle(skill, areaEffects: [area]);
        SpawnPair(battle);
        battle.Enqueue(new CastSkillCommand(1, 1, skill.Id, TargetPoint: Point(8, 2)));

        var first = battle.Tick();
        Assert.Single(first.OfType<AreaEffectSpawnedEvent>());
        Assert.Single(first.OfType<AreaEffectTickedEvent>());
        Assert.Equal(90, battle.GetCharacter(2)!.CurrentHp);

        var second = battle.Tick();
        Assert.Single(second.OfType<AreaEffectTickedEvent>());
        Assert.Equal(80, battle.GetCharacter(2)!.CurrentHp);

        var third = battle.Tick();
        Assert.Single(third.OfType<AreaEffectDespawnedEvent>());
        Assert.Empty(battle.GetAreaEffects());
    }

    [Fact]
    public void AreaEffect_FollowsTarget()
    {
        var area = new AreaEffectDefinition(
            "aura",
            Fix64.FromInt(1),
            durationFrames: 5,
            intervalFrames: 5,
            TargetRelation.Self,
            [new HitHealEffect(1, CanCrit: false)],
            followsTarget: true);
        var skill = new SkillDefinition(
            "aura_skill",
            1,
            0,
            [
                new SkillSpawnAreaEffectEffect(0, SkillTarget.Self, area.Id),
            ]);
        var battle = CreateBattle(skill, areaEffects: [area]);
        SpawnPair(battle);
        battle.Enqueue(new CastSkillCommand(1, 1, skill.Id));
        battle.Tick();
        battle.Enqueue(new MoveDirectionCommand(2, 1, Point(1, 0)));
        battle.Tick();

        var snapshot = Assert.Single(battle.GetAreaEffects());
        Assert.Equal(battle.GetCharacter(1)!.Position, snapshot.Position);
        Assert.Equal(1, snapshot.FollowTargetId);
    }

    [Fact]
    public void Config_RejectsUnknownProjectileOnSkill()
    {
        var values = BaseAttributes();
        var skill = FireSkill("missing_bolt");
        var template = new CharacterTemplate(
            "hero",
            Fix64.FromRatio(1, 2),
            values,
            skillIds: [skill.Id]);

        var error = Assert.Throws<ConfigurationException>(() =>
            new BattleConfig(
                values.Keys,
                TestBattleFactory.Bindings,
                [template],
                [skill]));

        Assert.Contains("missing_bolt", error.Message, StringComparison.Ordinal);
    }

    private static ProjectileDefinition LinearBolt(int pierce) =>
        new(
            "bolt",
            Fix64.FromInt(30),
            Fix64.FromRatio(1, 5),
            lifetimeFrames: 30,
            pierceCount: pierce,
            TargetRelation.Enemies,
            [new HitDamageEffect(DamageType.True, 20, CanCrit: false)]);

    private static SkillDefinition FireSkill(string projectileId) =>
        new(
            "firebolt",
            1,
            0,
            [new SkillSpawnProjectileEffect(0, SkillTarget.Self, projectileId)]);

    private static BattleInstance CreateBattle(
        SkillDefinition skill,
        IReadOnlyList<ProjectileDefinition>? projectiles = null,
        IReadOnlyList<AreaEffectDefinition>? areaEffects = null)
    {
        var values = BaseAttributes();
        var template = new CharacterTemplate(
            "hero",
            Fix64.FromRatio(1, 2),
            values,
            skillIds: [skill.Id]);
        var config = new BattleConfig(
            values.Keys,
            TestBattleFactory.Bindings,
            [template],
            [skill],
            projectileDefinitions: projectiles,
            areaEffectDefinitions: areaEffects);
        return TestBattleFactory.CreateBattle(config);
    }

    private static void SpawnPair(BattleInstance battle)
    {
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 2, Point(8, 2)));
        battle.Tick();
    }

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
