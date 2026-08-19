# Dealt and taken are separate permyriad multiplies after armor

After mitigation, a damage hit is scaled by the attacker's DamageDealt then the victim's DamageTaken (config-declared attribute ids, may be negative). Heals skip this step. Adding the two rates into one multiply was rejected so a +20% deal and +10% vulnerability stay 1.2×1.1. Mapping the hit through the generic attribute modifier stack was rejected as opaque.
