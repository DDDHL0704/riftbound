# 新窗口接手指南

更新时间：2026-04-28

这份文档用于在新 Codex 窗口中快速恢复上下文，确保后续开发不偏离《符文战场》的最终目标。

## 1. 一句话目标

构建一款精美、稳定、可双人联机对战、服务端权威结算、可断线重连、可回放、可长期生产化维护的 Web 卡牌游戏。

关键原则：

- 玩家只提交行为意图。
- 服务端根据五份官方 PDF、官网卡面和当前状态自动裁决并结算。
- 前端只渲染服务端下发的 `Prompt`、`Events`、`PlayerSnapshot` 和错误信息。
- 每个规则能力都必须能通过 command log 回放，并以 PDF/FAQ 规则依据作为最终裁决；Java 输出只作为旧实现对照。

## 2. 当前项目边界

主要工作区：

- 新项目：`/Users/dinghaolin/MyProjects/riftbound-dotnet`
- 旧 Java 参考项目：`/Users/dinghaolin/MyProjects/riftbound-server`

最高优先级资料：

1. `/Users/dinghaolin/MyProjects/riftbound-dotnet/《符文战场》核心规则_260330.pdf`
2. `/Users/dinghaolin/MyProjects/riftbound-dotnet/裁判FAQ_251023.pdf`
3. `/Users/dinghaolin/MyProjects/riftbound-dotnet/铸魂淬炼系列_裁判FAQ.pdf`
4. `/Users/dinghaolin/MyProjects/riftbound-dotnet/铸魂淬炼系列_官方FAQ_260114.pdf`
5. `/Users/dinghaolin/MyProjects/riftbound-dotnet/《符文战场》破限系列_裁判FAQ_260416.pdf`
6. `/Users/dinghaolin/MyProjects/riftbound-dotnet/data/official/card-catalog.zh-CN.json`
7. 旧 Java 项目的实现、测试、生成矩阵和迭代日志

当前固定 Java 参考点：

- `75bf7cf feat: expand equipment and control card behaviors`

注意：

- 规则 PDF/FAQ 是未跟踪文件，不要提交。
- 不回退现有改动。
- 旧 Java 代码是历史行为参考，不是新项目架构模板；如果它与 FAQ 或官网卡面冲突，以 PDF/FAQ/官网卡面为准。

## 3. 新窗口先读顺序

打开新窗口后，按这个顺序读即可进入状态：

1. `README.md`
2. `docs/START_HERE.md`
3. `docs/master-development-plan.md`
4. `docs/phase-1.md`
5. `docs/rules-authority-and-audit.md`
6. `docs/development-audit-status.md`
7. `docs/rules-evidence-index.md`
8. `docs/protocol-semantics.md`
9. `docs/rules-card-baseline.md`
10. `docs/p2-rules-preflight.md`
11. `docs/conformance-fixture-format.md`
12. 如需迁移背景，再读旧项目的 `docs/dotnet-migration-plan.md`

如果上下文不够，再回到旧 Java 项目阅读：

- `README.md`
- `docs/current-development-status.md`
- `docs/iteration-log-260330.md` 最新章节
- `docs/multiplayer-auto-resolution-protocol.md`
- `docs/card-behavior-regression-plan.md`

## 4. 当前状态

新项目已建立第一阶段骨架：

- `Riftbound.Contracts`：协议 DTO 草案。
- `Riftbound.Engine`：`MatchSession`、串行处理、幂等占位。
- `Riftbound.Api`：ASP.NET Core + SignalR `GameHub` 草案。
- `Riftbound.Persistence`：PostgreSQL 事件日志 schema 与 `IMatchJournal` 实现。
- `Riftbound.CardCatalog`：官网卡牌快照加载与功能逻辑单元分组。
- `Riftbound.ConformanceTests`：conformance 测试骨架。

已完成的本机环境：

- .NET SDK：10.0.203，安装在 `~/.dotnet`。
- PostgreSQL：16.13，Homebrew service 已启动。
- Redis：6.2.4，Homebrew service 已启动。
- Node：24.15.0，安装在 `/opt/homebrew/opt/node@24/bin`。
- 本地数据库：`riftbound_dev`。

本机当前限制：

- .NET 10 SDK 已安装到 `~/.dotnet`。
- PostgreSQL 16、Redis、Node 24 已可用于本地开发。
- 新终端先执行 `source scripts/dev-env.sh`，确保当前 shell 使用正确版本。

