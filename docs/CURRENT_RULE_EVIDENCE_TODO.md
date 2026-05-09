# 当前规则证据 TODO

更新时间：2026-05-10
结论：**NOT READY**

本文记录 E 证据/审计 worker 第一轮 P0 交接项、阶段 1 D 协议审计、阶段 2 D P0 规则证据链和 A 主控验收结果，不替代 `docs/CURRENT_SERVER_RULE_AUDIT.md`。

当前 HEAD / 3B checkpoint：`a74beac`
阶段 1 基线提交：`78b6896`
阶段 2 证据链计划：`docs/CURRENT_STAGE2_P0_CONTRACT_PLAN.md`
阶段 3 核心流程审计：`docs/CURRENT_STAGE3_CORE_FLOW_AUDIT.md`
阶段 3A 完成记录：`docs/CURRENT_STAGE3A_PLAN.md`
阶段 3B 当前计划：`docs/CURRENT_STAGE3B_PLAN.md`
阶段 3C 当前证据：`docs/CURRENT_STAGE3C_SPELL_DUEL_BATTLE_DAMAGE_EVIDENCE.md`
阶段 3D / 第三阶段收口审计：`docs/CURRENT_STAGE3_COMPLETION_AUDIT.md`
阶段 4C-1 触发排序审计：`docs/CURRENT_STAGE4C_BATCH1_TRIGGER_ORDERING_AUDIT.md`
阶段 4C-2 真实触发入队审计：`docs/CURRENT_STAGE4C_BATCH2_REAL_TRIGGER_ENQUEUE_AUDIT.md`
阶段 4C-3 绝念真实触发入队审计：`docs/CURRENT_STAGE4C_BATCH3_LAST_BREATH_ENQUEUE_AUDIT.md`
阶段 4C-4 触发支付 / 拒付审计：`docs/CURRENT_STAGE4C_BATCH4_TRIGGER_PAYMENT_AUDIT.md`
阶段 4C-5 state-based cleanup trigger enqueue 审计：`docs/CURRENT_STAGE4C_BATCH5_STATE_CLEANUP_TRIGGER_AUDIT.md`
阶段 4C-6 Honest Broker cleanup trigger enqueue 审计：`docs/CURRENT_STAGE4C_BATCH6_HONEST_CLEANUP_TRIGGER_AUDIT.md`
阶段 4C-7 Scouting Warhawk trigger enqueue 审计：`docs/CURRENT_STAGE4C_BATCH7_SCOUTING_WARHAWK_TRIGGER_AUDIT.md`
阶段 4C-8 Scouting Warhawk cleanup trigger enqueue 审计：`docs/CURRENT_STAGE4C_BATCH8_SCOUTING_WARHAWK_CLEANUP_TRIGGER_AUDIT.md`
阶段 4C-9 Poro cleanup trigger enqueue 审计：`docs/CURRENT_STAGE4C_BATCH9_PORO_CLEANUP_TRIGGER_AUDIT.md`
阶段 4C-10 Unsung Hero cleanup trigger enqueue 审计：`docs/CURRENT_STAGE4C_BATCH10_UNSUNG_HERO_CLEANUP_TRIGGER_AUDIT.md`
阶段 4C-11 Ghostly Centaur cleanup trigger enqueue 审计：`docs/CURRENT_STAGE4C_BATCH11_GHOSTLY_CENTAUR_CLEANUP_TRIGGER_AUDIT.md`
阶段 4C-12 Resonant Soul cleanup trigger enqueue 审计：`docs/CURRENT_STAGE4C_BATCH12_RESONANT_SOUL_CLEANUP_TRIGGER_AUDIT.md`
阶段 4C-13 Stack destroyed trigger migration 审计：`docs/CURRENT_STAGE4C_BATCH13_STACK_DESTROYED_TRIGGER_MIGRATION_AUDIT.md`

## B 修复验收

### 0/负战力

证据锚点：`SOUL-OFAQ-260114` p19-p20；`CORE-260330` p14-p15 rules 142-143、p31-p33 rules 318-324、p77 rule 460。

验收条目：
- `Power <= 0 && Damage == 0` 的正面场上单位不得暴露 `DESTROY_ZERO_POWER_UNIT` blocking task，不得自动进入废牌堆。
- `Power <= 0 && Damage >= 1` 的正面场上单位应在清理中被摧毁；事件/任务命名不得再表达为“仅因 0 战力死亡”。
- 负战力单位参与战斗时，战斗伤害输出按 0 计算，但对象实际战力值必须保留，用于后续增减计算。
- `Damage == 0` 不是有效伤害，不能触发 0/负战力单位死亡。

### 具体战场 destination 大小写

证据锚点：`data/official/card-catalog.zh-CN.json` 中 `OGN·276a/298`、`OGN·278a/298`、`OGN·293a/298`。

验收条目：
- `BATTLEFIELD:<objectId>` 只规范化 `BATTLEFIELD` zone；冒号后的 objectId/cardNo 大小写逐字保留。
- 小写 `a` 战场应覆盖 prompt destination、submit destination、`ObjectLocations.BattlefieldObjectId`、`BattlefieldStates` snapshot。
- 重连/recovery 后不应出现 `276A`、`278A`、`293A` 之类被上转的 object id。

## 已验收但需防回归

- 0/负战力清理语义：B 已完成代码修复，A 主控已用聚焦测试与后端 full test 验收；后续不应再列为未清零 P0，但需要保留证据锚点与回归测试。
- 具体战场 destination 大小写：B 已完成代码修复，A 主控已用小写 `a` 战场移动聚焦测试验收；后续不应再列为未清零 P0，但需要保留官方卡号与重连/recovery 防回归。

## 阶段 1 D 协议/前端证据汇总

- 已新增 `docs/CURRENT_STAGE1_PROTOCOL_BASELINE.md`，记录当前真实协议字段：`SnapshotDto`、`ActionPromptDto`、`PromptViewDto`、`ActionPromptCandidateDto`、`GameEvent`、`ErrorDto`。
- 已确认当前没有独立 `MatchSnapshot`、`LegalAction`、`RoomState`、`GameLogEntry`、`ActionError` DTO；后续文档和任务不应把这些名称当成已存在协议。
- 已更新 `docs/CURRENT_FRONTEND_CONTRACT_GAPS.md`：复杂 prompt 的通用降级展示已经可作为安全承接路径；阶段 2 B 已补 `PAY_COST` / `ASSIGN_COMBAT_DAMAGE` / `ORDER_TRIGGERS` command/schema skeleton 与 `INVALID_PAYLOAD`；阶段 3A 已补 `PAY_COST` 最小 runtime，阶段 3C 已补 `ASSIGN_COMBAT_DAMAGE` 最小 runtime。除这些最小切片外，完整 runtime 状态机、专用交互和错误 details 仍是 P0。
- 阶段 1 文档结论仍是 **NOT READY**；D 本轮没有关闭新的功能 P0/P1，只关闭了“文档描述不准”的口径风险。

## 阶段 2 D P0 规则证据链汇总

本轮 D 已新增 `docs/CURRENT_STAGE2_P0_CONTRACT_PLAN.md`，并把同一套 P0 证据链同步到 `docs/CURRENT_SERVER_RULE_AUDIT.md` 与 `docs/rules-evidence-index.md`。

