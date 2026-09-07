# Shared combat kernel; split spatial backends

Top-down ARPG and 2D side-view action share the same deterministic combat kernel (tick, commands/events, attributes, damage pipeline, buffs, skill timelines, RNG, config). They do not share one movement/collision implementation. Y is a planar axis in V1 top-down space and height in side-view space; folding both into `BattleInstance` with mode flags, or faking gravity via host `MoveDirection`, was rejected.

A second combat framework was rejected because it would duplicate the expensive correctness surface (determinism, snapshots, death cleanup, modifier order) while the actual divergence is spatial. Fighting-game frame data, animation-locked hitboxes, and variable-dt physics remain out of this core ([CONTEXT](../../CONTEXT.md) Avoid: 格斗帧对战). Side-view is post-V1: extract a space service with the current top-down behavior unchanged, then add `SideViewSpace`. One battle instance uses one spatial mode; do not mix.

Working notes for the extraction: [space-modes.md](../space-modes.md).
