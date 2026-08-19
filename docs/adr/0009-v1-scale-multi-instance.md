# V1 targets mid-ARPG scale; many Battle instances allowed

V1 is sized for roughly 64 Characters, 256 Projectiles, 64 AreaEffects, and 128 Terrains so naive collision and per-entity events stay acceptable at 30 Hz without survivor-grade spatial hashing and projectile pooling. Multiple Battle instances may exist in one process; each owns its seed and mutable world. Survivor-scale entity counts are explicitly out of V1.