| P0 | 规则依据 | 当前状态 | 归属 agent | 下一步 |
| --- | --- | --- | --- | --- |
| battlefield / standby / control / held / conquer lifecycle | `CORE-260330` rules 107.2-107.3, 187-189, 315.2.b.2, 319-323, 344-348, 461-464；`JFAQ-251023` q4.1-5.4；`SOUL-OFAQ-260114` p21；`SOUL-JFAQ-260114` p4-p5 | `ObjectLocations`、`BattlefieldStates`、`BattlefieldTasks`、具体战场移动已有；完整 control freeze/release、standby removal、held/conquer scoring 未完成 | B 主实现；E 证据 fixture；C 等 schema；D 文档 | 建 board task model，覆盖控制检查、待命移除、征服/据守得分 |
| cleanup queue | `CORE-260330` rules 319-324；`JFAQ-251023` q5.1-5.2；`SOUL-OFAQ-260114` p19-p20 | `PendingTaskQueue`、`PendingCleanupTasks`、`RunStateBasedCleanupLoop`、blocking guard 已有；未覆盖全部状态变化和替代/控制权任务 | B 主实现；E 场景证据；D 文档 | 所有 command/stack/trigger/move/enter/leave/damage/power change 统一 enqueue cleanup |
| spell duel / battle lifecycle | `CORE-260330` rules 307-313, 333-348, 454-461；`JFAQ-251023` q2.3-q2.4, q3.1-q3.3 | `SpellDuelState`、`BattleState`、关联 id 和焦点恢复已有；`DECLARE_BATTLE` 仍是同步代表路径 | B 主实现；E 初始链/焦点/触发 fixture；C 等 typed prompt；D 文档 | 由 cleanup queue 创建并推进 spell duel / battle task |
| damage assignment | `CORE-260330` rules 142-143, 417, 460；`JFAQ-251023` q6.1-q6.4；`SOUL-OFAQ-260114` p19-p20 | 阶段 3C 已补最小 `ASSIGN_COMBAT_DAMAGE` runtime prompt、damagePool/legalTargets、合法/非法/stale、零副作用拒绝和 simultaneous commit；完整壁垒/后排/负战力/不可分配矩阵仍缺 | B 主实现；E 多单位/壁垒/后排 fixture；C 仅同步类型/调试展示；D 文档 | 后续扩展完整 damage assignment constraints 和 battle task |
| `PAY_COST` / payment windows | `CORE-260330` rules 131, 135.2.e, 162-167, 356-357, 377, 403-405, 414, 416；`JFAQ-251023` q2.5；`SOUL-OFAQ-260114` p1-p4, p19-p21 | `PaymentCostRules`、typed `RunePool`、代表性 `COST_PAID` 已有；阶段 2 B 已补 `PAY_COST` command/schema skeleton 与 `INVALID_PAYLOAD`；阶段 3A 已补最小 pending payment prompt/submit；阶段 4C-4 已补 `SFD·220/221` `TRIGGER_PAYMENT` 支付 / 拒付 / 支付失败 no-mutation 代表路径；完整 PaymentEngine、替代/额外费用、非出牌支付窗口仍缺 | B 主实现；E 支付/拒付 fixture；C 仅同步类型/调试展示；D 文档 | 建 `PaymentPlan/paymentPlanId/paymentWindow` 与 Quote/Authorize/Commit，并扩到更多 triggered-cost FUs |
| `ORDER_TRIGGERS` / trigger payment | `CORE-260330` rules 318-324, 333-340, 377, 383.3.d-383.3.e, 403-405；`JFAQ-251023` q2.2-q2.3, q2.5 | 阶段 3D 已补最小 runtime window / UI / evidence；阶段 4C-1 已补保守 APNAP controller-block 子集、battle initial stack 代表路径和 face-down standby source 脱敏；阶段 4C-2 已补 Watchful Sentinel 多触发真实 `UNIT_DESTROYED` -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority -> draw 代表路径；阶段 4C-3 已补 Honest Broker 遗言金币真实 `UNIT_DESTROYED` -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority -> `EQUIPMENT_TOKEN_CREATED` 代表路径，以及非法排序 no mutation 复核；阶段 4C-4 已补 `SFD·220/221` trigger payment / decline / payment failure no-mutation 代表路径；阶段 4C-5 已补 state-based cleanup `LETHAL_DAMAGE` -> visible Watchful Sentinel last-breath enqueue 代表路径；阶段 4C-6 已补 state-based cleanup `LETHAL_DAMAGE` -> visible Honest Broker last-breath enqueue 代表路径；阶段 4C-7 已补 Scouting Warhawk explicit destroy -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority -> `RUNES_CALLED` 代表路径；阶段 4C-8 已补 Scouting Warhawk state-based cleanup `LETHAL_DAMAGE` -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority -> `RUNES_CALLED` 代表路径；阶段 4C-9 已补 Sad / Loyal Poro state-based cleanup 条件抽牌 -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority -> `CARD_DRAWN` 代表路径；阶段 4C-10 已补 Unsung Hero state-based cleanup powerful draw-2 -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority -> `CARD_DRAWN` x2 代表路径，并确认 power < 5 与 hidden / face-down / standby Unsung cleanup 不入队；阶段 4C-11 已补 Ghostly Centaur state-based cleanup friendly-destroyed power +2 -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority -> `POWER_MODIFIED_UNTIL_END_OF_TURN` 代表路径，并确认 hidden / face-down / standby / opponent source 不入队、source 在本轮 cleanup removal set 中保守不入队、同一 source 同一 cleanup pass 最多入队一次；阶段 4C-12 已补 Resonant Soul state-based cleanup first-friendly-destroyed draw -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority -> `CARD_DRAWN` 1 代表路径，并确认 hidden / face-down / standby / opponent source 不入队、source 在本轮 cleanup removal set 中保守不入队、每 owner 每 cleanup pass 只按首次 destroyed unit 生成本批 source set、同回合已记录 destroyed owner 时不入队；阶段 4C-13 已把 Ghostly Centaur 与 Resonant Soul 的 true stack destruction 旧 immediate compatibility 迁移为 real trigger queue / stack / priority 语义，覆盖非 cleanup `UNIT_DESTROYED` -> `TriggerQueue` -> `ORDER_TRIGGERS` 或 single-trigger auto-stack -> `StackItems` -> priority -> Ghostly `POWER_MODIFIED_UNTIL_END_OF_TURN` +2 / Resonant `CARD_DRAWN` 1，并保持 cleanup path 通过 `IsStateBasedCleanupDestroyedEvent` 排除旧 helper 防重复；完整 trigger engine、其他 destroyed / last-breath / friendly-destroyed FUs、Viktor / Kogmaw / Karthus / Undercover Agent、完整“每回合首次”时序、完整同时死亡触发次数、effective power / LayerEngine、temporary modifier、battlefield objectLocation matrix、hidden / face-down 原始触发建模、更多 trigger payment、完整 effect resolution、FAQ regression、1009/811 full-official 仍缺 | B 主实现；E 触发族 / FAQ fixture；C 只提交服务端 prompt；D 文档 | 以 Watchful Sentinel + Honest Broker 两条 last-breath real enqueue、Treasure Pile 触发支付、visible Watchful / Honest cleanup enqueue、Warhawk explicit / cleanup enqueue、Sad / Loyal Poro cleanup enqueue、Unsung Hero cleanup enqueue、Ghostly Centaur cleanup / stack enqueue 和 Resonant Soul cleanup / stack enqueue 代表路径为基线，继续扩其他 destroyed-family / friendly-destroyed FUs、“每回合首次”时序、同时死亡触发次数、effective power / LayerEngine、temporary modifier、battlefield objectLocation 条件矩阵、hidden / face-down trigger policy、更多触发费用拒付、effect resolution 和 FAQ regression |

## superseded / 防误读

- 0/负战力：阶段 1 已修复并由 A 验收；后续只保留防回归，不再列为未清零 P0。
- 具体战场 objectId 大小写：阶段 1 已修复并由 A 验收；后续只保留防回归，不再列为未清零 P0。
- replay/final hash：历史“仍缺严格 action-log replay final-state 校验”口径已被当前 P1-004 状态替代；当前有 representative verifier、恢复前审计和 Postgres smoke，剩余风险是全命令/全恢复/全随机 property。
- 复杂 prompt 降级展示：阶段 1 已完成安全降级与 prompt 戳过期保护；历史“完全没有复杂 prompt 入口”已 superseded。
- 复杂 prompt schema：阶段 2 B 已补 `PAY_COST` / `ASSIGN_COMBAT_DAMAGE` / `ORDER_TRIGGERS` command/schema skeleton 与 malformed payload 稳定拒绝；历史“完全没有正式 schema/稳定拒绝语义”已 superseded。阶段 3A 已补 `PAY_COST` 最小 runtime；阶段 3C 已补 `ASSIGN_COMBAT_DAMAGE` 最小 runtime；阶段 3D 已补 `ORDER_TRIGGERS` 最小 runtime / UI；阶段 4C-1 已补 `ORDER_TRIGGERS` 保守 APNAP controller-block 子集；阶段 4C-2 已补 Watchful Sentinel 真实多触发入队代表路径；阶段 4C-3 已补 Honest Broker 遗言金币真实入队代表路径；阶段 4C-4 已补 `SFD·220/221` trigger payment / decline 代表路径；阶段 4C-5 / 4C-6 已补 state-based cleanup -> visible Watchful / Honest Broker last-breath enqueue 代表路径；阶段 4C-7 已补 Scouting Warhawk explicit destroy real enqueue 代表路径；阶段 4C-8 已补 Scouting Warhawk state-based cleanup lethal damage real enqueue 代表路径；阶段 4C-9 已补 Sad / Loyal Poro state-based cleanup 条件抽牌 real enqueue 代表路径；阶段 4C-10 已补 Unsung Hero state-based cleanup powerful draw-2 real enqueue 代表路径；阶段 4C-11 已补 Ghostly Centaur state-based cleanup friendly-destroyed power +2 real enqueue 代表路径；阶段 4C-12 已补 Resonant Soul state-based cleanup first-friendly-destroyed draw real enqueue 代表路径；阶段 4C-13 已迁移 Ghostly Centaur / Resonant Soul true stack destruction 旧 immediate compatibility 到 real trigger queue / stack / priority 语义。`PAY_COST` 完整 PaymentEngine、`ASSIGN_COMBAT_DAMAGE` 全规则矩阵、`ORDER_TRIGGERS` 完整 trigger engine / 其他 destroyed-family / friendly-destroyed FUs / 完整“每回合首次”时序 / 完整同时死亡触发次数 / effective power 或 LayerEngine / temporary modifier / battlefield objectLocation matrix / hidden 或 face-down 原始触发建模 / effect resolution / 更多 trigger payment / FAQ regression 仍是 P0。
- 阶段 3A OPEN 口径：阶段 3A 已由 A 验收并关闭 smoke 基线、三类复杂命令强类型映射、`PAY_COST` 最小 runtime 和前端外壳安全接线；历史“3A 仍待验证/未完成”表述已 superseded。不得把 3A 关闭误读为 Stage 3、3B 或 READY。

## 阶段 2 B 已关闭的 P0 子项

- `PAY_COST`、`ASSIGN_COMBAT_DAMAGE`、`ORDER_TRIGGERS` 有稳定 command 名称。
- 三类 command 有首版 payload 字段名：`paymentId/paymentWindow/paymentChoiceIds`、`battleId/battlefieldId/assignments[].sourceObjectId/targetObjectId/damage`、`triggerIds`。
- malformed payload 可稳定返回 `INVALID_PAYLOAD`；合法形状且进入“已识别但未实现”的执行点时仍返回 `UNSUPPORTED_COMMAND`，窗口/前置状态不满足时可能先走 `PHASE_NOT_ALLOWED` 或 `INVALID_TARGET` 等拒绝路径。

## 阶段 3 D 对战桌面 / 核心流程证据框架

阶段 3 围绕本地双人 1v1 对战桌面的连续路径做审计：创建 / 加入、卡组、准备、开局、起手、第一回合、召符文、打牌、移动、争夺或结算链或法术对决、结束回合、投降或胜负结算。当前结论仍为 **NOT READY**。阶段 3A 已完成最小 Chrome smoke、三类复杂命令强类型映射、`PAY_COST` 最小 runtime 和前端桌面外壳；正式 18 步 E2E、3B/3C 连续对战流程、完整 P0/P1 清零仍未完成。

### 阶段 3A 范围修正

阶段 3A 已完成。宽阶段 3 审计框架保留为后续总图，不作为 READY 依据。3A 只收口 smoke 基线、三类复杂命令强类型映射、`PAY_COST` 最小 runtime、前端对战桌面外壳安全接线。

3A 暂不进入：最终正式 18 步 E2E、1009 张卡 full-official 覆盖、完整 battle runtime、完整 `ASSIGN_COMBAT_DAMAGE` runtime、`ORDER_TRIGGERS` 完整 trigger engine / APNAP / battle initial stack / trigger payment、完整 battlefield / standby / control / held / conquer lifecycle、完整 PaymentEngine / LayerEngine。3D 已关闭的是 `ORDER_TRIGGERS` 最小 runtime / UI / evidence 子项。

