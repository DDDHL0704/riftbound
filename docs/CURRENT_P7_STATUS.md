# Current P7 Status

Last updated: 2026-05-05

P7 goal:

> 完成 P7 产品级 Web 对战：在 P2-P6 服务端权威规则、GameHub/Room、ActionPrompt、BehaviorSpec 和 conformance 体系基础上，构建精美、稳定、可双人联机对战、断线重连、事件日志/战报、基础回放/观战入口、图鉴与卡牌详情的产品级 Web 对战体验；UI 只开放 CONFORMANCE_PASS 的可玩能力，所有 P6 deferred/manual 能力必须禁用、过滤或明确提示，前端不裁决规则、不持有权威状态，保持后端测试全绿。

## Baseline Confirmation

- Starting commit: `3523af6 test: complete p6 final audit`
- Expected dirty state at phase start: only untracked `riftbound-dotnet.sln`
- P6 final audit: `1009/1009` official entries have status; `811/811` functional units have implemented coverage or explicit deferred coverage.
- Current P6 implementation split: `713/811 = 87.9%` implemented; `98/811 = 12.1%` manual deferred, limited to `传奇` and `战场`; `0/811 = 0.0%` unimplemented.
- Latest recorded full validation before P7: `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` passed `2612/2612`.
- Latest focused suites before P7:
  - `ConformanceFixtureRunnerTests`: `2507/2507`
  - `CardCatalogBaselineTests`: `37/37`
  - `GameHubJoinTests`: `26/26`

## Evidence Sources Read

- `docs/CURRENT_P6_STATUS.md`
- `docs/CURRENT_P5_STATUS.md`
- `docs/CURRENT_P4_STATUS.md`
- `docs/CURRENT_P3_STATUS.md`
- `docs/CURRENT_P2_STATUS.md`
- `docs/CURRENT_P2_5_STATUS.md`
- `docs/master-development-plan.md`, P7 section
- `docs/START_HERE.md`
- `README.md`
- `src/Riftbound.Api`
- `src/Riftbound.Engine/MatchSession.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`
- `src/Riftbound.DevUi`

## P7 Scope

In scope:

- Product-grade React/Vite Web battle experience built on the existing SignalR `GameHub`.
- Create/join/reconnect flow for two players.
- Server snapshot/prompt/event rendering as the only source of truth.
- Desktop battle table for hand, base, two battlefield lanes, rune pool, legend, champion, stack, equipment attachment, ownership/control, damage, temporary power, exhaustion, combat flags, and common status tags/effects.
- ActionPrompt-driven operations: `READY`, `PASS_PRIORITY`, `PASS_FOCUS`, `PASS`, `END_TURN`, `PLAY_CARD`, `MOVE_UNIT`, `ASSEMBLE_EQUIPMENT`, and `DECLARE_BATTLE`.
- Payment/optional-cost and target-selection panels that submit intents but do not decide legality.
- Event log, match report, and basic replay/spectator entry surfaces with explicit backend boundary when persistence or spectator hub support is not yet available.
- Official catalog and card details using `/catalog/behavior-specs`, including status, official text, parsed specs, conformance/deferred messaging, and P6 manual boundaries.

Out of scope:

- Complex AI.
- Mobile-specific layout work.
- Production accounts, matchmaking, deployment, monitoring, security/risk systems, and production replay storage. These stay in P8.
- Committing rules PDF/FAQ files.
- Committing untracked `riftbound-dotnet.sln`.
- Implementing legend or battlefield non-`PLAY_CARD` rule domains during P7 unless a separate rule phase explicitly starts them.

## P7.0 Audit

### Backend/API

- `Riftbound.Api` exposes:
  - `GET /health`
  - `GET /catalog/summary`
  - `GET /catalog/p3-status`
  - `GET /catalog/behavior-specs`
  - SignalR hub `/hubs/game`
- `GameHub` supports:
  - `JoinRoom(roomId, playerId, reconnectToken?)`
  - `Reconnect(roomId, playerId, reconnectToken)`
  - `RequestSnapshot(roomId, playerId)`
  - `Ready(roomId, playerId, clientIntentId)`
  - `Pass`, `EndTurn`, and generic `SubmitIntent`
  - development-only `SeedScenario`
