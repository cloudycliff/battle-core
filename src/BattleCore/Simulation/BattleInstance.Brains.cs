using BattleCore.Brains;
using BattleCore.Buffs;
using BattleCore.Math;
using BattleCore.Simulation.Commands;
using BattleCore.Simulation.Events;
using BattleCore.Skills;

namespace BattleCore.Simulation;

public sealed partial class BattleInstance
{
    private void RunBrains(List<BattleEvent> events)
    {
        foreach (var actor in _characters.Values)
        {
            if (actor.PlayerControlled ||
                !actor.CanAct ||
                actor.Brain is not { } brain)
            {
                continue;
            }

            switch (brain.Kind)
            {
                case BrainKind.Idle:
                    break;
                case BrainKind.HoldPosition:
                    ThinkHoldPosition(actor, brain);
                    break;
                case BrainKind.ChaseAttack:
                    ThinkChaseAttack(actor, brain);
                    break;
                case BrainKind.FollowOwner:
                    ThinkFollowOwner(actor, brain);
                    break;
            }
        }

        ConsumeCommands(events);
    }

    private void ThinkHoldPosition(CharacterState actor, BrainDefinition brain)
    {
        var target = AcquireHostile(actor, brain);
        if (target is not null && TryCastReadySkillInRange(actor, brain, target))
        {
            StopIfMoving(actor);
            return;
        }

        StopIfMoving(actor);
    }

    private void ThinkChaseAttack(CharacterState actor, BrainDefinition brain)
    {
        var target = AcquireHostile(actor, brain);
        if (target is not null && TryCastReadySkillInRange(actor, brain, target))
        {
            StopIfMoving(actor);
            return;
        }

        if (target is not null && actor.CanMove)
        {
            Chase(actor, target, ClosestReadySkillRange(actor, brain));
            return;
        }

        StopIfMoving(actor);
    }

    private void ThinkFollowOwner(CharacterState actor, BrainDefinition brain)
    {
        if (IsInCombat(actor, brain))
        {
            ThinkChaseAttack(actor, brain);
            return;
        }

        if (actor.OwnerId is not long ownerId ||
            !_characters.TryGetValue(ownerId, out var owner) ||
            !owner.CanAct)
        {
            StopIfMoving(actor);
            return;
        }

        if (!actor.CanMove)
        {
            return;
        }

        var follow = brain.FollowDistance + owner.Template.Radius;
        if (FixVector2.DistanceSquared(actor.Position, owner.Position) > follow * follow)
        {
            Enqueue(new MoveToCommand(CurrentFrame, actor.Id, owner.Position));
            return;
        }

        StopIfMoving(actor);
    }

    private bool IsInCombat(CharacterState actor, BrainDefinition brain)
    {
        if (TryGetCurrentHostile(actor, out _))
        {
            return true;
        }

        if (actor.OwnerId is long ownerId &&
            _characters.TryGetValue(ownerId, out var owner) &&
            TryGetCurrentHostile(owner, out _))
        {
            return true;
        }

        return FindNearestHostile(actor, brain.AggroRadius) is not null;
    }

    private CharacterState? AcquireHostile(CharacterState actor, BrainDefinition brain)
    {
        if (TryGetCurrentHostile(actor, out var current))
        {
            return current;
        }

        var nearest = FindNearestHostile(actor, brain.AggroRadius);
        if (nearest is null)
        {
            if (actor.CurrentTargetId is not null)
            {
                Enqueue(new SetTargetCommand(CurrentFrame, actor.Id, null));
            }

            return null;
        }

        if (actor.CurrentTargetId != nearest.Id)
        {
            Enqueue(new SetTargetCommand(CurrentFrame, actor.Id, nearest.Id));
        }

        return nearest;
    }

    private bool TryCastReadySkillInRange(
        CharacterState actor,
        BrainDefinition brain,
        CharacterState target)
    {
        if (actor.ActiveSkill is not null ||
            actor.HasControl(ControlType.Stun))
        {
            return false;
        }

        foreach (var skill in OffensiveSkills(actor))
        {
            if (!IsSkillReady(actor, skill))
            {
                continue;
            }

            if (!IsInAttackRange(actor, target, SkillRange(skill, brain)))
            {
                continue;
            }

            IssueCast(actor, skill, target);
            return true;
        }

        return false;
    }

