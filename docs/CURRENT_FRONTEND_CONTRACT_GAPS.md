# 当前前端契约缺口

日期：2026-05-09

本文件只记录正式产品 UI 继续推进所需的服务端字段/规则窗口缺口。依据当前 DevUi 与协议：

- `src/Riftbound.DevUi/src/types/protocol.ts`
- `src/Riftbound.DevUi/src/stores/useMatchController.ts`
- `src/Riftbound.DevUi/src/components/match/**`
- `src/Riftbound.DevUi/src/components/cards/CardDetailDrawer.tsx`
- `src/Riftbound.Contracts/Protocol.cs`
- `docs/CURRENT_STAGE1_PROTOCOL_BASELINE.md`
- `docs/CURRENT_STAGE3B_PLAN.md`
- `docs/CURRENT_STAGE3C_SPELL_DUEL_BATTLE_DAMAGE_EVIDENCE.md`
- `docs/CURRENT_STAGE3_COMPLETION_AUDIT.md`
- `docs/符文战场_前端Web开发需求文档_给Codex.md`

## 1. 当前协议基线

当前前端和服务端契约已经对齐到以下最小模型：

- `SNAPSHOT`：`SnapshotDto` 下发 `tick`、`turnNumber`、`activePlayerId`、`players`、`lanes`、`stack`、`timing`、`turnState`。
- `PROMPT`：`ActionPromptDto` 下发 `playerId`、`actionable`、`reason`、`actions`、`promptId`、`snapshotTick`、`candidates`、`view`。
- `PromptViewDto`：以 `type`、`title`、`message`、`relatedBattlefieldId`、`relatedStackItemId`、`relatedBattleId`、`relatedSpellDuelId`、`minSelection`、`maxSelection`、`metadata` 提供当前 prompt 的最小展示视图。
- `ActionPromptCandidateDto`：以 `action`、`label`、`enabled`、`reason`、`sources`、`targets`、`destinations`、`modes`、`optionalCosts`、`metadata` 表达当前可提交候选。
- `EVENTS`：`GameEvent` 只稳定提供 `kind`、`description`、`payload`。
- `ERROR`：`ErrorDto` 只稳定提供 `code`、`message`。
- 命令：C#/TS 已覆盖 `SUBMIT_DECK`、`MULLIGAN`、`PASS_PRIORITY`、`PASS_FOCUS`、`PASS`、`END_TURN`、`SURRENDER`、`PLAY_CARD`、`HIDE_CARD`、`REVEAL_CARD`、`TAP_RUNE`、`RECYCLE_RUNE`、`MOVE_UNIT`、`ASSEMBLE_EQUIPMENT`、`DECLARE_BATTLE`、`ACTIVATE_ABILITY`、`LEGEND_ACT`、`PAY_COST`、`ASSIGN_COMBAT_DAMAGE`、`ORDER_TRIGGERS`。

当前仓库没有独立 `MatchSnapshot`、`LegalAction`、`RoomState`、`GameLogEntry`、`ActionError` DTO；对应能力分别落在 `SnapshotDto`、`ActionPromptDto/ActionPromptCandidateDto`、未开局 snapshot/prompt/session、`GameEvent[]`、`ErrorDto` 上。

这个模型足够支撑 DevUi 作为服务端联调桌面。第二轮已追加最小 `PromptView/PromptType` 入口，第三轮已追加复杂 prompt 的安全降级展示，阶段 2 B 已补后三类复杂命令的首版 schema skeleton；距离需求文档中的完整正式 prompt 契约，还缺少 runtime 约束、状态机绑定与复杂规则窗口的结构化解释。

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

需求文档要求所有玩家决策窗口由 `PromptView.type` 驱动，例如 `PAY_COST`、`ORDER_TRIGGERS`、`ASSIGN_COMBAT_DAMAGE`、`SPELL_DUEL_ACTION`。当前 C#/TS 契约已具备 `ActionPromptDto.view` 的最小入口，并且已经把复杂 prompt 降级展示、正式 command/schema 骨架、真实 runtime prompt 缺口分开。