- `GameHub` broadcasts `Joined`, `Snapshot`, `Prompt`, `Events`, and `Error` messages. Player-specific snapshots are sent only to `room:{roomId}:player:{playerId}` groups.
- Reconnect tokens are already rotated and tested; plaintext tokens are not persisted.
- Development CORS currently allows Vite ports `5173` and `5174`.

### Engine/Prompt

- `SnapshotDto` already exposes:
  - player zones: hand/hidden hand, base, battlefields, graveyard, banished, legendZone, championZone, deck counts
  - rune pool, score, experience, cards played this turn
  - visible card objects with damage, power, temp power modifier, exhaustion, face-down, attacking/defending, tags, until-end-of-turn effects, attachedToObjectId, ownerId, controllerId
  - stack, trigger queue, phase, timingState, priority/focus player, winner, ready players
- `ActionPromptDto` exposes `actionable`, `reason`, and action ids. P7 UI may render controls from action ids, but must still submit all command details to the server and trust server errors.
- Current open-main `ResolutionResult.BuildPrompts` exposes broad actions (`PLAY_CARD`, `ACTIVATE_ABILITY`, `ASSEMBLE_EQUIPMENT`, `MOVE_UNIT`, `HIDE_CARD`, `TAP_RUNE`, `LEGEND_ACT`, `PASS`, `END_TURN`). `CoreRuleEngine.BuildCorePrompts` narrows post-resolution open-main prompts to `END_TURN` and priority/focus pass prompts. P7 must display the current prompt exactly and avoid inventing hidden legal actions.

### Existing Web UI

- `src/Riftbound.DevUi` is React 19 + Vite + SignalR.
- It already has two local player connections, join/reconnect/ready/snapshot, scenario seeds, raw `SubmitIntent`, a coarse battle desk, command log, event/error display, and fixture draft helper.
- P7 will keep the reliable SignalR plumbing and replace the dev-test presentation with a product battle room first screen. Development-only seeds may remain available only as a clearly labeled local smoke tool.

## P7 Available Ability Matrix

| Surface | P7 UI disposition | Server authority |
| --- | --- | --- |
| Room create/join | Enabled | `JoinRoom` assigns seats and rejects third player. |
| P1/P2 ready | Enabled from prompt `READY` | `Ready` starts match only after both players are ready. |
| Reconnect | Enabled with stored reconnect token | `Reconnect` validates and rotates token. |
| Snapshot refresh | Enabled | `RequestSnapshot`; UI never mutates authoritative state. |
| `PLAY_CARD` | Enabled only when prompt contains `PLAY_CARD`; command builder uses selected hand/source object and optional inputs | `CoreRuleEngine.ResolvePlayCard` and conformance fixtures decide legality. |
| `PASS_PRIORITY` | Enabled only when prompt contains `PASS_PRIORITY` | Priority window and stack resolution are server-owned. |
| `PASS_FOCUS` | Enabled only when prompt contains `PASS_FOCUS` | Spell-duel focus window is server-owned. |
| `PASS` | Enabled only when prompt contains `PASS` | Server may accept/reject; UI does not implement pass semantics. |
| `END_TURN` | Enabled only when prompt contains `END_TURN` | End-turn cleanup, rune call, draw, score, and win are server-owned. |
| `MOVE_UNIT` | Enabled only when prompt contains `MOVE_UNIT`; UI submits selected source/origin/destination | Movement legality remains server-owned. |
| `ASSEMBLE_EQUIPMENT` | Enabled only when prompt contains `ASSEMBLE_EQUIPMENT`; UI submits source/target/cost | Attachment legality remains server-owned. |
| `DECLARE_BATTLE` | Enabled only when prompt contains `DECLARE_BATTLE`; UI submits battlefield/attackers/defenders/cost | Combat damage and cleanup remain server-owned. |
| `ACTIVATE_ABILITY`, `HIDE_CARD`, `REVEAL_CARD`, `TAP_RUNE`, `LEGEND_ACT` | Rendered as blocked or unavailable unless current prompt exposes an implemented command path and UI has a concrete server command form | Unsupported or deferred commands must surface backend error or explicit P7 boundary. |

