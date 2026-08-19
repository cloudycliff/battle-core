# Shared base formula; heals may overheal

Damage and heal effects use `flat + attr * ratio / 10000` with a single attribute id. Heals may crit, skip defense and shields, and add the full amount to CurrentHP even above MaxHP. Multi-attribute bases and clamping heals to MaxHP were rejected for V1.
