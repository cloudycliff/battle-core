# Spatial fixed-point + combat integers

Strict determinism across platforms makes IEEE floats unsafe for spatial logic. We use fixed-point for positions, velocities, distances, and angles, and plain integers for combat values (HP, damage, armor, cooldown frames, buff stacks). Rejected full fixed-point (overkill for attribute formulas) and controlled floats (cross-platform risk too high for our determinism bar).
