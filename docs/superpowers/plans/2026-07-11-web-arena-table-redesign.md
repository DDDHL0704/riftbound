# Web Arena Table Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the Web match route's stacked rows and fixed action dock with a full-viewport arena where shared battlefields dominate, the local hand is a bottom fan, and server-prompted actions use direct table selection plus contextual layers.

**Architecture:** Keep `MatchPage`, snapshot projection, prompt interaction models, command builders, and official card rendering authoritative. Add an arena presentation contract and focused DOM components around the existing zone renderers, then load a final route-specific stylesheet after legacy styles so geometry is explicit and measurable without rewriting rule-facing models.

**Tech Stack:** React 19, TypeScript, Vite, CSS Grid/container constraints, SignalR, existing Node source-contract checks, Playwright QA, Chrome CDP smoke.

## Global Constraints

- Modify only the Web client and its documentation; do not change `Riftbound.Engine`, `Riftbound.Contracts`, `Riftbound.Api`, or gameplay rules.
- All legality, candidates, payment choices, and target highlights come from the player-scoped server snapshot and `ActionPromptDto`.
- Opponent hand and face-down standby objects expose card backs and counts only, including accessible labels and background-art selection.
- At 1440x900 and 1920x1080, the battlefield/unit region is at least 65% of effective table height and the resting local hand is at most 18% of viewport height.
- At 1280x720, every core action remains reachable without document-level vertical scrolling; 390x844 uses a separate vertical layout.
- Use complete official card fronts and existing fallback cards. Do not draw replacement frames over official art.
- Every task follows red-green verification and ends in a focused commit.

---

### Task 1: Arena Layout Contract And Failing Gate

**Files:**
- Modify: `src/Riftbound.DevUi/src/components/match/wireTableLayout.ts`
- Modify: `src/Riftbound.DevUi/src/components/match/wireTableLayoutData.json`
- Create: `src/Riftbound.DevUi/scripts/check-arena-table-contract.mjs`
- Modify: `src/Riftbound.DevUi/package.json`

**Interfaces:**
- Consumes: existing semantic player, hand, battlefield, and zone IDs in `WIRE_TABLE_LAYOUT`.
- Produces: `ArenaLayoutContract`, `ARENA_LAYOUT`, and a build-time gate for required slots and ratios.

- [ ] **Step 1: Write the failing contract check**

```js
const arena = layout.arena;
assert(arena, "wireTableLayoutData.json must define arena");
assert(arena.battlefieldMinHeightRatio >= 0.65, "battlefield ratio must be at least 0.65");
assert(arena.handMaxViewportRatio <= 0.18, "hand ratio must be at most 0.18");
assert.deepEqual(arena.battlefieldSlots, ["leftSite", "leftLane", "rightLane", "rightSite"]);
assert.equal(arena.self.runes, "bottomLeft");
assert.equal(arena.self.mainDeck, "bottomRight");
assert.equal(arena.opponent.hiddenHand, "topFan");
```

- [ ] **Step 2: Run the check and verify it fails**

Run: `npm --prefix src/Riftbound.DevUi run check:arena-table-contract`

Expected: non-zero exit with `wireTableLayoutData.json must define arena`.

- [ ] **Step 3: Add the typed arena contract**

```ts
export type ArenaEdgePosition = "topLeft" | "topCenter" | "topRight" | "bottomLeft" | "bottomCenter" | "bottomRight";

export type ArenaLayoutContract = {
  battlefieldMinHeightRatio: number;
  handMaxViewportRatio: number;
  battlefieldSlots: ["leftSite", "leftLane", "rightLane", "rightSite"];
  desktopMinWidth: number;
  compactDesktopMinWidth: number;
  self: { runes: ArenaEdgePosition; home: ArenaEdgePosition; mainDeck: ArenaEdgePosition; hand: "bottomFan" };
  opponent: { runes: ArenaEdgePosition; home: ArenaEdgePosition; mainDeck: ArenaEdgePosition; hiddenHand: "topFan" };
};

export const ARENA_LAYOUT = WIRE_TABLE_LAYOUT.arena;
```

