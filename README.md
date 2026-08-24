# Battle Core

Engine-agnostic **.NET 8** combat sim for 2D real-time ARPG. Logic only: no Godot, no Unity, no rendering, no netcode.

The host owns the clock. Create a `BattleInstance`, enqueue frame-stamped commands, call `Tick()` (30 Hz, one logic frame each), and consume events.

**Host / 外围 agent：** [交接说明](docs/host-handoff.md)（只读本仓、不要改核）。**怎么 Tick：** [使用手册](docs/user-guide.md)。**进度：** [实现状态](docs/status.md)。

```powershell
dotnet test BattleCore.slnx --configuration Release
```

Add a project reference to `src/BattleCore/BattleCore.csproj` (`net8.0`).

## Status

Usable for a host: movement, skills, damage pipeline, resource costs, buffs, linear projectiles, area effects, four built-in Brains, skill summons, carry/throw, pickups, corpse revive skills, dash, named modifier sources, time-based spawn tables.

Not in core yet (host must work around or wait): JSON loaders, auto-facing on aimed casts, cast range checks, projectile-vs-terrain, knockback/pull, spawn leash, combat triggers. See [docs/status.md](docs/status.md).

## Design (frozen)

- [V1 requirements](docs/v1-requirements.md)
- [Domain glossary](CONTEXT.md)
- [Architecture decisions](docs/adr/)
