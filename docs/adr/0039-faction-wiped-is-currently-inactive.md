# FactionWiped means no currently active combatants, not final defeat

`FactionWiped` is a battle fact emitted the first time a faction has no Character in `Alive` or `Reviving` state. A `Dead` Character that could later be revived by a skill does not block the event. The host must not interpret `FactionWiped` alone as irreversible defeat; victory and whether to stop ticking remain host policy.

Counting every indefinitely retained `CanBeRevived` corpse as active was rejected because such a faction might never report that it has been cleared. Making the core decide final defeat was also rejected because available revivers, encounter rules, and host-side objectives are outside the battle core.

Each faction emits this event at most once per battle. A later revive or Spawn does not reset the emitted marker, so clearing that faction again does not repeat the event. Repeated transition events were rejected because consumers could mistake them for multiple independent outcome signals; hosts that need current occupancy can use Query.
