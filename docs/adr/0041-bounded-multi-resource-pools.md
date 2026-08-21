# Bounded multi-resource pools; spend at cast start

Battle Config declares resource ids and per-id maxima. Character templates supply initial values (missing ids start at 0). Skills may list one or more costs; `CastSkill` spends them atomically when the timeline starts. If any cost cannot be paid, the cast is rejected: no spend, no cooldown, no timeline. Stun cancel does not refund.

Current values clamp to `[0, maximum]`. Death preserves remaining amounts; revival does not refill. Regeneration is ordinary grants (pickup, command, periodic Buff), not a hidden regen ticker.

A single unnamed resource and unbounded integers were rejected: catalogs already mix mana-like and charge-like pools, and an implicit cap would hide overflow. Spending on effect frames was rejected because interrupt timing would make refunds mandatory.
