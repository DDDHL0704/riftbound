# Riftbound Godot Minimal Playable Client Plan

Godogen checkpoint for rebuilding the desktop client around official card art,
clear rules-facing interaction, and human playability. The previous inksteel
wire-table and final P5 evidence push are no longer the active product route.

## Guardrails

- Work in `clients/godot/` unless the user explicitly authorizes another area.
- The server remains authoritative. The client renders per-player snapshots and
  prompts, then submits only server-provided candidates and templates.
- Opponent hands, hidden standby cards, and unrevealed information render only
  as card backs and counts.
- Keep `Riftbound.Contracts`, backend rules, API behavior, and React DevUi
  unchanged. Stop and report if a required direct interaction lacks safe server
  prompt metadata.
- Use official catalog card fronts as the card presentation. Do not draw a
  second decorative frame over official cards.
- Automated smoke proves protocol regression only. It never proves human
  usability or visual quality.

## Approved Product Direction

- Style: minimal desktop card table, neutral graphite surfaces, readable type,
  restrained state colors, and official card artwork as the visual focus.
- Priority order: rule clarity, direct interaction, readable cards, responsive
  layout, then motion and decorative polish.
- Avoid: black-on-black wire grids, technical IDs, server ticks, raw logs,
  permanent generic prompt forms, tiny card thumbnails, and large empty panels.
- Required viewports: 1440x900 and 1920x1080, with a minimum supported window
  of 1280x720.
- Design specification:
  `docs/2026-07-10-minimal-official-card-client-design.md`.
- Executable implementation plan:
  `docs/plans/2026-07-10-minimal-official-card-client-implementation.md`.

## Current Status

- The responsive/keyboard/legacy-removal pass is complete. `Main.tscn` now
  mounts one lobby, one match screen, focused overlays, and the hidden log sink;
  the old snapshot rows, hand rail, prompt form, preview frame, and result rail
  are gone. `CardControlRenderer`, `RunestoneTheme`, `RunestoneBackdrop`, and
  `RunestoneSurface` are deleted. Official cards use the small independent
  `CardTextureLoader`. Five project input actions drive visible focus, inspect,
  cancel, confirm, and action cycling without changing server candidate rules.
  Fresh non-headless 1280x720, 1440x900, and 1920x1080 evidence plus MCP focus
  evidence is archived under `screenshots/units/m7-*`.
- The contextual action bar, prompt-owned selection controller, and dedicated
  special-prompt overlays are complete. Enabled actions come only from the
  current server prompt; direct card and safe choice activation accepts exact
  current candidate membership, clears on prompt/tick replacement, and maps
  back into the existing command submission methods. Mulligan renders only the
  current action's server-approved, visible self-hand cards with server min/max;
  trigger ordering preserves all server IDs behind server labels; damage
  assignment exposes only server metadata and choices. Missing metadata disables
  its overlay with a diagnostic instead of client rule inference. Focused checks,
  client build, and server-produced fixture-shape tests pass. A real two-window
  1440x900 mulligan run now proves official self-hand faces, opponent backs only,
  and zero hidden identity leaks. Isolated non-headless trigger-order and damage-
  assignment proof scenes exercise the production adapters with server fixture
  metadata shapes; evidence is archived under `screenshots/units/m6-*`.
- Focused card inspection and result presentation are complete. Visible cards
  open in a centered overlay with one large uncropped official face and safe
  catalog text; face-down or non-visible cards are rejected before display.
  Authoritative match results now use a centered win/loss overlay over the
  latched final table, with only viewer-safe winner, score, and reason text.
  Visible two-window 1440x900 evidence is archived under
  `screenshots/units/m4-card-inspect-*` and `screenshots/units/m4-result-*`.
- The responsive official-card match screen is complete. It presents opponent
  summary/back, two aligned battlefields, self public zones, a readable hand,
  and a stable bottom action host without the old permanent right rail. The old
  renderer is retained only behind a default-off fallback. Visible
  1280x720/1440x900/1920x1080 evidence is archived under
  `screenshots/units/m3-minimal-match-*` with zero hidden identity leaks.
