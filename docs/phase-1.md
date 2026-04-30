# 第一阶段开发计划

## 阶段目标

建立 .NET 服务端的最小生产化骨架：协议、SignalR 入口、房间串行、幂等、事件日志、玩家视角快照和基于五份 PDF/FAQ 的 conformance tests。旧 Java 输出只作为 legacy oracle 对照。

## 当前进展

P1 联机底座和 P2 preflight 基础设施已完成到可持续迁移卡牌的状态。当前短状态以 `docs/CURRENT_P2_STATUS.md` 为准；完整 fixture、规则证据和 P2 进度分别维护在 `docs/p2-rules-preflight.md` 与 `docs/rules-evidence-index.md`。

阶段性快照：

- 本机环境：.NET 10.0.203、PostgreSQL 16、Redis、Node 24。
- solution 级 build/test 可用。
- `CoreRuleEngine` 已接入 API DI，并保留 `PlaceholderRuleEngine` 作为 legacy fallback。
- P2 schema v2、`MatchState` 权威字段、richer expected diff、符文池、回合开始/结束、优先权/焦点让过、燃尽、清理、基础卡牌结算路径已落地。
- 最近全量验证：`dotnet test Riftbound.slnx --no-restore` 通过 `312/312`。
- 最小 card behavior registry：`148/811 = 18.2%`。
- P2 preflight 清单已完成到 `179`，下一项继续迁移低复杂度官方卡牌。

本文件不再维护完整已覆盖卡牌长清单，避免新窗口加载和每卡同步时反复消耗上下文。需要精确卡牌状态时读：

- `docs/CURRENT_P2_STATUS.md`：当前短交接。
- `docs/p2-rules-preflight.md`：P2 fixture 表、事件词表和进度项。
- `docs/rules-evidence-index.md`：每个 fixture 的规则证据。

## 不做范围

- 不迁移全部卡牌。
- 不重做最终产品级前端 UI；允许在联机底座可用后做简洁精美的开发期测试 UI。
- 不实现复杂 AI。
- 不做移动端适配。
- 不做多实例房间热迁移。

## 验收清单

- `GameHub` 可让 P1/P2 加入同一房间。
- `MatchSession` 为前两名加入者分配稳定 `P1`/`P2` 座位，并拒绝第三名玩家。
- `GameHub.Ready` 可让双方准备；第一名 Ready 广播 `READY`，第二名 Ready 广播 `START` 并进入 `IN_PROGRESS`。
- `MatchSession` 对同一房间命令严格串行。
- 未 Join 的玩家提交命令会被拒绝为 `PLAYER_NOT_IN_ROOM`，不再隐式入座。
- 空 `clientIntentId` 会被拒绝为 `CLIENT_INTENT_ID_REQUIRED`，服务端不再随机生成 intentId。
- 未 Ready 开局前提交命令会被拒绝为 `MATCH_NOT_STARTED`。
- `clientIntentId` 重复提交不推进 tick，不重复写事件。
- `UNSUPPORTED_COMMAND` 和 `CLIENT_INTENT_CONFLICT` 在 Hub 层有稳定错误码测试。
- 协议层明确区分 `PASS_PRIORITY`、`PASS_FOCUS`、`END_TURN`，裸 `PASS` 仅用于 legacy 兼容。
- WebSocket/SignalR envelope 带默认 `protocolVersion` 和 `schemaVersion`。
- `IRuleEngine` 的输出能被事件和快照投影消费。
- PostgreSQL EventStore schema 草案完成，包含规则版本/FAQ/audit 元数据，并能由开发环境启动时初始化。
- 事件日志具有 match 内单调 `event_sequence`，command/snapshot/prompt 能记录对应 sequence 边界。
- command log payload 保留客户端原始命令 JSON，用于后续 command log 回放和审计。
- recovery 读取/校验路径可从 PostgreSQL 取回事件流和最新玩家视图，并拒绝 event sequence 缺口或未来 snapshot。
- RoomRegistry/MatchSession 可从 recovery frame 恢复 P1 底座状态；重启后的重复 intent 不推进 tick、不重复写 journal。
- `state_snapshots` 保存服务端权威 `MatchState`，恢复时优先使用权威状态而不是从玩家视角 snapshot 反推。
- `match_players.reconnect_token_hash` 保存 reconnect token hash，不保存明文；重连成功会轮换 token/hash。
- 恢复后的已有玩家不能通过 Join 直接领取新 token，必须用旧 token 走 Reconnect 验证。
- P2 fixture schema 能表达 `initialState`、权威最终状态、事件序列、玩家 prompt；runner 能把 `initialState` 应用为真实权威状态，当前已有回合开始 preflight 样例。
- `MatchState` 能保存并投影 P2 基础权威字段：`turnPlayerId`、`phase`、`timingState`、`runePools`、`playerZones`、`playerScores`。
- 官网卡牌快照可在新项目中加载，1009 官方条目和 811 功能逻辑单元基线测试通过。
- solution 级 `restore/build/test` 可作为新窗口默认验证入口。
- PDF/FAQ 规则依据覆盖首批高价值路径。
- Java legacy oracle fixture 至少覆盖 10 条高价值路径，或已记录与 PDF/FAQ 的差异。
- C# conformance tests 能读取 fixture 并输出稳定 diff。
- 一旦开发期测试 UI 接入，阶段验收必须用 Codex 内置浏览器执行真实 P1/P2 smoke，并记录 roomId、操作路径、事件和最终 snapshot 摘要。
