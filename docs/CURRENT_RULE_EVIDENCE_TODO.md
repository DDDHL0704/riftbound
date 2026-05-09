# 当前规则证据 TODO

更新时间：2026-05-09
结论：**NOT READY**

本文记录 E 证据/审计 worker 第一轮 P0 交接项、阶段 1 D 协议审计、阶段 2 D P0 规则证据链和 A 主控验收结果，不替代 `docs/CURRENT_SERVER_RULE_AUDIT.md`。

当前 HEAD：`4b41e81`
阶段 1 基线提交：`78b6896`
阶段 2 证据链计划：`docs/CURRENT_STAGE2_P0_CONTRACT_PLAN.md`
阶段 3 核心流程审计：`docs/CURRENT_STAGE3_CORE_FLOW_AUDIT.md`
阶段 3A 当前计划：`docs/CURRENT_STAGE3A_PLAN.md`

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

## 阶段 3 D 对战桌面 / 核心流程证据框架

阶段 3 围绕本地双人 1v1 对战桌面的连续路径做审计：创建 / 加入、卡组、准备、开局、起手、第一回合、召符文、打牌、移动、争夺或结算链或法术对决、结束回合、投降或胜负结算。当前结论仍为 **NOT READY**。阶段 3A 已完成最小 Chrome smoke、三类复杂命令强类型映射、`PAY_COST` 最小 runtime 和前端桌面外壳；正式 18 步 E2E、3B/3C 连续对战流程、完整 P0/P1 清零仍未完成。

### 阶段 3A 范围修正

当前只执行阶段 3A。宽阶段 3 审计框架保留为后续总图，不作为本轮验收范围。3A 只收口 smoke 基线、三类复杂命令强类型映射、`PAY_COST` 最小 runtime、前端对战桌面外壳安全接线。

3A 暂不进入：最终正式 18 步 E2E、1009 张卡 full-official 覆盖、完整 battle runtime、完整 `ASSIGN_COMBAT_DAMAGE` runtime、完整 `ORDER_TRIGGERS` runtime、完整 battlefield / standby / control / held / conquer lifecycle、完整 PaymentEngine / LayerEngine。

| 3A P0 | 规则 / 契约依据 | 当前状态 | 归属 agent | 下一步 |
| --- | --- | --- | --- | --- |
| 3A-P0-001 Chrome smoke 基线 | A 目标文档阶段 3 / Chrome smoke；`CORE-260330` rules 107-129 | 已关闭：`npm run smoke:chrome -- --start-api` 通过，覆盖 API、DevUi、Chrome headless-CDP 与 7 个基础路由 | C / A / D | 3B 再扩双人连续流程与隐藏信息断言 |
| 3A-P0-002 三类复杂命令强类型映射 | `CommandTypes`、三类 command DTO、`ActionPromptContracts` | 已关闭：三类 JSON command -> typed command mapper 已落地，malformed payload 稳定拒绝，后端 full test 3324/3324 通过 | B / D | 后续 runtime 逐类开放时补对应合法性测试 |
| 3A-P0-003 `PAY_COST` 最小 runtime | `CORE-260330` rules 131、135.2.e、162-167、356-357、377、403-405、414、416；`JFAQ-251023` q2.5 | 已关闭最小切片：pending payment prompt、choices、合法提交、stale/invalid/零副作用测试已通过 | B / E / C / D | 完整 PaymentEngine、decline、替代/额外费用仍是后续 P0 |
| 3A-P0-004 前端外壳不裁决规则 | 服务端权威原则；`CORE-260330` rules 107-129；`ActionPromptDto` / `SnapshotDto` | 已关闭 3A 外壳：只读 snapshot/prompt、只提交服务端候选；未冻结 complex prompt safe fallback | C / D | 正式复杂交互等待服务端 runtime 冻结 |

