# Side-view gameplay range is horizontal

In `SideView`, gameplay range uses horizontal separation only: cast range,
Brain aggro and attack range, leash distance, and owner-follow distance compare
`abs(from.X - to.X)`. Vertical separation does not make a target farther away
in the single-floor push model.

`TopDown` keeps Euclidean 2D range. Occupancy, terrain, projectile collision,
and skill hit geometry such as circles, rings, and fans remain 2D in both
modes; this decision changes range gates and Brain decisions, not collision or
hit shapes.

The metric lives on `ISpatialBackend` so Skills and Brains do not branch on
`SpatialMode`. This also prevents a SideView Brain from reaching the target's
X coordinate but continuing to chase forever because the target is airborne.
