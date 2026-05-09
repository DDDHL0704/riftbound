# A 主控 Checkpoint

更新日期：2026-05-09
当前结论：**NOT READY**

本文是 A 主控架构 agent 的恢复入口。任何窗口中断或 Codex 关闭后，先读本文，再读 `README.md`、`docs/START_HERE.md`、`docs/符文战场_前端Web开发需求文档_给Codex.md`、`docs/符文战场_服务端核心规则自查文档.md`、`docs/CURRENT_SERVER_RULE_AUDIT.md`、`docs/CURRENT_FRONTEND_REBUILD_PLAN.md`、`docs/CURRENT_COMPLETION_AUDIT.md`，然后用 `git status --short --branch` 和 `git log --oneline -8` 对齐仓库事实。

## 0. A 主控职责边界

A 是主控规划 / 架构 / 验收 agent，不是默认功能实现 agent。A 的职责是：

- 管理 B/C/D/E 等子 agent 的任务分发、写入范围、验收标准和合并顺序。
- 维护本 checkpoint、当前审计文档、任务拆分、阻断清单和恢复入口。
- 审查子 agent 产出的 diff、测试结果和文档结论，决定是否进入下一轮。
- 遇到前端或服务端实现任务时，优先拆给对应子 agent；A 只做必要的只读审计、命令验证和小型文档维护。
- A 不得默认亲自继续实现功能代码；只有用户明确授权“A 可以亲自改这块”时，才允许修改功能实现。
- A 不得把项目标记为 READY 或完成 goal，除非按 active goal 完成审计证明所有服务端 P0/P1、前端 E2E、1009 张卡效果和文档验收均已收口。

如果 active goal 文本没有显式写出“A 主控职责”，以后恢复时仍以本节为准。

### 0.1 常驻子 agent 复用策略

A 不应为每个小问题反复创建全新子 agent。当前阶段采用“常驻分工池”：

- B：服务端规则 / 协议 / 测试实现与审查。
- C：前端契约 / Web UI / Chrome smoke。
- D：文档、规则证据、P0/P1 审计。
- E：卡牌效果覆盖、官方文本与 FAQ 证据矩阵。

复用原则：

- 同一阶段优先用 `send_input` 继续给已有子 agent 派任务，保留其已读上下文。
- 不主动关闭仍有后续价值的子 agent；只在阶段收口、职责变更或上下文明显污染时关闭。
- 如果窗口中断，先查看本 checkpoint 中记录的 agent id；可恢复时继续向同一 agent 发任务。
- 若底层环境无法恢复某个 agent，则用本 checkpoint、两份当前阶段文档和该 agent 最近总结作为最小 warm start，不再让它无目的重读全项目。

当前常驻审查池：

- B-Review / Bohr：`019e0bbc-d5d3-75a2-bde2-13e99da8ed76`
- C-Review / Copernicus：`019e0bbc-df6f-7151-baf5-f79ff466c5a9`
- D-Review / Pasteur：`019e0bbc-ece9-7fe1-a2ea-8e2afee1f5a2`

## 0.2 阶段 0 当前基线

阶段 0 只做主控建档、只读审计与任务拆分，不实现功能代码。已读取：

- `docs/A_MASTER_AGENT_GOAL.md`
- `docs/符文战场_前端Web开发需求文档_给Codex.md`
- `docs/符文战场_服务端核心规则自查文档.md`
- 五份本地规则 / FAQ PDF：`《符文战场》核心规则_260330.pdf`、`裁判FAQ_251023.pdf`、`铸魂淬炼系列_官方FAQ_260114.pdf`、`铸魂淬炼系列_裁判FAQ.pdf`、`《符文战场》破限系列_裁判FAQ_260416.pdf`

### 当前仓库结构

- `src/Riftbound.Api`：ASP.NET Core HTTP API 与 SignalR `GameHub`。
- `src/Riftbound.Contracts`：协议 DTO、命令、snapshot、prompt、事件与错误码。
- `src/Riftbound.Engine`：规则引擎、MatchSession、恢复、日志、卡牌/关键词/战场/传奇能力目录。
- `src/Riftbound.CardCatalog`：官网卡牌快照加载、schema 校验、行为规格与关键词覆盖报告。
- `src/Riftbound.Persistence`：PostgreSQL journal / recovery / player store。
- `src/Riftbound.DevUi`：React + TypeScript + Vite Web UI。
- `tests/Riftbound.ConformanceTests`：规则、Hub、恢复、fixture 与 conformance 测试。
- `data/official`：2026-04-27 官网卡牌快照，当前目标统计口径为 1009 条。
- `docs`：主控、审计、计划、证据和验收文档。

### 当前已修改文件

当前 `git status --short --branch` 显示：

- 新增暂存/未提交：`docs/A_MASTER_AGENT_GOAL.md`
- 已修改：`docs/CURRENT_SERVER_RULE_AUDIT.md`、`docs/rules-evidence-index.md`
- 已修改协议/服务端：`src/Riftbound.Contracts/Protocol.cs`、`src/Riftbound.Engine/CoreRuleEngine.cs`、`src/Riftbound.Engine/MatchSession.cs`
- 已修改前端：`src/Riftbound.DevUi/src/components/match/ActionPanel.tsx`、`src/Riftbound.DevUi/src/components/match/MatchTopBar.tsx`、`src/Riftbound.DevUi/src/pages/MatchPage.tsx`、`src/Riftbound.DevUi/src/stores/useMatchController.ts`、`src/Riftbound.DevUi/src/styles/globals.css`、`src/Riftbound.DevUi/src/types/protocol.ts`、`src/Riftbound.DevUi/src/utils/errors.ts`
- 已修改测试：`tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`、`tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs`、`tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-moonrise-enemy-battlefield-power-minus-2.fixture.json`、`tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`
- 未跟踪主控/审计文档：`docs/CURRENT_A_MASTER_CHECKPOINT.md`、`docs/CURRENT_FRONTEND_CONTRACT_GAPS.md`、`docs/CURRENT_PAYMENT_LAYER_DESIGN.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md`
- 未跟踪服务端文件：`src/Riftbound.Engine/PaymentCostRules.cs`
- 未跟踪本地文件：`riftbound-dotnet.sln`，不属于交付，默认不提交。

