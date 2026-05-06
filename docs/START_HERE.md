# 新窗口接手指南

更新时间：2026-05-06

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

默认先读短交接，避免每次新窗口都加载完整历史长清单：

1. `docs/CURRENT_P7_9_STATUS.md`
2. `docs/CURRENT_P7_STATUS.md`
3. `docs/CURRENT_P6_STATUS.md`
4. `README.md`
5. `docs/START_HERE.md`
6. `docs/rules-evidence-index.md` 中目标卡牌对应行
7. `docs/conformance-fixture-format.md` 中 fixture schema 规则

按需再读：

- `docs/START_HERE.md`：项目边界和总体目标
- `docs/master-development-plan.md`：阶段计划
- `docs/phase-1.md`：P1/P2 过渡状态
- `docs/protocol-semantics.md`：命令和事件语义
- `docs/rules-card-baseline.md`：卡牌基线和官方目录说明
- `docs/rules-authority-and-audit.md`：规则权威和审计原则
- `docs/development-audit-status.md`：模块级审计状态

如果上下文不够，再回到旧 Java 项目阅读 `README.md`、`docs/current-development-status.md`、`docs/iteration-log-260330.md` 最新章节、`docs/multiplayer-auto-resolution-protocol.md` 和 `docs/card-behavior-regression-plan.md`。

## 4. 当前状态

新项目已建立完整的 P1-P7 内部可玩链路：`Riftbound.Contracts`、`Riftbound.Engine`、`Riftbound.Api`、`Riftbound.Persistence`、`Riftbound.CardCatalog`、`Riftbound.ConformanceTests` 和 `Riftbound.DevUi` 均已可用。本机环境为 .NET 10.0.203、PostgreSQL 16、Redis 6.2.4、Node 24，新的终端先执行 `source scripts/dev-env.sh`。

P7 产品级 Web 对战已完成，最终状态以 `docs/CURRENT_P7_STATUS.md` 为准。P7.9 本地产品版全卡可玩也已完成，最终状态入口为 `docs/CURRENT_P7_9_STATUS.md`。当前实现 `1009/1009` 官方条目 `CONFORMANCE_PASS`，`811/811` 功能单元已实现，manual deferred 剩余 `0/811`，blocked 剩余 `0`。P7.9 已完成结构化 ActionPrompt、点击式出牌/目标/费用/战斗/传奇/战场操作、全卡图鉴详情、事件日志/战报、基础回放/观战只读边界、断线重连 smoke、战斗完整性复核和最终 Browser smoke/full audit。2026-05-06 追加了新的默认产品级对战页：顶部 HUD、全高度竞技桌面、P2/P1 上下席位、双战场、右侧抽屉式操作/席位/日志/图鉴和中文化展示；同日追加开发期 `双人测试牌组` 场景，可在右侧 `操作` 抽屉的 `本地场景` 中给 P1/P2 各加载一套本地测试牌组。

完整卡牌/模式覆盖列表不再重复维护在本文件中：

- 每个 fixture 的规则证据见 `docs/rules-evidence-index.md`。
- P2 规则进度、事件词表和 fixture 表见 `docs/p2-rules-preflight.md`。
- 协议命令语义见 `docs/protocol-semantics.md`。

当前工作区预期只剩未跟踪的 `riftbound-dotnet.sln`，不要提交它，除非用户明确要求。

## 5. 立即开发顺序

当前完成阶段是 P7.9 本地产品版全卡可玩。除非用户明确要求生产化，不进入 P8 账号/匹配/部署/监控/风控。

立即顺序：

1. 以 `docs/CURRENT_P7_9_STATUS.md` 作为当前短交接入口，保留最终验证、Browser smoke 和进度记录。
2. 如果继续维护，只做本地产品版 alpha 范围内的 bugfix、回归测试、文档同步或小幅 polish；默认页面应保持产品级对战桌面，调试能力收进抽屉或折叠面板。
3. 任何新增可玩能力仍必须来自服务端 `ActionPrompt`、conformance/engine/GameHub 覆盖和 Browser smoke。
4. P8 只能在用户明确要求时启动；启动前重新审计账号、匹配、部署、监控、风控、持久化回放/观战等生产边界。
5. 每次修改后保持后端测试、前端 build、Browser smoke 和 clean status 绿色。

这些边界仍保持：不进入 P8 账号/匹配/部署/监控/风控；不做复杂 AI；不做移动端专项；不提交规则 PDF/FAQ；不提交未跟踪的 `riftbound-dotnet.sln`。

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
先读取 docs/CURRENT_P7_9_STATUS.md、docs/CURRENT_P7_STATUS.md、docs/CURRENT_P6_STATUS.md、README.md 和 docs/START_HERE.md；需要迁移具体卡牌或关键词时，再读取 docs/rules-evidence-index.md 的目标行，以及 docs/conformance-fixture-format.md 的 schema 规则。
目标不变：.NET 10 + ASP.NET Core + SignalR 服务端权威双人 Web 卡牌游戏。五份官方 PDF、FAQ 与官网卡牌快照是最终规则权威，旧 Java 项目只作为历史行为参考和 fixture 导出工具。
P7 产品级 Web 对战和 P7.9 本地产品版全卡可玩均已完成：1009/1009 官方条目 CONFORMANCE_PASS，811/811 功能单元实现，0/811 manual deferred；结构化 ActionPrompt、点击式 UI 操作、全卡图鉴详情、战斗完整性、事件日志/战报、基础回放/观战只读边界和最终 Browser smoke/full audit 已通过。当前默认 UI 是 2026-05-06 追加的新产品级对战页，调试/席位/日志/图鉴位于右侧抽屉，页面文字应保持中文展示。除非用户明确要求，不要进入 P8 账号/匹配/部署/监控/风控；不要提交规则 PDF/FAQ，不要提交未跟踪的 riftbound-dotnet.sln，不要回退现有改动。
```
