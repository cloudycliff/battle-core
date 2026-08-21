# Skill charges and cooldown reduction are deferred

V1 keeps one cooldown timeline per skill, equivalent to a single charge. Multi-charge inventories and a CDR attribute that scales recharge time are postponed so they do not block resource costs and combat triggers. A process-wide GCD is out of V1.

One Character may have only one active skill timeline. From windup start through recovery end, another skill cannot start; V1 has no recovery-cancel flag. This is timeline exclusivity rather than a shared cooldown: once the timeline ends, another ready skill may start immediately. Configurable recovery cancellation was rejected for V1 because it adds another interrupt path and complicates resource and event ordering.
