# Layered shields; expire first, then instanceId

Shield is a per-Buff pool, not a single character field and not the same as HP overheal. Incoming damage drains shields in ascending expiry frame, then ascending instanceId, then HP. Heals do not refill shields. A merged shield bar was rejected because overlapping shield skills would be unordered.
