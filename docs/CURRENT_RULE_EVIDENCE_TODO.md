# 当前规则证据 TODO

更新时间：2026-05-09
结论：**NOT READY**

本文记录 E 证据/审计 worker 第一轮 P0 交接项、阶段 1 D 协议审计、阶段 2 D P0 规则证据链和 A 主控验收结果，不替代 `docs/CURRENT_SERVER_RULE_AUDIT.md`。

当前 HEAD：`bc0872d`
阶段 1 基线提交：`78b6896`
阶段 2 证据链计划：`docs/CURRENT_STAGE2_P0_CONTRACT_PLAN.md`

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
- 已更新 `docs/CURRENT_FRONTEND_CONTRACT_GAPS.md`：复杂 prompt 的通用降级展示已经可作为安全承接路径；阶段 2 B 已补 `PAY_COST` / `ASSIGN_COMBAT_DAMAGE` / `ORDER_TRIGGERS` command/schema skeleton 与 `INVALID_PAYLOAD`；真实 runtime prompt、状态机、专用交互和错误 details 仍是 P0。
- 阶段 1 文档结论仍是 **NOT READY**；D 本轮没有关闭新的功能 P0/P1，只关闭了“文档描述不准”的口径风险。

## 阶段 2 D P0 规则证据链汇总

本轮 D 已新增 `docs/CURRENT_STAGE2_P0_CONTRACT_PLAN.md`，并把同一套 P0 证据链同步到 `docs/CURRENT_SERVER_RULE_AUDIT.md` 与 `docs/rules-evidence-index.md`。

| P0 | 规则依据 | 当前状态 | 归属 agent | 下一步 |
| --- | --- | --- | --- | --- |
| battlefield / standby / control / held / conquer lifecycle | `CORE-260330` rules 107.2-107.3, 187-189, 315.2.b.2, 319-323, 344-348, 461-464；`JFAQ-251023` q4.1-5.4；`SOUL-OFAQ-260114` p21；`SOUL-JFAQ-260114` p4-p5 | `ObjectLocations`、`BattlefieldStates`、`BattlefieldTasks`、具体战场移动已有；完整 control freeze/release、standby removal、held/conquer scoring 未完成 | B 主实现；E 证据 fixture；C 等 schema；D 文档 | 建 board task model，覆盖控制检查、待命移除、征服/据守得分 |
| cleanup queue | `CORE-260330` rules 319-324；`JFAQ-251023` q5.1-5.2；`SOUL-OFAQ-260114` p19-p20 | `PendingTaskQueue`、`PendingCleanupTasks`、`RunStateBasedCleanupLoop`、blocking guard 已有；未覆盖全部状态变化和替代/控制权任务 | B 主实现；E 场景证据；D 文档 | 所有 command/stack/trigger/move/enter/leave/damage/power change 统一 enqueue cleanup |
| spell duel / battle lifecycle | `CORE-260330` rules 307-313, 333-348, 454-461；`JFAQ-251023` q2.3-q2.4, q3.1-q3.3 | `SpellDuelState`、`BattleState`、关联 id 和焦点恢复已有；`DECLARE_BATTLE` 仍是同步代表路径 | B 主实现；E 初始链/焦点/触发 fixture；C 等 typed prompt；D 文档 | 由 cleanup queue 创建并推进 spell duel / battle task |
| damage assignment | `CORE-260330` rules 142-143, 417, 460；`JFAQ-251023` q6.1-q6.4；`SOUL-OFAQ-260114` p19-p20 | 有代表性 `BuildBattleDamageAssignmentOrder`；阶段 2 B 已补 `ASSIGN_COMBAT_DAMAGE` command/schema skeleton 与 `INVALID_PAYLOAD`；runtime prompt、pending battle damage assignment phase 和状态机仍缺 | B 主实现；E 多单位/壁垒/后排 fixture；C 仅同步类型/调试展示；D 文档 | 拆 battle damage assignment phase，服务端下发 damage pool / targets / constraints |
| `PAY_COST` / payment windows | `CORE-260330` rules 131, 135.2.e, 162-167, 356-357, 377, 403-405, 414, 416；`JFAQ-251023` q2.5；`SOUL-OFAQ-260114` p1-p4, p19-p21 | `PaymentCostRules`、typed `RunePool`、代表性 `COST_PAID` 已有；阶段 2 B 已补 `PAY_COST` command/schema skeleton 与 `INVALID_PAYLOAD`；runtime prompt、`DECLINE_PAY_COST`、pending payment state 仍缺 | B 主实现；E 支付/拒付 fixture；C 仅同步类型/调试展示；D 文档 | 建 `PaymentPlan/paymentPlanId/paymentWindow` 与 Quote/Authorize/Commit |
| `ORDER_TRIGGERS` | `CORE-260330` rules 333-340, 383.3.d-383.3.e；`JFAQ-251023` q2.2-q2.3, q2.5 | 有 `TRIGGER_QUEUED` / `TRIGGER_RESOLVED` 与部分 `triggerQueue` view；阶段 2 B 已补 `ORDER_TRIGGERS` command/schema skeleton 与 `INVALID_PAYLOAD`；runtime prompt、trigger batch ordering 状态机仍缺 | B 主实现；E 同时触发/战斗初始触发 fixture；C 仅同步类型/调试展示；D 文档 | 建 `TriggerInstance`、trigger batch、排序 prompt 和提交命令 |

