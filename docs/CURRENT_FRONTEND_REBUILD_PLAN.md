# 符文战场 Web 前端重建与服务端补齐计划

更新日期：2026-05-06
当前结论：**NOT READY**
用途：作为本轮“产品级 Web 前端重建 + 服务端规则补齐”的短入口，后续每个批次都应回到本文更新范围、验收和剩余风险。

## 1. 已读取并确认的资料

本批次已先读取并确认以下资料：

- `docs/符文战场_前端Web开发需求文档_给Codex.md`：2485 行。前端目标是产品级、中文、1v1 双人联机、服务端权威、围绕 `Snapshot` / `ActionPrompt` / `Events` / 错误运行；禁止前端自行裁决规则或隐藏服务端缺口。
- `docs/符文战场_服务端核心规则自查文档.md`：1452 行。服务端 READY 门槛是无 P0、完整官方 1v1 开局/回合/FEPR/法术对决/战斗/得分/胜负/隐藏信息/FAQ/关键词/单卡证据。
- `docs/CURRENT_SERVER_RULE_AUDIT.md`：当前明确 **NOT READY**，剩余阻断集中在完整 battlefield/standby/control task 状态机、central cleanup task queue、spell duel/battle lifecycle、PaymentEngine、LayerEngine 和全官方卡牌证据。
- `docs/START_HERE.md` 与 `README.md`：仍保留 P7/P7.9 已完成的旧交接口径；本轮以 `CURRENT_SERVER_RULE_AUDIT.md`、新的前端需求文档和本文为准。
- 根目录五个官方 PDF/FAQ，已用 `pdftotext` 抽取到临时文本并核对页数：
  - `《符文战场》核心规则_260330.pdf`：105 页，核心规则最后更新时间 2026-03-30。
  - `裁判FAQ_251023.pdf`：10 页，裁判 FAQ 最后更新时间 2025-10-23。
  - `铸魂淬炼系列_官方FAQ_260114.pdf`：21 页，官方 FAQ，含勘误和规则阐明。
  - `铸魂淬炼系列_裁判FAQ.pdf`：25 页，更新日期 2026-01-14。
  - `《符文战场》破限系列_裁判FAQ_260416.pdf`：11 页，破限系列裁判 FAQ，更新日期 2026-04-16。
- `src/Riftbound.Api`：ASP.NET Core + SignalR；REST 目前包括 `/health`、`/catalog/summary`、`/catalog/p3-status`、`/catalog/behavior-specs`、`/catalog/keyword-coverage`，实时入口为 `/hubs/game`。
- `src/Riftbound.Engine/MatchSession.cs`：当前已有 `MatchState`、视角化 snapshot、prompt builder、官方 deck submit/mulligan、对象位置、battlefield/task/pending queue 视图，以及 `serialGate` 串行命令处理。
- `src/Riftbound.Engine/CoreRuleEngine.cs`：当前已有代表性规则执行、typed power 部分支付、移动/战斗/栈结算局部 cleanup、spell duel focus 修复；仍缺统一 PaymentEngine、LayerEngine 和完整任务状态机。
- `tests/Riftbound.ConformanceTests`：当前包含 1210 个 fixture 文件，核心测试入口包括 `ConformanceFixtureRunnerTests`、`ConformanceFixtureShapeTests`、`GameHubJoinTests`、`OfficialOpeningTests`、`CardCatalogBaselineTests`、`MatchRecoveryTests`。

## 2. 当前前端现状

现有前端位于 `src/Riftbound.DevUi`，技术栈为 React 19 + TypeScript + Vite + SignalR。新的前端需求文档没有要求更换技术栈，且仓库已有可用 Vite/React 构建，因此本轮沿用该栈；新增 `lucide-react` 作为轻量图标库，不引入重 UI 框架或 Next.js。

现状问题：

- 代码集中在 `src/Riftbound.DevUi/src/main.tsx`（7083 行）和 `src/Riftbound.DevUi/src/styles.css`（4570 行），不满足长期维护、组件拆分、adapter/store 分层要求。
- 当前 UI 仍混有开发期操作台、调试面板、产品桌面和 JSON intent 工作台，不符合“清理旧 UI，重建产品级卡牌对战桌面”的执行要求。
- 已经接入真实 `GameHub`，但前端内部类型、页面结构和路由仍不够清晰；后续必须拆成 `services`、`types`、`stores`、`components`、`pages` 和 `features`。
- 现有页面已有动作候选、卡牌快捷操作、图鉴和双人测试能力，可作为协议理解参考，但不能作为最终架构继续堆叠。

## 3. 当前服务端可供前端消费的能力

已确认可用：