## P6 Deferred/Manual UI Policy

- `manual-rule-required` cards in categories `传奇` and `战场` are not playable controls in P7.
- Legend and battlefield objects may be displayed in their zones as identity cards, but any active/passive/trigger/static text is marked `deferred/manual domain`.
- `LEGEND_ACT`, battlefield granted abilities, token activated resource abilities, token copy timing, and token battlefield effects are disabled with explicit messaging unless a backend prompt and command route is implemented later.
- Catalog entries show exact `BehaviorSpec.status`, reason, `implementedByCardNo`, and `implementedEffectKind`.
- Catalog filtering defaults to playable/conformance-pass surfaces. Deferred/manual entries are visible in the catalog, but not surfaced as recommended play actions.
- UI wording must never imply a P6 manual/deferred ability is playable.

## Browser Smoke Plan

Local run:

```bash
source scripts/dev-env.sh
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5088 dotnet run --project src/Riftbound.Api/Riftbound.Api.csproj
```

```bash
source scripts/dev-env.sh
cd src/Riftbound.DevUi
npm run dev
```

Core P7 smoke path:

1. Open the local Web URL.
2. Create a room and join P1/P2.
3. Ready both players.
4. Reconnect at least one player with the current reconnect token.
5. Use a conformance-backed scenario or natural snapshot to execute:
   - `PLAY_CARD`
   - `PASS_PRIORITY`
   - `END_TURN`
   - `MOVE_UNIT` or `DECLARE_BATTLE`
6. Confirm event log, match report/replay entry, and final snapshot summary.
7. Record URL, roomId, operation path, event summary, and final snapshot summary in this file.

## P7 Batch Plan

| Batch | Status | Target | Gate |
| --- | --- | --- | --- |
| P7.0 | Done | Audit/status file, ability matrix, deferred policy, smoke path. | `git diff --check`; docs-only commit. |
| P7.1 | Done | Product room, two-player connection, reconnect, snapshot/prompt/event shell. | Browser smoke: join, ready, reconnect. |
| P7.2 | Done | Battle desktop layout with lanes/base/hand/runes/legend/champion/equipment/control markers. | Browser visual smoke. |
| P7.3 | Done | ActionPrompt-driven play/pass/end-turn/move/battle controls. | Browser smoke: play, pass, end turn, move/battle. |
| P7.4 | Done | Payment and target-selection UX, response windows, spell-duel surface. | Browser smoke: target and response flow. |
| P7.5 | Done | Status badges, equipment attachment strip, damage/temp power/exhaustion/combat/shield/swift/ephemeral/control markers. | Browser visual smoke passed. |
| P7.6 | Done | Product event log, match report, replay/spectator entry with explicit backend boundary. | Browser smoke passed: report and boundary. |
| P7.7 | Done | Catalog and card detail browser with BehaviorSpec status and deferred labels. | Browser smoke passed: catalog/detail filtering. |
| P7.8 | Done | Product polish, loading/empty/error/disconnect states, stable desktop sizing. | Browser smoke passed; screenshot fallback noted. |
| P7.x | Done | Final full validation, status sync, clean status, commit. | Browser smoke + full backend test gate passed. |

Current P7 progress: `10/10 batches = 100.0%`; estimated remaining batches: `0`.

## Validation Policy

Every .NET command must be run through:

```bash
source scripts/dev-env.sh
```

