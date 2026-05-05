# Current P7.9 Status

Last updated: 2026-05-05

P7.9 working objective:

> 完成 P7.9 本地产品版全卡可玩：在 P7 产品级 Web 对战基础上，补齐全部卡牌规则能力并让页面可操作；实现精美 UI、完整卡牌对战流程、结构化 ActionPrompt、点击式出牌/目标/费用/战斗/传奇/战场操作、全卡图鉴详情和本地回放/观战体验；关闭 P6 的 manual deferred 缺口，所有能力以后端权威规则和 prompt 为准，前端不裁决规则、不持有权威状态；同步所有相关状态文档，保持后端 full test、focused suites、前端 build、Browser smoke 和 clean status 全绿。

Execution note:

- The previous thread-level P7 goal is already marked `complete`, but the current goal tool slot is still occupied by that completed goal and rejected creation of a second goal in this same thread.
- This file is the authoritative P7.9 tracker for the current thread, together with the incremental plan, commits, validation records, and progress reports.

## Baseline Confirmation

- P7 final commit: `d415356 test: complete p7 final audit`
- Expected dirty state at P7.9 start: only untracked `riftbound-dotnet.sln`
- P7 final validation:
  - `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed
  - `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2613/2613`
  - `ConformanceFixtureRunnerTests`: passed `2507/2507`
  - `CardCatalogBaselineTests`: passed `37/37`
  - `GameHubJoinTests`: passed `27/27`
  - `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed
  - P7 final Browser smoke room `p7-final-1777957045775`: passed create/join/ready/play/pass/end-turn/battle/reconnect
  - `git diff --check`: passed
- P6 final card status:
  - `1009/1009` official entries have explicit status
  - `811/811` functional units have implementation or explicit deferred coverage
  - `713/811 = 87.9%` functional units implemented
  - `98/811 = 12.1%` functional units manual deferred
  - `0/811 = 0.0%` unimplemented
- Remaining P6 manual domains:
  - `传奇`: `44` functional units / `106` entries
  - `战场`: `54` functional units / `57` entries

## P7.9 Scope

In scope:

- Product-grade local Web battle experience, not production accounts or deployment.
- More polished desktop UI for room entry, battle table, card objects, command panel, prompt status, card details, event log, report, replay boundary, and spectator boundary.
- Click-first operation flow for play, pass, end turn, move, battle declaration, target selection, optional costs, response windows, equipment, legend actions, and battlefield actions.
- Structured server prompt contract that exposes legal action candidates, legal sources, targets, destinations, optional costs, modes, and prompt/snapshot versions.
- Full implementation plan for every remaining manual functional unit, especially legends and battlefields.
- Browser smoke for every significant UI/flow batch.
- Conformance and focused tests for every rule batch.
- Documentation synchronization after each batch.

Out of scope:

- P8 production accounts, matchmaking, ranking, deployment, monitoring, risk control, and production security systems.
- Complex AI.
- Mobile-specific adaptation.
- Submitting or committing rules PDF/FAQ files.
- Committing untracked `riftbound-dotnet.sln` unless explicitly requested.
- Frontend rule adjudication or hidden client-side authority.

## Non-Negotiable Principles

- The frontend only submits player intent.
- The backend remains the only rule authority.
- UI buttons and choices must come from server prompt data.
- No card ability can be shown as playable before backend implementation and conformance coverage exist.
- Development helpers may remain, but must be hidden behind an explicit local development surface and never be confused with the product path.
- Every batch ends with updated status, validation notes, and a commit.

## Gap Audit

### Rule Gaps

The hard blocker for "all cards fully playable" is not the P7 UI. It is the remaining P6 manual domain boundary:

| Domain | Functional units | Entries | P7 disposition | P7.9 target |
| --- | ---: | ---: | --- | --- |
| Legends | 0 remaining | 0 remaining | P7.9.6 implemented all `44/44` legend FUs / `106/106` entries | Keep prompt/UI operation coverage green |
| Battlefields | 17 remaining | 18 remaining | P7.9.7 has migrated `37/54` battlefield FUs / `39/57` entries | Implement battlefield control, hold/conquer/scoring effects, battlefield triggers/static effects, prompt exposure, conformance, UI operation |

Related rule surfaces to re-check while closing the manual domains:

- Multi-attacker and multi-defender battle declaration.
- Battle damage assignment and damage prevention/replacement.
- Conquest, holding a battlefield, battlefield control changes, and scoring.
- Trigger ordering and simultaneous battlefield/legend effects.
- Token battlefield objects and token copy boundaries.
- Equipment/control interactions during movement, battle, and zone changes.
- Hidden information and old snapshot submission boundaries.

### Protocol Gaps

Current `ActionPromptDto` exposes:

- `playerId`
- `actionable`
- `reason`
- `actions: string[]`

P7.9 needs a backward-compatible structured layer:

- `promptId` or prompt version
- `snapshotTick` / `eventSequence`
- legal action candidates
- legal source object ids
- legal target object ids and target groups
- legal destinations
- legal modes
- legal optional costs and payment hints
- disabled/deferred action candidates with backend reasons
- stale prompt rejection semantics

The existing `actions` list must remain until conformance fixtures and UI are migrated.

### UI Gaps

The P7 UI is usable, but P7.9 needs to remove remaining product friction:

- Hide raw JSON and fixture tooling by default.
- Convert object-id text entry into click-to-select operations.
- Add card hover/detail, keyword explanation, richer status badges, and clearer action summaries.
- Keep card and board dimensions stable at desktop widths.
- Improve event log wording from raw machine events into player-readable match history.
- Make reconnect recovery and room identity obvious.
- Make catalog filters and card status clear enough to explain why a card is playable or not.

## Optimization Backlog

| Area | Optimization |
| --- | --- |
| UI visual design | Stronger table composition, card elevation, readable typography, restrained palette, polished empty/loading/error states |
| Card interaction | Hover zoom, details drawer, click-to-source, click-to-target, selected-object path, cancel/reselect |
| Prompt UX | Current actor, current phase, priority/focus player, waiting reason, stale prompt warnings |
| Payment UX | Optional cost chips, resource preview from server hints, disabled costs with reasons |
| Combat UX | Attacker/defender assignment panel, battlefield lane focus, battle summary before submit |
| Legend UX | Legend zone actions, passive labels, identity effects, `LEGEND_ACT` button candidates |
| Battlefield UX | Battlefield control marker, hold/conquer state, battlefield effect detail panel |
| Event log | Human-readable labels, filters, event grouping, turn markers, report summary |
| Replay/spectator | Local replay stepping from event stream; spectator stays read-only until production persistence exists |
| Catalog | Search/filter/sort, implementation status, rule-domain tags, playable path indicator |
| Dev tools | Collapsible local-only drawer for seeds, raw JSON, fixture draft, protocol payloads |
| Quality | Browser smoke script notes, focused conformance, prompt contract regression tests |

## P7.9 Batch Plan

| Batch | Status | Target | Gate |
| --- | --- | --- | --- |
| P7.9.0 | Done | Audit, current status file, `START_HERE`, README, and master plan synchronization. | `git diff --check`; docs-only commit. |
| P7.9.1 | Done | Backward-compatible structured `ActionPrompt` contract design and first DTO/test slice. | Build + prompt serialization tests. |
| P7.9.2 | Done | Product operation shell: hide dev tools by default, click-to-source/target scaffolding, action summary. | Frontend build + Browser smoke. |
| P7.9.3 | Done | Structured prompt candidates for core actions: ready, pass, end turn, play card, move, assemble, battle. | Focused GameHub tests + Browser smoke. |
| P7.9.4 | Done | Click-first cost, target, response-window, and battle declaration flow from prompt candidates. | Browser smoke: play, target, cost, pass, battle. |
| P7.9.5 | Done | Legend domain foundation: `LEGEND_ACT` command contract, blocked-to-implemented migration path, representative conformance. | Focused conformance + GameHub tests. |
| P7.9.6 | Done | Legend functional-unit batches complete. Active/reaction, automatic-trigger/replacement, and static slices migrated `44/44` legend FUs. | Functional-unit coverage tests. |
| P7.9.7 | In progress | Battlefield domain foundation: battlefield object destinations, hold/conquer/static/resource-token event model, selected battlefield targets, top-deck reveal branches, score/rune statics, turn-start damage/destroy-draw, hero-zone return, static movement restrictions/roam, movement-trigger power, unit-play restrictions, echo/equipment cost reductions, and representative effects. Battlefield slices migrated `37/54` battlefield FUs. | Focused conformance + GameHub tests. |
| P7.9.8 | Planned | Battlefield functional-unit batches until all remaining `17` battlefield units are implemented or split into smaller committed slices. | Functional-unit coverage tests. |
| P7.9.9 | Planned | Combat completeness pass: multi-unit battles, damage assignment, scoring, conquest/hold triggers, UI operation. | Conformance + Browser smoke. |
| P7.9.10 | Planned | Full-card catalog and page operation integration: no playable card hidden by manual/deferred status. | `CardCatalogBaselineTests` updated and green. |
| P7.9.11 | Planned | Visual polish, event report, local replay/spectator read-only boundary, accessibility and keyboard/mouse pass. | Frontend build + Browser visual smoke. |
| P7.9.x | Planned | Final audit: `811/811` functional units implemented, no manual deferred, full tests, Browser smoke, clean status. | Full final validation and commit. |

Initial estimate: `13` top-level batches, with P7.9.6 and P7.9.8 likely split into additional rule sub-batches if the legend/battlefield surfaces are not homogeneous enough for safe large commits.

## Validation Policy

Every .NET command must be run through:

```bash
source scripts/dev-env.sh
```

Routine gates:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`
- Browser smoke after significant frontend batches
- `git diff --check`
- After each batch commit, `git status --short` should show only `?? riftbound-dotnet.sln`

Final P7.9 gate:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`
- Browser smoke:
  1. Open local Web URL.
  2. Create/join room.
  3. Ready both players.
  4. Execute at least play card, pass, end turn, move or battle declaration, legend action, battlefield action, and reconnect.
  5. Record URL, roomId, operation path, event log summary, and final snapshot summary in this file.
- `git diff --check`
- `git status --short` only shows `?? riftbound-dotnet.sln`

## Current Progress

- P7.9.0 status: done.
- P7.9.1 status: done.
- P7.9.2 status: done.
- P7.9.3 status: done.
- P7.9.4 status: done.
- P7.9.5 status: done.
- P7.9.6 status: done.
- P7.9.7 status: in progress; battlefield foundation slices 1-36 done.
- P7.9.6 active-ability slices: `10` done.
- P7.9.6 automatic-trigger/replacement slices: `17` done.
- P7.9.6 static legend slices: `6` done.
- P7.9.7 battlefield foundation slices: `36` done.
- Current functional-unit implementation: `794/811 = 97.9%`.
- Current manual deferred boundary: `17/811 = 2.1%`.
- Remaining manual domains:
  - `战场`: `17` functional units / `18` entries
- Overall P7.9 progress: `7/13 top-level batches = 53.8%`; P7.9.6 legend domain is complete at `44/44` functional units / `106/106` entries.
- Estimated remaining top-level batches: `6`.

## P7.9.0 Delivered

- Added this P7.9 status file with baseline, scope, gap audit, optimization backlog, batch plan, validation policy, and progress tracking.
- Synchronized `docs/START_HERE.md` so new windows start from P7 complete and P7.9 active instead of stale P5 guidance.
- Synchronized `README.md` so current status, local UI notes, and handoff order point at P7.9/P7/P6.
- Added P7.9 to `docs/master-development-plan.md` and updated the immediate execution order to stop before P8.

P7.9.0 validation:

- `git diff --check`: passed.

## P7.9.1 Delivered

- Added a backward-compatible structured prompt layer:
  - `ActionPromptDto.promptId`
  - `ActionPromptDto.snapshotTick`
  - `ActionPromptDto.candidates`
  - `ActionPromptCandidateDto`
  - `ActionPromptChoiceDto`
- Kept the existing `actions: string[]` contract intact for conformance fixtures and current UI compatibility.
- Added `ActionPromptBuilder` so current prompts automatically expose a structured candidate per action with localized labels and enabled/disabled state.
- Populated structured candidates for ready, wait, pass, end turn, play card, move, assemble, battle, legend, and other current action ids.
- Updated the React DTO type so the UI can start consuming prompt candidates in later P7.9 batches.
- Added GameHub assertions that prompt payloads include prompt id, snapshot tick, enabled `READY`/`PASS_PRIORITY` candidates, and disabled `WAIT` candidates.

P7.9.1 validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `27/27`.

## P7.9.2 Delivered

- Added a product operation shell on top of the P7 workbench:
  - desktop click mode selector for play source, play target, move source, assemble source, assemble target, battle attacker, and battle defender
  - selected object highlighting on the battle desk
  - intent summary panel for the currently drafted `PLAY_CARD`, `MOVE_UNIT`, `ASSEMBLE_EQUIPMENT`, and `DECLARE_BATTLE` commands
  - prompt candidate labels in command chips instead of raw action ids where structured prompt data is available
- Added a top-level `开发工具` toggle.
- Hid scenario seeds, fixture draft, raw `SubmitIntent JSON`, and debug JSON panels by default.
- Kept all operation drafting as client intent assembly only; no frontend legality rules were introduced.

P7.9.2 validation:

- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- Browser smoke:
  - Web URL: `http://127.0.0.1:5173/`
  - Temporary current-code API URL: `http://127.0.0.1:5089`
  - Room ID: `p7-1777959564489`
  - Operation path: reload Web URL -> set server URL to `5089` -> `new-room` -> `join-both` -> `ready-both` -> open dev tools -> `seed-basic-play`
  - UI summary: `桌面点击模式` and `待提交操作` visible; `Scenario Seeds`, `Fixture Draft`, and raw JSON hidden before opening dev tools; debug prompt contains `promptId`, `snapshotTick`, and `candidates`.

## P7.9.3 Delivered

- Expanded structured prompt candidates with backend-generated choice hints for current core actions:
  - `PLAY_CARD`: hand source candidates, board target candidates, base/battlefield destinations, common mode hints, optional cost hints
  - `MOVE_UNIT`: controlled unit sources and movement destinations
  - `ASSEMBLE_EQUIPMENT`: controlled equipment sources, controlled unit host targets, assemble cost hint
  - `DECLARE_BATTLE`: controlled battlefield attacker sources, opposing defender targets, battlefield destinations, combat assignment cost hint
- Kept the existing `actions` list intact.
- Added metadata policies to make clear that prompt choices are server-provided hints and final legality is still validated on submit.
- Added a Web panel that shows structured server candidates in player-readable groups.
- Added GameHub assertions for seeded prompt candidate sources, destinations, and metadata.

P7.9.3 validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `27/27`.
- Browser smoke:
  - Web URL: `http://127.0.0.1:5173/`
  - Temporary current-code API URL: `http://127.0.0.1:5089`
  - Room ID: `p7-1777959880566`
  - Operation path: reload Web URL -> set server URL to `5089` -> `new-room` -> `join-both` -> `ready-both` -> open dev tools -> `seed-basic-play`
  - UI summary: `服务端候选` showed `PLAY_CARD` with source `P1-UNIT-MIGHTY-FAERIE`, destinations `基地`/`己方主战场`, modes, and optional costs; debug prompt JSON included `sources`, `destinations`, and `optionalCosts`.

## P7.9.4 Delivered

- Wired structured prompt choices into the product operation builders:
  - `PLAY_CARD` source, destination, mode, target, and optional-cost chips
  - `MOVE_UNIT` source and destination chips
  - `ASSEMBLE_EQUIPMENT` equipment source, host target, and cost chips
  - `DECLARE_BATTLE` battlefield, attacker, defender, and cost chips
- Added stable `data-testid` values for prompt choice chips so Browser smoke can target repeated labels reliably.
- Kept all chips as intent-draft helpers; final legality still comes from server submission.

P7.9.4 validation:

- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- Browser smoke:
  - Web URL: `http://127.0.0.1:5173/`
  - Temporary current-code API URL: `http://127.0.0.1:5089`
  - Room ID: `p7-1777960124126`
  - Operation path: reload Web URL -> set server URL to `5089` -> `new-room` -> `join-both` -> `ready-both` -> open dev tools -> `seed-basic-play` -> click `play-source-choice-P1-UNIT-MIGHTY-FAERIE` -> click `play-destination-choice-BASE` -> click `cost-echo` -> clear optional cost -> submit `PLAY_CARD`
  - UI summary: source field became `P1-UNIT-MIGHTY-FAERIE`, destination field became `BASE`, optional cost field became `ECHO` before clearing, and event log included `CARD_PLAYED`, `COST_PAID`, and `STACK_ITEM_ADDED`.

## P7.9.5 Delivered

- Added the first server-authoritative legend action command:
  - `LegendActCommand`
  - JSON mapper support for `cmdType: "LEGEND_ACT"`
  - `CoreRuleEngine.ResolveLegendAct`
- Implemented the first representative legend ability path for Poppy (`UNL-237/219`):
  - requires active player's main open window
  - requires source in the acting player's legend zone
  - requires `abilityId = LEGEND_SPEND_3_EXPERIENCE_EXHAUST_DRAW`
  - requires `optionalCosts = ["SPEND_EXPERIENCE:3"]`
  - spends 3 experience, exhausts the legend, and draws 1 card
  - emits `LEGEND_ABILITY_ACTIVATED`, `EXPERIENCE_SPENT`, `LEGEND_EXHAUSTED`, and `CARD_DRAWN`
- Added structured `LEGEND_ACT` prompt choices:
  - implemented legend source candidates
  - ability choice
  - required experience cost hint
  - metadata policy explaining the server-side validation boundary
