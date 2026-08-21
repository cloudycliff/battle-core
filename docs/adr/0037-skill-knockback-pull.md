# Skill knockback and pull are in-core truncated displacements

V1 adds `SkillKnockbackEffect` and `SkillPullEffect` on the skill timeline. Knockback moves each resolved target away from the caster (or along cast direction when configured) by a positive Fix64 distance; pull moves them toward the caster, stopping at overlap rather than tunneling into the caster circle. Both reuse `FindMovementFraction` so terrain AABBs and hostile blockers shorten the path with no wall-slide. Rooted or carried targets do not move.

If the displaced target is itself a carrier, its carried Character follows in the same frame, preserves `CarryOffset`, and skips a separate collision test until the relation ends. Presentation-only knockback was rejected because range, leash, carry, and blocking would desync from the sim. Airborne Z and trigger-dealt knockback stay out of V1.
