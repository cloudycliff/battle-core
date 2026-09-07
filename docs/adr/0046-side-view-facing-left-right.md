# Side-view aim and facing are left or right

In `SideView`, locomotion, `SetFacing`, cast facing, fans, dashes, and
projectiles consume only the sign of X. Step 0 faces +X and 512 faces -X; other
Facing steps snap to the nearer of those two (up and down, steps 256 and 768,
snap to +X). A purely vertical aim vector keeps the Character's current
left/right facing instead of aiming at the sky.

Knockback, pull, and throw keep the unclamped distance vector so an upward
cast direction can still launch. TopDown facing is unchanged: full-turn
`Facing.FromDirection` and movement does not rewrite facing.

Driving SideView facing from a separate host `SetFacing` on every walk was
rejected so Brain and player locomotion stay consistent. Animation-locked
facing remains out of this core.