最近一次验证：

- `source scripts/dev-env.sh` 通过。
- `dotnet build Riftbound.slnx --no-restore` 通过。
- `dotnet test Riftbound.slnx --no-build` 通过，当前 70 个测试。
- Java oracle exporter 已导出 `java-oracle-p1-pass.fixture.json`、`java-oracle-p1-end-turn.fixture.json`、`java-oracle-p1-duplicate-pass.fixture.json`。
- C# 测试已能读取 Java fixture 元数据，并对齐 `PASS`、`END_TURN`、重复 `PASS` 的首批事件日志 fixture；这些 fixture 现在默认 `RequiresRuleAudit`。
- `ConformanceFixture` 已能读取可选的 `rulesEvidence`、`faqVersion`、`auditStatus`、`seed` 字段。
- `docs/rules-evidence-index.md` 已建立五份 PDF/FAQ 的规则域索引和当前 fixture 审查映射。
- Java exporter 已输出 `legacyOracle`，并暂时保留旧 `oracle` 兼容字段。
- P1 PostgreSQL schema 和 `PostgresMatchJournal` 已补 `ruleset_version`、`faq_version`、`rules_audit_status`、`rules_evidence`；schema initializer 会按文件名顺序执行 `Sql/*.sql`。
- 当前 3 条 Java legacy fixture 的 evidence 已细化到具体页码、规则号/FAQ 问题号，并记录 `PASS -> TURN_ENDED` 是 legacy mismatch candidate。
- `docs/p2-rules-preflight.md` 已建立 P2 前置审查：符文池、`END_TURN`、`PASS_PRIORITY`、`PASS_FOCUS`、清理/特殊清理的裁决、最小状态模型、事件词表和首批 P2 fixture。
- `WsClientMessage` / `WsServerMessage` 已接入默认 `protocolVersion = 1`、`schemaVersion = 1`；canonical JSON 测试已固定 camelCase envelope 字段。
- `docs/protocol-semantics.md` 已明确 `PASS_PRIORITY`、`PASS_FOCUS`、`END_TURN` 的协议边界；`PASS` 仅保留为 legacy 兼容。
- 手写 C# placeholder fixture 已迁移为 `p1-placeholder-pass-priority.fixture.json`，使用显式 `PASS_PRIORITY`；裸 `PASS` 只保留在 Java legacy fixture 和兼容测试中。
- `MatchSession` 已能为前两名加入者分配稳定 `P1` / `P2` 座位，并在 snapshot 的 `players` 视图中暴露 seat；第三名玩家会被拒绝。
- `GameHub.JoinRoom` 已有最小 SignalR 级测试，覆盖双人加入、room/player group、snapshot/prompt 推送和第三人满员错误。
- `GameHub.Reconnect`、`GameHub.RequestSnapshot`、`GameHub.Ready`、稳定 `ErrorDto` 错误码和内存重连 token 已有最小 Hub 级测试。
- P1 SQL 和 `PostgresMatchJournal` 已补 `event_sequence`、command 起止 event sequence、snapshot/prompt `last_event_sequence`，并通过新库/旧库迁移 smoke。
- `MatchJournalEntry` 和 `PostgresMatchJournal` 已保留客户端原始命令 JSON；`SubmitIntent` 的原文会进入 `command_log.payload.rawCommand`，便于后续回放、审计和 canonical diff。
- `IMatchRecoveryStore`、`PostgresMatchRecoveryStore` 和 `MatchRecoveryValidator` 已建立最小恢复读取/校验路径：可读取 match、command、events、最新 snapshot/prompt，并校验 event sequence 连续性、command 边界和 player view sequence；本机 PostgreSQL smoke 通过。
- `001_p1_event_store.sql` 已移除依赖 003 新列的 sequence 索引创建，旧库可按 `001 -> 002 -> 003` 顺序升级。
- `MatchSession` 已接入 `EMPTY -> SEATING -> IN_PROGRESS` 的最小房间生命周期；`Ready` 会记录 `PLAYER_READY` / `MATCH_STARTED` lifecycle events，snapshot/prompt 暴露 ready 状态，未 Ready 开局前提交命令会被 `MATCH_NOT_STARTED` 拒绝。
- `MatchSession.SubmitAsync` 已要求玩家先 JoinRoom、携带非空 `clientIntentId` 且房间已开始；Hub 级测试覆盖 `PLAYER_NOT_IN_ROOM`、`CLIENT_INTENT_ID_REQUIRED`、`MATCH_NOT_STARTED`、`UNSUPPORTED_COMMAND`、`CLIENT_INTENT_CONFLICT` 五条命令提交错误路径。
- `InMemoryMatchSessionRegistry` 已接入 `IMatchRecoveryStore` 异步恢复入口；恢复时只恢复 P1 底座所需的 tick、turn、active player、P1/P2 seat、lastEventSequence 和已见过的 intent，用于防止重启后重复提交污染事件流，不把玩家视角 snapshot 当完整权威规则状态；跨 room recovery frame 会被 `RECOVERY_INCONSISTENT` 拒绝。
- `state_snapshots` 权威状态快照表已加入 P1 schema 和 `004_p1_state_snapshots.sql` 迁移；`PostgresMatchJournal` 会写入服务端 `MatchState`，`PostgresMatchRecoveryStore` 会优先读取与当前 `last_event_sequence` 对齐的权威状态，并校验玩家视角 snapshot 与权威状态一致；本机 PostgreSQL state snapshot smoke 通过。
- `IMatchPlayerStore`、`PostgresMatchPlayerStore` 和 `ReconnectTokenHasher` 已接入；Join/Reconnect 会把 reconnect token 以 `sha256:` hash 写入 `match_players.reconnect_token_hash`，恢复后的 session 可用旧 token hash 重连，并在重连成功后轮换新 token/hash；恢复后已有座位但没有 live token 的玩家必须走 Reconnect，不能用 Join 直接领取新 token；本机 PostgreSQL token smoke 通过，库中不保存 token 明文。
- P2 preflight 已开始落代码：`ConformanceFixture` 已能读取 schema v2 的 `initialState`、P2 `expected.finalState/events/prompts`；新增 `p2-preflight-turn-start.fixture.json` 作为规则审查后的回合开始样例。
- conformance runner 已能把 P2 `initialState` 应用为真实权威初始局面，不再走 Join/Ready 覆盖 `TURN_START`；P2 expected 仍未升级为完整规则断言。
- `MatchState` 已增加 P2 权威字段：`turnPlayerId`、`phase`、`timingState`、`runePools`、`playerZones`、`playerScores`、`cardObjects`、`priorityPlayerId`、`passedPriorityPlayerIds`、`stackItems`、`focusPlayerId`、`passedFocusPlayerIds`；snapshot 已投影 timing、公开结算链和焦点字段，玩家 `handSize` 和 `score` 来自权威状态，后续扩展对象状态、战场控制和卡牌效果时必须继续维护 `state_snapshots.payload`。
- `CoreRuleEngine` 已接入 API DI，并保留 `PlaceholderRuleEngine` 作为 legacy fallback；当前已能通过 `p2-preflight-turn-start.fixture.json` 验证普通回合开始：召出 2 张符文、抽 1 张牌、清空符文池、进入主阶段；也已通过 `p2-preflight-turn-start-short-rune-deck.fixture.json` 验证符文牌堆不足 2 张时有多少召出多少，通过 `p2-preflight-turn-start-first-p2-extra-rune.fixture.json` 验证 1v1 第二个行动玩家首个召出阶段额外召出 1 张符文，并通过 `p2-preflight-turn-start-burnout.fixture.json` 验证抽牌阶段主牌堆为空时燃尽、对手得 1 分、废牌堆回收后抽牌；`END_TURN` fixture 已覆盖回合推进、特殊清理、伤害移除、本回合内效果失效和清理重复；`PASS_PRIORITY` / FEPR fixture 已覆盖误提交拒绝、优先权让过、双方让过后结算最新项目、结算链未空时新的最新项目控制者获得优先权；`p2-preflight-spell-duel-pass-focus-closes-window.fixture.json` 已验证法术对决中焦点让过、焦点转移、双方让过后关闭法术对决并回到普通主阶段。
- API 在 `http://127.0.0.1:5088` 启动成功。
- `/health` 返回 ok。
- `/catalog/summary` 返回 1009 官方条目、811 功能逻辑单元。
- PostgreSQL 已创建 P1 表：`matches`、`command_log`、`game_events`、`state_snapshots`、`snapshots`、`action_prompts`、`official_cards`、`functional_units`、`oracle_fixtures`。

