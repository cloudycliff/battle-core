# This repo is the engine-agnostic core only

battle-core ships the C# simulation library and its tests. Godot (or Unity) hosts live in other repositories and consume the core as a project or package reference. A Godot tree inside this repo was rejected for V1 so engine types cannot leak into the logic assembly.
