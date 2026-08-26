using BattleCore.Buffs;
using BattleCore.Combat;
using BattleCore.Simulation.Commands;
using BattleCore.Simulation.Events;

namespace BattleCore.Simulation;

public sealed partial class BattleInstance
{
    private void FireCombatTriggers(
        CombatTriggerKind kind,
        CharacterState? owner,
        CharacterState? other,
        bool hitLanded,
        bool wasCrit,
        DamageType? damageType,
        List<BattleEvent> events)
    {
        if (_firingCombatTriggers || owner is null || !owner.CanAct)
        {
            return;
        }

        _firingCombatTriggers = true;
        try
        {
            foreach (var instance in owner.Buffs.OrderBy(buff => buff.InstanceId).ToArray())
            {
                if (!owner.Buffs.Contains(instance))
                {
                    continue;
                }

                for (var effectIndex = 0; effectIndex < instance.Definition.Effects.Count; effectIndex++)
                {
                    if (instance.Definition.Effects[effectIndex] is not CombatTriggerBuffEffect trigger ||
                        trigger.Kind != kind)
                    {
                        continue;
                    }

                    if (trigger.RequireHitLanded && !hitLanded)
                    {
                        continue;
                    }

                    if (trigger.RequireCrit && !wasCrit)
                    {
                        continue;
                    }

                    if (trigger.DamageType is { } requiredType &&
                        (damageType is null || damageType != requiredType))
                    {
                        continue;
                    }

                    if (instance.NextPeriodicFrames.TryGetValue(effectIndex, out var readyFrame) &&
                        CurrentFrame < readyFrame)
                    {
                        continue;
                    }

                    foreach (var action in trigger.Actions)
                    {
                        ExecuteTriggerAction(owner, other, action, events);
                    }

                    if (trigger.CooldownFrames > 0)
                    {
                        instance.NextPeriodicFrames[effectIndex] =
                            CurrentFrame + trigger.CooldownFrames;
                    }
                }
            }
        }
        finally
        {
            _firingCombatTriggers = false;
        }
    }

    private void ExecuteTriggerAction(
        CharacterState owner,
        CharacterState? other,
        CombatTriggerAction action,
        List<BattleEvent> events)
    {
        var target = action.Target == CombatTriggerTarget.Owner ? owner : other;
        if (target is null || !target.CanAct)
        {
            return;
        }

        switch (action)
        {
            case TriggerHealAction heal:
                ApplyHeal(
                    new HealCommand(
                        CurrentFrame,
                        owner.Id,
                        target.Id,
                        heal.Flat,
                        heal.AttributeId,
                        heal.Ratio,
                        heal.CanCrit),
                    events);
                break;
            case TriggerGrantResourceAction resource:
                ChangeResource(target, resource.ResourceId, resource.Amount, events, command: null);
                break;
            case TriggerApplyBuffAction buff:
                ApplyBuff(
                    new ApplyBuffCommand(
                        CurrentFrame,
                        owner.Id,
                        target.Id,
                        buff.BuffId),
                    events);
                break;
            case TriggerGrantShieldAction shield:
                GrantShield(
                    new GrantShieldCommand(
                        CurrentFrame,
                        target.Id,
                        shield.Amount,
                        shield.DurationFrames),
                    events);
                break;
        }
    }
}
