# Fixed tick phases and entityId order; drop late commands

Each Tick runs a fixed phase sequence (commands → Brain → movement → skill timelines → projectiles/area effects → buffs → damage → death/despawn → events). Within a phase, entities are processed in ascending entityId. Brain sees the previous frame's world so AI and players share the same Command pipeline. Late Commands (stamp < current frame) are dropped, never retroactively applied—the core is not a netcode rollback engine.