注意：`PaymentCostRules.cs` 已被 `CoreRuleEngine.cs` 引用，任何合并该批服务端改动时必须一并纳入，否则会构建失败。

### 当前服务端接口

HTTP：

- `GET /health`
- `GET /catalog/summary`
- `GET /catalog/p3-status`
- `GET /catalog/behavior-specs?cardNo=...`
- `GET /catalog/keyword-coverage`

SignalR：

- Hub：`/hubs/game`
- Client callable：`JoinRoom(roomId, playerId, reconnectToken?)`、`Reconnect(roomId, playerId, reconnectToken)`、`RequestSnapshot(roomId, playerId)`、`Pass(roomId, playerId, clientIntentId)`、`EndTurn(roomId, playerId, clientIntentId)`、`Ready(roomId, playerId, clientIntentId)`、`SubmitIntent(roomId, playerId, clientIntentId, cmd)`、development-only `SeedScenario(roomId, playerId, scenarioId, clientIntentId)`
- Server events：`Joined`、`Snapshot`、`Prompt`、`Events`、`Error`

核心协议 DTO：

- `SnapshotDto(Tick, TurnNumber, ActivePlayerId, Players, Lanes, Stack, Timing, TurnState)`
- `ActionPromptDto(PlayerId, Actionable, Reason, Actions, PromptId?, SnapshotTick?, Candidates?, View?)`
- `ActionPromptCandidateDto(Action, Label, Enabled, Reason, Sources?, Targets?, Destinations?, Modes?, OptionalCosts?, Metadata?)`
- `PromptViewDto(Type, Title, Message, RelatedBattlefieldId?, RelatedStackItemId?, RelatedBattleId?, RelatedSpellDuelId?, MinSelection?, MaxSelection?, Metadata?)`
- `GameEvent(Kind, Description, Payload)`
- `ErrorDto(Code, Message)`

当前没有名为 `MatchSnapshot`、`LegalAction`、`RoomState`、`GameLogEntry`、`ActionError` 的独立公开 DTO；真实公开名以 `SnapshotDto`、`ActionPromptCandidateDto`、SignalR room/session payload、`GameEvent`、`ErrorDto` 为准。

### 当前前端接口

前端服务 adapter：

- `ApiClient.health() -> /health`
- `ApiClient.behaviorSpecs() -> /catalog/behavior-specs`
- `ApiClient.keywordCoverage() -> /catalog/keyword-coverage`
- `MatchSocket.connect()`
- `MatchSocket.joinRoom(roomId, playerId, reconnectToken?)`
- `MatchSocket.reconnect(roomId, playerId, reconnectToken)`
- `MatchSocket.requestSnapshot(roomId, playerId)`
- `MatchSocket.ready(roomId, playerId, clientIntentId)`
- `MatchSocket.submitIntent(roomId, playerId, clientIntentId, command)`
- `MatchSocket.disconnect()`

前端路由：

- `home`、`cards`、`decks`、`lobby`、`room/:roomId`、`match/:matchId`、`result/:matchId`、`settings`

前端命令类型当前包括：

- `SUBMIT_DECK`、`MULLIGAN`、`PASS_PRIORITY`、`PASS_FOCUS`、`PASS`、`END_TURN`、`SURRENDER`、`PLAY_CARD`、`HIDE_CARD`、`REVEAL_CARD`、`TAP_RUNE`、`RECYCLE_RUNE`、`MOVE_UNIT`、`ASSEMBLE_EQUIPMENT`、`DECLARE_BATTLE`、`ACTIVATE_ABILITY`、`LEGEND_ACT`
- 命令可附 `promptId/snapshotTick`，但 C-Review 指出下一轮应把戳从“提交时套当前 prompt”调整为“命令生成时捕获对应 prompt”，避免旧草稿贴新戳。

### 当前 P0/P1 阻断

结论：**NOT READY**。

P0：

- 完整 battlefield / standby / control / held / conquer task lifecycle 未官方化。
- central cleanup task queue 未覆盖所有状态变化、替代效果和进出战场路径。
- spell duel / battle lifecycle 仍有大量代表路径，缺完整官方 pending / focus / initial-stack / combat damage assignment / battle cleanup 状态机。
- `PAY_COST`、`ASSIGN_COMBAT_DAMAGE`、`ORDER_TRIGGERS` 尚无正式 prompt / command / payload schema。
- 完整正式 18 步 E2E 尚未以同一连续正式牌局收口。
- 当前复杂 prompt 只有安全降级展示，尚未形成正式产品交互。

P1：

