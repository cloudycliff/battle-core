# Carry is a first-class relation; skills only start and end it

Grab-carry-throw spans many frames and couples two Characters' movement, so it is not modeled as a one-shot skill timeline or a passive buff with a carrier id. The core owns a Carry relation (follow offset, move-authority handoff, drop rules). Skills expose configurable effects such as StartCarry, Throw, and Drop. V1 includes the mechanism plus one demo skill; same-faction only, one carried target, drop on carrier control effects.
