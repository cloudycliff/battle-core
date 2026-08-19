# Map is its own config: bounds, blockers, spawn points

Play-space is a rectangle from a Map Config, separate from character/skill tables so the same combat catalog can run on different maps. The map also carries terrain AABBs and spawn-point markers. Units cannot leave the bounds; projectiles despawn when they exit. An infinite plane was rejected because projectiles would leak.
