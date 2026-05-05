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
| P7.9.6 | In progress | Legend functional-unit batches until all `44/44` legend units are implemented or split into smaller committed slices. Active/trigger slices migrated `10/44` legend FUs. | Functional-unit coverage tests. |
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
- P7.9.6 active-ability slices: `3` done.
- P7.9.6 automatic-trigger slices: `1` done.
- Current functional-unit implementation: `723/811 = 89.1%`.
- Current manual deferred boundary: `88/811 = 10.9%`.
- Remaining manual domains:
  - `传奇`: `34` functional units / `76` entries
  - `战场`: `54` functional units / `57` entries
- Overall P7.9 progress: `6/13 top-level batches = 46.2%`; inside P7.9.6, `3` legend active-ability slices and `1` automatic-trigger slice are complete.
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
