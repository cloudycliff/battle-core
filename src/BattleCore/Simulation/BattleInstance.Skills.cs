using BattleCore.Buffs;
using BattleCore.Combat;
using BattleCore.Math;
using BattleCore.Simulation.Commands;
using BattleCore.Simulation.Events;
using BattleCore.Skills;

namespace BattleCore.Simulation;

public sealed partial class BattleInstance
{

    private void CastSkill(CastSkillCommand command, List<BattleEvent> events)
    {
        if (!TryGetLiving(command.CasterId, command, events, out var caster))
        {
            return;
        }

        if (!_config.SkillDefinitions.TryGetValue(command.SkillId, out var skill) ||
            !caster.Template.SkillIds.Contains(command.SkillId, StringComparer.Ordinal))
        {
            Reject(command, events, $"Character does not own skill '{command.SkillId}'.");
            return;
        }

        if (caster.HasControl(ControlType.Stun) ||
            (skill.Effects.OfType<SkillDashEffect>().Any() &&
             caster.HasControl(ControlType.Root)) ||
            (skill.Category == SkillCategory.Spell &&
             caster.HasControl(ControlType.Silence)) ||
            (skill.Category == SkillCategory.Weapon &&
             caster.HasControl(ControlType.Disarm)))
        {
            Reject(command, events, "A control effect prevents casting this skill.");
            return;
        }

        if (caster.ActiveSkill is not null)
        {
            Reject(command, events, "Character is already casting a skill.");
            return;
        }

        if (caster.CooldownReadyFrames.TryGetValue(skill.Id, out var readyFrame) &&
            CurrentFrame < readyFrame)
        {
            Reject(command, events, $"Skill '{skill.Id}' is on cooldown until frame {readyFrame}.");
            return;
        }

        if (skill.Effects.Any(effect =>
                effect.Target.Kind == SkillTargetKind.ExplicitTarget) &&
            !HasValidExplicitTarget(command, skill, caster, events))
        {
            return;
        }

        if (skill.Effects.Any(effect =>
                effect.Target.Kind is
                    SkillTargetKind.CircleAtPoint or
                    SkillTargetKind.RingAtPoint) &&
            (command.TargetPoint is not FixVector2 point ||
             !_map.Bounds.ContainsCircle(point, Fix64.Zero)))
        {
            Reject(command, events, "Skill requires a target point inside map bounds.");
            return;
        }

        if (skill.Effects.Any(effect =>
                effect.Target.Kind == SkillTargetKind.FanAtCaster) &&
            (command.Direction is not FixVector2 direction ||
             direction == FixVector2.Zero))
        {
            Reject(command, events, "Skill requires a non-zero direction.");
            return;
        }

        if (skill.Effects.OfType<SkillSpawnProjectileEffect>().Any() &&
            (command.Direction is null || command.Direction == FixVector2.Zero) &&
            command.TargetPoint is null &&
            command.TargetId is null)
        {
            Reject(command, events, "Projectile skills require a direction, point, or target.");
            return;
        }

        if (skill.Effects.OfType<SkillThrowCarryEffect>().Any() &&
            (command.Direction is null || command.Direction == FixVector2.Zero) &&
            command.TargetPoint is null)
        {
            Reject(command, events, "Throw skills require a direction or point.");
            return;
        }

        if (skill.Effects.OfType<SkillDashEffect>().Any() &&
            (command.Direction is null || command.Direction == FixVector2.Zero) &&
            command.TargetPoint is null &&
            command.TargetId is null)
        {
            Reject(command, events, "Dash skills require a direction, point, or target.");
            return;
        }

        if (skill.Effects.OfType<SkillDashEffect>().Any() &&
            caster.CarriedById is not null)
        {
            Reject(command, events, "A carried character cannot dash.");
            return;
        }

        if (!TrySpendResources(caster, skill.ResourceCosts, events, command))
        {
            return;
        }

        var cooldownReadyFrame = CurrentFrame + skill.CooldownFrames;
        caster.CooldownReadyFrames[skill.Id] = cooldownReadyFrame;
        caster.ActiveSkill = new ActiveSkillCast
        {
            Definition = skill,
            StartedFrame = CurrentFrame,
            ExplicitTargetId = command.TargetId,
            TargetPoint = command.TargetPoint,
            Direction = command.Direction?.NormalizeOrZero(),
        };
        events.Add(new SkillStartedEvent(
            CurrentFrame,
            caster.Id,
            skill.Id,
            cooldownReadyFrame));
    }