- PaymentEngine 仍未统一；主支付路径已有事件包络，但自动触发 / 替代费用等旧 `COST_PAID` 路径仍待迁移。
- LayerEngine / 持续效果 / 替代效果 / 禁止效果仍未达到最终完整模型。
- 1009 张卡效果覆盖矩阵、官方文本、FAQ 证据与自动化测试仍未最终清零。
- replay / recovery / determinism 仍需按全命令、全恢复、全随机边界补最终审计。
- 文档存在若干已修复项与历史批次描述混杂的问题，需要 D/E 后续清理 superseded 标注。

### B/C/D/E 子 agent 分工

- B：服务端规则 / 协议 / 测试。下一轮优先做负力量残留 clamp 审计、payment envelope 自动路径、battle/spellDuel 生命周期 id 预备、central cleanup queue 设计切片。
- C：前端契约 / Web UI / Chrome smoke。下一轮优先做 prompt 戳生成位置审查、未知 prompt 通用降级策略、metadata 安全展示策略、`relatedBattleId/relatedSpellDuelId` 展示与 E2E 断言准备。
- D：文档、规则证据、P0/P1 审计。下一轮优先更新前端契约缺口矩阵、阶段 0 checkpoint、completion audit 与 NOT READY 口径。
- E：卡牌效果覆盖、官方文本与 FAQ 证据矩阵。下一轮优先建立 full-official / representative 区分、1009 张卡覆盖矩阵、规则证据缺口索引。

### 不可并行修改文件列表

同一轮只能由一个 agent 修改：

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `src/Riftbound.Engine/PaymentCostRules.cs`
- `src/Riftbound.Contracts/Protocol.cs`
- `src/Riftbound.DevUi/src/stores/useMatchController.ts`
- `src/Riftbound.DevUi/src/services/matchSocket.ts`
- `src/Riftbound.DevUi/src/types/protocol.ts`
- `src/Riftbound.DevUi/src/components/match/ActionPanel.tsx`
- `src/Riftbound.DevUi/src/components/match/CardDetailDrawer.tsx`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_FRONTEND_CONTRACT_GAPS.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/rules-evidence-index.md`
- `data/official/card-catalog.zh-CN.json`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs`
- `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`
- `tests/Riftbound.ConformanceTests/Fixtures/**`

阶段 0 完成后必须等待用户确认，再进入下一阶段。

## 0.3 阶段 1 协议与规则基线汇总

阶段 1 已按 `docs/A_MASTER_AGENT_GOAL.md` 执行，范围限定为协议、`ActionPrompt` / command / payload、snapshot / visibility、服务端 P0 阻断梳理与小范围初步收口；未进入完整前端重建，未进入 1009 张卡全量实现，结论仍为 **NOT READY**。

阶段 1 checkpoint 保护：

- 已创建 commit：`78b6896 checkpoint: complete stage 1 protocol baseline`
- 该 commit 纳入 `docs/A_MASTER_AGENT_GOAL.md`、`docs/CURRENT_A_MASTER_CHECKPOINT.md`、`docs/CURRENT_STAGE1_PROTOCOL_BASELINE.md`、`docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`、`docs/CURRENT_FRONTEND_CONTRACT_GAPS.md`、`docs/CURRENT_RULE_EVIDENCE_TODO.md` 与被 `CoreRuleEngine` 引用的 `src/Riftbound.Engine/PaymentCostRules.cs`。
- `riftbound-dotnet.sln` 是本地不交付文件，未纳入 commit。
- commit 前验证：`git diff --check` 通过；`dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore` 3312/3312 通过；`npm run build` 通过。

### B 服务端协议 / 规则 / 测试

完成项：

- 确认真实协议 DTO 字段，不需要修改 `src/Riftbound.Contracts/Protocol.cs`。
- 为 `promptId + snapshotTick` 服务端语义补测试：匹配戳可通过；stale `promptId` 继续 `PROMPT_EXPIRED`；仅 `snapshotTick` 过期且 `promptId` 匹配时也返回 `PROMPT_EXPIRED`。
- 修复负力量残留 clamp：真实负力量叠加临时正战力修正后，回合结束 / 清除临时修正不再错误归零；snapshot 的 `basePower` 保留负值。

本阶段 B 修改：

- `src/Riftbound.Engine/MatchSession.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs`

关闭：

- 负力量“临时正修正到期后被夹成 0”的服务端 P0 残留。
- prompt 戳服务端最小语义测试缺口。

未关闭：

- 完整 CleanupQueue、Battle / SpellDuel lifecycle、PaymentEngine、LayerEngine、复杂 prompt payload、全卡 full-official-rule-pass。

### C 前端契约

完成项：

- `useMatchController.submitCommand` 只补缺失的 `promptId/snapshotTick`，不覆盖命令生成时已经携带的戳。
- `ActionPanel` 与 `CardDetailDrawer` 在命令生成处携带当前 prompt 戳。
- `PromptType` 允许未知服务端类型，避免未来复杂窗口让前端类型层卡死。
- 复杂 / 未知 prompt fallback 扩为安全通用策略；metadata 默认只展示数量 / 类型摘要，只有白名单安全字段展示字符串。
- 未新增 `PAY_COST`、伤害分配或触发排序命令；前端仍只展示、收集并提交服务端已有候选。

本阶段 C 修改：

- `src/Riftbound.DevUi/src/types/protocol.ts`
- `src/Riftbound.DevUi/src/stores/useMatchController.ts`
- `src/Riftbound.DevUi/src/components/match/ActionPanel.tsx`
- `src/Riftbound.DevUi/src/components/cards/CardDetailDrawer.tsx`

未关闭：

