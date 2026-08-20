# Skill charges and cooldown reduction are deferred

V1 keeps one cooldown timeline per skill, equivalent to a single charge. Multi-charge inventories and a CDR attribute that scales recharge time are postponed so they do not block resource costs and combat triggers. A process-wide GCD is out of V1; overlapping casts are already prevented by a single active timeline.