    private bool HasValidExplicitTarget(
        CastSkillCommand command,
        SkillDefinition skill,
        CharacterState caster,
        List<BattleEvent> events)
    {
        if (command.TargetId is not long targetId ||
            !_characters.TryGetValue(targetId, out var target))
        {
            Reject(command, events, "Skill requires an available explicit target.");
            return false;
        }

        var explicitTargets = skill.Effects
            .Where(effect => effect.Target.Kind == SkillTargetKind.ExplicitTarget)
            .Select(effect => effect.Target)
            .ToArray();
        var corpsesOnly = explicitTargets.All(item => item.CorpsesOnly);
        var matchesRelation = explicitTargets.Any(item =>
            IsRelationMatch(caster, target, item.Relation));
        var valid = corpsesOnly
            ? target.IsReviveTarget && matchesRelation
            : target.IsSelectable;
        if (!valid)
        {
            Reject(
                command,
                events,
                corpsesOnly
                    ? "Skill requires an eligible dead ally."
                    : "Skill requires an available explicit target.");
            return false;
        }

        return true;
    }

    private void AdvanceSkills(List<BattleEvent> events)
    {
        foreach (var caster in _characters.Values.ToArray())
        {
            var cast = caster.ActiveSkill;
            if (cast is null || !caster.CanAct)
            {
                continue;
            }

            var elapsed = checked((int)(CurrentFrame - cast.StartedFrame));
            while (cast.NextEffectIndex < cast.Definition.Effects.Count)
            {
                var effectIndex = cast.NextEffectIndex;
                var effect = cast.Definition.Effects[effectIndex];
                if (effect.Frame > elapsed)
                {
                    break;
                }

                cast.NextEffectIndex++;
                if (effect.Frame == elapsed)
                {
                    ExecuteSkillEffect(caster, cast, effect, effectIndex, events);
                }
            }

            if (elapsed < cast.Definition.DurationFrames - 1)
            {
                continue;
            }

            caster.ActiveSkill = null;
            events.Add(new SkillEndedEvent(
                CurrentFrame,
                caster.Id,
                cast.Definition.Id));
        }
    }

    private void ExecuteSkillEffect(
        CharacterState caster,
        ActiveSkillCast cast,
        SkillEffect effect,
        int effectIndex,
        List<BattleEvent> events)
    {
        switch (effect)
        {
            case SkillSpawnProjectileEffect spawnProjectile:
                SpawnProjectileFromSkill(caster, cast, spawnProjectile, effectIndex, events);
                return;
            case SkillSpawnAreaEffectEffect spawnArea:
                SpawnAreaEffectFromSkill(caster, cast, spawnArea, effectIndex, events);
                return;
            case SkillSpawnCharacterEffect spawnCharacter:
                SpawnCharactersFromSkill(caster, cast, spawnCharacter, effectIndex, events);
                return;
            case SkillDropCarryEffect:
                DropCarry(caster, events);
                events.Add(new SkillHitEvent(
                    CurrentFrame,
                    caster.Id,
                    cast.Definition.Id,
                    effectIndex,
                    caster.Id));
                return;
            case SkillThrowCarryEffect throwCarry:
                ThrowCarry(caster, cast, throwCarry, events);
                events.Add(new SkillHitEvent(
                    CurrentFrame,
                    caster.Id,
                    cast.Definition.Id,
                    effectIndex,
                    caster.Id));
                return;
            case SkillDashEffect dash:
                Dash(caster, cast, dash, events);
                events.Add(new SkillHitEvent(
                    CurrentFrame,
                    caster.Id,
                    cast.Definition.Id,
                    effectIndex,
                    caster.Id));
                return;
        }

        foreach (var target in ResolveSkillTargets(caster, cast, effect.Target))
        {
            events.Add(new SkillHitEvent(
                CurrentFrame,
                caster.Id,
                cast.Definition.Id,
                effectIndex,
                target.Id));

            switch (effect)
            {
                case SkillDamageEffect damage:
                    {
                        var command = new DealDamageCommand(
                            CurrentFrame,
                            caster.Id,
                            target.Id,
                            damage.DamageType,
                            damage.Flat,
                            damage.AttributeId,
                            damage.Ratio,
                            damage.CanCrit);
                        QueueCombat(
                            target.Id,
                            _nextCommandSequence++,
                            command,
                            events);
                        break;
                    }
                case SkillHealEffect heal:
                    {
                        var command = new HealCommand(
                            CurrentFrame,
                            caster.Id,
                            target.Id,
                            heal.Flat,
                            heal.AttributeId,
                            heal.Ratio,
                            heal.CanCrit);
                        QueueCombat(
                            target.Id,
                            _nextCommandSequence++,
                            command,
                            events);
                        break;
                    }
                case SkillShieldEffect shield:
                    GrantShield(
                        new GrantShieldCommand(
                            CurrentFrame,
                            target.Id,
                            shield.Amount,
                            shield.DurationFrames),
                        events);
                    break;
                case SkillApplyBuffEffect buff:
                    ApplyBuff(
                        new ApplyBuffCommand(
                            CurrentFrame,
                            caster.Id,
                            target.Id,
                            buff.BuffId),
                        events);
                    break;
                case SkillStartCarryEffect:
                    TryStartCarry(caster, target, events);
                    break;
                case SkillReviveEffect:
                    TryRevive(caster, target, events);
                    break;
            }
        }
    }