| 3A P0 | 规则 / 契约依据 | 当前状态 | 归属 agent | 下一步 |
| --- | --- | --- | --- | --- |
| 3A-P0-001 Chrome smoke 基线 | A 目标文档阶段 3 / Chrome smoke；`CORE-260330` rules 107-129 | 已关闭：`npm run smoke:chrome -- --start-api` 通过，覆盖 API、DevUi、Chrome headless-CDP 与 7 个基础路由 | C / A / D | 3B 再扩双人连续流程与隐藏信息断言 |
| 3A-P0-002 三类复杂命令强类型映射 | `CommandTypes`、三类 command DTO、`ActionPromptContracts` | 已关闭：三类 JSON command -> typed command mapper 已落地，malformed payload 稳定拒绝，后端 full test 3324/3324 通过 | B / D | 后续 runtime 逐类开放时补对应合法性测试 |
| 3A-P0-003 `PAY_COST` 最小 runtime | `CORE-260330` rules 131、135.2.e、162-167、356-357、377、403-405、414、416；`JFAQ-251023` q2.5 | 已关闭最小切片：pending payment prompt、choices、合法提交、stale/invalid/零副作用测试已通过 | B / E / C / D | 完整 PaymentEngine、decline、替代/额外费用仍是后续 P0 |
| 3A-P0-004 前端外壳不裁决规则 | 服务端权威原则；`CORE-260330` rules 107-129；`ActionPromptDto` / `SnapshotDto` | 已关闭 3A 外壳：只读 snapshot/prompt、只提交服务端候选；未冻结 complex prompt safe fallback | C / D | 正式复杂交互等待服务端 runtime 冻结 |

### 阶段 3B 范围修正

当前只执行阶段 3B：Battlefield / Standby / Control / Conquer lifecycle + Central cleanup queue 最小官方化切片。阶段 3B 不启动最终 18 步 E2E，不进入 1009 张卡全量实现，不扩大到完整 battle/damage/trigger runtime，不提交 `riftbound-dotnet.sln`。

3B 暂不进入：完整 battle / spell duel lifecycle、完整 `ASSIGN_COMBAT_DAMAGE` runtime、`ORDER_TRIGGERS` 完整 trigger engine / APNAP / battle initial stack / trigger payment、完整 PaymentEngine、LayerEngine、全路径 replay/determinism。3D 后最小排序窗口已关闭，完整触发系统仍 P0。

| 3B 规则域 | 规则 / FAQ 入口 | 当前状态 | 归属 agent | 下一步 |
| --- | --- | --- | --- | --- |
| Battlefield / standby zone | `CORE-260330` rules 107.2-107.3；rules 315.2.b.2、319-323 | 具体战场移动、`BattlefieldStates`、待命数量和 task view 已有；完整 standby 时机和全卡族仍缺 | B / C / D | 固化 snapshot 字段和隐藏信息边界 |
| Control / contest / freeze | `CORE-260330` rules 187-189、344-348、461-464；`JFAQ-251023` q4.1-q5.4；`SOUL-OFAQ-260114` p21 | 战后控制代表路径、`BATTLEFIELD_CONTROL_RESOLVED`、battlefield contest smoke 已有；freeze/release 仍缺 | B / E / D | 3B 先收代表控制结算，freeze/release 继续 P0 |
| Held / conquer scoring | `CORE-260330` rules 315.2.b.2、461-464；`SOUL-JFAQ-260114` p4-p5 | `BATTLEFIELD_HELD` / `BATTLEFIELD_CONQUERED` 大量代表 fixture 和 snapshot 结果已有 | B / E / D | 3B 收一条 held 与一条 conquer 代表链；全战场卡留后续 |
| Central cleanup queue | `CORE-260330` rules 319-324；`JFAQ-251023` q5.1-q5.2；`SOUL-OFAQ-260114` p19-p20 | `PendingTaskQueue`、`PendingCleanupTasks`、blocking guard、非法待命/致命伤害/未贴附装备代表任务已有 | B / C / D | 3B 收最小 task view；全触发面 enqueue 仍 P0 |

3B 关闭候选：

- 3B-CAND-001 battlefield/standby snapshot 只读字段。
- 3B-CAND-002 非法待命 cleanup 代表路径与 blocking guard。
- 3B-CAND-003 control / held / conquer 代表结果可 snapshot/reconnect。
- 3B-CAND-004 central cleanup queue 最小 task view。

3B 仍存在 P0/P1：

- 3B-P0-001 cleanup queue 全触发面未完成。
- 3B-P0-002 control freeze/release 未完成。
- 3B-P0-003 delayed illegal standby removal 全时机未完成。
- 3B-P0-004 held/conquer scoring order 和全战场卡未完成。
- 3B-P0-005 3B smoke 证据不是最终 18 步 E2E。
- 3B-P1-001 前端 battlefield / cleanup 字段仍偏 DevUi，正式 DTO 未冻结。

### 阶段 3C 范围修正

当前只执行阶段 3C：spell duel / battle / `ASSIGN_COMBAT_DAMAGE` / battle cleanup 的规则证据和最小官方化候选。阶段 3C 不启动最终 18 步 E2E，不进入 1009 张卡全量实现，不标记 READY，也不回滚或重判 3B battlefield / standby / control / conquer 前置产物。

3C 暂不进入：`ORDER_TRIGGERS` 完整 trigger engine / APNAP / battle initial stack / trigger payment、完整 PaymentEngine / `DECLINE_PAY_COST`、LayerEngine、全路径 replay/determinism、1009 全量卡牌覆盖、最终正式 18 步 E2E。3D 后最小排序窗口已关闭，完整触发系统仍 P0。

| 3C 规则域 | 规则 / FAQ 入口 | 当前实现状态 | 归属 agent | 下一步 |
| --- | --- | --- | --- | --- |
| Spell duel lifecycle | `CORE-260330` rules 307-313、333-348；`JFAQ-251023` q3.1-q3.3 | `SpellDuelState`、`PASS_FOCUS`、焦点恢复、swift/反应 timing context 有代表证据；3C 已补 close -> damage assignment -> cleanup/control 连续测试 | B 主实现；C 只读展示；E fixture；D 审计 | 完整 `SPELL_DUEL_ACTION`、全反应链和触发排序仍留 P0 |
| Battle lifecycle | `CORE-260330` rules 454-461；rules 319-324；`JFAQ-251023` q2.3-q2.4 | `DECLARE_BATTLE` 代表路径、`BattleState`、`BattleResolutionState`、多防守/多参与者代表测试已有；仍偏同步结算片段，不是完整 task | B 主实现；E battle fixture；C 只读展示；D 审计 | 把 START_BATTLE -> battle view -> result/cleanup 串成可审计最小 task |
| Damage assignment | `CORE-260330` rules 142-143、417、460；`JFAQ-251023` q6.1-q6.4；`SOUL-OFAQ-260114` p19-p20 | 3C 已补最小 runtime prompt、validation、stale prompt、zero-side-effect reject 与 simultaneous commit | B 主实现；E 多单位/壁垒/后排/负战力 fixture；C runtime UI；D 审计 | 完整壁垒/后排/同优先级/负战力/不可分配矩阵仍留 P0 |
| Battle cleanup | `CORE-260330` rules 319-324、461-464；`JFAQ-251023` q5.1-q5.2 | 3C 已覆盖 battle damage -> lethal cleanup -> battle close -> battlefield control update | B 主实现；E battle cleanup fixture；C 只读展示；D 审计 | 替代/预防、LayerEngine、control freeze/release 与 cleanup queue 全触发面仍缺 |

3C 关闭候选：

- 3C-CAND-001 spell duel focus/pass/close 的最小 lifecycle 证据；3C 专项 close -> next task 仍需 B 测试补齐。
- 3C-CAND-002 battle view / battle resolution 的最小 task 证据。
- 3C-CAND-003 `ASSIGN_COMBAT_DAMAGE` runtime 最小 prompt。
- 3C-CAND-004 battle cleanup 最小结果链。

3C 仍存在 P0/P1：

- 3C-P0-001 spell duel 完整 lifecycle 未完成：3C 已补 focus/pass/close 代表链；全反应链、复杂 `SPELL_DUEL_ACTION`、触发排序和全部 close -> next task 全路径仍缺。
- 3C-P0-002 battle 完整 lifecycle 未完成：完整 battle task、战斗响应窗口、所有多攻防组合和初始栈仍缺。
- 3C-P0-003 `ASSIGN_COMBAT_DAMAGE` full-rule runtime 未完成：3C 最小 prompt / constraints / submit-reject / simultaneous commit 已落地；完整壁垒/后排/同优先级/负战力/不可分配矩阵仍缺。
- 3C-P0-004 battle cleanup 全路径未完成：3C 已有 battle damage -> cleanup -> control update 代表链；替代/预防、LayerEngine、control freeze/release 与 cleanup queue 全触发面仍缺。
- 3C-P0-005 3C smoke/evidence 不是最终 18 步 E2E。
- 3C-P1-001 前端 `timing.spellDuel/battle/battleResolutions` 仍是 dictionary view，正式 DTO 未冻结。

### 阶段 3D / 第三阶段收口审计

3D 只做文档 / 规则证据 / 第三阶段收口审计，不实现功能代码，不启动最终验收版 18 步 E2E，不进入 1009 张卡全量，不标记 READY。

第三阶段收口：

- 3A 已关闭：Chrome route smoke、三类复杂命令 typed mapper、`PAY_COST` 最小 runtime、前端外壳不裁决规则。
- 3B 已关闭：battlefield / standby snapshot 只读字段、非法待命 cleanup 代表路径、control / held / conquer 代表结果、central cleanup queue 最小 task view。
- 3C 已关闭：spell duel focus/pass/close 代表链、battle view / battle resolution 最小 task、`ASSIGN_COMBAT_DAMAGE` 最小 runtime prompt / submit / reject / simultaneous commit、battle damage -> cleanup -> control update 代表链。
- 3D 关闭：`ORDER_TRIGGERS` 最小 runtime / UI / evidence 子项、第三阶段审计口径、阶段 4 与最终验收边界的文档风险。

3D 证据状态：

| 规则域 | 当前证据状态 | 仍缺口 | 下一阶段 |
| --- | --- | --- | --- |
| priority / focus | `PASS_PRIORITY`、`PASS_FOCUS`、spell duel focus、prompt stamp、stale prompt 代表证据已有 | 完整 `SPELL_DUEL_ACTION`、全反应 / 迅捷 / 反制链、触发排序交织 | 阶段 4 |
| spell duel close | 3C 已有 close -> damage assignment -> cleanup/control update 代表链 | 所有 close -> next task、非战斗法术对决、触发排序 | 阶段 4 |
| battle lifecycle | `BattleState`、`BattleResolutionState`、多攻防代表路径和 3C 最小 damage assignment 已有 | 完整 battle task、initial stack、响应窗口、freeze/release | 阶段 4 |
| damage assignment | 3C 最小 prompt / validation / submit / reject / simultaneous commit 已关闭 | 壁垒、后排、同优先级、负战力、不可分配、替代/预防矩阵 | 阶段 4 |
| battle cleanup | 3C 已有 battle damage -> lethal cleanup -> battle close -> control update | cleanup queue 全触发面、LayerEngine、control freeze/release | 阶段 4 |
| battlefield control update | 3B/3C 有战后 control update 与重连展示代表证据 | 战斗 / 法术对决期间 freeze 与关闭后 release，全控制改变卡 | 阶段 4 |
| conquer / hold scoring | 3B 有 held/conquer 代表路径与 snapshot 结果 | scoring order、全战场卡、得分替代、付费触发拒付、同时触发排序 | 阶段 4 |
| standby visibility | 3B 有 standby / faceDown count / illegal cleanup 代表路径 | 全 standby 卡族、失控待命全时机、freeze 期间不提前移除、正式 DTO | 阶段 4 |
| cleanup queue | 3B 有 central queue 最小 task view、active task、blocking guard | 全 command/stack/trigger/move/enter/leave/damage/power change 统一 enqueue 与 repeat-until-stable audit | 阶段 4 |