Add JSON values `0.65`, `0.18`, `1280`, and `900`, with the approved edge positions. Preserve existing semantic zone mappings while the old row values remain temporarily available to regression tests.

- [ ] **Step 4: Register and run the check**

Add `"check:arena-table-contract": "node scripts/check-arena-table-contract.mjs"` before `check:playable-surface` in the build chain.

Run: `npm --prefix src/Riftbound.DevUi run check:arena-table-contract`

Expected: `Arena table contract check passed.`

- [ ] **Step 5: Run adjacent layout gates**

Run: `npm --prefix src/Riftbound.DevUi run check:tabletop-layout && npm --prefix src/Riftbound.DevUi run check:wire-table-layout`

Expected: both checks pass without changing server zone semantics.

- [ ] **Step 6: Commit**

```bash
git add src/Riftbound.DevUi/package.json \
  src/Riftbound.DevUi/scripts/check-arena-table-contract.mjs \
  src/Riftbound.DevUi/src/components/match/wireTableLayout.ts \
  src/Riftbound.DevUi/src/components/match/wireTableLayoutData.json
git commit -m "Define the Web arena layout contract"
```

### Task 2: Full-Viewport Arena Composition

**Files:**
- Create: `src/Riftbound.DevUi/src/components/match/ArenaTable.tsx`
- Modify: `src/Riftbound.DevUi/src/components/match/PlayableMatchSurface.tsx`
- Modify: `src/Riftbound.DevUi/src/pages/MatchPage.tsx`
- Modify: `src/Riftbound.DevUi/scripts/check-playable-surface.mjs`

**Interfaces:**
- Consumes: the five existing React zone renderers and `ARENA_LAYOUT`.
- Produces: `ArenaTableProps`, `data-arena-table`, named arena slots, and a playable surface with overlay layers instead of a fixed dock row.

- [ ] **Step 1: Change the playable-surface gate to the desired DOM**

```js
requireText(arenaTableSource, "data-arena-table", "match must expose the arena table");
for (const slot of ["opponent-edge", "battlefield", "self-edge", "opponent-hand", "self-hand"]) {
  requireText(arenaTableSource, `data-arena-slot=\"${slot}\"`, `arena is missing ${slot}`);
}
rejectText(playableMatchSource, "data-game-action-dock", "fixed action dock must be removed");
requireText(playableMatchSource, "data-arena-action-layer", "context actions must remain inside an overlay layer");
```

- [ ] **Step 2: Run the gate and verify it fails**

Run: `npm --prefix src/Riftbound.DevUi run check:playable-surface`

Expected: failures for missing `ArenaTable` and the still-present fixed dock.

- [ ] **Step 3: Create the arena slot component**

```tsx
export type ArenaTableProps = {
  battlefield: ReactNode;
  opponentEdge: ReactNode;
  opponentHand: ReactNode;
  selfEdge: ReactNode;
  selfHand: ReactNode;
  opponentBackdrop?: string;
  selfBackdrop?: string;
};

export function ArenaTable(props: ArenaTableProps) {
  return (
    <section className="arena-table" data-arena-table>
      <div aria-hidden="true" className="arena-backdrop is-opponent" style={backdropStyle(props.opponentBackdrop)} />
      <div aria-hidden="true" className="arena-backdrop is-self" style={backdropStyle(props.selfBackdrop)} />
      <div className="arena-edge is-opponent" data-arena-slot="opponent-edge">{props.opponentEdge}</div>
      <div className="arena-hand is-opponent" data-arena-slot="opponent-hand">{props.opponentHand}</div>
      <div className="arena-battlefield" data-arena-battlefield-region data-arena-slot="battlefield">{props.battlefield}</div>
      <div className="arena-edge is-self" data-arena-slot="self-edge">{props.selfEdge}</div>
      <div className="arena-hand is-self" data-arena-hand data-arena-slot="self-hand">{props.selfHand}</div>
    </section>
  );
}
```

- [ ] **Step 4: Compose explicit arena slots in `MatchPage`**

Replace the row map with named render values for `opponentHand`, `opponentHome`, `battlefield`, `selfHome`, and `selfHand`. Pass those nodes to `ArenaTable`; do not change `buildWireTableViewModel`, `WireBattlefieldTable`, `WirePlayerHome`, or `WireHandRail` data inputs.

- [ ] **Step 5: Remove the layout-owned action dock**

Change `PlayableMatchSurfaceProps.actionDock` to `actionLayer`. Render it inside `game-table-stage` after the arena table:

```tsx
<section className="wire-table-shell game-table-stage" data-game-table tabIndex={0}>
  {table}
  <div className="arena-action-layer" data-arena-action-layer>{actionLayer}</div>
  {objectTray ? <div className="game-object-tray">{objectTray}</div> : null}
