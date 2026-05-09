# 当前前端契约缺口

日期：2026-05-09

本文件只记录正式产品 UI 继续推进所需的服务端字段/规则窗口缺口。依据当前 DevUi 与协议：

- `src/Riftbound.DevUi/src/types/protocol.ts`
- `src/Riftbound.DevUi/src/stores/useMatchController.ts`
- `src/Riftbound.DevUi/src/components/match/**`
- `src/Riftbound.DevUi/src/components/cards/CardDetailDrawer.tsx`
- `src/Riftbound.Contracts/Protocol.cs`
- `docs/CURRENT_STAGE1_PROTOCOL_BASELINE.md`
- `docs/符文战场_前端Web开发需求文档_给Codex.md`

## 1. 当前协议基线

当前前端和服务端契约已经对齐到以下最小模型：

- `SNAPSHOT`：`SnapshotDto` 下发 `tick`、`turnNumber`、`activePlayerId`、`players`、`lanes`、`stack`、`timing`、`turnState`。
- `PROMPT`：`ActionPromptDto` 下发 `playerId`、`actionable`、`reason`、`actions`、`promptId`、`snapshotTick`、`candidates`、`view`。
- `PromptViewDto`：以 `type`、`title`、`message`、`relatedBattlefieldId`、`relatedStackItemId`、`relatedBattleId`、`relatedSpellDuelId`、`minSelection`、`maxSelection`、`metadata` 提供当前 prompt 的最小展示视图。
- `ActionPromptCandidateDto`：以 `action`、`label`、`enabled`、`reason`、`sources`、`targets`、`destinations`、`modes`、`optionalCosts`、`metadata` 表达当前可提交候选。
- `EVENTS`：`GameEvent` 只稳定提供 `kind`、`description`、`payload`。
- `ERROR`：`ErrorDto` 只稳定提供 `code`、`message`。
- 命令：C#/TS 已覆盖 `SUBMIT_DECK`、`MULLIGAN`、`PASS_PRIORITY`、`PASS_FOCUS`、`PASS`、`END_TURN`、`SURRENDER`、`PLAY_CARD`、`HIDE_CARD`、`REVEAL_CARD`、`TAP_RUNE`、`RECYCLE_RUNE`、`MOVE_UNIT`、`ASSEMBLE_EQUIPMENT`、`DECLARE_BATTLE`、`ACTIVATE_ABILITY`、`LEGEND_ACT`。

当前仓库没有独立 `MatchSnapshot`、`LegalAction`、`RoomState`、`GameLogEntry`、`ActionError` DTO；对应能力分别落在 `SnapshotDto`、`ActionPromptDto/ActionPromptCandidateDto`、未开局 snapshot/prompt/session、`GameEvent[]`、`ErrorDto` 上。

这个模型足够支撑 DevUi 作为服务端联调桌面。第二轮已追加最小 `PromptView/PromptType` 入口，第三轮已追加复杂 prompt 的安全降级展示，但它仍只是现有 prompt 的兼容视图；距离需求文档中的完整正式 prompt 契约，还缺少通用约束 schema、提交 payload schema 与复杂规则窗口的结构化解释。

## 2. 已可由 ActionPrompt/snapshot 驱动的 UI

以下 UI 已经可以不在前端裁决规则，完全依赖当前 `ActionPrompt`、`snapshot`、`events`、`errors` 推进：

