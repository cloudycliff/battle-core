using BattleCore.AreaEffects;
using BattleCore.Combat;
using BattleCore.Effects;
using BattleCore.Math;
using BattleCore.Projectiles;
using BattleCore.Randomness;
using BattleCore.Simulation.Commands;
using BattleCore.Simulation.Events;
using BattleCore.Skills;

namespace BattleCore.Simulation;

public sealed partial class BattleInstance
{

    private void SpawnProjectileFromSkill(
        CharacterState caster,
        ActiveSkillCast cast,
        SkillSpawnProjectileEffect effect,
        int effectIndex,
        List<BattleEvent> events)
    {
        if (!_config.ProjectileDefinitions.TryGetValue(effect.ProjectileId, out var definition))
        {
            return;
        }

        var direction = ResolveProjectileDirection(caster, cast);
        if (direction == FixVector2.Zero)
        {
            return;
        }

        var speedPerFrame = definition.Speed / Fix64.FromInt(LogicFramesPerSecond);
        var projectile = new ProjectileState
        {
            Id = _nextEntityId++,
            Definition = definition,
            SourceId = caster.Id,
            FactionId = caster.FactionId,
            Position = caster.Position,
            Velocity = direction * speedPerFrame,
            ExpiryFrame = CurrentFrame + definition.LifetimeFrames,
            RemainingHits = definition.PierceCount + 1,
        };
        _projectiles.Add(projectile.Id, projectile);
        events.Add(new SkillHitEvent(
            CurrentFrame,
            caster.Id,
            cast.Definition.Id,
            effectIndex,
            caster.Id));
        events.Add(new ProjectileSpawnedEvent(
            CurrentFrame,
            projectile.Id,
            definition.Id,
            caster.Id,
            projectile.Position));
    }

    private FixVector2 ResolveProjectileDirection(
        CharacterState caster,
        ActiveSkillCast cast)
    {
        if (cast.Direction is { } directed && directed != FixVector2.Zero)
        {
            return directed;
        }

        if (cast.TargetPoint is { } point)
        {
            return (point - caster.Position).NormalizeOrZero();
        }

        if (cast.ExplicitTargetId is long targetId &&
            _characters.TryGetValue(targetId, out var target))
        {
            return (target.Position - caster.Position).NormalizeOrZero();
        }

        return FixVector2.Zero;
    }

    private void SpawnAreaEffectFromSkill(
        CharacterState caster,
        ActiveSkillCast cast,
        SkillSpawnAreaEffectEffect effect,
        int effectIndex,
        List<BattleEvent> events)
    {
        if (!_config.AreaEffectDefinitions.TryGetValue(effect.AreaEffectId, out var definition))
        {
            return;
        }

        var followId = definition.FollowsTarget
            ? effect.Target.Kind switch
            {
                SkillTargetKind.Self or SkillTargetKind.CircleAtCaster => caster.Id,
                SkillTargetKind.ExplicitTarget => cast.ExplicitTargetId,
                SkillTargetKind.CurrentTarget => caster.CurrentTargetId,
                _ => null,
            }
            : null;

        var position = followId is long id && _characters.TryGetValue(id, out var followed)
            ? followed.Position
            : effect.Target.Kind switch
            {
                SkillTargetKind.CircleAtPoint or SkillTargetKind.RingAtPoint
                    when cast.TargetPoint is { } point => point,
                SkillTargetKind.ExplicitTarget when cast.ExplicitTargetId is long targetId &&
                    _characters.TryGetValue(targetId, out var target) => target.Position,
                SkillTargetKind.CurrentTarget when caster.CurrentTargetId is long currentId &&
                    _characters.TryGetValue(currentId, out var current) => current.Position,
                _ => caster.Position,
            };

        var areaId = _nextEntityId++;
        var rng = DeterministicRng.Derive(Seed, areaId, AreaStream);
        var area = new AreaEffectState
        {
            Id = areaId,
            Definition = definition,
            SourceId = caster.Id,
            FactionId = caster.FactionId,
            Position = position,
            FollowTargetId = followId,
            NextTickFrame = definition.TickOnSpawn
                ? CurrentFrame
                : CurrentFrame + definition.IntervalFrames,
            ExpiryFrame = CurrentFrame + definition.DurationFrames,
            CombatSnapshot = CaptureCombatSnapshot(caster, rng),
        };
        _areaEffects.Add(area.Id, area);
        events.Add(new SkillHitEvent(
            CurrentFrame,
            caster.Id,
            cast.Definition.Id,
            effectIndex,
            caster.Id));
        events.Add(new AreaEffectSpawnedEvent(
            CurrentFrame,
            area.Id,
            definition.Id,
            caster.Id,
            area.Position));
    }