| 流程 / P0 | 规则依据入口 | 当前实现状态 | 分类 | 归属 agent | 下一步 |
| --- | --- | --- | --- | --- | --- |
| 创建 / 加入 / 卡组 / 准备 / 开局连续链路 | `CORE-260330` rule 103；rules 107-129；工程会话契约 | 后端和 UI 有分散代表路径；缺同一双浏览器阶段 3 smoke | 阻断 smoke | B/C；D 审计 | C 补最小 Chrome smoke，B 保持服务端权威，D 记录证据 |
| 起手调整与隐藏信息 | `CORE-260330` rules 107-129；开局流程 | `MULLIGAN` 与 selection guard 有代表测试；DOM/store/WS 双窗口隐藏断言未收口 | 阻断 smoke | B/C；D 审计 | smoke 同时断言对手手牌、牌堆顺序、hidden metadata 不泄漏 |
| 第一回合 / 召符文 / 抽牌 | `CORE-260330` p20 rules 164-167；p28-p29 rule 315；rule 481.7 | `p2-preflight-turn-start-*` 已有；缺浏览器连续证明 | 阻断 smoke | B/C；D 审计 | 断言 turn、active player、phase、runePool、hand count 来自 snapshot |
| 打出卡牌 / 支付 | `CORE-260330` rules 349+、355-357、377、403-405；`JFAQ-251023` q2.5 | 代表 `PLAY_CARD` / `COST_PAID` 有；`PAY_COST` runtime / PaymentEngine 未关闭 | 阻断 smoke；可在阶段 3 内继续 | B 主；C 等 schema；E fixture；D 审计 | smoke 用简单服务端候选先闭环；B 继续真实 payment window |
| 移动 / 争夺 / 结算链 / 法术对决 | `CORE-260330` rules 187-189、307-313、333-348、344-348；`JFAQ-251023` q2.2-q5.4 | 具体战场移动、代表争夺、stack/pass/focus 有；完整 lifecycle 未关闭 | 阻断 smoke；可在阶段 3 内继续 | B/C/E；D 审计 | smoke 选择一条最短路径，不关闭完整 lifecycle P0 |
| 结束回合 / 投降或胜负 | `CORE-260330` rules 316-324、461-464；产品会话契约 | end-turn / result 代表路径有；连续浏览器 result 未收口 | 阻断 smoke | B/C；D 审计 | smoke 至少进入下一回合，并覆盖投降或服务端 winner/result |

阶段 3 B 服务端阻断关闭时，D 必须在 `docs/CURRENT_STAGE3_CORE_FLOW_AUDIT.md` 的记录位补齐：规则依据、实现状态、测试证据、仍缺口和 D 审计结论。

## 仍未清零阻断

- P0-S2-001 battlefield / standby / control / held / conquer lifecycle 未完成。
- P0-S2-002 cleanup queue 未完成。
- P0-S2-003 spell duel / battle lifecycle 未完成。
- P0-S2-004 `ASSIGN_COMBAT_DAMAGE` runtime prompt、damage assignment phase/state machine 未完成；schema skeleton 子项已关闭。
- P0-S2-005 `PAY_COST` 完整 PaymentEngine 与 `DECLINE_PAY_COST` / 替代费用 / 额外费用 / 非出牌支付窗口仍未完成；阶段 3A 最小 runtime 切片已关闭。
- P0-S2-006 `ORDER_TRIGGERS` runtime prompt、trigger batch ordering 状态机未完成；schema skeleton 子项已关闭。
- 3A-P0-001 / 002 / 003 / 004 已关闭；不得把这些 3A 子项误读为完整 Stage 3 或 READY。
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
- D-Stage3Flow：维护 `docs/CURRENT_STAGE3_CORE_FLOW_AUDIT.md`，每次 B/C 报告阶段 3 smoke 或服务端阻断关闭时，补规则依据、实现状态、测试证据、仍缺口和是否仍阻断。
- D-FrontendContract：继续把正式复杂 prompt 字段草案拆成 `PAY_COST`、`ORDER_TRIGGERS`、`ASSIGN_COMBAT_DAMAGE`、`SPELL_DUEL_ACTION` 四张契约清单；对后三者标注 schema skeleton 已有、runtime prompt 未开放，现有降级面板只能临时承接展示。
- E-RuleEvidence：把五份官方规则/FAQ PDF 与 2026-04-27 官网卡牌快照继续映射到 P0-S2-001 至 P0-S2-006，尤其补 `JFAQ` q2.2/q2.3/q2.5/q5.4/q6.x 和 `SOUL-OFAQ` p21 的 fixture 锚点。
- E-Conformance：为仍未清零 P0 建最小官方场景，优先覆盖失控待命延迟移除、恶意收购非战斗法术对决、触发排序、触发费用拒付、多单位伤害分配、战斗清理与征服/据守得分。

## A 主控验收记录

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ZeroPower|FullyQualifiedName~NegativePower|FullyQualifiedName~LowercaseOfficialBattlefield|FullyQualifiedName~MoveUnitCommandMovesBaseUnitToConcreteBattlefield|FullyQualifiedName~ActionPromptOffersConcreteBattlefieldsForBaseUnitMove"`：11/11 通过。
- `git diff --check`：通过。
- `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore`：3304/3304 通过。
