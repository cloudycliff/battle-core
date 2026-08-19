# Battle Core

Engine-agnostic deterministic 2D combat simulation for .NET 8.

The first implementation slice includes:

- Q16.16 fixed-point values, vectors, angles, circles, and AABBs
- validated attribute bindings and character/map configuration
- ordered frame-stamped commands and incremental battle events
- character spawn, query, facing, soft targets, directional movement, and `MoveTo`
- map, terrain, and enemy blocking rules
- deterministic attribute modifier ordering

Run the test suite:

```powershell
dotnet test BattleCore.slnx --configuration Release
```

Design:

- [V1 requirements](docs/v1-requirements.md)
- [Domain glossary](CONTEXT.md)
- [Architecture decisions](docs/adr/)
