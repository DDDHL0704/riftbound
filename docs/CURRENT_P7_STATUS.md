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
| P7.1 | Planned | Product room, two-player connection, reconnect, snapshot/prompt/event shell. | Browser smoke: join, ready, reconnect. |
| P7.2 | Planned | Battle desktop layout with lanes/base/hand/runes/legend/champion/equipment/control markers. | Browser visual smoke. |
| P7.3 | Planned | ActionPrompt-driven play/pass/end-turn/move/battle controls. | Browser smoke: play, pass, end turn, move/battle. |
| P7.4 | Planned | Payment and target-selection UX, response windows, spell-duel surface. | Browser smoke: target and response flow. |
| P7.5 | Planned | Status, equipment, damage, temp power, exhaustion, combat, shield/swift/ephemeral markers. | Browser visual smoke. |
| P7.6 | Planned | Event log, match report, replay/spectator entry with backend boundary. | Browser smoke: report and boundary. |
| P7.7 | Planned | Catalog and card detail browser with BehaviorSpec status and deferred labels. | Browser smoke: catalog/detail filtering. |
| P7.8 | Planned | Product polish, loading/empty/error/disconnect states, stable desktop sizing. | Browser visual smoke. |
| P7.x | Planned | Final full validation, status sync, clean status, commit. | Browser smoke + full backend test gate. |

Current P7 progress: `1/10 batches = 10.0%`; estimated remaining batches: `9`.

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

## Browser Smoke Records

- Pending.