阶段 1 已完成/正在做：

- C#/TS 已预留 `PAY_COST`、`ORDER_TRIGGERS`、`ASSIGN_COMBAT_DAMAGE`、`SPELL_DUEL_ACTION` prompt type。
- `ActionPanel` 已为这四类复杂 prompt 增加通用服务端选项面板，可展示候选来源、目标、位置、模式、费用和安全 metadata 摘要。
- `promptId + snapshotTick` 的服务端过期错误语义已具备最小实现：前端提交命令时会附带当前 prompt 戳，服务端若发现与当前权威 prompt 不匹配，返回 `PROMPT_EXPIRED` 且不进入规则引擎。
- 这条路径只允许“服务端已经声明的候选”被展示，不在前端计算费用、排序触发、分配伤害或推进法术对决。

阶段 2 B 已同步的契约骨架：

- 新增 `ErrorCodes.InvalidPayload`，malformed `PAY_COST` / `ASSIGN_COMBAT_DAMAGE` / `ORDER_TRIGGERS` payload 可稳定返回 `INVALID_PAYLOAD`。
- 新增 `PayCostCommand(paymentId, paymentWindow, paymentChoiceIds)`。
- 新增 `CombatDamageAssignmentDto(sourceObjectId, targetObjectId, damage)` 与 `AssignCombatDamageCommand(battleId, battlefieldId, assignments)`。
- 新增 `OrderTriggersCommand(triggerIds)`。
- 新增 `ActionPromptContractDto` / `ActionPromptContracts`，用于描述复杂 prompt 的 required payload、legal choices、validation errors、visible metadata 与 hidden metadata。

C 可同步类型和做安全承接：

- 同步 TS command union、`ActionPromptContractDto`、错误码文案和调试展示。
- 在服务端实际发出 runtime prompt/candidate 时，继续使用现有降级面板展示服务端声明的候选和安全 metadata。
- 对 malformed payload / stale prompt / unsupported command 做可见错误提示。

C 当前复杂交互边界：

- 不实现手动支付向导、完整伤害分配器、完整 APNAP / 跨控制者触发排序或法术对决复杂选择器；3D 仅允许 `ORDER_TRIGGERS` 最小上移 / 下移排序 UI。
- 不在浏览器侧构造服务端未声明的 `paymentChoiceIds`、`assignments`、`triggerIds` 或 `orderedTriggerIds`。
- 不把合法形状 command 能被识别误读为完整规则窗口已开放；`PAY_COST`、`ASSIGN_COMBAT_DAMAGE`、`ORDER_TRIGGERS` 只有阶段性最小 runtime，完整 PaymentEngine / damage matrix / trigger engine 仍未关闭。

仍缺正式产品契约：

- `options/selectableCards/selectableTargets/selectableZones/constraints/defaultAction` 等 typed 字段。
- runtime prompt 里的合法选择、默认动作、constraints 与 typed error details。
- `PAY_COST` 已有阶段 3A 最小 runtime 切片，`ASSIGN_COMBAT_DAMAGE` 已有阶段 3C 最小 runtime / UI 切片，`ORDER_TRIGGERS` 已有阶段 3D 最小 runtime / UI 切片；完整 PaymentEngine / decline / 替代费用 / 额外费用 / 非出牌支付窗口、完整 damage assignment 全规则矩阵、完整 trigger engine / APNAP / battle initial stack、`SPELL_DUEL_ACTION` 的专用交互模型和 E2E 断言点仍缺。
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

当前已有 `ASSIGN_COMBAT_DAMAGE` command/schema 骨架、`CombatDamageAssignmentDto` 和阶段 3C 最小 runtime prompt / UI。阶段 1 已有 `DECLARE_BATTLE` 代表路径、`PromptView.relatedBattleId` 和 `timing.battle/battleResolutions` 摘要，阶段 2 关闭“无正式 schema/无稳定拒绝语义”的子项，阶段 3C 关闭最小 damage assignment window；这些仍不能替代完整全规则伤害分配窗口。正式伤害分配 UI 还需要服务端继续冻结：

