# Side-view forced displacement is a horizontal shove plus ballistic vertical impulse

Dash, knockback, pull, and throw keep TopDown's truncated planar path. In SideView the
same configured distance vector is split: X is applied this frame as a blocked
horizontal shove; Y is converted to vertical speed `sign(Y) * sqrt(2 * gravity * |Y|)`
so an upward 2 m component peaks near 2 m instead of teleporting 2 m in one tick.
The first frame also integrates that new vertical velocity. A downward component is
ignored while grounded and only speeds a fall while airborne. Root and carried
characters still skip forced displacement entirely.

Reusing TopDown's single-frame path truncate for height was rejected because it
cannot leave a character airborne. Treating Y as metres per second was rejected
because the skill field is already a distance shared with TopDown.
