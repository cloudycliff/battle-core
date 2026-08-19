# V1 ships four built-in Brains

Idle, ChaseAttack, HoldPosition, and FollowOwner run inside the tick and emit normal Commands. ChaseAttack picks the nearest valid hostile when untargeted and fires the highest-priority ready skill in range. FollowOwner stays with the summoner and switches to ChaseAttack in combat. Kiting/flee and editable behavior graphs are out of V1.