- 正式 `PAY_COST`、`ASSIGN_COMBAT_DAMAGE`、`ORDER_TRIGGERS` payload / schema / command。
- prompt 变化后清空各类 composer 草稿的系统化处理。
- 正式支付、伤害分配、触发排序专用 UI。
- typed snapshot views、battle / spell duel lifecycle 展示字段、cleanup queue 解释字段。

### D 文档 / 前端契约缺口

完成项：

- 新增 `docs/CURRENT_STAGE1_PROTOCOL_BASELINE.md`，记录真实 `SnapshotDto`、`ActionPromptDto`、`PromptViewDto`、`ActionPromptCandidateDto`、`GameEvent`、`ErrorDto` 字段，并明确当前没有独立 `MatchSnapshot` / `LegalAction` / `RoomState` / `GameLogEntry` / `ActionError` DTO。
- 更新 `docs/CURRENT_FRONTEND_CONTRACT_GAPS.md`，区分复杂 prompt 降级展示已做 / 正在做，与正式 payload / command / 专用交互仍缺。
- 更新 `docs/CURRENT_RULE_EVIDENCE_TODO.md`，把已验收的 0 / 负战力、具体战场大小写移到防回归，并补阶段 1 阻断视角。

本阶段 D 修改 / 新增：

- 新增 `docs/CURRENT_STAGE1_PROTOCOL_BASELINE.md`
- 修改 `docs/CURRENT_FRONTEND_CONTRACT_GAPS.md`
- 修改 `docs/CURRENT_RULE_EVIDENCE_TODO.md`

关闭：

- 阶段 1 协议字段文档口径缺口。

未关闭：

- 复杂 prompt 正式 payload / command、typed error details、stack / timing / object state 稳定字段、battle / spellDuel 完整 lifecycle 文档与实现缺口。

### E 卡牌覆盖基线

完成项：

- 新增 `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`。
- 基于 `data/official/card-catalog.zh-CN.json` 做只读统计：官方快照 1009 条，1009 个唯一 `id`，1009 个唯一精确 `cardNo`。
- 建立阶段 1 统计口径：card entry、collector no、base collector、effect-oracle / functional unit、representative、full-official。
- 确认 functional unit 口径为 811 个功能单元；113 个重复功能组涉及 311 条，理论可少写 198 个重复实现。
- 确认 `cardQaList` 为 0，FAQ 证据必须从 PDF / FAQ 抽取，不能从卡表字段推断。

本阶段 E 新增：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`

未关闭：

- 1009 张卡 full-official 覆盖矩阵、FAQ 证据抽取、逐卡实现 / 测试。

是否允许进入卡牌效果批量覆盖：**不允许**。阶段 4 前不得启动 1009 张全量实现。

### 阶段 1 修改 / 新增文件

本阶段新增：

- `docs/CURRENT_STAGE1_PROTOCOL_BASELINE.md`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`

本阶段修改：

- `src/Riftbound.Engine/MatchSession.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs`
- `src/Riftbound.DevUi/src/types/protocol.ts`
- `src/Riftbound.DevUi/src/stores/useMatchController.ts`
- `src/Riftbound.DevUi/src/components/match/ActionPanel.tsx`
- `src/Riftbound.DevUi/src/components/cards/CardDetailDrawer.tsx`
- `docs/CURRENT_FRONTEND_CONTRACT_GAPS.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`

注意：阶段 1 之外的既有未提交改动仍在工作区，包括 `docs/A_MASTER_AGENT_GOAL.md`、`docs/CURRENT_SERVER_RULE_AUDIT.md`、`docs/rules-evidence-index.md`、`src/Riftbound.Contracts/Protocol.cs`、`src/Riftbound.Engine/PaymentCostRules.cs` 等；最终合并时必须统一审查。

### 阶段 1 验收命令

A 主控复验：

- `git diff --check`：通过。
- `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore`：3312/3312 通过。
- `source ../../scripts/dev-env.sh && npm run build`：通过；包含 `check:event-labels` 与 `check:user-facing-text`。

子 agent 报告的补充验证：

- B 聚焦测试 4/4 通过，相关回归 16/16 通过，完整 conformance 3312/3312 通过。
- C DevUi build 通过，前端锁定文件 `git diff --check` 通过。

### 阶段 1 判断

- 服务端协议是否可以冻结：**阶段 1 协议壳可以冻结**。`SnapshotDto`、`ActionPromptDto`、`PromptViewDto`、`ActionPromptCandidateDto`、`GameEvent`、`ErrorDto` 作为当前基线可用；但复杂窗口仍只是预留，不代表完整官方规则 READY。
- 是否允许 C 进入正式前端 UI 开发：**只允许进入阶段 2 的基础页面 / 数据层 / 外壳开发**；复杂支付、伤害分配、触发排序 UI 必须等待服务端正式 payload / command 落地。
- 是否允许 E 进入卡牌效果批量覆盖：**不允许**。E 只能继续做 FAQ 抽取计划、覆盖矩阵和代表 / full-official 区分。
- 是否标记 READY：**不允许**。

### 阶段 1 后仍存在的 P0/P1

P0：

- 完整 battlefield / standby / control / held / conquer task lifecycle 未官方化。
- central cleanup task queue 未覆盖所有状态变化、替代效果和进出战场路径。
- spell duel / battle lifecycle 缺完整官方 pending / focus / initial-stack / damage assignment / cleanup 状态机。
- `PAY_COST`、`ASSIGN_COMBAT_DAMAGE`、`ORDER_TRIGGERS` 尚无正式 prompt / command / payload schema。
- 正式 18 步 E2E 未以同一连续正式牌局收口。
- 正式复杂 prompt 交互仍缺。

P1：

