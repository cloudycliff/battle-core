using BattleCore.AreaEffects;
using BattleCore.Combat;
using BattleCore.Effects;
using BattleCore.Math;
using BattleCore.Projectiles;
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

        var area = new AreaEffectState
        {
            Id = _nextEntityId++,
            Definition = definition,
            SourceId = caster.Id,
            FactionId = caster.FactionId,
            Position = position,
            FollowTargetId = followId,
            NextTickFrame = definition.TickOnSpawn
                ? CurrentFrame
                : CurrentFrame + definition.IntervalFrames,
            ExpiryFrame = CurrentFrame + definition.DurationFrames,
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
            if (TryCollideProjectile(projectile, events))
            {
                continue;
            }

            var from = projectile.Position;
            projectile.Position += projectile.Velocity;
            events.Add(new ProjectileMovedEvent(
                CurrentFrame,
                projectile.Id,
                from,
                projectile.Position));

            if (!_map.Bounds.ContainsCircle(projectile.Position, projectile.Definition.Radius) ||
                CurrentFrame >= projectile.ExpiryFrame)
            {
                DespawnProjectile(projectile, events);
                continue;
            }

            TryCollideProjectile(projectile, events);
        }
    }

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

            if (CurrentFrame < area.NextTickFrame ||
                !_characters.TryGetValue(area.SourceId, out var source))
            {
                continue;
            }

            events.Add(new AreaEffectTickedEvent(CurrentFrame, area.Id));
            foreach (var target in ResolveCircle(area.Position, area.Definition.Radius, false)
                         .Where(candidate =>
                             IsRelationMatch(source, candidate, area.Definition.Relation))
                         .OrderBy(candidate => candidate.Id))
            {
                ApplyHitEffects(source.Id, target, area.Definition.HitEffects, events);
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
        List<BattleEvent> events)
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
                        events);
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
                        events);
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
                        events);
                    break;
            }
        }
    }
}
