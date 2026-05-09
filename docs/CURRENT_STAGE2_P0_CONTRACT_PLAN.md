# 阶段 2 P0 规则证据链与契约计划

更新日期：2026-05-09
当前 HEAD：`bc0872d`
阶段 1 基线提交：`78b6896`
结论：**NOT READY**

本文只记录 D 文档/规则证据审计结论，不要求本轮修改功能代码。阶段 2 的核心判断是：阶段 1 协议壳、复杂 prompt 安全降级展示和 B 的首版 command/schema skeleton 可以作为联调基础，但剩余 P0 必须先形成服务端权威状态机、runtime prompt/command/payload 和对应规则证据链，才能进入 READY 验收。

## superseded 口径

- 0/负战力自动死亡：已被阶段 1 B 修复与 A 验收替代。历史中出现 `DESTROY_ZERO_POWER_UNIT` / `ZERO_POWER` / `Power <= 0` 自动清理的描述只能作为旧冲突证据，不再表示当前行为。
- 具体战场 objectId 大小写：已被阶段 1 B 修复与 A 验收替代。历史中“具体战场移动大小写风险”保留为防回归，不再列为未清零 P0。
- replay / final hash：历史“仍缺严格 action-log replay final-state 校验”的说法已被当前 `P1-004` 状态替代。当前已具备 representative final hash verifier、recovery 前审计和 Postgres smoke；剩余风险是全命令、全恢复、全随机路径 property 覆盖不足。
- 复杂 prompt 降级展示：阶段 1 已完成安全降级展示与 `promptId/snapshotTick` 过期保护。历史“完全没有复杂 prompt 入口”的说法 superseded。
- 复杂 prompt 契约骨架：阶段 2 B 已新增正式 command/schema 与 malformed payload 稳定拒绝语义。历史“完全没有 `PAY_COST` / `ASSIGN_COMBAT_DAMAGE` / `ORDER_TRIGGERS` command 或 payload 字段名”的说法 superseded；但真实 runtime prompt、状态机和专用交互仍是 P0。

## 阶段 2 B 契约骨架补同步

B 已在服务端协议层新增：

- `ErrorCodes.InvalidPayload = "INVALID_PAYLOAD"`。
- `CommandTypes.PayCost = "PAY_COST"`、`CommandTypes.AssignCombatDamage = "ASSIGN_COMBAT_DAMAGE"`、`CommandTypes.OrderTriggers = "ORDER_TRIGGERS"`。
- `PayCostCommand(PaymentId, PaymentWindow, PaymentChoiceIds)`。
- `CombatDamageAssignmentDto(SourceObjectId, TargetObjectId, Damage)`。
- `AssignCombatDamageCommand(BattleId, BattlefieldId, Assignments)`。
- `OrderTriggersCommand(TriggerIds)`。
- `ActionPromptContractDto(PromptKind, CandidateAction, RequiredPayload, LegalChoices, ValidationErrors, VisibleMetadata, HiddenMetadata)` 与 `ActionPromptContracts`。

`ActionPromptContracts` 当前声明的三张复杂契约：

| PromptKind / CandidateAction | RequiredPayload | LegalChoices | ValidationErrors | VisibleMetadata | HiddenMetadata |
| --- | --- | --- | --- | --- | --- |
| `PAY_COST` | `paymentId`、`paymentWindow`、`paymentChoiceIds` | `candidate.metadata.paymentChoices`、`candidate.metadata.paymentResourceChoices` | `INVALID_PAYLOAD`、`PHASE_NOT_ALLOWED`、`INSUFFICIENT_COST` | `paymentId`、`paymentWindow`、`cost`、`paymentChoices`、`paymentResourceChoices` | `serverPaymentState`、`resourceLedgerBeforePayment` |
| `ASSIGN_COMBAT_DAMAGE` | `battleId`、`battlefieldId`、`assignments[].sourceObjectId`、`assignments[].targetObjectId`、`assignments[].damage` | `candidate.metadata.assignmentChoices`、`candidate.metadata.battleParticipants` | `INVALID_PAYLOAD`、`PHASE_NOT_ALLOWED`、`INVALID_TARGET` | `battleId`、`battlefieldId`、`assignmentChoices`、`battleParticipants` | `battleState`、`participantControllerIds`、`damageLedger` |
| `ORDER_TRIGGERS` | `triggerIds` | `candidate.metadata.triggerChoices` | `INVALID_PAYLOAD`、`PHASE_NOT_ALLOWED`、`INVALID_TARGET` | `triggerChoices`、`triggeredByEventKind` | `triggerQueue` |