    private void SpawnCharactersFromSkill(
        CharacterState caster,
        ActiveSkillCast cast,
        SkillSpawnCharacterEffect effect,
        int effectIndex,
        List<BattleEvent> events)
    {
        if (!_config.CharacterTemplates.TryGetValue(effect.TemplateId, out var template))
        {
            return;
        }

        var position = ResolveSummonPosition(caster, cast, effect.Target);
        events.Add(new SkillHitEvent(
            CurrentFrame,
            caster.Id,
            cast.Definition.Id,
            effectIndex,
            caster.Id));

        for (var index = 0; index < effect.Count; index++)
        {
            if (!IsPositionValid(
                    position,
                    template.Radius,
                    caster.FactionId,
                    template.BlocksMovement))
            {
                continue;
            }

            Spawn(
                new SpawnCharacterCommand(
                    CurrentFrame,
                    effect.TemplateId,
                    caster.FactionId,
                    position,
                    BrainId: effect.BrainId,
                    OwnerId: effect.BindOwner ? caster.Id : null),
                events);
        }
    }

    private void Dash(
        CharacterState caster,
        ActiveSkillCast cast,
        SkillDashEffect effect,
        List<BattleEvent> events)
    {
        if (caster.CarriedById is not null ||
            caster.HasControl(ControlType.Root))
        {
            return;
        }

        var desiredDelta = cast.Direction is { } direction &&
                           direction != FixVector2.Zero
            ? direction * effect.Distance
            : cast.TargetPoint is { } targetPoint
                ? DeltaToward(caster.Position, targetPoint, effect.Distance)
                : cast.ExplicitTargetId is long targetId &&
                  _characters.TryGetValue(targetId, out var target)
                    ? (target.Position - caster.Position).NormalizeOrZero() *
                      effect.Distance
                    : FixVector2.Zero;
        if (desiredDelta == FixVector2.Zero)
        {
            return;
        }

        var from = caster.Position;
        var fraction = FindMovementFraction(caster, desiredDelta);
        var destination = from + (desiredDelta * fraction);
        if (destination == from)
        {
            return;
        }

        caster.Position = destination;
        events.Add(new CharacterMovedEvent(
            CurrentFrame,
            caster.Id,
            from,
            destination));
        FollowCarries(events);
    }

    private FixVector2 ResolveSummonPosition(
        CharacterState caster,
        ActiveSkillCast cast,
        SkillTarget target) =>
        target.Kind switch
        {
            SkillTargetKind.CircleAtPoint or SkillTargetKind.RingAtPoint
                when cast.TargetPoint is { } point => point,
            SkillTargetKind.ExplicitTarget when cast.ExplicitTargetId is long targetId &&
                _characters.TryGetValue(targetId, out var targetCharacter) =>
                targetCharacter.Position,
            SkillTargetKind.CurrentTarget when caster.CurrentTargetId is long currentId &&
                _characters.TryGetValue(currentId, out var current) => current.Position,
            _ => caster.Position,
        };