- 连接、重连、重新同步：`useMatchController` 只保存服务端 session、snapshot、prompt、events、errors，并通过 `MatchSocket` 提交 intent。
- 房间入座、提交 starter deck、准备：`RoomPage` 只展示 room snapshot，并从 `prompt.candidates` 中执行 `SUBMIT_DECK`、`READY`。
- 对局顶栏：`MatchTopBar` 从 `snapshot.timing.turnWindow`、`timing.spellDuel`、`timing.promptPlayerId`、`activePlayerId` 展示阶段、窗口、行动权、焦点、提示归属。
- 玩家区域：`PlayerBoard` 从 `PlayerSnapshotView` 展示分数、经验、ready、runePool、zones、objects；对手手牌使用 `handSize/handHidden` 生成卡背，不读取未公开卡名。
- 公共战场：`BattlefieldArea` 从 `snapshot.lanes.battlefields` 和对象索引展示战场、控制者、争夺中、单位、待命公开/隐藏数量。
- 卡面和详情：`CardFace`、`CardDetailDrawer` 只用 snapshot 暴露的 object 与 catalog 行为文本展示卡牌、状态、伤害、战力、隐藏信息。
- 结算链/任务概览：`StackPanel` 从 `snapshot.stack`、`timing.pendingTaskQueue`、`timing.battleResolutions`、`timing.battlefieldResolutions` 展示服务端队列摘要。
- 事件/错误日志：`EventLog` 使用服务端 `GameEvent` 和 `ErrorDto`，对内部 token 做前端文案兜底/脱敏，不产生规则结果。
- 动作栏简单命令：`ActionPanel` 可由候选直接提交 `PASS_PRIORITY`、`PASS_FOCUS`、`PASS`、`END_TURN`、`SURRENDER`、单来源 `TAP_RUNE`、`RECYCLE_RUNE`，以及不需要额外选择的单来源 `PLAY_CARD`。
- 起手调整：`MulliganCandidate` 已支持 `MULLIGAN`，依赖 `candidate.sources` 与 `candidate.metadata.maxSelectionCount`，不会本地决定可调度上限。
- 卡牌来源操作 composer：`CardDetailDrawer` 已按 `candidate.metadata.sourceRequirements` 支持 `PLAY_CARD`、`HIDE_CARD`、`REVEAL_CARD`、`MOVE_UNIT`、`ASSEMBLE_EQUIPMENT`、`ACTIVATE_ABILITY`、`LEGEND_ACT`、`DECLARE_BATTLE` 的来源、目标、模式、目的地、可选费用、支付资源、额外费用组合。

注意：这些 composer 只是在前端根据服务端约束做表单形状校验和禁用态；最终合法性仍必须由服务端确认。正式产品 UI 可以复用这条思路，但应把 `metadata.sourceRequirements` 收敛为稳定契约，而不是继续扩散临时 metadata。

## 3. 前端阻断清单

### P0：PromptView 仍缺少复杂窗口契约

需求文档要求所有玩家决策窗口由 `PromptView.type` 驱动，例如 `PAY_COST`、`ORDER_TRIGGERS`、`ASSIGN_COMBAT_DAMAGE`、`SPELL_DUEL_ACTION`。当前 C#/TS 契约已具备 `ActionPromptDto.view` 的最小入口，并且已经把复杂 prompt 降级展示与正式 payload 缺口分开。

阶段 1 已完成/正在做：

- C#/TS 已预留 `PAY_COST`、`ORDER_TRIGGERS`、`ASSIGN_COMBAT_DAMAGE`、`SPELL_DUEL_ACTION` prompt type。
- `ActionPanel` 已为这四类复杂 prompt 增加通用服务端选项面板，可展示候选来源、目标、位置、模式、费用和安全 metadata 摘要。
- `promptId + snapshotTick` 的服务端过期错误语义已具备最小实现：前端提交命令时会附带当前 prompt 戳，服务端若发现与当前权威 prompt 不匹配，返回 `PROMPT_EXPIRED` 且不进入规则引擎。
- 这条路径只允许“服务端已经声明的候选”被展示，不在前端计算费用、排序触发、分配伤害或推进法术对决。

仍缺正式产品契约：

- `options/selectableCards/selectableTargets/selectableZones/constraints/defaultAction` 等 typed 字段。
- 复杂 prompt 提交 payload 的统一命令或 discriminated union。
- `PAY_COST`、`ORDER_TRIGGERS`、`ASSIGN_COMBAT_DAMAGE`、`SPELL_DUEL_ACTION` 的专用交互模型、错误 details 和 E2E 断言点。
- 未知 prompt 的通用渲染协议仍需稳定：标题、说明、安全 JSON 摘要、默认动作约定、不可提交策略。

正式 UI 可继续兼容 `ActionPromptCandidateDto`，但复杂窗口需要服务端提供显式 prompt 类型与专用 payload。

### P0：snapshot / visibility 正式契约缺口

当前 `BuildSnapshotForViewer` 已按 viewer 构造玩家视图，对手手牌以数量和隐藏项展示，DevUi 也不从隐藏对象读取卡名；这是阶段 1 可继续推进的基础。但正式产品 UI 仍缺稳定 snapshot/visibility 契约：

- `players/lanes/timing/stack` 仍大量依赖字典或 unknown 投影，字段含义和可见性边界没有 schema 化。
- 没有独立 `RoomState` / `MatchSnapshot` DTO，未开局房间状态、已开局对局状态、重连状态仍复用 snapshot/prompt/session。
- `GameEvent.payload` 是开放字典，不能作为隐藏信息或规则结果的长期稳定来源。
- 隐藏信息的红线需要正式化到 contract：哪些 zone 只给数量，哪些对象可给 cardNo，哪些 metadata 不得泄漏给非 controller。
- 后续 Chrome E2E 必须覆盖双窗口 store、DOM、network/WS 三层隐藏信息断言。

