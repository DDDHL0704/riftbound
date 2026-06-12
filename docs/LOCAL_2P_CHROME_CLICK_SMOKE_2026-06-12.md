# Local 2P Chrome Click Smoke - 2026-06-12

Status: NOT READY. Independent local playability smoke record only.

## Scope

- Worktree: `/Users/dinghaolin/MyProjects/riftbound-local-2p-smoke`
- Branch: `codex/local-2p-smoke-20260612`
- Browser: Chrome plugin, two visible local tabs
- Frontend: `http://127.0.0.1:5173` and `http://localhost:5173`
- Backend: `http://127.0.0.1:5088`
- Persistence: no local Postgres; backend was restarted with `ConnectionStrings__Riftbound=` and no-persistence stores.

This smoke used actual page clicks for room join, deck submit, ready, mulligan, rune tap, play card, priority pass, standard move, and turn advancement. It did not continue Stage 4D triggerQueue/runtime closure work and did not edit shared board, completion audit, or closure plan docs.

## Rule Source Checked

- Official Rules Hub / Core Rules PDF: `https://riftbound.leagueoflegends.com/en-us/rules-hub/`
- Official Origins FAQ: `https://riftbound.leagueoflegends.com/en-us/news/rules-and-releases/riftbound-origins-faq/`

Smoke interpretation used the public rule distinction that standard move exhausts the moving unit; card/spell/ability movement effects are not treated as standard move unless implemented through the player `MOVE_UNIT` action.

## Pre-Fix Reproduction

Room: `click-smoke-1781255520297`

Actual clicks performed:

- P1 and P2 joined the same room.
- Both submitted the default legal deck and readied.
- Both entered the match page and connected.
- Both completed mulligan.
- P1 tapped two runes by opening card details and clicking `横置符文`.
- P1 played `OGN·010/298 军团后卫` to base from the card detail drawer.
- P1 and P2 clicked `让过优先权`; the unit resolved into base.
- P1 opened `军团后卫`, chose opponent battlefield `OGN·275/298`, and clicked `确认移动`.

Observed blocker:

- `UNIT_MOVED_TO_BATTLEFIELD` was logged, but `军团后卫` still displayed `正常` instead of `横置`.
- The main action prompt still exposed movement for a unit that should have paid standard-move exhaustion.

## Fix Applied

Runtime:

- `src/Riftbound.Engine/CoreRuleEngine.cs`
  - rejects `MOVE_UNIT` when the source unit is already exhausted.
  - exhausts the `MOVE_UNIT` source after accepted standard movement.
- `src/Riftbound.Engine/MatchSession.cs`
  - filters `MOVE_UNIT` prompt sources to ready, face-up, controlled, non-combat units.
  - updates `MOVE_UNIT` source policy metadata to ready-source wording.
- `src/Riftbound.DevUi/src/services/starterDeck.ts`
  - adds a hidden local smoke override for `buildStarterDeck()` via `?starterDeckOverride=<json>` or `localStorage["riftbound.dev.starterDeckOverride"]`.
  - default starter deck behavior is unchanged when no override is supplied.

Tests and fixtures were updated only for the standard `MOVE_UNIT` state expectation.

## Validation

Focused backend:

- `DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:/opt/homebrew/opt/node@24/bin:/opt/homebrew/opt/postgresql@16/bin:$PATH dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MoveUnit|FullyQualifiedName~BoardTaskQueueFoundationTests|FullyQualifiedName~JhinMovementResourceSkillTests"`
- Result: 108/108 passed.

Adjacent backend:

- `DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:/opt/homebrew/opt/node@24/bin:/opt/homebrew/opt/postgresql@16/bin:$PATH dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~MoveUnit|FullyQualifiedName~GameHub"`
- Result: 1111/1111 passed after rebasing onto latest `origin/main`.

Backend health after restart:

- `GET http://127.0.0.1:5088/health`
- Result: `{"status":"ok","service":"riftbound-dotnet","role":"migration-skeleton","dotnet":"10.0.0"}`

Frontend:

- `PATH=/opt/homebrew/opt/node@24/bin:$PATH npm run build` in `src/Riftbound.DevUi`
- Result: passed (`check:event-labels`, `check:user-facing-text`, `tsc -b`, `vite build`).