    private IReadOnlyList<CharacterState> ResolveSkillTargets(
        CharacterState caster,
        ActiveSkillCast cast,
        SkillTarget target)
    {
        IEnumerable<CharacterState> candidates = target.Kind switch
        {
            SkillTargetKind.Self => [caster],
            SkillTargetKind.ExplicitTarget => ResolveOne(cast.ExplicitTargetId, target.CorpsesOnly),
            SkillTargetKind.CurrentTarget => ResolveOne(caster.CurrentTargetId, target.CorpsesOnly),
            SkillTargetKind.CircleAtPoint => ResolveCircle(
                cast.TargetPoint,
                target.Radius,
                target.CorpsesOnly),
            SkillTargetKind.CircleAtCaster => ResolveCircle(
                caster.Position,
                target.Radius,
                target.CorpsesOnly),
            SkillTargetKind.FanAtCaster => ResolveFan(
                caster,
                cast.Direction,
                target.Radius,
                target.MinimumDot,
                target.CorpsesOnly),
            SkillTargetKind.RingAtPoint => ResolveRing(
                cast.TargetPoint,
                target.InnerRadius,
                target.Radius,
                target.CorpsesOnly),
            _ => [],
        };

        return candidates
            .Where(candidate => IsRelationMatch(caster, candidate, target.Relation))
            .OrderBy(candidate => candidate.Id)
            .ToArray();
    }

    private IEnumerable<CharacterState> ResolveOne(long? characterId, bool corpsesOnly)
    {
        if (characterId is long id &&
            _characters.TryGetValue(id, out var character) &&
            MatchesTargetFilter(character, corpsesOnly))
        {
            yield return character;
        }
    }

    private IEnumerable<CharacterState> ResolveCircle(
        FixVector2? point,
        Fix64 radius,
        bool corpsesOnly)
    {
        if (point is not FixVector2 center)
        {
            yield break;
        }

        foreach (var character in _characters.Values)
        {
            if (!MatchesTargetFilter(character, corpsesOnly))
            {
                continue;
            }

            var hitRadius = radius + character.Template.Radius;
            if (FixVector2.DistanceSquared(center, character.Position) <=
                hitRadius * hitRadius)
            {
                yield return character;
            }
        }
    }

    private IEnumerable<CharacterState> ResolveFan(
        CharacterState caster,
        FixVector2? direction,
        Fix64 radius,
        Fix64 minimumDot,
        bool corpsesOnly)
    {
        if (direction is not FixVector2 facing || facing == FixVector2.Zero)
        {
            yield break;
        }

        foreach (var character in ResolveCircle(caster.Position, radius, corpsesOnly))
        {
            var offset = character.Position - caster.Position;
            if (offset == FixVector2.Zero)
            {
                yield return character;
                continue;
            }

            if (FixVector2.Dot(offset.NormalizeOrZero(), facing) >= minimumDot)
            {
                yield return character;
            }
        }
    }

    private IEnumerable<CharacterState> ResolveRing(
        FixVector2? point,
        Fix64 innerRadius,
        Fix64 outerRadius,
        bool corpsesOnly)
    {
        if (point is not FixVector2 center)
        {
            yield break;
        }

        foreach (var character in ResolveCircle(center, outerRadius, corpsesOnly))
        {
            var innerExclusionRadius = Fix64.Max(
                Fix64.Zero,
                innerRadius - character.Template.Radius);
            if (FixVector2.DistanceSquared(center, character.Position) >=
                innerExclusionRadius * innerExclusionRadius)
            {
                yield return character;
            }
        }
    }

    private static bool MatchesTargetFilter(CharacterState character, bool corpsesOnly) =>
        corpsesOnly
            ? character.IsReviveTarget
            : character.IsSelectable;

    private static bool IsRelationMatch(
        CharacterState caster,
        CharacterState target,
        TargetRelation relation) =>
        IsRelationMatch(caster.Id, caster.FactionId, target, relation);

    private static bool IsRelationMatch(
        long sourceId,
        int factionId,
        CharacterState target,
        TargetRelation relation) =>
        relation switch
        {
            TargetRelation.Self => sourceId == target.Id,
            TargetRelation.Allies => factionId == target.FactionId,
            TargetRelation.Enemies => factionId != target.FactionId,
            TargetRelation.Any => true,
            _ => false,
        };
}
