# A blocked leash return counts as arrival

Amends [ADR 0035](0035-spawn-leash-defer-threat.md). While `ReturningToLeash`
is set, the Brain issues `MoveTo` origin every frame and skips target
acquisition until it is within its own radius of the origin. If a hostile
stands on the origin the mob can never get closer than two radii, so the
return never ended and the mob stayed passive indefinitely — a body parked
on a spawn point turned that mob into a punching bag.

Rule: if the return `MoveTo` is cancelled because the path was blocked
(`SpatialMoveResult.MovementIntentBlocked`, or a zero-length resolved move),
the return is over on the next Brain frame exactly as if the mob had
arrived. `SpawnOrigin` is not moved. If the mob is still outside
`LeashRadius` at that point it re-enters the return next frame; against a
blocker that stays put this alternates one frame of returning with one
frame of fighting, which is acceptable and strictly better than freezing.

Straight-line returns blocked by terrain (a mob that chased around a wall)
now jitter the same way instead of freezing. Pathing around obstacles is
still out of V1 (no A*), so this is documented rather than solved here.
