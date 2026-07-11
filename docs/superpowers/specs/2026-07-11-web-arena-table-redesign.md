# Web Arena Table Redesign

## Decision

The React DevUi match route will replace its five-row table and fixed bottom
action dock with one full-viewport, layered DOM arena. This document supersedes
the match-layout and action-dock portions of
`2026-07-10-web-first-official-card-client-design.md`; the earlier server
authority, official-card, lobby, room, and result decisions remain in force.

This goal ends after the Web client is complete. Godot parity is a separate
later phase.

## Product Goal

The two shared battlefields and their opposing units must be the dominant
first-viewport experience. The local hand remains immediately usable without
reserving a large horizontal panel, and every server-prompted action is
completed directly on the table or in a small contextual layer.

The client remains a player-scoped projection of server snapshots and prompts.
It never determines legality, invents candidates, or infers hidden identities.

## Desktop Arena

The match fills `100dvh` and does not use document-level vertical scrolling.
A compact top bar of approximately 48 px contains exit, turn, phase, current
window, score, connection state, and server-exposed quick actions. The remaining
space is one continuous table rather than stacked framed sections.

The desktop arrangement is:

```text
+----------------------------------------------------------------+
| exit | turn / phase / window              score / connection    |
+----------------------------------------------------------------+
| opponent runes  opponent base / hero / legend  deck / banish    |
|                     opponent hidden hand fan                     |
|                                                                  |
| [left site]  left opposing units  right opposing units [right site]|
|              left local units     right local units               |
|              standby slots remain attached to their lane          |
|                                                                  |
| local runes       local base / hero / legend      deck / banish  |
|                       local hand fan                              |
+----------------------------------------------------------------+
```

- The left battlefield card is anchored to the table's outer left edge; the
  right battlefield card is anchored to the outer right edge.
- The two lane interiors occupy the center and each place opponent units above
  local units. Standby slots remain visibly associated with their side and lane.
- The battlefield and unit region receives about 50% of the effective table
  height at 1440x900 and 1920x1080, leaving expanded public player zones.
- Local runes and the rune deck form a compact cluster in the lower-left
  viewpoint corner. Main deck, banish, and related piles occupy the lower-right.
  The opponent arrangement mirrors this at the top.
- Base, hero, and legend cards sit along the centered top and bottom edges
  instead of consuming full-width rows.
- The local hand is an overlay anchored to the bottom edge. Its resting visible
  height is no more than 18% of the viewport and it does not participate in the
  arena's grid sizing.
- The opponent hand is a compact top card-back fan and count. It never exposes
  face images, card names, ordering, or identity-bearing accessibility text.

## Card Layout And Overflow

Official complete card fronts remain the normal representation for all visible
cards. Battlefield cards are the largest persistent table objects, followed by
units and other public cards. Runes and pile markers are smaller edge controls.

Public cards preserve a readable size as object counts grow. A zone first
reduces gaps, then introduces mild horizontal overlap. Hovered, keyboard-focused,
or selected cards move above their neighbors. Only extreme counts introduce
zone-local scrolling; the implementation must not continuously shrink every
card to illegibility.

The local hand uses a true fan presentation:

- Cards overlap and rotate around a shared lower pivot.
- Resting cards expose cost, title, and primary art.
- Hover or keyboard focus lifts and enlarges one card while nearby cards yield
  enough space to keep the focused face readable.
- A selected card remains raised until the selection is completed or cancelled.
- Lift and inspection layers must not cover a currently legal destination. The
  arena reserves a lower interaction-safe boundary for highlighted targets.

## Direct Interaction

The fixed `game-action-dock` is removed from the playable match layout.

1. Clicking a server-highlighted source selects it.
2. The client highlights only legal next targets, destinations, and optional
   costs already present in the current server prompt model.
3. Clicking the next table object advances the existing typed selection draft.
4. If a complete candidate needs payment, optional costs, mode selection, or
   confirmation, a compact popover opens near the selected source or target.
5. Submitting uses the existing command builder with prompt and tick identity.
6. Cancellation clears the draft without changing authoritative state.

Hover supplies a quick enlarged preview. A context-menu action or explicit
detail icon pins the complete card view, keeping ordinary click reserved for
game actions. Keyboard focus mirrors hover; arrow navigation, Enter, and Escape
provide the same source, target, confirmation, and cancellation flow.

Mulligan, damage assignment, trigger ordering, and other multi-object prompt
families remain dedicated centered overlays because a small anchored popover
would obscure required context. The table stays visible behind these overlays.
Pass, skip, response, and end-turn actions remain visible near the current
window in the top bar when the server exposes them.