Final P7 gate:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`
- Browser smoke recorded in this file.
- `git diff --check`
- After each batch commit, `git status --short` should show only `?? riftbound-dotnet.sln`.

## P7.0 Validation

- `git diff --check`: passed.

## P7.1 Delivered

- Promoted the web header and default room naming from the P2.5 dev-test identity into a P7 room-first product entry.
- Persisted the current P7 room id and P1/P2 ids in browser local storage.
- Persisted reconnect sessions per room/player and let `Reconnect` recover from either in-memory session state or stored reconnect tokens.
- Kept SignalR `Joined`, `Snapshot`, `Prompt`, `Events`, and `Error` flows unchanged and server-authoritative.
- Kept development scenario seeds available only as local smoke helpers; they are still guarded by the backend `Development` environment check.

P7.1 validation:

- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `26/26`.
- `git diff --check`: passed.

## P7.2 Delivered

- Added `cardNo` to visible server snapshot card objects so the battle table can show authoritative card identity without guessing from object ids.
- Upgraded the desk card presentation for hand, base, two battlefield lanes, legend, champion, graveyard, and banished zones.
- Added stable card dimensions and visual metadata for power, damage, mana cost, face-down, exhaustion, combat flags, attachment, owner/controller divergence, tags, and until-end-of-turn effects.
- Added rune/experience/deck/hand counts to the player resource row.
- Kept all object picking as intent-building only; no frontend legality checks were added.

P7.2 validation:

- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- Browser visual smoke: passed with the `equipment` scenario; desk showed P1 hand Long Sword, base assemble target, legend/champion slots, two battlefield lanes, and stable object cards.
- `git diff --check`: passed.

## P7.3 Delivered

- Added product command panels for `MOVE_UNIT`, `ASSEMBLE_EQUIPMENT`, and `DECLARE_BATTLE` alongside `PLAY_CARD`.
- Added command builders for play destination, move origin/destination, assemble source/target/cost, and battle attackers/defenders/costs.
- Converted the workbench prompt action chips into actual prompt buttons for direct prompt actions such as `PASS`, `PASS_PRIORITY`, `PASS_FOCUS`, and `END_TURN`.
- Disabled command submit buttons unless the current server `ActionPrompt` is actionable and exposes that command.
- Added `DECLARE_BATTLE` to the open-main server prompt surface so the UI does not need to invent a battle action.
- Added a development `battle-declare` smoke seed button to exercise the prompt-driven combat declaration path.

P7.3 validation:

- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `26/26`.
- Browser smoke: passed with prompt-enabled `MOVE_UNIT` and `DECLARE_BATTLE`.
- `git diff --check`: passed.

## P7.4 Delivered

- Added selected-target state and clear-target controls to the `PLAY_CARD` builder.
- Added visible-object selection states so target picks are clear without making target legality decisions.
- Added optional-cost quick chips for current representative costs such as `ECHO`, `ASSEMBLE_RED`, `COMBAT_ASSIGNMENT`, `STANDBY_REVEAL_0`, `ROAM`, and `SPEND_POWER:1`.
- Added a response-window panel showing server timing state, priority player, focus player, stack count, and stack labels.
- Verified spell/response flow through server prompt and events; the UI did not infer priority legality.

P7.4 validation:

- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- Browser smoke: passed with `spell-duel`, target selection, `PLAY_CARD`, P1 `PASS_PRIORITY`, P2 `PASS_PRIORITY`, and stack resolution.
- `git diff --check`: passed.

## P7.5 Delivered

- Added snapshot-driven status badges for damage, temporary power, exhaustion, attack/defense, face-down, attached, control divergence, Spellshield, Stun, Ephemeral, Swift, Standby, Ambush, Echo, Boon, and Roam.
- Added an attachment strip under host objects so equipment is visible both as its own server object and as attached to its target.
- Added visual classes for damaged, shielded, ephemeral, attached, and control-diverged objects without changing any frontend legality logic.
- Added a `status-showcase` development seed that exposes attached equipment, control transfer markers, damage, temp power, Spellshield, Stun, Ephemeral, Swift, Standby, and Roam in one server snapshot.
- Added a GameHub regression test for the showcase snapshot to keep attached equipment and owner/controller fields stable.

P7.5 validation:

- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `27/27`.
- Browser smoke: passed with `status-showcase`; desk showed attachment, Spellshield, Stun, Ephemeral, Swift, Roam, damage, `+2 战力`, and control badge text from the server snapshot.
- `git diff --check`: passed.

## P7.6 Delivered

- Added a product event log panel that merges and de-duplicates server `Events` messages from both clients.
- Added a match report panel summarizing room id, room status, turn, active player, winner, and each player's score/hand/base/battlefield/graveyard counts from the latest server snapshot.
- Added a visible replay/spectator entry with disabled buttons and an explicit boundary: current Web UI can show received events and latest snapshot, while product replay/spectator APIs remain deferred until backend endpoints exist.
- Kept raw debug JSON below the product telemetry; the new panels are readable surfaces, not rule or replay simulators.

P7.6 validation:

- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- Browser smoke: passed with `battle-score` + prompt `END_TURN`; telemetry showed `21 events`, `BURNOUT_APPLIED`, `MATCH_WON`, report status `FINISHED`, winner `P1`, and disabled replay/spectator buttons.
- `git diff --check`: passed.

## P7.7 Delivered

- Added a card catalog panel backed by `/catalog/behavior-specs`.
- Defaulted the catalog to `CONFORMANCE_PASS` cards and exposed filters for `P6 manual deferred`, `blocked/unimplemented`, and all statuses.
- Added card detail surfaces for official text, BehaviorSpec reason, functional unit id, implemented effect kind/card, templates, keywords, targets, triggers, and effects.
- Marked manual deferred legend/battlefield cards with `P6 MANUAL DEFERRED` and an explicit boundary that they are blocked from P7 playable controls until backend domains exist.
- Kept catalog browsing read-only; it does not create commands or alter ActionPrompt behavior.

P7.7 validation:

- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- Browser smoke: passed; catalog loaded `1009` specs, showed `CONFORMANCE_PASS 846`, `Manual deferred 163`, and a manual legend detail with the P7 blocked boundary.
- `git diff --check`: passed.

## P7.8 Delivered

- Shifted the product UI palette away from the earlier warm dev-bench theme into a cleaner blue/green desktop table style.
- Added a system notice strip for empty seating, reconnecting/error states, and catalog loading/error state.
- Added keyboard `focus-visible` outlines for buttons and form controls.
- Tightened empty-state spacing after Browser smoke caught the `No player snapshot` copy running together with its helper line.
- Removed the remaining dominant beige/cream hard-coded colors from the main CSS scan.

P7.8 validation:

- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- Browser smoke: passed via Browser Use DOM verification; first screen showed room entry, system notice, battle desk, operation panel, event log, and catalog. Empty desk copy was verified as two separate lines after the fix.
- Browser screenshot note: screenshot capture succeeded before the empty-state fix and exposed the text issue; after the fix, repeated screenshot attempts timed out in the browser CDP path, so the fixed state was verified by DOM smoke.
- `rg` CSS scan for the previous dominant beige/cream hard-coded values: passed with no matches.
- `git diff --check`: passed.

## P7.x Final Validation

- Synchronized legacy fixture prompt expectations with the P7 server prompt contract: open-main prompts now include `DECLARE_BATTLE` after `MOVE_UNIT`.
- Increased the product event log visible window to `32` events so full play/pass/end-turn/battle smoke keeps early `CARD_PLAYED` evidence visible.
- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2613/2613`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2507/2507`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `37/37`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `27/27`.
- `source ../../scripts/dev-env.sh && npm run build` from `src/Riftbound.DevUi`: passed.
- Browser final smoke: passed; details recorded below.
- `git diff --check`: passed.

## P7 Follow-up Polish

- Added card ownership presentation to product battle cards: visible objects now show a `我方/对方` controller ribbon based on authoritative `controllerId`/`ownerId`, with green left rails for the active player's controlled cards and red rails for opponent-controlled cards.
- Added runtime official card art lookup from the public card gallery API used by `https://playloltcg.com/card.html`; front images are referenced from the official CDN at render time and are not committed into the repository.
- Official art loading has an explicit system notice boundary: loading state is shown while fetching, and failure degrades to the existing text card face without changing game rules or server state.
- Verification: `source scripts/dev-env.sh && npm run build --prefix src/Riftbound.DevUi` passed. Local service health checks returned `200` for `http://127.0.0.1:5173/` and `http://127.0.0.1:5088/health`. Browser fallback smoke opened the local page and the official gallery page; Computer Use could verify first-screen rendering but did not dispatch product UI clicks reliably in this turn, so this follow-up is covered by build plus service health and code-path inspection.

