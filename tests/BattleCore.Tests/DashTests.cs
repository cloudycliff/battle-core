using BattleCore.Buffs;
using BattleCore.Configuration;
using BattleCore.Math;
using BattleCore.Simulation;
using BattleCore.Simulation.Commands;
using BattleCore.Simulation.Events;
using BattleCore.Skills;

namespace BattleCore.Tests;

public sealed class DashTests
{
    [Fact]
    public void DirectionDash_MovesConfiguredDistance()
    {
        var battle = CreateBattle(Dash());
        SpawnCaster(battle);
        battle.Enqueue(new CastSkillCommand(
            1,
            1,
            "dash",
            Direction: Point(1, 0)));

        var events = battle.Tick();

        var moved = Assert.Single(events.OfType<CharacterMovedEvent>());
        Assert.Equal(Point(2, 2), moved.From);
        Assert.Equal(Point(8, 2), moved.To);
        Assert.Equal(Point(8, 2), battle.GetCharacter(1)!.Position);
        Assert.Single(events.OfType<SkillHitEvent>());
    }

    [Fact]
    public void PointDash_StopsAtNearerTargetPoint()
    {
        var battle = CreateBattle(Dash());
        SpawnCaster(battle);
        battle.Enqueue(new CastSkillCommand(
            1,
            1,
            "dash",
            TargetPoint: Point(5, 2)));

        battle.Tick();

        Assert.Equal(Point(5, 2), battle.GetCharacter(1)!.Position);
    }

    [Fact]
    public void TargetDash_UsesTargetAsDirection()
    {
        var battle = CreateBattle(Dash(Fix64.FromInt(2)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 2, Point(8, 2)));
        battle.Tick();
        battle.Enqueue(new CastSkillCommand(1, 1, "dash", TargetId: 2));

        battle.Tick();

        Assert.Equal(Point(4, 2), battle.GetCharacter(1)!.Position);
    }

    [Fact]
    public void Dash_DoesNotTunnelThroughTerrain()
    {
        var wall = new FixAabb(Point(5, 0), Point(6, 10));
        var battle = CreateBattle(Dash(Fix64.FromInt(8)), terrain: [wall]);
        SpawnCaster(battle);
        battle.Enqueue(new CastSkillCommand(
            1,
            1,
            "dash",
            Direction: Point(1, 0)));

        battle.Tick();

        Assert.Equal(
            new FixVector2(Fix64.FromRatio(9, 2), Fix64.FromInt(2)),
            battle.GetCharacter(1)!.Position);
    }

    [Fact]
    public void Dash_StopsAtEnemyCollision()
    {
        var battle = CreateBattle(Dash(Fix64.FromInt(8)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 2, Point(6, 2)));
        battle.Tick();
        battle.Enqueue(new CastSkillCommand(
            1,
            1,
            "dash",
            Direction: Point(1, 0)));

        battle.Tick();

        Assert.Equal(Point(5, 2), battle.GetCharacter(1)!.Position);
    }

    [Fact]
    public void Dash_RejectsMissingDirectionAndRootedCaster()
    {
        var root = new BuffDefinition(
            "root",
            durationFrames: 0,
            BuffStackingRule.Unique,
            [new ControlBuffEffect(ControlType.Root)]);
        var battle = CreateBattle(Dash(), [root]);
        SpawnCaster(battle);
        battle.Enqueue(new CastSkillCommand(1, 1, "dash"));
        Assert.Single(battle.Tick().OfType<CommandRejectedEvent>());

        battle.Enqueue(new ApplyBuffCommand(2, 1, 1, root.Id));
        battle.Tick();
        battle.Enqueue(new CastSkillCommand(
            3,
            1,
            "dash",
            Direction: Point(1, 0)));

        Assert.Single(battle.Tick().OfType<CommandRejectedEvent>());
        Assert.Equal(Point(2, 2), battle.GetCharacter(1)!.Position);
    }

    [Fact]
    public void Config_RejectsNonPositiveDashDistance()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            Dash(Fix64.Zero));

        Assert.Contains("dash distance", error.Message, StringComparison.Ordinal);
    }

    private static SkillDefinition Dash(Fix64? distance = null) =>
        new(
            "dash",
            durationFrames: 1,
            cooldownFrames: 0,
            [new SkillDashEffect(0, distance ?? Fix64.FromInt(6))]);

    private static BattleInstance CreateBattle(
        SkillDefinition skill,
        IEnumerable<BuffDefinition>? buffs = null,
        IEnumerable<FixAabb>? terrain = null)
    {
        var values = TestBattleFactory.Bindings.AllIds()
            .ToDictionary(id => id, _ => 0L, StringComparer.Ordinal);
        values[TestBattleFactory.Bindings.MaxHp] = 100;
        values[TestBattleFactory.Bindings.CritDamage] = 15_000;
        values[TestBattleFactory.Bindings.MoveSpeed] = 3;
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
            buffs);
        return TestBattleFactory.CreateBattle(config, terrain);
    }

    private static void SpawnCaster(BattleInstance battle)
    {
        battle.Enqueue(new SpawnCharacterCommand(0, "hero", 1, Point(2, 2)));
        battle.Tick();
    }

    private static FixVector2 Point(long x, long y) =>
        new(Fix64.FromInt(x), Fix64.FromInt(y));
}
