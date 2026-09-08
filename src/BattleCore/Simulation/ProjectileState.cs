using BattleCore.Combat;
using BattleCore.Math;
using BattleCore.Projectiles;

namespace BattleCore.Simulation;

internal sealed class ProjectileState
{
    public required long Id { get; init; }
    public required ProjectileDefinition Definition { get; init; }
    public required long SourceId { get; init; }
    public required int FactionId { get; init; }
    public required FixVector2 Position { get; set; }
    public required FixVector2 Velocity { get; init; }
    public required long ExpiryFrame { get; init; }
    public required int RemainingHits { get; set; }

    /// <summary>
    /// Source-side contribution frozen at launch (ADR 0042 semantics): an
    /// arrow already in flight keeps hitting after the archer dies or is
    /// despawned.
    /// </summary>
    public required CombatSourceSnapshot CombatSnapshot { get; init; }

    public HashSet<long> HitCharacterIds { get; } = [];
}
