using BattleCore.Brains;
using BattleCore.Buffs;
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

public sealed class RemainingV1RuleTests
{
    [Fact]
    public void Projectile_DespawnsOnTerrainWithoutHitting()
    {
        var projectile = LinearBolt();
        var skill = FireSkill(projectile.Id);
        var wall = new FixAabb(Point(5, 0), Point(6, 10));
        var battle = CreateBattle([skill], projectiles: [projectile], terrain: [wall]);
        SpawnPair(battle);
        battle.Enqueue(new CastSkillCommand(1, 1, skill.Id, Direction: Point(1, 0)));

        ProjectileHitEvent? hit = null;
        ProjectileDespawnedEvent? despawn = null;
        for (var i = 0; i < 12; i++)
        {
            var events = battle.Tick();
            hit ??= events.OfType<ProjectileHitEvent>().FirstOrDefault();
            despawn ??= events.OfType<ProjectileDespawnedEvent>().FirstOrDefault();
            if (despawn is not null)
            {
                break;
            }
        }

        Assert.Null(hit);
        Assert.NotNull(despawn);
        Assert.Equal(100, battle.GetCharacter(2)!.CurrentHp);
        Assert.Empty(battle.GetProjectiles());
    }

    [Fact]
    public void Projectile_IgnoresTerrainWhenFlagIsFalse()
    {
        var projectile = LinearBolt(collidesWithTerrain: false);
        var skill = FireSkill(projectile.Id);
        var wall = new FixAabb(Point(5, 0), Point(6, 10));
        var battle = CreateBattle([skill], projectiles: [projectile], terrain: [wall]);
        SpawnPair(battle);
        battle.Enqueue(new CastSkillCommand(1, 1, skill.Id, Direction: Point(1, 0)));

        ProjectileHitEvent? hit = null;
        for (var i = 0; i < 12; i++)
        {
            hit = battle.Tick().OfType<ProjectileHitEvent>().FirstOrDefault();
            if (hit is not null)
            {
                break;
            }
        }

        Assert.NotNull(hit);
        Assert.Equal(80, battle.GetCharacter(2)!.CurrentHp);
    }

    [Fact]
    public void Knockback_MovesTargetAwayAndFollowsCarry()
    {
        var grab = new SkillDefinition(
            "grab",
            1,
            0,
            [new SkillStartCarryEffect(0, SkillTarget.ExplicitAlly)]);
        var blast = new SkillDefinition(
            "blast",
            1,
            0,
            [
                new SkillKnockbackEffect(
                    0,
                    SkillTarget.ExplicitEnemy,
                    Fix64.FromInt(4)),
            ]);
        var battle = CreateBattle(
            [grab, blast],
            extraTemplate: Ally("carrier"));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Enqueue(new SpawnCharacterCommand(0, "carrier", 2, Point(8, 2)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 2, Point(9, 2)));
        battle.Tick();
        battle.Enqueue(new CastSkillCommand(1, 2, "grab", TargetId: 3));
        battle.Tick();
        battle.Enqueue(new CastSkillCommand(2, 1, "blast", TargetId: 2));

        battle.Tick();

        Assert.Equal(Point(12, 2), battle.GetCharacter(2)!.Position);
        Assert.Equal(Point(13, 2), battle.GetCharacter(3)!.Position);
    }