### P0：damage assignment 窗口缺口

当前无 `ASSIGN_COMBAT_DAMAGE` prompt，也无对应命令。阶段 1 已有 `DECLARE_BATTLE` 代表路径、`PromptView.relatedBattleId` 和 `timing.battle/battleResolutions` 摘要，但这些不能替代独立伤害分配窗口。正式伤害分配 UI 需要服务端下发：

- `battleId`、`battlefieldId`、`assigningPlayerId`、进攻/防守身份、关联战斗阶段。
- 可分配总伤害、每个伤害来源、是否同时造成伤害。
- 可被分配单位列表：`objectId`、controller、当前战力、已有伤害、致命阈值、壁垒/后排/不可分配等标记。
- 约束：每个单位 min/max、必须先致命/不能过量、可重复/不可重复、剩余伤害处理、合法默认方案。
- 提交命令：按对象提交 `{ targetObjectId, amount }[]`，带 `promptId/snapshotTick`。
- 服务端错误：非法分配的具体原因，用于 UI 提示。

前端不得根据单位战力自行判定最终合法分配；只能禁用明显不满足服务端 constraints 的输入。

### P0：trigger ordering 窗口缺口

当前只在事件中看到 `TRIGGER_QUEUED/TRIGGER_RESOLVED`，没有 `ORDER_TRIGGERS` prompt 和命令。阶段 1 只有 prompt type 预留和降级面板承接能力，服务端尚未实际发出排序 payload。正式 UI 需要服务端下发：

- 待排序触发列表：`triggerId`、controller、sourceObjectId、sourceCardNo、触发时机、简短文本、是否可选。
- 排序约束：哪些触发必须相邻、哪些不可调整、默认顺序、是否允许取消可选触发。
- 目的地：加入结算链、立即结算、进入任务队列。
- 提交命令：`triggerIds` 的最终顺序与可选触发选择。

拖拽排序只能收集用户选择，结算链顺序仍由服务端确认。

### P0：payment plan 窗口缺口

当前出牌/装配 composer 能读取部分 `sourceRequirements` 里的费用、可选费用、支付资源；服务端已有部分 `PaymentCostRules` / 支付事件包络可以降低审计风险。但这不是通用 `PAY_COST` prompt，也没有 `DECLINE_PAY_COST`、pending payment state 或统一 Quote/Authorize/Commit 生命周期。正式 UI 需要服务端提供稳定支付计划：

- 费用项列表：基础费用、强制额外费用、可选额外费用、触发费用、替代费用、减少/增加费用来源。
- 可用资源来源：法力、符能按特性、可回收/可横置/可消费对象及其贡献。
- 自动支付预览：如果服务端自动支付，需返回支付方案和解释。
- 手动支付约束：是否必须精确支付、是否允许超付、是否可拒绝支付、是否可取消。
- 提交命令：独立 `PAY_COST`/`DECLINE_PAY_COST`，或在当前 action 中带结构化 `paymentPlanId` 与选择项。

仅靠 `optionalCosts: string[]` 无法表达通用触发费用、拒绝支付、替代费用和自动支付说明。

### P0：cleanup queue 窗口/解释缺口

当前 `StackPanel` 可以读取 `timing.pendingTaskQueue` 并展示前 4 个任务摘要，但字段是 `Record<string, unknown>`，没有正式 UI 契约。正式 UI 需要：

- `phase`、`activeTaskId`、`isBlocking` 的稳定枚举与含义。
- 每个 cleanup task 的 `taskId/kind/reason/relatedObjectIds/relatedBattlefieldId/createdBy/expectedResult/visibility/animationHint`。
- 重要清理结果的事件聚合：摧毁、触发入队、控制权变化、待命移除、战斗身份变化。
- 如果清理中需要玩家选择，必须转换为正式 prompt，而不是只出现在 task queue。

产品 UI 默认不为每次清理弹窗，但必须能解释“服务端正在处理什么”和“为什么普通行动被阻塞”。

### P0：spell duel / battle lifecycle 缺口

当前顶栏读取 `timing.spellDuel.focusPlayerId`，`StackPanel` 读取 `battleResolutions/battlefieldResolutions`，`DECLARE_BATTLE` composer 可声明战斗；阶段 1 已有 `spellDuelId/battleId` 及相关 `PromptView` 关联字段，但缺少完整生命周期契约：

