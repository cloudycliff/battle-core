using BattleCore.Attributes;
using BattleCore.Brains;
using BattleCore.Buffs;
using BattleCore.Combat;
using BattleCore.Configuration;
using BattleCore.Math;
using BattleCore.Pickups;
using BattleCore.Simulation;
using BattleCore.Simulation.Commands;
using BattleCore.Simulation.Events;
using BattleCore.Skills;

namespace BattleCore.Tests;

/// <summary>
/// Regressions for the 2026-09-08 review (docs/review-2026-09-08.md).
/// </summary>
public sealed class Review20260908RegressionTests
{
    [Fact]
    public void Revive_UnderAnEnemy_RelocatesInsteadOfFreezingBoth()
    {
        var config = TestBattleFactory.CreateConfig(canBeRevived: true);
        var battle = TestBattleFactory.CreateBattle(config);
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(5, 5)));  // ally, will die
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 2, Point(10, 5))); // enemy
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 5)));  // ally, reviver
        battle.Tick();
        battle.Enqueue(new DealDamageCommand(1, 2, 1, DamageType.True, 1000));
        battle.Tick();
        Assert.Equal(LifeState.Dead, battle.GetCharacter(1)!.LifeState);

        battle.Enqueue(new MoveToCommand(2, 2, Point(5, 5)));
        for (var frame = 2; frame < 80; frame++)
        {
            battle.Tick();
        }

        Assert.Equal(Point(5, 5), battle.GetCharacter(2)!.Position);

        battle.Enqueue(new ReviveCommand(80, 3, 1));
        var revive = battle.Tick();
        Assert.Contains(revive, e => e is RevivedEvent);
        var moved = Assert.Single(revive.OfType<CharacterMovedEvent>(), e => e.CharacterId == 1);
        Assert.Equal(Point(5, 5), moved.From);

        var revived = battle.GetCharacter(1)!;
        var combined = revived.Radius + battle.GetCharacter(2)!.Radius;
        Assert.True(
            FixVector2.DistanceSquared(revived.Position, Point(5, 5)) >= combined * combined,
            $"revived at {revived.Position} still overlaps the enemy");

        battle.Enqueue(new MoveDirectionCommand(81, 1, Point(1, 0)));
        battle.Enqueue(new MoveDirectionCommand(81, 2, Point(-1, 0)));
        battle.Tick();
        Assert.NotEqual(revived.Position, battle.GetCharacter(1)!.Position);
        Assert.NotEqual(Point(5, 5), battle.GetCharacter(2)!.Position);
    }

    [Fact]
    public void PickupAttributeEffect_SkipsAttributesThePickerLacks()
    {
        var attrs = TestBattleFactory.Bindings.AllIds().Concat(["attack", "bonus"]).ToArray();
        var values = TestBattleFactory.Bindings.AllIds().Concat(["attack"])
            .ToDictionary(id => id, _ => 0L, StringComparer.Ordinal);
        values["max_hp"] = 100;
        values["crit_damage"] = 15_000;
        values["move_speed"] = 3;
        var template = new CharacterTemplate("hero", Fix64.FromRatio(1, 2), values);
        var pickup = new PickupDefinition(
            "orb",
            Fix64.One,
            PickupKind.Instant,
            [
                new PickupAttributeEffect("bonus", ModifierKind.Flat, 5),
                new PickupAttributeEffect("attack", ModifierKind.Flat, 7),
            ]);
        var config = new BattleConfig(attrs, TestBattleFactory.Bindings, [template], pickupDefinitions: [pickup]);
        var battle = TestBattleFactory.CreateBattle(config);
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(5, 5)));
        battle.Enqueue(new SpawnPickupCommand(0, "orb", Point(5, 5)));

        var events = battle.Tick();

        Assert.Single(events.OfType<PickedUpEvent>());
        Assert.Null(battle.GetAttribute(1, "bonus"));
        Assert.Equal(7, battle.GetAttribute(1, "attack"));
    }

    [Fact]
    public void MoveTo_OutsideMapBounds_IsRejectedInsteadOfOverflowing()
    {
        var battle = TestBattleFactory.CreateBattle();
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(5, 5)));
        battle.Tick();
        battle.Enqueue(new MoveToCommand(1, 1, Point(100_000_000, 0)));

        var events = battle.Tick();

        Assert.Single(events.OfType<CommandRejectedEvent>());
        Assert.Equal(Point(5, 5), battle.GetCharacter(1)!.Position);
    }

    [Fact]
    public void CastSkill_TargetPointOutsideMap_IsRejectedForDirectedSkills()
    {
        var dash = new SkillDefinition("dash", 1, 0, [new SkillDashEffect(0, Fix64.FromInt(3))]);
        var battle = TestBattleFactory.CreateBattle(WithSkill(TestBattleFactory.CreateConfig(), dash));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(5, 5)));
        battle.Tick();
        battle.Enqueue(new CastSkillCommand(1, 1, "dash", TargetPoint: Point(100_000_000, 0)));

        var events = battle.Tick();

        Assert.Single(events.OfType<CommandRejectedEvent>());
        Assert.Empty(events.OfType<SkillStartedEvent>());
    }

    [Fact]
    public void HugeDirectionVectors_NormalizeWithoutOverflow()
    {
        var huge = new FixVector2(Fix64.FromInt(300_000_000), Fix64.FromInt(-400_000_000));

        var unit = huge.NormalizeOrZero();

        Assert.True(unit.X > Fix64.Zero && unit.Y < Fix64.Zero);
        var lengthSquared = unit.LengthSquared;
        Assert.True(
            Fix64.Abs(lengthSquared - Fix64.One) <= Fix64.FromRaw(16),
            $"|unit|^2 = {lengthSquared}");

        var battle = TestBattleFactory.CreateBattle();
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(5, 5)));
        battle.Tick();
        battle.Enqueue(new MoveDirectionCommand(1, 1, huge));
        var events = battle.Tick();
        Assert.Single(events.OfType<CharacterMovedEvent>());
    }

    [Fact]
    public void NegativeMoveSpeed_StopsMovementInsteadOfReversingOrThrowing()
    {
        var slow = new BuffDefinition(
            "slow",
            0,
            BuffStackingRule.Unique,
            [new AttributeBuffEffect("move_speed", ModifierKind.Flat, -10)]);
        var baseConfig = TestBattleFactory.CreateConfig();
        var template = baseConfig.CharacterTemplates["hero"];
        var config = new BattleConfig(
            baseConfig.AttributeIds,
            baseConfig.Bindings,
            [template],
            buffDefinitions: [slow]);

        foreach (var sideView in new[] { false, true })
        {
            var battle = sideView
                ? SideViewBattle(config, [Box(0, 0, 20, 2)])
                : TestBattleFactory.CreateBattle(config);
            var start = sideView
                ? new FixVector2(Fix64.FromInt(5), Fix64.FromRatio(5, 2))
                : Point(5, 5);
            battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, start));
            battle.Tick();
            battle.Enqueue(new ApplyBuffCommand(1, 1, 1, "slow"));
            battle.Enqueue(new MoveToCommand(1, 1, Point(10, 2)));

            var events = battle.Tick();

            Assert.Empty(events.OfType<CharacterMovedEvent>());
            Assert.Equal(start, battle.GetCharacter(1)!.Position);
        }
    }

    [Fact]
    public void Config_RejectsNonPositiveMaxHpAndNegativeMoveSpeed()
    {
        var values = TestBattleFactory.Bindings.AllIds()
            .ToDictionary(id => id, _ => 0L, StringComparer.Ordinal);
        values["max_hp"] = 0;
        values["move_speed"] = -1;
        var template = new CharacterTemplate("hero", Fix64.FromRatio(1, 2), values);

        var exception = Assert.Throws<ConfigurationException>(() =>
            new BattleConfig(values.Keys, TestBattleFactory.Bindings, [template]));

        Assert.Contains(exception.Errors, error => error.Contains("MaxHP", StringComparison.Ordinal));
        Assert.Contains(exception.Errors, error => error.Contains("MoveSpeed", StringComparison.Ordinal));
    }

    [Fact]
    public void SideView_RestingOnPlatformCorner_IsGroundedAndCanJump()
    {
        var battle = SideViewBattle(TestBattleFactory.CreateConfig(), [Box(0, 0, 10, 2)]);
        battle.Enqueue(new SpawnCharacterCommand(
            0,
            "hero",
            1,
            new FixVector2(Fix64.FromRatio(103, 10), Fix64.FromInt(8))));
        battle.Tick();
        for (var frame = 1; frame <= 120; frame++)
        {
            battle.Tick();
        }

        var perched = battle.GetCharacter(1)!;
        Assert.True(perched.Grounded, $"y={perched.Position.Y} vy={perched.VerticalVelocity}");
        Assert.True(perched.Position.Y > Fix64.FromInt(2));

        battle.Enqueue(new JumpCommand(121, 1));
        var events = battle.Tick();
        Assert.Empty(events.OfType<CommandRejectedEvent>());
        Assert.True(battle.GetCharacter(1)!.VerticalVelocity > Fix64.Zero);
    }

    [Fact]
    public void SideView_StandingOnEnemyHead_IsGroundedAndFallsWhenTheyLeave()
    {
        var battle = SideViewBattle(TestBattleFactory.CreateConfig(), [Box(0, 0, 20, 2)]);
        battle.Enqueue(new SpawnCharacterCommand(
            0,
            "hero",
            2,
            new FixVector2(Fix64.FromInt(5), Fix64.FromRatio(5, 2)))); // id 1: floor
        battle.Enqueue(new SpawnCharacterCommand(
            0,
            "hero",
            1,
            new FixVector2(Fix64.FromInt(5), Fix64.FromInt(8))));       // id 2: falls
        battle.Tick();
        for (var frame = 1; frame <= 120; frame++)
        {
            battle.Tick();
        }

        var onHead = battle.GetCharacter(2)!;
        Assert.True(onHead.Grounded, $"y={onHead.Position.Y} vy={onHead.VerticalVelocity}");
        Assert.True(onHead.Position.Y > Fix64.FromInt(3));

        battle.Enqueue(new MoveDirectionCommand(121, 1, Point(1, 0)));
        for (var frame = 121; frame <= 200; frame++)
        {
            battle.Tick();
        }

        var landed = battle.GetCharacter(2)!;
        Assert.True(landed.Grounded);
        Assert.Equal(Fix64.FromRatio(5, 2), landed.Position.Y);
    }

    [Fact]
    public void LeashReturn_BlockedByEnemyOnOrigin_ReengagesInsteadOfIdlingForever()
    {
        var strike = new SkillDefinition(
            "strike",
            1,
            10,
            [new SkillDamageEffect(0, SkillTarget.ExplicitEnemy, DamageType.True, 10)],
            range: Fix64.One);
        var shove = new SkillDefinition(
            "shove",
            1,
            0,
            [new SkillKnockbackEffect(0, SkillTarget.ExplicitEnemy, Fix64.FromInt(6), AlongCastDirection: true)]);
        var brain = new BrainDefinition(
            "chase",
            BrainKind.ChaseAttack,
            aggroRadius: Fix64.FromInt(10),
            attackRange: Fix64.One,
            leashRadius: Fix64.FromInt(3));
        var baseConfig = TestBattleFactory.CreateConfig();
        var template = baseConfig.CharacterTemplates["hero"];
        var config = new BattleConfig(
            baseConfig.AttributeIds,
            baseConfig.Bindings,
            [
                new CharacterTemplate("player", template.Radius, template.BaseAttributes, skillIds: ["shove"]),
                new CharacterTemplate("mob", template.Radius, template.BaseAttributes, skillIds: ["strike"], brainId: "chase"),
            ],
            [strike, shove],
            brainDefinitions: [brain]);
        var battle = TestBattleFactory.CreateBattle(config);
        battle.Enqueue(new SpawnCharacterCommand(0, "mob", 2, Point(10, 10)));                              // id 1
        battle.Enqueue(new SpawnCharacterCommand(0, "player", 1, Point(10, 12), PlayerControlled: true));  // id 2
        battle.Tick();
        battle.Enqueue(new CastSkillCommand(1, 2, "shove", TargetId: 1, Direction: Point(0, -1)));
        battle.Tick();
        battle.Enqueue(new MoveToCommand(2, 2, Point(10, 10)));

        var hitFrame = -1L;
        for (var frame = 2; frame < 400 && hitFrame < 0; frame++)
        {
            if (battle.Tick().OfType<DamageEvent>().Any(e => e.TargetId == 2))
            {
                hitFrame = frame;
            }
        }

        Assert.True(hitFrame > 0, "mob never re-engaged after its return path was blocked");
        Assert.Equal(Point(10, 10), battle.GetCharacter(2)!.Position);
        Assert.Equal(2, battle.GetCharacter(1)!.CurrentTargetId);
    }

    private static BattleConfig WithSkill(BattleConfig baseConfig, SkillDefinition skill)
    {
        var template = baseConfig.CharacterTemplates["hero"];
        return new BattleConfig(
            baseConfig.AttributeIds,
            baseConfig.Bindings,
            [
                new CharacterTemplate(
                    template.Id,
                    template.Radius,
                    template.BaseAttributes,
                    template.Targetable,
                    template.Damageable,
                    template.BlocksMovement,
                    template.Lives,
                    [skill.Id],
                    template.BrainId,
                    template.CanBeRevived,
                    template.BaseResources),
            ],
            [skill]);
    }

    private static BattleInstance SideViewBattle(BattleConfig config, IReadOnlyList<FixAabb> terrain)
    {
        var map = new MapConfig(
            new FixAabb(Point(0, 0), Point(20, 20)),
            terrain,
            spatialMode: SpatialMode.SideView,
            sideView: new SideViewSettings(Fix64.FromInt(30), Fix64.FromInt(20), Fix64.FromInt(10)));
        return new BattleInstance(config, map, seed: 123, maxFrames: 100_000);
    }

    private static FixAabb Box(long minX, long minY, long maxX, long maxY) =>
        new(Point(minX, minY), Point(maxX, maxY));

    private static FixVector2 Point(long x, long y) =>
        new(Fix64.FromInt(x), Fix64.FromInt(y));
}
