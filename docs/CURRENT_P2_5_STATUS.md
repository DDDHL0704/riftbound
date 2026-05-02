# Current P2.5 Status

更新时间：2026-05-02

这是 P2.5 开发期测试 UI 的短状态文件。P2 core rules preflight 完成状态仍以 `docs/CURRENT_P2_STATUS.md` 为准。

## Goal

完成 P2.5 开发期测试 UI：人工测试者可以快速连接本地 `Riftbound.Api` 的 SignalR `GameHub`，创建或加入房间，分别操作 P1/P2，查看真实 snapshot、prompt、events、errors、command log，使用 ActionPrompt 提交最小 intent，并通过开发场景 seed 快速进入基础打牌、移动、法术窗口、装备、控制、战斗计分和指定手牌场景。

## P2 Entry Check

- P2 core rules preflight：`811/811 = 100.0%`
- 最小 card behavior registry：`794/811 = 97.9%`
- 可打出官方牌差集：已清空
- 本轮全量验证记录：`dotnet test Riftbound.slnx --no-restore` 通过 `1627/1627`
- 当前 P2.5 不进入 P3 `BehaviorSpec` / template executor，不扩展最终产品 UI。

## Scope

已完成：

- 独立 `src/Riftbound.DevUi` React + Vite 开发测试台，第一屏就是实际 GameHub 测试台。
- 支持配置 server URL、roomId、P1/P2 playerId。
- 支持 `JoinRoom`、`Reconnect`、`Ready`、`RequestSnapshot`。
- 两个独立 SignalR 连接分别代表 P1/P2，显示连接状态、重连状态、座位、tick、prompt 状态和房间摘要。
- Battle Desk 显示双方可见 zones、rune pool、score、hand count、base、battlefields、legend/champion、graveyard/banished、stack。
- Snapshot 投影扩展为开发视图：当前玩家可见自己的手牌，对手手牌只显示 `handHidden` 数量，不暴露对手手牌对象。
- Operation Panel 显示当前 ActionPrompt，提供 `END_TURN`、`PASS_PRIORITY`、`PASS_FOCUS`、`PASS`、`READY` 等最小按钮。
- `PLAY_CARD` builder 支持 sourceObjectId、cardNo、targetObjectIds、mode、optionalCosts，并可从可见 object chip/target palette 选目标。
- 手写 JSON `SubmitIntent` 面板保留，方便复现 conformance fixture 场景。
- Fixture Draft 面板可基于 command log 生成一份最小 fixture 草稿并复制。
- Development-only `GameHub.SeedScenario`：`basic-play`、`movement`、`spell-duel`、`equipment`、`control`、`battle-score`、`specified-hand`。
- `Riftbound.Api` 为本地 dev UI origin 增加 CORS。
- PostgreSQL journal 移除旧的 `(match_id, event_tick, event_order)` 唯一约束；同一 tick 可有多个命令写事件，事件唯一性以 `event_sequence` 为准。

明确不做：

- 最终产品 UI、移动端适配、复杂动画、卡牌美术、完整卡牌图库。
- 前端规则裁定、前端合法性推断。
- P3 card behavior template executor。
- 规则 PDF/FAQ 提交。

## Running Locally

终端 A：

```bash
source scripts/dev-env.sh
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5088 dotnet run --project src/Riftbound.Api/Riftbound.Api.csproj
```

终端 B：

```bash
source scripts/dev-env.sh
cd src/Riftbound.DevUi
npm install
npm run dev
```

默认访问：

- API health: `http://127.0.0.1:5088/health`
- Dev UI: `http://127.0.0.1:5173`
- GameHub: `http://127.0.0.1:5088/hubs/game`

`SeedScenario` 只应在 Development 环境使用；Production 环境下 hub 会返回 `UNSUPPORTED_COMMAND`。

## Smoke Path

1. 打开 dev UI。
2. 确认 server URL 为 `http://127.0.0.1:5088`。
3. 使用同一 `roomId`，P1 playerId 为 `P1`，P2 playerId 为 `P2`。
4. 点击 `Join Both`。
5. 点击 `Ready Both`。
6. 点击 `Snapshot Both`。
7. 点击 `Basic Play` seed，使用 `PLAY_CARD Builder` 提交 `P1-UNIT-MIGHTY-FAERIE`，P1/P2 依次 `PASS_PRIORITY`，确认 `UNIT_PLAYED_TO_BASE`。
8. 点击 `Movement` seed，提交 `P1-SPELL-RIDE-THE-WIND` 指向 `P1-BATTLEFIELD-UNIT-001`，P1/P2 依次 `PASS_PRIORITY`，确认 `UNIT_MOVED_*`。
9. 点击 `Battle Score` seed，点击 P1 `END_TURN`，确认 `BURNOUT_APPLIED` 和 score 更新。
10. 点击 `Refresh Draft`，确认 fixture draft 包含已提交 commands。

## Progress

| Item | Status |
|---|---|
| P2 完成状态确认 | Done |
| dev UI 脚手架 | Done |
| P1/P2 SignalR 连接与重连 | Done |
| JoinRoom / Ready / RequestSnapshot | Done |
| ActionPrompt 最小按钮 | Done |
| PLAY_CARD builder 与目标选择 | Done |
| Development scenario seeds | Done |
| Battle Desk / Operation Panel / Debug Panel | Done |
| Fixture draft copy/export helper | Done |
| Snapshot 对手手牌隐藏边界测试 | Done |
| GameHub SeedScenario Development guard 测试 | Done |
| Browser Use smoke | Done |
| 全量测试 | Done |
| 提交 | Done |

## Validation

已完成：

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`
- `source ../../scripts/dev-env.sh && npm run build`（在 `src/Riftbound.DevUi`）
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureShapeTests"` 通过 `21/21`
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` 通过 `16/16`
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 `1627/1627`
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"` 通过 `1574/1574`
- Browser Use smoke:
  - URL: `http://127.0.0.1:5173`
  - API: `http://127.0.0.1:5088`
  - roomId: `dev-full-1777733504005`
  - 操作：`Join Both` -> `Ready Both` -> `Snapshot Both`
  - Basic Play：seed `basic-play` -> `PLAY_CARD P1-UNIT-MIGHTY-FAERIE` -> P1/P2 `PASS_PRIORITY` -> events 包含 `UNIT_PLAYED_TO_BASE`
  - Movement：seed `movement` -> `PLAY_CARD P1-SPELL-RIDE-THE-WIND` targeting `P1-BATTLEFIELD-UNIT-001` -> P1/P2 `PASS_PRIORITY` -> events 包含 `UNIT_MOVED_*`
  - Battle Score：seed `battle-score` -> P1 `END_TURN` -> events 包含 `BURNOUT_APPLIED`，snapshot 显示 P1 score `8`
  - Fixture Draft：`Refresh Draft` 后草稿包含 `PLAY_CARD` 和 `END_TURN`

- `git diff --check`