## 5. 立即开发顺序

当前阶段是从 P1 联机底座过渡到 P2 核心规则 preflight。可以继续做 fixture schema、权威 `MatchState`、符文池和回合开始/结束流程；仍不要进入大规模全卡牌迁移、最终 UI 或复杂 AI。

第一批任务：

1. 继续按 `docs/p2-rules-preflight.md` 扩展 P2 preflight：下一步补废牌堆也为空导致连续燃尽/胜利判定 fixture。
2. 接着准备真实卡牌能力进入 FEPR 栈项目的最小 handler 边界。
3. P1 协议错误码保持稳定；P2 再加入费用不足、目标非法、阶段不允许等规则错误码。
4. 协议版本治理剩余项：TypeScript DTO 生成、客户端兼容策略、SignalR 方法版本和事件 upcaster；不要在没有前端接入点前过度设计。
5. 后续新增 fixture 必须使用 `PASS_PRIORITY` / `PASS_FOCUS` / `END_TURN`，裸 `PASS` 只保留在 Java legacy oracle 和兼容测试中。
6. 后续扩展 `MatchState` 时，同步扩展 `state_snapshots` 的权威 payload，不把隐藏信息或完整规则状态塞进玩家视角 snapshot。

P1 验收前不要开始：

- 最终产品级 UI。
- 全卡牌迁移。
- 复杂 AI。
- 移动端适配。
- 多实例房间热迁移。

