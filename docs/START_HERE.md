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
10. `docs/conformance-fixture-format.md`
10. 如需迁移背景，再读旧项目的 `docs/dotnet-migration-plan.md`

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
- `dotnet test Riftbound.slnx --no-build` 通过，当前 10 个测试。
- Java oracle exporter 已导出 `java-oracle-p1-pass.fixture.json`、`java-oracle-p1-end-turn.fixture.json`、`java-oracle-p1-duplicate-pass.fixture.json`。
- C# 测试已能读取 Java fixture 元数据，并对齐 `PASS`、`END_TURN`、重复 `PASS` 的首批事件日志 fixture；这些 fixture 现在默认 `RequiresRuleAudit`。
- `ConformanceFixture` 已能读取可选的 `rulesEvidence`、`faqVersion`、`auditStatus`、`seed` 字段。
- `docs/rules-evidence-index.md` 已建立五份 PDF/FAQ 的规则域索引和当前 fixture 审查映射。
- Java exporter 已输出 `legacyOracle`，并暂时保留旧 `oracle` 兼容字段。
- P1 PostgreSQL schema 和 `PostgresMatchJournal` 已补 `ruleset_version`、`faq_version`、`rules_audit_status`、`rules_evidence`；schema initializer 会按文件名顺序执行 `Sql/*.sql`。
- 当前 3 条 Java legacy fixture 的 evidence 已细化到具体页码、规则号/FAQ 问题号，并记录 `PASS -> TURN_ENDED` 是 legacy mismatch candidate。
- `docs/protocol-semantics.md` 已明确 `PASS_PRIORITY`、`PASS_FOCUS`、`END_TURN` 的协议边界；`PASS` 仅保留为 legacy 兼容。
- API 在 `http://127.0.0.1:5088` 启动成功。
- `/health` 返回 ok。
- `/catalog/summary` 返回 1009 官方条目、811 功能逻辑单元。
- PostgreSQL 已创建 P1 表：`matches`、`command_log`、`game_events`、`snapshots`、`action_prompts`、`official_cards`、`functional_units`、`oracle_fixtures`。

## 5. 立即开发顺序

当前阶段只推进 P1，不进入大规模规则重构和最终 UI。

第一批任务：

1. 迁移 P1/P2 加入、座位状态、玩家视角 snapshot，并按五份 PDF 审核。
2. 再扩展 P1 的重连 token、错误码和 command log 原始 payload。
3. 完善 event sequence / recovery 字段，支撑后续断线重连与事件重放。
4. 开始把新 fixture 从裸 `PASS` 迁移到 `PASS_PRIORITY` / `PASS_FOCUS` / `END_TURN`。

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
先读取 README.md、docs/START_HERE.md、docs/master-development-plan.md、docs/phase-1.md、docs/rules-authority-and-audit.md、docs/development-audit-status.md、docs/rules-evidence-index.md、docs/rules-card-baseline.md。
目标不变：.NET 10 + ASP.NET Core + SignalR 服务端权威双人 Web 卡牌游戏。五份官方 PDF、FAQ 与官网卡牌快照是最终规则权威，旧 Java 项目只作为历史行为参考和 fixture 导出工具。
当前阶段只推进 P1：先完成五份 PDF 的规则索引和现有 fixture 重审，再继续扩展联机底座、Persistence/CardCatalog、conformance runner。
不要重做最终 UI，不要全量迁移卡牌，不要提交规则 PDF/FAQ，不要回退现有改动。
```
