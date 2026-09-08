# Projectiles snapshot their source; one expiry-frame rule for timed things

Extends [ADR 0042](0042-area-effect-snapshot-and-rng.md) to Projectiles. A
projectile captures the caster's source-side combat contribution at launch
(evaluated bases, `DamageDealt`, CritRate, CritDamage) and derives its own
deterministic RNG from the battle seed and its entity id (stream 5). Hits
use that snapshot; the projectile's faction, not a live caster lookup,
decides relation. It therefore keeps flying and hitting after the caster
dies or the corpse is recycled. The previous "despawn when the source
Character is gone" made loosed arrows vanish 30 frames after the archer
fell, which contradicted the AreaEffect rule and was visible to players.

Frame semantics for timed objects are unified as follows:

- Projectiles and AreaEffects are gone at the start of their expiry frame.
  `LifetimeFrames = N` means exactly N frames of travel and collision; the
  old check ran after movement and gave N + 1.
- A periodic effect (Buff or AreaEffect tick) that falls due on the expiry
  frame fires first, then the owner is removed. `Duration == Interval`
  therefore yields exactly one tick, not zero; the alternative (zero ticks)
  makes the most natural DoT configuration silently do nothing.
- Shields keep expiring at the start of their expiry frame as before.