- `spellDuelId/battleId`、关联战场、触发原因、进入/关闭时间点。
- 法术对决状态：普通/法术对决、开环/闭环、焦点玩家、上次让过玩家、可响应窗口、关闭条件。
- 战斗状态：进攻方/防守方、参战单位、战斗前法术对决、伤害分配阶段、同时造成伤害、战斗清理、结果。
- 与 prompt 的关联：`SPELL_DUEL_ACTION`、`PASS_FOCUS`、`DECLARE_BATTLE`、`ASSIGN_COMBAT_DAMAGE` 应能通过 `relatedBattlefieldId/battleId/stackItemId` 串起来。
- 前端展示用 summary：当前等待谁、为什么等待、下一步可能进入什么窗口。

没有这些字段，正式 UI 只能显示碎片化状态，无法做战场高亮、焦点提示、战斗阶段条和可靠 E2E 断言。

### P1：layer/effect explanation 缺口

当前 `stack` 项只展示 controller、cardNo、effectKind、targetObjectIds、destination 等摘要。正式规则助手和产品 UI 需要服务端提供玩家可读解释：

- stack/effect 项目：`stackItemId/effectId/layer/effectKind/sourceObjectId/sourceCardNo/controllerId/targets/mode/destination/status`。
- 效果解释：将执行的卡面文本片段、规则编号、替代/预防效果、费用变化、目标变化。
- “为什么合法/非法”的 reason：对禁用 candidate、目标、费用、窗口状态提供可展示原因。
- 变更预览与结算结果关联：哪些对象会被影响，哪些结果已经由服务端确认。

前端可以做 tooltip/规则助手，但不应在本地推导层、替代效果、持续效果或依赖关系。

### P1：快照字段仍偏 DevUi

当前 `players/lanes/timing/stack` 多处是 `Record<string, unknown>` 或 `unknown[]`，适合快速联调，不适合正式产品 UI 长期维护。建议逐步固化：

- `BattlefieldView`：控制者、争夺状态、得分状态、参战/待命槽、关联 battle/spellDuel。
- `StackItemView`：待处理/已确认/结算中、来源、目标、控制者、等待玩家。
- `TimingView`：phase、turnWindow、spellDuel、battle、promptPlayer、winner/roomStatus。
- `ObjectStateView`：关键词/状态效果的稳定枚举、damage/effectivePower/lethal risk、attacking/defending。

## 4. Chrome 调试计划：18 步 E2E 拆分

后续正式 Chrome E2E 不建议写成一条巨长脚本。建议拆为 4 组、18 个可独立截图/断言的步骤，每步记录 console、network/WS、关键截图和当前 `promptId/snapshot.tick`。

### A. 接入与房间

1. 启动 API 与前端，打开 Chrome，确认无首屏 console error。
2. 窗口 A/B 使用不同 `playerId`，分别进入同一房间。
3. A/B 连接/重连入座，断言 room snapshot 出现两个玩家且不重复入座。
4. A/B 按 prompt 提交卡组，断言 `deckSubmitted` 只来自服务端 snapshot。
5. A/B 准备并进入 match，断言 `MATCH_STARTED`/opening events 与初始 snapshot。

### B. 开局与普通行动

6. 起手调整：A/B 各只看到自己的 4 张手牌，选择 0-2 张并提交，断言对手手牌不泄漏。
7. 顶栏：断言 turn、phase、普通/法术对决窗口、行动权、焦点、prompt 归属。
8. 资源/符文：执行服务端允许的 `TAP_RUNE`/`RECYCLE_RUNE` 或召出符文流程，断言资源只随 snapshot 更新。
9. 打出卡牌：从候选打开卡牌详情 composer，完成模式/目标/目的地/费用选择，提交后等待 snapshot。
10. 移动/待命：执行 `MOVE_UNIT`、`HIDE_CARD` 或 `REVEAL_CARD`，断言非法目标只显示服务端原因。

### C. 结算链、法术对决、战斗

11. 结算链：打出法术/能力后断言 stack item 来源、控制者、目标、状态。
12. 普通闭环：A/B 轮流 `PASS_PRIORITY`，断言前端不自行结算，只等服务端事件和 snapshot。
13. 法术对决开始：触发争夺或服务端 fixture，断言 `SPELL_DUEL_STARTED`、焦点玩家、可用反应候选。
14. 法术对决关闭：双方 `PASS_FOCUS`，断言关闭事件、窗口转换、战场状态仍以服务端为准。
15. 声明战斗：执行 `DECLARE_BATTLE`，断言 battle id/参战单位/攻防身份/关联战场可见。
16. 战斗伤害分配：进入 `ASSIGN_COMBAT_DAMAGE`，完成分配并提交，断言伤害在服务端确认后才显示。