- `battleId`、`battlefieldId`、`assigningPlayerId`、进攻/防守身份、关联战斗阶段。
- 可分配总伤害、每个伤害来源、是否同时造成伤害。
- 可被分配单位列表：`objectId`、controller、当前战力、已有伤害、致命阈值、壁垒/后排/不可分配等标记。
- 约束：每个单位 min/max、必须先致命/不能过量、可重复/不可重复、剩余伤害处理、合法默认方案。
- 提交命令：按服务端 schema 提交 `assignments[].sourceObjectId/targetObjectId/damage`，带 `promptId/snapshotTick`。
- 服务端错误：非法分配的具体原因，用于 UI 提示。

前端不得根据单位战力自行判定最终合法分配；只能禁用明显不满足服务端 constraints 的输入。

阶段 3C 前端口径：

- C 已同步 `ASSIGN_COMBAT_DAMAGE` typed command、`CombatDamageAssignmentDto`、`ActionPromptContracts.AssignCombatDamage` 和错误文案。
- C 已在服务端 runtime prompt 出现时，用专用面板展示 `damagePool`、`legalTargets`、`existingDamage`、`lethalDamageThreshold`、`assignmentChoices` / `battleParticipants` / visible metadata，并提交服务端支持的 command。
- C 暂不得本地计算致命/过量/壁垒/后排顺序、不得本地结算伤害结果、不得从 `BattleResolutionState` 反推出可提交 assignments。
- 3C 已关闭最小 prompt + submit + reject + simultaneous commit 子项；完整全规则伤害分配矩阵仍需阶段 4 / 最终验收。

### P0：trigger ordering 窗口缺口

当前已有 `ORDER_TRIGGERS(triggerIds)` command/schema 骨架，并且阶段 3D 已开放最小 runtime prompt / UI。服务端 prompt metadata 包含 `orderingPlayerId`、`orderedTriggerIds`、`triggerIds`、`triggers`、`triggerChoices`、`legalOrderingConstraints`、`triggeredByEventKind`；command 支持 `orderedTriggerIds` 并兼容 `triggerIds`。正式完整 UI 仍需要服务端继续冻结：

- 待排序触发列表：`triggerId`、controller、sourceObjectId、sourceCardNo、触发时机、简短文本、是否可选。
- 排序约束：哪些触发必须相邻、哪些不可调整、默认顺序、是否允许取消可选触发。
- 目的地：加入结算链、立即结算、进入任务队列。
- 提交命令：`orderedTriggerIds` 的最终顺序与可选触发选择。

拖拽排序只能收集用户选择，结算链顺序仍由服务端确认。

阶段 3D 前端口径：

- C 已实现 `ORDER_TRIGGERS` UI：上移 / 下移排序，提交 `orderedTriggerIds`，不在浏览器侧结算触发。
- C 侧 build / smoke / `stage3-preflight.mjs` 已通过。
- C 暂不得实现触发费用拒付、完整 APNAP / 跨控制者复杂排序或 battle initial stack 全规则交互。
- C 不得根据事件日志、卡面文本或本地控制者顺序生成服务端未声明的 trigger id；只能重排服务端 runtime prompt 明确给出的 trigger choices。
- 阶段 4 需要继续冻结完整 `TriggerInstance` / `TriggerBatchPromptView` 字段，并接入 battle initial stack / trigger payment / decline。

### P0：payment plan 窗口缺口

当前已有 `PAY_COST(paymentId,paymentWindow,paymentChoiceIds)` command/schema 骨架，出牌/装配 composer 也能读取部分 `sourceRequirements` 里的费用、可选费用、支付资源；阶段 3A 已关闭 `PAY_COST` 最小 runtime 切片，服务端可以下发一条 pending payment prompt 并接受合法最小支付提交。但这还不是完整 PaymentEngine，也没有覆盖 `DECLINE_PAY_COST`、触发费用拒付、替代/额外费用全族或统一 Quote/Authorize/Commit 生命周期。正式 UI 需要服务端提供稳定支付计划：

