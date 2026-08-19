# C# .NET 8 core with zero engine dependencies

The battle core is a C# class library targeting .NET 8 and must not reference Godot, Unity, or other engine APIs. Godot (or a later host) only ticks the simulation and maps events to presentation. Engine-specific C# is not portable between Godot and Unity; a pure logic assembly is. GDScript-as-core was rejected as a poor fit for determinism, config, and tests. Native GDExtension was rejected for V1 to keep iteration and debugging simpler.
