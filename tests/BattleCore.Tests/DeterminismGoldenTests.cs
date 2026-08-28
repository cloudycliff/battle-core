using BattleCore.Combat;
using BattleCore.Math;
using BattleCore.Serialization;
using BattleCore.Simulation;
using BattleCore.Simulation.Commands;
using BattleCore.Simulation.Events;

namespace BattleCore.Tests;

/// <summary>
/// Golden regression for the core determinism contract: the same bundle,
/// seed, and command script must yield an identical event stream on every
/// run. Any nondeterminism (hash-order iteration, RNG misuse, uninitialized
/// state) shows up as a diff here.
/// </summary>
public sealed class DeterminismGoldenTests
{
    private const int ScriptFrames = 300;

    [Fact]
    public void SameBundleAndScript_ProducesIdenticalEventStream()
    {
        var first = RunScriptedBattle();
        var second = RunScriptedBattle();

        Assert.True(
            first.Count > 50,
            $"Scenario produced only {first.Count} events; too trivial to guard determinism.");
        Assert.Equal(first, second);
    }

    private static List<BattleEvent> RunScriptedBattle()
    {
        var json = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "samples", "arena.bundle.json"));
        var battle = BattleJson.CreateBattle(json);
        var marker = battle.GetPlayerSpawnMarkers().Single();
        var collected = new List<BattleEvent>();
        long? goblinId = null;

        battle.Enqueue(new SpawnCharacterCommand(
            0,
            "hero",
            1,
            marker.Position,
            PlayerControlled: true));

        for (long frame = 0; frame < ScriptFrames; frame++)
        {
            var events = battle.Tick();
            collected.AddRange(events);

            goblinId ??= events
                .OfType<CharacterSpawnedEvent>()
                .FirstOrDefault(spawned => spawned.TemplateId == "goblin")
                ?.CharacterId;

            var next = frame + 1;
            if (next == 5)
            {
                battle.Enqueue(new MoveDirectionCommand(
                    next,
                    1,
                    new FixVector2(Fix64.One, Fix64.Zero)));
            }

            if (next == 40)
            {
                battle.Enqueue(new StopMovingCommand(next, 1));
            }

            if (next == 45 && goblinId is long targetId)
            {
                battle.Enqueue(new SetTargetCommand(next, 1, targetId));
            }

            if (next >= 50 && next % 20 == 0)
            {
                battle.Enqueue(new CastSkillCommand(next, 1, "strike"));
            }

            if (next == 120)
            {
                battle.Enqueue(new MoveToCommand(
                    next,
                    1,
                    new FixVector2(Fix64.FromInt(15), Fix64.FromInt(15))));
            }

            if (next == 150)
            {
                battle.Enqueue(new HealCommand(next, 1, 1, Flat: 15, CanCrit: false));
            }

            if (next == 160)
            {
                battle.Enqueue(new SpawnCharacterCommand(
                    next,
                    "hero",
                    1,
                    marker.Position + new FixVector2(Fix64.FromInt(2), Fix64.Zero)));
            }
        }

        return collected;
    }
}