</section>
```

Remove the `game-action-dock` section entirely. Keep diagnostics as a closed details element until Task 5 converts it to the side drawer.

- [ ] **Step 6: Run source and type gates**

Run: `npm --prefix src/Riftbound.DevUi run check:playable-surface && npm --prefix src/Riftbound.DevUi run typecheck:strict`

Expected: both pass.

- [ ] **Step 7: Commit**

```bash
git add src/Riftbound.DevUi/scripts/check-playable-surface.mjs \
  src/Riftbound.DevUi/src/components/match/ArenaTable.tsx \
  src/Riftbound.DevUi/src/components/match/PlayableMatchSurface.tsx \
  src/Riftbound.DevUi/src/pages/MatchPage.tsx
git commit -m "Compose the Web match as a full arena"
```

### Task 3: Fan Hand And Stable Card Overflow

**Files:**
- Modify: `src/Riftbound.DevUi/src/components/cards/CardFace.tsx`
- Modify: `src/Riftbound.DevUi/src/components/match/wireCardFlow.tsx`
- Modify: `src/Riftbound.DevUi/src/pages/MatchPage.tsx`
- Create: `src/Riftbound.DevUi/scripts/check-arena-fan-hand.mjs`
- Modify: `src/Riftbound.DevUi/package.json`

**Interfaces:**
- Consumes: existing `WireCardFlow` IDs, objects, hidden-object fallback, selection, preview, and interaction maps.
- Produces: `presentation?: "rail" | "fan"`, per-card fan variables, and equivalent local/hidden fan DOM without identity leakage.

- [ ] **Step 1: Write the failing fan and hidden-information gate**

```js
requireText(flowSource, 'presentation?: "rail" | "fan"', "card flow must expose fan presentation");
requireText(flowSource, '"--arena-fan-index"', "fan cards need stable index geometry");
requireText(flowSource, '"--arena-fan-count"', "fan cards need total-count geometry");
requireText(matchSource, 'presentation="fan"', "both hand renderers must opt into fan presentation");
requireText(cardSource, "className", "CardFace must accept presentation classes");
requireText(cardSource, "style", "CardFace must accept fan CSS variables");
requireText(cardSource, 'cardAccessibilityLabel("未公开卡牌"', "hidden fans must retain neutral labels");
```

- [ ] **Step 2: Run the gate and verify it fails**

Run: `npm --prefix src/Riftbound.DevUi run check:arena-fan-hand`

Expected: non-zero exit for missing fan presentation.

- [ ] **Step 3: Extend `CardFace` without changing card semantics**

Add optional `className?: string` and `style?: CSSProperties` props. Append the class to all hidden, image, and fallback containers and pass the style directly to the same container. Do not pass a front image or title to a hidden card.

- [ ] **Step 4: Add fan geometry to `WireCardFlow`**

```ts
function fanCardStyle(index: number, count: number): WireCssProperties {
  return {
    "--arena-fan-index": index,
    "--arena-fan-count": count,
    "--arena-fan-center": (count - 1) / 2
  };
}
```

When `presentation === "fan"`, pass `className="arena-fan-card"` and the style above to every `CardFace`. Keep the existing object, spec, selection, preview, and prompt props unchanged.

- [ ] **Step 5: Use fan mode for both hands**

Pass `presentation="fan"` in `WireHandRail`; distinguish self and opponent only through the existing `hidden` flag and side classes.

- [ ] **Step 6: Run fan, information-boundary, and type gates**

Run: `npm --prefix src/Riftbound.DevUi run check:arena-fan-hand && npm --prefix src/Riftbound.DevUi run check:wire-information-boundary-plan && npm --prefix src/Riftbound.DevUi run typecheck:strict`

Expected: all pass.

- [ ] **Step 7: Commit**

```bash
git add src/Riftbound.DevUi/package.json \
  src/Riftbound.DevUi/scripts/check-arena-fan-hand.mjs \
  src/Riftbound.DevUi/src/components/cards/CardFace.tsx \
  src/Riftbound.DevUi/src/components/match/wireCardFlow.tsx \
  src/Riftbound.DevUi/src/pages/MatchPage.tsx