`ORDER_TRIGGERS` / 多触发排序：

- 规则入口：`CORE-260330` rules 333-340、383.3.d-383.3.e；`JFAQ-251023` q2.2-q2.3、q2.5；battle initial stack 关联 rules 454-461 与 q2.3-q2.4。
- 已有证据：`ORDER_TRIGGERS(triggerIds)` command/schema skeleton、malformed payload `INVALID_PAYLOAD`、`TRIGGER_QUEUED` / `TRIGGER_RESOLVED` 代表事件、部分 `triggerQueue` view。
- 3D 新增证据：B 已实现最小 runtime window，prompt metadata 包含 `orderingPlayerId/orderedTriggerIds/triggerIds/triggers/triggerChoices/legalOrderingConstraints/triggeredByEventKind`；command 支持 `orderedTriggerIds` 并兼容 `triggerIds`；合法排序清空 `TriggerQueue`、按顺序加入 `StackItems`、设置 priority player，事件 `TRIGGERS_ORDERED` / `TRIGGERS_MOVED_TO_STACK`。
- B 验证：`ConformanceFixtureShapeTests` 109/109 通过；full `dotnet test Riftbound.slnx --no-restore` 3333/3333 通过；`git diff --check` 通过。
- C 已实现 `ORDER_TRIGGERS` UI，上移 / 下移排序，提交 `orderedTriggerIds`，不本地结算；build / smoke / `stage3-preflight.mjs` 通过。
- E 已补 stage3D 矩阵 overlay 和 `ORDER_TRIGGERS` 证据文档。
- 仍缺 P0：完整 trigger engine、完整 effect resolution、真实卡牌全触发生成、完整 APNAP 多玩家独立排序、battle initial stack 全官方规则、trigger cost / decline / payment。
- 阶段 4C-1 已把最小 runtime 推进到 APNAP controller-block 子集；后续继续扩完整 APNAP / 多玩家独立排序、battle initial stack / attacker-defender order、真实卡牌触发生成和触发费用拒付 / PaymentEngine。

阶段 4 / 最终边界：

- 必须进阶段 4：完整 trigger engine / APNAP / battle initial stack / trigger payment、priority/focus 与 `SPELL_DUEL_ACTION`、完整 battle task、damage assignment 全规则矩阵、battle cleanup / control freeze-release、cleanup queue 全触发面、PaymentEngine、正式 snapshot/prompt DTO 与双窗口隐藏信息 smoke。
- 必须留到最终验收：最终正式 18 步 E2E、1009 张卡 full-official 覆盖、LayerEngine / 替代 / 预防全模型、replay/recovery/determinism 全边界、产品 UI polish。
- A 主控 final validation 已通过，第三阶段可判定 **DONE**；可以准备进入阶段 4，但仍 **NOT READY**。

### 阶段 4C-1 触发排序审计

4C-1 证据入口：`docs/CURRENT_STAGE4C_BATCH1_TRIGGER_ORDERING_AUDIT.md`。本节只更新规则证据 / P0-P1 审计，不代表 READY，不代表 1009 full-official。

4C-1 关闭的 P0 子项：

- `ORDER_TRIGGERS` 升级为保守 APNAP controller-block 子集。
- prompt metadata 约定：`orderedTriggerIds` 是合法 APNAP resolution top-first 默认提交顺序，`triggerIds` 是 raw queue order。
- `legalOrderingConstraints` 明确 APNAP policy、top-first semantics、controller block order、legal resolution block order、跨控制者不可重排、同控制者可重排。
- runtime 校验覆盖合法排序 accepted；非法跨控制者重排 rejected 且 no state mutation。
- `ORDER_TRIGGERS` prompt 优先于 `START_BATTLE` / task prompt。
- battle initial stack 代表证据覆盖 active battle attacker / defender 初始触发 -> `ORDER_TRIGGERS` -> stack priority。
- trigger prompt / snapshot 对不可见 face-down standby source 做 viewer 级脱敏。

规则证据入口：

- Trigger ordering / APNAP：`CORE-260330` p33-p35 rules 333-340；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- Trigger payment / decline：`CORE-260330` p52-p55 rules 377, 403-405；`JFAQ-251023` p2-p4 q2.5。
- Battle initial stack：`CORE-260330` p35-p36 rules 341-348；`CORE-260330` p77-p78 rules 454-461；`JFAQ-251023` p2-p4 q2.2-q2.4。
- Hidden information / face-down standby source：`CORE-260330` p4-p8 rules 107-129；待命 / 显露相关 evidence 继续复用 `CORE-260330` p39-p42 rules 355-356；更精确 FAQ 页码暂为 evidence TODO。

验证记录：

- A 后端 full test：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3337/3337。

仍缺 P0/P1：

- P0：完整 trigger engine、完整 effect resolution、真实卡牌全触发生成。
- P0：trigger payment / decline / payment failure 与完整 PaymentEngine 统一。
- P0：完整 APNAP 多玩家独立排序；4C-1 只关闭 controller-block 子集。
- P0：battle initial stack 全官方规则、攻防触发特殊排序、battle response window 与 FAQ 回归。
- P0：最终正式 18 步 E2E、1009 张卡 full-official 覆盖。
- P1：`TriggerInstance` / `TriggerBatchPromptView` / `legalOrderingConstraints` 正式 DTO、产品解释字段、多语言 UI 文案和证据链接。

### 阶段 4C-2 真实触发入队审计

4C-2 证据入口：`docs/CURRENT_STAGE4C_BATCH2_REAL_TRIGGER_ENQUEUE_AUDIT.md`。本节只更新规则证据 / P0-P1 审计，不代表 READY，不代表 1009 / 811 full-official。

4C-2 关闭的 P0 子项：

- 真实 `UNIT_DESTROYED` 路径中，多张 Watchful Sentinel / 《警觉的哨兵》（`CATALOG` OGN·096/298）遗言抽牌触发已接入 `TriggerQueue`。
- 多触发代表路径已串成 `TriggerQueue` -> `ORDER_TRIGGERS` prompt -> `StackItems` -> pass priority -> `TRIGGER_RESOLVED` / `CARD_DRAWN`。
- 单个 Watchful Sentinel 保留即时结算兼容；本批不宣称统一单触发策略。
- 跨控制者 APNAP 默认 `orderedTriggerIds` 可直接提交并 accepted；非法跨控制者排序 rejected 且 no state mutation。
- 未改协议 / 前端。

规则证据入口：

- 真实卡牌触发入队：`CATALOG` OGN·096/298；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- `ORDER_TRIGGERS` / stack / priority：`CORE-260330` p33-p35 rules 333-340；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- Trigger payment / decline：`CORE-260330` p52-p55 rules 377, 403-405；`JFAQ-251023` p2-p4 q2.5。
- State-based cleanup triggers：`CORE-260330` p31-p33 rules 318-324；更精确 FAQ 页码暂为 TODO。

验证记录：

- A focused：11/11 通过。
- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3338/3338。
- A frontend build：passed。
- A Chrome smoke：passed。
- A stage3 preflight：passed。

仍缺 P0/P1：

- P0：完整 trigger engine。
- P0：其他 destroyed-family / friendly-destroyed / attack / conquer 触发族。
- P0：state-based cleanup 触发统一入队。
- P0：trigger payment / decline / payment failure。
- P0：完整 effect resolution 与完整 FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18 步 E2E。
- P1：`TriggerInstance` / `TriggerBatchPromptView` / `legalOrderingConstraints` 正式 DTO、真实触发解释字段、单触发即时结算兼容策略文档化。

### 阶段 4C-3 绝念真实触发入队审计

4C-3 证据入口：`docs/CURRENT_STAGE4C_BATCH3_LAST_BREATH_ENQUEUE_AUDIT.md`。本节只更新规则证据 / P0-P1 审计，不代表 READY，不代表 1009 / 811 full-official。

4C-3 关闭的 P0 子项：

