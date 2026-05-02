# Current P2.5 Status

更新时间：2026-05-02

这是 P2.5 开发期测试 UI 的短状态文件。P2 core rules preflight 完成状态仍以 `docs/CURRENT_P2_STATUS.md` 为准。

## Goal

完成第一批可用开发测试 UI，让人工测试者可以快速连接本地 `Riftbound.Api` 的 SignalR `GameHub`，创建或加入房间，分别操作 P1/P2，查看真实 snapshot、prompt、events、errors、command log，并提交最小 intent。

## P2 Entry Check

- P2 core rules preflight：`811/811 = 100.0%`
- 最小 card behavior registry：`794/811 = 97.9%`
- 可打出官方牌差集：已清空
- 最近全量验证记录：`dotnet test Riftbound.slnx --no-restore` 通过 `1623/1623`
- 当前 P2.5 不进入 P3 `BehaviorSpec` / template executor，不扩展全卡牌系统。

## Scope

第一批范围：

- 新增独立 `src/Riftbound.DevUi` React + Vite 开发测试台。
- 支持配置 server URL、roomId、P1/P2 playerId。
- 支持 `JoinRoom`、`Ready`、`RequestSnapshot`。
- 两个独立 SignalR 连接分别代表 P1/P2。
- 显示连接状态、自动重连状态、当前玩家、座位、tick 和房间摘要。
- 显示最新 `Snapshot`、`ActionPrompt`、server events、errors、command log。
- 根据 `ActionPrompt.actions` 提供 `END_TURN`、`PASS_PRIORITY`、`PASS_FOCUS` 最小按钮。
- 提供手写 JSON `SubmitIntent` 面板，所有规则裁定仍交给后端。
- `Riftbound.Api` 仅为本地 dev UI origin 增加 CORS。
- PostgreSQL journal 移除旧的 `(match_id, event_tick, event_order)` 唯一约束；同一 tick 可有多个命令写事件，事件唯一性以 `event_sequence` 为准。

明确不做：

- 最终产品 UI、移动端适配、复杂动画、卡牌美术、完整卡牌图库。
- 前端规则裁定、前端合法性推断。
- P3 card behavior template executor。

## Running Locally

终端 A：

```bash
source scripts/dev-env.sh
ASPNETCORE_URLS=http://127.0.0.1:5088 dotnet run --project src/Riftbound.Api/Riftbound.Api.csproj
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

## Smoke Path

1. 打开 dev UI。
2. 确认 server URL 为 `http://127.0.0.1:5088`。
3. 使用同一 `roomId`，P1 playerId 为 `P1`，P2 playerId 为 `P2`。
4. 点击 `Join Both`。
5. 点击 `Ready Both`。
6. 点击 `Snapshot Both`。
7. 在当前可行动玩家上提交一个来自 prompt 的最小按钮，或在 JSON 面板提交：

```json
{
  "cmdType": "END_TURN"
}
```

8. 确认 events、prompt、snapshot 和 command log 更新。

## Progress

| Item | Status |
|---|---|
| P2 完成状态确认 | Done |
| dev UI 脚手架 | Done |
| P1/P2 SignalR 连接 | Done |
| JoinRoom / Ready / RequestSnapshot | Done |
| ActionPrompt 最小按钮 | Done |
| JSON SubmitIntent 面板 | Done |
| Snapshot / prompt / events / errors / command log 调试视图 | Done |
| Browser Use smoke | Done |
| 全量测试 | Done |
| 提交 | Done |

## Validation

已完成：

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`
- `source ../../scripts/dev-env.sh && npm run build`（在 `src/Riftbound.DevUi`）
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 `1623/1623`
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"` 通过 `1574/1574`
- `git diff --check`
- Browser Use smoke:
  - URL: `http://127.0.0.1:5173`
  - API: `http://127.0.0.1:5088`
  - roomId: `dev-smoke-1777732359709`
  - 操作：`Join Both` -> `Ready Both` -> `Snapshot Both` -> P1 prompt `END_TURN`
  - 结果：P1/P2 snapshot 进入 `IN_PROGRESS`；events 包含 `PLAYER_READY`、`MATCH_STARTED`；`END_TURN` 后 events 包含 `TURN_END_DECLARED`、`TURN_PLAYER_ADVANCED`，snapshot active player 更新为 `P2`。
- Browser Use JSON panel smoke:
  - roomId: `dev-json-1777732396755`
  - 操作：`Join Both` -> `Ready Both` -> P1 JSON `SubmitIntent`:

```json
{
  "cmdType": "END_TURN"
}
```

  - 结果：command log 记录 `SubmitIntent`；events 包含 `TURN_END_DECLARED`；snapshot active player 更新为 `P2`。

待完成：

- 后续批次再继续扩展打出卡牌、目标选择、支付费用、fixture 导入导出等开发测试入口。
