using BattleCore.AreaEffects;
using BattleCore.Buffs;
using BattleCore.Combat;
using BattleCore.Configuration;
using BattleCore.Effects;
using BattleCore.Math;
using BattleCore.Projectiles;
using BattleCore.Serialization;
using BattleCore.Simulation;
using BattleCore.Simulation.Commands;
using BattleCore.Simulation.Events;
using BattleCore.Skills;

namespace BattleCore.Tests;

/// <summary>
/// Regressions for the queued items of docs/review-2026-08-28.md (#6, #7, #8)
/// and docs/review-2026-08-28-round2.md (N6, N9, N10, N11, N12, N15, N16),
/// plus review-2026-09-08 item 11 (periodic tick on the expiry frame).
/// </summary>
public sealed class Round2QueueRegressionTests
{
    [Fact]
    public void Projectile_OutlivesCasterCorpseAndStillHits()
    {
        // 3 m/s bolt: 60 frames to cross 6 m. The archer dies at frame 2 and
        // the corpse is recycled at frame ~5, long before impact.
        var bolt = new ProjectileDefinition(
            "bolt",
            Fix64.FromInt(3),
            Fix64.FromRatio(1, 5),
            lifetimeFrames: 120,
            pierceCount: 0,
            TargetRelation.Enemies,
            [new HitDamageEffect(DamageType.True, 20, CanCrit: false)]);
        var fire = new SkillDefinition("fire", 1, 0, [new SkillSpawnProjectileEffect(0, SkillTarget.Self, "bolt")]);
        var battle = Build(fire, projectiles: [bolt], corpseDurationFrames: 3);
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 2, Point(8, 2)));
        battle.Tick();
        battle.Enqueue(new CastSkillCommand(1, 1, "fire", Direction: Point(1, 0)));
        battle.Tick();
        battle.Enqueue(new DealDamageCommand(2, 2, 1, DamageType.True, 1000, CanCrit: false));
        battle.Tick();

        var hit = false;
        for (var frame = 3; frame < 90 && !hit; frame++)
        {
            var events = battle.Tick();
            if (frame == 10)
            {
                Assert.Null(battle.GetCharacter(1));
                Assert.Single(battle.GetProjectiles());
            }

            hit = events.OfType<ProjectileHitEvent>().Any(e => e.TargetId == 2);
        }

        Assert.True(hit, "projectile vanished with its caster's corpse");
        Assert.Equal(80, battle.GetCharacter(2)!.CurrentHp);
    }

    [Fact]
    public void ExplicitAllyHeal_OnEnemy_IsRejectedAtCast()
    {
        var heal = new SkillDefinition("mend", 1, 10, [new SkillHealEffect(0, SkillTarget.ExplicitAlly, 10)]);
        var battle = Build(heal);
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 2, Point(3, 2)));
        battle.Tick();
        battle.Enqueue(new CastSkillCommand(1, 1, "mend", TargetId: 2));

        var events = battle.Tick();

        Assert.Single(events.OfType<CommandRejectedEvent>());
        Assert.Empty(events.OfType<SkillStartedEvent>());
    }

    [Fact]
    public void MinimumDamage_RaisesLowHitsToTheFloor()
    {
        var battle = Build(minimumDamage: 5);
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 2, Point(8, 2)));
        battle.Tick();
        battle.Enqueue(new DealDamageCommand(1, 1, 2, DamageType.True, 3, CanCrit: false));

        var damage = Assert.Single(battle.Tick().OfType<DamageEvent>());

        Assert.Equal(5, damage.Amount);
        Assert.Equal(95, battle.GetCharacter(2)!.CurrentHp);
    }

    [Fact]
    public void ControlBuff_OnNonDamageableTemplate_IsRejected()
    {
        var stun = new BuffDefinition("stun", 30, BuffStackingRule.Unique, [new ControlBuffEffect(ControlType.Stun)]);
        var mark = new BuffDefinition("mark", 30, BuffStackingRule.Unique, [new UntargetableBuffEffect()]);
        var battle = Build(buffs: [stun, mark], damageable: false);
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 2, Point(8, 2)));
        battle.Tick();
        battle.Enqueue(new ApplyBuffCommand(1, 1, 2, "stun"));
        battle.Enqueue(new ApplyBuffCommand(1, 1, 2, "mark"));

        var events = battle.Tick();

        Assert.Single(events.OfType<CommandRejectedEvent>());
        Assert.Single(events.OfType<BuffAddedEvent>());
        Assert.False(battle.GetCharacter(2)!.Controls.Contains(ControlType.Stun));
    }

    [Fact]
    public void Projectile_LifetimeFrames_MeansExactlyThatManyMoves()
    {
        var bolt = new ProjectileDefinition(
            "bolt",
            Fix64.FromInt(30),
            Fix64.FromRatio(1, 5),
            lifetimeFrames: 1,
            pierceCount: 0,
            TargetRelation.Enemies,
            [new HitDamageEffect(DamageType.True, 20, CanCrit: false)]);
        var fire = new SkillDefinition("fire", 1, 0, [new SkillSpawnProjectileEffect(0, SkillTarget.Self, "bolt")]);
        var battle = Build(fire, projectiles: [bolt]);
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Tick();
        battle.Enqueue(new CastSkillCommand(1, 1, "fire", Direction: Point(1, 0)));

        var first = battle.Tick();
        Assert.Single(first.OfType<ProjectileSpawnedEvent>());
        Assert.Single(first.OfType<ProjectileMovedEvent>());
        Assert.Empty(first.OfType<ProjectileDespawnedEvent>());

        var second = battle.Tick();
        Assert.Empty(second.OfType<ProjectileMovedEvent>());
        Assert.Single(second.OfType<ProjectileDespawnedEvent>());
    }

    [Fact]
    public void Stun_ClearsMovementIntent_AndRejectsMovementWhileControlled()
    {
        var stun = new BuffDefinition("stun", 3, BuffStackingRule.Unique, [new ControlBuffEffect(ControlType.Stun)]);
        var battle = Build(buffs: [stun]);
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 2, Point(18, 18)));
        battle.Tick();
        battle.Enqueue(new MoveDirectionCommand(1, 1, Point(1, 0)));
        battle.Tick();
        battle.Enqueue(new ApplyBuffCommand(2, 2, 1, "stun"));
        battle.Tick();
        var stunnedAt = battle.GetCharacter(1)!.Position;

        battle.Enqueue(new MoveDirectionCommand(3, 1, Point(1, 0)));
        var duringStun = battle.Tick();
        Assert.Single(duringStun.OfType<CommandRejectedEvent>());

        for (var frame = 4; frame < 20; frame++)
        {
            battle.Tick();
        }

        Assert.False(battle.GetCharacter(1)!.Controls.Contains(ControlType.Stun));
        Assert.Equal(stunnedAt, battle.GetCharacter(1)!.Position);
    }

    [Fact]
    public void Kill_EmitsShieldAndTargetClearEvents()
    {
        var battle = Build();
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 2, Point(8, 2)));
        battle.Tick();
        battle.Enqueue(new GrantShieldCommand(1, 2, 50, 0));
        battle.Enqueue(new SetTargetCommand(1, 2, 1));
        battle.Tick();
        battle.Enqueue(new DealDamageCommand(2, 1, 2, DamageType.True, 1000, CanCrit: false));

        var events = battle.Tick();

        Assert.Contains(events.OfType<TargetChangedEvent>(), e => e.CharacterId == 2 && e.TargetId is null);
        var lost = events.OfType<ShieldChangeEvent>().Where(e => e.CharacterId == 2).Sum(e => e.Amount);
        Assert.Equal(-50, lost);
        Assert.Equal(0, battle.GetCharacter(2)!.Shield);
    }

    [Fact]
    public void CombatMath_OverflowsLoudlyInsteadOfWrapping()
    {
        Assert.Throws<OverflowException>(() => CombatMath.ScaleByRate(long.MaxValue / 2, 10_000));
        Assert.Throws<OverflowException>(() => CombatMath.ApplyMitigation(long.MaxValue / 2, 100, 100, 8_000));
    }

    [Fact]
    public void HitShieldEffect_RejectsNegativeDurationAndNonPositiveAmount()
    {
        Assert.Throws<ArgumentException>(() => new HitShieldEffect(10, -1));
        Assert.Throws<ArgumentException>(() => new HitShieldEffect(0, 10));
    }

    [Fact]
    public void Json_Int32Overflow_IsAConfigurationError()
    {
        var error = Assert.Throws<ConfigurationException>(() =>
            BattleJson.LoadBattleConfig(
                """
                {
                  "attributeIds": ["max_hp"],
                  "bindings": {
                    "maxHp": "max_hp",
                    "physicalDefense": "max_hp",
                    "magicalDefense": "max_hp",
                    "critRate": "max_hp",
                    "critDamage": "max_hp",
                    "damageDealt": "max_hp",
                    "damageTaken": "max_hp",
                    "moveSpeed": "max_hp"
                  },
                  "characterTemplates": [
                    { "id": "hero", "radius": 1, "baseAttributes": { "max_hp": 1 } }
                  ],
                  "skills": [
                    { "id": "wait", "durationFrames": 3000000000, "cooldownFrames": 0, "effects": [] }
                  ]
                }
                """));

        Assert.Contains("32-bit", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PeriodicBuff_DurationEqualsInterval_TicksOnceOnExpiry()
    {
        var dot = new BuffDefinition("dot", 60, BuffStackingRule.Unique, [new PeriodicDamageBuffEffect(60, DamageType.True, 5)]);
        var battle = Build(buffs: [dot]);
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Tick();
        battle.Enqueue(new ApplyBuffCommand(1, 1, 1, "dot"));

        var ticks = 0;
        for (var frame = 1; frame < 200; frame++)
        {
            ticks += battle.Tick().OfType<DamageEvent>().Count();
        }

        Assert.Equal(1, ticks);
        Assert.Equal(95, battle.GetCharacter(1)!.CurrentHp);
    }

    [Fact]
    public void AreaEffect_DurationEqualsInterval_TicksOnceOnExpiry()
    {
        var pool = new AreaEffectDefinition(
            "pool",
            Fix64.FromInt(2),
            durationFrames: 30,
            intervalFrames: 30,
            TargetRelation.Enemies,
            [new HitDamageEffect(DamageType.True, 5, CanCrit: false)],
            tickOnSpawn: false);
        var drop = new SkillDefinition("drop", 1, 0, [new SkillSpawnAreaEffectEffect(0, SkillTarget.Self, "pool")]);
        var battle = Build(drop, areas: [pool]);
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 2, Point(3, 2)));
        battle.Tick();
        battle.Enqueue(new CastSkillCommand(1, 1, "drop"));

        var ticks = 0;
        for (var frame = 1; frame < 120; frame++)
        {
            ticks += battle.Tick().OfType<AreaEffectTickedEvent>().Count();
        }

        Assert.Equal(1, ticks);
        Assert.Equal(95, battle.GetCharacter(2)!.CurrentHp);
        Assert.Empty(battle.GetAreaEffects());
    }

    private static BattleInstance Build(
        SkillDefinition? skill = null,
        IReadOnlyList<BuffDefinition>? buffs = null,
        IReadOnlyList<ProjectileDefinition>? projectiles = null,
        IReadOnlyList<AreaEffectDefinition>? areas = null,
        long minimumDamage = 1,
        int corpseDurationFrames = 30,
        bool damageable = true)
    {
        var values = TestBattleFactory.Bindings.AllIds()
            .Concat(["attack"])
            .ToDictionary(id => id, _ => 0L, StringComparer.Ordinal);
        values[TestBattleFactory.Bindings.MaxHp] = 100;
        values[TestBattleFactory.Bindings.CritDamage] = 15_000;
        values[TestBattleFactory.Bindings.MoveSpeed] = 3;
        values["attack"] = 100;
        var template = new CharacterTemplate(
            "hero",
            Fix64.FromRatio(1, 2),
            values,
            damageable: damageable,
            skillIds: skill is null ? null : [skill.Id]);
        var config = new BattleConfig(
            values.Keys,
            TestBattleFactory.Bindings,
            [template],
            skill is null ? null : [skill],
            buffs,
            projectiles,
            areas,
            minimumDamage: minimumDamage,
            corpseDurationFrames: corpseDurationFrames);
        return TestBattleFactory.CreateBattle(config);
    }

    private static FixVector2 Point(long x, long y) =>
        new(Fix64.FromInt(x), Fix64.FromInt(y));
}
