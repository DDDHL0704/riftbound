# Minimal Official-Card Godot Client Design

## Decision

Rebuild the Riftbound Godot client as a minimal, rules-first desktop card table.
Official catalog card fronts are the primary visual system. The client will not
add an ornamental frame over official cards or build a decorative fantasy table
before the core flow is readable and playable.

This design replaces the earlier black/ivory wire-table direction. Existing
transport, server-authoritative command submission, official image loading,
multi-window session isolation, hidden-information checks, and evidence tooling
remain in scope and should be reused.

## Product Outcome

A player should be able to enter a room, choose a preconstructed deck, ready,
complete mulligan, and play a full match without consulting logs, object IDs, or
server terminology. At every prompt the screen must answer three questions:

1. Whose turn or priority is it?
2. What can I interact with now?
3. What will submit the current server-provided action?

## Visual System

### Surfaces

- Background: neutral graphite table surface with subtle value separation.
- Play zones: slightly lighter neutral surfaces, differentiated primarily by
  spacing and labels rather than repeated borders.
- Text: high-contrast off-white for primary information and cool gray for
  secondary information.
- State colors: green for legal/selectable, amber for selected/pending, red for
  hostile targets or destructive confirmation, and blue-gray for waiting.
- Decoration: no generated fantasy background, bevel system, or ornamental card
  frame is required for the playable milestone.

### Cards

- Face-up cards use the official `frontImage` as the entire visible card face.
- Preserve the official card aspect ratio. Never crop card text, cost, or stats.
- Hand cards target 112x156 at 1440x900 and may overlap horizontally while
  keeping the focused card fully visible.
- Battlefield cards target 84x117 at 1440x900; compact zones may use 72x101 only
  when a hover/focus path exposes the full card immediately.
- Hidden cards use one neutral project-owned card back and a count badge when a
  stack would exceed available space.
- Hover, keyboard focus, selection, and legal target are external outlines or
  shadows; they do not alter the official card image.

### Typography and Controls

- Primary body text is at least 16 px at 1440x900; secondary text is at least
  14 px. Turn and phase status is 20-24 px.
- Core click targets are at least 40 px high.
- Buttons use text or familiar icons with tooltips. Technical command names,
  prompt IDs, object IDs, and server ticks are not shown in the primary UI.

## Screen Architecture

### App Shell

The root shell owns connection lifecycle and switches between screens. It does
not render cards or construct prompt controls itself.

### Lobby Screen

The lobby contains identity/connection status, room or matchmaking entry,
preconstructed deck selection, submit, and ready. Advanced connection details
are collapsed under a settings affordance instead of occupying the main flow.

### Match Screen

The match uses five stable vertical regions:

1. Opponent summary and hidden hand.
2. Opponent units and compact public zones.
3. Two side-by-side battlefields with aligned opponent/self unit areas.
4. Self units and compact public zones.
5. Large self hand plus the contextual action bar.

Deck, rune pool, score, hero, base, legend, graveyard, banished, and standby are
compact labeled zones around the main formation. Empty zones remain identifiable
but do not expand into large blank rectangles.

### Card Inspection

Hover or keyboard focus shows a lightweight preview. Click or the inspect key
opens a large modal card view with the official image and readable catalog text.
The overlay closes with Escape and never reveals hidden card identity.

### Action Bar

The action bar shows only the current server-enabled commands. Direct actions
use this sequence:

1. The server prompt marks valid source cards or zones.
2. The player selects one source directly on the table.
3. The table marks valid targets or destinations from that same prompt.
4. The action bar summarizes the selection and exposes one submit button.
5. Submission uses the existing server prompt ID, snapshot tick, and command
   template; those identifiers remain internal.

Cancel clears only local selection state. It never changes game state.

### Focused Prompt Overlays

- Mulligan: selectable official hand cards with a selection counter and one
  confirm button.
- Trigger ordering: a simple reorderable list using server-provided labels.
- Damage assignment: attackers, blockers, and remaining damage shown together,
  with only server-provided assignment choices enabled.
- Destructive actions such as surrender require a confirmation dialog.

These overlays are purpose-built views, not a generic dropdown/form renderer.