- 费用项列表：基础费用、强制额外费用、可选额外费用、触发费用、替代费用、减少/增加费用来源。
- 可用资源来源：法力、符能按特性、可回收/可横置/可消费对象及其贡献。
- 自动支付预览：如果服务端自动支付，需返回支付方案和解释。
- 手动支付约束：是否必须精确支付、是否允许超付、是否可拒绝支付、是否可取消。
- 提交命令：`PAY_COST(paymentId,paymentWindow,paymentChoiceIds)` 已有 skeleton；下一步仍需 `DECLINE_PAY_COST` 或等价拒付语义，并把选择项绑定到服务端 payment plan。

仅靠 `optionalCosts: string[]` 无法表达通用触发费用、拒绝支付、替代费用和自动支付说明。

### P0：cleanup queue 窗口/解释缺口

当前 `StackPanel` 可以读取 `timing.pendingTaskQueue` 并展示前 4 个任务摘要；阶段 3B 允许 C 继续做只读展示和 smoke 断言，但字段仍是 `Record<string, unknown>`，没有正式 UI 契约。正式 UI 需要：

- `phase`、`activeTaskId`、`isBlocking` 的稳定枚举与含义。
- 每个 cleanup task 的 `taskId/kind/reason/relatedObjectIds/relatedBattlefieldId/createdBy/expectedResult/visibility/animationHint`。
- 重要清理结果的事件聚合：摧毁、触发入队、控制权变化、待命移除、战斗身份变化。
- 如果清理中需要玩家选择，必须转换为正式 prompt，而不是只出现在 task queue。

产品 UI 默认不为每次清理弹窗，但必须能解释“服务端正在处理什么”和“为什么普通行动被阻塞”。

阶段 3B 前端口径：

- C 可以展示 `phase/activeTaskId/isBlocking/tasks`、battlefield control/standby/result 摘要和服务端事件。
- C 不得根据单位位置、战力、控制者或待命数量自行裁决控制权、待命是否合法、清理顺序、征服/据守、胜负或下一步窗口。
- 如果 cleanup 中需要玩家选择，前端必须等待正式 prompt；不能从 `pendingTaskQueue` 的说明字段自行合成 command。
- 3B smoke 需要记录 reload/reconnect 后 battlefield result 和 task view 是否仍来自 snapshot，而不是只存在事件日志。

### P0：battlefield / standby / control / conquer 展示契约缺口

阶段 3B 已把 battlefield / standby / control / conquer lifecycle 作为当前最小官方化切片。DevUi 目前可以展示 `snapshot.lanes.battlefields`、`controllerId`、`occupantControllerIds`、`standbyObjectIds`、`faceDownStandbyCount`、`pendingTaskKinds`、`timing.battlefieldResolutions` 等字段，但这些字段仍需要正式契约化：

- `BattlefieldView`：`battlefieldObjectId/cardNo/controllerId/status/occupantControllerIds/unitObjectIds/standbyObjectIds/faceDownStandbyCount/pendingTaskKinds/recentResolutionIds`。
- `BattlefieldResolutionView`：`resolutionId/kind/battlefieldObjectId/previousControllerId/newControllerId/scoringPlayerId/scoreDelta/triggerIds/eventIds/createdAtTick`。
- `StandbyView`：公开待命对象、面朝下数量、失控待命 task、可见性原因，且不能给非控制者泄漏 cardNo。
- `CleanupTaskView`：`taskId/kind/reason/relatedObjectIds/relatedBattlefieldId/createdBy/visibility/isBlocking/expectedResult`。

前端可以用这些字段做高亮、摘要、tooltip 和 smoke 断言；规则结论仍必须由服务端事件和 snapshot 确认。

### P0：spell duel / battle lifecycle 缺口

当前顶栏读取 `timing.spellDuel.focusPlayerId`，`StackPanel` 读取 `battleResolutions/battlefieldResolutions`，`DECLARE_BATTLE` composer 可声明战斗；阶段 1 已有 `spellDuelId/battleId` 及相关 `PromptView` 关联字段，但缺少完整生命周期契约：

