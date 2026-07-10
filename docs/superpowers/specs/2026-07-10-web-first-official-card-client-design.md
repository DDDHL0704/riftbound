# Web First Official Card Client Design

## Decision

The React DevUi becomes the primary playable client. Godot remains the later desktop client; it is not used for Web export because the current Godot 4 C# client cannot produce a supported Web build.

The visual direction is deliberately restrained: official complete card fronts, a neutral graphite table, warm white text, and a small set of gold, blue, green, and red state colors. The interface serves rules and playability before decoration.

## Product Goal

A player can open the site, create or join a room, choose a deck, ready up, complete mulligan and every server-prompted game action, inspect official cards, and reach the result screen without reading server logs, object IDs, prompt IDs, ticks, or protocol terminology.

The client remains a projection of player-scoped server state. It never determines legality, invents candidates, or infers hidden identities.

## Primary Journey

1. **Lobby**: one clear primary action for matchmaking, one room-code entry path, and a compact list of public rooms.
2. **Room**: room code, two seats, deck selection, submission state, and ready action. Recovery is visible only when it needs attention.
3. **Match**: opponent at the top, two battlefield lanes in the center, the local player's board and hand at the bottom, with a compact status bar and server-driven action dock.
4. **Inspect**: hover gives an enlarged visual preview; click opens official card art and readable text with only legal context actions.
5. **Result**: winner, score, rematch/return actions, and a compact match summary.

The home, profile, card library, deck library, layout lab, audit, and settings routes remain available but are secondary. They must not compete with the play journey in the main navigation.

## Match Information Architecture

### Always Visible

- Opponent name, score, deck/rune counts, battlefield cards, and hand backs/count only.
- Turn number, phase, priority owner, and one plain-language guidance sentence.
- Two battlefields with control and score state from the snapshot.
- Local bases, units, gear, runes, deck/graveyard/banished counts, and hand.
- Legal source/target/destination highlights derived exclusively from `ActionPromptDto` candidates.
- A bottom action dock containing the current prompt title, selection progress, legal command buttons, and pass/skip/end-turn actions exposed by the server.

### On Demand

- Card detail drawer.
- Connection recovery only when disconnected, stale, or rejected.
- Event history and server diagnostics inside a collapsed debug drawer.

### Never on the Primary Surface

- Prompt IDs, snapshot ticks, object IDs, command JSON, server event tables, rule queues, or protocol-stage diagrams.
- Opponent hand faces, deck order, unrevealed rune identity, or face-down standby identity.

## Interaction Rules

- Clicking a highlighted card or zone updates the existing prompt selection draft.
- Selected objects receive a stable blue outline; valid next choices use gold; ready-to-submit actions use green; rejected/stale state uses red.
- Complex prompt families such as mulligan, damage assignment, payment, trigger ordering, battle declaration, and movement keep their existing typed models, but render in the bottom dock rather than a technical side panel.
- Buttons submit existing `GameCommand` values with prompt/tick identity. The Web client does not add rule branches.
- Keyboard focus mirrors hover/selection. `Escape` closes detail layers, and all command buttons remain reachable in logical tab order.

## Official Card Presentation

- `BehaviorSpec.frontImage` is the card face whenever available. No decorative replacement frame is drawn over official card art.
- Cards keep a fixed official-card aspect ratio. Hover enlargement cannot shift the table layout.
- Power/damage/exhaustion and legal-action state are external badges or outlines so official text and art stay unobstructed.
- Missing images fall back to the existing text card without changing layout.
- Hidden objects always use one neutral card back and accessible text that contains no identity.

## Visual System

- Backgrounds: `#101416` page, `#171c1f` table, `#202629` raised controls.
- Text: `#f2eee5` primary, `#b8b8b0` secondary.
- Borders: subtle neutral `#3a4143`; no black-and-white wireframe outlines.
- State accents: gold `#d7ad57`, blue `#63a7d8`, green `#68b684`, red `#d36b65`.
- Typography: system sans for UI and a restrained serif only for route/page titles. Letter spacing remains `0`.
- Corners: 4-8 px. Repeated cards may be framed; page sections are unframed bands.
- Motion: 120-180 ms state transitions and restrained card lift/selection. Respect `prefers-reduced-motion`.

## Responsive Contract

- Primary design target: 1440x900.
- Verification target: 1920x1080.
- Minimum supported desktop viewport: 1280x720.
- Match uses the full viewport. The action dock may scroll internally; cards and battlefields must not overlap or push essential actions below the viewport.
- Smaller viewports reduce card size and spacing, not typography via viewport scaling.

## Technical Boundaries

- Reuse `useMatchController`, `MatchSocket`, `buildWireTableViewModel`, `buildPromptInteractionModel`, `ActionPanel`, `CardFace`, `CardDetailDrawer`, and all existing command builders.
- Introduce a new presentational match surface and a final route-specific stylesheet instead of extending the 21k-line legacy wireframe stylesheet with more unrelated rules.
- Preserve existing test hooks where they express protocol behavior. Add new hooks for the playable surface rather than deleting authority checks.
- No changes to `Riftbound.Engine`, `Riftbound.Contracts`, or `Riftbound.Api` are expected for the Web redesign.

## Acceptance

- A two-browser match reaches result using only visible game controls.
- The 1440x900 and 1920x1080 screenshots read as a card game table, not an admin/debug console.
- Lobby, room, match, and result share the same restrained visual system.
- Opponent hidden information is represented only by backs and counts in screenshots and DOM.
- `npm run build`, `npm run check:tabletop-layout`, `npm run smoke:chrome`, `npm run qa:appshots`, and `git diff --check` pass.

