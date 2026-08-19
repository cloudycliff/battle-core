# Implement deterministically; defer hash and replay tools

The simulation remains strictly deterministic (fixed-point space, integer combat, layered PRNG, ordered tick phases). V1 does not ship FrameHash, command-log replay, or snapshot restore. Skipping verification tools is not permission to use platform rand or unordered iteration.
