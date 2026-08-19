# Isolated instances may Tick in parallel; one instance is serial

Battle instances share no mutable globals, so different instances may Tick concurrently. All APIs on a single instance (enqueue, Tick, query) must be called serially. Intra-Tick parallel systems were rejected because they threaten determinism. A process-wide singleton world was rejected as incompatible with multi-instance tests and rooms.
