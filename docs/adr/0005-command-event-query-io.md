# Frame-stamped commands, events out, queries read-only

External API is narrow: spawn and enqueue Commands, drive with Tick, consume Battle Events, optionally Battle Query. Commands carry a logic-frame stamp and are applied only on that frame, enabling replay. Presentation is event-driven; full snapshots are not the primary output. Read-only queries cover UI/debug/AI sensing without mutating simulation. Lockstep input boxes can be layered later atop this contract.
