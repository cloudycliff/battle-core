# Projectiles collide with terrain by default; ground-targeted skills do not use LoS

Projectile definitions include `CollidesWithTerrain`, default true. A projectile circle that intersects a terrain AABB despawns without applying hit effects. Projectiles that should pass through walls set the flag false. Map-bounds despawn is unchanged. Instant skills that aim a point or caster-centered area still only require the point inside the map; they do not raycast against walls. Dash already uses movement blocking. Full line-of-sight traces were rejected as V1 geometry scope; walls stay AABBs.
