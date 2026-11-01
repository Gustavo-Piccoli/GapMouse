---
version: 1
slug: "src-gapmouse-app-views-homeview-axaml"
primary_target: "src/GapMouse.App/Views/HomeView.axaml"
related_targets: ["src/GapMouse.App/Views/MainWindow.axaml","src/GapMouse.App/Views/LogsView.axaml","src/GapMouse.App/Views/SettingsView.axaml","src/GapMouse.App/Views/AboutView.axaml","src/GapMouse.App/Views/CustomPauseWindow.axaml"]
---

# Surface brief: GapMouse main window (Home, Logs, Settings, About, custom pause)

Mode: Operate. Platform: Windows desktop (Avalonia 12, Fluent controls, Native AOT). Build path: code-led.

Audience and job: someone who opens the window from the tray for a few seconds to confirm GapMouse is doing its
job, pause it, read what happened, or change a setting, then closes it back to the tray.

Unattended run: the owner authorized a fully automatic run, so the dealt direction (candidate 4) was built without
a decision round. Alternate kept on file: Metro typographic tiles (competitive on audience identification only).

## Direction contract

THESIS: GapMouse is the door hanger on your desk: one face, one word, readable from across the room, turned over
when the state changes. Refuses the category default of a grey Fluent tab strip with a status dot and a form.

OWN-WORLD: Hotel door-hanger card stock. Committed state fields own the Home face: cobalt (waiting), green
(moving), amber (paused), each with its own ink. Tinted charcoal ground, a darker card-stock navigation rail, one
corner radius and one hairline weight everywhere. Segoe UI Variable; rank by weight and case, tight scale.

STORY: The visitor sees the state word and its colour first, the idle ring filling toward the next movement,
then today's totals and recent times; pauses or resumes with the primary button; reads the log as stamped rows.

FIRST VIEWPORT: Left rail (Home, Logs, Settings, About; icon plus label), content on the tinted ground. Home: the
hanger face on the left, full-height state-coloured card with the knob-hole ring (idle progress) at its top, state
word in bold caps, idle and pause text below; right column: Today (active time, activations) and recent times as
aligned rows with tabular numerals; action bar at the bottom with the primary Pause/Resume and a quiet Exit.

FORM: Door hanger (candidate 4 of 7; seed:d8eec48099a74886). Signature move: the hanger face turns over (a short
horizontal flip) when the state changes, and its knob hole is the idle-progress ring.
Raises: from the timetable rack, rank by weight and case inside one tight scale; from the split-flap board, live
values sit in fixed cells (tabular numerals) so nothing jitters; from the ticket wallet, log tags are stamped
fixed-width fields; from the convention catalog, the log keeps its density; from the transit diagram, one geometry
(one radius, one stroke) across every surface.

FINISH: unreviewed and undocumented is unfinished; this build ends with the finish review, the verdict, DESIGN.md, and every shipping raster carrying its provenance
