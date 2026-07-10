# Web First Official Card Client Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver the React Web client as a rules-complete, two-player playable Riftbound interface using official card faces and a minimal game-table layout.

**Architecture:** Keep the existing server-authoritative transport, snapshots, prompt interaction models, and command builders. Add a presentational play surface that composes the existing table rows and action components, move technical surfaces into a collapsed debug region, and apply a new route-focused stylesheet imported after the legacy CSS.

**Tech Stack:** React 19, TypeScript 5.9, Vite 7, SignalR, lucide-react, existing Riftbound protocol/view-model utilities, in-app browser verification.

## Global Constraints

- The server remains the sole rules authority; the client submits only existing prompt candidates.
- Never expose opponent hand faces, deck order, unrevealed rune identity, or face-down standby identity.
- Use complete official card fronts from `BehaviorSpec.frontImage`; use the existing text fallback when unavailable.
- Primary target is 1440x900, verification target is 1920x1080, and minimum desktop target is 1280x720.
- Primary UI must not display object IDs, prompt IDs, snapshot ticks, command JSON, or protocol-stage diagrams.
- Do not modify `src/Riftbound.Engine`, `src/Riftbound.Contracts`, or gameplay rules.

---

### Task 1: Restore the Frontend Gate

**Files:**
- Modify: `src/Riftbound.DevUi/src/utils/eventLogPlan.ts`
- Test: `src/Riftbound.DevUi/scripts/check-event-labels.mjs`

**Interfaces:**
- Consumes: the event-kind inventory parsed by `check-event-labels.mjs`.
- Produces: Chinese labels for `CARD_CHOICE_REQUESTED` and `CARD_CHOICE_RESOLVED` through the existing event label map.

- [x] Run `npm run check:event-labels` and retain the missing-label failure as the red test.
- [x] Add both labels to the existing exhaustive presentation map without changing event behavior.
- [x] Run `npm run check:event-labels`; expected result is `event labels complete`.
- [x] Run `npm run typecheck:strict`; expected exit code is 0.
- [x] Commit the focused repair with a message explaining that it restores the frontend verification gate before visual work.

### Task 2: Add the Minimal Web Visual Foundation

**Files:**
- Create: `src/Riftbound.DevUi/src/styles/game-client.css`
- Modify: `src/Riftbound.DevUi/src/main.tsx`
- Modify: `src/Riftbound.DevUi/src/components/layout/AppShell.tsx`
- Test: `src/Riftbound.DevUi/scripts/check-playable-surface.mjs`
- Modify: `src/Riftbound.DevUi/package.json`

**Interfaces:**
- Consumes: existing `app-frame`, `main-nav`, `Button`, and route state.
- Produces: `game-app-shell`, `game-primary-nav`, shared color/focus/card-size variables, and `check:playable-surface`.

- [x] Create `check-playable-surface.mjs` to fail until `game-client.css` is imported after `globals.css`, the shell exposes `data-game-shell`, and the primary navigation contains only play, cards, decks, and settings destinations.
- [x] Run `npm run check:playable-surface`; expected result is a nonzero exit with the missing surface markers.
- [x] Add the route-focused stylesheet import and simplify the visible navigation while retaining secondary routes in a labelled overflow group.
- [x] Implement neutral graphite tokens, focus rings, buttons, forms, reduced motion, and 1280x720 constraints in `game-client.css`.
- [x] Run `npm run check:playable-surface` and `npm run typecheck:strict`; both must pass.
- [x] Capture the lobby at 1440x900 and inspect for overflow, contrast, and obvious wireframe styling.
- [x] Commit the visual foundation as one reviewable unit.

### Task 3: Make Lobby and Room Play-First

**Files:**
- Modify: `src/Riftbound.DevUi/src/pages/LobbyPage.tsx`
- Modify: `src/Riftbound.DevUi/src/pages/RoomPage.tsx`
- Modify: `src/Riftbound.DevUi/src/styles/game-client.css`
- Test: `src/Riftbound.DevUi/scripts/check-playable-surface.mjs`

**Interfaces:**
- Consumes: current matchmaking, room join, starter-deck submission, ready, connection recovery, and room workflow handlers.
- Produces: `data-play-lobby`, `data-play-room`, a compact primary action region, two seat summaries, and a collapsed diagnostics region.

- [ ] Extend the static check to require the new lobby/room markers and to reject `prompt/tick` and server-chain headings outside diagnostic containers.
- [ ] Run the check and confirm it fails on the current pages.
- [ ] Reorder Lobby so matchmaking is primary, room-code join is secondary, and public rooms are a compact list.
- [ ] Reorder Room so room code, seats, deck submission, and ready state are visible above the fold; wrap logs, workflow diagrams, and submission evidence in a collapsed diagnostics section.
- [ ] Verify create/join room and deck/ready actions against the running API in two browser pages.
- [ ] Capture lobby and room at 1440x900, then run static check and typecheck.
- [ ] Commit the play-first lobby/room flow.