当前关闭的只是 P0 子项：

- 无正式 command 名称。
- 无正式 payload 字段名。
- malformed payload 没有稳定拒绝语义。

仍未关闭的 P0：

- `PAY_COST` runtime prompt 尚未开放，仍没有 PaymentEngine Quote/Authorize/Commit、pending payment state、拒付路径和统一 rollback。
- `ASSIGN_COMBAT_DAMAGE` runtime prompt 尚未开放，仍没有独立 damage assignment phase、约束状态机和同时造成伤害提交/结算。
- `ORDER_TRIGGERS` runtime prompt 尚未开放，仍没有 trigger batch ordering 状态机、可选触发选择和入结算链确认。
- typed/raw command 现在能被 Core 识别；malformed payload 返回 `INVALID_PAYLOAD`。合法形状且进入“已识别但未实现”的执行点时仍返回 `UNSUPPORTED_COMMAND`；窗口不存在或前置状态不满足时可能先返回 `PHASE_NOT_ALLOWED` / `INVALID_TARGET` 等稳定拒绝，不能视为规则功能完成。

## P0-S2-001 battlefield / standby / control / held / conquer lifecycle

规则依据：

- `CORE-260330` p4-p8 rules 107.2-107.3：战场是公开位置；每处战场有独立待命子区域；待命容量、控制者限制和失控后下次清理移除待命牌都必须建模。
- `CORE-260330` p22-p26 rules 187-189：战场控制权、争夺状态、控制权冻结、技能控制者和费用支付责任必须稳定。
- `CORE-260330` p28-p33 rules 315.2.b.2、319-323：据守在开始阶段发生；清理会处理场地状态、失控战场、待命移除、待发生法术对决和待发生战斗。
- `CORE-260330` p35-p36 rules 344-348：无人控制战场被争夺时，会在清理中开启非战斗法术对决；关闭后可能确立控制权并触发征服。
- `CORE-260330` p77-p78 rules 461-464：战斗结算后判定结果、清除争夺、移除非法待命、建立控制、征服/据守得分。
- `JFAQ-251023` p5-p7 questions 4.1-5.4：战斗/法术对决期间战场控制权冻结；争夺状态清除后的下一次清理才移除失控待命。
- `SOUL-OFAQ-260114` p21：恶意收购类场景可触发非战斗法术对决，且存在控制权冻结特例。
- `SOUL-JFAQ-260114` p4-p5：征服/据守、得分替代和战场得分动作的 FAQ 边界。

当前实现状态：

- 已有 `ObjectLocations`、`BattlefieldStates`、`BattlefieldTasks`、具体战场移动、战场控制/待命 snapshot、部分 battle/control/reconnect smoke。
- 已修复 `BATTLEFIELD:<objectId>` 只规范化 zone、不改 objectId 大小写。
- 仍缺完整 board task model：standby capacity、held/conquer scoring、control freeze/release、contest resolution、standby removal、battlefield trigger owner、conquer/held trigger 都没有统一生命周期。

归属 agent：

- B：实现 board task model 与服务端状态机。
- E：补官方 FAQ 到 fixture 的证据锚点。
- D：维护本文、`CURRENT_SERVER_RULE_AUDIT.md`、`rules-evidence-index.md` 的 P0 状态。
- C：等待正式 snapshot/prompt schema 后再做产品 UI。

下一步建议：

- 先把 `BattlefieldTaskState` 升级为权威 task：`CONTROL_CHECK`、`REMOVE_STANDBY`、`START_SPELL_DUEL`、`START_BATTLE`、`CONQUER_SCORE`、`HELD_SCORE`。
- 每个 task 必须有 `taskId/kind/battlefieldId/controllerBefore/controllerAfter/createdBy/sourceEvent/status/blocksActions`。
- fixture 优先覆盖：空战场争夺、敌方控制战场争夺、恶意收购、失控待命延迟移除、征服得分被替代、据守触发。

