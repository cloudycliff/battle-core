# Directed casts update facing when the cast starts

When a `CastSkill` with a non-zero direction or target point passes validation and starts its timeline, the core immediately updates the caster's Facing to that aim direction. The facing change occurs with cast acceptance, before later movement and effect frames. If the skill is subsequently cancelled, Facing is not rolled back.

Waiting until the effect frame was rejected because presentation and windup orientation would disagree. Requiring the host to enqueue a separate `SetFacing` was rejected because Brain and player casts would diverge and command ordering would become host policy. Self casts and casts without a usable direction or target point leave Facing unchanged.