    [Fact]
    public void Pull_StopsAtCasterRadiusAndSkipsRooted()
    {
        var root = new BuffDefinition(
            "root",
            0,
            BuffStackingRule.Unique,
            [new ControlBuffEffect(ControlType.Root)]);
        var pull = new SkillDefinition(
            "pull",
            1,
            0,
            [
                new SkillPullEffect(
                    0,
                    new SkillTarget(SkillTargetKind.CircleAtCaster, TargetRelation.Enemies, Fix64.FromInt(10)),
                    Fix64.FromInt(10)),
            ]);
        var battle = CreateBattle([pull], [root]);
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 2, Point(8, 2)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 2, Point(8, 4)));
        battle.Tick();
        battle.Enqueue(new ApplyBuffCommand(1, 1, 3, "root"));
        battle.Tick();
        battle.Enqueue(new CastSkillCommand(2, 1, "pull"));

        battle.Tick();

        Assert.Equal(Point(3, 2), battle.GetCharacter(2)!.Position);
        Assert.Equal(Point(8, 4), battle.GetCharacter(3)!.Position);
    }

    [Fact]
    public void ImmuneToDamage_SkipsPipelineAndHealStillApplies()
    {
        var immune = new BuffDefinition(
            "immune",
            0,
            BuffStackingRule.Unique,
            [new ImmuneToDamageBuffEffect()]);
        var battle = CreateBattle([], [immune]);
        SpawnPair(battle);
        battle.Enqueue(new ApplyBuffCommand(1, 1, 2, "immune"));
        battle.Tick();
        battle.Enqueue(new DealDamageCommand(2, 1, 2, DamageType.True, 40, CanCrit: false));
        battle.Enqueue(new HealCommand(2, 1, 2, 5, CanCrit: false));

        var events = battle.Tick();

        Assert.Empty(events.OfType<DamageEvent>());
        Assert.Single(events.OfType<HealEvent>());
        Assert.Equal(105, battle.GetCharacter(2)!.CurrentHp);
        Assert.False(battle.GetCharacter(2)!.Damageable);
    }

    [Fact]
    public void Untargetable_SkipsGeneralTargetingButNotRevive()
    {
        var hide = new BuffDefinition(
            "hide",
            0,
            BuffStackingRule.Unique,
            [new UntargetableBuffEffect()]);
        var strike = new SkillDefinition(
            "strike",
            1,
            0,
            [
                new SkillDamageEffect(
                    0,
                    SkillTarget.ExplicitEnemy,
                    DamageType.True,
                    10,
                    CanCrit: false),
            ]);
        var battle = CreateBattle([strike], [hide]);
        SpawnPair(battle);
        battle.Enqueue(new ApplyBuffCommand(1, 1, 2, "hide"));
        battle.Tick();
        battle.Enqueue(new CastSkillCommand(2, 1, "strike", TargetId: 2));

        Assert.Single(battle.Tick().OfType<CommandRejectedEvent>());
        Assert.False(battle.GetCharacter(2)!.Targetable);
    }

    [Fact]
    public void ChaseAttack_ReturnsHomeWhenLeashExceeded()
    {
        var brain = new BrainDefinition(
            "chase",
            BrainKind.ChaseAttack,
            aggroRadius: Fix64.FromInt(12),
            attackRange: Fix64.FromRatio(1, 2),
            leashRadius: Fix64.FromInt(2));
        var battle = CreateBattle(
            [],
            brains: [brain]);
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2), BrainId: "chase"));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 2, Point(8, 2)));
        battle.Tick();

        var wentOut = false;
        var cameHome = false;
        for (var i = 0; i < 120; i++)
        {
            battle.Tick();
            var position = battle.GetCharacter(1)!.Position;
            if (position.X > Fix64.FromInt(4))
            {
                wentOut = true;
            }

            if (wentOut &&
                FixVector2.DistanceSquared(position, Point(2, 2)) <= Fix64.One * Fix64.One)
            {
                cameHome = true;
                break;
            }
        }

        Assert.True(wentOut);
        Assert.True(cameHome);
        Assert.Equal(100, battle.GetCharacter(1)!.CurrentHp);
    }

    [Fact]
    public void TakeDamageTrigger_CanSaveFromLethalHit()
    {
        var save = new BuffDefinition(
            "save",
            0,
            BuffStackingRule.Unique,
            [
                new CombatTriggerBuffEffect(
                    CombatTriggerKind.TakeDamage,
                    [new TriggerHealAction(CombatTriggerTarget.Owner, Flat: 50)]),
            ]);
        var battle = CreateBattle([], [save], heroHp: 20);
        SpawnPair(battle);
        battle.Enqueue(new ApplyBuffCommand(1, 1, 2, "save"));
        battle.Tick();
        battle.Enqueue(new DealDamageCommand(2, 1, 2, DamageType.True, 25, CanCrit: false));

        var events = battle.Tick();

        Assert.Single(events.OfType<DamageEvent>());
        Assert.Single(events.OfType<HealEvent>());
        Assert.Empty(events.OfType<DiedEvent>());
        Assert.Equal(45, battle.GetCharacter(2)!.CurrentHp);
    }

    [Fact]
    public void SkillStartTrigger_GrantsResourceOncePerCooldown()
    {
        var spark = new BuffDefinition(
            "spark",
            0,
            BuffStackingRule.Unique,
            [
                new CombatTriggerBuffEffect(
                    CombatTriggerKind.SkillStart,
                    [new TriggerGrantResourceAction(CombatTriggerTarget.Owner, "mana", 2)],
                    CooldownFrames: 5),
            ]);
        var skill = new SkillDefinition(
            "wave",
            1,
            0,
            [new SkillHealEffect(0, SkillTarget.Self, Flat: 1, CanCrit: false)]);
        var battle = CreateBattle(
            [skill],
            [spark],
            resources: [new ResourceDefinition("mana", 10)],
            baseResources: new Dictionary<string, long>(StringComparer.Ordinal) { ["mana"] = 0 });
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Tick();
        battle.Enqueue(new ApplyBuffCommand(1, 1, 1, "spark"));
        battle.Tick();
        battle.Enqueue(new CastSkillCommand(2, 1, "wave"));
        Assert.Equal(2, Assert.Single(battle.Tick().OfType<ResourceChangedEvent>()).Remaining);

        battle.Enqueue(new CastSkillCommand(3, 1, "wave"));
        Assert.Empty(battle.Tick().OfType<ResourceChangedEvent>());
    }

    [Fact]
    public void DealDamageTrigger_DoesNotNestFromItsOwnHeal()
    {
        var loop = new BuffDefinition(
            "loop",
            0,
            BuffStackingRule.Unique,
            [
                new CombatTriggerBuffEffect(
                    CombatTriggerKind.DealDamage,
                    [new TriggerHealAction(CombatTriggerTarget.Owner, Flat: 3)]),
            ]);
        var battle = CreateBattle([], [loop]);
        SpawnPair(battle);
        battle.Enqueue(new ApplyBuffCommand(1, 1, 1, "loop"));
        battle.Tick();
        battle.Enqueue(new DealDamageCommand(2, 1, 2, DamageType.True, 10, CanCrit: false));

        var events = battle.Tick();

        Assert.Single(events.OfType<DamageEvent>());
        Assert.Single(events.OfType<HealEvent>());
        Assert.Equal(103, battle.GetCharacter(1)!.CurrentHp);
    }

    private static ProjectileDefinition LinearBolt(bool collidesWithTerrain = true) =>
        new(
            "bolt",
            Fix64.FromInt(30),
            Fix64.FromRatio(1, 5),
            lifetimeFrames: 30,
            pierceCount: 0,
            TargetRelation.Enemies,
            [new HitDamageEffect(DamageType.True, 20, CanCrit: false)],
            collidesWithTerrain);

    private static SkillDefinition FireSkill(string projectileId) =>
        new(
            "firebolt",
            1,
            0,
            [new SkillSpawnProjectileEffect(0, SkillTarget.Self, projectileId)]);

    private static CharacterTemplate Ally(string id)
    {
        var values = HeroAttributes(100);
        return new CharacterTemplate(id, Fix64.FromRatio(1, 2), values, skillIds: ["grab"]);
    }

    private static BattleInstance CreateBattle(
        IReadOnlyList<SkillDefinition> skills,
        IReadOnlyList<BuffDefinition>? buffs = null,
        IReadOnlyList<ProjectileDefinition>? projectiles = null,
        IEnumerable<FixAabb>? terrain = null,
        CharacterTemplate? extraTemplate = null,
        IReadOnlyList<BrainDefinition>? brains = null,
        long heroHp = 100,
        IReadOnlyList<ResourceDefinition>? resources = null,
        IReadOnlyDictionary<string, long>? baseResources = null)
    {
        var values = HeroAttributes(heroHp);
        var skillIds = skills.Select(item => item.Id).ToArray();
        var hero = new CharacterTemplate(
            "hero",
            Fix64.FromRatio(1, 2),
            values,
            skillIds: skillIds,
            baseResources: baseResources);
        CharacterTemplate[] templates = extraTemplate is null ? [hero] : [hero, extraTemplate];
        var config = new BattleConfig(
            values.Keys,
            TestBattleFactory.Bindings,
            templates,
            skills,
            buffs,
            projectiles,
            brainDefinitions: brains,
            resourceDefinitions: resources);
        return TestBattleFactory.CreateBattle(config, terrain);
    }

    private static void SpawnPair(BattleInstance battle)
    {
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 2, Point(8, 2)));
        battle.Tick();
    }

    private static Dictionary<string, long> HeroAttributes(long hp)
    {
        var values = TestBattleFactory.Bindings.AllIds()
            .Concat(["attack"])
            .ToDictionary(id => id, _ => 0L, StringComparer.Ordinal);
        values[TestBattleFactory.Bindings.MaxHp] = hp;
        values[TestBattleFactory.Bindings.CritDamage] = 15_000;
        values[TestBattleFactory.Bindings.MoveSpeed] = 3;
        values["attack"] = 100;
        return values;
    }

    private static FixVector2 Point(long x, long y) =>
        new(Fix64.FromInt(x), Fix64.FromInt(y));
}
