# Per-hit crit is a CritDamage multiply

Each damage instance rolls independently against CritRate (permyriad) on the attacker combat RNG. A success multiplies the current hit by CritDamage / 10000 (e.g. 15000 = 1.5x). Skills may disable crit. One roll per skill cast and crit-as-alternate-skill-coeffs were rejected so multi-projectile and AoE stay simple and replayable.
