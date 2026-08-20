using BattleCore.Attributes;
using BattleCore.Combat;
using BattleCore.Simulation.Commands;
using BattleCore.Simulation.Events;

namespace BattleCore.Simulation;

public sealed partial class BattleInstance
{

    private void GrantShield(GrantShieldCommand command, List<BattleEvent> events)
    {
        WithLiving(command.TargetId, command, events, character =>
        {
            if (command.Amount <= 0)
            {
                Reject(command, events, "Shield amount must be positive.");
                return;
            }

            var instanceId = _nextShieldId++;
            var expiry = command.DurationFrames <= 0
                ? long.MaxValue
                : CurrentFrame + command.DurationFrames;
            character.Shields.Add(new ShieldLayer
            {
                InstanceId = instanceId,
                Amount = command.Amount,
                ExpiryFrame = expiry,
            });
            events.Add(new ShieldChangeEvent(
                CurrentFrame,
                character.Id,
                instanceId,
                command.Amount,
                character.ShieldTotal));
        });
    }

    private void Revive(ReviveCommand command, List<BattleEvent> events)
    {
        if (!TryGetLiving(command.SourceId, command, events, out var source))
        {
            return;
        }

        if (!_characters.TryGetValue(command.TargetId, out var target))
        {
            Reject(command, events, $"Character '{command.TargetId}' does not exist.");
            return;
        }

        if (target.LifeState != LifeState.Dead || source.FactionId != target.FactionId)
        {
            Reject(command, events, "Revive requires a dead ally in the corpse window.");
            return;
        }

        StandUp(target, events);
    }
    private void ResolveCombat(List<BattleEvent> events)
    {
        ExpireShields(events);

        foreach (var pending in _pendingCombat
                     .OrderBy(item => item.TargetId)
                     .ThenBy(item => item.Sequence))
        {
            switch (pending.Command)
            {
                case DealDamageCommand damage:
                    ApplyDamage(damage, events);
                    break;
                case HealCommand heal:
                    ApplyHeal(heal, events);
                    break;
            }
        }

        _pendingCombat.Clear();
    }

    private void ApplyDamage(DealDamageCommand command, List<BattleEvent> events)
    {
        if (!TryGetLiving(command.SourceId, command, events, out var source) ||
            !TryGetCharacter(command.TargetId, command, events, out var target))
        {
            return;
        }

        if (!target.Template.Damageable || !target.CanAct)
        {
            Reject(command, events, "Target cannot receive damage.");
            return;
        }

        var hit = ComputeBase(source, command.AttributeId, command.Flat, command.Ratio);
        var wasCrit = false;
        if (command.CanCrit)
        {
            var critRate = System.Math.Clamp(
                source.Attributes.Get(_config.Bindings.CritRate),
                0,
                AttributeSet.RatioScale);
            wasCrit = source.CombatRng.NextInt((int)AttributeSet.RatioScale) < critRate;
            if (wasCrit)
            {
                hit = hit * source.Attributes.Get(_config.Bindings.CritDamage) /
                      AttributeSet.RatioScale;
            }
        }

        if (command.DamageType != DamageType.True)
        {
            var defenseId = command.DamageType == DamageType.Physical
                ? _config.Bindings.PhysicalDefense
                : _config.Bindings.MagicalDefense;
            hit = CombatMath.ApplyMitigation(
                hit,
                target.Attributes.Get(defenseId),
                _config.DefenseK,
                _config.MaximumMitigation);
        }

        hit = CombatMath.ScaleByRate(hit, source.Attributes.Get(_config.Bindings.DamageDealt));
        hit = CombatMath.ScaleByRate(hit, target.Attributes.Get(_config.Bindings.DamageTaken));
        if (hit < 0)
        {
            hit = 0;
        }

        if (hit == 0 && CombatMath.ComputeBase(
                command.Flat,
                ReadAttribute(source, command.AttributeId),
                command.Ratio) > 0)
        {
            hit = _config.MinimumDamage;
        }

        var shieldAbsorbed = AbsorbShield(target, hit, events);
        var hpDamage = hit - shieldAbsorbed;
        if (hpDamage > 0)
        {
            target.CurrentHp -= hpDamage;
        }

        events.Add(new DamageEvent(
            CurrentFrame,
            source.Id,
            target.Id,
            command.DamageType,
            hpDamage,
            shieldAbsorbed,
            wasCrit));

        if (target.CurrentHp <= 0)
        {
            Kill(target, source.Id, events);
        }
    }

    private void ApplyHeal(HealCommand command, List<BattleEvent> events)
    {
        if (!TryGetLiving(command.SourceId, command, events, out var source) ||
            !TryGetCharacter(command.TargetId, command, events, out var target))
        {
            return;
        }

        if (!target.CanAct)
        {
            Reject(command, events, "Target cannot receive healing.");
            return;
        }

        var amount = ComputeBase(source, command.AttributeId, command.Flat, command.Ratio);
        var wasCrit = false;
        if (command.CanCrit)
        {
            var critRate = System.Math.Clamp(
                source.Attributes.Get(_config.Bindings.CritRate),
                0,
                AttributeSet.RatioScale);
            wasCrit = source.CombatRng.NextInt((int)AttributeSet.RatioScale) < critRate;
            if (wasCrit)
            {
                amount = amount * source.Attributes.Get(_config.Bindings.CritDamage) /
                         AttributeSet.RatioScale;
            }
        }

        if (amount <= 0)
        {
            return;
        }

        target.CurrentHp += amount;
        events.Add(new HealEvent(
            CurrentFrame,
            source.Id,
            target.Id,
            amount,
            wasCrit));
    }

