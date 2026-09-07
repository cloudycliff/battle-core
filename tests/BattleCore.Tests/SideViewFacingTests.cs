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

public sealed class SideViewFacingTests
{
    [Fact]
    public void DiagonalCast_FacesAndDashesHorizontally()
    {
        var dash = new SkillDefinition(
            "dash",
            1,
            0,
            [new SkillDashEffect(0, Fix64.FromInt(4))]);
        var battle = CreateBattle(dash);
        Spawn(battle, Point(4, FloorY));
        battle.Enqueue(new CastSkillCommand(1, 1, "dash", Direction: Point(1, 1)));

        var events = battle.Tick();

        Assert.Equal(0, battle.GetCharacter(1)!.Facing.Step);
        Assert.Equal(Fix64.FromInt(8), battle.GetCharacter(1)!.Position.X);
        Assert.Equal(FloorY, battle.GetCharacter(1)!.Position.Y);
        Assert.True(battle.GetCharacter(1)!.Grounded);
        Assert.Empty(events.OfType<CommandRejectedEvent>());
    }

    [Fact]
    public void VerticalAim_KeepsCurrentFacing()
    {
        var dash = new SkillDefinition(
            "dash",
            1,
            0,
            [new SkillDashEffect(0, Fix64.FromInt(3))]);
        var battle = CreateBattle(dash);
        Spawn(battle, Point(8, FloorY));
        battle.Enqueue(new SetFacingCommand(1, 1, new Facing(512)));
        battle.Tick();
        Assert.Equal(512, battle.GetCharacter(1)!.Facing.Step);

        battle.Enqueue(new CastSkillCommand(2, 1, "dash", Direction: Point(0, 1)));
        battle.Tick();

        Assert.Equal(512, battle.GetCharacter(1)!.Facing.Step);
        Assert.Equal(Fix64.FromInt(5), battle.GetCharacter(1)!.Position.X);
        Assert.Equal(FloorY, battle.GetCharacter(1)!.Position.Y);
    }

    [Fact]
    public void WalkingLeft_UpdatesFacing()
    {
        var battle = CreateBattle();
        Spawn(battle, Point(8, FloorY));
        battle.Enqueue(new MoveDirectionCommand(1, 1, Point(-1, 1)));

        var events = battle.Tick();

        Assert.Equal(512, battle.GetCharacter(1)!.Facing.Step);
        Assert.Equal(512, Assert.Single(events.OfType<FacingChangedEvent>()).Facing.Step);
        Assert.True(battle.GetCharacter(1)!.Position.X < Fix64.FromInt(8));
        Assert.Equal(FloorY, battle.GetCharacter(1)!.Position.Y);
    }

    [Fact]
    public void SetFacing_SnapsUpToRight()
    {
        var battle = CreateBattle();
        Spawn(battle, Point(4, FloorY));
        battle.Enqueue(new SetFacingCommand(1, 1, new Facing(512)));
        battle.Tick();
        Assert.Equal(512, battle.GetCharacter(1)!.Facing.Step);

        battle.Enqueue(new SetFacingCommand(2, 1, new Facing(256)));
        var events = battle.Tick();

        Assert.Equal(0, Assert.Single(events.OfType<FacingChangedEvent>()).Facing.Step);
        Assert.Equal(0, battle.GetCharacter(1)!.Facing.Step);
    }

    [Fact]
    public void Projectile_IgnoresVerticalAimComponent()
    {
        var projectile = new ProjectileDefinition(
            "bolt",
            Fix64.FromInt(30),
            Fix64.FromRatio(1, 5),
            lifetimeFrames: 30,
            pierceCount: 0,
            TargetRelation.Enemies,
            [new HitDamageEffect(DamageType.True, 10, CanCrit: false)]);
        var skill = new SkillDefinition(
            "firebolt",
            1,
            0,
            [new SkillSpawnProjectileEffect(0, SkillTarget.Self, projectile.Id)]);
        var battle = CreateBattle(skill, [projectile]);
        Spawn(battle, Point(4, FloorY));
        battle.Enqueue(new CastSkillCommand(1, 1, "firebolt", Direction: Point(1, 1)));
        battle.Tick();

        var bolt = Assert.Single(battle.GetProjectiles());
        Assert.True(bolt.Position.X > Fix64.FromInt(4));
        Assert.Equal(FloorY, bolt.Position.Y);
    }

    private static readonly Fix64 FloorY = Fix64.FromRatio(1, 2);

    private static BattleInstance CreateBattle(
        SkillDefinition? skill = null,
        IReadOnlyList<ProjectileDefinition>? projectiles = null)
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
            FloorY,
            values,
            skillIds: skill is null ? [] : [skill.Id]);
        var config = new BattleConfig(
            values.Keys,
            TestBattleFactory.Bindings,
            [template],
            skill is null ? null : [skill],
            projectileDefinitions: projectiles);
        var map = new MapConfig(
            new FixAabb(Point(0, 0), Point(40, 8)),
            spatialMode: SpatialMode.SideView,
            sideView: new SideViewSettings(
                Gravity: Fix64.FromInt(30),
                MaximumFallSpeed: Fix64.FromInt(20),
                JumpSpeed: Fix64.FromInt(10)));
        return new BattleInstance(config, map, seed: 123, maxFrames: 1_000);
    }

    private static void Spawn(BattleInstance battle, FixVector2 position)
    {
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, position));
        Assert.Single(battle.Tick().OfType<CharacterSpawnedEvent>());
    }

    private static FixVector2 Point(long x, long y) =>
        new(Fix64.FromInt(x), Fix64.FromInt(y));

    private static FixVector2 Point(long x, Fix64 y) =>
        new(Fix64.FromInt(x), y);
}