- `HonestBrokerCardNo` / `HONEST_BROKER_LAST_BREATH_CREATE_GOLD` 从直接结算扩展到真实多触发路径。
- Honest Broker / 《诚实掮客》（`CATALOG` SFD·155/221）遗言金币代表路径已串成 `UNIT_DESTROYED` -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` -> `EQUIPMENT_TOKEN_CREATED`。
- 与 4C-2 合并后，Watchful Sentinel / 《警觉的哨兵》（`CATALOG` OGN·096/298）和 Honest Broker / 《诚实掮客》（`CATALOG` SFD·155/221）两条 last-breath 代表路径已有 real enqueue 证据。
- 跨控制者真实 last-breath APNAP 默认顺序可直接提交 accepted；非法跨控制者排序 rejected 且 no state mutation。
- 单触发 Watchful Sentinel / Honest Broker 仍保留即时结算兼容；本批不宣称统一单触发策略完成。

规则证据入口：

- Honest Broker last-breath enqueue：`CATALOG` SFD·155/221；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- Watchful Sentinel last-breath enqueue：`CATALOG` OGN·096/298；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- `ORDER_TRIGGERS` / stack / priority：`CORE-260330` p33-p35 rules 333-340；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- Trigger payment / decline：`CORE-260330` p52-p55 rules 377, 403-405；`JFAQ-251023` p2-p4 q2.5。
- State-based cleanup triggers：`CORE-260330` p31-p33 rules 318-324；更精确 FAQ 页码暂为 TODO。

验证记录：

- A focused：13/13 通过。
- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3339/3339。
- A frontend build：passed。
- A Chrome smoke：passed。
- A stage3 preflight：passed。

仍缺 P0/P1：

- P0：完整 trigger engine。
- P0：其他 destroyed-family、state-based cleanup 触发入队。
- P0：trigger payment / decline / payment failure。
- P0：完整 effect resolution 与完整 FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18 步 E2E。
- P1：`TriggerInstance` / `TriggerBatchPromptView` / `legalOrderingConstraints` 正式 DTO、真实触发解释字段、单触发即时结算兼容策略文档化。

### 阶段 4C-4 触发支付 / 拒付审计

4C-4 证据入口：`docs/CURRENT_STAGE4C_BATCH4_TRIGGER_PAYMENT_AUDIT.md`。本节只更新规则证据 / P0-P1 审计，不代表 READY，不代表 1009 / 811 full-official。

4C-4 关闭的 P0 子项：

- `SFD·220/221`《珍宝堆》征服触发进入服务端权威 `TRIGGER_PAYMENT`。
- `PAY_COST` 支持 `SPEND_MANA:1` 和 `DECLINE` 两个服务端合法选项。
- 支付成功扣 1 点法力并创建休眠“金币”装备指示物；拒付关闭窗口且不扣费、不创建指示物。
- wrong player、stale prompt、unknown choice、duplicate choice、pay+decline、malformed payload、insufficient mana 都拒绝且 no state mutation。

规则证据入口：

- Trigger payment / decline：`CORE-260330` p52-p55 rules 377, 403-405；`JFAQ-251023` p2-p4 q2.5；`CATALOG` SFD·220/221。
- `PAY_COST` runtime validation：`CORE-260330` p39-p42 rules 356-357；p52-p55 rules 403-405。
- Battlefield conquer trigger：`CORE-260330` p77-p78 rules 454-461；`CATALOG` SFD·220/221。

验证记录：

- A focused trigger payment：11/11 通过。
- A trigger ordering regression：13/13 通过。
- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3344/3344。
- A frontend build / Chrome smoke / stage3 preflight：通过。

仍缺 P0/P1：

- P0：完整 PaymentEngine。
- P0：`SFD·220/221` 之外 triggered-cost functional units。
- P0：完整 trigger engine、state-based cleanup trigger enqueue、完整 effect resolution 与完整 FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18 步 E2E。
- P1：`TRIGGER_PAYMENT` 长期 DTO / 解释字段 / UX 契约冻结。

### 阶段 4C-5 State-Based Cleanup Trigger Enqueue 审计

4C-5 证据入口：`docs/CURRENT_STAGE4C_BATCH5_STATE_CLEANUP_TRIGGER_AUDIT.md`。本节只更新规则证据 / P0-P1 审计，不代表 READY，不代表 1009 / 811 full-official。

4C-5 关闭的 P0 子项：

- State-based cleanup `LETHAL_DAMAGE` -> visible Watchful Sentinel last-breath enqueue representative。
- 服务端只接入可见、非 face-down、非 standby 的 Watchful Sentinel / 《警觉的哨兵》（`CATALOG` OGN·096/298）。
- Starfall / 《星落》（`CATALOG` OGN·029/298）造成致命伤害后，state-based cleanup `LETHAL_DAMAGE` 摧毁两名 Watchful，并串成 `TRIGGER_QUEUED` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` / `CARD_DRAWN`。
- hidden / standby Watchful Sentinel 不入队，避免 trigger metadata 泄漏不可见或待命来源。
- 本批不授予 full-official，不扩完整 trigger engine。

规则证据入口：

- State-based cleanup lethal destroy：`CORE-260330` p31-p33 rules 318-324；`CORE-260330` p14-p15 rules 142-143；`CORE-260330` p77 rule 460；`SOUL-OFAQ-260114` p19-p20。
- Watchful Sentinel last-breath enqueue：`CATALOG` OGN·096/298；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- Starfall lethal cleanup representative：`CATALOG` OGN·029/298；`CORE-260330` p14-p15 rules 142-143；`CORE-260330` p39-p42 rules 355-356。
- Hidden / standby source redaction：`CORE-260330` p4-p8 rules 107-129；更精确 FAQ 页码暂为 TODO。

验证记录：

- A focused RealTriggerQueueTests：4/4 通过。
- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3346/3346。
- A frontend build：通过，仅有既有 SignalR / Rollup `PURE` 注释警告。
- A Chrome smoke：通过。
- A stage3 preflight：通过。
- A B-file diff check：`git diff --check -- src/Riftbound.Engine/CoreRuleEngine.cs tests/Riftbound.ConformanceTests/RealTriggerQueueTests.cs` 通过。

仍缺 P0/P1：

- P0：完整 trigger engine。
- P0：其他 destroyed / last-breath / friendly-destroyed functional units。
- P0：隐藏 / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：完整 effect resolution 与完整 FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18 步 E2E。
- P1：trigger batch 正式 DTO、触发来源解释字段、hidden / standby trigger policy 文档化。

### 阶段 4C-6 Honest Broker Cleanup Trigger Enqueue 审计

4C-6 证据入口：`docs/CURRENT_STAGE4C_BATCH6_HONEST_CLEANUP_TRIGGER_AUDIT.md`。本节只更新规则证据 / P0-P1 审计，不代表 READY，不代表 1009 / 811 full-official。

4C-6 关闭的 P0 子项：

- State-based cleanup `LETHAL_DAMAGE` -> visible Honest Broker last-breath enqueue representative。
- 服务端只接入可见、非 face-down、非 standby 的 Honest Broker / 《诚实掮客》（`CATALOG` SFD·155/221）。
- Starfall / 《星落》（`CATALOG` OGN·029/298）造成致命伤害后，state-based cleanup `LETHAL_DAMAGE` 摧毁两个 Honest Broker，并串成 `TRIGGER_QUEUED` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` / `EQUIPMENT_TOKEN_CREATED`。
- hidden / standby Honest Broker 不入队、不创建 token，避免 trigger metadata 泄漏不可见或待命来源。
- 本批不改协议或前端，不授予 full-official，不扩完整 trigger engine。

规则证据入口：

- State-based cleanup lethal destroy：`CORE-260330` p31-p33 rules 318-324；`CORE-260330` p14-p15 rules 142-143；`CORE-260330` p77 rule 460；`SOUL-OFAQ-260114` p19-p20。
- Honest Broker last-breath enqueue：`CATALOG` SFD·155/221；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- Starfall lethal cleanup representative：`CATALOG` OGN·029/298；`CORE-260330` p14-p15 rules 142-143；`CORE-260330` p39-p42 rules 355-356。
- Hidden / standby source redaction：`CORE-260330` p4-p8 rules 107-129；更精确 FAQ 页码暂为 TODO。

验证记录：

- A focused RealTriggerQueueTests：6/6 通过。
- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3348/3348。
- A frontend build：通过，仅有既有 SignalR / Rollup `PURE` 注释警告。
- A Chrome smoke：通过。
- A stage3 preflight：通过。
- A B-file diff check：`git diff --check -- src/Riftbound.Engine/CoreRuleEngine.cs tests/Riftbound.ConformanceTests/RealTriggerQueueTests.cs` 通过。

仍缺 P0/P1：

- P0：完整 trigger engine。
- P0：其他 destroyed / last-breath / friendly-destroyed functional units。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：完整 effect resolution 与完整 FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18 步 E2E。
- P1：trigger batch 正式 DTO、触发来源解释字段、hidden / standby trigger policy 文档化。

### 阶段 4C-7 Scouting Warhawk Trigger Enqueue 审计

4C-7 证据入口：`docs/CURRENT_STAGE4C_BATCH7_SCOUTING_WARHAWK_TRIGGER_AUDIT.md`。本节只更新规则证据 / P0-P1 审计，不代表 READY，不代表 1009 / 811 full-official。

4C-7 关闭的 P0 子项：

- Scouting Warhawk / 《侦察飞鹰》（`CATALOG` OGN·216/298，`FU-0500c77a70`）explicit destroy real trigger enqueue representative。
- Spirit Fire / 《妖异狐火》（`CATALOG` OGN·256/298）作为 explicit destroy source；本批不是 state cleanup。
- explicit destroy `UNIT_DESTROYED` -> visible Scouting Warhawk `SCOUTING_WARHAWK_LAST_BREATH_CALL_RUNE_1` -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` / `RUNES_CALLED`。
- hidden / face-down / standby Warhawk 不入队、不显示 prompt metadata、不触发 `RUNES_CALLED`。
- single-trigger compatibility 保留，既有 `P79ScoutingWarhawk` 测试继续通过。
- 本批没有协议 / 前端字段变化，不授予 full-official，不扩完整 trigger engine。

规则证据入口：

- Scouting Warhawk last-breath enqueue：`CATALOG` OGN·216/298；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- Spirit Fire explicit destroy source：`CATALOG` OGN·256/298；`CORE-260330` p39-p42 rules 355-356；`CORE-260330` p62-p63 rule 428。
- `ORDER_TRIGGERS` / stack / priority：`CORE-260330` p33-p35 rules 333-340；`CORE-260330` p52-p55 rules 383.3.d-383.3.e。
- Hidden / face-down / standby source guard：`CORE-260330` p4-p8 rules 107-129；更精确 FAQ 页码暂为 TODO。

验证记录：

- A focused：9/9 通过。
- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3350/3350。
- A frontend build：passed。
- A Chrome smoke：passed。
- A stage3 preflight：passed。
- A git diff check：passed。

仍缺 P0/P1：

- P0：完整 trigger engine。
- P0：state cleanup Warhawk（4C-8 后续已补代表路径；不再作为当前独立 P0 子项）。
- P0：其他 last-breath / destroyed / friendly-destroyed functional units。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：完整 effect resolution 与 FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18-step E2E。
- P1：trigger batch 正式 DTO、触发来源解释字段、hidden / standby / face-down trigger policy 文档化。

### 阶段 4C-8 Scouting Warhawk Cleanup Trigger Enqueue 审计

4C-8 证据入口：`docs/CURRENT_STAGE4C_BATCH8_SCOUTING_WARHAWK_CLEANUP_TRIGGER_AUDIT.md`。本节只更新规则证据 / P0-P1 审计，不代表 READY，不代表 1009 / 811 full-official。

4C-8 关闭的 P0 子项：