- SignalR 方法：`JoinRoom(roomId, playerId, reconnectToken?)`、`Reconnect(roomId, playerId, reconnectToken)`、`RequestSnapshot(roomId, playerId)`、`Ready(roomId, playerId, clientIntentId)`、`SubmitIntent(roomId, playerId, clientIntentId, cmd)`。
- Development-only：`SeedScenario(roomId, playerId, scenarioId, clientIntentId)`，只能用于本地 smoke 和开发场景，不得伪装成生产对局能力。
- 服务端消息：`Joined`、`Snapshot`、`Prompt`、`Events`、`Error`。
- 命令 DTO：`SUBMIT_DECK`、`MULLIGAN`、`PASS_PRIORITY`、`PASS_FOCUS`、`PASS`、`END_TURN`、`PLAY_CARD`、`ACTIVATE_ABILITY`、`LEGEND_ACT`、`HIDE_CARD`、`REVEAL_CARD`、`MOVE_UNIT`、`ASSEMBLE_EQUIPMENT`、`DECLARE_BATTLE`。
- Snapshot 已提供 `players`、`lanes`、`stack`、`timing`、`turnState`，其中 `timing` 已包含 `turnWindow`、`spellDuel`、`battle`、`battlefieldTasks`、`pendingTaskQueue`、`continuousEffects` 等服务端权威视图。
- Prompt 已提供 `actions` 与结构化 `candidates`，包含 `sources`、`targets`、`destinations`、`modes`、`optionalCosts`、`metadata`。
- 图鉴与卡牌状态可从 `/catalog/behavior-specs` 和 `/catalog/keyword-coverage` 获得；当前只能展示 `representative-rule-pass`，不能宣称 full official rule pass。

## 4. 服务端缺口处理原则

本轮原则不是永久降级前端能力，而是：

1. 前端发现服务端未提供 snapshot 字段、ActionPrompt、候选、命令、事件或状态视图时，先记录为服务端缺口。
2. 对 P0/P1 或对产品级对战必需的缺口，必须按五个官方规则/FAQ PDF 补齐服务端实现、测试和文档。
3. 在服务端补齐并有测试前，前端不得自行裁决或假装可玩；只能暂时禁用入口、过滤候选或明确提示“等待服务端规则能力”。
4. 服务端补齐后，前端再接入对应 prompt/snapshot/event，而不是保留 mock 或客户端规则判断。

当前已知服务端缺口：

- 完整 battlefield/standby/control task 状态机未完成：前端只能展示服务端 `battlefieldTasks` / `pendingTaskQueue`，不能自行推进战场控制、待命移除、征服/据守或争夺结论。
- Central cleanup task queue 未完成：前端只能展示清理结果和阻塞 `WAIT` prompt，不能本地继续开放普通行动。
- Spell duel/battle lifecycle 未完整官方化：前端可以显示 `spellDuel`、`battle`、`PASS_FOCUS`、`DECLARE_BATTLE` 等候选，但不能用客户端 UI 计算“法术对决结束”“战斗伤害结算”“控制权改变”。
- PaymentEngine 未统一：前端只能提交服务端候选中暴露的 `optionalCosts` / 支付 token；未暴露的费用分支不得做成可选项。
- LayerEngine 未完整：前端展示 `basePower` / `effectivePower` / `continuousEffects`，不得从卡面和装备自行重算战力或关键词。
- 全官方卡牌证据仍不足：图鉴必须明确展示 `representative-rule-pass` / deferred family 状态，不能显示“官方完整通过”。

## 5. 前端重建批次

### Batch 1：需求与现状审计

状态：完成。

交付：

- 确认需求文档、自查文档、五个 PDF、服务端入口、关键引擎和测试入口。
- 新增本文，明确当前 NOT READY、真实接口、前端清理范围和服务端缺口。
- 提交后继续进入前端清理与新架构。

验收：

- `git diff --check`
- 提交后 `git status --short` 只剩 `?? riftbound-dotnet.sln`

### Batch 2：前端清理与新架构

状态：完成。

交付：

- 删除旧 `main.tsx` 巨型 UI 和旧 CSS 组织方式，保留 Vite/React/SignalR 构建配置与 package lock。
- 建立全新目录：
  - `src/app`
  - `src/components`
  - `src/pages`
  - `src/services`
  - `src/stores`
  - `src/types`
  - `src/utils`
  - `src/styles`
- 建立真实 REST/SignalR adapter：`ApiClient`、`MatchSocket`、catalog store、settings store、match controller。
- 建立中文产品级页面壳：首页、大厅、房间、对战桌面、图鉴、卡组、设置。
- 首批桌面只渲染服务端 snapshot / prompt / candidate / event，不保留旧 JSON 调试台和客户端规则裁决入口。

验收：

- `source ../../scripts/dev-env.sh && npm run build`：通过。
- Browser Use smoke：打开 `http://127.0.0.1:5173/`、`/cards`、`/lobby`、创建房间、入座、提交测试卡组、ready、进入 `/matches/{roomId}` 并重连同步 snapshot；控制台无 error/warn。
- Smoke 发现并修复对战桌面横向溢出，右侧行动面板已在 1248px 浏览器宽度内完整可见。
- Smoke 发现并修复首次随机玩家名不持久化导致刷新后重连身份变化的问题。

