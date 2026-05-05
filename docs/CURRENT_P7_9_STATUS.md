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
| P7.9.3 | Planned | Structured prompt candidates for core actions: ready, pass, end turn, play card, move, assemble, battle. | Focused GameHub tests + Browser smoke. |
| P7.9.4 | Planned | Click-first cost, target, response-window, and battle declaration flow from prompt candidates. | Browser smoke: play, target, cost, pass, battle. |
| P7.9.5 | Planned | Legend domain foundation: `LEGEND_ACT` command contract, blocked-to-implemented migration path, representative conformance. | Focused conformance + GameHub tests. |
| P7.9.6 | Planned | Legend functional-unit batches until all `44/44` legend units are implemented or split into smaller committed slices. | Functional-unit coverage tests. |
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
- Overall P7.9 progress: `3/13 top-level batches = 23.1%`.
- Estimated remaining top-level batches: `10`.

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