## Post-Fix Chrome Click Smoke

Room: `move-fix-click-1781256898116`

Actual clicks performed:

- P1 and P2 opened the same room from separate origins.
- P1 clicked `连接/重连并入座`.
- P2 clicked `连接/重连并入座`.
- P1 clicked `提交卡组`.
- P2 clicked `提交卡组`.
- P1 clicked `准备`.
- P2 clicked `准备`.
- P1 and P2 clicked `进入对战桌面`.
- P1 and P2 clicked match-page `连接/重连`.
- P2 clicked `确认起手调整`.
- P1 clicked `确认起手调整`.
- P2 was first player and clicked `结束回合`.
- P1 clicked `OGN·007a/298 炽烈符文`, then detail `横置符文`.
- P1 clicked `SFD·R03b 灵光符文`, then detail `横置符文`.
- P1 clicked `OGN·010/298 军团后卫`, selected `基地`, and clicked `确认打出`.
- P1 clicked `让过优先权`.
- P2 clicked `让过优先权`.
- P1 clicked `军团后卫`, selected opponent battlefield `OGN·275/298`, and clicked `确认移动`.
- P1 clicked `结束回合`.

Observed post-fix state:

- `军团后卫` moved to `OGN·275/298 团结圣坛`.
- The moved unit displayed `横置`.
- The P1 prompt no longer exposed `移动单位` for that exhausted unit.
- P1 end turn advanced the match to `第 3 回合｜主阶段｜普通开环` with P2 active.

## Regular Legend Chrome Click Attempt

Scope: regular-version representative legends only; same legend name only one representative. Decks were generated by the same logic as `src/Riftbound.DevUi/scripts/local-legend-batch-smoke.mjs` and submitted through the actual room page `提交卡组` button by passing the generated deck through the URL override.

Output file:

- `/tmp/riftbound-chrome-regular-page-click-smoke-1781258377517.json`

Completed actual Chrome page-click rooms: 7/40.

Passed representatives:

- `傲之追猎者` / `UNL-183/219`
- `奥术先驱` / `OGN·265/298`
- `暴走萝莉` / `OGN·251/298`
- `不灭狂雷` / `OGN·249/298`
- `愁云使者` / `UNL-193/219`
- `翠神` / `UNL-195/219`
- `刀锋舞者` / `SFD·195/221`

Each completed room used actual Chrome page clicks for both clients: join room, submit generated deck, ready, enter match desktop, connect/reconnect, complete mulligan, tap runes, attempt to play cards through the card detail composer, pass priority when stack resolution required it, and end turns through at least three main phases. The completed rooms all observed draw, rune tap, card play, cost payment, priority pass, and turn advancement in the visible event log.

Tool blocker:

- The Chrome plugin connection repeatedly failed with `native pipe is closed` while retrying representative #8 (`德玛西亚之力` / `OGS·023/024`).
- This stopped full 40/40 page-click coverage. It is recorded as a Chrome automation/tooling blocker, not as a product P0/P1 local 2P blocker.

## P0/P1 Findings

- P0: none after the `MOVE_UNIT` fix for this local two-player click path.
- P1 fixed in this branch: standard `MOVE_UNIT` did not exhaust the moving unit and allowed already-exhausted sources through the API/prompt path.
- P1 remaining: `OGN·010/298 军团后卫` still enters base as `正常` when played without paying the HASTE_READY optional cost. This smoke did not widen the fix to unit entry timing because that touches broader Haste/entry rules and existing fixture expectations.
- P1 existing environment issue: backend startup is fragile on this machine with the configured Postgres connection; no-persistence startup via `ConnectionStrings__Riftbound=` remains required for local smoke.
- Tooling blocker: full regular-version representative Chrome page-click coverage stopped at 7/40 because the Chrome plugin pipe closed repeatedly at representative #8.

## Remaining Coverage

- The prior batch harness covered 40 regular-version legend representatives, same legend name only one representative, but that was SignalR-driven rather than page-clicked.
- Full regular-version legend coverage with actual Chrome clicks remains open from representative #8 onward.
- Project remains NOT READY; full official card matrix, broader P0/P1 runtime closure, DB-backed persistence smoke, formal E2E, and final readiness remain open.