    private long ComputeBase(
        CharacterState source,
        string? attributeId,
        long flat,
        long ratio) =>
        CombatMath.ComputeBase(flat, ReadAttribute(source, attributeId), ratio);

    private static long ReadAttribute(CharacterState source, string? attributeId) =>
        attributeId is null ? 0 : source.Attributes.Get(attributeId);

    private void ExpireShields(List<BattleEvent> events)
    {
        foreach (var character in _characters.Values)
        {
            for (var index = character.Shields.Count - 1; index >= 0; index--)
            {
                var layer = character.Shields[index];
                if (layer.ExpiryFrame > CurrentFrame && layer.Amount > 0)
                {
                    continue;
                }

                character.Shields.RemoveAt(index);
                events.Add(new ShieldChangeEvent(
                    CurrentFrame,
                    character.Id,
                    layer.InstanceId,
                    -layer.Amount,
                    character.ShieldTotal));
            }
        }
    }

    private long AbsorbShield(CharacterState target, long hit, List<BattleEvent> events)
    {
        if (hit <= 0 || target.Shields.Count == 0)
        {
            return 0;
        }

        var remaining = hit;
        var absorbed = 0L;
        foreach (var layer in target.Shields
                     .OrderBy(item => item.ExpiryFrame)
                     .ThenBy(item => item.InstanceId)
                     .ToArray())
        {
            if (remaining <= 0)
            {
                break;
            }

            var taken = System.Math.Min(layer.Amount, remaining);
            layer.Amount -= taken;
            remaining -= taken;
            absorbed += taken;
            events.Add(new ShieldChangeEvent(
                CurrentFrame,
                target.Id,
                layer.InstanceId,
                -taken,
                target.ShieldTotal));
        }

        target.Shields.RemoveAll(layer => layer.Amount <= 0);
        return absorbed;
    }

    private void Kill(CharacterState target, long sourceId, List<BattleEvent> events)
    {
        foreach (var buff in target.Buffs.ToArray())
        {
            RemoveBuffInstance(target, buff, events);
        }

        if (target.ActiveSkill is { } cast)
        {
            target.ActiveSkill = null;
            events.Add(new SkillEndedEvent(
                CurrentFrame,
                target.Id,
                cast.Definition.Id));
        }

        target.CurrentHp = 0;
        target.Movement = MovementIntent.None;
        target.CurrentTargetId = null;
        target.Shields.Clear();

        if (target.Lives > 0)
        {
            target.Lives--;
            target.LifeState = LifeState.Reviving;
            target.ReviveAtFrame = CurrentFrame + _config.ReviveDelayFrames;
            target.DespawnAtFrame = null;
            events.Add(new DiedEvent(CurrentFrame, target.Id, sourceId));
            events.Add(new ReviveStartEvent(CurrentFrame, target.Id));
            return;
        }

        target.LifeState = LifeState.Dead;
        target.ReviveAtFrame = null;
        target.DespawnAtFrame = CurrentFrame + _config.CorpseDurationFrames;
        events.Add(new DiedEvent(CurrentFrame, target.Id, sourceId));
        MaybeEmitFactionWiped(target.FactionId, events);
    }

    private void StandUp(CharacterState character, List<BattleEvent> events)
    {
        character.LifeState = LifeState.Alive;
        character.ReviveAtFrame = null;
        character.DespawnAtFrame = null;
        character.CurrentHp = character.Attributes.Get(_config.Bindings.MaxHp);
        events.Add(new RevivedEvent(CurrentFrame, character.Id));
    }

    private void ResolveLife(List<BattleEvent> events)
    {
        foreach (var character in _characters.Values.ToArray())
        {
            if (character.LifeState == LifeState.Reviving &&
                character.ReviveAtFrame is long reviveAt &&
                CurrentFrame >= reviveAt)
            {
                StandUp(character, events);
            }

            if (character.LifeState == LifeState.Dead &&
                character.DespawnAtFrame is long despawnAt &&
                CurrentFrame >= despawnAt)
            {
                _characters.Remove(character.Id);
                events.Add(new CorpseDespawnedEvent(CurrentFrame, character.Id));
                MaybeEmitFactionWiped(character.FactionId, events);
            }
        }

        foreach (var character in _characters.Values)
        {
            if (character.CurrentTargetId is long targetId &&
                (!_characters.TryGetValue(targetId, out var target) || !target.IsSelectable))
            {
                character.CurrentTargetId = null;
                events.Add(new TargetChangedEvent(CurrentFrame, character.Id, null));
            }
        }
    }

    private void MaybeEmitFactionWiped(int factionId, List<BattleEvent> events)
    {
        if (_wipedFactions.Contains(factionId))
        {
            return;
        }

        if (_characters.Values.Any(character =>
                character.FactionId == factionId && character.LifeState != LifeState.Dead))
        {
            return;
        }

        _wipedFactions.Add(factionId);
        events.Add(new FactionWipedEvent(CurrentFrame, factionId));
    }
}
