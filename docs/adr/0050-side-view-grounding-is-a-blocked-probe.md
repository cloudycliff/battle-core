# Side-view grounding is "a downward probe is blocked"

Clarifies [ADR 0044](0044-side-view-gravity-grounding-jump.md), which already
described grounding as a blocked downward probe. The first implementation
instead matched the circle's bottom against terrain top faces within a
two-raw-unit tolerance, using a square footprint. That diverged from the
circular collision the mover actually uses, with two visible consequences:
a circle resting on a platform corner (or on an enemy's head, since blocking
Characters stop vertical movement too) was never grounded, so it hovered
with gravity re-zeroed every frame and could not jump; and a circle whose
centre had walked past the edge still counted as standing.

Grounded is now decided by the same `IsPositionValid` test that movement
uses: the Character is supported when its position moved down by 16 raw
units (about 0.24 mm) would be invalid for it — map floor, any terrain face
or corner, or another movement-blocking Character. Landing bisection leaves
at most a couple of raw units of gap, so the probe is deeper than that and
still invisible. Standing on a blocking Character is therefore grounded;
when that Character walks away the probe clears and the stander falls.
Non-blocking templates only probe terrain and bounds, matching how they
move.
