# Carry is a first-class relation; skills only start and end it

Grab-carry-throw spans many frames and couples two Characters' movement, so it is not modeled as a one-shot skill timeline or a passive buff with a carrier id. The core owns a Carry relation (follow offset, move-authority handoff, drop rules). Skills expose configurable effects such as StartCarry, Throw, and Drop. V1 includes the mechanism plus one demo skill; same-faction only and one carried target per carrier.

The relation ends when the carrier is stunned or either Character dies. Root only prevents carrier movement; Silence and Disarm do not break an existing physical carry. Dropping on every control effect was rejected because spell/weapon lockouts are unrelated to whether the carrier can keep holding a target.

When a dash, knockback, pull, or other skill-stage displacement moves the carrier, the carried Character follows in the same frame and preserves `CarryOffset`. The carried Character does not run a separate collision test while carried; the relation validates or relocates it to a legal position when dropped. Delaying follow until the next movement phase was rejected because the relation must not expose a one-frame split.