    private Fix64 ClosestReadySkillRange(CharacterState actor, BrainDefinition brain)
    {
        foreach (var skill in OffensiveSkills(actor))
        {
            if (IsSkillReady(actor, skill))
            {
                return SkillRange(skill, brain);
            }
        }

        return brain.AttackRange;
    }

    private void Chase(CharacterState actor, CharacterState target, Fix64 range)
    {
        if (IsInAttackRange(actor, target, range))
        {
            StopIfMoving(actor);
            return;
        }

        Enqueue(new MoveToCommand(CurrentFrame, actor.Id, target.Position));
    }

    private void StopIfMoving(CharacterState actor)
    {
        if (actor.Movement.Kind != MovementKind.None)
        {
            Enqueue(new StopMovingCommand(CurrentFrame, actor.Id));
        }
    }

    private void IssueCast(
        CharacterState actor,
        SkillDefinition skill,
        CharacterState target)
    {
        var offset = target.Position - actor.Position;
        Enqueue(new CastSkillCommand(
            CurrentFrame,
            actor.Id,
            skill.Id,
            TargetId: target.Id,
            TargetPoint: target.Position,
            Direction: offset == FixVector2.Zero ? null : offset));
    }

    private bool TryGetCurrentHostile(CharacterState actor, out CharacterState target)
    {
        if (actor.CurrentTargetId is long targetId &&
            _characters.TryGetValue(targetId, out var found) &&
            found.IsSelectable &&
            found.FactionId != actor.FactionId)
        {
            target = found;
            return true;
        }

        target = null!;
        return false;
    }

    private CharacterState? FindNearestHostile(CharacterState actor, Fix64 aggroRadius)
    {
        CharacterState? nearest = null;
        var nearestDistance = Fix64.Zero;
        var reach = aggroRadius;
        var reachSquared = reach * reach;

        foreach (var candidate in _characters.Values)
        {
            if (candidate.Id == actor.Id ||
                candidate.FactionId == actor.FactionId ||
                !candidate.IsSelectable)
            {
                continue;
            }

            var distanceSquared = FixVector2.DistanceSquared(actor.Position, candidate.Position);
            if (distanceSquared > reachSquared)
            {
                continue;
            }

            if (nearest is not null &&
                (distanceSquared > nearestDistance ||
                 (distanceSquared == nearestDistance && candidate.Id > nearest.Id)))
            {
                continue;
            }

            nearest = candidate;
            nearestDistance = distanceSquared;
        }

        return nearest;
    }

    private IEnumerable<SkillDefinition> OffensiveSkills(CharacterState actor)
    {
        foreach (var skillId in actor.Template.SkillIds)
        {
            if (!_config.SkillDefinitions.TryGetValue(skillId, out var skill) ||
                !skill.Effects.Any(IsOffensiveEffect))
            {
                continue;
            }

            yield return skill;
        }
    }

    private static bool IsOffensiveEffect(SkillEffect effect) =>
        effect.Target.Relation is TargetRelation.Enemies or TargetRelation.Any &&
        effect.Target.Kind != SkillTargetKind.Self;

    private bool IsSkillReady(CharacterState actor, SkillDefinition skill)
    {
        if (skill.Category == SkillCategory.Spell &&
            actor.HasControl(ControlType.Silence))
        {
            return false;
        }

        if (skill.Category == SkillCategory.Weapon &&
            actor.HasControl(ControlType.Disarm))
        {
            return false;
        }

        return !actor.CooldownReadyFrames.TryGetValue(skill.Id, out var readyFrame) ||
               CurrentFrame >= readyFrame;
    }

    private static Fix64 SkillRange(SkillDefinition skill, BrainDefinition brain)
    {
        var radius = Fix64.Zero;
        foreach (var effect in skill.Effects.Where(IsOffensiveEffect))
        {
            radius = Fix64.Max(radius, effect.Target.Radius);
        }

        return radius > Fix64.Zero ? radius : brain.AttackRange;
    }

    private static bool IsInAttackRange(
        CharacterState actor,
        CharacterState target,
        Fix64 range)
    {
        var reach = range + target.Template.Radius;
        return FixVector2.DistanceSquared(actor.Position, target.Position) <= reach * reach;
    }
}