- `spellDuelId/battleId`、关联战场、触发原因、进入/关闭时间点。
- 法术对决状态：普通/法术对决、开环/闭环、焦点玩家、上次让过玩家、可响应窗口、关闭条件。
- 战斗状态：进攻方/防守方、参战单位、战斗前法术对决、伤害分配阶段、同时造成伤害、战斗清理、结果。
- 与 prompt 的关联：`SPELL_DUEL_ACTION`、`PASS_FOCUS`、`DECLARE_BATTLE`、`ASSIGN_COMBAT_DAMAGE` 应能通过 `relatedBattlefieldId/battleId/stackItemId` 串起来。
- 前端展示用 summary：当前等待谁、为什么等待、下一步可能进入什么窗口。

没有这些字段，正式 UI 只能显示碎片化状态，无法做战场高亮、焦点提示、战斗阶段条和可靠 E2E 断言。

阶段 3C 前端口径：

- C 可以继续只读展示 `timing.spellDuel`、`timing.battle`、`timing.battleResolutions` 与服务端 prompt 关联字段。
- C 不得根据事件日志或 battle resolution 自行推进 battle phase、关闭法术对决、生成 `PASS_FOCUS` / `DECLARE_BATTLE` / `ASSIGN_COMBAT_DAMAGE` 命令。
- 3C 需要服务端正式冻结 `SpellDuelView`、`BattleView`、`BattleResolutionView` 与 `DamageAssignmentPromptView` 字段后，前端才可做专用交互和阶段条。
- 当前 `p2-preflight-spell-duel-pass-focus-closes-window` 已确认存在，且 3C 已补 close -> damage assignment -> battle cleanup/control update 代表证据；完整 `SPELL_DUEL_ACTION`、battle phase 和正式 DTO 仍留阶段 4。

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
- 历史第二轮仅预留 `SPELL_DUEL_ACTION`、`PAY_COST`、`ORDER_TRIGGERS`、`ASSIGN_COMBAT_DAMAGE` prompt type；其中阶段 2 B 已为后三者补 command/schema skeleton，后续阶段 3A/3C 分别补了 `PAY_COST` 与 `ASSIGN_COMBAT_DAMAGE` 最小 runtime。
- DevUi 已同步 TypeScript 类型，可读取 `prompt.view.type`；`ActionPanel` 与 `MatchTopBar` 已开始消费该视图。
- 本轮仍不实现 damage assignment 状态机、payment engine、trigger ordering 状态机，也不补完整 battle/spell duel 生命周期。

## 7. 第二轮 DevUi PromptView 消费记录

- `ActionPanel` 已优先展示服务端 `prompt.view.title/message/type`；旧 prompt 没有 view 时继续显示原有 reason 文案和候选行动。
- `relatedBattlefieldId`、`relatedStackItemId` 仅作为轻量关联文本展示，不在前端推导战斗、结算或规则结果。
- `MatchTopBar` 当前没有接收 prompt；为遵守本轮写入范围，未改 `MatchPage` 传参链路。

## 8. 第二轮 battle/spellDuel 关联 ID 记录

- 后端已为 `snapshot.timing.spellDuel` 追加 `spellDuelId` 与 `battlefieldObjectId`，为 `snapshot.timing.battle` 追加 `battleId`。
- `timing.battlefieldTasks` 的 `START_SPELL_DUEL` / `START_BATTLE` 任务已分别带 `spellDuelId` / `battleId`。
- `PromptView.relatedSpellDuelId` 已在法术对决焦点窗口填充；`PromptView.relatedBattleId` 已在声明战斗窗口填充。
- 历史第二轮仍未开放 runtime `ASSIGN_COMBAT_DAMAGE` prompt；阶段 3C 已补最小 runtime prompt/UI，但完整战斗仍未从 `DECLARE_BATTLE` 同步代表路径全面拆成多阶段状态机。

## 9. 第三轮 DevUi 顶栏 PromptView 消费记录