- PaymentEngine 自动触发 / 替代费用路径仍未完全统一。
- LayerEngine / 持续效果 / 替代效果 / 禁止效果未最终收口。
- 1009 张卡 full-official 覆盖矩阵、官方文本、FAQ 证据与自动化测试未完成。
- replay / recovery / determinism 仍需最终审计。

### 下一阶段建议

阶段 2 只建议进入“前端数据层与基础页面”：

- C 可推进正式 UI 外壳、页面结构和 snapshot / prompt 数据流，但不得实现复杂规则窗口裁决。
- B 继续设计 `PAY_COST` / `ASSIGN_COMBAT_DAMAGE` / `ORDER_TRIGGERS` 的正式服务端 schema 与最小状态机切片。
- D 维护阶段 2 前端契约验收矩阵。
- E 继续 FAQ 抽取计划和覆盖矩阵，不进入批量卡牌实现。

阶段 1 完成后必须等待用户确认，再进入阶段 2。

## 1. 总目标

以当前仓库五份官方规则 / FAQ PDF 与 `data/official/card-catalog.zh-CN.json` 的 2026-04-27 官网卡牌快照为准，完成本地双人 1v1 标准构筑产品级 Web 游戏基线：

- 服务端是唯一规则权威。
- 前端只展示并提交服务端 `ActionPrompt` 与权威 `snapshot` 支持的合法操作。
- 先本地房间，先双人对战，暂不做账号、人机、观战、回放、云部署。
- 前端目标为正式产品 UI，允许使用官网卡图；需要原型图时可使用 image generation。
- 允许分阶段小步拆出 `CleanupQueue`、`PaymentEngine`、`LayerEngine`、`BattleEngine` 等架构，但禁止无边界大改。
- 最终清除审计文档中的 P0/P1 阻断，后端 full test、Chrome smoke / 正式 18 步 E2E 与审计文档全部收口。
- 最终卡牌层目标是当前快照 1009 张卡按官方文本与 FAQ 完整覆盖。

当前阶段必须始终对齐两份用户指定文档：

- `docs/符文战场_前端Web开发需求文档_给Codex.md`：前端 P0 包括正式产品 UI、通用 prompt 渲染、支付费用、伤害分配、触发排序、结算链/法术对决/战斗状态展示，且前端不得裁决规则。
- `docs/符文战场_服务端核心规则自查文档.md`：服务端审计必须覆盖权威状态、隐藏信息、区域/对象/控制权、费用、清理、战斗、法术对决、伤害分配、得分胜负、单卡/关键词与测试证据。

## 2. 当前仓库基线

当前 HEAD：`dda6385 feat: support concrete battlefield moves`

当前工作区预期：

- `main` 分支。
- `riftbound-dotnet.sln` 可为未跟踪本地文件，不属于交付，不应提交。

最新提交已补：

- `MOVE_UNIT` 支持 `BASE -> BATTLEFIELD:<battlefieldObjectId>`。
- 基地单位移动到敌方已占战场可触发战场争夺与法术对决代表路径。
- `PLAY_CARD` prompt 的条件减费与 Core 条件判断对齐，避免未满足条件时暴露实际付不起的来源。

`dda6385` 后已由 A 主控确认、并在第一轮修复的风险：

- `CoreRuleEngine.NormalizeMoveUnitLocation` 曾把整个 destination 转大写，可能把正式战场 objectId 中的小写 `a` 改成 `A`。
- 官方快照存在小写 `a` 战场：`OGN·276a/298`、`OGN·278a/298`、`OGN·293a/298`。
- 第一轮 B 已修复为只规范化 zone，不改变 objectId 大小写，并补小写 `a` 战场移动测试。
- 第一轮 B 已修复 0 / 负力量清理语义：`Power <= 0 && Damage == 0` 不再自动入清理；`Power <= 0 && Damage > 0` 走致命伤害清理；负力量对象真实 `Power` 保留，战斗输出仍按 0。

## 3. 第一轮 Agent 分工

### B 服务端 P0

负责人：子 agent `019e0b77-176c-7dd1-918b-9d173df7e681`

允许修改：

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs`

任务：

- 修复具体战场移动 objectId 大小写回归，并补小写 `a` 战场测试。
- 修复 0 / 负力量单位清理规则风险：力量小于等于 0 不能自动死亡；受到至少 1 点伤害后应死亡；负力量战斗输出按 0，但真实力量保留。
- 状态：**已完成，A 主控已跑聚焦测试与后端 full test 验收。**

禁止：

- 不做 `PaymentEngine` / `LayerEngine` / `BattleEngine` 大重构。
- 不改前端和审计文档。

### E 证据审计

负责人：子 agent `019e0b77-20bc-7a51-8d00-b1d4d30d270e`

允许修改：

- `docs/rules-evidence-index.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- 可新增小型证据 TODO 文档

任务：

- 落 0 / 负力量 FAQ 证据。
- 落具体战场 objectId 大小写风险和验收条目。
- 保持 `NOT READY` 结论。
- 状态：**已完成。**

### C 服务端规则设计

负责人：子 agent `019e0b77-3747-7cd0-9824-04477aa9b88c`

允许修改：

- `docs/CURRENT_PAYMENT_LAYER_DESIGN.md`

任务：

- 输出第一阶段 `PaymentEngine` / `LayerEngine` 最小接口设计。
- 列出当前费用、减费、额外费用、持续效果散落点。
- 状态：**已完成，产物为 `docs/CURRENT_PAYMENT_LAYER_DESIGN.md`。**

### D 前端契约

