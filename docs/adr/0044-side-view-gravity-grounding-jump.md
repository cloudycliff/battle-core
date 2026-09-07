# Side-view uses explicit gravity settings, grounded state, and jump commands

`SideView` treats X as horizontal and positive Y as up. Its map config must
provide positive gravity, maximum fall speed, and jump speed in spatial units
per second (gravity is units/second²). Each 30 Hz logic frame uses
semi-implicit Euler: vertical velocity first changes by `-gravity / 30`, is
clamped to `-maximumFallSpeed`, then position changes by velocity `/ 30`.
These calculations use `Fix64`.

A Character is grounded when its occupancy shape is touching the map's lower
bound or the top face of a terrain AABB and a one-raw-unit downward probe
would be blocked. Grounding clears downward vertical velocity. Side-view
`MoveDirection` consumes X only; Y is ignored. Gravity still advances while
the Character is stunned, rooted, or in a non-movable skill timeline. A
carried Character follows the carrier and does not integrate gravity until
the carry ends.

Jump is an explicit `JumpCommand`, not upward `MoveDirection`. It is accepted
only for an Alive, grounded, movable Character and sets vertical velocity to
the configured jump speed. Rejected jumps emit `CommandRejectedEvent`.
Grounded and vertical velocity are exposed by Character query snapshots;
movement continues to use `CharacterMovedEvent`, so no separate per-frame
physics event is added.

Side-view initially reuses circular Character occupancy and terrain AABBs.
Slopes, one-way platforms, moving platforms, coyote time, multiple jumps,
wall movement, and animation-locked fighting-game hitboxes are deferred.
Reusing host physics or injecting a downward movement command was rejected
because either would break deterministic authority or mix gravity with
MoveSpeed and control effects.