- `MatchPage` 已把当前 `prompt` 传入 `MatchTopBar`。
- `MatchTopBar` 已显示服务端 `prompt.view.title/type`，用于快速确认当前服务端窗口。
- 顶栏只展示服务端窗口状态，不在前端推导合法行动、费用、战斗或结算结果。

## 10. 第三轮复杂 PromptType 预留记录

- C#/TS 契约均已预留 `SPELL_DUEL_ACTION`、`ASSIGN_COMBAT_DAMAGE`、`PAY_COST`、`ORDER_TRIGGERS` prompt type；阶段 2 B 已追加 `PAY_COST`、`ASSIGN_COMBAT_DAMAGE`、`ORDER_TRIGGERS` command/schema skeleton 和 `ActionPromptContractDto` 类型。
- 本轮历史记录只稳定正式 UI 后续要识别的窗口名称与后三者的命令骨架；其中 `PAY_COST` 最小 runtime 已在阶段 3A 被后续工作替代，`ASSIGN_COMBAT_DAMAGE` 最小 runtime 已在阶段 3C 被后续工作替代，`ORDER_TRIGGERS` 最小 runtime / UI 已在阶段 3D 被后续工作替代；前端仍不得本地裁决触发效果或结算结果。
- 已验证：Contracts build 通过、DevUi build 通过、后端 conformance 3308/3308 通过、`git diff --check` 通过。

## 11. 第三轮 prompt 戳过期保护记录

- DevUi `GameCommand` 已支持可选 `promptId/snapshotTick`，`useMatchController.submitCommand` 会把当前服务端 `ActionPromptDto` 的戳附到命令上；错误文案已覆盖 `PROMPT_EXPIRED`。
- 服务端 `MatchSession.SubmitAsync` 会在进入规则引擎前检查这些可选字段；若客户端提交的 prompt 戳已过期，返回 `PROMPT_EXPIRED`。
- 旧客户端不传 `promptId/snapshotTick` 时仍走原有规则检查路径，保证兼容。
- 本轮历史记录只处理过期窗口错误语义；阶段 2 B 后续已补 `PAY_COST` / `ASSIGN_COMBAT_DAMAGE` / `ORDER_TRIGGERS` command/schema skeleton。阶段 3A 已补 `PAY_COST` 最小 runtime，阶段 3C 已补 `ASSIGN_COMBAT_DAMAGE` 最小 runtime，阶段 3D 已补 `ORDER_TRIGGERS` 最小 runtime / UI；完整 PaymentEngine、完整 damage assignment 全规则矩阵与完整 trigger engine 仍未实现。

## 12. 第三轮复杂 PromptView 降级渲染记录

- `ActionPanel` 已为 `PAY_COST`、`ORDER_TRIGGERS`、`ASSIGN_COMBAT_DAMAGE`、`SPELL_DUEL_ACTION` 增加通用服务端选项面板。
- 面板只展示候选来源、目标、位置、模式、费用和安全 metadata 摘要；不根据这些字段本地裁决规则，也不生成服务端未提供的命令。
- 该渲染用于正式复杂 prompt 接入前的安全降级；阶段 2 B 已补后三类命令骨架，阶段 3A 已补 `PAY_COST` 最小 runtime，阶段 3C 已补 `ASSIGN_COMBAT_DAMAGE` 最小 runtime，阶段 3D 已补 `ORDER_TRIGGERS` 最小 runtime / UI。完整手动支付 / decline / PaymentEngine、伤害分配全规则矩阵、完整 trigger engine 与 battle initial stack 仍是 P0 缺口。
- 已验证：DevUi build 通过。

## 13. 阶段 1 D 汇总

完成项：