- Added a `legend-act` development seed with P1 Poppy, 3 experience, and a known card to draw.
- Added a product workbench `LEGEND_ACT` panel with source, ability, target, and cost fields plus server-provided choice chips.
- Added click-mode selection support for legend sources.
- Added representative tests:
  - direct rule/conformance tests for successful legend action and insufficient-experience rejection without side effects
  - GameHub seed/prompt/submit/snapshot coverage for online `LEGEND_ACT`
- Kept the old P6 deferred `ACTIVATE_ABILITY` boundary intact. This batch establishes the new command path and one implemented representative; it does not yet close all `44/44` legend functional units.

P7.9.5 validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2509/2509`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `28/28`.
- `git diff --check`: passed.
- Browser smoke:
  - Web URL: `http://127.0.0.1:5173/`
  - Temporary current-code API URL: `http://127.0.0.1:5089`
  - Room ID: `p7-1777961365175`
  - Operation path: reload Web URL -> set server URL to `5089` -> `new-room` -> `双人入座` -> second `双人入座` retry for P2 seating -> `双方准备` -> open dev tools -> `Legend Act` seed -> click legend source `UNL-237/219 / P1-LEGEND-POPPY` -> click ability `花费 3 经验并横置：抽 1 张` -> click cost `支付 3 经验` -> submit `LEGEND_ACT`
  - Event log summary: `LEGEND_ABILITY_ACTIVATED`, `EXPERIENCE_SPENT`, `LEGEND_EXHAUSTED`, `CARD_DRAWN`
  - Final snapshot summary: P1 experience `0`; `P1-LEGEND-POPPY` exhausted; P1 hand contains `P1-LEGEND-DRAW-001`; P1 main deck count `0`
  - Browser screenshot capture was attempted through the in-app Browser API, but the Browser CDP `Page.captureScreenshot` command timed out repeatedly. DOM/state smoke still verified the visible operation path and final state.

## P7.9.6 Active-Ability Slice Delivered

This is the first committed rule slice inside the broader P7.9.6 legend-domain batch. P7.9.6 remains in progress until all remaining legend functional units are closed.

- Expanded `LEGEND_ACT` from the single Poppy representative into a reusable legend active-ability path.
- Added implemented server-authoritative abilities:
  - Yasuo / 疾风剑豪: pay `2` mana, exhaust legend, move a controlled unit between battlefield and base.
  - Lee Sin / 盲僧: pay `1` mana, exhaust legend, grant boon to a controlled unit.
  - Poppy / 圣锤之毅: spend `3` experience, exhaust legend, draw `1`.
  - Viktor / 奥术先驱: pay `1` mana, exhaust legend, create a `1` power unit token in base.
- Accepted same-functional-unit legend reprints/variants for those abilities:
  - `FND-259/298`, `OGN·259/298`, `OGN·305*/298`, `OGN·305/298`
  - `OGN·257/298`, `OGN·304*/298`, `OGN·304/298`
  - `UNL-203/219`, `UNL-237*/219`, `UNL-237/219`
  - `FND-265/298`, `OGN·265/298`, `OGN·308*/298`, `OGN·308/298`
- Added `legend-active-actions` dev seed and UI defaults so the product workbench can exercise the active legend path from server prompt choices.
- Added server prompt candidates for implemented legend sources, ability modes, controlled-unit targets, and required mana/experience costs.
- Migrated the first legend action domain slice in `BehaviorSpec`:
  - Implemented functional units: `717/811`
  - Manual deferred functional units: `94/811`
  - Implemented official entries: `860/1009`
  - Manual deferred official entries: `149/1009`
  - Legend action domain implemented: `4` functional units / `14` entries
  - Remaining legend manual deferred: `40` functional units / `92` entries
  - Remaining battlefield manual deferred: `54` functional units / `57` entries

P7.9.6 active-ability validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2513/2513`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `28/28`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2620/2620`.
- `git diff --check`: passed.
- Browser smoke:
  - Web URL: `http://127.0.0.1:5173/`
  - Temporary current-code API URL: `http://127.0.0.1:5089`
  - Room ID: `p7-9-legend-active-1777962540842`
  - Operation path: reload Web URL -> set server URL to `5089` -> set room id -> `join-both` -> `ready-both` -> open dev tools -> `Legend Actions` seed -> submit default Yasuo `LEGEND_ACT`
  - Event log summary: `LEGEND_ABILITY_ACTIVATED`, `COST_PAID`, `LEGEND_EXHAUSTED`, `UNIT_MOVED_TO_BASE`
  - Final snapshot summary: tick `906`; P1 active; P1 base count `2`; P1 battlefield count `0`; `P1-LEGEND-BATTLEFIELD-UNIT` moved into base; match remains `IN_PROGRESS`
  - Screenshot note: Browser operation path and DOM/state verification passed. The in-app Browser screenshot APIs (`playwright.screenshot` and `cua.get_visible_screenshot`) timed out in this environment, matching the prior P7.9.5 screenshot limitation.

## P7.9.6 Active-Ability Slice 2 Delivered

This is the second committed rule slice inside P7.9.6. It keeps using the same `LEGEND_ACT` server-authoritative command and expands the implemented legend action domain without adding frontend legality rules.

- Added implemented server-authoritative abilities:
  - Miss Fortune / 赏金猎人: exhaust legend, give a controlled unit `ROAM` until end of turn.
  - Kha'Zix / 虚空掠夺者: spend `1` experience and exhaust legend to grant boon to a controlled unit.
  - Kha'Zix / 虚空掠夺者: spend `2` experience and exhaust legend to move an exhausted controlled battlefield unit to base.
  - Pyke / 血港鬼影: pay `1` mana and exhaust legend to return a controlled battlefield unit to its owner's hand, then create an exhausted coin equipment token in base.
- Accepted same-functional-unit legend reprints/variants for those abilities:
  - `OGN·267/298`, `OGN·309*/298`, `OGN·309/298`
  - `UNL-201/219`, `UNL-236*/219`, `UNL-236/219`
  - `UNL-185/219`, `UNL-228*/219`, `UNL-228/219`
- Expanded `legend-active-actions` dev seed with Miss Fortune, Kha'Zix, Pyke, and an exhausted battlefield unit target.
- Expanded `LEGEND_ACT` prompt modes and cost choices for the new abilities.
- Migrated the second legend action domain slice in `BehaviorSpec`:
  - Implemented functional units: `721/811`
  - Manual deferred functional units: `90/811`
  - Implemented official entries: `869/1009`
  - Manual deferred official entries: `140/1009`
  - Legend action domain implemented: `8` functional units / `23` entries
  - Remaining legend manual deferred: `36` functional units / `83` entries
  - Remaining battlefield manual deferred: `54` functional units / `57` entries

P7.9.6 active-ability slice 2 validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2517/2517`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `28/28`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2624/2624`.
- `git diff --check`: passed.
- Browser smoke:
  - Web URL: `http://127.0.0.1:5173/`
  - Temporary current-code API URL: `http://127.0.0.1:5089`
  - Room ID: `p7-9-legend-pyke-clean-1777963470048`
  - Operation path: reload Web URL -> set server URL to `5089` -> set room id -> `join-both` -> `ready-both` -> open dev tools -> `Legend Actions` seed -> choose source `P1-LEGEND-PYKE` -> choose ability `LEGEND_PAY_1_EXHAUST_RECALL_BATTLEFIELD_UNIT_CREATE_COIN` -> choose cost `SPEND_MANA:1` -> submit `LEGEND_ACT`
  - Event log summary: `COST_PAID`, `LEGEND_ABILITY_ACTIVATED`, `LEGEND_EXHAUSTED`, `UNIT_RETURNED_TO_HAND`, `EQUIPMENT_TOKEN_CREATED`
  - Final snapshot summary: match `IN_PROGRESS`; tick `906`; P1 hand count `1`; P1 base count `2`; P1 battlefield count `1`; `P1-LEGEND-PYKE-TOKEN-001` created in base; `P1-LEGEND-BATTLEFIELD-UNIT` returned to hand.

## P7.9.6 Automatic-Trigger Slice Delivered

This is the third committed rule slice inside P7.9.6. It starts closing legend passive/trigger surfaces that do not require a frontend button, while still keeping all authority in `CoreRuleEngine`.

- Added Jinx / 暴走萝莉 turn-start legend trigger:
  - after the normal turn-start draw, if the active player's hand is still below `2`, Jinx draws `1` additional card
  - emits `LEGEND_TRIGGER_RESOLVED` before the extra draw event
  - preserves burnout/winner handling by skipping the trigger when the normal draw already ended the match
- Accepted same-functional-unit legend reprints/variants for this trigger:
  - `FND-251/298`
  - `OGN·251/298`
  - `OGN·301*/298`
  - `OGN·301/298`
- Migrated this legend trigger slice in `BehaviorSpec`:
  - Implemented functional units: `722/811`
  - Manual deferred functional units: `89/811`
  - Implemented official entries: `873/1009`
  - Manual deferred official entries: `136/1009`
  - Legend rule-domain implemented: `9` functional units / `27` entries
  - Remaining legend manual deferred: `35` functional units / `79` entries
  - Remaining battlefield manual deferred: `54` functional units / `57` entries

P7.9.6 automatic-trigger validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests|FullyQualifiedName~P79LegendTriggerJinxDrawsAtTurnStartWhenHandBelowTwo"`: passed `38/38`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2518/2518`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `28/28`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2625/2625`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this rule-only slice because Jinx is an automatic turn-start backend trigger and this batch introduced no new frontend operation path. The next significant UI/flow batch must run Browser smoke again.

## P7.9.6 Active-Ability Slice 3 Delivered

This is the fourth committed rule slice inside P7.9.6. It keeps the `LEGEND_ACT` path server-authoritative and adds a dynamic-cost zero-target legend action.

- Added Lillia / 含羞蓓蕾 legend active ability:
  - pays base `4` mana, reduced by the number of controlled friendly `瞬息` objects in base/battlefield
  - exhausts the legend source
  - creates an active `3` power `精灵` unit token in base using token card `UNL·T07`
  - token carries `CARD_TYPE:UNIT`, `瞬息`, and `仙灵`
- Accepted same-functional-unit legend reprints/variants:
  - `UNL-189/219`
  - `UNL-230*/219`
  - `UNL-230/219`
- Expanded `legend-active-actions` dev seed with Lillia and a friendly ephemeral support unit so the reduced-cost path can be exercised from prompt choices.
- Expanded structured prompt choices with the Lillia ability id plus `SPEND_MANA:3` and `SPEND_MANA:4` cost chips.
- Migrated this legend active-ability slice in `BehaviorSpec`:
  - Implemented functional units: `723/811`
  - Manual deferred functional units: `88/811`
  - Implemented official entries: `876/1009`
  - Manual deferred official entries: `133/1009`
  - Legend rule-domain implemented: `10` functional units / `30` entries
  - Remaining legend manual deferred: `34` functional units / `76` entries
  - Remaining battlefield manual deferred: `54` functional units / `57` entries

P7.9.6 active-ability slice 3 validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests|FullyQualifiedName~P79LegendActLillia"`: passed `39/39`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2520/2520`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `28/28`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2627/2627`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke:
  - Web URL: `http://127.0.0.1:5173/`
  - Temporary current-code API URL: `http://127.0.0.1:5089`
  - Room ID: `p7-9-lillia-1777964799876`
  - Operation path: reload Web URL -> set server URL to `5089` -> set room id -> `join-both` -> `ready-both` -> open dev tools -> `Legend Actions` seed -> choose source `P1-LEGEND-LILLIA` -> choose ability `LEGEND_DYNAMIC_PAY_EXHAUST_CREATE_FAERIE` -> choose cost `SPEND_MANA:3` -> submit `LEGEND_ACT`
  - Event log summary: `LEGEND_ABILITY_ACTIVATED`, `COST_PAID`, `LEGEND_EXHAUSTED`, `UNIT_TOKEN_CREATED`
  - Final snapshot summary: match remains `IN_PROGRESS`; P1 mana `0`; `P1-LEGEND-LILLIA` exhausted; P1 base contains `P1-LEGEND-LILLIA-TOKEN-001`; token card no `UNL·T07`.
  - Screenshot note: Browser DOM/state smoke passed. The in-app Browser screenshot call timed out after `15s`, consistent with the prior screenshot limitation in this environment.

## P7.9.6 Static Legend Slice Delivered

This is the fifth committed rule slice inside P7.9.6. It closes the first legend static keyword surface without adding any frontend legality rule: combat damage remains resolved entirely by `CoreRuleEngine`.

- Added Rumble / 机械公敌 legend static ability:
  - your controlled `机械` units get `坚守` while defending
  - the bonus is calculated during `DECLARE_BATTLE` combat-power resolution
  - the `DAMAGE_APPLIED` event payload exposes `keyword = 坚守`, `keywordBonus = 1`, and the resulting `combatPower`
- Accepted same-functional-unit legend entries:
  - `SFD·181/221`
  - `SFD·240/221`
- Added representative conformance coverage for a mechanical defender under Rumble receiving `+1` defensive combat power and dealing the increased damage back to the attacker.
- Migrated this legend static slice in `BehaviorSpec`:
  - Implemented functional units: `724/811`
  - Manual deferred functional units: `87/811`
  - Implemented official entries: `878/1009`
  - Manual deferred official entries: `131/1009`
  - Legend rule-domain implemented: `11` functional units / `32` entries
  - Remaining legend manual deferred: `33` functional units / `74` entries
  - Remaining battlefield manual deferred: `54` functional units / `57` entries

P7.9.6 static legend slice validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests|FullyQualifiedName~P79LegendStaticRumble"`: passed `38/38`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2521/2521`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `28/28`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2628/2628`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this rule-only slice because Rumble is a passive combat static effect and this batch introduced no new UI operation path. The battle UI continues to submit `DECLARE_BATTLE` through server prompt candidates; the new legality and power result only appears in server events/snapshot.

## P7.9.6 Static Legend Slice 2 Delivered

This is the sixth committed rule slice inside P7.9.6. It continues the combat-static legend path by making attached equipment provide server-calculated attack power through Lucian.

- Added Lucian / 圣枪游侠 legend static ability:
  - each controlled attached equipment on an attacking unit contributes `强攻 +1`
  - the bonus is calculated during `DECLARE_BATTLE` combat-power resolution
  - the `DAMAGE_APPLIED` event payload exposes `keyword = 强攻`, `keywordBonus`, and the resulting `combatPower`
- Accepted same-functional-unit legend entries:
  - `SFD·183/221`
  - `SFD·241/221`
- Added representative conformance coverage for an attacking unit with one attached equipment under Lucian dealing one extra combat damage.
- Migrated this legend static slice in `BehaviorSpec`:
  - Implemented functional units: `725/811`
  - Manual deferred functional units: `86/811`
  - Implemented official entries: `880/1009`
  - Manual deferred official entries: `129/1009`
  - Legend rule-domain implemented: `12` functional units / `34` entries
  - Remaining legend manual deferred: `32` functional units / `72` entries
  - Remaining battlefield manual deferred: `54` functional units / `57` entries

P7.9.6 static legend slice 2 validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests|FullyQualifiedName~P79LegendStaticLucian"`: passed `38/38`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2522/2522`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `28/28`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2629/2629`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this rule-only slice because Lucian is a passive combat static effect and this batch introduced no new UI operation path. The battle UI continues to submit `DECLARE_BATTLE`; attached-equipment assault is visible through server events/snapshot.

## P7.9.6 Static Legend Slice 3 Delivered

This is the seventh committed rule slice inside P7.9.6. It adds the first non-keyword combat-static legend bonus and exposes that distinction in server combat event payloads.

- Added OGS Master Yi / 无极剑圣 legend static ability:
  - if the controller has exactly one friendly unit defending in the battle, that defender gets `+2` combat power
  - the bonus is calculated during `DECLARE_BATTLE` combat-power resolution
  - `DAMAGE_APPLIED` now includes `staticPowerBonus` when non-keyword static combat power modifies the result
- Accepted legend entry:
  - `OGS·019/024`
- Added representative conformance coverage for a single defending unit under Master Yi dealing `+2` extra combat damage while keeping `keywordBonus = 0`.
- Migrated this legend static slice in `BehaviorSpec`:
  - Implemented functional units: `726/811`
  - Manual deferred functional units: `85/811`
  - Implemented official entries: `881/1009`
  - Manual deferred official entries: `128/1009`
  - Legend rule-domain implemented: `13` functional units / `35` entries
  - Remaining legend manual deferred: `31` functional units / `71` entries
  - Remaining battlefield manual deferred: `54` functional units / `57` entries

P7.9.6 static legend slice 3 validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests|FullyQualifiedName~P79LegendStaticMasterYi"`: passed `38/38`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2523/2523`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `28/28`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2630/2630`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this rule-only slice because Master Yi is a passive combat static effect and this batch introduced no new UI operation path. The battle UI continues to submit `DECLARE_BATTLE`; the static bonus is visible through server events/snapshot.

## P7.9.6 Static Legend Slice 4 Delivered

This is the eighth committed rule slice inside P7.9.6. It adds an enemy-attack combat-static legend penalty and reuses the `staticPowerBonus` payload field introduced by the Master Yi slice.

- Added Ahri / 九尾妖狐 legend static ability:
  - when an enemy unit attacks a battlefield controlled by Ahri's controller, that attacker gets `-1` combat power
  - the penalty cannot reduce the attacker's combat power below `1`
  - the penalty is calculated during `DECLARE_BATTLE` combat-power resolution
  - `DAMAGE_APPLIED` exposes `staticPowerBonus = -1` when the penalty applies
- Accepted same-functional-unit legend entries:
  - `OGN·255/298`
  - `OGN·303*/298`
  - `OGN·303/298`
