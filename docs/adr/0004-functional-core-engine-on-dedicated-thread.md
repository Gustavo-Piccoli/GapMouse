# 4. Functional core engine on a dedicated thread

## Context

The keep-awake logic (when to wait, when to move, how to pause, how to react to the
user returning) is the part most likely to contain subtle bugs, and it depends on time,
idle input, cursor position and OS power state. Testing it against the real world is
slow and non-deterministic. Separately, `SetThreadExecutionState` (see ADR 0002) is
per-thread: the request is tied to the thread that made it, so applying and releasing
it from different threads would leak or drop the request.

## Decision

Split the engine into a pure core and a thin imperative shell.

- `KeepAwakeEngine` (Domain) is a synchronous state machine: input in
  (`now`, idle, cursor, monitor), `EngineStep` plus events out. It performs no I/O,
  reads no clock and starts no threads.
- `EngineHost` (Application) owns exactly one background thread. On it, the host
  creates the engine, reads the ports, calls `Advance`, applies the power request,
  moves the cursor, raises events and publishes a snapshot. Commands (pause, resume,
  settings) are queued and wake the loop, so they apply within one iteration.
- Every `IPowerRequestService.Apply` call, including the final release on shutdown,
  runs on that thread. Port failures are logged once per streak and never stop the loop.

## Consequences

- The core is tested deterministically with a fake clock and scripted inputs, with no
  sleeps or threads; the host needs only a few thread-level tests.
- Power requests stay valid for the engine's lifetime and are released reliably.
- The UI reads the latest immutable snapshot and receives events on the engine thread,
  so it must marshal to its own thread.
- One extra long-lived thread, which is cheap for a loop that mostly sleeps.