- 新增 `docs/CURRENT_STAGE1_PROTOCOL_BASELINE.md`，记录 `SnapshotDto`、`ActionPromptDto`、`PromptViewDto`、`ActionPromptCandidateDto`、`GameEvent`、`ErrorDto` 的真实字段。
- 明确当前没有独立 `MatchSnapshot`、`LegalAction`、`RoomState`、`GameLogEntry`、`ActionError` DTO，避免后续任务引用不存在的协议名。
- 本文件已区分“复杂 prompt 降级展示已做/正在做”和“正式 runtime payload/command/专用交互仍缺”；阶段 2 后续已进一步关闭后三类复杂命令的 schema skeleton 子项。
- 已把阶段 1 服务端 P0 阻断映射到前端契约视角：ActionPrompt/command/payload、snapshot/visibility、Payment/trigger/damage assignment、battle/spellDuel lifecycle。

本轮修改/新增文件：

- `docs/CURRENT_STAGE1_PROTOCOL_BASELINE.md`
- `docs/CURRENT_FRONTEND_CONTRACT_GAPS.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`

关闭/仍存在 P0/P1：

- 本轮 D 只做文档与规则证据审计，没有关闭新的功能 P0/P1。
- 已关闭口径风险：prompt 戳过期保护、复杂 prompt 降级展示、`PromptView` 最小入口不应继续被描述为“完全没有”。
- 仍存在 P0：复杂 prompt runtime payload/状态机中除 `PAY_COST`、`ASSIGN_COMBAT_DAMAGE`、`ORDER_TRIGGERS` 最小切片外的完整 PaymentEngine / `DECLINE_PAY_COST`、`ASSIGN_COMBAT_DAMAGE` 全规则矩阵、完整 trigger engine / APNAP / battle initial stack、battle/spellDuel 完整生命周期、snapshot/visibility typed contract、正式 18 步 E2E。
- 仍存在 P1：layer/effect explanation、typed error details、stack/timing/object state 的长期稳定字段。

文档风险：

- 阶段 1 当轮 `docs/CURRENT_SERVER_RULE_AUDIT.md` 与 `docs/rules-evidence-index.md` 按锁不修改；阶段 2 后续已由 D/E/A 按新写入锁继续同步。
- 当前基线来自源码字段审计，不等同于官方规则完成度证明；READY 判断仍必须等服务端规则、前端 E2E、证据索引一起清零。

## 14. 阶段 2 B 契约骨架补同步记录

完成项：

- 已同步 B 新增的 `INVALID_PAYLOAD`、`PAY_COST`、`ASSIGN_COMBAT_DAMAGE`、`ORDER_TRIGGERS`、`ActionPromptContractDto` / `ActionPromptContracts` 口径。
- 已把“无正式 schema / 无稳定拒绝语义”从“真实 runtime 未完成”里拆开：前者已关闭，后者仍是 P0。
- 已明确 C 可同步 TS 类型、错误文案和调试展示，但不得实现手动支付、伤害分配、触发排序等复杂专用交互。

关闭/仍存在 P0：

- 已关闭子项：后三类复杂命令没有稳定 command 名称、payload 字段名、malformed payload error code。
- 仍存在 P0：`PAY_COST` 最小 runtime 之外的完整 PaymentEngine / decline / 替代费用、`ASSIGN_COMBAT_DAMAGE` 最小 runtime 之外的全规则矩阵、`ORDER_TRIGGERS` 最小 runtime 之外的完整 trigger engine / APNAP / battle initial stack / trigger payment、`SPELL_DUEL_ACTION` runtime lifecycle、复杂 prompt E2E。

文档风险：

- 当前文档反映的是契约骨架与阶段 3A/3B 的最小切片，不代表 Core 已开放完整 runtime prompt。后续如果 B 改动 `ActionPromptContracts` 字段名，D 需要同步本文件、`CURRENT_STAGE2_P0_CONTRACT_PLAN.md` 和 `CURRENT_RULE_EVIDENCE_TODO.md`。

## 15. 阶段 3B 前端契约补同步记录

完成项：

- 已把 `docs/CURRENT_STAGE3B_PLAN.md` 纳入本文件依据。
- 已补阶段 3B 口径：battlefield / standby / control / conquer / cleanup queue 只能作为服务端只读展示和 smoke 断言来源。
- 已把 `BattlefieldView`、`BattlefieldResolutionView`、`StandbyView`、`CleanupTaskView` 的正式字段草案列为 P0 契约缺口。