- Added representative conformance coverage for a `3` power attacker dealing only `2` combat damage while attacking into Ahri's controlled battlefield.
- Migrated this legend static slice in `BehaviorSpec`:
  - Implemented functional units: `727/811`
  - Manual deferred functional units: `84/811`
  - Implemented official entries: `884/1009`
  - Manual deferred official entries: `125/1009`
  - Legend rule-domain implemented: `14` functional units / `38` entries
  - Remaining legend manual deferred: `30` functional units / `68` entries
  - Remaining battlefield manual deferred: `54` functional units / `57` entries

P7.9.6 static legend slice 4 validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests|FullyQualifiedName~P79LegendStaticAhri"`: passed `38/38`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2524/2524`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `28/28`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2631/2631`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this rule-only slice because Ahri is a passive combat static effect and this batch introduced no new UI operation path. The battle UI continues to submit `DECLARE_BATTLE`; the attack penalty is visible through server events/snapshot.

## P7.9.6 Automatic-Trigger Slice 2 Delivered

This is the ninth committed rule slice inside P7.9.6. It adds a battle-won legend trigger that resolves after lethal combat cleanup and before the final snapshot is emitted.

- Added Draven / 荣耀行刑官 legend trigger:
  - after combat cleanup, if exactly one side still has units remaining in that battle, that side is considered to have won the battle
  - if the battle winner controls Draven in their legend zone, draw `1`
  - emits `LEGEND_TRIGGER_RESOLVED` with `trigger = BATTLE_WON_DRAW_ONE`, then normal `CARD_DRAWN` events
  - preserves burnout/winner handling through the existing `ApplyDrawToPlayer` path
- Accepted same-functional-unit legend entries:
  - `SFD·185/221`
  - `SFD·242/221`
- Added representative conformance coverage for an attacker winning combat under Draven and drawing the top main-deck card.
- Migrated this legend trigger slice in `BehaviorSpec`:
  - Implemented functional units: `728/811`
  - Manual deferred functional units: `83/811`
  - Implemented official entries: `886/1009`
  - Manual deferred official entries: `123/1009`
  - Legend rule-domain implemented: `15` functional units / `40` entries
  - Remaining legend manual deferred: `29` functional units / `66` entries
  - Remaining battlefield manual deferred: `54` functional units / `57` entries

P7.9.6 automatic-trigger slice 2 validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests|FullyQualifiedName~P79LegendTriggerDraven"`: passed `38/38`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2525/2525`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `28/28`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2632/2632`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this rule-only slice because Draven is a passive combat trigger and this batch introduced no new UI operation path. The battle UI continues to submit `DECLARE_BATTLE`; the trigger and draw are visible through server events/snapshot.

## P7.9.6 Automatic-Trigger Slice 3 Delivered

This is the tenth committed rule slice inside P7.9.6. It maps Garen's conquest draw trigger onto the current P4 `BATTLEFIELD_CONQUERED` representative path.

- Added OGS Garen / 德玛西亚之力 legend trigger:
  - when the controller conquers a battlefield through the current `BATTLEFIELD_CONQUERED` path and controls at least four battlefield units, draw `2`
  - emits `LEGEND_TRIGGER_RESOLVED` with `trigger = BATTLEFIELD_CONQUERED_DRAW_TWO`, then normal `CARD_DRAWN` events with `count = 2`
  - uses the existing draw/burnout/winner handling path
- Boundary note: until P7.9.7/P7.9.8 add battlefield-specific location ownership, this representative counts controlled battlefield unit objects in the controller's battlefield zone.
- Accepted legend entry:
  - `OGS·023/024`
- Added representative conformance coverage for a hunt conquest with four controlled battlefield units drawing two cards.
- Migrated this legend trigger slice in `BehaviorSpec`:
  - Implemented functional units: `729/811`
  - Manual deferred functional units: `82/811`
  - Implemented official entries: `887/1009`
  - Manual deferred official entries: `122/1009`
  - Legend rule-domain implemented: `16` functional units / `41` entries
  - Remaining legend manual deferred: `28` functional units / `65` entries
  - Remaining battlefield manual deferred: `54` functional units / `57` entries

P7.9.6 automatic-trigger slice 3 validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests|FullyQualifiedName~P79LegendTriggerGaren"`: passed `38/38`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2526/2526`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `28/28`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2633/2633`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this rule-only slice because Garen is a passive conquest trigger and this batch introduced no new UI operation path. The battle UI continues to submit `DECLARE_BATTLE`; the conquest trigger and draw are visible through server events/snapshot.

## P7.9.6 Automatic-Trigger Slice 4 Delivered

This is the eleventh committed rule slice inside P7.9.6. It adds a play-trigger legend draw for a high-cost spell without adding any frontend legality rule.

- Added OGS Lux / 光辉女郎 legend trigger:
  - when the controller successfully plays a spell with printed mana cost at least `5`, draw `1`
  - uses the server `CardBehaviorDefinition` to identify non-unit/non-equipment spells and the official printed mana cost, not frontend card text parsing
  - emits `LEGEND_TRIGGER_RESOLVED` with `trigger = HIGH_COST_SPELL_DRAW_ONE`, then normal `CARD_DRAWN` events
  - preserves burnout/winner handling through the existing `ApplyDrawToPlayer` path
- Accepted legend entry:
  - `OGS·021/024`
- Added representative conformance coverage for playing `OGN·114/298` Evolution Day under Lux and drawing before the stack item resolves.
- Migrated this legend trigger slice in `BehaviorSpec`:
  - Implemented functional units: `730/811`
  - Manual deferred functional units: `81/811`
  - Implemented official entries: `888/1009`
  - Manual deferred official entries: `121/1009`
  - Legend rule-domain implemented: `17` functional units / `42` entries
  - Remaining legend manual deferred: `27` functional units / `64` entries
  - Remaining battlefield manual deferred: `54` functional units / `57` entries

P7.9.6 automatic-trigger slice 4 validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests|FullyQualifiedName~P79LegendTriggerLux"`: passed `38/38`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2527/2527`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `28/28`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2634/2634`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this rule-only slice because Lux is a passive `PLAY_CARD` trigger and this batch introduced no new UI operation path. The play-card UI continues to submit server prompt commands; the trigger and draw are visible through server events/snapshot.

## P7.9.6 Static Legend Slice 5 Delivered

This is the twelfth committed rule slice inside P7.9.6. It adds the UNL Master Yi level legend aura without adding frontend legality logic.

- Added UNL Master Yi / 无极宗师 level legend static:
  - at `6+` experience, friendly combat units receive a server-calculated `staticPowerBonus = +1`
  - at `11+` experience, units entering under the controller are forced active in the unit-entry path
  - reuses the existing `PlayerExperience`, legend-zone, combat event payload, and unit-entry server state paths
- Accepted legend entries:
  - `UNL-191/219`
  - `UNL-231/219`
  - `UNL-231*/219`
- Added representative conformance coverage for the level-6 combat aura and level-11 active-entry boundary.
- Migrated this legend static slice in `BehaviorSpec`:
  - Implemented functional units: `732/811`
  - Manual deferred functional units: `79/811`
  - Implemented official entries: `891/1009`
  - Manual deferred official entries: `118/1009`
  - Legend rule-domain implemented: `19` functional units / `45` entries
  - Remaining legend manual deferred: `25` functional units / `61` entries
  - Remaining battlefield manual deferred: `54` functional units / `57` entries

P7.9.6 static legend slice 5 validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests|FullyQualifiedName~P79LegendStaticMasterYiLevel"`: passed `39/39`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2529/2529`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `28/28`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2636/2636`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this rule-only slice because the Master Yi level aura is a passive combat/unit-entry static and this batch introduced no new UI operation path. The play-card and battle UI continue to submit server prompt commands; the power bonus and active-entry state are visible through server events/snapshot.

## P7.9.6 Automatic-Trigger Slice 5 Delivered

This is the thirteenth committed rule slice inside P7.9.6. It adds a turn-end legend resource trigger without adding frontend legality logic.

- Added OGS Annie / 黑暗之女 legend trigger:
  - at the controller's turn end, ready up to `2` exhausted rune objects in that controller's base
  - emits `LEGEND_TRIGGER_RESOLVED` with `trigger = TURN_END_READY_TWO_RUNES`
  - emits `RUNE_READIED` with the exact readied rune object ids and `count = 2` when two runes are readied
  - leaves non-rune exhausted objects and additional exhausted runes unchanged
- Accepted legend entry:
  - `OGS·017/024`
- Added representative conformance coverage for ending the turn with Annie, three exhausted runes, and an exhausted non-rune unit.
- Migrated this legend trigger slice in `BehaviorSpec`:
  - Implemented functional units: `733/811`
  - Manual deferred functional units: `78/811`
  - Implemented official entries: `892/1009`
  - Manual deferred official entries: `117/1009`
  - Legend rule-domain implemented: `20` functional units / `46` entries
  - Remaining legend manual deferred: `24` functional units / `60` entries
  - Remaining battlefield manual deferred: `54` functional units / `57` entries

P7.9.6 automatic-trigger slice 5 validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests|FullyQualifiedName~P79LegendTriggerAnnie"`: passed `38/38`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2530/2530`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `28/28`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2637/2637`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this rule-only slice because Annie is a passive turn-end trigger and this batch introduced no new UI operation path. The end-turn UI continues to submit server prompt commands; the rune-ready trigger is visible through server events/snapshot.

## P7.9.6 Automatic-Trigger Slice 6 Delivered

This is the fourteenth committed rule slice inside P7.9.6. It adds powerful-unit legend rune triggers without adding frontend legality logic.

- Added Volibear / 不灭狂雷 and Fiora / 无双剑姬 legend triggers:
  - after a controller's played source unit resolves onto the field, if that unit has `5+` power, a ready matching legend may exhaust to call `1` rune
  - the server checks the resolved unit object state after unit-entry static modifiers
  - the legend is exhausted only when a rune is actually called
  - emits `LEGEND_TRIGGER_RESOLVED` with `trigger = POWERFUL_UNIT_PLAYED_CALL_RUNE`, then `LEGEND_EXHAUSTED` and `RUNES_CALLED`
- Accepted same-functional-unit legend entries:
  - `FND-249/298`
  - `OGN·249/298`
  - `OGN·300/298`
  - `OGN·300*/298`
  - `SFD·205/221`
  - `SFD·251/221`
- Added representative conformance coverage for Volibear and Fiora variants playing a `5`-power unit and calling an exhausted rune.
- Migrated this legend trigger slice in `BehaviorSpec`:
  - Implemented functional units: `735/811`
  - Manual deferred functional units: `76/811`
  - Implemented official entries: `898/1009`
  - Manual deferred official entries: `111/1009`
  - Legend rule-domain implemented: `22` functional units / `52` entries
  - Remaining legend manual deferred: `22` functional units / `54` entries
  - Remaining battlefield manual deferred: `54` functional units / `57` entries

P7.9.6 automatic-trigger slice 6 validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests|FullyQualifiedName~P79LegendTriggerPowerfulUnit"`: passed `39/39`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2532/2532`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `28/28`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2639/2639`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this rule-only slice because the Volibear/Fiora powerful-unit trigger is a passive stack-resolution trigger and this batch introduced no new UI operation path. The play-card UI continues to submit server prompt commands; the trigger, legend exhaustion, and called rune are visible through server events/snapshot.

## P7.9.6 Active-Ability Slice 4 Delivered

This is the fifteenth committed rule slice inside P7.9.6. It adds Jax / 武器大师 legend armament attachment actions through the existing server-authoritative `LEGEND_ACT` command.

- Added Jax legend active abilities:
  - `LEGEND_PAY_1_EXHAUST_ATTACH_UNATTACHED_ARMAMENT`: pays `1`, exhausts Jax, and attaches a controlled unattached `武装` equipment to a controlled unit
  - `LEGEND_EXHAUST_REATTACH_ATTACHED_ARMAMENT`: exhausts Jax and reattaches a controlled attached `武装` equipment to a different controlled unit
  - both abilities validate source legend ownership, active timing, source exhaustion, target order, armament state, and server-side cost payment
- Expanded `ActionPrompt` legend candidates:
  - `LEGEND_ACT` target candidates now include controlled units and controlled equipment so the page can select the unit + armament pair
  - added prompt mode labels for the Jax attach and reattach abilities
- Accepted same-functional-unit legend entries:
  - `SFD·193/221`
  - `SFD·245/221`
- Added representative conformance coverage for Jax attach and Jax reattach paths.
- Migrated this legend active slice in `BehaviorSpec`:
  - Implemented functional units: `736/811`
  - Manual deferred functional units: `75/811`
  - Implemented official entries: `900/1009`
  - Manual deferred official entries: `109/1009`
  - Legend rule-domain implemented: `23` functional units / `54` entries
  - Remaining legend manual deferred: `21` functional units / `52` entries
  - Remaining battlefield manual deferred: `54` functional units / `57` entries

P7.9.6 active-ability slice 4 validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests|FullyQualifiedName~P79LegendActJax"`: passed `39/39`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2534/2534`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `28/28`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2641/2641`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this rule-only slice because the existing `LEGEND_ACT` UI path already renders source, mode, target, and cost candidates; this batch added new server candidates and backend validation without a new page flow. The Jax attach/reattach actions are visible through server prompt candidates, `LEGEND_ABILITY_ACTIVATED`, `LEGEND_EXHAUSTED`, `EQUIPMENT_ATTACHED`, and `EQUIPMENT_REATTACHED` events.

## P7.9.6 Active-Ability Slice 5 Delivered

This is the sixteenth committed rule slice inside P7.9.6. It adds Darius / 诺克萨斯之手 legend encourage resource actions through `LEGEND_ACT`.

- Added Darius legend active ability:
  - `LEGEND_ENCOURAGE_EXHAUST_GAIN_1_MANA`
  - requires the controller to have played another card this turn via existing `PlayerCardsPlayedThisTurn`
  - exhausts the legend and gains `1` mana through server state
  - emits `LEGEND_ABILITY_ACTIVATED`, `LEGEND_EXHAUSTED`, and `MANA_GAINED`
- Expanded `ActionPrompt` legend candidates with a Darius mode label.
- Accepted same-functional-unit legend entries:
  - `OGN·253/298`
  - `OGN·302/298`
  - `OGN·302*/298`
- Added representative conformance coverage for successful Darius encourage mana gain and rejection before another card was played this turn.
- Migrated this legend active slice in `BehaviorSpec`:
  - Implemented functional units: `737/811`
  - Manual deferred functional units: `74/811`
  - Implemented official entries: `903/1009`
  - Manual deferred official entries: `106/1009`
  - Legend rule-domain implemented: `24` functional units / `57` entries
  - Remaining legend manual deferred: `20` functional units / `49` entries
  - Remaining battlefield manual deferred: `54` functional units / `57` entries

P7.9.6 active-ability slice 5 validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests|FullyQualifiedName~P79LegendActDarius"`: passed `39/39`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2536/2536`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `28/28`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2643/2643`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this rule-only slice because the existing `LEGEND_ACT` UI path already renders no-target, no-cost legend actions through server candidates. The Darius action is visible through server prompt candidates and the resulting `MANA_GAINED` event/snapshot.

## P7.9.6 Active/Replacement Slice 6 Delivered

This is the seventeenth committed rule slice inside P7.9.6. It adds Teemo / 迅捷斥候 legend recall and standby hide replacement coverage.

- Added Teemo legend active ability:
  - `LEGEND_PAY_1_EXHAUST_RECALL_OWNED_TEEMO_UNIT`
  - pays `1`, exhausts Teemo, and returns an owned Teemo unit from the champion zone or field to hand
  - validates source legend, target ownership, target card identity, unit tag, zone, source exhaustion, and mana payment
  - emits `LEGEND_ABILITY_ACTIVATED`, `COST_PAID`, `LEGEND_EXHAUSTED`, and `UNIT_RETURNED_TO_HAND`
- Added Teemo standby replacement support:
  - `HIDE_CARD optionalCosts=["STANDBY_TEEMO_MANA"]`
  - requires a controlled Teemo legend
  - spends `1` mana and records `teemoStandbyHideReplacement` in the cost event
- Expanded `ActionPrompt` legend candidates with the Teemo recall mode and champion-zone unit targets for legend actions.
- Accepted same-functional-unit legend entries:
  - `OGN·263/298`
  - `OGN·263a/298`
  - `OGN·307/298`
  - `OGN·307*/298`
- Added representative conformance coverage for Teemo legend recall, Teemo standby replacement success, and replacement rejection without a Teemo legend.
- Migrated this legend active/replacement slice in `BehaviorSpec`:
  - Implemented functional units: `738/811`
  - Manual deferred functional units: `73/811`
  - Implemented official entries: `907/1009`
  - Manual deferred official entries: `102/1009`
  - Legend rule-domain implemented: `25` functional units / `61` entries
  - Remaining legend manual deferred: `19` functional units / `45` entries
  - Remaining battlefield manual deferred: `54` functional units / `57` entries

P7.9.6 active/replacement slice 6 validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests|FullyQualifiedName~P79LegendActTeemo|FullyQualifiedName~P79LegendTeemo"`: passed `40/40`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2539/2539`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `28/28`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2646/2646`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this rule slice. The Teemo recall path uses the existing `LEGEND_ACT` candidate UI, and standby replacement remains a backend `HIDE_CARD` rule path without a new product panel in this batch.

## P7.9.6 Active/Static Slice 7 Delivered

This is the eighteenth committed rule slice inside P7.9.6. It adds Azir / 沙漠皇帝 legend sand-soldier creation plus the static Sand Soldier `百炼` tag surface.

