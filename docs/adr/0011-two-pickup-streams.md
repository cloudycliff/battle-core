# Stash loot is event-only; inventory lives outside

Proximity pickup is simulated in-core so who touches an item is deterministic. Instant pickups apply combat effects immediately. Stash (out-of-run) pickups only emit PickedUp and despawn the world object; the bag, reject rules, and re-drops are external and come back as later Spawns if needed. An in-core stash was rejected so the battle core does not own economy truth.