关闭/仍存在 P0/P1：

- 可候选关闭：前端能展示服务端已给出的 battlefield / standby / pending task / battlefield result 摘要，不在本地裁决规则。
- 仍存在 P0：正式 DTO 未冻结、cleanup 中需要选择时必须转 prompt、双窗口隐藏信息和 reload/reconnect 仍需 3B smoke 证据。
- 仍存在 P1：DevUi 字段较多仍为 `Record<string, unknown>`，后续产品 UI 需要稳定枚举和解释字段。

## 16. 阶段 3C 前端契约补同步记录

完成项：

- 已把 `docs/CURRENT_STAGE3C_SPELL_DUEL_BATTLE_DAMAGE_EVIDENCE.md` 纳入本文件依据。
- 已补阶段 3C 口径：spell duel / battle / `ASSIGN_COMBAT_DAMAGE` / battle cleanup 只能作为服务端 prompt/snapshot 驱动的只读展示或安全降级来源。
- 已明确 `ASSIGN_COMBAT_DAMAGE` 的 3C 最小 runtime prompt、damage pool、constraints、submit/reject 和前端 UI 已关闭；完整全规则伤害分配矩阵仍未关闭。
- 已明确 `p2-preflight-spell-duel-pass-focus-closes-window` 已用 `rg` 确认存在，3C 专项 close -> damage assignment -> battle cleanup/control update 已由 B/C 补证据。

关闭/仍存在 P0/P1：

- 可候选关闭：C 可同步 `SpellDuelState`、`BattleState`、`BattleResolutionState`、`AssignCombatDamageCommand` 和 `ActionPromptContracts.AssignCombatDamage` 的类型与只读展示。
- 仍存在 P0：正式 `SPELL_DUEL_ACTION`、完整 battle phase、完整 `ASSIGN_COMBAT_DAMAGE` 全规则矩阵、battle cleanup 选择窗口和最终 18 步 E2E 仍未关闭。
- 仍存在 P1：`SpellDuelView`、`BattleView`、`BattleResolutionView`、`DamageAssignmentPromptView` 的正式 DTO 尚未冻结，当前字段仍偏 DevUi / dictionary view。

## 17. 阶段 3D 第三阶段收口记录

完成项：

- 已把 `docs/CURRENT_STAGE3_COMPLETION_AUDIT.md` 纳入本文件依据。
- 已明确第三阶段 preflight / smoke 不是最终验收版 18 步 E2E，前端不得据此宣称产品 READY。
- 已明确 3A 关闭前端外壳安全接线，3B 关闭 battlefield / cleanup 只读展示最小切片，3C 关闭 damage assignment 最小 UI 切片，3D 关闭 `ORDER_TRIGGERS` 最小 UI / preflight 子项。
- 已记录 C 侧 `ORDER_TRIGGERS` UI：上移 / 下移排序，提交 `orderedTriggerIds`，不本地结算；build / smoke / `stage3-preflight.mjs` 通过。

仍存在 P0/P1：

- P0：完整 trigger engine、完整 effect resolution、APNAP / 跨控制者复杂排序、trigger cost / decline / payment 和 battle initial stack 全规则仍未开放。
- P0：完整 `SPELL_DUEL_ACTION`、battle phase、control freeze/release、cleanup queue 全触发面和 full damage assignment matrix 仍需服务端先冻结。
- P0：最终 18 步 E2E 与双窗口隐藏信息三层断言仍未完成。
- P1：正式 snapshot/prompt DTO、规则解释字段、产品 UI polish 和可访问性仍需阶段 4 / 最终验收。
- 第三阶段 A final validation 已通过；前端侧确认 3D 最小 `ORDER_TRIGGERS` UI 子项关闭，第三阶段可判定 DONE，但项目仍 NOT READY。

阶段 4 前端建议：

- 可以进入阶段 4 的前端联调，但必须继续以服务端 snapshot/prompt 为唯一规则权威。
- 不允许进入 1009 全量卡牌 UI 验收或最终 18 步 E2E，除非 A/用户单独开启。