- Added Azir legend active ability:
  - `LEGEND_PAY_1_EXHAUST_CREATE_SAND_SOLDIER_AFTER_ARMAMENT`
  - requires the controller to have played a `武装` equipment this turn through a server-side until-end-of-turn marker
  - pays `1`, exhausts Azir, and creates official token `SFD·T02` 黄沙士兵 in the controller's base
  - applies Azir's static surface by adding `百炼` to created Sand Soldiers while the controller has an Azir legend
  - emits `LEGEND_ABILITY_ACTIVATED`, `COST_PAID`, `LEGEND_EXHAUSTED`, and `UNIT_TOKEN_CREATED`
- Expanded `ActionPrompt` legend candidates with the Azir mode label and updated the local legend-actions seed so the page can operate the new ability from the existing `LEGEND_ACT` panel.
- Accepted same-functional-unit legend entries:
  - `SFD·197/221`
  - `SFD·247/221`
- Added representative conformance coverage for Azir success, rejection before an armament was played this turn, and the armament-play marker produced by playing official weapon `SFD·095/221` 多兰之刃 even when the source object lacks a preexisting `武装` tag.
- Migrated this legend active/static slice in `BehaviorSpec`:
  - Implemented functional units: `739/811`
  - Manual deferred functional units: `72/811`
  - Implemented official entries: `909/1009`
  - Manual deferred official entries: `100/1009`
  - Legend rule-domain implemented: `26` functional units / `63` entries
  - Remaining legend manual deferred: `18` functional units / `43` entries
  - Remaining battlefield manual deferred: `54` functional units / `57` entries

P7.9.6 active/static slice 7 validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests|FullyQualifiedName~P79LegendActAzir|FullyQualifiedName~P79LegendAzir"`: passed `40/40`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2542/2542`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `28/28`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2649/2649`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this rule slice because it reuses the existing `LEGEND_ACT` UI path and no new frontend flow was added. The Azir action is visible through server prompt candidates and the resulting `UNIT_TOKEN_CREATED` event/snapshot.

## P7.9.6 Automatic-Trigger Slice 7 Delivered

This is the nineteenth committed rule slice inside P7.9.6. It adds Rengar / 傲之追猎者's unit-play trigger.

- Added Rengar legend trigger support for:
  - `UNL-183/219`
  - `UNL-227/219`
  - `UNL-227*/219`
- Extended server-authoritative `PLAY_CARD` planning so a Rengar controller can submit one extra unit trigger target while the played card's own behavior targets remain separate on the stack item.
- When the unit resolves, the engine consumes the queued trigger target and gives that unit `S+1` until end of turn, emitting `LEGEND_TRIGGER_RESOLVED` and `POWER_MODIFIED_UNTIL_END_OF_TURN`.
- Invalid non-unit trigger targets are rejected by the backend with `InvalidTarget`; frontend remains an intent submitter and does not own the rule decision.
- Migrated this legend trigger slice in `BehaviorSpec`:
  - Implemented functional units: `740/811`
  - Manual deferred functional units: `71/811`
  - Implemented official entries: `912/1009`
  - Manual deferred official entries: `97/1009`
  - Legend rule-domain implemented: `27` functional units / `66` entries
  - Remaining legend manual deferred: `17` functional units / `40` entries
  - Remaining battlefield manual deferred: `54` functional units / `57` entries

P7.9.6 automatic-trigger slice 7 validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests|FullyQualifiedName~P79LegendTriggerRengar"`: passed `39/39`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2544/2544`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `28/28`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2651/2651`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this rule slice. It extends backend `PLAY_CARD` trigger resolution without adding a new frontend flow in this batch; later prompt/UI candidate work will expose the extra trigger target as a click path.

## P7.9.6 Automatic-Trigger Slice 8 Delivered

This is the twentieth committed rule slice inside P7.9.6. It adds Leona / 曙光女神's enemy-stunned boon trigger.

- Added Leona legend trigger support for:
  - `OGN·261/298`
  - `OGN·306/298`
  - `OGN·306*/298`
- Extended server-authoritative `PLAY_CARD` planning so a Leona controller can submit one extra boon target on a card that applies `STUNNED`; the played card's own targets remain the only stack targets.
- When the stack item actually stuns at least one enemy unit, the engine consumes the queued boon target and grants that friendly unit `增益`, including the `S+1` power bump when it did not already have boon.
- Invalid non-friendly or non-unit boon targets are rejected by the backend with `InvalidTarget`; frontend remains an intent submitter and does not infer trigger legality.
- Migrated this legend trigger slice in `BehaviorSpec`:
  - Implemented functional units: `741/811`
  - Manual deferred functional units: `70/811`
  - Implemented official entries: `915/1009`
  - Manual deferred official entries: `94/1009`
  - Legend rule-domain implemented: `28` functional units / `69` entries
  - Remaining legend manual deferred: `16` functional units / `37` entries
  - Remaining battlefield manual deferred: `54` functional units / `57` entries

P7.9.6 automatic-trigger slice 8 validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests|FullyQualifiedName~P79LegendTriggerLeona"`: passed `39/39`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2546/2546`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `28/28`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2653/2653`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this backend rule slice. It extends the same server-authoritative `PLAY_CARD` target-splitting model used by Rengar; later prompt/UI work will expose the extra trigger target as a click path.

## P7.9.6 Automatic-Trigger Slice 9 Delivered

This is the twenty-first committed rule slice inside P7.9.6. It adds Sivir / 战争女神's rune-recycle and enemy-destroyed triggers.

- Added Sivir legend trigger support for:
  - `SFD·203/221`
  - `SFD·250/221`
- Reuses existing `CARDS_RECYCLED` events to detect when the Sivir controller recycles a rune, exhausts the legend, and creates a dormant `金币` equipment token in base.
- Reuses existing destroyed-unit ownership tracking to ready each exhausted Sivir whose enemy unit is destroyed, including opponent-controlled stack items that destroy their own unit.
- Expanded recycle event payloads with `cardIds` where needed so the backend can distinguish recycled rune cards without frontend inference.
- Migrated this legend trigger slice in `BehaviorSpec`:
  - Implemented functional units: `742/811`
  - Manual deferred functional units: `69/811`
  - Implemented official entries: `917/1009`
  - Manual deferred official entries: `92/1009`
  - Legend rule-domain implemented: `29` functional units / `71` entries
  - Remaining legend manual deferred: `15` functional units / `35` entries
  - Remaining battlefield manual deferred: `54` functional units / `57` entries

P7.9.6 automatic-trigger slice 9 validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests|FullyQualifiedName~P79LegendTriggerSivir"`: passed `40/40`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2549/2549`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `28/28`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2656/2656`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this backend rule slice. It consumes existing server events and emits snapshot/event updates; later prompt/UI work will only display those authoritative results.

## P7.9.6 Active/Reaction Resource Slice 8 Delivered

This is the twenty-second committed rule slice inside P7.9.6. It adds reaction-speed legend resource actions for Diana / 皎月女神, Kai'Sa / 虚空之女, and Ornn / 山隐之焰 through the existing server-authoritative `LEGEND_ACT` command.

- Added Diana legend action support for:
  - `UNL-197/219`
  - `UNL-234/219`
  - `UNL-234*/219`
- Added Kai'Sa legend action support for:
  - `OGN·247/298`
  - `OGN·299/298`
  - `OGN·299*/298`
- Added Ornn legend action support for:
  - `SFD·189/221`
  - `SFD·244/221`
- Split `LEGEND_ACT` timing validation into ordinary main-window, priority-window, and spell-duel-focus windows.
- Exposed `LEGEND_ACT` prompt candidates in priority and spell-duel focus windows so the page can operate these reaction-resource legends from server prompts instead of frontend rules.
- Kaisa validates that a pending stack source is a spell before granting `A`/power; Ornn validates that a pending stack source is equipment before granting `A`/power; Diana validates the spell-duel focus window before granting `1` mana.
- Migrated this legend resource slice in `BehaviorSpec`:
  - Implemented functional units: `746/811`
  - Manual deferred functional units: `65/811`
  - Implemented official entries: `925/1009`
  - Manual deferred official entries: `84/1009`
  - Legend rule-domain implemented: `33` functional units / `79` entries
  - Remaining legend manual deferred: `11` functional units / `27` entries
  - Remaining battlefield manual deferred: `54` functional units / `57` entries

P7.9.6 active/reaction resource slice 8 validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79LegendActDiana|FullyQualifiedName~P79LegendActKaisa|FullyQualifiedName~P79LegendActOrnn"`: passed `5/5`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests|FullyQualifiedName~P79LegendActDiana|FullyQualifiedName~P79LegendActKaisa|FullyQualifiedName~P79LegendActOrnn"`: passed `42/42`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2554/2554`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `28/28`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2661/2661`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this backend/prompt slice. The change is covered by conformance prompt assertions for priority and spell-duel focus windows; later UI smoke should verify reaction-window chips in the product workbench.

## P7.9.6 Automatic-Trigger Slice 10 Delivered

This is the twenty-third committed rule slice inside P7.9.6. It adds Jhin / 戏命师 high-cost spell tracking.

- Added Jhin legend trigger support for:
  - `UNL-181/219`
  - `UNL-226/219`
  - `UNL-226*/219`
- When a Jhin controller resolves a spell with mana cost at least `4`, the backend marks and banishes that spell through the Jhin trigger rather than letting the frontend infer a zone move.
- When four spells have been banished this way, the backend moves those tracked spells to graveyard, calls four runes, and draws one card.
- Migrated this legend trigger slice in `BehaviorSpec`:
  - Implemented functional units: `747/811`
  - Manual deferred functional units: `64/811`
  - Implemented official entries: `928/1009`
  - Manual deferred official entries: `81/1009`
  - Legend rule-domain implemented: `34` functional units / `82` entries
  - Remaining legend manual deferred: `10` functional units / `24` entries
  - Remaining battlefield manual deferred: `54` functional units / `57` entries

P7.9.6 automatic-trigger slice 10 validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79LegendTriggerJhin"`: passed `1/1`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests|FullyQualifiedName~P79LegendTriggerJhin"`: passed `38/38`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2555/2555`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `28/28`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2662/2662`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this backend trigger slice. It updates authoritative events/snapshots through existing stack resolution.

## P7.9.6 Active/Reaction Draw Slice 9 Delivered

This is the twenty-fourth committed rule slice inside P7.9.6. It adds Ezreal / 探险家's reaction draw condition through the existing server-authoritative `LEGEND_ACT` command.

- Added Ezreal legend action support for:
  - `SFD·199/221`
  - `SFD·248/221`
- The backend now tracks, until end of turn, whether an Ezreal controller has twice targeted enemy units or equipment with spells or implemented unit skills.
- Ezreal's reaction action is only accepted in a priority window when the target-count condition has reached `2`; it exhausts the legend and draws one card without resolving or mutating the pending stack item.
- The unit-skill target tracker is gated by actual Ezreal legend presence so unrelated skill fixtures do not gain Ezreal-only until-end-of-turn markers.
- Migrated this legend reaction draw slice in `BehaviorSpec`:
  - Implemented functional units: `748/811`
  - Manual deferred functional units: `63/811`
  - Implemented official entries: `930/1009`
  - Manual deferred official entries: `79/1009`
  - Legend rule-domain implemented: `35` functional units / `84` entries
  - Remaining legend manual deferred: `9` functional units / `22` entries
  - Remaining battlefield manual deferred: `54` functional units / `57` entries

P7.9.6 active/reaction draw slice 9 validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79LegendActEzreal"`: passed `2/2`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79LegendActEzreal|FullyQualifiedName~CoreRuleEnginePlaysStandFirmAndPreventsXerathSkillDamageThisTurn"`: passed `3/3`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2557/2557`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `28/28`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2664/2664`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this backend/prompt rule slice. It reuses the existing priority-window `LEGEND_ACT` UI path and is covered by conformance prompt assertions.

## P7.9.6 Automatic-Trigger Slice 11 Delivered

This is the twenty-fifth committed rule slice inside P7.9.6. It adds Vi / 皮城执法官's overkill conquer trigger.

- Added Vi legend trigger support for:
  - `UNL-187/219`
  - `UNL-229/219`
  - `UNL-229*/219`
- `DECLARE_BATTLE` now records `assignedOverkillDamageToEnemyUnits` on `BATTLEFIELD_CONQUERED` events.
- When a Vi controller conquers after assigning at least `3` overkill damage to enemy units, the backend exhausts the Vi legend and readies one exhausted friendly field unit.
- The trigger is skipped if the overkill threshold is not met, the Vi legend is already exhausted, or no exhausted friendly unit exists.
- Migrated this legend trigger slice in `BehaviorSpec`:
  - Implemented functional units: `749/811`
  - Manual deferred functional units: `62/811`
  - Implemented official entries: `933/1009`
  - Manual deferred official entries: `76/1009`
  - Legend rule-domain implemented: `36` functional units / `87` entries
  - Remaining legend manual deferred: `8` functional units / `19` entries
  - Remaining battlefield manual deferred: `54` functional units / `57` entries

P7.9.6 automatic-trigger slice 11 validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79LegendTriggerVi"`: passed `2/2`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2559/2559`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `28/28`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2666/2666`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this backend battle-trigger rule slice. It reuses existing `DECLARE_BATTLE` UI flow and emits authoritative event/snapshot changes.

## P7.9.6 Automatic-Trigger Slice 12 Delivered

This is the twenty-sixth committed rule slice inside P7.9.6. It adds Vex / 愁云使者's battlefield-hold draw trigger.

- Added Vex legend trigger support for:
  - `UNL-193/219`
  - `UNL-232/219`
  - `UNL-232*/219`
- When the defending player wins a battle and controls an active Vex legend, the backend emits `BATTLEFIELD_HELD`, exhausts Vex, and draws one card for that player.
- The trigger is skipped when the Vex legend is already exhausted; no frontend rule inference is added.
- Migrated this legend trigger slice in `BehaviorSpec`:
  - Implemented functional units: `750/811`
  - Manual deferred functional units: `61/811`
  - Implemented official entries: `936/1009`
  - Manual deferred official entries: `73/1009`
  - Legend rule-domain implemented: `37` functional units / `90` entries
  - Remaining legend manual deferred: `7` functional units / `16` entries
  - Remaining battlefield manual deferred: `54` functional units / `57` entries

P7.9.6 automatic-trigger slice 12 validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79LegendTriggerVex"`: passed `2/2`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2561/2561`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `28/28`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2668/2668`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this backend battle-trigger rule slice. It reuses existing `DECLARE_BATTLE` UI flow and emits authoritative event/snapshot changes.

## P7.9.6 Active/Reaction Ready Slice 10 Delivered

This is the twenty-seventh committed rule slice inside P7.9.6. It adds Irelia / 刀锋舞者's friendly-target reaction and conquest ready trigger.

- Added Irelia legend action/trigger support for:
  - `SFD·195/221`
  - `SFD·195a/221·P`
  - `SFD·246/221`
- Added `LEGEND_REACTION_PAY_1_EXHAUST_READY_TARGETED_FRIENDLY_UNIT` through the existing server-authoritative `LEGEND_ACT` path.
- The reaction is accepted only in a priority window when a pending spell or skill controlled by Irelia's controller targets the selected friendly field unit; the backend charges `SPEND_MANA:1`, exhausts Irelia, and readies that unit.
- `DECLARE_BATTLE` now resolves Irelia's conquest trigger on the existing `BATTLEFIELD_CONQUERED` path: if Irelia is exhausted and the controller has 1 mana, the backend pays 1 and readies the legend.
- Added the Irelia ability to structured prompt modes so the page can operate it from server prompt data without frontend legality rules.
- Migrated this legend ready slice in `BehaviorSpec`:
  - Implemented functional units: `751/811`
  - Manual deferred functional units: `60/811`
  - Implemented official entries: `939/1009`
  - Manual deferred official entries: `70/1009`
  - Legend rule-domain implemented: `38` functional units / `93` entries
  - Remaining legend manual deferred: `6` functional units / `13` entries
  - Remaining battlefield manual deferred: `54` functional units / `57` entries

P7.9.6 active/reaction ready slice 10 validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79LegendActIrelia|FullyQualifiedName~P79LegendTriggerIrelia"`: passed `4/4`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2565/2565`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `28/28`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2672/2672`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this backend/prompt rule slice. It reuses the existing priority-window `LEGEND_ACT` and `DECLARE_BATTLE` UI paths and emits authoritative prompt/event/snapshot changes; the next significant frontend flow batch should smoke the newly exposed Irelia mode.

## P7.9.6 Automatic-Trigger Slice 13 Delivered

This is the twenty-eighth committed rule slice inside P7.9.6. It adds Renata Glasc / 炼金男爵's battlefield-hold Gold trigger and near-victory Gold bonus marker.

- Added Renata legend trigger support for:
  - `SFD·201/221`
  - `SFD·249/221`
- Generalized the defender-wins battle path so `BATTLEFIELD_HELD` is emitted once when any implemented held-battlefield legend trigger resolves.
- When the defending player wins a battle and controls an active Renata legend, the backend exhausts Renata and creates a dormant `金币` equipment token in that player's base.
- When Renata's controller is within `3` points of the winning score, the created Gold token receives `RENATA_GOLD_EXTRA_1_MANA`, and the trigger event exposes `renataGoldExtraManaActive = true`.
- The trigger is skipped when Renata is already exhausted; no frontend rule inference is added.
- Migrated this legend trigger/static marker slice in `BehaviorSpec`:
  - Implemented functional units: `752/811`
  - Manual deferred functional units: `59/811`
  - Implemented official entries: `941/1009`
  - Manual deferred official entries: `68/1009`
  - Legend rule-domain implemented: `39` functional units / `95` entries
  - Remaining legend manual deferred: `5` functional units / `11` entries
  - Remaining battlefield manual deferred: `54` functional units / `57` entries

P7.9.6 automatic-trigger slice 13 validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79LegendTriggerRenata"`: passed `3/3`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2568/2568`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `28/28`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2675/2675`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this backend battle-trigger rule slice. It reuses existing `DECLARE_BATTLE` UI flow and emits authoritative event/snapshot changes; later UI polish should render the Renata Gold bonus marker in card/token detail.