## P0-S2-002 cleanup queue

规则依据：

- `CORE-260330` p31-p33 rules 319-324：阶段转换、开闭环转换、待处理/合法项目变化、对象进出场、状态变化、移动完成都会产生清理未决任务；清理中不结算合法项目；清理需重复直到不再改变状态。
- `CORE-260330` p31-p33 rules 323.1-323.13：清理顺序包括胜负、攻防身份、场地状态、致命伤害、开放战场失控、非法待命/未贴附装备召回、待发生法术对决、待发生战斗。
- `JFAQ-251023` p6-p7 questions 5.1-5.2：清理期间不会传递优先行动权或焦点；特殊清理引发的新清理是常规清理，不重复特殊步骤。
- `SOUL-OFAQ-260114` p19-p20：0/负战力不是自动死亡条件；至少 1 点有效伤害后才满足致命伤害清理。

当前实现状态：

- 已有 `PendingTaskQueue`、`PendingCleanupTasks`、`RunStateBasedCleanupLoop`、blocking prompt/command guard；移动、栈结算、战斗伤害、Xerath 技能伤害和部分回合开始伤害会触发状态性清理。
- 0/负战力清理语义已按 FAQ 修正，历史自动死亡口径 superseded。
- 仍缺覆盖所有状态变化的中央持久任务队列；替代效果、控制权变化、所有进出战场、战斗/法术对决任务与胜负检查尚未纳入一个统一 loop。

归属 agent：

- B：统一 `RunCleanupLoop` 与 task queue。
- E：为每类 cleanup task 建最小官方场景。
- D：追踪状态与文档口径。

下一步建议：

- 将所有 command / stack resolve / trigger resolve / move / enter / leave / damage / power change 统一接入 cleanup enqueue。
- `PendingTaskQueue` 不只做 snapshot 视图，而应成为推进规则任务的权威来源。
- 每个 cleanup task 需要可重放事件与测试断言，避免再次出现“局部 helper 已清理但权威位置索引未同步”。

## P0-S2-003 spell duel / battle lifecycle

规则依据：

- `CORE-260330` p26-p28 rules 307-313：普通/法术对决、开环/闭环、优先行动权、焦点共同决定行动窗口。
- `CORE-260330` p33-p36 rules 333-348：HOT FEPR、优先权、让过、法术对决开始、焦点传递、让过焦点关闭法术对决。
- `CORE-260330` p77-p78 rules 454-461：战斗待发生、战斗法术对决、进攻/防守身份、战斗结算链、战斗伤害、战斗清理、战斗结果、控制权确立。
- `JFAQ-251023` p4-p5 questions 3.1-3.3：获得资源技能不传递焦点；初始结算链结束后焦点不转移；有待处理触发时进攻方暂不能使用优先权。
- `JFAQ-251023` p2-p4 questions 2.3-2.4：战斗初始结算链的触发排序、进攻/防守触发每场战斗只触发一次。

当前实现状态：

- 已有 `TurnWindowState`、`SpellDuelState`、`BattleState`、`BattlefieldTaskState`、`relatedSpellDuelId/relatedBattleId` 和法术对决焦点恢复。
- `ResolveDeclareBattle` 仍是同步/代表性战斗片段；`DECLARE_BATTLE` 兼具启动与结算职责，不是官方 battle task 的多阶段推进。
- `SPELL_DUEL_ACTION` 只预留 prompt type，没有正式 action payload；battle/spellDuel 还不能覆盖初始结算链、焦点、pass、swift/reaction、伤害分配、结算、清理、结果的完整生命周期。

归属 agent：

- B：拆 `SpellDuelState` / `BattleState` 为可推进状态机。
- E：补初始结算链、进攻/防守触发、焦点传递 fixture。
- C：等待 `PromptView` 专用字段后做阶段条和操作 UI。
- D：保持 P0 证据链。