### D. 清理、终局、安全

17. 清理/得分/胜负：断言 cleanup queue、摧毁、控制权、征服/据守、winner/result 只来自服务端。
18. 断线重连与隐藏信息：断开、重连、请求 snapshot，断言行动禁用、场面恢复、DOM/store/network 不泄漏对手隐藏信息。

## 5. 产品 UI 注意事项

- DevUi 是契约探针，不应直接当正式 UI；正式 UI 应保留服务端权威、隐藏信息保护、prompt 驱动和错误可见性，再重做产品级布局、动画、可访问性和响应式。
- 官网卡图允许用于公开卡牌、图鉴、卡牌详情和已公开对象展示；隐藏对象仍只能显示卡背/数量，不得因为本地 catalog 或图片 URL 推断对手手牌/待命。
- 前端不得裁决规则：不判定卡牌能否打出、目标是否合法、费用是否合法、战斗是否发生、伤害是否合法、清理结果、得分胜负、投降成功。
- 前端可以做输入形状校验、禁用态和解释，但依据必须来自服务端 prompt constraints/candidates/reasons。
- 未知 prompt 类型必须可降级显示：标题、说明、服务端选项、原始安全 JSON 摘要、不可提交或仅提交服务端声明的默认动作。
- 每次 `promptId` 或 `snapshotTick` 改变时，正式 UI 应清空对应本地草稿，避免向旧窗口提交选择。
- 服务端错误不能吞掉：非法行动、过期 prompt、不同步、费用不足、非法目标都应在 prompt 内或 toast 中可见。

## 6. 第二轮 PromptView 契约入口完成记录

- 已在正式协议追加 `ActionPromptDto.view`，字段类型为 `PromptViewDto`；旧 `ActionPromptDto(playerId, actionable, reason, actions, ...)` 构造保持兼容。
- `PromptViewDto` 当前最小字段包含 `type/title/message`，并预留关联战场、结算链项目、战斗、法术对决、选择上下限与 metadata。
- 后端 `ActionPromptBuilder.Build` 已为现有 prompt 生成 view，不改变 `actions/candidates` 语义；已覆盖 `ROOM_SETUP`、`MULLIGAN`、`MAIN_ACTION`、`STACK_PRIORITY`、`SPELL_DUEL_FOCUS`、`BATTLE_DECLARATION`、`TASK_QUEUE`、`WAIT`、`MATCH_RESULT`。
- 契约已预留但尚未实际发出 `SPELL_DUEL_ACTION`、`PAY_COST`、`ORDER_TRIGGERS`、`ASSIGN_COMBAT_DAMAGE`。
- DevUi 已同步 TypeScript 类型，可读取 `prompt.view.type`；`ActionPanel` 与 `MatchTopBar` 已开始消费该视图。
- 本轮仍不实现 damage assignment、payment engine、trigger ordering，也不补完整 battle/spell duel 生命周期。

## 7. 第二轮 DevUi PromptView 消费记录

- `ActionPanel` 已优先展示服务端 `prompt.view.title/message/type`；旧 prompt 没有 view 时继续显示原有 reason 文案和候选行动。
- `relatedBattlefieldId`、`relatedStackItemId` 仅作为轻量关联文本展示，不在前端推导战斗、结算或规则结果。
- `MatchTopBar` 当前没有接收 prompt；为遵守本轮写入范围，未改 `MatchPage` 传参链路。

## 8. 第二轮 battle/spellDuel 关联 ID 记录

- 后端已为 `snapshot.timing.spellDuel` 追加 `spellDuelId` 与 `battlefieldObjectId`，为 `snapshot.timing.battle` 追加 `battleId`。
- `timing.battlefieldTasks` 的 `START_SPELL_DUEL` / `START_BATTLE` 任务已分别带 `spellDuelId` / `battleId`。
- `PromptView.relatedSpellDuelId` 已在法术对决焦点窗口填充；`PromptView.relatedBattleId` 已在声明战斗窗口填充。
- 本轮仍未实现 `ASSIGN_COMBAT_DAMAGE` prompt/command，也未把战斗从 `DECLARE_BATTLE` 同步结算拆成多阶段状态机。