## P7.9.6 Automatic-Trigger Slice 14 Delivered

This is the twenty-ninth committed rule slice inside P7.9.6. It adds LeBlanc / 诡术妖姬's battlefield-result Image trigger and keeps all trigger choice, discard, copy, and token creation authority in `CoreRuleEngine`.

- Added LeBlanc legend trigger support for:
  - `UNL-199/219`
  - `UNL-235/219`
  - `UNL-235*/219`
- When LeBlanc's controller conquers or holds a battlefield, the backend can resolve the trigger if LeBlanc is active, that player has a hand card to discard, and there is a valid unit at that battlefield to copy.
- The trigger discards the first hand card deterministically, exhausts LeBlanc, and creates an active battlefield `映像` token that copies the chosen unit's card number, power, and tags while adding `瞬息` and `映像`.
- The event stream records `CARD_DISCARDED`, `LEGEND_TRIGGER_RESOLVED`, `LEGEND_EXHAUSTED`, and `UNIT_TOKEN_CREATED` with `copiedTargetObjectId`, `battlefieldId`, token tags, and destination-zone payloads for UI rendering.
- The trigger is skipped when LeBlanc is exhausted, the controller has no hand card to discard, or no legal unit copy source exists; no frontend rule inference is added.
- Migrated this legend trigger/copy-token slice in `BehaviorSpec`:
  - Implemented functional units: `753/811`
  - Manual deferred functional units: `58/811`
  - Implemented official entries: `944/1009`
  - Manual deferred official entries: `65/1009`
  - Legend rule-domain implemented: `40` functional units / `98` entries
  - Remaining legend manual deferred: `4` functional units / `8` entries
  - Remaining battlefield manual deferred: `54` functional units / `57` entries

P7.9.6 automatic-trigger slice 14 validation:

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79LegendTriggerLeblanc"`: passed `3/3`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2571/2571`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `28/28`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2678/2678`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this backend battle-trigger rule slice. It reuses existing `DECLARE_BATTLE` UI flow and emits authoritative event/snapshot changes; later UI polish should render the active Image token, copied target, `瞬息`, and copy tags in card/token detail.

## P7.9.6 Automatic-Trigger Slice 15 Delivered

This is the thirtieth committed rule slice inside P7.9.6. It adds Rek'Sai / 虚空遁地兽's conquest reveal, play-one, and recycle-rest trigger on top of the existing server-authoritative battle and main-deck movement helpers.

- Added Rek'Sai legend trigger support for:
  - `SFD·187/221`
  - `SFD·243/221`
- When Rek'Sai's controller conquers a battlefield and controls an active Rek'Sai legend, the backend exhausts Rek'Sai, reveals the top two main-deck cards, plays the first revealed unit to that player's base, and recycles the other revealed cards to the main-deck bottom.
- If the revealed pair has no unit card, the backend still resolves the reveal/recycle trigger and recycles both revealed cards without creating a fake playable action.
- The event stream records `LEGEND_TRIGGER_RESOLVED`, `LEGEND_EXHAUSTED`, `CARDS_REVEALED`, optional `UNIT_PLAYED_TO_BASE`, and `CARDS_RECYCLED` with revealed, played, recycled, source-zone, and destination-zone payloads for UI rendering.
- The trigger is skipped when Rek'Sai is exhausted or the controller has no main-deck cards; no frontend rule inference is added.
- Migrated this legend trigger/recycle slice in `BehaviorSpec`:
  - Implemented functional units: `754/811`
  - Manual deferred functional units: `57/811`
  - Implemented official entries: `946/1009`
  - Manual deferred official entries: `63/1009`
  - Legend rule-domain implemented: `41` functional units / `100` entries
  - Remaining legend manual deferred: `3` functional units / `6` entries
  - Remaining battlefield manual deferred: `54` functional units / `57` entries

P7.9.6 automatic-trigger slice 15 validation:

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79LegendTriggerReksai"`: passed `3/3`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2574/2574`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `28/28`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2681/2681`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this backend battle-trigger rule slice. It reuses existing `DECLARE_BATTLE` UI flow and emits authoritative event/snapshot changes; later UI polish should render the revealed cards, played unit, and recycled cards as a grouped event-log entry.

## P7.9.6 Automatic-Trigger Slice 16 Delivered

This is the thirty-first committed rule slice inside P7.9.6. It adds Ivern / 翠神's conquest-or-hold Brush battlefield replacement trigger, using the official `UNL·T03` token identity from `P6TokenFactoryCatalog`.

- Added Ivern legend trigger support for:
  - `UNL-195/219`
  - `UNL-233/219`
  - `UNL-233*/219`
- When Ivern's controller conquers or holds a battlefield and controls an active Ivern legend, the backend exhausts Ivern and creates a `草丛` battlefield token in that player's battlefield zone.
- The created token uses official card number `UNL·T03`, carries `CARD_TYPE:BATTLEFIELD`, `草丛`, and `REPLACES_BATTLEFIELD:{battlefieldId}` tags, and does not masquerade as a unit.
- The event stream records `LEGEND_TRIGGER_RESOLVED`, `LEGEND_EXHAUSTED`, `BATTLEFIELD_TOKEN_CREATED`, and `BATTLEFIELD_REPLACED` with source, battlefield, token, trigger, and replacement payloads for UI rendering.
- The trigger is skipped when Ivern is exhausted. The Brush token's own battlefield static/replacement text remains a battlefield-domain concern for P7.9.7/P7.9.8; no frontend rule inference is added.
- Migrated this legend trigger/battlefield-token slice in `BehaviorSpec`:
  - Implemented functional units: `756/811`
  - Manual deferred functional units: `55/811`
  - Implemented official entries: `949/1009`
  - Manual deferred official entries: `60/1009`
  - Legend rule-domain implemented: `43` functional units / `103` entries
  - Remaining legend manual deferred: `1` functional unit / `3` entries
  - Remaining battlefield manual deferred: `54` functional units / `57` entries

P7.9.6 automatic-trigger slice 16 validation:

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79LegendTriggerIvern"`: passed `3/3`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2577/2577`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `28/28`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2684/2684`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this backend battle-trigger rule slice. It reuses existing `DECLARE_BATTLE` UI flow and emits authoritative event/snapshot changes; later battlefield UI work should render Brush replacement as a battlefield object rather than a unit.

## P7.9.6 Automatic/Replacement Slice 17 Delivered

This is the thirty-second committed rule slice inside P7.9.6 and closes the remaining legend-domain manual deferred unit. It adds Sett / 腕豪's Boon-unit replacement and conquest ready trigger while keeping battle resolution server-authoritative.

- Added Sett legend support for:
  - `OGN·269/298`
  - `OGN·310/298`
  - `OGN·310*/298`
- When a controlled Boon unit would be destroyed by lethal battle damage and the controller has an active Sett legend plus `1` mana, the backend pays `1` mana, removes Boon, reduces that unit's power by `1`, clears its damage, exhausts it, moves it to base, and exhausts Sett.
- When Sett's controller conquers a battlefield with Sett exhausted, the backend readies Sett through the existing battle-result trigger path.
- The event stream records `LEGEND_TRIGGER_RESOLVED`, `COST_PAID`, `BOON_CONSUMED`, `LEGEND_EXHAUSTED`, `UNIT_RECALLED_TO_BASE`, and `LEGEND_READIED` as applicable so UI/event-log work can render the replacement and ready result without client-side rule inference.
- The replacement is skipped when the unit is not Boon, Sett is exhausted, or the controller lacks mana; no frontend rule inference is added.
- Migrated this legend replacement/trigger slice in `BehaviorSpec`:
  - Implemented functional units: `757/811`
  - Manual deferred functional units: `54/811`
  - Implemented official entries: `952/1009`
  - Manual deferred official entries: `57/1009`
  - Legend rule-domain implemented: `44` functional units / `106` entries
  - Remaining legend manual deferred: `0` functional units / `0` entries
  - Remaining battlefield manual deferred: `54` functional units / `57` entries

P7.9.6 automatic/replacement slice 17 validation:

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79LegendTriggerSett"`: passed `3/3`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2580/2580`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `28/28`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2687/2687`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this backend battle-trigger/replacement slice. It reuses the existing `DECLARE_BATTLE` UI flow and emits authoritative event/snapshot changes; P7.9.7 battlefield UI work should render recalled exhausted Boon units, Sett exhaustion/readying, and replacement event grouping from server events.

## P7.9.7 Battlefield Foundation Slice 1 Delivered

This is the first committed rule slice inside P7.9.7. It starts the battlefield rule domain by allowing server-known battlefield card objects to be selected as `DECLARE_BATTLE.battlefieldId` destinations and implements the first no-cost held-battlefield trigger.

- Added battlefield object support for battle declaration:
  - existing compatibility destination `BATTLEFIELD:{playerId}-MAIN` remains accepted
  - actual battlefield card object ids are accepted when the object is on the field and has `CARD_TYPE:BATTLEFIELD` or an implemented battlefield card identity
  - structured `ActionPrompt` `DECLARE_BATTLE` destinations now include public battlefield card object choices
- Added the first implemented battlefield card:
  - `OGN·280/298`
  - when the defending player holds that battlefield, the backend emits `BATTLEFIELD_HELD`, resolves `BATTLEFIELD_HELD_DRAW_ONE`, and draws one card for the holder
- Added a `battlefield-held-draw` local development seed so GameHub/prompt tests can exercise the battlefield destination and trigger path without client-side rule inference.
- Migrated this battlefield held-trigger slice in `BehaviorSpec`:
  - Implemented functional units: `758/811`
  - Manual deferred functional units: `53/811`
  - Implemented official entries: `953/1009`
  - Manual deferred official entries: `56/1009`
  - Battlefield rule-domain implemented: `1` functional unit / `1` entry
  - Remaining battlefield manual deferred: `53` functional units / `56` entries

P7.9.7 battlefield foundation slice 1 validation:

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldHeldDraw"`: passed `2/2`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2581/2581`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `29/29`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2689/2689`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this backend/prompt seed slice. GameHub coverage verifies the structured destination candidate and submitted object-id path; the existing UI already renders prompt destinations and battle events from server data.

## P7.9.7 Battlefield Foundation Slice 2 Delivered

This is the second rule slice inside P7.9.7. It adds the first conquest-trigger battlefield effect on the battlefield object path established by slice 1.

- Added implemented battlefield card:
  - `SFD·212/221`
- When a player conquers this battlefield, the backend moves the top two cards of that player's main deck to their graveyard.
- The event stream records `BATTLEFIELD_CONQUERED`, `BATTLEFIELD_TRIGGER_RESOLVED` with trigger `BATTLEFIELD_CONQUERED_MILL_TOP_TWO`, and `CARDS_MILLED` with source/destination zone payloads.
- Added a `battlefield-conquer-mill` local development seed and GameHub coverage for the battlefield object destination and submitted object-id path.
- Migrated this battlefield conquer-trigger slice in `BehaviorSpec`:
  - Implemented functional units: `759/811`
  - Manual deferred functional units: `52/811`
  - Implemented official entries: `954/1009`
  - Manual deferred official entries: `55/1009`
  - Battlefield rule-domain implemented: `2` functional units / `2` entries
  - Remaining battlefield manual deferred: `52` functional units / `55` entries

P7.9.7 battlefield foundation slice 2 validation:

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldConquerMill"`: passed `2/2`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2582/2582`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `30/30`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2691/2691`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this backend/prompt seed slice. GameHub coverage verifies the structured battlefield destination and the `DECLARE_BATTLE` object-id submit path.

## P7.9.7 Battlefield Foundation Slice 3 Delivered

This is the third rule slice inside P7.9.7. It adds the first static battlefield combat modifier on the same battlefield-object destination path.

- Added implemented battlefield card:
  - `OGN·294/298`
- When battle is declared at this battlefield object, the backend gives all participating unit card objects `+1` static combat power for that battle calculation.
- The static bonus is included in authoritative `DAMAGE_APPLIED` payloads as `staticPowerBonus`, and the frontend can render it from server events without doing rule inference.
- Added a `battlefield-static-power` local development seed and GameHub coverage for prompt destination exposure and submitted object-id battle resolution.
- Migrated this battlefield static slice in `BehaviorSpec`:
  - Implemented functional units: `760/811`
  - Manual deferred functional units: `51/811`
  - Implemented official entries: `955/1009`
  - Manual deferred official entries: `54/1009`
  - Battlefield rule-domain implemented: `3` functional units / `3` entries
  - Remaining battlefield manual deferred: `51` functional units / `54` entries

P7.9.7 battlefield foundation slice 3 validation:

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldStaticPower"`: passed `2/2`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2583/2583`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `31/31`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2693/2693`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this backend/prompt seed slice. GameHub coverage verifies the structured battlefield destination and `DECLARE_BATTLE` object-id submit path, including server-emitted static combat bonus events.

## P7.9.7 Battlefield Foundation Slice 4 Delivered

This is the fourth rule slice inside P7.9.7. It adds deterministic held-battlefield token/resource effects that reuse the battlefield-object destination path and `BATTLEFIELD_HELD` event model.

- Added implemented battlefield cards:
  - `OGN·275/298`: when the controller holds this battlefield, the backend creates a `1` power `随从` unit token in that player's base using official token identity `OGN·271/298`.
  - `SFD·219/221`: when the controller holds this battlefield, the backend calls one dormant rune for each player in holder-first order.
- Event stream additions:
  - `BATTLEFIELD_TRIGGER_RESOLVED` with `BATTLEFIELD_HELD_CREATE_MINION`
  - `UNIT_TOKEN_CREATED` with `tokenCardNo`, `tokenObjectId`, `power`, and destination payloads
  - `BATTLEFIELD_TRIGGER_RESOLVED` with `BATTLEFIELD_HELD_EACH_PLAYER_CALL_RUNE`
  - `RUNES_CALLED` for each affected player
- Added `battlefield-held-minion` and `battlefield-held-runes` local development seeds plus GameHub coverage for prompt destination exposure and submitted object-id battle resolution.
- Migrated these battlefield held-trigger slices in `BehaviorSpec`:
  - Implemented functional units: `762/811`
  - Manual deferred functional units: `49/811`
  - Implemented official entries: `957/1009`
  - Manual deferred official entries: `52/1009`
  - Battlefield rule-domain implemented: `5` functional units / `5` entries
  - Remaining battlefield manual deferred: `49` functional units / `52` entries

P7.9.7 battlefield foundation slice 4 validation:

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldHeld"`: passed `6/6`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2585/2585`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `33/33`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2697/2697`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this backend/prompt seed slice. GameHub coverage verifies the structured battlefield destinations and `DECLARE_BATTLE` object-id submit paths for both held-token and held-rune effects.

## P7.9.7 Battlefield Foundation Slice 5 Delivered

This is the fifth rule slice inside P7.9.7. It adds the first deterministic discard/draw conquest battlefield effect on the same battlefield-object destination path.

- Added implemented battlefield card:
  - `OGN·298/298`: when the controller conquers this battlefield, the backend discards that player's first hand card if one exists, then draws one card.
- Event stream additions:
  - `BATTLEFIELD_TRIGGER_RESOLVED` with `BATTLEFIELD_CONQUERED_DISCARD_DRAW`
  - `CARD_DISCARDED` with source battlefield object, discarded object id, reason, and graveyard destination
  - existing authoritative `CARD_DRAWN`/burnout events from `ApplyDrawToPlayer`
- Added `battlefield-conquer-discard-draw` local development seed plus GameHub coverage for prompt destination exposure and submitted object-id battle resolution.
- Migrated this battlefield conquer-trigger slice in `BehaviorSpec`:
  - Implemented functional units: `763/811`
  - Manual deferred functional units: `48/811`
  - Implemented official entries: `958/1009`
  - Manual deferred official entries: `51/1009`
  - Battlefield rule-domain implemented: `6` functional units / `6` entries
  - Remaining battlefield manual deferred: `48` functional units / `51` entries

P7.9.7 battlefield foundation slice 5 validation:

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldConquerDiscardDraw"`: passed `1/1`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldConquerDiscards"`: passed `1/1`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2586/2586`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `34/34`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2699/2699`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this backend/prompt seed slice. GameHub coverage verifies the structured battlefield destination and `DECLARE_BATTLE` object-id submit path for the conquest discard/draw effect.

## P7.9.7 Battlefield Foundation Slice 6 Delivered

This is the sixth rule slice inside P7.9.7. It adds the first battlefield static keyword grant that modifies combat damage without adding frontend rule authority.

- Added implemented battlefield card:
  - `UNL-208/219`: when battle is declared at this battlefield object, defending `瞬息` unit card objects gain `坚守` for combat power calculation.
- The bonus is represented through the authoritative `DAMAGE_APPLIED` payload:
  - `keyword = "坚守"`
  - `keywordBonus = 1`
  - adjusted `combatPower`
- Added `battlefield-ephemeral-steadfast` local development seed plus GameHub coverage for prompt destination exposure and submitted object-id battle resolution.
- Migrated this battlefield static/keyword slice in `BehaviorSpec`:
  - Implemented functional units: `764/811`
  - Manual deferred functional units: `47/811`
  - Implemented official entries: `959/1009`
  - Manual deferred official entries: `50/1009`
  - Battlefield rule-domain implemented: `7` functional units / `7` entries
  - Remaining battlefield manual deferred: `47` functional units / `50` entries

P7.9.7 battlefield foundation slice 6 validation:

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldEphemeral"`: passed `2/2`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2587/2587`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `35/35`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2701/2701`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this backend/prompt seed slice. GameHub coverage verifies the structured battlefield destination and `DECLARE_BATTLE` object-id submit path for the static keyword-grant effect.

