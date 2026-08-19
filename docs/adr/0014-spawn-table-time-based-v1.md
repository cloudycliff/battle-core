# Configurable spawn table in-core; V1 is time-based only

Spawning is a core system with its own table, referenced by map spawn points. Triggers are pluggable: V1 implements spawn-at-logic-frame (from battle start). Wave-cleared and enter-region triggers are reserved extension points, not V1. External Spawn remains for the player, skill summons, and pickups. Level scripting does not live inside skill timelines.