负责人：子 agent `019e0b77-2c13-7290-b14b-4d4939eed5a0`

允许修改：

- `docs/CURRENT_FRONTEND_CONTRACT_GAPS.md`

任务：

- 列出正式产品 UI 仍需服务端补的字段 / 窗口。
- 拆正式 18 步 E2E 的 Chrome 调试顺序。
- 状态：**已完成，产物为 `docs/CURRENT_FRONTEND_CONTRACT_GAPS.md`。**

## 4. 第一轮验收记录

B 的代码结果已优先审查，E/C/D 文档结果已纳入工作区。

已跑验证：

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ZeroPower|FullyQualifiedName~NegativePower|FullyQualifiedName~LowercaseOfficialBattlefield|FullyQualifiedName~MoveUnitCommandMovesBaseUnitToConcreteBattlefield|FullyQualifiedName~ActionPromptOffersConcreteBattlefieldsForBaseUnitMove"`：11/11 通过。
- `git diff --check`：通过。
- `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore`：3304/3304 通过。

第一轮不得把项目标记为 READY。即使本轮全过，仍至少保留这些阻断：

- central cleanup queue 未完整官方化。
- spell duel / battle 完整生命周期仍未完成。
- PaymentEngine 未统一。
- LayerEngine 未统一。
- 全官方卡牌证据与执行仍未完成。
- 正式 18 步 E2E 未最终收口。

## 5. 恢复步骤

中断后恢复时：

1. 读本文。
2. 跑 `git status --short --branch`。
3. 看最新 8 条提交：`git log --oneline -8`。
4. 若子 agent 已完成，先审其修改文件和测试结果。
5. 本阶段拆任何前端/服务端任务前，先用 `docs/符文战场_前端Web开发需求文档_给Codex.md` 与 `docs/符文战场_服务端核心规则自查文档.md` 校验目标边界。
6. 若有冲突，优先保护 B 的服务端核心修复，再合并 E/C/D 文档。
7. 每轮结束更新本文的 HEAD、分工状态、测试记录和剩余阻断。

## 6. 第二轮 B2 协议契约状态

目标：最小、兼容补正式 PromptView/PromptType 契约入口。

已完成：

- `Riftbound.Contracts` 新增 `PromptTypes` 常量与 `PromptViewDto`，并在 `ActionPromptDto` 末尾追加可空 `View` 字段。
- `MatchSession.ActionPromptBuilder.Build` 为现有 prompt 构造 view，不改变 `actions/candidates` 语义。
- DevUi 协议类型新增 `PromptType`、`PromptViewDto`、`ActionPromptDto.view`，未改 UI 使用逻辑。
- 补充 `PromptView` 聚焦测试，覆盖 main prompt 的 `MAIN_ACTION` 与 mulligan prompt 的 `MULLIGAN`。
- A 主控已复审实现顺序：`PromptTypeFor` 与现有 `BuildPrompts` 的窗口优先级一致，且 `view` 为追加可空字段，不破坏旧消费者。

已跑验证：

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActionPrompt|FullyQualifiedName~PromptView|FullyQualifiedName~MatchRecovery"`：66/66 通过。
- `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore`：3306/3306 通过。
- `source ../../scripts/dev-env.sh && npm run build`：通过。
- `git diff --check`：通过。

仍保留阻断：

- damage assignment/payment/trigger ordering 本轮未实现。
- battle/spell duel 完整生命周期仍未收口；稳定 battle/spellDuel id 在第二波只做了派生视图入口。
- central cleanup queue、PaymentEngine、LayerEngine、正式 18 步 E2E 仍未最终 READY。

## 7. 第二波并行任务

启动时间：2026-05-09

### B2 前端 PromptView 消费

负责人：子 agent `019e0b88-0775-74a1-8b1f-741daa61d93d`

状态：**已完成，A 主控已复审并跑前端 build / full conformance 验收。**

允许修改：

- `src/Riftbound.DevUi/src/components/match/ActionPanel.tsx`
- `src/Riftbound.DevUi/src/components/match/MatchTopBar.tsx`
- 必要时新增小型前端 helper
- 可在 `docs/CURRENT_FRONTEND_CONTRACT_GAPS.md` 追加完成记录

禁止修改服务端契约和规则代码。

结果：

- `ActionPanel` 已优先展示 `prompt.view.title/message/type`；旧 prompt 无 view 时继续展示原 reason。
- `relatedBattlefieldId`、`relatedStackItemId` 只做轻量文本展示，不在前端推导规则。
- `MatchTopBar` 因需要改父级传参链路，本轮未动。

### Galileo Payment 探针

负责人：子 agent `019e0b92-1a62-7ec0-be51-615120328845`

状态：**已完成，只读。**

目标：基于当前代码找出 `PaymentEngine/PAY_COST` 下一步最小安全写入范围、测试过滤器与独占文件。

结论：

- 下一步 Payment 不应整体搬 `TryBuildPlayCardPlan`，应先抽支付原语、支付资源动作、token 解析和 quote 元数据。
- 建议最小新增 `src/Riftbound.Engine/PaymentCostRules.cs`，只在 `CoreRuleEngine.cs` / `MatchSession.cs` 接入调用点；第一刀不改 `Protocol.cs`，不引入 pending `PAY_COST` window。
- 独立 `PAY_COST` 仍缺 `PromptTypes.PayCost`、`PayCostCommand`、`paymentPlanId/paymentId/window`、typed cost/resource/additional cost schema 和 pending payment state。

### Faraday Battle/Prompt 探针

