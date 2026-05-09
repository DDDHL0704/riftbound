# 阶段 4C-13 Stack Destroyed Trigger Migration 审计

更新时间：2026-05-10
结论：**NOT READY**

本文记录 D 对阶段 4C-13 true stack destruction trigger migration 的规则证据与 P0/P1 审计口径。D 本轮只更新用户授权的 checkpoint / audit docs，不修改服务端、前端、覆盖矩阵、E evidence 文件或 `riftbound-dotnet.sln`。

## 1. 4C-13 关闭的 P1 / P0 子项

4C-13 不新增 FU；本批迁移并关闭 4C-11 / 4C-12 留下的 P1：Ghostly Centaur + Resonant Soul true stack destruction 旧 immediate compatibility -> real trigger queue。

覆盖 FUs：

- Ghostly Centaur / 《幽魂半人马》（`CATALOG` UNL-068/219，`FU-0f2c4a3ea5`）。
- Resonant Soul / 《残响之魂》（`CATALOG` OGN·118/298，`FU-c146331876`）。

4C-13 可以关闭以下子项：

- Ghostly Centaur true stack destruction friendly-destroyed power +2 从旧 immediate helper 迁移到真实 `TriggerQueue` / stack / priority 语义。
- Resonant Soul true stack destruction first-friendly-destroyed draw 从旧 immediate helper 迁移到真实 `TriggerQueue` / stack / priority 语义。
- 官方化路径为：true stack destruction 非 cleanup `UNIT_DESTROYED` -> `TriggerQueue` -> `ORDER_TRIGGERS` or single-trigger auto-stack -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` -> Ghostly `POWER_MODIFIED_UNTIL_END_OF_TURN` +2 / Resonant `CARD_DRAWN` 1。
- cleanup path 继续通过 `IsStateBasedCleanupDestroyedEvent` 排除旧 stack helper，避免重复入队。
- hidden / face-down / standby / opponent-controlled source 不入队；source 必须留场、正面、非 standby、同 controller。
- Resonant Soul 继续尊重 `DestroyedUnitOwnerIdsThisTurn`。
- 旧 P79 tests 已更新为 queue / stack / priority 语义。
- 本批不覆盖 Viktor / Kogmaw / Karthus / Undercover Agent，不授予 full-official，不扩完整 trigger engine。

## 2. 规则证据入口

| 规则域 | 证据入口 | 4C-13 审计口径 | 仍需补证 |
| --- | --- | --- | --- |
| Stack destruction `UNIT_DESTROYED` 触发入队 | `CORE-260330` p14-p15 rules 142-143；`CORE-260330` p33-p35 rules 333-340；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3 | true stack destruction 非 cleanup `UNIT_DESTROYED` 可进入 `TriggerQueue`，并经 `ORDER_TRIGGERS` 或 single-trigger auto-stack、`StackItems`、priority pass 结算 | 完整 stack destruction trigger engine、更多 destroyed-family FU、FAQ regression |
| Ghostly Centaur power +2 | `CATALOG` UNL-068/219；`CORE-260330` p52-p55 rules 383.3.d-383.3.e | 4C-11 cleanup representative 保留；4C-13 补 true stack destruction -> real queue / stack / priority -> `POWER_MODIFIED_UNTIL_END_OF_TURN` +2 | Viktor / Kogmaw / Karthus / Undercover Agent、完整同时死亡触发次数、完整 effect resolution |
| Resonant Soul first-friendly-destroyed draw | `CATALOG` OGN·118/298；`CORE-260330` p52-p55 rules 383.3.d-383.3.e | 4C-12 cleanup representative 保留；4C-13 补 true stack destruction -> real queue / stack / priority -> `CARD_DRAWN` 1，并继续尊重 `DestroyedUnitOwnerIdsThisTurn` | 完整“每回合首次”时序、更多 friendly-destroyed FU、完整 effect resolution |
| Cleanup / stack helper 防重复 | `CORE-260330` p31-p33 rules 318-324；工程事件来源契约 | cleanup path 继续通过 `IsStateBasedCleanupDestroyedEvent` 排除旧 stack helper，避免 cleanup 与 stack helper 双重入队 | 所有 cleanup / stack / replacement / prevention 来源统一事件 provenance |
| Hidden / face-down / standby / opponent source guard | `CORE-260330` p4-p8 rules 107-129；更精确 FAQ 页码 TODO | hidden / face-down / standby / opponent-controlled source 不入队；source 必须留场、正面、非 standby、同 controller | face-down 原始触发建模、显露窗口、viewer 级 trigger metadata 全路径、控制权变化组合 |

## 3. A 复核记录

- B focused：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RealTriggerQueueTests|FullyQualifiedName~P79GhostlyCentaur|FullyQualifiedName~P79ResonantSoul"` 通过，30/30。
- B full backend：passed，3370/3370。
- A backend full：passed，3370/3370。
- B diff check：`git diff --check -- src/Riftbound.Engine/CoreRuleEngine.cs tests/Riftbound.ConformanceTests/RealTriggerQueueTests.cs tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` 通过。
- A frontend build：passed。
- A Chrome smoke：passed。
- A Stage 3 preflight：passed。

## 4. 仍保留 P0/P1

- P0：完整 trigger engine。
- P0：其他 last-breath / destroyed / friendly-destroyed functional units。
- P0：Viktor / Kogmaw / Karthus / Undercover Agent 等 friendly-destroyed 或相关触发族。
- P0：完整“每回合首次”时序、完整同时死亡触发次数、同一 cleanup pass 多对象排序、source 同时死亡时的官方裁定。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：完整 effect resolution 与 FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18-step E2E。
- P1：trigger batch 正式 DTO、触发来源解释字段、hidden / standby / face-down trigger policy 文档化。

## 5. D 审计结论

4C-13 可关闭 4C-11 / 4C-12 留下的 P1 migration：Ghostly Centaur 与 Resonant Soul 的 true stack destruction 代表路径已从旧 immediate compatibility 迁移为真实触发队列、入栈、优先权结算语义。

项目仍 **NOT READY**：本批不新增 FU，不覆盖 Viktor / Kogmaw / Karthus / Undercover Agent，不覆盖完整 trigger engine、完整“每回合首次”时序、完整同时死亡触发次数、hidden / face-down 原始触发建模、完整 effect resolution、FAQ regression、1009 / 811 full-official 或最终正式 18-step E2E。阶段 4C 仍处于逐 FU、逐测试的小批推进中。