下一步建议：

- 先由 cleanup queue 创建 `START_SPELL_DUEL` / `START_BATTLE` 权威任务，再让任务推进状态，而不是让 `DECLARE_BATTLE` 直接结算。
- 建立 `battleId/spellDuelId/window/status/focusPlayerId/priorityPlayerId/passedPlayers/initialStackCreated/result` 的 typed snapshot。
- 将 `PASS_FOCUS`、`PASS_PRIORITY`、swift/reaction play、battle start、battle close 放到同一生命周期 fixture。

## P0-S2-004 damage assignment

规则依据：

- `CORE-260330` p14-p15 rules 142-143：战力、伤害、0/负战力参与战斗伤害计算的边界。
- `CORE-260330` p62-p63 rule 417：分配伤害不是造成伤害；所有伤害分配完毕时才同时造成；战斗伤害来源视为单位。
- `CORE-260330` p77-p78 rule 460：进攻方和防守方按战力总和分配伤害；必须先分配致命伤害；多个同优先级要求时由分配者决定顺序；无法承受伤害的单位不需要分配致命伤害。
- `JFAQ-251023` p7-p10 questions 6.1-6.4：分配伤害与造成伤害的区别、同时造成、伤害来源、壁垒/后排/互斥分配要求。
- `SOUL-OFAQ-260114` p19-p20：负战力单位战斗输出按 0，但真实战力保留。

当前实现状态：

- `ResolveDeclareBattle` 中已有代表性 `BuildBattleDamageAssignmentOrder` 和部分壁垒/后排/同优先级顺序策略。
- 阶段 1 `PromptTypes.AssignCombatDamage` 已预留，DevUi 有安全降级面板。
- 已有 `ASSIGN_COMBAT_DAMAGE` command/schema 骨架、`CombatDamageAssignmentDto` 和 malformed payload 的 `INVALID_PAYLOAD` 拒绝语义。
- 仍没有 runtime `ASSIGN_COMBAT_DAMAGE` prompt、pending battle damage assignment phase、约束状态机和同时造成伤害结算；前端不能让玩家为多单位、多要求、可选/互斥分配做合法选择。

归属 agent：

- B：服务端 `ASSIGN_COMBAT_DAMAGE` runtime prompt、状态机和合法性校验。
- E：补多防守者、壁垒、后排、无法承受伤害、负战力的官方 fixture。
- C：等正式 prompt 后实现专用 damage assignment UI。
- D：维护契约风险。

下一步建议：

- 先新增 battle pending state 中的 damage assignment phase，不要让 `DECLARE_BATTLE` 一步完成伤害。
- runtime prompt payload 至少包含 `battleId/assigningPlayerId/sourceSide/damagePool/targets/constraints/defaultAssignment`。
- 当前 command skeleton 只覆盖 `battleId/battlefieldId/assignments[].sourceObjectId/targetObjectId/damage`；下一步要把候选合法选择、默认分配和 typed illegal reason 绑定到权威 battle state。

## P0-S2-005 PAY_COST / payment windows

规则依据：

- `CORE-260330` p10-p13 rules 131, 135.2.e：卡牌费用、法力费用、符能费用、`[A]` 任意符能、特定特性符能。
- `CORE-260330` p20 rules 162-167：符文产出法力/符能、符文池、回合流程清空。
- `CORE-260330` p39-p42 rules 356-357：确定总费用、强制额外费用、可选额外费用、增费、减费、支付费用、支付步骤中的资源技能。
- `CORE-260330` p52-p55 rules 377, 403-405：主动/触发技能也要完成选择、确定总费用、支付费用、检查合法性；触发式技能费用可在支付步骤拒付。
- `CORE-260330` p61-p67 rules 414, 416：休眠、回收等非标准费用必须能完成才可支付。
- `JFAQ-251023` p2-p4 question 2.5：触发式技能产生费用时，玩家可以拒绝支付，拒付后该技能从结算链移除。
- `SOUL-OFAQ-260114` p1-p4、p19-p21：`[A]`/`[C]`、法盾强制额外费用、回响/急速可选额外费用、装配不是可选额外费用、回响费用按印刷费用等边界。