git commit -m "Add the arena fan hand presentation"
```

### Task 4: Contextual And Complex Prompt Layers

**Files:**
- Create: `src/Riftbound.DevUi/src/utils/arenaPromptPresentation.ts`
- Create: `src/Riftbound.DevUi/src/components/match/ArenaActionLayer.tsx`
- Modify: `src/Riftbound.DevUi/src/components/match/PlayableMatchSurface.tsx`
- Modify: `src/Riftbound.DevUi/src/pages/MatchPage.tsx`
- Create: `src/Riftbound.DevUi/scripts/check-arena-action-layer.mjs`
- Modify: `src/Riftbound.DevUi/package.json`

**Interfaces:**
- Consumes: `ActionPromptDto.view.type`, `selectedObjectId`, existing `ActionPanel`, and `WireObjectCommandTray`.
- Produces: `ArenaPromptPresentationMode = "hidden" | "context" | "modal"`, `buildArenaPromptPresentation`, and an action layer anchored to a selected table object when available.

- [ ] **Step 1: Write the failing presentation check**

```js
requireText(planSource, '"MULLIGAN"', "mulligan must use a modal layer");
requireText(planSource, '"ASSIGN_COMBAT_DAMAGE"', "damage assignment must use a modal layer");
requireText(planSource, '"ORDER_TRIGGERS"', "trigger ordering must use a modal layer");
requireText(layerSource, "data-arena-action-mode", "action layer must expose its presentation mode");
requireText(layerSource, "data-object-id", "context mode must anchor from the selected card DOM");
rejectText(surfaceSource, "game-action-dock", "legacy dock must not return");
```

- [ ] **Step 2: Run the check and verify it fails**

Run: `npm --prefix src/Riftbound.DevUi run check:arena-action-layer`

Expected: non-zero exit for missing plan and layer.

- [ ] **Step 3: Add a presentation-only prompt plan**

```ts
const MODAL_PROMPTS = new Set(["MULLIGAN", "ASSIGN_COMBAT_DAMAGE", "ORDER_TRIGGERS"]);

export function buildArenaPromptPresentation(input: {
  actionable: boolean;
  promptType?: string;
  selectedObjectId?: string;
}): ArenaPromptPresentationPlan {
  if (!input.actionable) return { mode: "hidden", anchorObjectId: undefined };
  if (input.promptType && MODAL_PROMPTS.has(input.promptType)) return { mode: "modal", anchorObjectId: undefined };
  return { mode: "context", anchorObjectId: input.selectedObjectId };
}
```

This utility controls presentation only; it must not inspect costs, card traits, zones, or legal rules.

- [ ] **Step 4: Implement the action-layer anchor**

`ArenaActionLayer` locates the visible `[data-object-id]` matching `anchorObjectId`, reads its bounding rectangle in `useLayoutEffect`, and exposes `--arena-action-x` and `--arena-action-y`. It recalculates on window resize. If the object is absent, context mode falls back to bottom-center above the hand. Modal mode uses a centered dialog-like region with an accessible label.

- [ ] **Step 5: Reuse existing command presenters**

Render the existing `ActionPanel presentation="play"` as the layer body. Render `WireObjectCommandTray` in the same context layer instead of as a separate layout row. Do not duplicate command construction or special prompt state.

- [ ] **Step 6: Run prompt-family regression gates**

Run: `npm --prefix src/Riftbound.DevUi run check:arena-action-layer && npm --prefix src/Riftbound.DevUi run check:action-panel-render-plan && npm --prefix src/Riftbound.DevUi run check:candidate-interaction-plan && npm --prefix src/Riftbound.DevUi run check:wire-object-command-tray-plan && npm --prefix src/Riftbound.DevUi run typecheck:strict`

Expected: all pass.

- [ ] **Step 7: Commit**

```bash
git add src/Riftbound.DevUi/package.json \
  src/Riftbound.DevUi/scripts/check-arena-action-layer.mjs \
  src/Riftbound.DevUi/src/components/match/ArenaActionLayer.tsx \
  src/Riftbound.DevUi/src/components/match/PlayableMatchSurface.tsx \
  src/Riftbound.DevUi/src/pages/MatchPage.tsx \
  src/Riftbound.DevUi/src/utils/arenaPromptPresentation.ts
