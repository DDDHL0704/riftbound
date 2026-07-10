# M3 Minimal Match Visible Runs

The M3 captures came from two normal Godot windows connected to one real
memory-mode API room. The paired `*-visible-run.txt` files begin with the
Metal/OpenGL renderer, include accepted setup/mulligan traffic, record the exact
PNG capture path, and report the machine hidden-information boundary.

The runs used `run-local-human-playtest-stack.sh` without `--headless`; setup
and mulligan were advanced through the existing non-final smoke flags so the
new table could be inspected at a stable first-turn state. This is visual-unit
evidence, not final two-human evidence.

Direct inspection:

- 1280x720: turn status, opponent summary/back, both battlefields, official
  landscape sites, self public cards, full hand, and bottom action host are all
  visible without clipping.
- 1440x900: both player perspectives show uncropped official hand cards; the
  opponent hand is one neutral back plus count and never a face.
- 1920x1080: the table expands without adding a permanent right rail; official
  cards retain stable sizes and both lanes remain aligned.
- All captured logs report `opponentHandFaces=0` and
  `opponentStandbyFaces=0` plus `hiddenCardIdentityLeaks=0`. The boundary marks
  either face count as a violation. Final logs contain no Godot warning, leaked
  CanvasItem, leaked ObjectDB instance, exception, or script error.

The official battlefield source images store landscape cards rotated inside a
portrait bitmap. `OfficialCardView` follows the server-visible `rotated` flag,
rotates only the runtime texture counterclockwise, and preserves the full image.
