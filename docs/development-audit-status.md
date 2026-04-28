# 当前开发审查状态

更新时间：2026-04-28

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
| `Riftbound.Contracts` 协议 DTO | 可表达基础 message、command、event、snapshot，已区分 `PASS_PRIORITY`、`PASS_FOCUS`、`END_TURN`，并有 `ErrorDto` / `PlayerSessionDto` | 需要后续扩展 | P1 加 `protocolVersion`、TypeScript DTO 生成，并扩大稳定错误码覆盖面。 |
| `GameHub` | 支持 Join、Reconnect、RequestSnapshot、Pass、EndTurn、SubmitIntent、snapshot/prompt/events 推送；加入/重连/快照/提交错误有最小 SignalR 级测试 | 需要后续扩展 | 保留；补 Ready、生产级 reconnect token 持久化和 P2 规则错误码。 |
| `MatchSession` | 支持串行、幂等、journal、占位状态、P1/P2 座位分配、snapshot seat、内存 reconnect token；提交命令要求玩家已 JoinRoom | 需要规则审查 | 串行、幂等、座位、内存重连可保留；规则状态和 prompt 只是占位。 |
| `PlaceholderRuleEngine` | 对齐旧 Java 的 `PASS`、`END_TURN`、重复 `PASS` 事件形状 | 需要重审 | 标记 `NEEDS_RULE_AUDIT`；补 PDF/FAQ evidence 后决定是否改行为。 |
| `PostgresMatchJournal`、`PostgresMatchRecoveryStore` 和 P1 SQL | 能记录命令、事件、snapshot、prompt，并写入 ruleset/FAQ/audit 元数据、event sequence 边界和客户端原始 command payload；已能读取恢复帧并校验 sequence 连续性 | 需要后续扩展 | 保留；后续补 RoomRegistry/MatchSession 恢复入口、权威 full-state snapshot 和持久化重连 token。 |
| `Riftbound.CardCatalog` | 能加载 1009 官方条目并生成 811 功能单元 | 需要 FAQ 标注 | 保留；后续增加 FAQ 涉及卡牌/关键词标记。 |
| `ConformanceFixture` | 能回放 command log 并比较 event/prompt | 已补规则审查字段 | 新增 `rulesEvidence`、`faqVersion`、`auditStatus` 读取能力；旧 fixture 默认需要重审。 |
| 3 条 Java fixture | `PASS`、`END_TURN`、重复 `PASS` 已与 C# 占位规则对齐，evidence 已细化 | 必须重审 | 保留为 legacy oracle；`PASS -> TURN_ENDED` 标记为 legacy mismatch candidate。 |
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

1. 接入 RoomRegistry/MatchSession 恢复入口；在权威 full-state snapshot 完成前，只把 player snapshot 作为重连视图和恢复校验依据。
2. 继续协议错误码治理；P1 提交路径已覆盖未知玩家、unsupported command、重复 intent 冲突，P2 需要加入费用不足、目标非法、阶段不允许等规则错误码。
3. 新增 fixture 使用 `PASS_PRIORITY` / `PASS_FOCUS` / `END_TURN`，裸 `PASS` 仅保留在 legacy oracle。

## 5. 重审验收

一个已开发能力从 `NEEDS_RULE_AUDIT` 恢复为可继续开发状态，至少需要：

- 覆盖五份 PDF/FAQ 中相关章节或 FAQ 问题。
- `rulesEvidence` 记录到 fixture 或状态矩阵。
- 如果 Java legacy oracle 与 PDF/FAQ 冲突，记录差异并以 PDF/FAQ 更新 `expected`。
- `dotnet test Riftbound.slnx --no-build` 通过。
- 文档中的当前状态同步更新。