git commit -m "Move server prompts into arena action layers"
```

### Task 5: Arena Styling, Backdrops, Side Drawer, And Mobile Layout

**Files:**
- Create: `src/Riftbound.DevUi/src/styles/arena-table.css`
- Modify: `src/Riftbound.DevUi/src/main.tsx`
- Modify: `src/Riftbound.DevUi/src/pages/MatchPage.tsx`
- Modify: `src/Riftbound.DevUi/src/components/match/PlayableMatchSurface.tsx`
- Create: `src/Riftbound.DevUi/scripts/check-arena-visual-contract.mjs`
- Modify: `src/Riftbound.DevUi/package.json`

**Interfaces:**
- Consumes: arena data markers, visible `WirePlayerEntry` champion/legend objects, `BehaviorSpec.frontImage`, and existing diagnostics content.
- Produces: final post-legacy arena CSS, viewer-safe background URLs, measurable geometry markers, a closed right drawer, and the independent narrow layout.

- [ ] **Step 1: Write the failing visual-contract check**

```js
requireText(mainSource, 'import "./styles/arena-table.css";', "arena CSS must load last");
requireText(styles, "min-height: 65%", "battlefield region must reserve 65% effective height");
requireText(styles, "max-height: 18dvh", "resting hand must remain under 18% viewport height");
requireText(styles, "@media (max-width: 899px)", "mobile must have an independent layout");
requireText(styles, ".arena-side-drawer", "diagnostics must use a right drawer");
rejectText(styles, "font-size: clamp(", "font size must not scale with viewport width");
```

- [ ] **Step 2: Run the check and verify it fails**

Run: `npm --prefix src/Riftbound.DevUi run check:arena-visual-contract`

Expected: non-zero exit for missing stylesheet.

- [ ] **Step 3: Derive viewer-safe backdrops**

Add a helper in `MatchPage` that searches only `entry.zones.championZone` and `entry.zones.legendZone`, resolves the already-visible object and its `BehaviorSpec.frontImage`, and returns `undefined` when unavailable or face-down. Pass the URLs to `ArenaTable`; do not inspect hidden hand or standby IDs.

- [ ] **Step 4: Implement desktop geometry in the final stylesheet**

Use one absolute/grid arena containing top/bottom edge rails, the 65% battlefield core, side battlefield cards, and bottom/top fan overlays. Override legacy `!important` declarations only under `.playable-match-surface:has([data-arena-table])` so lobby, room, result, and diagnostic routes are unaffected.

Use these state tokens exactly:

```css
--arena-table: #111615;
--arena-line: #56605a;
--arena-text: #f0eee7;
--arena-legal: #55c89b;
--arena-selected: #d5ae62;
--arena-danger: #e46c72;
```

Backdrop layers are desaturated and darkened with pseudo-element masks. Official card fronts remain unfiltered.

- [ ] **Step 5: Implement hand and overflow motion**

Use `--arena-fan-index`, `--arena-fan-center`, and stable card dimensions to compute overlap, rotation, and vertical arc. Hover/focus/selection raises the card above siblings without changing container size. Respect `prefers-reduced-motion` by removing transforms that animate over time while preserving final selected geometry.

- [ ] **Step 6: Convert diagnostics to an on-demand drawer**

Keep the existing `<details>` semantics, but style it as a right overlay with a compact icon/summary trigger. Closed state reserves no arena width. Open state traps no pointer input outside the drawer and remains dismissible with Escape through native details behavior or the existing close handler.

- [ ] **Step 7: Add compact desktop and mobile layouts**

At `900px–1279px`, reduce edge-rail width and increase card overlap without shrinking text. At `max-width: 899px`, use a vertical layout with an opponent summary, horizontally scrollable/snap-aligned battlefield lanes, compact edge resources, and an expandable bottom fan. Keep the same DOM, candidate model, and hidden-card component.

- [ ] **Step 8: Run visual, text, and type gates**

Run: `npm --prefix src/Riftbound.DevUi run check:arena-visual-contract && npm --prefix src/Riftbound.DevUi run check:user-facing-text && npm --prefix src/Riftbound.DevUi run typecheck:strict`

Expected: all pass.

- [ ] **Step 9: Commit**

```bash
git add src/Riftbound.DevUi/package.json \
  src/Riftbound.DevUi/scripts/check-arena-visual-contract.mjs \
  src/Riftbound.DevUi/src/components/match/PlayableMatchSurface.tsx \
  src/Riftbound.DevUi/src/main.tsx \
  src/Riftbound.DevUi/src/pages/MatchPage.tsx \
  src/Riftbound.DevUi/src/styles/arena-table.css