当前实现状态：

- 已有 `PaymentCostRules.BuildPaymentId`、`BuildCostPaidPayload`、typed `RunePool`、代表性 `COST_PAID` 包络、支付资源动作、出牌/装配/技能/传奇/战场部分费用路径。
- 阶段 1 `PromptTypes.PayCost` 已预留，DevUi 有安全降级面板。
- 已有独立 `PAY_COST` command/schema 骨架和 malformed payload 的 `INVALID_PAYLOAD` 拒绝语义。
- 仍没有 `PAY_COST` runtime prompt、`DECLINE_PAY_COST` command、pending payment state、PaymentEngine Quote/Authorize/Commit、支付失败 typed details 和所有窗口统一 rollback。

归属 agent：

- B：PaymentEngine 与 `PAY_COST` runtime prompt/状态机。
- E：支付/拒付/替代费用/额外费用/资源技能 fixture。
- C：正式支付 UI 等待 typed payload。
- D：维护证据链与 superseded 口径。

下一步建议：

- 第一刀建立服务端 `PaymentPlan` / `paymentPlanId` / `paymentWindow`，把当前 `PAY_COST(paymentId,paymentWindow,paymentChoiceIds)` skeleton 接入真实 pending payment，先覆盖触发技能费用拒付和出牌支付资源动作。
- 统一 Quote/Authorize/Commit，禁止 prompt 与 Core 各算一套费用。
- `COST_PAID` 事件继续保留兼容字段，但正式 UI 应以 `PAY_COST` prompt 和最终 snapshot 为准。

## P0-S2-006 ORDER_TRIGGERS

规则依据：

- `CORE-260330` p52-p55 rules 383.3.d-383.3.e：同一时间多个触发由控制者决定放入结算链顺序；多个玩家同时触发时从回合玩家开始按回合顺序处理。
- `CORE-260330` p33-p35 rules 333-340：待处理项目进入结算链后走 HOT FEPR，任务期间可以把触发加入结算链但不立即结算合法项目。
- `JFAQ-251023` p2-p4 questions 2.2-2.3：同时触发排序、战斗初始结算链中进攻/防守触发排序有特殊规则，防守方触发始终在最后。
- `JFAQ-251023` p2-p4 question 2.5：触发式技能可能产生费用，排序后仍需进入确认/支付/合法性流程。

当前实现状态：

- 当前有 `TRIGGER_QUEUED` / `TRIGGER_RESOLVED` 事件和部分 `triggerQueue` snapshot。
- 阶段 1 `PromptTypes.OrderTriggers` 已预留，DevUi 有安全降级面板。
- 已有 `ORDER_TRIGGERS` command/schema 骨架和 malformed payload 的 `INVALID_PAYLOAD` 拒绝语义。
- 仍没有 runtime `ORDER_TRIGGERS` prompt、trigger batch ordering 状态机、可选触发选择、拖拽排序 schema 或战斗初始结算链排序规则。

归属 agent：

- B：触发队列与 `ORDER_TRIGGERS` runtime prompt/状态机。
- E：同时触发、跨玩家触发、战斗初始触发、触发费用拒付 fixture。
- C：正式排序 UI 等待 typed payload。
- D：维护证据链。

下一步建议：

- 先实现 `TriggerInstance`：`triggerId/controllerId/sourceObjectId/sourceCardNo/timing/rulesAnchor/isOptional/paymentRequired/defaultOrder`.
- 由 cleanup / battle / stack resolve 产生 trigger batch，进入 `ORDER_TRIGGERS` prompt。
- 当前 command skeleton 提交有序 `triggerIds`；下一步需要补 optional trigger choices、控制者分批规则和服务端确认后再入结算链。

## 阶段 2 验收门槛

- 每个 P0 必须同时具备：规则证据锚点、服务端状态机、正式 runtime prompt/command/payload、正反 fixture、隐藏信息检查、reconnect/replay 最小证明。
- D 本轮只建立证据链和文档计划，不关闭功能 P0。
- READY 判断仍应由 A 在 B/C/E 验收证据全部到位后统一完成。
