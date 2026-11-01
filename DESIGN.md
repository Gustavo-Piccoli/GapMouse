---
name: GapMouse
description: A door hanger on your desk for a keep-awake tray app; one face, one word, readable from across the room.
colors:
  cobalt-waiting: "#2F5FD0"
  hanger-green-moving: "#157545"
  amber-paused: "#E3A21A"
  ink-on-cobalt: "#FFFFFF"
  ink-on-cobalt-secondary: "#E6EDFF"
  ink-on-green: "#FFFFFF"
  ink-on-green-secondary: "#D3F5E2"
  ink-on-amber: "#1F1503"
  ink-on-amber-secondary: "#4A3407"
  accent-dark: "#2F6FED"
  accent-text-dark: "#8FB0FF"
  accent-light: "#2F5FD0"
  accent-text-light: "#2350B8"
  ground-dark: "#14161B"
  rail-dark: "#0F1115"
  surface-dark: "#1B1E25"
  surface-hover-dark: "#232731"
  hairline-dark: "#2C313C"
  text-dark: "#ECEEF2"
  text-muted-dark: "#A0A8B6"
  critical-text-dark: "#F87171"
  warning-text-dark: "#FBBF24"
  ground-light: "#F4F5F7"
  rail-light: "#E6E9F0"
  surface-light: "#FFFFFF"
  surface-hover-light: "#EEF0F4"
  hairline-light: "#D8DCE3"
  text-light: "#16181D"
  text-muted-light: "#576070"
  critical-text-light: "#B42318"
  warning-text-light: "#9A5B00"
typography:
  state-word:
    fontFamily: "Segoe UI Variable Display, Segoe UI, sans-serif"
    fontSize: "30px"
    fontWeight: 700
    letterSpacing: "0.6px"
  title:
    fontFamily: "Segoe UI Variable Text, Segoe UI, sans-serif"
    fontSize: "20px"
    fontWeight: 600
  group:
    fontFamily: "Segoe UI Variable Text, Segoe UI, sans-serif"
    fontSize: "16px"
    fontWeight: 600
  body:
    fontFamily: "Segoe UI Variable Text, Segoe UI, sans-serif"
    fontSize: "14px"
    fontWeight: 400
  caption:
    fontFamily: "Segoe UI Variable Text, Segoe UI, sans-serif"
    fontSize: "12px"
    fontWeight: 400
  log-tag:
    fontFamily: "Segoe UI Variable Text, Segoe UI, sans-serif"
    fontSize: "11px"
    fontWeight: 600
  log-time:
    fontFamily: "Cascadia Mono, Consolas, monospace"
    fontSize: "14px"
    fontWeight: 400
    fontFeature: "tnum"
rounded:
  control: "6px"
  card: "12px"
spacing:
  xs: "4px"
  sm: "8px"
  md: "16px"
  lg: "24px"
components:
  hanger-face-waiting:
    backgroundColor: "{colors.cobalt-waiting}"
    textColor: "{colors.ink-on-cobalt}"
    rounded: "{rounded.card}"
    padding: "16px"
    width: "220px"
  hanger-face-moving:
    backgroundColor: "{colors.hanger-green-moving}"
    textColor: "{colors.ink-on-green}"
    rounded: "{rounded.card}"
    padding: "16px"
    width: "220px"
  hanger-face-paused:
    backgroundColor: "{colors.amber-paused}"
    textColor: "{colors.ink-on-amber}"
    rounded: "{rounded.card}"
    padding: "16px"
    width: "220px"
  card:
    backgroundColor: "{colors.surface-dark}"
    rounded: "{rounded.card}"
  button-accent:
    backgroundColor: "{colors.accent-dark}"
    textColor: "#FFFFFF"
    rounded: "{rounded.control}"
  button-quiet:
    backgroundColor: "transparent"
    textColor: "{colors.text-muted-dark}"
    rounded: "{rounded.control}"
  rail-item:
    textColor: "{colors.text-dark}"
    rounded: "{rounded.control}"
    height: "40px"
  rail-item-selected:
    backgroundColor: "{colors.surface-hover-dark}"
  log-tag:
    textColor: "#FFFFFF"
    rounded: "{rounded.control}"
    typography: "{typography.log-tag}"
    width: "76px"
---

# Design System: GapMouse

## Overview

**Creative North Star: "The Door Hanger"**

GapMouse is the door hanger on your desk: one face, one word, readable from across the room, turned over when the state changes. The window is opened from the tray for a few seconds to confirm the app is doing its job, so the first thing seen is a full-height card in the colour of the current state with the state word in bold caps. Everything else (totals, recent times, the log, settings) is quiet instrument-panel reading on a tinted charcoal ground.