## P7.9.7 Battlefield Foundation Slice 7 Delivered

This is the seventh rule slice inside P7.9.7. It adds the first battlefield effect that requires a server-validated battlefield target choice during battle declaration.

- Added implemented battlefield card:
  - `OGN·279/298`: when the controller defends at this battlefield object, the backend selects one defending unit through `DECLARE_BATTLE.battlefieldTargetObjectIds`; if there is exactly one defender and no explicit choice, the server deterministically selects that defender. The selected defender gets `坚守2` for combat power calculation.
- Extended the authoritative battle command contract:
  - `DeclareBattleCommand.BattlefieldTargetObjectIds`
  - `GameCommandJsonMapper` support for `battlefieldTargetObjectIds`
  - Dev UI battle draft field and server-target chips for submitting battlefield effect targets without client-side rule adjudication.
- Event stream additions:
  - `BATTLEFIELD_TRIGGER_RESOLVED` with `BATTLEFIELD_DEFENSE_GRANT_STEADFAST_TWO`
  - `BATTLE_DECLARED` now echoes the accepted `battlefieldTargetObjectIds`
  - authoritative `DAMAGE_APPLIED` payloads expose `keyword = "坚守"`, `keywordBonus = 2`, and adjusted `combatPower`.
- Added `battlefield-defender-steadfast` local development seed plus GameHub coverage for prompt destination exposure, target choice submission, and damage payload verification.
- Migrated this battlefield trigger/static keyword slice in `BehaviorSpec`:
  - Implemented functional units: `765/811`
  - Manual deferred functional units: `46/811`
  - Implemented official entries: `960/1009`
  - Manual deferred official entries: `49/1009`
  - Battlefield rule-domain implemented: `8` functional units / `8` entries
  - Remaining battlefield manual deferred: `46` functional units / `49` entries

P7.9.7 battlefield foundation slice 7 validation:

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldDefender"`: passed `2/2`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2588/2588`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `36/36`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2703/2703`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this backend/protocol/prompt seed slice. GameHub coverage verifies the structured battlefield destination, server-side battlefield target submission, and authoritative event/snapshot path; the Dev UI build verifies the new battlefield target field compiles.

## P7.9.7 Battlefield Foundation Slice 8 Delivered

This is the eighth rule slice inside P7.9.7. It adds the first battlefield conquest effect that recycles an already-called rune.

- Added implemented battlefield card:
  - `OGN·287/298`: when the controller conquers this battlefield, the backend recycles that player's first rune object from base to the bottom of the main deck.
- Event stream additions:
  - `BATTLEFIELD_TRIGGER_RESOLVED` with `BATTLEFIELD_CONQUERED_RECYCLE_RUNE`
  - `CARDS_RECYCLED` with the source battlefield object, recycled rune object id, base source zone, and main-deck destination zone
- Added `battlefield-conquer-recycle-rune` local development seed plus GameHub coverage for prompt destination exposure, `DECLARE_BATTLE` submission, recycle event payload, hidden main-deck count, and base-zone removal.
- Migrated this battlefield conquest/recycle slice in `BehaviorSpec`:
  - Implemented functional units: `766/811`
  - Manual deferred functional units: `45/811`
  - Implemented official entries: `961/1009`
  - Manual deferred official entries: `48/1009`
  - Battlefield rule-domain implemented: `9` functional units / `9` entries
  - Remaining battlefield manual deferred: `45` functional units / `48` entries

P7.9.7 battlefield foundation slice 8 validation:

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldConquerRecycl"`: passed `2/2`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2589/2589`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2705/2705`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this backend/prompt seed slice. GameHub coverage verifies the structured battlefield destination and submitted object-id battle resolution for the conquest rune-recycle effect.

## P7.9.7 Battlefield Foundation Slice 9 Delivered

This is the ninth rule slice inside P7.9.7. It adds a defending battlefield top-deck reveal branch with both draw and recycle outcomes.

- Added implemented battlefield card:
  - `SFD·215/221`: when the controller defends at this battlefield object, the backend reveals that player's top main-deck card. If it is a spell card, it moves to hand; otherwise it is recycled to the bottom of the main deck.
- Event stream additions:
  - `BATTLEFIELD_TRIGGER_RESOLVED` with `BATTLEFIELD_DEFENSE_REVEAL_TOP_DRAW_SPELL_OR_RECYCLE`
  - `CARDS_REVEALED` for the top-deck card
  - `CARD_DRAWN` for the spell branch
  - `CARDS_RECYCLED` for the non-spell branch
- Added `battlefield-defend-reveal-spell` local development seed plus GameHub coverage for prompt destination exposure, top-card reveal, spell draw event, hidden main-deck count, and resulting hand state.
- Migrated this battlefield defending-trigger/recycle slice in `BehaviorSpec`:
  - Implemented functional units: `767/811`
  - Manual deferred functional units: `44/811`
  - Implemented official entries: `962/1009`
  - Manual deferred official entries: `47/1009`
  - Battlefield rule-domain implemented: `10` functional units / `10` entries
  - Remaining battlefield manual deferred: `44` functional units / `47` entries

P7.9.7 battlefield foundation slice 9 validation:

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldDefendReveal"`: passed `3/3`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2591/2591`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `38/38`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2708/2708`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this backend/prompt seed slice. GameHub coverage verifies the structured battlefield destination and submitted object-id battle resolution for the defending top-deck reveal/draw path.

## P7.9.7 Battlefield Foundation Slice 10 Delivered

This is the tenth rule slice inside P7.9.7. It adds a defending battlefield static penalty that changes combat damage for an isolated defender without adding frontend rule authority.

- Added implemented battlefield card:
  - `UNL-210/219`: when a player defends at this battlefield object with exactly one defending unit, that isolated defender gets `坚守 -2` for combat power calculation.
- The penalty is resolved inside `CoreRuleEngine.ResolveBattleCombatPower`; the client still only submits `DECLARE_BATTLE` with server-prompted battlefield destinations.
- Added `battlefield-isolated-defender` local development seed plus GameHub coverage for prompt destination exposure, submitted object-id battle resolution, and authoritative damage payload verification.
- Migrated this battlefield static/keyword-penalty slice in `BehaviorSpec`:
  - Implemented functional units: `768/811`
  - Manual deferred functional units: `43/811`
  - Implemented official entries: `963/1009`
  - Manual deferred official entries: `46/1009`
  - Battlefield rule-domain implemented: `11` functional units / `11` entries
  - Remaining battlefield manual deferred: `43` functional units / `46` entries

P7.9.7 battlefield foundation slice 10 validation:

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldIsolated"`: passed `2/2`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2592/2592`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `39/39`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2710/2710`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this backend/prompt seed slice. GameHub coverage verifies the structured battlefield destination and submitted object-id battle resolution for the isolated-defender static penalty.

## P7.9.7 Battlefield Foundation Slice 11 Delivered

This is the eleventh rule slice inside P7.9.7. It adds a conquest-trigger battlefield cost/payment effect that readies the controller's legend through server-authoritative battle resolution.

- Added implemented battlefield card:
  - `SFD·210/221`: when a player conquers this battlefield object, the backend pays `1` mana if available and readies that player's first exhausted legend.
- Event stream additions:
  - `BATTLEFIELD_TRIGGER_RESOLVED` with `BATTLEFIELD_CONQUERED_PAY_1_READY_LEGEND`
  - `COST_PAID` for the battlefield trigger cost
  - `LEGEND_READIED` for the readied legend object
- Added `battlefield-conquer-ready-legend` local development seed plus GameHub coverage for prompt destination exposure, submitted object-id battle resolution, mana payment, and final legend ready state.
- Migrated this battlefield conquest/payment slice in `BehaviorSpec`:
  - Implemented functional units: `769/811`
  - Manual deferred functional units: `42/811`
  - Implemented official entries: `964/1009`
  - Manual deferred official entries: `45/1009`
  - Battlefield rule-domain implemented: `12` functional units / `12` entries
  - Remaining battlefield manual deferred: `42` functional units / `45` entries

P7.9.7 battlefield foundation slice 11 validation:

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldConquerReadyLegend"`: passed `2/2`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2593/2593`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `40/40`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2712/2712`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this backend/prompt seed slice. GameHub coverage verifies the structured battlefield destination, payment event, and authoritative snapshot update for the ready-legend path.

## P7.9.7 Battlefield Foundation Slice 12 Delivered

This is the twelfth rule slice inside P7.9.7. It adds a conquest-trigger battlefield draw effect based on other controlled battlefield objects.

- Added implemented battlefield card:
  - `SFD·217/221`: when a player conquers this battlefield object, the backend counts that player's other controlled battlefield card objects and draws that many cards.
- Event stream additions:
  - `BATTLEFIELD_TRIGGER_RESOLVED` with `BATTLEFIELD_CONQUERED_DRAW_FOR_OTHER_BATTLEFIELDS`, `otherBattlefieldObjectIds`, and `drawCount`
  - `CARD_DRAWN` from the existing authoritative draw path
- Added `battlefield-conquer-draw-other` local development seed plus GameHub coverage for prompt destination exposure, submitted object-id battle resolution, draw event count, and final hand/main-deck snapshot.
- Migrated this battlefield conquest/draw slice in `BehaviorSpec`:
  - Implemented functional units: `770/811`
  - Manual deferred functional units: `41/811`
  - Implemented official entries: `965/1009`
  - Manual deferred official entries: `44/1009`
  - Battlefield rule-domain implemented: `13` functional units / `13` entries
  - Remaining battlefield manual deferred: `41` functional units / `44` entries

P7.9.7 battlefield foundation slice 12 validation:

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldConquerDraw"`: passed `2/2`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2594/2594`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `41/41`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2714/2714`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this backend/prompt seed slice. GameHub coverage verifies the structured battlefield destination and authoritative event/snapshot update for the draw-for-other-battlefields path.

## P7.9.7 Battlefield Foundation Slice 13 Delivered

This is the thirteenth rule slice inside P7.9.7. It adds a conquest-trigger battlefield payment/draw effect gated by a surviving powerful unit.

- Added implemented battlefield card:
  - `SFD·218/221`: when a player conquers this battlefield object and the surviving attacker is a powerful unit, the backend pays `1` mana if available and draws `1` card.
- Event stream additions:
  - `BATTLEFIELD_TRIGGER_RESOLVED` with `BATTLEFIELD_CONQUERED_POWERFUL_PAY_1_DRAW`, `powerfulObjectId`, and `drawCount`
  - `COST_PAID` for the battlefield trigger cost
  - `CARD_DRAWN` from the existing authoritative draw path
- Added `battlefield-conquer-powerful-draw` local development seed plus GameHub coverage for prompt destination exposure, submitted object-id battle resolution, cost payment, draw event, and final hand/mana snapshot.
- Migrated this battlefield conquest/payment/draw slice in `BehaviorSpec`:
  - Implemented functional units: `771/811`
  - Manual deferred functional units: `40/811`
  - Implemented official entries: `966/1009`
  - Manual deferred official entries: `43/1009`
  - Battlefield rule-domain implemented: `14` functional units / `14` entries
  - Remaining battlefield manual deferred: `40` functional units / `43` entries

P7.9.7 battlefield foundation slice 13 validation:

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldConquerPowerful"`: passed `2/2`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2595/2595`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `42/42`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2716/2716`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this backend/prompt seed slice. GameHub coverage verifies the structured battlefield destination, payment event, and authoritative event/snapshot update for the powerful-unit draw path.

## P7.9.7 Battlefield Foundation Slice 14 Delivered

This is the fourteenth rule slice inside P7.9.7. It adds a conquest-trigger battlefield payment effect that creates a dormant Gold equipment token.

- Added implemented battlefield card:
  - `SFD·220/221`: when a player conquers this battlefield object, the backend pays `1` mana if available and creates a dormant `金币` equipment token in that player's base.
- Event stream additions:
  - `BATTLEFIELD_TRIGGER_RESOLVED` with `BATTLEFIELD_CONQUERED_PAY_1_CREATE_GOLD`
  - `COST_PAID` for the battlefield trigger cost
  - `EQUIPMENT_TOKEN_CREATED` with `abilityId = BATTLEFIELD_CONQUERED_PAY_1_CREATE_GOLD`
- Added `battlefield-conquer-gold` local development seed plus GameHub coverage for prompt destination exposure, submitted object-id battle resolution, cost payment, token event, and final base/mana snapshot.
- Migrated this battlefield conquest/payment/token slice in `BehaviorSpec`:
  - Implemented functional units: `772/811`
  - Manual deferred functional units: `39/811`
  - Implemented official entries: `967/1009`
  - Manual deferred official entries: `42/1009`
  - Battlefield rule-domain implemented: `15` functional units / `15` entries
  - Remaining battlefield manual deferred: `39` functional units / `42` entries

P7.9.7 battlefield foundation slice 14 validation:

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldConquerGold"`: passed `2/2`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2596/2596`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `43/43`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2718/2718`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this backend/prompt seed slice. GameHub coverage verifies the structured battlefield destination, payment event, token creation event, and authoritative snapshot update for the Gold-token path.

## P7.9.7 Battlefield Foundation Slice 15 Delivered

This is the fifteenth rule slice inside P7.9.7. It adds a held-battlefield trigger that calls exactly one rune for the player who holds the battlefield.

- Added implemented battlefield card:
  - `OGN·288/298`: when a player holds this battlefield object, the backend calls `1` rune for that holder only.
- Event stream additions:
  - `BATTLEFIELD_TRIGGER_RESOLVED` with `BATTLEFIELD_HELD_CALL_RUNE`
  - `RUNES_CALLED` for the holder, with `runeObjectIds` listing the called rune
- Added `battlefield-held-rune` local development seed plus GameHub coverage for prompt destination exposure, submitted object-id battle resolution, holder-only rune call event, and final base snapshot.
- Migrated this battlefield held/rune slice in `BehaviorSpec`:
  - Implemented functional units: `773/811`
  - Manual deferred functional units: `38/811`
  - Implemented official entries: `968/1009`
  - Manual deferred official entries: `41/1009`
  - Battlefield rule-domain implemented: `16` functional units / `16` entries
  - Remaining battlefield manual deferred: `38` functional units / `41` entries

P7.9.7 battlefield foundation slice 15 validation:

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldHeldRune"`: passed `2/2`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2597/2597`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `44/44`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2720/2720`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this backend/prompt seed slice. GameHub coverage verifies the structured battlefield destination, holder-only rune event, and authoritative snapshot update for the held-rune path.

## P7.9.7 Battlefield Foundation Slice 16 Delivered

This is the sixteenth rule slice inside P7.9.7. It adds a conquest-trigger battlefield equipment reward and keeps equipment ready/detach state server-authoritative.

- Added implemented battlefield card:
  - `SFD·221/221`: when a player conquers this battlefield object, the backend readies the first exhausted friendly equipment; if that equipment is an attached `武装`, it is detached.
- Event stream additions:
  - `BATTLEFIELD_TRIGGER_RESOLVED` with `BATTLEFIELD_CONQUERED_READY_EQUIPMENT`
  - `EQUIPMENT_READIED` for the chosen friendly equipment
  - `EQUIPMENT_DETACHED` when the chosen ready equipment is an attached armament
- Added `battlefield-conquer-ready-equipment` local development seed plus GameHub coverage for prompt destination exposure, submitted object-id battle resolution, equipment ready event, armament detach event, and final equipment snapshot.
- Migrated this battlefield conquest/equipment slice in `BehaviorSpec`:
  - Implemented functional units: `774/811`
  - Manual deferred functional units: `37/811`
  - Implemented official entries: `969/1009`
  - Manual deferred official entries: `40/1009`
  - Battlefield rule-domain implemented: `17` functional units / `17` entries
  - Remaining battlefield manual deferred: `37` functional units / `40` entries

P7.9.7 battlefield foundation slice 16 validation:

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldConquerReadyEquipment"`: passed `1/1`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldConquerReadiesAndDetachesEquipment"`: passed `1/1`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2598/2598`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `45/45`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2722/2722`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this backend/prompt seed slice. GameHub coverage verifies the structured battlefield destination, equipment ready/detach events, and authoritative snapshot update for the armament-detach path.

## P7.9.7 Battlefield Foundation Slice 17 Delivered

This is the seventeenth rule slice inside P7.9.7. It adds a held-battlefield boon trigger and reuses the existing server-side Boon model.

- Added implemented battlefield card:
  - `OGN·283/298`: when a player holds this battlefield object, the backend grants Boon to the surviving defender at that battlefield; if it did not already have Boon, it gains `+1` power.
- Event stream additions:
  - `BATTLEFIELD_TRIGGER_RESOLVED` with `BATTLEFIELD_HELD_GRANT_BOON`
  - `BOON_GRANTED` for the selected battlefield unit, including `alreadyHadBoon`
- Added `battlefield-held-boon` local development seed plus GameHub coverage for prompt destination exposure, submitted object-id battle resolution, Boon event, and final unit power/tag snapshot.
- Migrated this battlefield held/boon slice in `BehaviorSpec`:
  - Implemented functional units: `775/811`
  - Manual deferred functional units: `36/811`
  - Implemented official entries: `970/1009`
  - Manual deferred official entries: `39/1009`
  - Battlefield rule-domain implemented: `18` functional units / `18` entries
  - Remaining battlefield manual deferred: `36` functional units / `39` entries
  - Secondary template families moved forward: `Boon` and `TempMight` each gained one implemented representative.

P7.9.7 battlefield foundation slice 17 validation:

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldHeldBoon"`: passed `1/1`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldHeldGrantsBoon"`: passed `1/1`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2599/2599`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `46/46`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2724/2724`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this backend/prompt seed slice. GameHub coverage verifies the structured battlefield destination, Boon event, and authoritative snapshot update for the held-boon path.