- Scouting Warhawk / 《侦察飞鹰》（`CATALOG` OGN·216/298，`FU-0500c77a70`）state-based cleanup lethal damage real trigger enqueue representative。
- Starfall / 《星落》（`CATALOG` OGN·029/298）作为 lethal damage + state-based cleanup source；本批不是 explicit destroy source 的新增覆盖。
- Starfall lethal damage -> state-based cleanup `LETHAL_DAMAGE` `UNIT_DESTROYED` -> visible Scouting Warhawk `SCOUTING_WARHAWK_LAST_BREATH_CALL_RUNE_1` -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` / `RUNES_CALLED`。
- hidden / face-down / standby Warhawk cleanup 路径不入队、不显示 prompt metadata、不触发 `RUNES_CALLED`。
- 4C-7 explicit destroy 路径和 single-trigger compatibility 保留。
- 本批没有协议 / 前端字段变化，不授予 full-official，不扩完整 trigger engine。

规则证据入口：

- State-based cleanup lethal destroy：`CORE-260330` p31-p33 rules 318-324；`CORE-260330` p14-p15 rules 142-143；`CORE-260330` p77 rule 460；`SOUL-OFAQ-260114` p19-p20。
- Scouting Warhawk last-breath enqueue：`CATALOG` OGN·216/298；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- Starfall lethal cleanup representative：`CATALOG` OGN·029/298；`CORE-260330` p14-p15 rules 142-143；`CORE-260330` p39-p42 rules 355-356。
- Hidden / face-down / standby source guard：`CORE-260330` p4-p8 rules 107-129；更精确 FAQ 页码暂为 TODO。

验证记录：

- A focused：11/11 通过。
- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3352/3352。
- A frontend build：passed。
- A Chrome smoke：passed。
- A stage3 preflight：passed。
- A git diff check：passed。

仍缺 P0/P1：

- P0：完整 trigger engine。
- P0：Sad / Loyal Poro（4C-9 后续已补 state-based cleanup 条件抽牌代表路径；不再作为当前独立 P0 子项）。
- P0：其他 last-breath / destroyed / friendly-destroyed functional units。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：完整 effect resolution 与 FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18-step E2E。
- P1：trigger batch 正式 DTO、触发来源解释字段、hidden / standby / face-down trigger policy 文档化。

### 阶段 4C-9 Poro Cleanup Trigger Enqueue 审计

4C-9 证据入口：`docs/CURRENT_STAGE4C_BATCH9_PORO_CLEANUP_TRIGGER_AUDIT.md`。本节只更新规则证据 / P0-P1 审计，不代表 READY，不代表 1009 / 811 full-official。

4C-9 关闭的 P0 子项：

- Sad Poro / 《哀哀魄罗》（`CATALOG` SFD·036/221，`FU-f8bfd5c6f9`）`SAD_PORO_LAST_BREATH_DRAW_1` state-based cleanup real trigger enqueue representative。
- Sad Poro / 《哀哀魄罗》（`CATALOG` UNL-221/219，`FU-938b749c23`）`SAD_PORO_LAST_BREATH_DRAW_1` state-based cleanup real trigger enqueue representative。
- Loyal Poro / 《忠忠魄罗》（`CATALOG` UNL-156/219，`FU-0415e3b46d`）`LOYAL_PORO_LAST_BREATH_DRAW_1` state-based cleanup real trigger enqueue representative。
- Starfall / 《星落》（`CATALOG` OGN·029/298）作为 lethal damage + state-based cleanup source。
- Starfall lethal damage -> state-based cleanup `LETHAL_DAMAGE` `UNIT_DESTROYED` -> visible base-zone Poro condition -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` / `CARD_DRAWN`。
- Sad 条件：base-zone、visible、非 face-down、非 standby，且同位置无其他友方正面非待命单位时触发。
- Loyal 条件：base-zone、visible、非 face-down、非 standby，且同位置有至少一个其他友方正面非待命单位，并且该友方不在本轮 cleanup removal set 中时触发。
- hidden / face-down / standby Poro cleanup 路径不入队、不显示 prompt metadata、不抽牌。
- 同位置其他友方也同时被 cleanup 摧毁的落单判定未官方化；runtime 对 Loyal 采取保守不入队，本批不宣称完整规则。
- 现有 explicit destroy P79 Sad / Loyal immediate compatibility 保留。
- 本批没有协议 / 前端字段变化，不授予 full-official，不扩完整 trigger engine。

规则证据入口：

- State-based cleanup lethal destroy：`CORE-260330` p31-p33 rules 318-324；`CORE-260330` p14-p15 rules 142-143；`CORE-260330` p77 rule 460；`SOUL-OFAQ-260114` p19-p20。
- Sad Poro condition draw：`CATALOG` SFD·036/221；`CATALOG` UNL-221/219；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- Loyal Poro condition draw：`CATALOG` UNL-156/219；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- Hidden / face-down / standby source guard：`CORE-260330` p4-p8 rules 107-129；更精确 FAQ 页码暂为 TODO。

验证记录：

- A focused：21/21 通过。
- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3358/3358。
- A frontend build：passed。
- A Chrome smoke：passed。
- A stage3 preflight：passed。
- A git diff check：passed。

仍缺 P0/P1：

- P0：完整 trigger engine。
- P0：其他 last-breath / destroyed / friendly-destroyed functional units。
- P0：battlefield objectLocation Poro condition matrix。
- P0：simultaneous cleanup condition timing，尤其同位置其他友方也同时被 cleanup 摧毁时的 Sad / Loyal 判定。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：完整 effect resolution 与 FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18-step E2E。
- P1：trigger batch 正式 DTO、触发来源解释字段、hidden / standby / face-down trigger policy 文档化。

### 阶段 4C-10 Unsung Hero Cleanup Trigger Enqueue 审计

4C-10 证据入口：`docs/CURRENT_STAGE4C_BATCH10_UNSUNG_HERO_CLEANUP_TRIGGER_AUDIT.md`。本节只更新规则证据 / P0-P1 审计，不代表 READY，不代表 1009 / 811 full-official。

4C-10 关闭的 P0 子项：

- Unsung Hero / 《无名英雄》（`CATALOG` SFD·167/221，`FU-1701d1d89a`）`UNSUNG_HERO_LAST_BREATH_DRAW_2_IF_POWERFUL` state-based cleanup real trigger enqueue representative。
- Starfall / 《星落》（`CATALOG` OGN·029/298）作为 lethal damage + state-based cleanup source。
- Starfall lethal damage -> state-based cleanup `LETHAL_DAMAGE` `UNIT_DESTROYED` -> visible base-zone Unsung Hero power >= 5 -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` / `CARD_DRAWN` x2。
- power < 5 cleanup 路径不入队、不抽牌。
- hidden / face-down / standby Unsung Hero cleanup 路径不入队、不显示 prompt metadata、不抽牌。
- 现有 explicit destroy P79 Unsung immediate compatibility 保留。
- 严格边界：本批只用 `CardObjectState.Power >= 5` 代表强力；不覆盖 LayerEngine / effective power / temporary modifier；不覆盖 battlefield objectLocation 全矩阵；不迁移 explicit destroy。
- 本批没有协议 / 前端字段变化，不授予 full-official，不扩完整 trigger engine。

规则证据入口：

- State-based cleanup lethal destroy：`CORE-260330` p31-p33 rules 318-324；`CORE-260330` p14-p15 rules 142-143；`CORE-260330` p77 rule 460；`SOUL-OFAQ-260114` p19-p20。
- Unsung Hero powerful last-breath draw：`CATALOG` SFD·167/221；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- Power threshold guard：`CATALOG` SFD·167/221；`CORE-260330` p57 rule 413.4；相关强力证据见 `rules-evidence-index.md` strong / powerful fixtures。
- Hidden / face-down / standby source guard：`CORE-260330` p4-p8 rules 107-129；更精确 FAQ 页码暂为 TODO。

验证记录：

- A focused：21/21 通过。
- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3361/3361。
- A frontend build：passed。
- A Chrome smoke：passed。
- A stage3 preflight：passed。
- A git diff check：passed。

仍缺 P0/P1：

- P0：完整 trigger engine。
- P0：其他 last-breath / destroyed / friendly-destroyed functional units。
- P0：effective power / LayerEngine、temporary modifier 和完整强力判定。
- P0：battlefield objectLocation matrix。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：完整 effect resolution 与 FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18-step E2E。
- P1：trigger batch 正式 DTO、触发来源解释字段、hidden / standby / face-down trigger policy 文档化。

### 阶段 4C-11 Ghostly Centaur Cleanup Trigger Enqueue 审计

4C-11 证据入口：`docs/CURRENT_STAGE4C_BATCH11_GHOSTLY_CENTAUR_CLEANUP_TRIGGER_AUDIT.md`。本节只更新规则证据 / P0-P1 审计，不代表 READY，不代表 1009 / 811 full-official。

4C-11 关闭的 P0 子项：

- Ghostly Centaur / 《幽魂半人马》（`CATALOG` UNL-068/219，`FU-0f2c4a3ea5`）friendly-destroyed power state-based cleanup real trigger enqueue representative。
- Starfall / 《星落》（`CATALOG` OGN·029/298）作为 lethal damage + state-based cleanup source。
- Starfall lethal damage -> state-based cleanup `LETHAL_DAMAGE` / `UNIT_DESTROYED` -> visible surviving friendly Ghostly source -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` -> `POWER_MODIFIED_UNTIL_END_OF_TURN` +2。
- hidden / face-down / standby / opponent-controlled Ghostly source 不入队、不显示 prompt metadata、不加战力。
- source 同时在本轮 cleanup removal set 中时保守不入队；本批不裁定完整同时死亡触发次数。
- 同一 Ghostly source 在同一个 cleanup pass 中最多入队一次，这是 4C-11 保守边界，不代表完整规则。
- 真实 stack destruction Ghostly 旧 P79 immediate compatibility 保留，未迁移到 `TriggerQueue`；这是 4C-11 当时 P1，4C-13 后已迁移关闭。
- 本批不覆盖 Viktor / Resonant Soul / Kogmaw / Karthus / Undercover Agent，不授予 full-official，不扩完整 trigger engine。

规则证据入口：

- State-based cleanup lethal destroy：`CORE-260330` p31-p33 rules 318-324；`CORE-260330` p14-p15 rules 142-143；`CORE-260330` p77 rule 460；`SOUL-OFAQ-260114` p19-p20。
- Ghostly Centaur friendly-destroyed power +2：`CATALOG` UNL-068/219；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- Cleanup removal-set guard：`CORE-260330` p31-p33 rules 318-324；精确同时死亡触发 FAQ 页码暂为 TODO。
- Hidden / face-down / standby / opponent source guard：`CORE-260330` p4-p8 rules 107-129；更精确 FAQ 页码暂为 TODO。

验证记录：

