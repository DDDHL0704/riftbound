# 当前开发审查状态

更新时间：2026-04-30

## 1. 结论

当前已开发内容仍然可以作为 P1 工程骨架保留，不需要立刻推倒重写。但任何涉及规则裁决的内容，不能再以“已对齐 Java”为完成标准，必须经过五份 PDF/FAQ 重审。

当前总体判断：

- 工程底座：继续保留并迭代。
- 规则占位实现：保留为测试骨架，但标记为 `NEEDS_RULE_AUDIT`。
- Java fixture：保留为 legacy oracle 对照，但不再作为最终 expected 的来源。
- 开发计划：需要调整为“先规则索引和审查状态，再扩展更多规则”。

## 2. 已开发内容状态

| 范围 | 当前状态 | 是否需要修改 | 处理决定 |
|---|---|---|---|
| `.NET 10` solution、项目分层、`scripts/dev-env.sh` | 工程骨架已可 build/test | 暂不需要 | 保留。 |
| `Riftbound.Contracts` 协议 DTO | 可表达基础 message、command、event、snapshot，已区分 `READY`、`PLAY_CARD`、`PASS_PRIORITY`、`PASS_FOCUS`、`END_TURN`，并有稳定 `ErrorDto` / `PlayerSessionDto`；client/server envelope 已带默认版本字段；P2 规则拒绝已覆盖阶段、费用、目标、手牌和不支持行为等错误码 | 需要后续扩展 | P1 后续补 TypeScript DTO 生成、客户端兼容策略、SignalR 方法版本、事件 upcaster，并扩大稳定错误码覆盖面。 |
| `GameHub` | 支持 Join、Reconnect、RequestSnapshot、Ready、Pass、EndTurn、SubmitIntent、snapshot/prompt/events 推送；加入/重连/Ready/快照/提交错误有最小 SignalR 级测试，空 `clientIntentId` 会稳定返回 `CLIENT_INTENT_ID_REQUIRED`；Join/Reconnect 走异步 token hash 持久化路径；API DI 已改用 `CoreRuleEngine` | 需要后续扩展 | 保留；后续补 P2 规则错误码。 |
| `MatchSession` / `InMemoryMatchSessionRegistry` | 支持串行、幂等、journal、P1/P2 座位、ready/lifecycle、reconnect token hash、重连轮换和基于权威 `MatchState` 的恢复；`MatchState` 已具备 P2 timing、区域、对象、结算链、焦点、赢家、随机游标和本回合摧毁记忆等基础字段 | 需要规则审查 | 保留工程底座；后续扩展规则状态时同步扩展权威 state snapshot，玩家视角 snapshot 只作视图和一致性校验。 |
| `PlaceholderRuleEngine` / `CoreRuleEngine` | `PlaceholderRuleEngine` 仅保留 legacy fallback；`CoreRuleEngine` 已实现 P2 回合、优先权、焦点、燃尽、清理和多类 `PLAY_CARD` preflight 路径。当前短状态见 `docs/CURRENT_P2_STATUS.md`，完整 fixture 覆盖和证据见 `docs/p2-rules-preflight.md` 与 `docs/rules-evidence-index.md`；最小 card behavior registry 当前为 `148/811`。 | 需要继续规则审查 | P2 新规则继续进入 `CoreRuleEngine`；不要在本表重复粘贴完整卡牌清单。 |
| `PostgresMatchJournal`、`PostgresMatchRecoveryStore`、`PostgresMatchPlayerStore` 和 P1 SQL | 能记录命令、lifecycle/game events、权威 state snapshot、玩家 snapshot、prompt、match_players token hash，并写入 ruleset/FAQ/audit 元数据、event sequence 边界和客户端原始 command payload；`matches.status` 使用权威 `MatchState.Status` 更新；已能读取恢复帧并校验 sequence 连续性和权威状态一致性 | 需要后续扩展 | 保留；后续随核心规则状态扩展 `state_snapshots.payload`。 |
| `Riftbound.CardCatalog` | 能加载 1009 官方条目并生成 811 功能单元 | 需要 FAQ 标注 | 保留；后续增加 FAQ 涉及卡牌/关键词标记。 |
| `ConformanceFixture` | 能回放 command log 并比较 event/prompt；已能读取 P2 schema v2 的 `initialState` 和 richer `expected`，并能把 `initialState` 应用为权威初始状态；`CompareExpected` 已开始通用比较 final tick、event kinds、event tick/sequence/payload 局部字段、prompt actions、最终 timing、符文池、分数、玩家区域、对象状态和结算链 | 已补规则审查字段 | 新增 `rulesEvidence`、`faqVersion`、`auditStatus` 读取能力；旧 fixture 默认需要重审；后续继续扩展 snapshots canonical diff。 |
| 3 条 Java fixture | `PASS`、`END_TURN`、重复 `PASS` 已与 C# 占位规则对齐，evidence 已细化 | 必须重审 | 保留为 legacy oracle；`PASS -> TURN_ENDED` 标记为 legacy mismatch candidate。 |
| P2 前置规则审查 | 已建立 `docs/p2-rules-preflight.md`，覆盖 P2 fixture schema、权威状态、事件词表、核心流程和当前卡牌/模式迁移进度；每个 fixture 的审计证据由 `docs/rules-evidence-index.md` 与 fixture 内 `rulesEvidence` 维护。当前短状态见 `docs/CURRENT_P2_STATUS.md`。 | 继续执行 | 下一步迁移更多低复杂度官方卡牌，并继续扩大通用 diff 覆盖面。 |
| 前端 UI | 尚未开始 | 暂不开始最终 UI | P2/P2.5 后做开发期测试 UI。 |

