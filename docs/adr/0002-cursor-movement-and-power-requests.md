# 2. Cursor movement and power requests

## Context

The original script keeps the PC active by moving the mouse when the user is idle.
It relies on a fact verified on 2026-10-31: `SetCursorPos` does not reset the Windows
idle counter (idle went from 28.2 s to 28.5 s after moving the cursor), so synthetic
movement can be told apart from real use. Teams still reports the user as Available
with this behavior, and that must be preserved. Because synthetic movement does not
prevent sleep, a separate power request is needed.

## Decision

- Move the cursor with `SetCursorPos` along a quadratic Bezier curve, as in the script.
- Read idle time with `GetLastInputInfo`. Real use is detected when idle drops below
  the elapsed time since the movement started (minus 0.5 s), or below 1 s while waiting.
- Prevent sleep with `SetThreadExecutionState`: `ES_CONTINUOUS | ES_SYSTEM_REQUIRED`
  plus `ES_DISPLAY_REQUIRED` when "keep screen on" is enabled. It is released on pause
  and on exit.
- Keep these behind ports (`ICursorController` and a return detector) so the
  implementation can be swapped.

## Consequences

- Known technical debt: if Teams (or another presence app) stops recognizing
  `SetCursorPos`, the alternative is to simulate input with `SendInput` (which resets
  idle) and detect the user with `WH_MOUSE_LL` / `WH_KEYBOARD_LL`, ignoring events
  flagged `LLMHF_INJECTED` / `LLKHF_INJECTED`.
- Thanks to the ports, that change is a new implementation, not a rewrite.
