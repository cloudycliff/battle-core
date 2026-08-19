# Fixed damage pipeline, configurable coefficients

Damage uses a fixed integer pipeline (base → crit → type-based mitigation → modifiers → shield → HP) so identical inputs replay identically and designers tune coefficients rather than rewrite math. An expression-string engine was rejected as a V1 source of ambiguity and non-portability. Death marks Dead immediately (stop Brain/skills, invalid as a target) and keeps a configurable corpse window before despawn, so presentation can play death without a separate Dying state machine.