## Browser Smoke Records

- P7.1 room/reconnect smoke:
  - Web URL: `http://127.0.0.1:5173/`
  - API URL: `http://127.0.0.1:5088`
  - roomId: `p7-2026-05-05-battle`
  - Operation path: open Web URL -> `双人入座` -> `双方准备` -> P1 `Stop` -> P1 `Reconnect`.
  - Event summary: P1/P2 joined, ready flow emitted `MATCH_STARTED`, reconnect returned a fresh `RECONNECT` session and current snapshot/prompt.
  - Final snapshot summary: roomStatus `IN_PROGRESS`, turn `#1`, active player `P1`, timing `NEUTRAL_OPEN`, stack `0`, no winner; P1 reconnect status `reconnected`, seat `P1`, prompt actionable with server prompt actions.
- P7.2 battle desk visual smoke:
  - Web URL: `http://127.0.0.1:5173/`
  - API URL: `http://127.0.0.1:5088`
  - roomId: `p7-1777954427927`
  - Operation path: reload Web URL -> `新房间` -> `双人入座` -> `双方准备` -> seed `equipment`.
  - Event summary: `DEV_SCENARIO_SEEDED` for `equipment`; snapshot showed P1 Long Sword in hand and a base unit for assembly.
  - Final snapshot summary: roomStatus `IN_PROGRESS`, turn `#787`, active player `P1`, timing `NEUTRAL_OPEN`, stack `0`; P1 resources mana `2`, power `1`, hand `1`, base `1`, legend/champion visible; P2 legend/champion visible.