## P7.9.7 Battlefield Foundation Slice 18 Delivered

This is the eighteenth rule slice inside P7.9.7. It adds a defending-battlefield optional movement target on the server-authoritative battle path.

- Added implemented battlefield card:
  - `OGN·285/298`: when a player defends this battlefield object and submits a valid battlefield target, the backend moves the chosen surviving friendly defender from battlefield to base after combat damage is cleaned up.
- Event stream additions:
  - `BATTLEFIELD_TRIGGER_RESOLVED` with `BATTLEFIELD_DEFENSE_MOVE_FRIENDLY_UNIT_TO_BASE`
  - `UNIT_MOVED_TO_BASE` with `originZone = BATTLEFIELD`, `destinationZone = BASE`, and the battlefield object as source.
- Added `battlefield-defend-move-to-base` local development seed plus GameHub coverage for prompt destination exposure, target choice submission, movement event, and final base/battlefield snapshot.
- Migrated this battlefield defense/move slice in `BehaviorSpec`:
  - Implemented functional units: `776/811`
  - Manual deferred functional units: `35/811`
  - Implemented official entries: `971/1009`
  - Manual deferred official entries: `38/1009`
  - Battlefield rule-domain implemented: `19` functional units / `19` entries
  - Remaining battlefield manual deferred: `35` functional units / `38` entries
  - Secondary template family moved forward: `Move` gained one implemented representative.

P7.9.7 battlefield foundation slice 18 validation:

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldDefendMove"`: passed `2/2`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2600/2600`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `47/47`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2726/2726`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated yet for this backend/prompt seed slice. GameHub coverage verifies the structured battlefield destination, chosen battlefield target, movement event, and authoritative snapshot update for the defend-move path.

## P7.9.7 Battlefield Foundation Slice 19 Delivered

This is the nineteenth rule slice inside P7.9.7. It adds a conquest-trigger Boon consumption draw effect and keeps the Boon/power mutation server-authoritative.

- Added implemented battlefield card:
  - `OGN·282/298`: when a player conquers this battlefield object, the backend consumes one controlled Boon and draws one card.
- Event stream additions:
  - `BATTLEFIELD_TRIGGER_RESOLVED` with `BATTLEFIELD_CONQUERED_CONSUME_BOON_DRAW`
  - `BOON_CONSUMED` with previous and resulting power
  - `CARD_DRAWN` for the conquering player
- Added `battlefield-conquer-boon-draw` local development seed plus GameHub coverage for prompt destination exposure, submitted object-id battle resolution, Boon consumption, draw event, and final hand/object snapshot.
- Migrated this battlefield conquest/Boon/draw slice in `BehaviorSpec`:
  - Implemented functional units: `777/811`
  - Manual deferred functional units: `34/811`
  - Implemented official entries: `972/1009`
  - Manual deferred official entries: `37/1009`
  - Battlefield rule-domain implemented: `20` functional units / `20` entries
  - Remaining battlefield manual deferred: `34` functional units / `37` entries
  - Secondary template families moved forward: `Boon` and `Draw` each gained one implemented representative.

P7.9.7 battlefield foundation slice 19 validation:

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldConquerBoonDraw"`: passed `1/1`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldConquerConsumesBoon"`: passed `1/1`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2601/2601`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `48/48`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2728/2728`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this backend/prompt seed slice. GameHub coverage verifies the structured battlefield destination, Boon consumption event, draw event, and authoritative snapshot update for the conquer-boon-draw path.

## P7.9.7 Battlefield Foundation Slice 20 Delivered

This is the twentieth rule slice inside P7.9.7. It adds a held-battlefield movement trigger on the same server-authoritative battle result path.

- Added implemented battlefield card:
  - `UNL-207/219`: when a player holds this battlefield object, the backend moves the first surviving battlefield unit among the battle participants to its controller's base.
- Event stream additions:
  - `BATTLEFIELD_TRIGGER_RESOLVED` with `BATTLEFIELD_HELD_MOVE_UNIT_TO_BASE`
  - `UNIT_MOVED_TO_BASE` with `originZone = BATTLEFIELD`, `destinationZone = BASE`, and the battlefield object as source.
- Added `battlefield-held-move-to-base` local development seed plus GameHub coverage for prompt destination exposure, submitted object-id battle resolution, movement event, and final base/battlefield snapshot.
- Migrated this battlefield held/move slice in `BehaviorSpec`:
  - Implemented functional units: `778/811`
  - Manual deferred functional units: `33/811`
  - Implemented official entries: `973/1009`
  - Manual deferred official entries: `36/1009`
  - Battlefield rule-domain implemented: `21` functional units / `21` entries
  - Remaining battlefield manual deferred: `33` functional units / `36` entries
  - Secondary template family moved forward: `Move` gained one implemented representative.

P7.9.7 battlefield foundation slice 20 validation:

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldHeldMove"`: passed `2/2`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2602/2602`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `49/49`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2730/2730`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this backend/prompt seed slice. GameHub coverage verifies the structured battlefield destination, held movement event, and authoritative snapshot update for the held-move path.

## P7.9.7 Battlefield Foundation Slice 21 Delivered

This is the twenty-first rule slice inside P7.9.7. It adds a conquest-overkill battlefield trigger that creates a Spellshield Warhawk token from the backend battle result.

- Added implemented battlefield card:
  - `UNL-217/219`: when a player conquers this battlefield object after assigning at least `3` overkill damage to enemy units, the backend creates a `1` power `战鹰` unit token with `Spellshield` in that player's battlefield zone.
- Event stream additions:
  - `BATTLEFIELD_TRIGGER_RESOLVED` with `BATTLEFIELD_CONQUERED_OVERKILL_CREATE_WARHAWK`
  - `UNIT_TOKEN_CREATED` with `tokenCardNo = UNL·T02`, `destinationZone = BATTLEFIELD`, `power = 1`, and the authoritative token tags
- Added `battlefield-conquer-warhawk` local development seed plus GameHub coverage for prompt destination exposure, submitted object-id battle resolution, overkill trigger event, token creation event, and final battlefield snapshot.
- Migrated this battlefield conquer/overkill/token slice in `BehaviorSpec`:
  - Implemented functional units: `779/811`
  - Manual deferred functional units: `32/811`
  - Implemented official entries: `974/1009`
  - Manual deferred official entries: `35/1009`
  - Battlefield rule-domain implemented: `22` functional units / `22` entries
  - Remaining battlefield manual deferred: `32` functional units / `35` entries
  - Secondary template family moved forward: `Damage` gained one implemented representative.

P7.9.7 battlefield foundation slice 21 validation:

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldConquerWarhawk"`: passed `1/1`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldConquerOverkill"`: passed `1/1`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2603/2603`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `50/50`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2732/2732`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this backend/prompt seed slice. GameHub coverage verifies the structured battlefield destination, conquest-overkill trigger event, Warhawk token creation event, and authoritative snapshot update for the battlefield token path.

## P7.9.7 Battlefield Foundation Slice 22 Delivered

This is the twenty-second rule slice inside P7.9.7. It adds the first battlefield static effect that changes the match victory threshold and exposes that server-computed threshold in snapshots.

- Added implemented battlefield cards:
  - `OGN·276/298` and `OGN·276a/298`: while these battlefield objects are present on the battlefield, the backend raises the effective winning score by `1` per object.
- Server-authoritative score threshold changes:
  - Burnout scoring now checks `EffectiveWinningScore` instead of the fixed base `8`.
  - `MATCH_WON` events emit the effective `winningScore`.
  - Player snapshots expose `timing.winningScore` so the UI can display the authoritative threshold without inferring battlefield rules.
  - Existing "within three of winning score" conditions now use the effective score threshold.
- Added `battlefield-winning-score` local development seed plus GameHub coverage for snapshot threshold exposure, burnout scoring to `8`, no premature win, and continued actionable prompt flow.
- Migrated this battlefield static/score-threshold slice in `BehaviorSpec`:
  - Implemented functional units: `780/811`
  - Manual deferred functional units: `31/811`
  - Implemented official entries: `976/1009`
  - Manual deferred official entries: `33/1009`
  - Battlefield rule-domain implemented: `23` functional units / `24` entries
  - Remaining battlefield manual deferred: `31` functional units / `33` entries
  - Duplicate battlefield group moved to implemented: `OGN·276/298` plus `OGN·276a/298`.

P7.9.7 battlefield foundation slice 22 validation:

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldStaticWinningScore|FullyQualifiedName~P79BattlefieldWinningScore"`: passed `3/3`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2605/2605`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `51/51`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2735/2735`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this backend/static snapshot slice. GameHub coverage verifies the seed, authoritative `winningScore` snapshot field, burnout score update, non-finished room state, and continued prompt flow.

## P7.9.7 Battlefield Foundation Slice 23 Delivered

This is the twenty-third rule slice inside P7.9.7. It adds the first battlefield static effect that modifies turn-start rune calls.

- Added implemented battlefield card:
  - `OGN·284/298`: during each player's first turn-start sequence, the backend calls `1` additional rune while this battlefield object is present.
- Server-authoritative rune static changes:
  - `RuneCallCount` now adds battlefield first-turn rune modifiers after applying the existing second-player first-turn baseline.
  - The extra rune is visible through the existing `RUNES_CALLED` event count and final snapshot base/rune-deck state.
  - No frontend rule inference is added; the UI consumes the authoritative event and snapshot.
- Added `battlefield-first-turn-rune` local development seed plus GameHub coverage for P2 first-turn rune calling from `3` to `4`, final base contents, and depleted rune deck count.
- Migrated this battlefield static/rune slice in `BehaviorSpec`:
  - Implemented functional units: `781/811`
  - Manual deferred functional units: `30/811`
  - Implemented official entries: `977/1009`
  - Manual deferred official entries: `32/1009`
  - Battlefield rule-domain implemented: `24` functional units / `25` entries
  - Remaining battlefield manual deferred: `30` functional units / `32` entries
  - Timing trigger coverage moved forward by one implemented battlefield representative.

P7.9.7 battlefield foundation slice 23 validation:

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldStaticFirstTurnRune|FullyQualifiedName~P79BattlefieldFirstTurnRune"`: passed `2/2`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2606/2606`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `52/52`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2737/2737`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this backend/static snapshot slice. GameHub coverage verifies the seed, authoritative rune-call event count, base contents, and rune deck count.

## P7.9.7 Battlefield Foundation Slice 24 Delivered

This is the twenty-fourth rule slice inside P7.9.7. It adds a first-turn battlefield scoring static and a player-readable score event.

- Added implemented battlefield card:
  - `OGN·290/298`: during each player's first turn-start sequence, the backend grants that player `1` score while this battlefield object is present.
- Server-authoritative score static changes:
  - Turn-start score gain happens before rune calling and draw resolution.
  - The score mutation emits `BATTLEFIELD_TRIGGER_RESOLVED` with `BATTLEFIELD_FIRST_TURN_GAIN_SCORE` and a `SCORE_GAINED` event.
  - Winning checks use the effective winning score after the static score mutation; no frontend scoring inference is added.
- Added `battlefield-first-turn-score` local development seed plus GameHub coverage for P2 first-turn score gain, event delivery, final score snapshot, and non-finished room state.
- Migrated this battlefield static/score slice in `BehaviorSpec`:
  - Implemented functional units: `782/811`
  - Manual deferred functional units: `29/811`
  - Implemented official entries: `978/1009`
  - Manual deferred official entries: `31/1009`
  - Battlefield rule-domain implemented: `25` functional units / `26` entries
  - Remaining battlefield manual deferred: `29` functional units / `31` entries
  - Timing trigger coverage moved forward by one implemented battlefield representative.

P7.9.7 battlefield foundation slice 24 validation:

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldStaticFirstTurnScore|FullyQualifiedName~P79BattlefieldFirstTurnScore"`: passed `2/2`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2607/2607`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `53/53`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2739/2739`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated for this backend/static snapshot slice. GameHub coverage verifies the seed, authoritative score event, score snapshot, and room state.

## P7.9.7 Battlefield Foundation Slice 25 Delivered

This is the twenty-fifth rule slice inside P7.9.7. It adds the first held-battlefield score payment effect that spends `Power` instead of `Mana`.

- Added implemented battlefield card:
  - `SFD·214/221`: when a player holds this battlefield, the backend pays `4` power if available and grants that player `1` additional score.
- Server-authoritative held score changes:
  - The held trigger runs on the existing `DECLARE_BATTLE` battle-winner path after the defender successfully holds the battlefield.
  - The mutation emits `BATTLEFIELD_HELD`, `BATTLEFIELD_TRIGGER_RESOLVED` with `BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE`, `COST_PAID`, and `SCORE_GAINED`.
  - Score win checks use the effective winning score; no frontend scoring or payment inference is added.
- Added `battlefield-held-score` local development seed plus GameHub coverage for prompt destination exposure, battle declaration, power payment, score event, final score snapshot, and non-finished room state.
- Migrated this battlefield held score/payment slice in `BehaviorSpec`:
  - Implemented functional units: `783/811`
  - Manual deferred functional units: `28/811`
  - Implemented official entries: `979/1009`
  - Manual deferred official entries: `30/1009`
  - Battlefield rule-domain implemented: `26` functional units / `27` entries
  - Remaining battlefield manual deferred: `28` functional units / `30` entries
  - Timing trigger coverage moved forward by one implemented battlefield representative.

P7.9.7 battlefield foundation slice 25 validation:

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldHeldPaysPowerToGainScore|FullyQualifiedName~P79BattlefieldHeldScoreSeedOffersBattlefieldDestinationAndGainsScore"`: passed `2/2`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2608/2608`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `54/54`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2741/2741`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated yet for this backend/battlefield-score slice. GameHub coverage verifies the seed, authoritative prompt destination, payment event, score snapshot, and room state.

## P7.9.7 Battlefield Foundation Slice 26 Delivered

This is the twenty-sixth rule slice inside P7.9.7. It adds a held-battlefield alternate win condition and covers the same-text variant in the official catalog.

- Added implemented battlefield cards:
  - `OGN·293/298` and `OGN·293a/298`: when a player holds this battlefield and controls at least `7` battlefield-zone units, the backend declares that player the winner.
- Server-authoritative special-win changes:
  - The held trigger runs on the existing `DECLARE_BATTLE` battle-winner path after the defender successfully holds the battlefield.
  - The mutation emits `BATTLEFIELD_HELD`, `BATTLEFIELD_TRIGGER_RESOLVED` with `BATTLEFIELD_HELD_SEVEN_UNITS_WIN`, and `MATCH_WON`.
  - Boundary note: because the current battlefield model stores battlefield-zone objects flatly, this representative counts controlled unit objects in the holding player's battlefield zone; no frontend location inference is added.
- Added `battlefield-held-seven-units-win` local development seed plus GameHub coverage for prompt destination exposure, battle declaration, special-win event delivery, final winner snapshot, and finished room state.
- Migrated this battlefield held special-win slice in `BehaviorSpec`:
  - Implemented functional units: `784/811`
  - Manual deferred functional units: `27/811`
  - Implemented official entries: `981/1009`
  - Manual deferred official entries: `28/1009`
  - Battlefield rule-domain implemented: `27` functional units / `29` entries
  - Remaining battlefield manual deferred: `27` functional units / `28` entries
  - Timing trigger coverage moved forward by one functional unit and two official same-text entries.

P7.9.7 battlefield foundation slice 26 validation:

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldHeldSevenUnitsWinsGame|FullyQualifiedName~P79BattlefieldHeldSevenUnitsSeedOffersBattlefieldDestinationAndWins"`: passed `3/3`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2610/2610`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `55/55`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2744/2744`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated yet for this backend/special-win battlefield slice. GameHub coverage verifies the seed, authoritative prompt destination, match-won event, winner snapshot, and finished room state.

## P7.9.7 Battlefield Foundation Slice 27 Delivered

This is the twenty-seventh rule slice inside P7.9.7. It adds a conquest top-deck reveal/recycle battlefield effect.

- Added implemented battlefield card:
  - `OGN·291/298`: when a player conquers this battlefield, the backend reveals the top two cards of that player's main deck and recycles the revealed cards as the deterministic representative choice.
- Server-authoritative reveal/recycle changes:
  - The conquest trigger runs on the existing `DECLARE_BATTLE` conquest path after the battlefield is conquered.
  - The mutation emits `BATTLEFIELD_CONQUERED`, `BATTLEFIELD_TRIGGER_RESOLVED` with `BATTLEFIELD_CONQUERED_REVEAL_TOP_TWO_RECYCLE`, `CARDS_REVEALED`, and `CARDS_RECYCLED`.
  - Boundary note: the official text allows choosing any number of the two revealed cards and ordering the rest; this slice uses the existing deterministic backend recycle model for a product-playable representative path and adds no frontend choice inference.
- Added `battlefield-conquer-reveal-recycle` local development seed plus GameHub coverage for prompt destination exposure, battle declaration, reveal/recycle events, final main-deck snapshot boundary, and non-finished room state.
- Migrated this battlefield conquest reveal/recycle slice in `BehaviorSpec`:
  - Implemented functional units: `785/811`
  - Manual deferred functional units: `26/811`
  - Implemented official entries: `982/1009`
  - Manual deferred official entries: `27/1009`
  - Battlefield rule-domain implemented: `28` functional units / `30` entries
  - Remaining battlefield manual deferred: `26` functional units / `27` entries
  - Timing trigger and Recycle template coverage moved forward by one implemented battlefield representative.

P7.9.7 battlefield foundation slice 27 validation:

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldConquerRevealRecyclesTopTwo|FullyQualifiedName~P79BattlefieldConquerRevealRecycleSeedOffersBattlefieldDestinationAndRecycles"`: passed `2/2`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2611/2611`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `56/56`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2746/2746`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated yet for this backend/reveal-recycle battlefield slice. GameHub coverage verifies the seed, authoritative prompt destination, reveal/recycle events, and snapshot boundary.