## 6. 阶段验收门禁

任何功能要进入“完成”状态，必须满足：

- PDF/FAQ 规则点已记录。
- 官网卡面文本已记录。
- 行为规格已结构化。
- C# 实现已完成。
- 单元测试通过。
- MatchSession/SignalR 测试通过。
- PDF/FAQ 裁决通过；Java legacy oracle conformance 通过或差异已记录。
- 前端 smoke 或等价 E2E 通过。
- 文档和状态矩阵已更新。

未通过 conformance 的能力不能进入正式可玩卡池。

## 7. UI 和浏览器测试原则

UI 分两段：

- P2.5：先做简单但精美的开发期测试 UI，用于人工测试、fixture 复现和 Browser Use smoke。
- P7：规则和卡池稳定后，再做产品级 Web 对战 UI。

开发期 UI 必须保持服务端权威：

- 所有按钮来自 `ActionPrompt`。
- UI 不自行判断规则合法性。
- UI 展示真实 `Snapshot`、`Events`、`Prompt`。

P2.5 后，每个高风险规则能力都要用 Codex 内置浏览器做真实 P1/P2 smoke，并记录本地 URL、roomId、操作路径、事件和最终 snapshot 摘要。

## 8. 防偏离检查

每次开始新任务前，先确认：

- 这是否服务于“服务端权威双人 Web 卡牌游戏”？
- 是否符合五份 PDF、FAQ 和官网卡牌优先级？
- 是否把 Java 当作旧实现对照，而不是最终规则裁判？
- 是否能产出 command log、events、snapshots？
- 是否有明确 conformance 或 Browser Use smoke 验收？
- 是否避免提前做最终 UI、全卡牌、复杂 AI 或移动端？

答案不清楚时，先补文档或测试，不直接扩张实现。

## 9. 推荐给新窗口的开场指令

可以直接给新窗口这段话：

```text
继续 /Users/dinghaolin/MyProjects/riftbound-dotnet 的《符文战场》新项目。
先读取 README.md、docs/START_HERE.md、docs/master-development-plan.md、docs/phase-1.md、docs/rules-authority-and-audit.md、docs/development-audit-status.md、docs/rules-evidence-index.md、docs/rules-card-baseline.md、docs/p2-rules-preflight.md。
目标不变：.NET 10 + ASP.NET Core + SignalR 服务端权威双人 Web 卡牌游戏。五份官方 PDF、FAQ 与官网卡牌快照是最终规则权威，旧 Java 项目只作为历史行为参考和 fixture 导出工具。
当前阶段从 P1 联机底座过渡到 P2 preflight：按 docs/p2-rules-preflight.md 继续 fixture schema、权威 MatchState、符文池和回合开始/结束流程；不要跳到全卡牌迁移。
不要重做最终 UI，不要全量迁移卡牌，不要提交规则 PDF/FAQ，不要回退现有改动。
```
