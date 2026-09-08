# Side-view targeting reach is horizontal within a vertical band

Amends [ADR 0047](0047-side-view-gameplay-range-is-horizontal.md). Cast
range and Brain aggro / attack range in `SideView` still compare X, but a
target also has to be within `SideViewSettings.VerticalReach` (|ΔY|,
default 2 m, JSON `verticalReach`, must be non-negative). Leash distance and
owner-follow distance remain pure X: a mob that fell off its platform must
still be able to return, and a pet only needs to line up under its owner.

The pure-X rule had a hole: `ExplicitTarget` / `CurrentTarget` effects have
no hit shape, so range was their only geometry, and with Y ignored a
`range: 1` melee strike landed on a target several floors up through solid
terrain. Brains suffered the mirror image: standing under a platform they
saw the target "in range", stopped, and cast a `BoxAtCaster` swipe every
cooldown without ever hitting.

The check lives on `ISpatialBackend.IsWithinReach`; `RangeDistanceSquared`
keeps the X metric for leash, follow, and nearest-target ordering. TopDown
is unchanged (Euclidean 2D for both). Hit shapes stay 2D as before.
