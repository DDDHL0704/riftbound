# M2 Minimal Lobby Visible Runs

These captures came from normal Godot windows against the local memory-mode
API. None of the commands used `--headless`. Each paired `*-visible-run.txt`
starts with the Metal/OpenGL renderer, records the server `ROOM` snapshot and
Prompt, confirms `submitDeck=True ready=False`, records the hidden-information
boundary, and names the PNG written by that same process.

The three runs used this command shape, with the resolution, handle, log, and
screenshot names changed together:

```bash
/Applications/Godot_dotnet.app/Contents/MacOS/Godot \
  --path clients/godot \
  --resolution 1440x900 \
  --log-file "$PWD/clients/godot/screenshots/units/m2-minimal-lobby-1440x900-visible.log" \
  --riftbound-ephemeral-session \
  --riftbound-handle=trace-lobby-1440 \
  --riftbound-smoke-auto-public-match \
  --riftbound-visual-screenshot="$PWD/clients/godot/screenshots/units/m2-minimal-lobby-1440x900.png" \
  --riftbound-visual-screenshot-min-table-cards=0
```

Direct inspection results:

- 1280x720: all setup controls remain visible and contained.
- 1440x900: reference layout is complete with no combat rail overlap.
- 1920x1080: the layout expands without introducing permanent side panels.
- All sizes: official preview, prompt, result, raw log, and battle table are
  absent in lobby state; Submit Deck is enabled by the server Prompt and Ready
  remains disabled.

`m2-minimal-lobby-ready-transition-visible-run.txt` is a second normal-window
server run through the setup sequence. It records the authoritative transition
from `SUBMIT_DECK:on` to `READY:on` to `WAIT:off`, and the UI availability moves
from `submitDeck=True ready=False` to `submitDeck=False ready=True` to both
false in the same order.