## 9. 第三轮 DevUi 顶栏 PromptView 消费记录

- `MatchPage` 已把当前 `prompt` 传入 `MatchTopBar`。
- `MatchTopBar` 已显示服务端 `prompt.view.title/type`，用于快速确认当前服务端窗口。
- 顶栏只展示服务端窗口状态，不在前端推导合法行动、费用、战斗或结算结果。

## 10. 第三轮复杂 PromptType 预留记录

- C#/TS 契约均已预留 `SPELL_DUEL_ACTION`、`ASSIGN_COMBAT_DAMAGE`、`PAY_COST`、`ORDER_TRIGGERS`。
- 本轮只稳定正式 UI 后续要识别的窗口名称，不发出这些窗口，不新增提交 payload，也不新增本地裁决。
- 已验证：Contracts build 通过、DevUi build 通过、后端 conformance 3308/3308 通过、`git diff --check` 通过。

## 11. 第三轮 prompt 戳过期保护记录

- DevUi `GameCommand` 已支持可选 `promptId/snapshotTick`，`useMatchController.submitCommand` 会把当前服务端 `ActionPromptDto` 的戳附到命令上；错误文案已覆盖 `PROMPT_EXPIRED`。
- 服务端 `MatchSession.SubmitAsync` 会在进入规则引擎前检查这些可选字段；若客户端提交的 prompt 戳已过期，返回 `PROMPT_EXPIRED`。
- 旧客户端不传 `promptId/snapshotTick` 时仍走原有规则检查路径，保证兼容。
- 本轮只处理过期窗口错误语义，不实现 `PAY_COST` / `ASSIGN_COMBAT_DAMAGE` / `ORDER_TRIGGERS` 的正式 payload schema。

## 12. 第三轮复杂 PromptView 降级渲染记录

- `ActionPanel` 已为 `PAY_COST`、`ORDER_TRIGGERS`、`ASSIGN_COMBAT_DAMAGE`、`SPELL_DUEL_ACTION` 增加通用服务端选项面板。
- 面板只展示候选来源、目标、位置、模式、费用和安全 metadata 摘要；不根据这些字段本地裁决规则，也不生成服务端未提供的命令。
- 该渲染用于正式复杂 prompt 接入前的安全降级；完整 payload schema、手动支付、伤害分配和触发排序交互仍是 P0 缺口。
- 已验证：DevUi build 通过。

## 13. 阶段 1 D 汇总

完成项：

- 新增 `docs/CURRENT_STAGE1_PROTOCOL_BASELINE.md`，记录 `SnapshotDto`、`ActionPromptDto`、`PromptViewDto`、`ActionPromptCandidateDto`、`GameEvent`、`ErrorDto` 的真实字段。
- 明确当前没有独立 `MatchSnapshot`、`LegalAction`、`RoomState`、`GameLogEntry`、`ActionError` DTO，避免后续任务引用不存在的协议名。
- 本文件已区分“复杂 prompt 降级展示已做/正在做”和“正式 payload/command/专用交互仍缺”。
- 已把阶段 1 服务端 P0 阻断映射到前端契约视角：ActionPrompt/command/payload、snapshot/visibility、Payment/trigger/damage assignment、battle/spellDuel lifecycle。

本轮修改/新增文件：

- `docs/CURRENT_STAGE1_PROTOCOL_BASELINE.md`
- `docs/CURRENT_FRONTEND_CONTRACT_GAPS.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`

关闭/仍存在 P0/P1：

- 本轮 D 只做文档与规则证据审计，没有关闭新的功能 P0/P1。
- 已关闭口径风险：prompt 戳过期保护、复杂 prompt 降级展示、`PromptView` 最小入口不应继续被描述为“完全没有”。
- 仍存在 P0：复杂 prompt 正式 payload/command、`PAY_COST`/`DECLINE_PAY_COST`、`ASSIGN_COMBAT_DAMAGE`、`ORDER_TRIGGERS`、battle/spellDuel 完整生命周期、snapshot/visibility typed contract、正式 18 步 E2E。
- 仍存在 P1：layer/effect explanation、typed error details、stack/timing/object state 的长期稳定字段。

文档风险：

- `docs/CURRENT_SERVER_RULE_AUDIT.md` 与 `docs/rules-evidence-index.md` 本轮按锁不修改，仍需 A/E 同步阶段 1 协议基线和 P0 状态。
- 当前基线来自源码字段审计，不等同于官方规则完成度证明；READY 判断仍必须等服务端规则、前端 E2E、证据索引一起清零。