    private void AdvanceProjectiles(List<BattleEvent> events)
    {
        foreach (var projectile in _projectiles.Values.ToArray())
        {
            if (TryDespawnProjectileOnTerrain(projectile, events))
            {
                continue;
            }

            if (TryCollideProjectile(projectile, events))
            {
                continue;
            }

            var from = projectile.Position;
            var destination = from + projectile.Velocity;
            var blockedByTerrain = false;
            if (projectile.Definition.CollidesWithTerrain)
            {
                var lastSafe = LastSafeProjectilePosition(from, projectile.Velocity, projectile.Definition.Radius);
                blockedByTerrain = lastSafe != destination;
                destination = lastSafe;
            }

            projectile.Position = destination;
            if (destination != from)
            {
                events.Add(new ProjectileMovedEvent(
                    CurrentFrame,
                    projectile.Id,
                    from,
                    projectile.Position));
            }

            if (SweepCollideProjectile(projectile, from, events))
            {
                continue;
            }

            if (blockedByTerrain ||
                !_map.Bounds.ContainsCircle(projectile.Position, projectile.Definition.Radius) ||
                CurrentFrame >= projectile.ExpiryFrame)
            {
                DespawnProjectile(projectile, events);
            }
        }
    }

    private bool TryDespawnProjectileOnTerrain(
        ProjectileState projectile,
        List<BattleEvent> events)
    {
        if (!projectile.Definition.CollidesWithTerrain ||
            !IntersectsTerrain(projectile.Position, projectile.Definition.Radius))
        {
            return false;
        }

        DespawnProjectile(projectile, events);
        return true;
    }

    private FixVector2 LastSafeProjectilePosition(FixVector2 from, FixVector2 delta, Fix64 radius)
    {
        if (delta == FixVector2.Zero || IntersectsTerrain(from + delta, radius) is false)
        {
            return from + delta;
        }

        if (IntersectsTerrain(from, radius))
        {
            return from;
        }

        var maximumAxisDistance = Fix64.Max(Fix64.Abs(delta.X), Fix64.Abs(delta.Y));
        var sampleDistanceRaw = System.Math.Max(1, radius.RawValue / 2);
        var sampleCount = maximumAxisDistance.RawValue / sampleDistanceRaw;
        if (maximumAxisDistance.RawValue % sampleDistanceRaw != 0)
        {
            sampleCount++;
        }

        sampleCount = System.Math.Max(1, sampleCount);
        var previous = from;
        for (long sample = 1; sample <= sampleCount; sample++)
        {
            var fraction = Fix64.FromRatio(sample, sampleCount);
            var candidate = from + (delta * fraction);
            if (IntersectsTerrain(candidate, radius))
            {
                return previous;
            }

            previous = candidate;
        }

        return from + delta;
    }

    private bool IntersectsTerrain(FixVector2 position, Fix64 radius) =>
        _map.Terrain.Any(blocker => blocker.IntersectsCircle(position, radius));

