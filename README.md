# Battle Core

Engine-agnostic **.NET 8** combat sim for 2D real-time ARPG. Logic only: no Godot, no Unity, no rendering, no netcode.

The host owns the clock. Create a `BattleInstance`, enqueue frame-stamped commands, call `Tick()` (30 Hz, one logic frame each), and consume events.

**Start here:** [使用手册](docs/user-guide.md) (host loop, config, commands, what is / is not implemented).

```powershell
dotnet test BattleCore.slnx --configuration Release
```

Add a project reference to `src/BattleCore/BattleCore.csproj` (`net8.0`).

## Status

Usable for a host: movement, skills, damage pipeline, buffs, linear projectiles, area effects, four built-in Brains, skill summons, carry/throw, pickups, corpse revive skills, time-based spawn tables.

Not done yet: dash, JSON loaders. See the guide for the full split, including V1 non-goals.

## Design (frozen)

- [V1 requirements](docs/v1-requirements.md)
- [Domain glossary](CONTEXT.md)
- [Architecture decisions](docs/adr/)