- P7.3 ActionPrompt command smoke:
  - Web URL: `http://127.0.0.1:5173/`
  - API URL: `http://127.0.0.1:5088`
  - roomId: `p7-1777954785777`
  - Operation path: reload Web URL -> `新房间` -> `双人入座` -> `双方准备` -> seed `movement` -> submit prompt-enabled `MOVE_UNIT` -> seed `battle-declare` -> submit prompt-enabled `DECLARE_BATTLE`.
  - Event summary: movement path emitted `UNIT_MOVED_TO_BASE`; battle path emitted `BATTLE_DECLARED`, two combat `DAMAGE_APPLIED` events, and defender cleanup.
  - Final snapshot summary: roomStatus `IN_PROGRESS`, turn `#95`, active player `P1`, timing `NEUTRAL_OPEN`, stack `0`; P1 attacker remains on battlefield with combat damage/attacking marker, P2 defender is in graveyard, current prompt only exposes `END_TURN` so play/move/assemble/battle buttons are disabled.
- P7.4 target/response smoke:
  - Web URL: `http://127.0.0.1:5173/`
  - API URL: `http://127.0.0.1:5088`
  - roomId: `p7-1777954984462`
  - Operation path: reload Web URL -> `新房间` -> `双人入座` -> `双方准备` -> seed `spell-duel` -> submit `PLAY_CARD P1-SPELL-HEXTECH-RAY` targeting `P2-UNIT-001` -> P1 `PASS_PRIORITY` -> switch active client to P2 -> P2 `PASS_PRIORITY`.
  - Event summary: `CARD_PLAYED`, `COST_PAID`, `STACK_ITEM_ADDED`, `PRIORITY_PASSED`, `STACK_ITEM_RESOLVED`, and damage resolution were observed.
  - Final snapshot summary: roomStatus `IN_PROGRESS`, turn `#9`, active player `P1`, timing `NEUTRAL_OPEN`, stack `0`; P1 spell is in graveyard, P2 target is in graveyard, selected target remains visible in the command builder as submitted intent context.
