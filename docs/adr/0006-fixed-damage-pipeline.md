# Fixed damage pipeline, configurable coefficients

Damage uses a fixed integer pipeline (base → crit → type-based mitigation → modifiers → shield → HP) so identical inputs replay identically and designers tune coefficients rather than rewrite math. An expression-string engine was rejected as a V1 source of ambiguity and non-portability. Death marks Dead immediately (stop Brain/skills, invalid as a normal target). Ordinary corpses keep a configurable presentation window before despawn; templates marked `CanBeRevived` instead retain the corpse until revival, as specified by ADR 0012.