The voice is calm, legible and native to Windows: Segoe UI Variable throughout, rank by weight and case inside one tight scale, one corner radius family and one hairline weight on every surface. Precision and restraint (Operate mode). The system rejects one thing outright: a grey Fluent tab strip with a status dot and a form.

**Key Characteristics:**
- Three committed state fields (cobalt, green, amber), each with its own ink; the hanger face is the only large colour on screen.
- Flat, tonal layering: ground, card-stock rail, surface cards separated by hairlines, never shadows.
- Live values sit in fixed cells with tabular numerals so nothing jitters.
- Stamped and steady: fixed-width log tags, a single short flip as the only motion.
- Dark and light themes with the same structure; state fields and log tag fills are identical in both.

## Colors

A tinted-charcoal neutral world (blue-leaning, never pure grey or black) carrying three saturated state fields and one cobalt UI accent.

### Primary
- **Cobalt (waiting)** (#2F5FD0): the hanger face while idle time counts toward the next movement; also the light-theme accent and the Waiting log tag. White ink (#FFFFFF), secondary ink pale cobalt (#E6EDFF).
- **Accent Cobalt, dark theme** (#2F6FED): the primary action button and the active rail indicator on the dark ground; text-weight accent is lifted to #8FB0FF, and in light theme to #2350B8.

### Secondary
- **Hanger Green (moving)** (#157545): the face while the cursor is moving, and the Moving log tag. White ink, secondary ink mint (#D3F5E2).

### Tertiary
- **Amber (paused)** (#E3A21A): the face while paused, and the Paused/Resumed log tags. The only state with dark ink (#1F1503), secondary ink deep brown (#4A3407).

### Neutral
- **Tinted Charcoal Ground** (#14161B dark / #F4F5F7 light): the content background and the knob-hole fill.
- **Card-Stock Rail** (#0F1115 dark / #E6E9F0 light): the navigation rail, one step darker than the ground.
- **Surface** (#1B1E25 / #FFFFFF): cards; hover and selected fill #232731 / #EEF0F4.
- **Hairline** (#2C313C / #D8DCE3): the single 1px stroke for card borders, dividers and the settings action bar edge.
- **Text** (#ECEEF2 / #16181D) and **Muted Text** (#A0A8B6 / #576070): primary and label text.
- **Critical text** (#F87171 / #B42318) and **Warning text** (#FBBF24 / #9A5B00): validation and caution messages only.

### Named Rules
**The Committed State Rule.** A state owns a whole field of colour with its own paired ink; never a dot, badge or tint of it. Colour on the face is state, nowhere else.
**The Ink Pairing Rule.** Text on a state field uses that state's ink tokens (primary and secondary), not the theme text colours. Amber is the only dark-ink field.
**The Fixed Tag Table Rule.** Log tag colours are a fixed table identical in both themes: Start slate (#475569), Waiting cobalt (#2F5FD0), Moving green (#157545), Return grey (#5B6474), Paused/Resumed amber (#E3A21A), Config violet (#6B5BD6), Stop/Error red (#B42318), Warn orange (#C2410C). White ink except on amber.

## Typography

**Display Font:** Segoe UI Variable Display (with Segoe UI)
**Body Font:** Segoe UI Variable Text (with Segoe UI)
**Label/Mono Font:** Cascadia Mono (with Consolas), log timestamps only

**Character:** One humanist system face, ranked by weight and case rather than by size. The state word is the single loud moment; everything else is calm Windows text.

### Hierarchy
- **State word** (Bold 700, 30px, 0.6px tracking, UPPERCASE): the face word (WAITING, MOVING, PAUSED); scales down only (Viewbox, DownOnly) to fit the face, never wraps.
- **Title** (SemiBold 600, 20px): page-level headings.
- **Group** (SemiBold 600, 16px): card titles such as Today and Recent, and settings group headings.
- **Body** (Regular 400, 14px): labels, rows, rail items, messages.
- **Value** (SemiBold 600, 14px, tabular numerals, right-aligned): live numbers in stat rows.
- **Caption** (Regular 12px, muted): secondary help.
- **Log tag** (SemiBold 600, 11px, centered): stamped tag text inside a 76px field.

### Named Rules
**The Tabular Cell Rule.** Any value that changes while the window is open uses tabular numerals in a fixed, right-aligned cell.
**The Weight-and-Case Rank Rule.** Rank is carried by weight and case inside the 11-12-14-16-20-30 scale; do not add sizes or a second family.

## Layout

A fixed 184px navigation rail on the left (Home, Logs, Settings, About) and content on the ground to its right. Window default 760x600, minimum 640x520. Content margin is 24px; sections stack at 16px; stat rows are 32px minimum with 16px side inset; form rows are 44px minimum with 16/12 inset. Spacing steps are 4, 8, 16, 24.

Home is a two-column hanger layout: the face (220px wide, full height) on the left, a 16px gutter, then a right column of two stacked cards (Today, Recent), with an action bar below (primary Pause/Resume left, quiet Exit right). Under 460px container width the face narrows to 176px. Settings content is capped at 640px with a fixed bottom action bar on the ground; fields right-align in 96px inputs.

## Elevation & Depth

Flat, tonal layering with no shadows anywhere. Depth is three steps of the same hue: rail (darkest), ground, surface card, with a 1px hairline on every card edge. Hover and selection are a one-step surface shift (#232731 / #EEF0F4), not a lift. The hanger face is a flat saturated fill; its only "depth" is the ground-coloured knob hole cut into it.

### Named Rules
**The Flat-Always Rule.** No drop shadows, glows or blur. Separate with tone and the single hairline.

## Shapes

One geometry across every surface: 12px corners on cards and the hanger face, 6px on controls, rail items and log tags, a 3x16 pill (1.5px radius) as the rail's selected indicator. One 1px hairline stroke. The hanger face's signature is the knob hole: a 56px circle at the top whose stroke is the idle-progress ring (6px, round caps, starting at 12 o'clock, track at 25% opacity), hidden while paused.

## Components

Stamped and steady: every component is a flat fixed-geometry field with one job.

### Hanger Face
- **Shape:** 12px radius, 220px wide, 16px padding, full height of the content area.
- **State fills:** cobalt, green or amber per the Committed State Rule; the face sets foreground ink and children inherit it, so ring, word and secondary lines follow.
- **Content:** knob-hole ring at top centre; state word in bold caps at bottom; idle and pause text beneath in secondary ink.
- **Motion:** on state change the face flips horizontally, ScaleX 0.3 to 1 over 0.22s with a cubic ease-out, authored inside each state-class style so it replays.

### Buttons
- **Accent:** cobalt fill (#2F6FED dark / #2F5FD0 light), white text, 6px radius; hover 0.9 opacity, pressed 0.8, disabled 0.5. Used once per view (Pause/Resume, Save, OK). Pause is a split-style drop-down offering 10 minutes, 30 minutes, 1 hour, custom, until resumed.
- **Quiet:** transparent, muted text, no border at any state; used for Exit.
- **Default:** Fluent button; in light theme it takes white fill, hairline border, #F6F7F9 hover and #EEF0F4 pressed.

### Cards / Containers
- **Corner Style:** 12px.
- **Background:** surface; 1px hairline border.
- **Shadow Strategy:** none.
- **Rows:** stat rows separated by 1px hairline dividers; card title in Group style at 16/12/16/4 margin.

### Inputs / Fields
- **Style:** Fluent text boxes and combo boxes at 96px wide in settings, right-aligned numbers; errors appear as critical-text lines under the row label.
- **Focus:** the platform Fluent focus treatment.

### Navigation
- **Style:** card-stock rail at 184px with the app icon and name at top, then 40px items (16px Segoe Fluent Icons glyph plus 14px label) at 2px gap.
- **States:** hover and selected share the one-step surface fill; selected adds the 3x16 cobalt pill at the left; keyboard focus draws a 2px text-colour outline inside the item.

### Log Row and Tag
- **Row:** mono tabular timestamp (muted), a 76px stamped tag, then the message, 10px column gap, 6/4 item padding for density.
- **Tag:** 6px radius, 2px vertical padding, 11px SemiBold centered text on a fill from the Fixed Tag Table Rule.

## Do's and Don'ts

### Do:
- **Do** give each state its whole face: cobalt #2F5FD0, green #157545, amber #E3A21A with their paired inks.
- **Do** use 12px for cards and the face, 6px for controls and tags, and the 1px hairline for every edge.
- **Do** put changing numbers in tabular, right-aligned cells.
- **Do** keep one accent button per view and everything else default or quiet.
- **Do** keep state and tag fills identical across light and dark; only the neutrals change.
- **Do** keep log tags and on-disk log words in English tags at a fixed 76px width.

### Don't:
- **Don't** build the category default: a grey Fluent tab strip with a status dot and a form.
- **Don't** use shadows, glows or blur for depth.
- **Don't** signal state with a dot, badge or tint; use the face field and its word.
- **Don't** add a second typeface, extra type sizes, or a second corner radius family.
- **Don't** put theme text colours on a state field; use the state's ink tokens.
- **Don't** animate anything except the face flip (0.22s ease-out).
