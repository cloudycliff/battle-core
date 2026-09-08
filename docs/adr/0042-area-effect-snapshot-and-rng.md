# AreaEffects snapshot the source and own their RNG

> Extended by [ADR 0052](0052-projectiles-snapshot-source-and-expiry-frame-rule.md):
> Projectiles follow the same snapshot rule, and a tick due on the expiry
> frame fires before the area is removed.

An AreaEffect captures the caster's source-side combat contribution at spawn: evaluated bases for its hit effects, `DamageDealt`, CritRate, and CritDamage. Later ticks use that snapshot. Target defense, `DamageTaken`, shields, and HP stay live. The area persists if the caster dies or despawns.

Followed targets: the area despawns when the follow target is `Dead` or missing. `Reviving` still follows.

Each AreaEffect derives its own deterministic RNG from the battle seed and its entity id. Buffs it applies derive a further stream from the buff instance id. Character combat streams stay reserved for that Character's own rolls. Reusing the caster RNG after despawn was rejected because it would pin a ghost Character state and couple unrelated crit sequences.
