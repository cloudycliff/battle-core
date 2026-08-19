# Layered deterministic RNG streams

Randomness (crit, hit rolls, Brain choices, spawns) uses a deterministic PRNG only. CreateBattle(seed) derives three layers: one battle stream, one combat stream per Character, one AI stream per Character. A single global cursor was rejected because adding or reordering entities reshuffles unrelated rolls. Stateless hashing was rejected as V1 primary RNG because multi-roll-per-frame still needs a cursor and is harder to test in isolation.
