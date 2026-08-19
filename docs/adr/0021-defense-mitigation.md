# Diminishing defense mitigation; no V1 penetration

After crit, Physical and Magical hits convert defense into a permyriad mitigation `min(def * 10000 / (def + K), MaxMit)` (default MaxMit 8000) then scale the hit. K and the cap live in Battle Config; both damage types share them and bind their own defense attribute ids. True skips mitigation. Final damage is at least 1. Subtract-def and linear armor-as-percent were rejected as too easy to trivialise or stack. Penetration is not V1.