负责人：子 agent `019e0b92-2552-7062-a452-b8fd2336441b`

状态：**已完成，只读。**

目标：基于当前代码找出 battle/spell duel 稳定 id、`ASSIGN_COMBAT_DAMAGE` 与复杂 prompt 下一步最小安全写入范围、测试过滤器与独占文件。

结论：

- 下一步小改应先做稳定 `battleId/spellDuelId` 派生视图，不应直接拆 `ASSIGN_COMBAT_DAMAGE`。
- 原因：`ASSIGN_COMBAT_DAMAGE` 需要新增命令、PromptType、pending battle state，并把 `ResolveDeclareBattle` 从同步结算拆成多阶段状态机，风险大且容易与 Payment/PromptView 冲突。

### A 主控第二波稳定 ID 小改

状态：**已完成。**

改动：

- `SpellDuelState` 派生 `spellDuelId` 与 `battlefieldObjectId`。
- `BattleState` 派生 `battleId`。
- `snapshot.timing.spellDuel`、`snapshot.timing.battle`、`timing.battlefieldTasks` 追加对应 id。
- `PromptView.relatedBattleId` / `relatedSpellDuelId` 在战斗声明、法术对决焦点窗口中填充。
- 未新增 `ASSIGN_COMBAT_DAMAGE`，未改变 `ResolveDeclareBattle` 的同步战斗伤害结算。

已跑验证：

- `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~ActionPromptView"`：4/4 通过。
- `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~ActionPromptView|FullyQualifiedName~CoreRuleEngineStartsBattlefieldSpellDuelAfterStackResolutionLeavesContestedBattlefield|FullyQualifiedName~CoreRuleEngineMarksContestSpellDuelCompletedWhenAllPlayersPassFocus|FullyQualifiedName~CoreRuleEngineAllowsDeclareBattleForActiveStartBattleTask|FullyQualifiedName~P4DeclareBattleCommand"`：63/63 通过。
- `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore`：3308/3308 通过。
- `source ../../scripts/dev-env.sh && npm run build`：通过。
- `git diff --check`：通过。

## 8. 当前独占文件原则

- `src/Riftbound.Engine/CoreRuleEngine.cs`：同一时间只允许一个服务端规则 worker 修改。
- `src/Riftbound.Engine/MatchSession.cs`：同一时间只允许一个服务端协议/状态 worker 修改；前端 worker 禁止修改。
- `src/Riftbound.Contracts/Protocol.cs`：契约字段必须由 A 主控确认后单 agent 修改。
- `src/Riftbound.DevUi/src/components/cards/CardDetailDrawer.tsx`：动作 composer 集中且风险高，同一时间只允许一个前端 worker 修改。
- `src/Riftbound.DevUi/src/components/match/ActionPanel.tsx`：第二波由 B2 修改完成；后续继续前端 UI 时由单个前端 worker 独占。
- `src/Riftbound.DevUi/src/components/match/MatchTopBar.tsx`：第二波未改；若接入 `prompt.view` 需连同父级传参由单个前端 worker 独占。
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`：巨大测试文件，同一时间只允许一个服务端 worker 修改。
- `docs/CURRENT_SERVER_RULE_AUDIT.md` 与 `docs/CURRENT_FRONTEND_CONTRACT_GAPS.md`：可追加，但主控合并前要避免互相覆盖最新结论。

## 9. 第三波 Payment 最小抽取

状态：**已完成。**

目标：按 Galileo 探针建议，只抽支付原语，不引入 `PAY_COST` 状态机，不搬 `TryBuildPlayCardPlan`。

改动：

- 新增 `src/Riftbound.Engine/PaymentCostRules.cs`。
- 将符文/符能支付纯逻辑集中到 `PaymentCostRules.PayRuneCosts`、`CanPayRuneCosts`、`CanPayPowerCost`、`PayPowerCost`、`NormalizePowerCostByTrait`。
- 将经验支付集中到 `PaymentCostRules.PayExperienceCosts`。
- `CoreRuleEngine` 原私有支付方法保留为薄包装，以减少调用点改动和冲突面。

已跑验证：

- `source scripts/dev-env.sh && dotnet build src/Riftbound.Engine/Riftbound.Engine.csproj --no-restore`：通过。
- `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~P7TypedPowerPayment|FullyQualifiedName~P7PlayCardRecyclesRune|FullyQualifiedName~PaymentResource|FullyQualifiedName~P4ExperienceOptionalCost|FullyQualifiedName~P4HasteOptionalReadyBranch|FullyQualifiedName~CrescentGuardReady|FullyQualifiedName~P4AssembleEquipmentCommand|FullyQualifiedName~P79RagingDrake|FullyQualifiedName~P79BattlefieldStatic|FullyQualifiedName~P79BattlefieldHeldUnitCostIncrease|FullyQualifiedName~P79LegendAct"`：195/195 通过。
- `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore`：3308/3308 通过。
- `git diff --check`：通过。

仍保留阻断：

- 尚未统一支付资源动作副作用。
- 尚未统一 Core/Prompt 减费 quote。
- 尚未新增 `PAY_COST` prompt/schema/command/pending payment state。
- `COST_PAID` payload 已在主要玩家命令窗口补了 `paymentId/paymentWindow/remaining*`，战场触发/替代费用等自动支付旧路径仍待迁移，并需先向深层 helper 传入当前 action tick。

## 10. 第三波前端 PromptView 顶栏接入

状态：**已完成。**

改动：

