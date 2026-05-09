# 当前规则证据 TODO

更新时间：2026-05-09
结论：**NOT READY**

本文记录 E 证据/审计 worker 第一轮 P0 交接项与 A 主控验收结果，不替代 `docs/CURRENT_SERVER_RULE_AUDIT.md`。

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
- 已更新 `docs/CURRENT_FRONTEND_CONTRACT_GAPS.md`：复杂 prompt 的通用降级展示已经可作为安全承接路径，但正式 `payload/command/专用交互/错误 details` 仍是 P0。
- 阶段 1 文档结论仍是 **NOT READY**；D 本轮没有关闭新的功能 P0/P1，只关闭了“文档描述不准”的口径风险。

## 仍未清零阻断

- ActionPrompt/command/payload：`PromptView` 最小入口、prompt type 预留、`ActionPanel` 降级展示和 `promptId/snapshotTick` 过期保护已具备；但 `PAY_COST`、`DECLINE_PAY_COST`、`ASSIGN_COMBAT_DAMAGE`、`ORDER_TRIGGERS`、`SPELL_DUEL_ACTION` 仍没有正式命令 union、typed payload schema 和专用交互。
- snapshot/visibility：`BuildSnapshotForViewer` 与 DevUi 隐藏展示路径已能支撑联调；但 `players/lanes/timing/stack` 仍大量依赖字典/unknown，没有正式 typed visibility contract，也没有独立 `RoomState/MatchSnapshot`。
- Payment/trigger/damage assignment：支付仍分散在 `optionalCosts`、临时 metadata、部分 `PaymentCostRules` 和事件包络中；触发排序没有 `ORDER_TRIGGERS` prompt；伤害分配没有独立 `ASSIGN_COMBAT_DAMAGE` prompt/command。
- spell duel / battle lifecycle：已有 `spellDuelId/battleId` 和部分 snapshot/prompt 关联字段；但法术对决、声明战斗、伤害分配、清理、结果仍没有完整状态机契约。
- central cleanup queue 未完整官方化；如果清理中需要玩家选择，必须收敛为正式 prompt。
- LayerEngine 未统一；持续/替代/预防/层系统解释仍不能支撑正式规则助手。
- 全官方卡牌证据与执行仍未完成。
- 正式 18 步 E2E 未最终收口，尤其需要覆盖双人窗口、隐藏信息、复杂 prompt、战斗/法术对决、断线重连。

## 推荐 D/E 子 agent 任务

- D-Protocol：继续维护 `docs/CURRENT_STAGE1_PROTOCOL_BASELINE.md`，每次服务端协议改动后同步真实 DTO、命令 union、错误码与不存在 DTO 列表。
- D-FrontendContract：把复杂 prompt 的正式字段草案拆成 `PAY_COST`、`ORDER_TRIGGERS`、`ASSIGN_COMBAT_DAMAGE`、`SPELL_DUEL_ACTION` 四张契约清单，并标注可由现有降级面板临时承接的范围。
- E-RuleEvidence：把五份官方规则/FAQ PDF 与 2026-04-27 官网卡牌快照映射到 Payment、trigger ordering、damage assignment、spell duel/battle lifecycle、cleanup/layer 的证据锚点。
- E-Conformance：为仍未清零 P0 建立最小可复现官方场景，优先覆盖支付拒绝/替代费用、触发排序、多单位伤害分配、法术对决关闭、战斗清理。

## A 主控验收记录

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ZeroPower|FullyQualifiedName~NegativePower|FullyQualifiedName~LowercaseOfficialBattlefield|FullyQualifiedName~MoveUnitCommandMovesBaseUnitToConcreteBattlefield|FullyQualifiedName~ActionPromptOffersConcreteBattlefieldsForBaseUnitMove"`：11/11 通过。
- `git diff --check`：通过。
- `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore`：3304/3304 通过。