- The focused lobby shell is complete. It owns connection, room/matchmaking,
  deck submission, and ready controls without sharing the viewport with battle
  prompts or card inspection. `SUBMIT_DECK` and `READY` availability mirrors
  enabled server prompt candidates. Visible 1280x720, 1440x900, and 1920x1080
  evidence is archived as `screenshots/units/m2-minimal-lobby-*`.
- M2 official-card foundation is complete: `OfficialCardView` preserves the
  official face aspect ratio, applies only external rule-state outlines, and
  suppresses identity/texture for hidden cards. Its visible 1440x900 proof is
  `screenshots/units/m1-official-card-component-1440x900.png`.
- Multi-window session isolation, SignalR/HTTP transport, official card catalog
  loading, preconstructed decks, prompt submission, hidden-information logging,
  screenshot capture, and final evidence tooling already exist and are retained.
- The coordinator remains concentrated in `Main.cs`; ordinary prompt actions
  use the focused action bar, and mulligan, trigger ordering, and combat damage
  use dedicated root overlays. There is no fallback table or generic prompt
  runtime left.
- The current client can reach a result through automated smoke, but the recent
  visual audit found that a human cannot reliably scan or operate it as a card
  game. P5 is paused until the interaction and layout rebuild is complete.

## Work Sequence

1. **M1 - Minimal shell and screen separation**
   - Separate lobby/deck setup, match table, card inspection, and result states.
   - Keep the existing coordinator and transport behavior intact while moving
     view ownership into focused scenes and controllers.
   - Done when lobby controls never share the battle viewport and both 1440x900
     and 1920x1080 screenshots show stable, unclipped shells.

2. **M2 - Official card component and readable table**
   - Render official card fronts without a custom ornamental frame.
   - Add one neutral card back, compact count badges, readable hover/focus, and
     a large inspection overlay.
   - Rebuild the table as opponent area, two aligned battlefields, self area,
     and a large bottom hand.
   - Done when card art is recognizable at normal view distance and hidden
     opponent cards remain backs/counts only.

3. **M3 - Direct prompt-driven interaction**
   - Map enabled prompt sources to selectable cards and zones.
   - Use a bottom action bar for current commands and temporary focused overlays
     for mulligan, trigger ordering, and damage assignment.
   - Remove technical IDs and generic selectors from the primary UI.
   - Done when a human can submit deck, ready, mulligan, tap runes, play a card,
     move, declare battle, assign damage, pass, and end turn without reading logs.

4. **M4 - State clarity and restrained feedback**
   - Add prominent turn/phase status, selection states, legal-target states,
     exhausted/tapped state, combat state, waiting state, and error recovery.
   - Add short transitions only where they confirm an accepted server action.
   - Done when screenshots and a short visible recording make the current turn,
     current selection, and next action obvious.

5. **M5 - Lobby, result, accessibility, and responsive completion**
   - Finish deck selection, room/matchmaking entry, result overlay, return flow,
     keyboard navigation, visible focus, and minimum target sizes.
   - Done when the core flow works at all supported viewports without clipping
     and can be completed by keyboard.

6. **M6 - Human full-match proof**
   - Run two real Godot clients with two human operators and preconstructed decks
     through a complete match to the result view.
   - Package screenshots/video, logs, hidden-information evidence, build output,
     asset manifest, and operator notes.
   - Done only when both humans confirm that no log or technical identifier was
     needed to understand the match.

## Work Unit Gate

For every implementation unit:

1. Build the Godot client before and after the change.
2. Run the real memory-mode API and open a visible Godot window.
3. Capture the affected flow at 1440x900; capture 1920x1080 when layout changes.
4. Inspect the image directly for readability, clipping, action clarity, and
   hidden-information safety.
5. Run focused static/integration checks, then `git diff --check`.
6. Update `PLAN.md`, `STRUCTURE.md`, `MEMORY.md`, and `assets/ASSETS.md` when
   their contracts change.
7. Commit and push the scoped `clients/godot/` change to `main`.

## Stop Conditions

- Completion requires a verified two-human full match using the rebuilt Godot
  interface, not the old wire table and not auto-smoke.
- Stop and report if direct interaction requires backend or contract fields that
  are not already present.
- Do not call the client complete while technical IDs, generic prompt forms,
  unreadable cards, hidden-information leaks, or viewport clipping remain.
- Do not mark the project READY.
