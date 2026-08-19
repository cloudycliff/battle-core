# Pipeline slots bind to declared attribute ids

The damage and movement systems read named slots (MaxHP, defenses, crit, damage amps, MoveSpeed). Battle Config maps those slots to whatever attribute ids the catalog declared. Sample names like ATK are optional. Hardcoding C# attribute enums was rejected; so was letting each skill pick a defense stat for Physical hits. CurrentHP, Shield, and Lives stay resources, not attributes.
