# Named modifier sources for external gear

Hosts apply and remove grouped attribute modifiers through `ApplyModifierSource` / `RemoveModifierSource`, with the same groups injectable on `SpawnCharacter`. Re-applying a source id replaces the previous group, so weapon slots can swap without the core knowing items or inventory. Anonymous pickup modifiers stay separate. Putting gear math inside templates or treating equipment as duration-less buffs was rejected because unload would not be a clean grouped remove.
