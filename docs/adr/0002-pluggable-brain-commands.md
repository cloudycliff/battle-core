# Pluggable Brain produces Commands inside the tick

Any Character (player, monster, summon) may be driven by a Brain. Brains run inside the battle tick and emit the same Command stream as external player input, so determinism and validation stay in one pipeline. V1 ships a few hardcoded built-in behavior modes with configurable parameters; complex behavior trees can be swapped in later via the same interface. Fully external async AI was rejected because out-of-band decisions easily break deterministic replay.