- `MatchPage` 将当前 `prompt` 传给 `MatchTopBar`。
- `MatchTopBar` 展示服务端 `prompt.view.title/type`，作为顶栏窗口状态。
- 仍然只展示服务端状态，不根据 `PromptView` 在前端裁决行动、战斗或费用。

已跑验证：

- `source ../../scripts/dev-env.sh && npm run build`：通过。

## 11. 第三波 PromptType 预留

状态：**已完成。**

改动：

- `Riftbound.Contracts.PromptTypes` 预留 `SPELL_DUEL_ACTION`、`ASSIGN_COMBAT_DAMAGE`、`PAY_COST`、`ORDER_TRIGGERS`。
- DevUi `PromptType` union 同步这些复杂窗口类型。
- 本轮只稳定命名，不实际发出这些窗口，也不新增提交命令或 pending state。

已跑验证：

- `source scripts/dev-env.sh && dotnet build src/Riftbound.Contracts/Riftbound.Contracts.csproj --no-restore`：通过。
- `source ../../scripts/dev-env.sh && npm run build`：通过。
- `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore`：3308/3308 通过。
- `git diff --check`：通过。

## 12. 第三波 Payment 事件包络

状态：**已完成。**

目标：不引入 `PAY_COST` 状态机，只让主支付路径事件具备后续正式 UI/审计可追踪的稳定字段。

改动：

- `PaymentCostRules` 新增 `BuildPaymentId` 与 `BuildCostPaidPayload`。
- `PLAY_CARD` 主路径、`ASSEMBLE_EQUIPMENT` 主路径、伏击打出、启动技能、传奇行动、待命埋伏、待命翻开反应的 `COST_PAID` 追加 `paymentId/paymentWindow/remainingMana/remainingPower/remainingPowerByTrait/remainingExperience`。
- 通过回收符文支付资源动作产生的 `RUNE_RECYCLED` / `POWER_GAINED` 与对应 `COST_PAID` 使用同一个 `paymentId`。
- 保留既有 `mana/power/powerByTrait/optionalCosts/paymentResourceActions/recycledRuneObjectIds` 等字段，避免破坏现有前端与 fixture。

已跑验证：

- `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~P79TypedPowerPaymentSeedOffersAmountChoicesAndPlaysThroughHub|FullyQualifiedName~P79AssemblePaymentRecycleSeedOffersResourceAndAttachesThroughHub"`：2/2 通过。
- `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~ActivateAbility|FullyQualifiedName~LegendAct|FullyQualifiedName~HideCard|FullyQualifiedName~RevealCard|FullyQualifiedName~AssemblePaymentRecycle|FullyQualifiedName~TypedPowerPayment"`：193/193 通过。
- `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore`：3308/3308 通过。
- `source ../../scripts/dev-env.sh && npm run build`：通过。
- `git diff --check`：通过。

仍保留阻断：

- 尚未新增独立 `PAY_COST` prompt/command/pending state。
- 战场触发、替代效果等自动支付 `COST_PAID` 旧路径仍待迁移到同一包络；迁移前应先把 action tick 传入相关 helper，避免生成不可追踪的支付 id。
- Core 与 Prompt 的减费 quote 仍未统一。

## 13. 第三波 prompt 戳过期保护

状态：**已完成。**

目标：给正式 UI 的 `promptId + snapshotTick` 提交提供最小服务端错误语义，不改变旧客户端。

改动：

- `ErrorCodes` 新增 `PROMPT_EXPIRED`。
- DevUi `GameCommand` 支持可选 `promptId/snapshotTick`。
- `useMatchController.submitCommand` 会把当前 `ActionPromptDto.promptId/snapshotTick` 附到提交命令。
- DevUi 错误文案已覆盖 `PROMPT_EXPIRED`。
- `MatchSession.SubmitAsync` 在调用规则引擎前读取 raw command 中的可选 `promptId/snapshotTick`，与当前服务端 prompt 比对；不匹配时返回 rejected `ResolutionResult`，由 Hub 下发 `PROMPT_EXPIRED`。
- 未强制旧客户端必须提交 prompt 戳；未实现复杂 prompt payload schema。

已跑验证：

- `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~SubmitIntentRejectsStalePromptStamp|FullyQualifiedName~P79TypedPowerPaymentSeedOffersAmountChoicesAndPlaysThroughHub"`：2/2 通过。
- `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore`：3309/3309 通过。
- `source ../../scripts/dev-env.sh && npm run build`：通过。
- `git diff --check`：通过。

仍保留阻断：

- `PAY_COST` / `ASSIGN_COMBAT_DAMAGE` / `ORDER_TRIGGERS` 仍缺正式 prompt payload 与命令。
- 前端仍需要未知复杂 prompt 的通用安全渲染。

## 14. 第三波复杂 PromptView 降级渲染

状态：**已完成。**

目标：在正式复杂 prompt schema 未完成前，让前端遇到服务端未来发出的复杂窗口时有安全、可读、不裁决规则的降级展示。

改动：

- `ActionPanel` 对 `PAY_COST`、`ORDER_TRIGGERS`、`ASSIGN_COMBAT_DAMAGE`、`SPELL_DUEL_ACTION` 展示通用服务端选项面板。
- 面板展示候选来源、目标、位置、模式、费用和安全 metadata 摘要。
- 不生成服务端未提供的命令，不在前端计算费用、排序、伤害分配或法术对决结果。

已跑验证：

- `source ../../scripts/dev-env.sh && npm run build`：通过。

仍保留阻断：

- 复杂 prompt 的正式 payload schema、手动支付、伤害分配提交和触发排序提交仍未实现。