    private bool TryCollideProjectile(ProjectileState projectile, List<BattleEvent> events)
    {
        if (!_characters.TryGetValue(projectile.SourceId, out var source))
        {
            DespawnProjectile(projectile, events);
            return true;
        }

        foreach (var target in _characters.Values)
        {
            if (target.Id == projectile.SourceId ||
                projectile.HitCharacterIds.Contains(target.Id) ||
                !target.IsSelectable ||
                !IsRelationMatch(source, target, projectile.Definition.Relation))
            {
                continue;
            }

            var combined = projectile.Definition.Radius + target.Template.Radius;
            if (FixVector2.DistanceSquared(projectile.Position, target.Position) >
                combined * combined)
            {
                continue;
            }

            projectile.HitCharacterIds.Add(target.Id);
            projectile.RemainingHits--;
            events.Add(new ProjectileHitEvent(
                CurrentFrame,
                projectile.Id,
                target.Id));
            ApplyHitEffects(source.Id, target, projectile.Definition.HitEffects, events);

            if (projectile.RemainingHits <= 0)
            {
                DespawnProjectile(projectile, events);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Continuous collision along this frame's travel segment so fast
    /// projectiles cannot tunnel through characters. Targets are hit in
    /// path order (closest-approach parameter, then entityId).
    /// </summary>
    private bool SweepCollideProjectile(
        ProjectileState projectile,
        FixVector2 from,
        List<BattleEvent> events)
    {
        if (!_characters.TryGetValue(projectile.SourceId, out var source))
        {
            DespawnProjectile(projectile, events);
            return true;
        }

        var to = projectile.Position;
        var delta = to - from;
        var hits = new List<(Fix64 Parameter, CharacterState Target)>();
        foreach (var target in _characters.Values)
        {
            if (target.Id == projectile.SourceId ||
                projectile.HitCharacterIds.Contains(target.Id) ||
                !target.IsSelectable ||
                !IsRelationMatch(source, target, projectile.Definition.Relation))
            {
                continue;
            }

            var combined = projectile.Definition.Radius + target.Template.Radius;
            if (!SweptGeometry.CircleSweepHitsCircle(from, to, target.Position, combined))
            {
                continue;
            }

            hits.Add((
                SweptGeometry.ClosestParameter(from, delta, target.Position),
                target));
        }

        foreach (var (_, target) in hits
                     .OrderBy(item => item.Parameter.RawValue)
                     .ThenBy(item => item.Target.Id))
        {
            projectile.HitCharacterIds.Add(target.Id);
            projectile.RemainingHits--;
            events.Add(new ProjectileHitEvent(
                CurrentFrame,
                projectile.Id,
                target.Id));
            ApplyHitEffects(source.Id, target, projectile.Definition.HitEffects, events);

            if (projectile.RemainingHits <= 0)
            {
                DespawnProjectile(projectile, events);
                return true;
            }
        }

        return false;
    }

    private void DespawnProjectile(ProjectileState projectile, List<BattleEvent> events)
    {
        if (_projectiles.Remove(projectile.Id))
        {
            events.Add(new ProjectileDespawnedEvent(CurrentFrame, projectile.Id));
        }
    }

    private void AdvanceAreaEffects(List<BattleEvent> events)
    {
        foreach (var area in _areaEffects.Values.ToArray())
        {
            if (area.FollowTargetId is long followId)
            {
                if (!_characters.TryGetValue(followId, out var followed) ||
                    followed.LifeState == LifeState.Dead)
                {
                    DespawnAreaEffect(area, events);
                    continue;
                }

                area.Position = followed.Position;
            }

            if (CurrentFrame >= area.ExpiryFrame)
            {
                DespawnAreaEffect(area, events);
                continue;
            }

            if (CurrentFrame < area.NextTickFrame)
            {
                continue;
            }

            events.Add(new AreaEffectTickedEvent(CurrentFrame, area.Id));
            foreach (var target in ResolveCircle(area.Position, area.Definition.Radius, false)
                         .Where(candidate =>
                             IsRelationMatch(
                                 area.SourceId,
                                 area.FactionId,
                                 candidate,
                                 area.Definition.Relation))
                         .OrderBy(candidate => candidate.Id))
            {
                ApplyHitEffects(
                    area.SourceId,
                    target,
                    area.Definition.HitEffects,
                    events,
                    area.CombatSnapshot);
            }

            area.NextTickFrame = CurrentFrame + area.Definition.IntervalFrames;
        }
    }

    private void DespawnAreaEffect(AreaEffectState area, List<BattleEvent> events)
    {
        if (_areaEffects.Remove(area.Id))
        {
            events.Add(new AreaEffectDespawnedEvent(CurrentFrame, area.Id));
        }
    }

    private void ApplyHitEffects(
        long sourceId,
        CharacterState target,
        IReadOnlyList<HitEffect> hitEffects,
        List<BattleEvent> events,
        CombatSourceSnapshot? snapshot = null)
    {
        foreach (var hitEffect in hitEffects)
        {
            switch (hitEffect)
            {
                case HitDamageEffect damage:
                    QueueCombat(
                        target.Id,
                        _nextCommandSequence++,
                        new DealDamageCommand(
                            CurrentFrame,
                            sourceId,
                            target.Id,
                            damage.DamageType,
                            damage.Flat,
                            damage.AttributeId,
                            damage.Ratio,
                            damage.CanCrit),
                        events,
                        snapshot);
                    break;
                case HitHealEffect heal:
                    QueueCombat(
                        target.Id,
                        _nextCommandSequence++,
                        new HealCommand(
                            CurrentFrame,
                            sourceId,
                            target.Id,
                            heal.Flat,
                            heal.AttributeId,
                            heal.Ratio,
                            heal.CanCrit),
                        events,
                        snapshot);
                    break;
                case HitShieldEffect shield:
                    GrantShield(
                        new GrantShieldCommand(
                            CurrentFrame,
                            target.Id,
                            shield.Amount,
                            shield.DurationFrames),
                        events);
                    break;
                case HitApplyBuffEffect buff:
                    ApplyBuff(
                        new ApplyBuffCommand(
                            CurrentFrame,
                            sourceId,
                            target.Id,
                            buff.BuffId),
                        events,
                        snapshot);
                    break;
            }
        }
    }
}
