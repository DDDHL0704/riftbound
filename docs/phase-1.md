# 第一阶段开发计划

## 阶段目标

建立 .NET 服务端的最小生产化骨架：协议、SignalR 入口、房间串行、幂等、事件日志、玩家视角快照和基于五份 PDF/FAQ 的 conformance tests。旧 Java 输出只作为 legacy oracle 对照。

## 当前进展

- 本机已安装 .NET 10.0.203、PostgreSQL 16、Redis、Node 24。
- `Riftbound.slnx` 已建立，solution 级 build/test 可用。
- `Riftbound.Persistence` 已接入 `IMatchJournal` 和 PostgreSQL P1 schema；journal 行已写入 `ruleset_version`、`faq_version`、`rules_audit_status`、`rules_evidence`。
- `Riftbound.CardCatalog` 已能加载官网快照，并生成功能逻辑单元。
- 当前测试覆盖：
  - 重复 `clientIntentId` 不推进 tick。
  - 协议 envelope 核心字段稳定。
  - 官网快照 1009 条加载。
  - 功能逻辑单元 811 个基线一致。
  - fixture runner 可读取 command log 格式并回放到 `MatchSession`。
  - 已从旧 Java 导出首批 3 条 legacy fixture：`java-oracle-p1-pass`、`java-oracle-p1-end-turn`、`java-oracle-p1-duplicate-pass`。
  - C# 测试可读取 Java fixture 元数据。
  - `PASS`、`END_TURN`、重复 `PASS` 的事件日志 fixture 已与 C# 当前规则骨架对齐；已完成首轮 FAQ 冲突检查，但最终 expected 仍需按协议语义重审。
  - `ConformanceFixture` 已能读取可选 `rulesEvidence`、`faqVersion`、`auditStatus`、`seed`；旧 Java fixture 仍因 `auditStatus = NEEDS_RULE_AUDIT` 被视为 `RequiresRuleAudit`。
  - `docs/rules-evidence-index.md` 已建立五份 PDF/FAQ 到规则域和当前 fixture 的索引。
  - Java exporter 已输出 `legacyOracle`，并暂时保留旧 `oracle` 兼容字段。
  - P1 SQL 已补 ruleset/FAQ/audit 字段和 `oracle_fixtures` 索引表；启动时按 `Sql/*.sql` 顺序初始化/迁移。
  - 当前 3 条 Java legacy fixture 的 evidence 已细化；FAQ 未发现推翻通用 `PASS` / `END_TURN` 的条目，但 `PASS -> TURN_ENDED` 被标记为 legacy mismatch candidate。
  - `docs/p2-rules-preflight.md` 已建立 P2 前置审查，覆盖符文池、`END_TURN`、`PASS_PRIORITY`、`PASS_FOCUS`、清理/特殊清理、最小状态模型、事件词表和首批 P2 fixture。
  - `WsClientMessage` / `WsServerMessage` 已带默认 `protocolVersion = 1`、`schemaVersion = 1`，并有 canonical JSON 测试固定 camelCase envelope。
  - 协议层已新增 `PASS_PRIORITY`、`PASS_FOCUS`，并记录 `END_TURN` 与 legacy `PASS` 的语义边界。
  - `MatchSession` 已能分配稳定 `P1` / `P2` 座位，snapshot 暴露 seat，第三名玩家加入会被拒绝。
  - `GameHub.JoinRoom` 已有最小 SignalR 级测试，覆盖双人加入、room/player group、snapshot/prompt 推送和满员错误。
  - `GameHub.Reconnect`、`GameHub.RequestSnapshot`、`GameHub.Ready`、稳定 `ErrorDto` 错误码和内存重连 token 已有最小 Hub 级测试。
  - P1 SQL 和 `PostgresMatchJournal` 已补全局 `event_sequence`、command 起止 sequence、snapshot/prompt `last_event_sequence`；新库/旧库迁移 smoke 通过。
  - `MatchJournalEntry` 已携带客户端原始命令 JSON；`GameHub.SubmitIntent` 到 journal 的 raw payload 已有测试覆盖，PostgreSQL `command_log.payload.rawCommand` 可用于回放和审计。
  - `IMatchRecoveryStore` 和 `PostgresMatchRecoveryStore` 已能读取 match、command、events、最新 snapshot/prompt，`MatchRecoveryValidator` 已覆盖 event sequence 连续性、command 边界和 player view sequence；本机 PostgreSQL smoke 通过。
  - `001_p1_event_store.sql` 不再创建依赖后续迁移新列的 sequence 索引，旧库升级顺序已恢复为 `001 -> 002 -> 003`。
  - `MatchSession` 已接入最小房间生命周期：新房 `EMPTY`，入座后 `SEATING`，双方 `Ready` 后进入 `IN_PROGRESS`；`FINISHED` 已保留为后续结算状态。
  - `Ready` 会记录 `PLAYER_READY` / `MATCH_STARTED` lifecycle events，Hub 会以 `READY` / `START` 消息广播；snapshot/prompt 暴露 ready 状态，conformance runner 会先 Ready 再回放规则命令，但比较时过滤 lifecycle events。
  - `MatchSession.SubmitAsync` 已要求玩家先 JoinRoom、携带非空 `clientIntentId` 且房间已开始；Hub 级错误码测试覆盖未知玩家提交、空 intent、未开局提交、unsupported command 和重复 intent 冲突。
  - 手写 C# placeholder fixture 已迁移为 `PASS_PRIORITY`；裸 `PASS` 只保留在 Java legacy fixture 和兼容测试中。
  - `InMemoryMatchSessionRegistry` 已接入异步恢复入口；恢复时只恢复 P1 底座所需的 tick、turn、active player、seat、lastEventSequence 和已见过 intent，避免把玩家视角 snapshot 误当完整规则状态。
  - `state_snapshots` 权威状态快照表和 `004_p1_state_snapshots.sql` 已加入；journal 写入服务端 `MatchState`，recovery 优先读取与当前 `last_event_sequence` 对齐的权威状态，并校验玩家视角 snapshot 与权威状态一致。
  - `IMatchPlayerStore` 和 `PostgresMatchPlayerStore` 已接入；Join/Reconnect 只持久化 `sha256:` reconnect token hash，恢复后的 session 可用旧 token hash 重连，并在成功重连后轮换新 token/hash；恢复后已有座位但没有 live token 的玩家必须走 Reconnect。
  - P2 preflight 第一刀已完成：fixture schema v2 可读取 `initialState`、`expected.finalState/events/prompts`；新增 `p2-preflight-turn-start.fixture.json` 作为回合开始、召出符文、抽牌、清空符文池的规则审查样例。
  - `MatchState` 已加入 `turnPlayerId`、`phase`、`timingState`、`runePools`，snapshot timing 已投影这些 P2 权威字段；当前只是状态地基，尚未实现真实符文池/回合开始自动结算。

