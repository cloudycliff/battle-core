# Battle Core

Engine-agnostic deterministic 2D combat simulation for .NET 8.

The first implementation slice includes:

- Q16.16 fixed-point values, vectors, angles, circles, and AABBs
- validated attribute bindings and character/map configuration
- ordered frame-stamped commands and incremental battle events
- character spawn, query, facing, soft targets, directional movement, and `MoveTo`
- map, terrain, and enemy blocking rules
- deterministic attribute modifier ordering
- damage, heal, layered shields, death, lives auto-revive, and ally corpse revive
- configurable skill timelines with cast duration, key-frame effects, cooldowns, and single/circle/fan/ring targeting
- linear piercing projectiles and interval area effects spawned from skills
- configurable Buff stacking, attribute modifiers, periodic effects, control states, immunity, replacement, and dispel
- built-in Brains (`Idle`, `ChaseAttack`, `HoldPosition`, `FollowOwner`) that emit the same commands as players
- time-based spawn tables referenced by map spawn points; player markers are query-only

Run the test suite:

```powershell
dotnet test BattleCore.slnx --configuration Release
```

Design:

- [V1 requirements](docs/v1-requirements.md)
- [Domain glossary](CONTEXT.md)
- [Architecture decisions](docs/adr/)
