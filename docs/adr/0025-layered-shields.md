# Independent layered shields; expire first, then instanceId

Shield is an independent `ShieldLayer` attached to a Character, not an amount owned by a Buff instance and not the same as HP overheal. Skills, commands, pickups, or Buff actions may create a layer; removing or dispelling the source Buff does not remove an already-created layer. Each layer follows its own expiry frame.

Incoming damage drains shields in ascending expiry frame, then ascending instanceId, then HP. Heals do not refill shields. Buff-owned shield amounts were rejected because dispel semantics would couple two otherwise separate lifecycles; a merged shield bar was rejected because overlapping shield grants would be unordered.