### Task 4: Replace the Wireframe Match Surface

**Files:**
- Create: `src/Riftbound.DevUi/src/components/match/PlayableMatchSurface.tsx`
- Modify: `src/Riftbound.DevUi/src/pages/MatchPage.tsx`
- Modify: `src/Riftbound.DevUi/src/styles/game-client.css`
- Test: `src/Riftbound.DevUi/scripts/check-playable-surface.mjs`

**Interfaces:**
- Consumes: `tableRows: ReactNode`, `actionDock: ReactNode`, `objectTray: ReactNode`, `debugContent: ReactNode`, match status strings, and existing navigation/recovery callbacks.
- Produces: `PlayableMatchSurface`, `data-playable-match-surface`, `data-game-table`, `data-game-action-dock`, and a collapsed `data-game-debug-drawer`.

- [ ] Extend the static check to require all playable match markers and verify the debug drawer is collapsed by default.
- [ ] Run the check and confirm it fails before the component exists.
- [ ] Implement the presentational component with a compact top status bar, full-height table region, bottom action dock, and debug details.
- [ ] Compose existing `WireBattlefieldTable`, `WireHandRail`, and `WirePlayerHome` rows into the new table without changing their view models or click handlers.
- [ ] Place existing `ActionPanel`, quick actions, and `WireObjectCommandTray` in the bottom dock; retain remaining authority/log panels under the debug drawer.
- [ ] Restyle table zones and official cards with stable aspect ratios, readable hover preview, selected/eligible/target state outlines, and a neutral hidden card back.
- [ ] Verify the layout fixture at 1280x720, 1440x900, and 1920x1080; no essential action may leave the viewport.
- [ ] Run static check, tabletop-layout check, typecheck, and capture all three viewport screenshots.
- [ ] Commit the playable match surface.

### Task 5: Complete Prompt and Result Presentation

**Files:**
- Modify: `src/Riftbound.DevUi/src/components/match/ActionPanel.tsx`
- Modify: `src/Riftbound.DevUi/src/components/cards/CardFace.tsx`
- Modify: `src/Riftbound.DevUi/src/components/cards/CardDetailDrawer.tsx`
- Modify: `src/Riftbound.DevUi/src/pages/ResultPage.tsx`
- Modify: `src/Riftbound.DevUi/src/styles/game-client.css`
- Test: existing action-panel, card-detail, hidden-information, and result checks.

**Interfaces:**
- Consumes: existing typed prompt choice models and `GameCommand` callbacks.
- Produces: plain-language selection progress, direct legal command controls, external card-state badges, and a minimal result summary.

- [ ] Run the focused action-panel and result checks before edits to establish the current baseline.
- [ ] Remove technical labels from visible prompt copy while retaining their data attributes for tests and diagnostics.
- [ ] Ensure mulligan, payment, movement, battle declaration, damage assignment, trigger ordering, response, pass, skip, and end-turn candidates remain reachable from the action dock.
- [ ] Ensure full official art remains unobstructed and hidden cards never receive a spec or front image.
- [ ] Recompose Result around winner, score, return to lobby, and replay actions; move event evidence into collapsed diagnostics.
- [ ] Run all focused checks and capture match/card-detail/result screenshots.
- [ ] Commit prompt, card, and result polish.

### Task 6: Prove a Full Two-Browser Match

**Files:**
- Modify only when a verified frontend defect requires it.
- Evidence: `src/Riftbound.DevUi/artifacts/web-playable-20260710/`

**Interfaces:**
- Consumes: the running API at `http://127.0.0.1:5088` and Vite at `http://127.0.0.1:5173`.
- Produces: screenshots for lobby, room, mulligan, main action, combat/damage, hidden opponent hand, and result.

- [ ] Start the API in in-memory development mode and Vite on an available port.
- [ ] Use two independent browser contexts to create/join one room, submit preconstructed decks, ready, mulligan, and play through every server prompt to result.
- [ ] At each stage, verify that player A cannot find player B hand identities or front-image URLs in visible DOM and vice versa.
- [ ] Save stage screenshots at 1440x900 and final match screenshots at 1920x1080.
- [ ] Run `npm run build`, `npm run check:tabletop-layout`, `npm run smoke:chrome`, `npm run qa:appshots`, and `git diff --check`; all must pass.
- [ ] Fetch and rebase on `origin/main`, commit any final frontend-only repairs, and push `main`.
- [ ] Record Web completion and create the separate Godot desktop-client implementation plan using the verified Web information architecture.