## 3. 计划需要调整的点

必须调整：

1. P0 不只是“规则 PDF 索引”，而是“五份 PDF/FAQ 索引 + FAQ 问题索引 + 规则冲突裁决表”。
2. P1 不应继续优先扩展 10 条玩法 fixture；应先补现有 3 条 fixture 的 `rulesEvidence`。
3. 完成状态必须新增 `NEEDS_RULE_AUDIT`，防止旧 Java conformance 被误当成规则完成。
4. 数据库和 fixture schema 已开始记录 `rulesetVersion` / `faqVersion` / audit 状态；后续事件回放必须持续带上这些字段。
5. Java exporter 的输出应逐步从 `oracle` 改成 `legacyOracle`；最终 `expected` 由 PDF/FAQ 裁决产生。

暂不需要调整：

- .NET 10 + ASP.NET Core + SignalR + PostgreSQL + Redis 的技术路线。
- “服务端权威、玩家只提交意图、事件日志、断线重连、长期生产化”的目标。
- UI 分两阶段：先开发期测试 UI，后产品级 UI。
- 卡牌批次按风险分层，不固定每批最多 6 个。

## 4. 下一步顺序

下一步不要直接继续扩展玩法实现。推荐顺序：

1. 继续 P2 preflight：迁移更多低复杂度官方卡牌，优先选择费用、单目标、伤害/本回合效果这类可复用路径。
2. 随卡牌迁移继续抽象对象控制者/所属者、隐藏信息和结算链事件 payload。
3. 继续把已有 P2 fixture 从手写断言迁移到 `CompareExpected`。
4. P1 提交路径已覆盖未知玩家、空 intent、未开局、unsupported command、重复 intent 冲突；P2 需要加入费用不足、目标非法、阶段不允许等规则错误码。
5. 新增 fixture 使用 `PASS_PRIORITY` / `PASS_FOCUS` / `END_TURN`，裸 `PASS` 仅保留在 legacy oracle。
6. 后续扩展核心规则时，同步扩展 `MatchState` 和 `state_snapshots.payload`，玩家视角 snapshot 只作为重连视图和一致性校验依据。

## 5. 重审验收

一个已开发能力从 `NEEDS_RULE_AUDIT` 恢复为可继续开发状态，至少需要：

- 覆盖五份 PDF/FAQ 中相关章节或 FAQ 问题。
- `rulesEvidence` 记录到 fixture 或状态矩阵。
- 如果 Java legacy oracle 与 PDF/FAQ 冲突，记录差异并以 PDF/FAQ 更新 `expected`。
- `dotnet test Riftbound.slnx --no-build` 通过。
- 文档中的当前状态同步更新。