git commit -m "Style the Web match as a layered card arena"
```

### Task 6: Browser Geometry, Accessibility, And Interaction QA

**Files:**
- Modify: `src/Riftbound.DevUi/scripts/chrome-smoke.mjs`
- Modify: `src/Riftbound.DevUi/scripts/playwright-qa.mjs`
- Modify: `src/Riftbound.DevUi/scripts/check-playwright-qa-match-state-surface.mjs`

**Interfaces:**
- Consumes: final arena DOM markers and seeded `midgame-showcase` / prompt fixtures.
- Produces: measured ratio, overflow, occlusion, hidden-information, focus, mobile, and screenshot evidence.

- [ ] **Step 1: Change smoke assertions before styling is considered complete**

```js
const stage = document.querySelector("[data-game-table]")?.getBoundingClientRect();
const battlefield = document.querySelector("[data-arena-battlefield-region]")?.getBoundingClientRect();
const hand = document.querySelector("[data-arena-hand]")?.getBoundingClientRect();
return {
  battlefieldHeightRatio: stage && battlefield ? battlefield.height / stage.height : 0,
  documentOverflowY: document.documentElement.scrollHeight > window.innerHeight + 1,
  hasFixedDock: Boolean(document.querySelector("[data-game-action-dock]")),
  handViewportRatio: hand ? hand.height / window.innerHeight : 1
};
```

For 1440x900 and 1920x1080, fail below `0.65` battlefield ratio or above `0.18` hand ratio. Fail any desktop viewport with a fixed dock or document overflow.

- [ ] **Step 2: Add legal-target occlusion and hidden-hand checks**

Collect prompt-enabled objects outside `[data-arena-hand]` and assert their centers do not fall inside the local hand rectangle. Assert opponent hand card count equals card-back count, with zero `.card-full-image` descendants and no identity-bearing accessible name.

- [ ] **Step 3: Add desktop and mobile QA shots**

Capture seeded arena states at 1920x1080, 1440x900, 1280x720, and 390x844. On mobile, assert the active lane and action layer are reachable without horizontal document overflow. Keep axe checks on lobby, desktop match, and mobile match.

- [ ] **Step 4: Add a direct-selection interaction**

Using fixture candidates, click one unique source card, assert `[data-selected="true"]`, click one highlighted target, and assert the contextual action layer reflects the advanced draft. Press Escape and verify selection/action context closes without changing the authoritative fixture snapshot.

- [ ] **Step 5: Run targeted QA**

Run: `npm --prefix src/Riftbound.DevUi run check:playwright-qa-match-state-surface && npm --prefix src/Riftbound.DevUi run qa:appshots`

Expected: all arena screenshots and accessibility audits pass.

- [ ] **Step 6: Run Chrome smoke**

Run: `npm --prefix src/Riftbound.DevUi run smoke:chrome -- --start-api`

Expected: all routes, four match viewports, hidden-information checks, card inspection, direct selection, and geometry assertions pass with no browser errors.

- [ ] **Step 7: Commit**

```bash
git add src/Riftbound.DevUi/scripts/check-playwright-qa-match-state-surface.mjs \
  src/Riftbound.DevUi/scripts/chrome-smoke.mjs \
  src/Riftbound.DevUi/scripts/playwright-qa.mjs
