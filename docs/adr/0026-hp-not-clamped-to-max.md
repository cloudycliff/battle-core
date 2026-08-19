# CurrentHP is not clamped when MaxHP changes

Overheal and MaxHP modifiers share one rule: CurrentHP is never auto-clamped to MaxHP. A MaxHP buff falling off does not chop health. Full-health checks use CurrentHP >= MaxHP. Separate overheal resources and clamp-on-max-change were rejected as inconsistent with allowing CurrentHP above MaxHP.