- B focused：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RealTriggerQueueTests|FullyQualifiedName~P79GhostlyCentaurGainsTemporaryPowerWhenAnotherFriendlyUnitDestroyed"` 通过，23/23。
- B diff check：`git diff --check -- src/Riftbound.Engine/CoreRuleEngine.cs tests/Riftbound.ConformanceTests/RealTriggerQueueTests.cs` 通过。
- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3364/3364。
- A frontend build：passed。
- A Chrome smoke：passed。
- A stage3 preflight：passed。

仍缺 P0/P1：

- P0：完整 trigger engine。
- P0：其他 last-breath / destroyed / friendly-destroyed functional units。
- P0：Viktor / Resonant Soul / Kogmaw / Karthus / Undercover Agent 等 friendly-destroyed 或相关触发族。
- P0：完整同时死亡触发次数、同一 cleanup pass 多对象时序、source 同时死亡时的官方裁定。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：完整 effect resolution 与 FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18-step E2E。
- 历史 P1（4C-13 后已关闭）：真实 stack destruction Ghostly 从 immediate compatibility 迁移到 `TriggerQueue`。
- P1：trigger batch 正式 DTO、触发来源解释字段、hidden / standby / face-down trigger policy 文档化。

### 阶段 4C-12 Resonant Soul Cleanup Trigger Enqueue 审计

4C-12 证据入口：`docs/CURRENT_STAGE4C_BATCH12_RESONANT_SOUL_CLEANUP_TRIGGER_AUDIT.md`。本节只更新规则证据 / P0-P1 审计，不代表 READY，不代表 1009 / 811 full-official。

4C-12 关闭的 P0 子项：

- Resonant Soul / 《残响之魂》（`CATALOG` OGN·118/298，`FU-c146331876`）first-friendly-destroyed draw state-based cleanup real trigger enqueue representative。
- Starfall / 《星落》（`CATALOG` OGN·029/298）作为 lethal damage + state-based cleanup source。
- Starfall lethal damage -> state-based cleanup `LETHAL_DAMAGE` / `UNIT_DESTROYED` -> visible surviving friendly Resonant Soul source，owner not already in `DestroyedUnitOwnerIdsThisTurn` -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` -> `CARD_DRAWN` 1。
- hidden / face-down / standby / opponent-controlled Resonant Soul source 不入队、不显示 prompt metadata、不抽牌。
- source 同时在本轮 cleanup removal set 中时保守不入队；本批不裁定完整同时死亡触发次数。
- 每 owner 每 cleanup pass 只按首次 destroyed unit 生成本批 source set；同回合已经记录 destroyed owner 时不入队。
- true stack destruction Resonant 旧 P79 immediate compatibility 保留，未迁移到 `TriggerQueue`；cleanup 事件跳过旧 immediate helper 防重复；这是 4C-12 当时 P1，4C-13 后已迁移关闭。
- 本批不覆盖 Viktor / Ghostly 后续 / Kogmaw / Karthus / Undercover Agent，不授予 full-official，不扩完整 trigger engine。

规则证据入口：

- State-based cleanup lethal destroy：`CORE-260330` p31-p33 rules 318-324；`CORE-260330` p14-p15 rules 142-143；`CORE-260330` p77 rule 460；`SOUL-OFAQ-260114` p19-p20。
- Resonant Soul first-friendly-destroyed draw：`CATALOG` OGN·118/298；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- Per-owner first destroy guard：`CATALOG` OGN·118/298；`CORE-260330` p31-p33 rules 318-324；精确同时死亡触发 FAQ 页码暂为 TODO。
- Cleanup removal-set guard：`CORE-260330` p31-p33 rules 318-324；精确同时死亡触发 FAQ 页码暂为 TODO。
- Hidden / face-down / standby / opponent source guard：`CORE-260330` p4-p8 rules 107-129；更精确 FAQ 页码暂为 TODO。

验证记录：

- B focused：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RealTriggerQueueTests|FullyQualifiedName~P79ResonantSoulDrawsOnlyForFirstFriendlyUnitDestroyedEachTurn"` 通过，27/27。
- B diff check：`git diff --check -- src/Riftbound.Engine/CoreRuleEngine.cs tests/Riftbound.ConformanceTests/RealTriggerQueueTests.cs` 通过。
- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3368/3368。
- A frontend build：passed。
- A Chrome smoke：passed。
- A stage3 preflight：passed。

仍缺 P0/P1：

- P0：完整 trigger engine。
- P0：其他 last-breath / destroyed / friendly-destroyed functional units。
- P0：Viktor / Ghostly 后续 / Kogmaw / Karthus / Undercover Agent 等 friendly-destroyed 或相关触发族。
- P0：完整“每回合首次”时序、完整同时死亡触发次数、同一 cleanup pass 多对象排序、source 同时死亡时的官方裁定。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：完整 effect resolution 与 FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18-step E2E。
- 历史 P1（4C-13 后已关闭）：true stack destruction Resonant Soul 从 immediate compatibility 迁移到 `TriggerQueue`。
- P1：trigger batch 正式 DTO、触发来源解释字段、hidden / standby / face-down trigger policy 文档化。

### 阶段 4C-13 Stack Destroyed Trigger Migration 审计

4C-13 证据入口：`docs/CURRENT_STAGE4C_BATCH13_STACK_DESTROYED_TRIGGER_MIGRATION_AUDIT.md`。本节只更新规则证据 / P0-P1 审计，不代表 READY，不代表 1009 / 811 full-official。

4C-13 不新增 FU；本批迁移 / 关闭 4C-11 / 4C-12 留下的 P1：Ghostly Centaur + Resonant Soul true stack destruction 旧 immediate compatibility -> real trigger queue。

覆盖 FUs：

- Ghostly Centaur / 《幽魂半人马》（`CATALOG` UNL-068/219，`FU-0f2c4a3ea5`）。
- Resonant Soul / 《残响之魂》（`CATALOG` OGN·118/298，`FU-c146331876`）。

4C-13 关闭的 P1 / P0 子项：

- P1：Ghostly Centaur true stack destruction friendly-destroyed power +2 从旧 immediate helper 迁移到真实 `TriggerQueue` / stack / priority 语义。
- P1：Resonant Soul true stack destruction first-friendly-destroyed draw 从旧 immediate helper 迁移到真实 `TriggerQueue` / stack / priority 语义。
- P0 子项：两个既有 FU 现在同时具备 cleanup representative 与 true stack destruction representative 的 real enqueue 证据。
- true stack destruction 非 cleanup `UNIT_DESTROYED` -> `TriggerQueue` -> `ORDER_TRIGGERS` or single-trigger auto-stack -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` -> Ghostly `POWER_MODIFIED_UNTIL_END_OF_TURN` +2 / Resonant `CARD_DRAWN` 1。
- cleanup path 继续通过 `IsStateBasedCleanupDestroyedEvent` 排除旧 stack helper，避免重复入队。
- hidden / face-down / standby / opponent-controlled source 不入队；source 必须留场、正面、非 standby、同 controller。
- Resonant 继续尊重 `DestroyedUnitOwnerIdsThisTurn`。
- 旧 P79 tests 已更新为 queue / stack / priority 语义。
- 本批不覆盖 Viktor / Kogmaw / Karthus / Undercover Agent，不授予 full-official，不扩完整 trigger engine。

规则证据入口：

- Stack destruction `UNIT_DESTROYED` 触发入队：`CORE-260330` p14-p15 rules 142-143；`CORE-260330` p33-p35 rules 333-340；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- Ghostly Centaur power +2：`CATALOG` UNL-068/219；`CORE-260330` p52-p55 rules 383.3.d-383.3.e。
- Resonant Soul first-friendly-destroyed draw：`CATALOG` OGN·118/298；`CORE-260330` p52-p55 rules 383.3.d-383.3.e。
- Cleanup / stack helper 防重复：`CORE-260330` p31-p33 rules 318-324；工程事件来源契约。
- Hidden / face-down / standby / opponent source guard：`CORE-260330` p4-p8 rules 107-129；更精确 FAQ 页码暂为 TODO。

验证记录：

- B focused：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RealTriggerQueueTests|FullyQualifiedName~P79GhostlyCentaur|FullyQualifiedName~P79ResonantSoul"` 通过，30/30。
- B full backend：passed，3370/3370。
- A backend full：passed，3370/3370。
- B diff check：`git diff --check -- src/Riftbound.Engine/CoreRuleEngine.cs tests/Riftbound.ConformanceTests/RealTriggerQueueTests.cs tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` 通过。
- A frontend build：passed。
- A Chrome smoke：passed。
- A stage3 preflight：passed。

仍缺 P0/P1：

- P0：完整 trigger engine。
- P0：其他 last-breath / destroyed / friendly-destroyed functional units。
- P0：Viktor / Kogmaw / Karthus / Undercover Agent 等 friendly-destroyed 或相关触发族。
- P0：完整“每回合首次”时序、完整同时死亡触发次数、同一 cleanup pass 多对象排序、source 同时死亡时的官方裁定。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：完整 effect resolution 与 FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18-step E2E。
- P1：trigger batch 正式 DTO、触发来源解释字段、hidden / standby / face-down trigger policy 文档化。

| 流程 / P0 | 规则依据入口 | 当前实现状态 | 分类 | 归属 agent | 下一步 |
| --- | --- | --- | --- | --- | --- |
| 创建 / 加入 / 卡组 / 准备 / 开局连续链路 | `CORE-260330` rule 103；rules 107-129；工程会话契约 | 后端和 UI 有分散代表路径；缺同一双浏览器阶段 3 smoke | 阻断 smoke | B/C；D 审计 | C 补最小 Chrome smoke，B 保持服务端权威，D 记录证据 |
| 起手调整与隐藏信息 | `CORE-260330` rules 107-129；开局流程 | `MULLIGAN` 与 selection guard 有代表测试；DOM/store/WS 双窗口隐藏断言未收口 | 阻断 smoke | B/C；D 审计 | smoke 同时断言对手手牌、牌堆顺序、hidden metadata 不泄漏 |
| 第一回合 / 召符文 / 抽牌 | `CORE-260330` p20 rules 164-167；p28-p29 rule 315；rule 481.7 | `p2-preflight-turn-start-*` 已有；缺浏览器连续证明 | 阻断 smoke | B/C；D 审计 | 断言 turn、active player、phase、runePool、hand count 来自 snapshot |
| 打出卡牌 / 支付 | `CORE-260330` rules 349+、355-357、377、403-405；`JFAQ-251023` q2.5 | 代表 `PLAY_CARD` / `COST_PAID` 有；阶段 3A 已补 `PAY_COST` 最小切片，完整 PaymentEngine 未关闭 | 阻断 smoke；可在阶段 3 内继续 | B 主；C 等 schema；E fixture；D 审计 | smoke 用简单服务端候选先闭环；B 继续完整 payment window |
| 移动 / 争夺 / 结算链 / 法术对决 | `CORE-260330` rules 187-189、307-313、333-348、344-348；`JFAQ-251023` q2.2-q5.4 | 具体战场移动、代表争夺、stack/pass/focus 有；完整 lifecycle 未关闭 | 阻断 smoke；可在阶段 3 内继续 | B/C/E；D 审计 | smoke 选择一条最短路径，不关闭完整 lifecycle P0 |
| 结束回合 / 投降或胜负 | `CORE-260330` rules 316-324、461-464；产品会话契约 | end-turn / result 代表路径有；连续浏览器 result 未收口 | 阻断 smoke | B/C；D 审计 | smoke 至少进入下一回合，并覆盖投降或服务端 winner/result |