### Batch 3：服务端能力矩阵与正式房间闭环

状态：完成。

交付：

- 前端房间页与对战行动面板改为只渲染 `Prompt.candidates` 中服务端支持的游戏命令；连接/重连和页面跳转仍作为非游戏命令常驻。
- 补齐服务端房间阶段 prompt：未提交合法卡组时只给 `SUBMIT_DECK`，提交后才给 `READY`，已准备后给 `WAIT`。
- 以 `SUBMIT_DECK -> READY -> MULLIGAN -> MAIN` 作为正式 Web smoke 入口；Development legacy ready 不再出现在新前端主流程。
- 若服务端缺 deck REST/保存接口，前端先做本地 deck import/select，但 ready 前必须走 `SUBMIT_DECK`。
- 把前端发现的 P0/P1 必需服务端缺口追加到 `CURRENT_SERVER_RULE_AUDIT.md`，并补服务端测试。

验收：

- `source ../../scripts/dev-env.sh && npm run build`：通过。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`：通过 85/85。
- Browser Use smoke：P1 创建房间并入座，提交卡组前只显示 `提交卡组` 候选且不显示 `准备`；提交卡组后只显示 `准备`；P2 通过房间码加入、提交卡组、ready；双方进入 `MULLIGAN`，依次执行起手调度后进入 `MAIN`；对战桌面展示事件日志、双方公开区、对手隐藏手牌卡背和最终 snapshot。
- Smoke 发现并修复正式开局后玩家面板内容溢出覆盖中央战场的问题；玩家区/战场区现在使用面板内安全滚动，避免 UI 重叠。

### Batch 4：对战桌面与卡牌详情

目标：

- 顶部状态栏、双方区域、两处战场、结算链、prompt/action 面板、事件日志、卡牌详情弹窗。
- 所有卡牌展示名称、编号、费用、符能费用、战力、类型、关键词、效果、状态、控制方/所属方。
- 对手手牌、对手牌堆、对手面朝下待命牌严格不渲染私密字段。

验收：

- Browser smoke：起手调整、进入主阶段、查看双方公开区、确认隐藏信息不出现在 DOM 展示层。

### Batch 5：卡牌驱动操作

目标：

- 点击卡牌弹出服务端候选动作菜单。
- 目标、模式、费用、目的地、确认/取消全部来自 `ActionPrompt.candidates`。
- 支持 `PLAY_CARD`、`MOVE_UNIT`、`ASSEMBLE_EQUIPMENT`、`LEGEND_ACT`、`ACTIVATE_ABILITY`、`PASS_PRIORITY`、`PASS_FOCUS`、`END_TURN`、`MULLIGAN`。

验收：

- Browser smoke：出牌、让过、结束回合、移动或声明战斗中至少一条真实服务端路径。

### Batch 6+：服务端 P0/P1 补齐

优先顺序：

1. 完整 battlefield/standby/control task 状态机。
2. Central cleanup task queue。
3. 由 task queue 驱动的 spell duel/battle lifecycle。
4. 全路径 PaymentEngine。
5. LayerEngine。
6. 全官方卡牌证据和图鉴状态收敛。

每个服务端批次必须先补测试，再补实现，最后更新 `CURRENT_SERVER_RULE_AUDIT.md` 和本文。

## 6. 当前总体进度

估算整体进度：**30%**

已经完成：

- 必读资料和五个 PDF 已确认。
- 真实前端栈、服务端接口、关键状态视图和测试入口已确认。
- 当前 NOT READY 根因已落到本文的前端硬约束和后续批次。
- 旧 Dev UI 已清理，新的 React/Vite 前端架构、中文页面壳、REST/SignalR adapter 和基础对战桌面已落地。
- 房间/连接/提交卡组/ready/起手调度到主阶段的正式双人 Web 闭环已由服务端 prompt 驱动，不再由前端常驻按钮绕过。

预计剩余批次数：**至少 8 批**

原因：

- 前端需要从双巨型文件重建为产品级架构。
- Browser smoke 要覆盖房间、双人 ready、mulligan、出牌/Pass/结束回合/移动或战斗、重连。
- 服务端仍有多个架构级 P0/P1 规则缺口，不是单个 UI 批次可以关闭。

## 7. 工作区与提交规则

- 不提交五个 PDF/FAQ。
- 不提交未跟踪 `riftbound-dotnet.sln`。
- 不回退用户或历史已完成改动。
- 运行 dotnet 前必须使用：`source scripts/dev-env.sh && ...`
- 每个显著前端批次都做 Browser Use 或 Computer Use smoke 并在本文或对应状态文档记录。
- 每批提交后 `git status --short` 只能剩 `?? riftbound-dotnet.sln`。
