# Data-driven attributes with a fixed modifier order

Attributes are declared in config (string IDs, compiled to ints at load). Runtime values are base plus a Modifier stack from buffs, gear, and passives. Resolution order is fixed—e.g. `(base + Σflat) * Π(1+pct) + Σfinal`—so identical stacks always yield identical ints. A hardcoded attribute enum was rejected as incompatible with full configurability; unordered float-style mul/add was rejected as non-deterministic to tune and reason about.
