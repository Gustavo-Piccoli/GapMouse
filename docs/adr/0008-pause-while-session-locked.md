# 8. Pause while the session is locked

## Context

With Win+L, GapMouse kept moving the mouse over the lock screen curtain, and on the PIN
screen (secure desktop) the cursor calls failed with access denied, producing
`WARN Port Cursor failed` lines in the log. While the session is locked there is no reason to
move the mouse; keeping the PC awake only makes sense in specific cases (a long download, for
example).

## Decision

- **Locking is a flag in the engine, orthogonal to the state.** `KeepAwakeEngine` gains
  `SetSessionLocked(bool, now)` (idempotent) and `AdvanceLocked(now)`, which needs only the
  time: the host reads neither cursor nor monitor while locked. Waiting, Moving and Paused
  still exist; unlocking returns to the previous state, and a manual pause is never undone.
- **Power while locked.** By default the power request is released (`PowerRequest.None`) and
  Windows follows its normal power plan. With `EngineSettings.KeepAwakeWhileLocked` (JSON
  `keepAwakeWhileLocked`, default `false`) the engine asks for `KeepSystemAwake`, never the
  display. While paused, power stays `None`.
- **Normal event flow.** `SessionLocked(At, KeepAwake)` and `SessionUnlocked(At)` become the
  `LOCKED` and `UNLOCKED` log lines (`LogKind.Locked/Unlocked`). `KeepAwake` is true only when
  not paused and the option is on.
- **Notification in the shell.** `ShellWindow` subscribes to WTS session notifications and
  exposes `SessionLockChanged` and `QueryIsSessionLocked()` (initial state); the app forwards
  them to `IEngineController.SetSessionLocked`, which is queued to the engine thread.
- **Logging.** Port recovery is now logged at Warning (the pair of the failure) and
  `ActivityLogger` no longer repeats the exception message.

## Alternatives

- **B. Lock handling in `EngineHost`.** Fewer engine changes, but the rule lives in
  imperative code, is harder to test, and statistics and events fall outside the normal flow.
- **C. Reuse Pause/Resume.** Rejected: unlocking would undo the user's manual pause.

## Consequences

- No cursor WARN lines are expected while locked once the lock has been processed (one failure/recovery pair can still appear at the lock edge).
- A timed pause can expire while locked, without moving the mouse until unlock.
- With the option on, only the system stays awake; the display can turn off.
- The DevConsole does not detect locking (it has no message window).
- Old settings files without the new field fall back to the default.
