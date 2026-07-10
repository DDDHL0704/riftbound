# M7 Responsive and Keyboard Visible Evidence

Date: 2026-07-10

## Supported desktop sizes

Each capture used two normal, non-headless Godot windows against the local
memory-mode API. Setup automation only selected a preconstructed deck, readied,
and confirmed a zero-card mulligan so the product reached the first main-phase
table. The screenshots are direct Godot viewport captures from the actionable
player.

- `m7-minimal-runtime-1280x720.png`
- `m7-minimal-runtime-1440x900.png`
- `m7-minimal-runtime-1920x1080.png`

At every size the turn banner, opponent summary and card backs, both aligned
battlefields, self public area, official hand cards, and contextual action bar
remain visible. No old snapshot rows, right prompt rail, preview frame, or
right-rail result panel is mounted.

The matching `m7-minimal-runtime-*-visible-run.txt` files contain the visible
run logs. They report no Godot exception or overlay diagnostic and keep
`opponentHandFaces=0`, `opponentStandbyFaces=0`, and
`hiddenCardIdentityLeaks=0`.

## Keyboard focus

After restarting the Godot editor to discard its pre-change scene cache, the
MCP runtime launched the current disk `Main.tscn`. The runtime tree contained
only `LobbyScreen`, `MatchScreen`, the five focused overlays, and the hidden log
sink. `m7-keyboard-lobby-focus-before.png` shows initial focus on the player-name
field; dispatching the real `ui_focus_next` input action produced
`m7-keyboard-lobby-focus-next.png`, with focus moved to the room field. Godot
reported zero runtime errors.

The project maps inspect, cancel, confirm, previous, and next actions in
`project.godot`. `Main.HandleKeyboardAction` routes them only to the visible top
overlay or contextual action bar. Confirm invokes an enabled focused control;
it never selects a rule candidate locally.