## Secondary Information

The primary table permanently shows only score, turn, phase, current window,
deck/rune counts, connection state, visible zones, and plain-language guidance.
Event history, diagnostics, rule evidence, and connection details move to an
on-demand right drawer. The drawer is closed by default and must not reserve
arena width.

Disconnected state appears in the top bar with a reconnect action while the
last authoritative snapshot remains visible. A rejected or stale submission
clears the local draft and explains the rejection at the originating popover or
selection point. Waiting, no-action, and result states do not replace the table
with technical output.

## Visual Direction

The presentation is restrained and uses official art rather than decorative
replacement frames.

- Table: `#111615`
- Neutral zone line: `#56605a`
- Primary text: `#f0eee7`
- Legal action: `#55c89b`
- Selection and confirmation: `#d5ae62`
- Damage, invalid, and failure: `#e46c72`

The visible hero or legend art for each player may become a low-contrast image
across that player's half. The source must already be visible to the viewer;
hidden data can never select a background. Art is desaturated and covered by a
dark table mask so official card fronts remain the highest-contrast elements.
Missing art falls back to the neutral table without affecting geometry.

Large framed panels are removed. Zones use quiet labels, subtle surface changes,
and thin boundaries only where ownership or lane association requires them.
Motion is limited to hand lift, legal-target pulse, selection connection,
object movement, and contextual-layer entrance, with reduced-motion support.

## Responsive Contract

- `1920x1080` and `1440x900`: full arena, side battlefield cards, edge resource
  clusters, fan hand, and contextual actions.
- `1280x720`: same spatial grammar with smaller edge clusters and denser card
  overlap; no essential control may clip or leave the viewport.
- Below the desktop breakpoint: a separate vertical match layout. Opponent
  summary stays at the top, battlefield lanes are horizontally switchable, and
  the local hand expands from the bottom. It does not compress the desktop
  arena into unreadable columns.
- `390x844` must complete the core source/target/action flow and preserve hidden
  information. Desktop and mobile share the same view model and command path.
- Font size does not scale with viewport width. Card and zone geometry use grid,
  container constraints, and stable aspect ratios.

## Component Boundaries

`MatchPage`, `useMatchController`, SignalR transport, snapshot projection,
prompt interaction models, command builders, and official card components stay
authoritative. The presentation is separated into focused components:

- `ArenaTable`: full-viewport layers and spatial contract.
- `BattlefieldLane`: lane site, opposing unit zones, and standby association.
- `PlayerEdgeRail`: base, hero, legend, deck, banish, and public piles.
- `RuneCluster`: rune deck and visible rune state at the viewpoint corner.
- `FanHand`: local fan and hidden opponent fan presentations.
- `ContextActionPopover`: anchored rendering of the existing candidate draft.
- `MatchSideDrawer`: event history, diagnostics, and secondary evidence.

The existing layout JSON remains the semantic zone contract. Its row-oriented
presentation fields may be replaced by arena slots and measurable layout tokens,
but zone IDs, sides, hidden-information fields, and server mappings must remain
testable. Complex prompt renderers are reused behind a new presentation adapter
rather than reimplemented with local rule branches.

## Acceptance And Evidence

- At 1440x900 and 1920x1080, automated geometry checks measure at least 50%
  effective table height for the battlefield/unit region and no more than 18%
  resting viewport height for the local hand.
- No fixed bottom action dock exists, the document has no match-route vertical
  overflow, and the local hand does not occlude any highlighted legal target.
- At 1280x720, every required action and confirmation remains reachable. At
  390x844, the independent vertical layout completes the core interaction flow.
- Every current server candidate family remains submit-capable through direct
  table selection, contextual presentation, or a dedicated complex-prompt
  overlay.
- Opponent hand and hidden standby checks find card backs and counts only, with
  zero face images and zero identity-bearing labels.
- Image failures preserve the table, card aspect ratios, and readable fallback
  content.
- `npm --prefix src/Riftbound.DevUi run build`, layout and interaction contract
  checks, `qa:appshots`, `smoke:chrome`, and `git diff --check` pass.
- Two visible Web clients connect to the memory API and cover room/deck/ready,
  mulligan, main actions, at least one complex prompt, and authoritative result.
  Accepted screenshots include lobby, opening hand, arena main action, complex
  prompt, and result states.

No changes to `Riftbound.Engine`, `Riftbound.Contracts`, or game rules are part
of this redesign.