git commit -m "Verify the Web arena across supported viewports"
```

### Task 7: Visible Two-Client Flow, Evidence, Documentation, And Push

**Files:**
- Create: `src/Riftbound.DevUi/artifacts/web-arena/README.md`
- Create: `src/Riftbound.DevUi/artifacts/web-arena/*.png`
- Modify: `src/Riftbound.DevUi/README.md`
- Modify: `docs/superpowers/plans/2026-07-11-web-arena-table-redesign.md`

**Interfaces:**
- Consumes: memory-mode API, two visible Web sessions, preconstructed decks, and the completed arena.
- Produces: visible evidence for lobby, mulligan, main action, complex prompt, and result; reproducible run instructions; pushed `main`.

- [ ] **Step 1: Start the real local stack**

Run API:

```bash
ASPNETCORE_ENVIRONMENT=Development \
ASPNETCORE_URLS=http://127.0.0.1:5088 \
ConnectionStrings__Riftbound='' \
~/.dotnet/dotnet run --project src/Riftbound.Api
```

Run Web client on the first free Vite port beginning at 5173:

```bash
npm --prefix src/Riftbound.DevUi run dev -- --host 127.0.0.1
```

- [ ] **Step 2: Open two visible isolated Web sessions**

Use separate browser contexts so player keys and hidden snapshots cannot cross. Create/join one private room, choose preconstructed decks, submit, ready, and complete mulligan. Do not use DOM state from one context to populate the other.

- [ ] **Step 3: Exercise the arena flow**

Complete at least one rune action, card play, movement or battle declaration, target/cost selection, one complex prompt when available, pass/response, and authoritative result. If a natural run does not produce a specific complex prompt, use the server's existing focused seeded scenario in a separate visible session and label it as scenario evidence rather than full-match evidence.

- [ ] **Step 4: Capture and inspect evidence**

Save accepted visible screenshots as:

```text
src/Riftbound.DevUi/artifacts/web-arena/01-lobby.png
src/Riftbound.DevUi/artifacts/web-arena/02-mulligan.png
src/Riftbound.DevUi/artifacts/web-arena/03-main-action.png
src/Riftbound.DevUi/artifacts/web-arena/04-complex-prompt.png
src/Riftbound.DevUi/artifacts/web-arena/05-result.png
```

Inspect each file and reject blank, loading, clipped, wrong-window, or identity-leaking captures. Record viewport, room/scenario, commit, expected visible state, and hidden-information result in the artifact README.

- [ ] **Step 5: Run the complete verification set**

Run:

```bash
npm --prefix src/Riftbound.DevUi run build
npm --prefix src/Riftbound.DevUi run qa:appshots
npm --prefix src/Riftbound.DevUi run smoke:chrome -- --start-api
git diff --check
```

Expected: all commands exit 0; browser logs contain no uncaught exception; final screenshot review finds no clipping or hidden face.

- [ ] **Step 6: Update documentation and plan status**

Document the Web URL, API command, supported viewports, direct-selection behavior, side drawer, evidence paths, and the fact that Godot parity is outside this goal. Mark every completed checkbox in this plan.

- [ ] **Step 7: Commit**

```bash
git add src/Riftbound.DevUi/README.md \
  src/Riftbound.DevUi/artifacts/web-arena \
  docs/superpowers/plans/2026-07-11-web-arena-table-redesign.md
git commit -m "Archive the playable Web arena evidence"
```

- [ ] **Step 8: Rebase and push**

```bash
git fetch origin main
git rebase origin/main
git push origin main
git status --short
```

Expected: push succeeds, `HEAD` equals `origin/main`, and status is empty.
