# 新窗口接手指南

更新时间：2026-05-08

这份文档用于在新 Codex 窗口中快速恢复上下文，确保后续开发不偏离《符文战场》的最终目标。

> 2026-05-06 复审提示：本文件早期段落保留 P7/P7.9 已完成的历史口径；当前最新结论以 `docs/CURRENT_SERVER_RULE_AUDIT.md`、新的前端需求文档和 `docs/CURRENT_FRONTEND_REBUILD_PLAN.md` 为准。当前总体状态是 **NOT READY**，本轮工作目标是清理旧前端、重建产品级 Web 前端，并继续补齐服务端 P0/P1 规则缺口。

## 1. 一句话目标

构建一款精美、稳定、可双人联机对战、服务端权威结算、可断线重连、可回放、可长期生产化维护的 Web 卡牌游戏。

关键原则：

- 玩家只提交行为意图。
- 服务端根据五份官方 PDF、官网卡面和当前状态自动裁决并结算。
- 前端只渲染服务端下发的行动提示、事件、玩家视角快照和错误信息。
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

1. `docs/CURRENT_FRONTEND_REBUILD_PLAN.md`
2. `docs/CURRENT_SERVER_RULE_AUDIT.md`
3. `docs/任务补充.md`
4. `README.md`
5. `docs/START_HERE.md`
6. `docs/CURRENT_P7_9_STATUS.md`
7. `docs/CURRENT_P7_STATUS.md`
8. `docs/rules-evidence-index.md` 中目标卡牌对应行
9. `docs/conformance-fixture-format.md` 中 fixture schema 规则

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

最新复审状态：**NOT READY**。`docs/CURRENT_SERVER_RULE_AUDIT.md` 已确认当前仍缺完整 battlefield/standby/control task 状态机、central cleanup task queue、spell duel/battle lifecycle、PaymentEngine、LayerEngine 和全官方卡牌证据。产品前端必须只展示服务端快照、服务端行动提示和合法候选支持的能力；前端发现 P0/P1 必需服务端能力缺口时，应按五个官方规则/FAQ PDF 补齐服务端实现、测试和文档。在补齐前，前端不得自行裁决或假装可玩。

新项目已建立完整的 P1-P7 内部可玩链路：`Riftbound.Contracts`、`Riftbound.Engine`、`Riftbound.Api`、`Riftbound.Persistence`、`Riftbound.CardCatalog`、`Riftbound.ConformanceTests` 和 `Riftbound.DevUi` 均已可用。本机环境为 .NET 10.0.203、PostgreSQL 16、Redis 6.2.4、Node 24，新的终端先执行 `source scripts/dev-env.sh`。

P7/P7.9 产品级 Web 对战与本地全卡可玩文档保留历史阶段完成记录，最终状态分别见 `docs/CURRENT_P7_STATUS.md` 与 `docs/CURRENT_P7_9_STATUS.md`。但当前 2026-05-08 复审已经把整体结论重新定为 **NOT READY**，当前验收以 `docs/CURRENT_FRONTEND_REBUILD_PLAN.md`、`docs/CURRENT_SERVER_RULE_AUDIT.md` 和用户补充任务为准；历史 `CONFORMANCE_PASS` 口径不得替代本轮完整官方规则复审。

完整卡牌/模式覆盖列表不再重复维护在本文件中：

- 每个 fixture 的规则证据见 `docs/rules-evidence-index.md`。
- P2 规则进度、事件词表和 fixture 表见 `docs/p2-rules-preflight.md`。
- 协议命令语义见 `docs/protocol-semantics.md`。

当前工作区预期只剩未跟踪的 `riftbound-dotnet.sln`，不要提交它，除非用户明确要求。

## 5. 立即开发顺序

当前阶段是产品级 Web 前端重建与服务端 P0/P1 规则缺口收口。除非用户明确要求生产化，不进入 P8 账号/匹配/部署/监控/风控。

立即顺序：

1. 以 `docs/CURRENT_FRONTEND_REBUILD_PLAN.md` 作为当前短交接入口，保留前端重建计划、服务端缺口和 Browser smoke 记录。
2. 清理旧 `Riftbound.DevUi` 巨型 UI，重新搭建可维护的 React + TypeScript + Vite 产品级前端架构。
3. 任何可玩能力必须来自服务端行动提示、权威快照和事件；P6 deferred/manual 或服务端未完整支持的能力必须先补服务端能力和测试，在补齐前前端不得自行裁决或假装可玩。
4. 持续修复 `docs/CURRENT_SERVER_RULE_AUDIT.md` 中 NOT READY 的 P0/P1 缺口，以及前端重建过程中发现的新增服务端缺口，直到复审无 P0/P1。
5. P8 只能在用户明确要求时启动；启动前重新审计账号、匹配、部署、监控、风控、持久化回放/观战等生产边界。
6. 每次修改后保持后端测试、前端 build、Browser smoke 和 clean status 绿色。

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

当前 Web UI 已进入产品级重建收口阶段，不再按早期 P2.5/P7 双阶段推进。历史 P2.5/P7 文档只作为阶段记录；当前验收以 `docs/CURRENT_FRONTEND_REBUILD_PLAN.md`、`docs/CURRENT_SERVER_RULE_AUDIT.md`、`docs/CURRENT_COMPLETION_AUDIT.md` 和 `docs/任务补充.md` 为准。

Web UI 必须保持服务端权威：

- 所有可提交按钮来自服务端行动提示、合法候选和 authoritative snapshot。
- UI 不自行判断规则合法性。
- UI 展示真实服务端快照、事件和行动提示。

每个显著前端批次都要优先用 Browser/Chrome 插件做真实 P1/P2 smoke；如果插件不能覆盖本地页面，使用后台 headless Chrome/CDP/Playwright，不要用 Computer Use 抢用户前台窗口。记录本地 URL、roomId、操作路径、事件、最终 snapshot 摘要和进程清理结果。

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
先读取 docs/CURRENT_FRONTEND_REBUILD_PLAN.md、docs/CURRENT_SERVER_RULE_AUDIT.md、docs/任务补充.md、README.md 和 docs/START_HERE.md；需要迁移具体卡牌或关键词时，再读取 docs/rules-evidence-index.md 的目标行，以及 docs/conformance-fixture-format.md 的 schema 规则。
目标不变：.NET 10 + ASP.NET Core + SignalR 服务端权威双人 Web 卡牌游戏。五份官方 PDF、FAQ 与官网卡牌快照是最终规则权威，旧 Java 项目只作为历史行为参考和 fixture 导出工具。
当前最新复审结论是 NOT READY，P7/P7.9 文档只作为历史阶段基线；本轮目标是完成产品级 Web 前端重建与服务端 P0/P1 规则缺口收口。前端必须只展示和提交服务端行动提示、权威快照、事件与合法候选支持的能力，页面文字保持中文展示。除非用户明确要求，不要进入 P8 账号/匹配/部署/监控/风控；不要提交规则 PDF/FAQ，不要提交未跟踪的 riftbound-dotnet.sln，不要回退现有改动。
```