### Result Overlay

Result is a centered overlay over the final table state. It shows win/loss,
winner, score, reason, and return-to-lobby. It does not share a narrow card
preview rail or expose raw event data.

## Runtime Boundaries

### Retained Services

- `RiftboundGameHubClient`: SignalR transport and server push events.
- `RiftboundApiClient`: preconstructed deck and HTTP data.
- `PlayerSessionSettings`: persistent or isolated identities.
- `OfficialCardCatalogService` and `OfficialCardImageLoader`: official card data
  and cached images.
- `CardViewFactory` and `SpecialPromptCommandBuilder`: safe mapping and command
  construction from visible server data.

### New View Boundaries

- `AppShell`: screen switching and connection/session coordination.
- `LobbyScreen`: lobby and deck setup presentation.
- `MatchScreen`: stable table layout and snapshot presentation.
- `OfficialCardView`: reusable face/back/focus/selection card component.
- `ActionBar`: current actions and selection summary.
- `PromptInteractionController`: local selection state derived only from the
  current server prompt; produces existing prompt submissions.
- `CardInspectOverlay`, `MulliganOverlay`, `TriggerOrderOverlay`,
  `DamageAssignmentOverlay`, and `ResultOverlay`: focused transient flows.

`Main.cs` will be reduced incrementally. Transport callbacks stay operational
while view construction moves behind these boundaries; a single rewrite is not
required.

## Data and Hidden Information

- Snapshot and prompt payloads are the only source of visible state and legal
  interactions.
- The client may store selected object IDs temporarily only when those IDs came
  from the current prompt. It may not infer candidates from card placement.
- Opponent hidden cards never enter `OfficialCardView` as face-up data.
- Card inspection is disabled for every hidden or face-down object.
- Prompt replacement or snapshot tick change clears stale local selections.

## Error Handling

- Connection failure returns to a clear reconnect state without destroying the
  selected local deck.
- Rejected or stale submissions clear pending animation, show the server message
  in plain language, and request a fresh snapshot when appropriate.
- Missing official art falls back to a readable neutral card containing safe
  visible catalog text; layout dimensions remain stable.
- Missing prompt metadata disables the affected action and logs a diagnostic.
  If the metadata is required for the core flow, implementation stops and the
  missing contract field is reported instead of adding client-side rules.

## Responsive Layout

- Reference viewport: 1440x900.
- Large viewport: 1920x1080 uses additional spacing and larger previews, not
  more permanent panels.
- Minimum supported viewport: 1280x720. Hand overlap may increase and compact
  public zones may collapse to stacks, but the action bar and submit button must
  remain visible.
- Layout uses responsive containers and aspect-ratio-preserving card controls;
  it must not depend on a fixed 820 px wire-table height.

## Accessibility

- All core actions are reachable by keyboard.
- Tab order follows lobby flow, then hand, table zones, action bar, and overlays.
- Focus is visible independently of hover.
- State is conveyed by label/icon plus color, not color alone.
- Escape cancels local selection or closes the top overlay; Enter confirms the
  focused enabled action.

## Verification

Each implementation milestone requires:

- `~/.dotnet/dotnet build clients/godot/Riftbound.GodotClient.csproj` with zero
  errors.
- A real memory-mode API and visible Godot window.
- Directly inspected screenshots at 1440x900 and, for layout work, 1920x1080.
- Hidden-information logs proving zero opponent hand faces and zero hidden
  identity leaks.
- A task script or focused test that checks the new component/state contract.
- Human review of action clarity; palette and geometry scripts are regression
  helpers only.

Final acceptance requires two human operators to complete a full match using
only the Godot UI, with no technical IDs or logs needed for decision making.

## Migration Order

1. Add the minimal theme tokens and official-card component.
2. Introduce the app shell and lobby screen while reusing existing callbacks.
3. Introduce the match screen and move snapshot table rendering into it.
4. Add prompt interaction state and direct source/target selection.
5. Replace mulligan, trigger, damage, and result generic prompt views.
6. Remove obsolete wire-table and right-rail code after parity is proven.
7. Complete responsive, keyboard, visual, and two-human verification.
