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

The hard blocker for "all cards fully playable" is not the P7 UI. It is the P6 manual domain boundary:

| Domain | Functional units | Entries | P7 disposition | P7.9 target |
| --- | ---: | ---: | --- | --- |
| Legends | 44 | 106 | Display only, active/passive domains blocked | Implement `LEGEND_ACT`, static/passive/trigger/identity surfaces, prompt exposure, conformance, UI operation |
| Battlefields | 54 | 57 | Display only, battlefield effects blocked | Implement battlefield control, hold/conquer/scoring effects, battlefield triggers/static effects, prompt exposure, conformance, UI operation |

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
| P7.9.6 | In progress | Legend functional-unit batches until all `44/44` legend units are implemented or split into smaller committed slices. Active/trigger/static/replacement slices migrated `35/44` legend FUs. | Functional-unit coverage tests. |
| P7.9.7 | Planned | Battlefield domain foundation: battlefield objects/control/hold/conquer event model and representative effects. | Focused conformance + GameHub tests. |
| P7.9.8 | Planned | Battlefield functional-unit batches until all `54/54` battlefield units are implemented or split into smaller committed slices. | Functional-unit coverage tests. |
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
- P7.9.6 status: in progress.
- P7.9.6 active-ability slices: `9` done.
- P7.9.6 automatic-trigger slices: `10` done.
- P7.9.6 static legend slices: `6` done.
- Current functional-unit implementation: `748/811 = 92.2%`.
- Current manual deferred boundary: `63/811 = 7.8%`.
- Remaining manual domains:
  - `传奇`: `9` functional units / `22` entries
  - `战场`: `54` functional units / `57` entries
- Overall P7.9 progress: `6/13 top-level batches = 46.2%`; inside P7.9.6, `9` legend active-ability slices, `10` automatic-trigger slices, and `6` static legend slices are complete.
- Estimated remaining top-level batches: `7`.

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