阶段 3 B 服务端阻断关闭时，D 必须在 `docs/CURRENT_STAGE3_CORE_FLOW_AUDIT.md` 的记录位补齐：规则依据、实现状态、测试证据、仍缺口和 D 审计结论。

## 仍未清零阻断

- P0-S2-001 battlefield / standby / control / held / conquer lifecycle 进入 3B 最小切片；代表 snapshot/control/held/conquer 子项可候选关闭，但完整 lifecycle 未完成。
- P0-S2-002 cleanup queue 进入 3B 最小切片；代表 task view/非法待命/致命伤害子项可候选关闭，但全触发面 central queue 未完成。
- P0-S2-003 spell duel / battle lifecycle 进入 3C 最小切片；focus/pass、battle view、battle resolution 代表子项可候选关闭，但完整 lifecycle 未完成。
- P0-S2-004 `ASSIGN_COMBAT_DAMAGE` runtime 进入 3C 最小切片；schema skeleton、最小 runtime prompt、damage assignment phase、submit/reject 和 simultaneous commit 子项已关闭，完整全规则矩阵未完成。
- P0-S2-005 `PAY_COST` 完整 PaymentEngine 与 `DECLINE_PAY_COST` / 替代费用 / 额外费用 / 非出牌支付窗口仍未完成；阶段 3A 最小 runtime 切片已关闭。
- P0-S2-006 `ORDER_TRIGGERS` 最小 runtime / UI / evidence 子项已关闭；4C-1 APNAP controller-block 子集、battle initial stack 代表证据和 hidden trigger source redaction 子项已关闭；4C-2 Watchful Sentinel 多触发真实入队、经排序 / stack / priority 结算和非法排序 no mutation 子项已关闭；4C-3 Honest Broker 遗言金币真实多触发排序 / 入栈 / 结算代表路径已关闭；4C-4 Treasure Pile 触发支付 / 拒付 / 支付失败 no-mutation 代表路径已关闭；4C-5 state-based cleanup `LETHAL_DAMAGE` -> visible Watchful last-breath enqueue 代表路径已关闭；4C-6 state-based cleanup `LETHAL_DAMAGE` -> visible Honest Broker last-breath enqueue 代表路径已关闭；4C-7 Scouting Warhawk explicit destroy real trigger enqueue 代表路径已关闭；4C-8 Scouting Warhawk state-based cleanup lethal damage real trigger enqueue 代表路径已关闭；4C-9 Sad / Loyal Poro state-based cleanup 条件抽牌 real trigger enqueue 代表路径已关闭；4C-10 Unsung Hero state-based cleanup powerful draw-2 real trigger enqueue 代表路径已关闭；4C-11 Ghostly Centaur state-based cleanup friendly-destroyed power +2 real trigger enqueue 代表路径已关闭；4C-12 Resonant Soul state-based cleanup first-friendly-destroyed draw real trigger enqueue 代表路径已关闭；4C-13 Ghostly Centaur / Resonant Soul true stack destruction 旧 immediate compatibility -> real trigger queue / stack / priority 迁移已关闭；完整 trigger engine、其他 destroyed / last-breath / friendly-destroyed FUs、Viktor / Kogmaw / Karthus / Undercover Agent、完整“每回合首次”时序、完整同时死亡触发次数、effective power / LayerEngine、temporary modifier、battlefield objectLocation matrix、hidden / face-down 原始触发建模、更多 trigger payment / decline、完整 effect resolution、FAQ regression、1009/811 full-official 未完成。
- 3A-P0-001 / 002 / 003 / 004 已关闭；不得把这些 3A 子项误读为完整 Stage 3 或 READY。
- 3B-CAND-001 / 002 / 003 / 004 只能作为阶段 3B 关闭候选；D/A 证据入账前不得移出 P0。
- 3C-CAND-001 / 002 / 003 / 004 只能作为阶段 3C 关闭候选；D/A 证据入账前不得移出 P0，且不得替代最终 18 步 E2E。
- 3C-P0-001 / 002 / 003 / 004 / 005 仍阻断 READY；3C-P1-001 仍需正式 DTO 冻结。
- 3D 关闭 `ORDER_TRIGGERS` 最小 runtime / UI / evidence 子项和第三阶段文档收口口径；A final validation 已通过，第三阶段可判定 DONE，允许进入阶段 4 不等于 READY。
- S3-P0-001 双浏览器连续核心链路未完成；3A 基础 Chrome route smoke 已完成。
- S3-P0-002 创建 / 加入 / 卡组 / 准备 / 开局 / 起手 / 第一回合未以同一阶段 3 smoke 证明。
- S3-P0-003 打牌 / 移动 / 争夺或结算链或法术对决 / 结束回合 / 投降或胜负未以同一阶段 3 smoke 证明。
- S3-P0-004 阶段 3 隐藏信息 DOM/store/WS 断言未收口。
- LayerEngine 未统一；持续/替代/预防/层系统解释仍不能支撑正式规则助手。
- 全官方卡牌证据与执行仍未完成。
- 正式 18 步 E2E 未最终收口，尤其需要覆盖双人窗口、隐藏信息、复杂 prompt、战斗/法术对决、断线重连。

## 推荐 D/E 子 agent 任务

- D-Protocol：继续维护 `docs/CURRENT_STAGE1_PROTOCOL_BASELINE.md`，每次服务端协议改动后同步真实 DTO、命令 union、错误码与不存在 DTO 列表。
- D-P0Contract：维护 `docs/CURRENT_STAGE2_P0_CONTRACT_PLAN.md`，每轮对齐 P0-S2-001 至 P0-S2-006 的证据链、实现状态和 owner。
- D-Stage3A：维护 `docs/CURRENT_STAGE3A_PLAN.md`，阶段 3A 只跟踪 smoke 基线、typed mapper、`PAY_COST` 最小 runtime、前端外壳安全接线，不扩大到 3B/3C。
- D-Stage3B：维护 `docs/CURRENT_STAGE3B_PLAN.md`，阶段 3B 只跟踪 battlefield / standby / control / conquer lifecycle 与 central cleanup queue 最小官方化切片，不扩大到 18 步 E2E 或 1009 全量。
- D-Stage3C：维护 `docs/CURRENT_STAGE3C_SPELL_DUEL_BATTLE_DAMAGE_EVIDENCE.md`，阶段 3C 只跟踪 spell duel / battle / `ASSIGN_COMBAT_DAMAGE` / battle cleanup 的规则证据、关闭候选和仍缺口，不扩大到最终 18 步 E2E、1009 全量、完整 PaymentEngine / `ORDER_TRIGGERS` / LayerEngine。
- D-Stage3D：维护 `docs/CURRENT_STAGE3_COMPLETION_AUDIT.md`，第三阶段收口只判断阶段性关闭项、仍缺 P0/P1、阶段 4 入口和最终验收边界；不得宣称 READY。
- D-Stage3Flow：维护 `docs/CURRENT_STAGE3_CORE_FLOW_AUDIT.md`，每次 B/C 报告阶段 3 smoke 或服务端阻断关闭时，补规则依据、实现状态、测试证据、仍缺口和是否仍阻断。
- D-FrontendContract：继续把正式复杂 prompt 字段草案拆成 `PAY_COST`、`ORDER_TRIGGERS`、`ASSIGN_COMBAT_DAMAGE`、`SPELL_DUEL_ACTION` 四张契约清单；标注 `PAY_COST` 已有最小 runtime 和 `SFD·220/221` `TRIGGER_PAYMENT` 代表路径、完整 PaymentEngine 未开放，`ASSIGN_COMBAT_DAMAGE` 只有最小 runtime、完整全规则矩阵未开放，`ORDER_TRIGGERS` 已到 Watchful Sentinel + Honest Broker 两条 last-breath real enqueue、visible Watchful / Honest cleanup enqueue、Warhawk explicit / cleanup enqueue、Sad / Loyal Poro cleanup enqueue、Unsung Hero cleanup enqueue、Ghostly Centaur cleanup / stack enqueue 与 Resonant Soul cleanup / stack enqueue 代表路径，但完整 trigger engine / 其他 destroyed-family / friendly-destroyed FUs / 完整“每回合首次”时序 / 完整同时死亡触发次数 / effective power 或 LayerEngine / temporary modifier / battlefield objectLocation matrix / hidden 或 face-down 原始触发建模 / effect resolution / 更多 trigger payment / FAQ regression 未开放，`SPELL_DUEL_ACTION` 仍没有完整 runtime prompt，现有降级面板只能临时承接展示。
- E-RuleEvidence：把五份官方规则/FAQ PDF 与 2026-04-27 官网卡牌快照继续映射到 P0-S2-001 至 P0-S2-006，尤其补 `JFAQ` q2.2/q2.3/q2.5/q5.4/q6.x 和 `SOUL-OFAQ` p21 的 fixture 锚点。
- E-Conformance：为仍未清零 P0 建最小官方场景，优先覆盖失控待命延迟移除、恶意收购非战斗法术对决、完整 APNAP 多玩家独立排序、其他 destroyed / last-breath / friendly-destroyed FUs、Viktor / Kogmaw / Karthus / Undercover Agent、完整“每回合首次”时序、完整同时死亡触发次数、hidden / face-down 原始触发建模、触发费用拒付、多单位伤害分配、战斗清理与征服/据守得分。

## A 主控验收记录

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ZeroPower|FullyQualifiedName~NegativePower|FullyQualifiedName~LowercaseOfficialBattlefield|FullyQualifiedName~MoveUnitCommandMovesBaseUnitToConcreteBattlefield|FullyQualifiedName~ActionPromptOffersConcreteBattlefieldsForBaseUnitMove"`：11/11 通过。
- `git diff --check`：通过。
- `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore`：3304/3304 通过。
