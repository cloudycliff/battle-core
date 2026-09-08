# Combat triggers live on Buffs, one-level, after the causing pipeline

Passive abilities are not a second Skill type. They are Buff effects that subscribe to a small set of combat facts. V1 events: deal damage, take damage, kill, and skill start. Filters (hit landed, crit, damage type) and an internal cooldown live on the effect. Actions are the existing grant/heal/buff/resource verbs, never a nested skill timeline and never a nested trigger. Damage applies first and emits `DamageEvent`; then owner and victim triggers run; lethal check and `OnKill` happen after those triggers so a same-frame heal can save the target. A dedicated PassiveSkill definition, aura ticking, HP-threshold polling, and host-side next-frame procs were rejected: the first splits the config model, the rest are later or non-deterministic relative to the combat pipeline.

A self-kill (source and victim are the same Character) does not fire that
Character's Kill triggers: death has already purged its normal Buffs and it
can no longer act when the Kill hook runs. This is the intended rule, not a
gap; on-kill rewards for killing yourself have no design use.

On-gain and on-loss buff triggers are deferred past V1. Most of those designs are already covered by skill timelines or damage triggers; unrestricted gain/loss would fire on Refresh, replace, dispel, expiry, and the death purge, and gain-applying-buff chains easily. If added later: match BuffId or Tag only (no wildcard), fire on-gain only for a new instance (not Refresh), omit death-purge from on-loss by default, and keep the one-level no-nest rule. Own-death is not modeled as "lost every buff"; add a dying-self event if that fact is needed.