## P7.9.7 Battlefield Foundation Slice 28 Delivered

This is the twenty-eighth rule slice inside P7.9.7. It adds a turn-start battlefield damage effect.

- Added implemented battlefield card:
  - `UNL-212/219`: at each player's turn start, before scoring/rune/draw flow, the backend deals `1` damage to battlefield-zone unit objects and performs lethal damage cleanup.
- Server-authoritative turn-start damage changes:
  - The trigger runs during the existing `ResolveTurnStart` path after ephemeral cleanup and before first-turn battlefield scoring, rune calls, and card draw.
  - The mutation emits `BATTLEFIELD_TRIGGER_RESOLVED` with `BATTLEFIELD_TURN_START_DAMAGE_ALL_UNITS`, per-target `DAMAGE_APPLIED`, and `UNIT_DESTROYED` when lethal damage removes a unit.
  - Boundary note: because the current battlefield model stores battlefield-zone objects flatly, this representative applies to battlefield-zone units while preserving server authority and no frontend location inference.
- Added `battlefield-turn-start-damage` local development seed plus GameHub coverage for end-turn transition into turn start, authoritative damage/destruction events, ordering before rune call, and final snapshot zone state.
- Migrated this battlefield turn-start damage slice in `BehaviorSpec`:
  - Implemented functional units: `786/811`
  - Manual deferred functional units: `25/811`
  - Implemented official entries: `983/1009`
  - Manual deferred official entries: `26/1009`
  - Battlefield rule-domain implemented: `29` functional units / `31` entries
  - Remaining battlefield manual deferred: `25` functional units / `26` entries
  - Damage template coverage moved forward by one implemented battlefield representative.

P7.9.7 battlefield foundation slice 28 validation:

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldTurnStartDamageAllBattlefieldUnitsBeforeScoring|FullyQualifiedName~P79BattlefieldTurnStartDamageSeedDamagesAndDestroysBeforeRuneCall"`: passed `2/2`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2612/2612`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `57/57`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2748/2748`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated yet for this backend/turn-start battlefield slice. GameHub coverage verifies the seed, authoritative event ordering, damage/destruction events, and snapshot boundary.

## P7.9.7 Battlefield Foundation Slice 29 Delivered

This is the twenty-ninth rule slice inside P7.9.7. It adds a held-battlefield hero-zone return trigger.

- Added implemented battlefield card:
  - `OGN·281/298`: when a player holds this battlefield and that player's champion zone is empty, the backend returns a representative owned unit card from that player's graveyard to the champion zone.
- Server-authoritative hero-zone return changes:
  - The held trigger runs on the existing `DECLARE_BATTLE` battle-winner path after the defender successfully holds the battlefield.
  - The mutation emits `BATTLEFIELD_HELD`, `BATTLEFIELD_TRIGGER_RESOLVED` with `BATTLEFIELD_HELD_RETURN_HERO_FROM_GRAVEYARD`, and `UNIT_RETURNED_TO_CHAMPION_ZONE`.
  - Boundary note: the current card object model does not persist full official `英雄单位` category metadata for every unit object; this representative uses server-owned unit/card-object state and test seed category tags while adding no frontend inference.
- Added `battlefield-held-return-hero` local development seed plus GameHub coverage for prompt destination exposure, battle declaration, hero-zone return event delivery, and final graveyard/champion-zone snapshot.
- Migrated this battlefield held return slice in `BehaviorSpec`:
  - Implemented functional units: `787/811`
  - Manual deferred functional units: `24/811`
  - Implemented official entries: `984/1009`
  - Manual deferred official entries: `25/1009`
  - Battlefield rule-domain implemented: `30` functional units / `32` entries
  - Remaining battlefield manual deferred: `24` functional units / `25` entries
  - Recall template and trigger coverage moved forward by one implemented battlefield representative.

P7.9.7 battlefield foundation slice 29 validation:

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldHeldReturnsHeroFromGraveyardToChampionZone|FullyQualifiedName~P79BattlefieldHeldReturnHeroSeedOffersBattlefieldDestinationAndReturnsHero"`: passed `2/2`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2613/2613`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `58/58`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2750/2750`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated yet for this backend/hero-zone return battlefield slice. GameHub coverage verifies the seed, authoritative prompt destination, return event, and snapshot boundary.

## P7.9.7 Battlefield Foundation Slice 30 Delivered

This is the thirtieth rule slice inside P7.9.7. It adds a turn-start battlefield destroy-and-draw effect.

- Added implemented battlefield card:
  - `UNL-209/219`: at the current player's turn start, before scoring/rune/draw flow, the backend destroys a representative controlled battlefield-zone unit, then draws `1` card.
- Server-authoritative turn-start destroy/draw changes:
  - The trigger runs during the existing `ResolveTurnStart` path after turn-start battlefield damage and before first-turn battlefield scoring, rune calls, and normal turn draw.
  - The mutation emits `BATTLEFIELD_TRIGGER_RESOLVED` with `BATTLEFIELD_TURN_START_DESTROY_UNIT_DRAW`, `UNIT_DESTROYED` with reason `BATTLEFIELD_TURN_START_DESTROY_UNIT_DRAW`, and a pre-rune-call `CARD_DRAWN`; normal turn draw then proceeds from the updated server deck state.
  - Boundary note: because the current battlefield model stores battlefield-zone objects flatly, this representative chooses a deterministic controlled battlefield-zone unit while preserving backend authority and no frontend location inference.
- Added `battlefield-turn-start-destroy-draw` local development seed plus GameHub coverage for end-turn transition into turn start, authoritative trigger/destruction/draw events, event ordering before rune call, and final snapshot zone state.
- Migrated this battlefield turn-start destroy/draw slice in `BehaviorSpec`:
  - Implemented functional units: `788/811`
  - Manual deferred functional units: `23/811`
  - Implemented official entries: `985/1009`
  - Manual deferred official entries: `24/1009`
  - Battlefield rule-domain implemented: `31` functional units / `33` entries
  - Remaining battlefield manual deferred: `23` functional units / `24` entries
  - Draw and Destroy template coverage moved forward by one implemented battlefield representative.

P7.9.7 battlefield foundation slice 30 validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldTurnStartDestroyDraw"`: passed `2/2`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2614/2614`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `59/59`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2752/2752`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated yet for this backend/turn-start destroy-draw battlefield slice. GameHub coverage verifies the seed, authoritative event ordering, destroy/draw events, and snapshot boundary.

## P7.9.7 Battlefield Foundation Slice 31 Delivered

This is the thirty-first rule slice inside P7.9.7. It adds a static battlefield roam permission.

- Added implemented battlefield card:
  - `OGN·297/298`: while this battlefield object is present in a player's battlefield zone, battlefield-zone unit objects can use `ROAM` for precise friendly battlefield-to-battlefield movement.
- Server-authoritative static roam changes:
  - `MOVE_UNIT` precise battlefield movement now resolves roam permission from native `游走`, temporary `ROAM`, or the server-side `OGN·297/298` battlefield static.
  - The mutation remains server-authoritative and emits the existing `UNIT_MOVED_TO_BATTLEFIELD` event with `movementKeyword = 游走`, origin, destination, and optional cost metadata.
  - Boundary note: because the current battlefield model stores battlefield-zone objects flatly, this representative applies the static roam permission to controlled battlefield-zone units while adding no frontend location inference.
- Added `battlefield-static-roam` local development seed plus GameHub coverage for prompt source exposure, `MOVE_UNIT` `ROAM` optional cost, precise battlefield move submission, and final snapshot boundary.
- Migrated this battlefield static roam slice in `BehaviorSpec`:
  - Implemented functional units: `789/811`
  - Manual deferred functional units: `22/811`
  - Implemented official entries: `986/1009`
  - Manual deferred official entries: `23/1009`
  - Battlefield rule-domain implemented: `32` functional units / `34` entries
  - Remaining battlefield manual deferred: `22` functional units / `23` entries
  - Move template coverage moved forward by one implemented battlefield representative.

P7.9.7 battlefield foundation slice 31 validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldStaticRoam"`: passed `2/2`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2615/2615`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `60/60`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2754/2754`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated yet for this backend/static-roam battlefield slice. GameHub coverage verifies the seed, prompt source/optional cost, authoritative move event, and snapshot boundary.

## P7.9.7 Battlefield Foundation Slice 32 Delivered

This is the thirty-second rule slice inside P7.9.7. It adds a static battlefield movement restriction.

- Added implemented battlefield card:
  - `OGN·295/298`: while this battlefield object is present in a player's battlefield zone, battlefield-zone units cannot move to that player's base through `MOVE_UNIT`.
- Server-authoritative static restriction changes:
  - `MOVE_UNIT` from `BATTLEFIELD` to `BASE` now checks the server-side `OGN·295/298` battlefield static before mutating zones.
  - The mutation is rejected with `ErrorCodes.InvalidTarget` and a stable message when the movement restriction applies; no frontend rule inference is required.
  - Boundary note: because the current battlefield model stores battlefield-zone objects flatly, this representative blocks controlled battlefield-zone units from moving to base while preserving the existing prompt and snapshot authority model.
- Added `battlefield-static-prevent-move-base` local development seed plus GameHub coverage for submitting the blocked move and receiving the stable backend error.
- Migrated this battlefield static movement restriction slice in `BehaviorSpec`:
  - Implemented functional units: `790/811`
  - Manual deferred functional units: `21/811`
  - Implemented official entries: `987/1009`
  - Manual deferred official entries: `22/1009`
  - Battlefield rule-domain implemented: `33` functional units / `35` entries
  - Remaining battlefield manual deferred: `21` functional units / `22` entries
  - Move template coverage moved forward by one implemented battlefield representative.

P7.9.7 battlefield foundation slice 32 validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldStaticPreventMove"`: passed `2/2`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2616/2616`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `61/61`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2756/2756`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated yet for this backend/static-prevent-move battlefield slice. GameHub coverage verifies the seed, blocked move submission, stable backend error, and snapshot boundary.

## P7.9.7 Battlefield Foundation Slice 33 Delivered

This is the thirty-third rule slice inside P7.9.7. It adds a battlefield movement-trigger temporary power effect.

- Added implemented battlefield card:
  - `OGN·277/298`: while this battlefield object is present in a player's battlefield zone, a controlled battlefield-zone unit that moves away from the battlefield gets `+1` power until end of turn.
- Server-authoritative movement-trigger power changes:
  - `MOVE_UNIT` now applies the trigger after a successful battlefield-origin move and before the resulting snapshot is emitted.
  - The mutation emits the existing movement event, then `BATTLEFIELD_TRIGGER_RESOLVED` with `BATTLEFIELD_UNIT_MOVED_POWER_PLUS_1`, then `POWER_MODIFIED_UNTIL_END_OF_TURN`.
  - Boundary note: because the current battlefield model stores battlefield-zone objects flatly, this representative checks the controller's battlefield zone and adds no frontend location inference.
- Added `battlefield-move-power` local development seed plus GameHub coverage for prompt submission, authoritative movement, trigger event delivery, and final snapshot power/base-zone state.
- Migrated this battlefield movement-trigger power slice in `BehaviorSpec`:
  - Implemented functional units: `791/811`
  - Manual deferred functional units: `20/811`
  - Implemented official entries: `988/1009`
  - Manual deferred official entries: `21/1009`
  - Battlefield rule-domain implemented: `34` functional units / `36` entries
  - Remaining battlefield manual deferred: `20` functional units / `21` entries
  - Move, temporary power, and timing-trigger coverage moved forward by one implemented battlefield representative.

P7.9.7 battlefield foundation slice 33 validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldMovePower|FullyQualifiedName~P79BattlefieldMovedUnit"`: passed `2/2`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2617/2617`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `62/62`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2758/2758`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated yet for this backend/movement-trigger battlefield slice. GameHub coverage verifies the seed, authoritative movement event, temporary power event, and snapshot boundary.

## P7.9.7 Battlefield Foundation Slice 34 Delivered

This is the thirty-fourth rule slice inside P7.9.7. It adds a static battlefield unit-play restriction.

- Added implemented battlefield card:
  - `SFD·216/221`: while this battlefield object is present in a player's battlefield zone, unit play attempts to that battlefield through the existing battlefield `PLAY_CARD` destination are rejected by the backend.
- Server-authoritative unit-play restriction changes:
  - The existing Ambush battlefield play path now checks this battlefield static after the command is otherwise recognized as a legal battlefield unit play and before costs are paid or stack items are created.
  - The mutation is rejected with `ErrorCodes.InvalidTarget` and a stable message; no frontend rule inference is added.
  - Boundary note: because the current battlefield model stores battlefield-zone objects flatly, this representative blocks the server-known battlefield destination for that controller while preserving prompt/snapshot authority.
- Added `battlefield-static-prevent-play-units` local development seed plus GameHub coverage for submitting the blocked battlefield Ambush play and receiving the stable backend error.
- Migrated this battlefield static unit-play restriction slice in `BehaviorSpec`:
  - Implemented functional units: `792/811`
  - Manual deferred functional units: `19/811`
  - Implemented official entries: `989/1009`
  - Manual deferred official entries: `20/1009`
  - Battlefield rule-domain implemented: `35` functional units / `37` entries
  - Remaining battlefield manual deferred: `19` functional units / `20` entries
  - Static battlefield restriction coverage moved forward by one implemented representative.

P7.9.7 battlefield foundation slice 34 validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldStaticPreventsUnitPlay|FullyQualifiedName~P79BattlefieldStaticPreventPlayUnitsSeed"`: passed `2/2`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2618/2618`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `63/63`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2760/2760`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated yet for this backend/static-prevent-play battlefield slice. GameHub coverage verifies the seed, blocked play submission, stable backend error, and unchanged snapshot boundary.

## P7.9.7 Battlefield Foundation Slice 35 Delivered

This is the thirty-fifth rule slice inside P7.9.7. It adds a static battlefield Echo cost reduction.

- Added implemented battlefield card:
  - `SFD·211/221`: while this battlefield object is present in a player's battlefield zone, that player's friendly `ECHO` optional cost is reduced by `1` mana.
- Server-authoritative Echo cost reduction changes:
  - `PLAY_CARD` optional-cost planning still accepts only the server-known `ECHO` token from the prompt/command path.
  - The backend applies the battlefield reduction to the Echo extra mana cost before cost validation and payment; no frontend cost calculation authority is added.
  - `COST_PAID` now includes `battlefieldEchoCostReductionMana` so the UI/event log can show why the paid mana total was lower.
  - Boundary note: because the current battlefield model stores battlefield-zone objects flatly, this representative checks the controller's battlefield zone for the static battlefield object.
- Added `battlefield-static-echo-cost-reduction` local development seed plus GameHub coverage for submitting an Echo spell with only reduced mana available and receiving the authoritative cost payload.
- Migrated this battlefield Echo cost reduction slice in `BehaviorSpec`:
  - Implemented functional units: `793/811`
  - Manual deferred functional units: `18/811`
  - Implemented official entries: `990/1009`
  - Manual deferred official entries: `19/1009`
  - Battlefield rule-domain implemented: `36` functional units / `38` entries
  - Remaining battlefield manual deferred: `18` functional units / `19` entries
  - Echo interaction coverage moved forward by one implemented battlefield representative.

P7.9.7 battlefield foundation slice 35 validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldStaticReducesEchoCost|FullyQualifiedName~P79BattlefieldStaticEchoCostReductionSeed"`: passed `2/2`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2619/2619`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `64/64`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2762/2762`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated yet for this backend/static-echo battlefield slice. GameHub coverage verifies the seed, reduced Echo cost payload, stack repeat count, and snapshot boundary.

## P7.9.7 Battlefield Foundation Slice 36 Delivered

This is the thirty-sixth rule slice inside P7.9.7. It adds a static battlefield equipment cost reduction.

- Added implemented battlefield card:
  - `SFD·213/221`: while this battlefield object is present in a player's battlefield zone, that player's first friendly equipment played each turn costs `1` less mana.
- Server-authoritative equipment cost reduction changes:
  - `PLAY_CARD` planning now applies the reduction only for equipment cards that the server already recognizes as equipment play commands.
  - The backend records `PLAYED_EQUIPMENT_THIS_TURN:{playerId}` as an until-end-of-turn marker after an equipment is played, so a second equipment in the same turn cannot receive the first-equipment reduction.
  - `COST_PAID` now includes `battlefieldEquipmentCostReductionMana` so the UI/event log can explain the reduced paid mana total without client-side rule math.
  - Boundary note: token equipment creation is not a `PLAY_CARD` equipment command, so token generation remains excluded from this reduction.
- Added `battlefield-static-equipment-cost-reduction` local development seed plus GameHub coverage for submitting a Long Sword with only reduced mana available and receiving the authoritative cost payload.
- Migrated this battlefield equipment cost reduction slice in `BehaviorSpec`:
  - Implemented functional units: `794/811`
  - Manual deferred functional units: `17/811`
  - Implemented official entries: `991/1009`
  - Manual deferred official entries: `18/1009`
  - Battlefield rule-domain implemented: `37` functional units / `39` entries
  - Remaining battlefield manual deferred: `17` functional units / `18` entries
  - Static equipment-cost coverage moved forward by one implemented battlefield representative.

P7.9.7 battlefield foundation slice 36 validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldStaticReducesFirstEquipmentCost|FullyQualifiedName~P79BattlefieldStaticEquipmentCostReductionSeed"`: passed `2/2`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2620/2620`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `65/65`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2764/2764`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `git diff --check`: passed.
- Browser smoke: not repeated yet for this backend/static-equipment battlefield slice. GameHub coverage verifies the seed, reduced equipment cost payload, stack item creation, and snapshot boundary.