下一步继续 P2 preflight：先让 runner 能把 `initialState` 应用到权威 `MatchState`，再实现符文池和回合开始流程；随后实现 `END_TURN` 特殊清理、`PASS_PRIORITY` / FEPR、`PASS_FOCUS` / 法术对决最小流程。协议版本治理剩余 TypeScript DTO 生成、客户端兼容策略、SignalR 方法版本和事件 upcaster；不要在没有前端接入点前过度设计。新增 fixture 不再使用裸 `PASS`。

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
- P2 fixture schema 能表达 `initialState`、权威最终状态、事件序列、玩家 prompt；当前已有回合开始 preflight 样例。
- `MatchState` 能保存并投影 P2 基础权威字段：`turnPlayerId`、`phase`、`timingState`、`runePools`。
- 官网卡牌快照可在新项目中加载，1009 官方条目和 811 功能逻辑单元基线测试通过。
- solution 级 `restore/build/test` 可作为新窗口默认验证入口。
- PDF/FAQ 规则依据覆盖首批高价值路径。
- Java legacy oracle fixture 至少覆盖 10 条高价值路径，或已记录与 PDF/FAQ 的差异。
- C# conformance tests 能读取 fixture 并输出稳定 diff。
- 一旦开发期测试 UI 接入，阶段验收必须用 Codex 内置浏览器执行真实 P1/P2 smoke，并记录 roomId、操作路径、事件和最终 snapshot 摘要。
