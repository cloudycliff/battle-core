# Lives auto-revive plus skill revive of allied corpses

Characters may hold a lives resource. On lethal damage, remaining lives start an untargetable countdown then stand up; zero lives uses Dead plus a corpse window. Skills can revive allied Characters that are Dead inside that window (e.g. player rezzing a pet). General targeting still skips Dead. Countdown auto-revive is not a valid target, so it cannot be double-rezzed. Replacing the entity via a fresh Spawn was rejected because it would break ids, buffs, and carry bindings.