- P7.5 status/equipment visual smoke:
  - Web URL: `http://127.0.0.1:5173/`
  - API URL: `http://127.0.0.1:5088`
  - roomId: `p7-5-status-1777955607381`
  - Operation path: reload Web URL -> set room id -> `双人入座` -> `双方准备` -> seed `status-showcase`.
  - Event summary: `DEV_SCENARIO_SEEDED` for `status-showcase`; no gameplay command was required because this smoke verifies product rendering of server snapshot markers.
  - Final snapshot summary: roomStatus `IN_PROGRESS`, turn `#555`, active player `P1`, timing `NEUTRAL_OPEN`, stack `0`; visible desk contained `P1-UNIT-STATUS-ANCHOR`, attached `P1-EQUIPMENT-LONG-SWORD` (`SFD·022/221`), `+2 战力`, `法盾`, `游走`, `瞬息`, `眩晕`, and `P2-CONTROLLED-UNIT` with `控制 P1`.
- P7.6 event log/report/replay-boundary smoke:
  - Web URL: `http://127.0.0.1:5173/`
  - API URL: `http://127.0.0.1:5088`
  - roomId: `p7-6-report-1777955956990`
  - Operation path: reload Web URL -> set room id -> `双人入座` -> `双方准备` -> seed `battle-score` -> P1 prompt `END_TURN`.
  - Event summary: product event log showed `21 events`, including `TURN_END_DECLARED`, `TURN_START_BEGAN`, multiple `BURNOUT_APPLIED`, and `MATCH_WON`.
  - Final snapshot summary: roomStatus `FINISHED`, turn `#76`, active player `P2`, winner `P1`, P1 score `8`, P2 score `0`; replay and spectator entry buttons were visible and disabled with a deferred backend boundary.
- P7.7 catalog/card detail smoke:
  - Web URL: `http://127.0.0.1:5173/`
  - API URL: `http://127.0.0.1:5088`
  - Operation path: reload Web URL -> wait for catalog load -> verify `CONFORMANCE_PASS` counts -> switch filter to `P6 manual deferred` -> open first manual card detail.
  - Catalog summary: loaded `1009` BehaviorSpecs; `CONFORMANCE_PASS 846`, `Manual deferred 163`, `Blocked 0`.
  - Detail summary: selected manual deferred legend `FND-249/298` 不灭狂雷; detail showed functional unit `FU-fd15be558d`, official text, BehaviorSpec reason `Category '传奇' requires a dedicated non-PLAY_CARD rule domain before template execution.`, and the P7 blocked-from-playable-controls boundary.
- P7.8 polish smoke:
  - Web URL: `http://127.0.0.1:5173/`
  - Operation path: reload Web URL -> verify first-screen room entry, system notice, battle desk, operation panel, event log, and catalog -> inspect empty desk copy.
  - Visual/DOM summary: `等待双人入座` system notice shown; `Battle Desk` empty state now renders `No player snapshot` and `Join both clients to populate the desk.` as separate lines; catalog still loads `CONFORMANCE_PASS 846`.
  - Screenshot boundary: Browser screenshot capture timed out after the fix, but the earlier Browser screenshot caught the empty-state layout issue and the corrected DOM was verified after patching.
- P7.x final smoke:
  - Web URL: `http://127.0.0.1:5173/`
  - API URL: `http://127.0.0.1:5088`
  - roomId: `p7-final-1777957045775`
  - Operation path: set room id -> `双人入座` -> `双方准备` -> seed `basic-play` -> submit prompt-enabled `PLAY_CARD` -> P1 `PASS_PRIORITY` -> P2 `PASS_PRIORITY` -> P1 `END_TURN` -> seed `battle-declare` -> submit prompt-enabled `DECLARE_BATTLE` -> P1 `Stop` -> P1 `Reconnect`.
  - Event summary: product event log showed `CARD_PLAYED`, `STACK_ITEM_RESOLVED`, `TURN_END_DECLARED`, `BATTLE_DECLARED`, and `DAMAGE_APPLIED`.
  - Final snapshot summary: roomStatus `IN_PROGRESS`, turn `#95`, active player `P1`, timing `NEUTRAL_OPEN`, stack `0`; P1 battlefield contains `P1-BATTLE-ATTACKER-001`, P2 graveyard contains `P2-BATTLE-DEFENDER-001`, scores `P1=0` and `P2=0`; P1 reconnect status showed `reconnected`.
