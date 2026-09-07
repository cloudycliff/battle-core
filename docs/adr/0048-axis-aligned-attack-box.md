# Axis-aligned attack boxes sit in front of facing

`BoxAtCaster` is a caster-centered AABB swipe. `Radius` is how far the box
extends along facing; `Height` is the full thickness perpendicular to that
axis, centered on the caster. Characters whose occupancy circles intersect
the box are eligible, then the usual relation filter applies.

SideView clamps facing to left or right first, so the box is a vertical
rectangle in front of the character. TopDown uses the same AABB builder on
the dominant facing axis (the four quadrants of the 1024-step facing). The
box uses the caster's Facing at the effect frame; a direction on the cast
is not required.

Replacing circles and fans with this kind was chosen for side-view melee.
Rotated (non-axis-aligned) hitboxes stay out of this core.