## superseded / 防误读

- 0/负战力：阶段 1 已修复并由 A 验收；后续只保留防回归，不再列为未清零 P0。
- 具体战场 objectId 大小写：阶段 1 已修复并由 A 验收；后续只保留防回归，不再列为未清零 P0。
- replay/final hash：历史“仍缺严格 action-log replay final-state 校验”口径已被当前 P1-004 状态替代；当前有 representative verifier、恢复前审计和 Postgres smoke，剩余风险是全命令/全恢复/全随机 property。
- 复杂 prompt 降级展示：阶段 1 已完成安全降级与 prompt 戳过期保护；历史“完全没有复杂 prompt 入口”已 superseded。
- 复杂 prompt schema：阶段 2 B 已补 `PAY_COST` / `ASSIGN_COMBAT_DAMAGE` / `ORDER_TRIGGERS` command/schema skeleton 与 malformed payload 稳定拒绝；历史“完全没有正式 schema/稳定拒绝语义”已 superseded，但 runtime prompt、状态机和专用交互仍是 P0。

## 阶段 2 B 已关闭的 P0 子项

- `PAY_COST`、`ASSIGN_COMBAT_DAMAGE`、`ORDER_TRIGGERS` 有稳定 command 名称。
- 三类 command 有首版 payload 字段名：`paymentId/paymentWindow/paymentChoiceIds`、`battleId/battlefieldId/assignments[].sourceObjectId/targetObjectId/damage`、`triggerIds`。
- malformed payload 可稳定返回 `INVALID_PAYLOAD`；合法形状且进入“已识别但未实现”的执行点时仍返回 `UNSUPPORTED_COMMAND`，窗口/前置状态不满足时可能先走 `PHASE_NOT_ALLOWED` 或 `INVALID_TARGET` 等拒绝路径。

## 仍未清零阻断

- P0-S2-001 battlefield / standby / control / held / conquer lifecycle 未完成。
- P0-S2-002 cleanup queue 未完成。
- P0-S2-003 spell duel / battle lifecycle 未完成。
- P0-S2-004 `ASSIGN_COMBAT_DAMAGE` runtime prompt、damage assignment phase/state machine 未完成；schema skeleton 子项已关闭。
- P0-S2-005 `PAY_COST` runtime prompt、PaymentEngine 与 `DECLINE_PAY_COST` / 拒付语义未完成；schema skeleton 子项已关闭。
- P0-S2-006 `ORDER_TRIGGERS` runtime prompt、trigger batch ordering 状态机未完成；schema skeleton 子项已关闭。
- LayerEngine 未统一；持续/替代/预防/层系统解释仍不能支撑正式规则助手。
- 全官方卡牌证据与执行仍未完成。
- 正式 18 步 E2E 未最终收口，尤其需要覆盖双人窗口、隐藏信息、复杂 prompt、战斗/法术对决、断线重连。

## 推荐 D/E 子 agent 任务

- D-Protocol：继续维护 `docs/CURRENT_STAGE1_PROTOCOL_BASELINE.md`，每次服务端协议改动后同步真实 DTO、命令 union、错误码与不存在 DTO 列表。
- D-P0Contract：维护 `docs/CURRENT_STAGE2_P0_CONTRACT_PLAN.md`，每轮对齐 P0-S2-001 至 P0-S2-006 的证据链、实现状态和 owner。
- D-FrontendContract：继续把正式复杂 prompt 字段草案拆成 `PAY_COST`、`ORDER_TRIGGERS`、`ASSIGN_COMBAT_DAMAGE`、`SPELL_DUEL_ACTION` 四张契约清单；对后三者标注 schema skeleton 已有、runtime prompt 未开放，现有降级面板只能临时承接展示。
- E-RuleEvidence：把五份官方规则/FAQ PDF 与 2026-04-27 官网卡牌快照继续映射到 P0-S2-001 至 P0-S2-006，尤其补 `JFAQ` q2.2/q2.3/q2.5/q5.4/q6.x 和 `SOUL-OFAQ` p21 的 fixture 锚点。
- E-Conformance：为仍未清零 P0 建最小官方场景，优先覆盖失控待命延迟移除、恶意收购非战斗法术对决、触发排序、触发费用拒付、多单位伤害分配、战斗清理与征服/据守得分。

## A 主控验收记录

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ZeroPower|FullyQualifiedName~NegativePower|FullyQualifiedName~LowercaseOfficialBattlefield|FullyQualifiedName~MoveUnitCommandMovesBaseUnitToConcreteBattlefield|FullyQualifiedName~ActionPromptOffersConcreteBattlefieldsForBaseUnitMove"`：11/11 通过。
- `git diff --check`：通过。
- `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore`：3304/3304 通过。
