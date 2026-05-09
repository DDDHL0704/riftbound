# 阶段 4C-12 Resonant Soul Cleanup Trigger Enqueue 审计

更新时间：2026-05-10
结论：**NOT READY**

本文记录 D 对阶段 4C-12 Resonant Soul first-friendly-destroyed draw state-based cleanup real trigger enqueue 最小切片的规则证据与 P0/P1 审计口径。D 本轮只更新用户授权的 checkpoint / audit docs，不修改服务端、前端、覆盖矩阵、E evidence 文件或 `riftbound-dotnet.sln`。

## 1. 4C-12 关闭的 P0 子项

4C-12 可以关闭以下 P0 子项：

- Resonant Soul / 《残响之魂》（`CATALOG` OGN·118/298，`FU-c146331876`）first-friendly-destroyed draw state-based cleanup real trigger enqueue representative。
- 支撑来源为 Starfall / 《星落》（`CATALOG` OGN·029/298）造成 lethal damage 后的 state-based cleanup。
- 官方化路径为：Starfall lethal damage -> state-based cleanup `LETHAL_DAMAGE` / `UNIT_DESTROYED` -> visible surviving friendly Resonant Soul source，owner not already in `DestroyedUnitOwnerIdsThisTurn` -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` -> `CARD_DRAWN` 1。
- hidden / face-down / standby / opponent-controlled Resonant Soul source 不入队、不显示 prompt metadata、不抽牌。
- source 同时在本轮 cleanup removal set 中时保守不入队；本批不裁定完整同时死亡触发次数。
- 每 owner 每 cleanup pass 只按首次 destroyed unit 生成本批 source set；同回合已经记录 destroyed owner 时不入队。
- true stack destruction Resonant Soul 旧 P79 immediate compatibility 保留，未迁移到 `TriggerQueue`；cleanup 事件跳过旧 immediate helper 以避免重复。
- 本批不覆盖 Viktor / Ghostly 后续 / Kogmaw / Karthus / Undercover Agent，不授予 full-official，不扩完整 trigger engine。

## 2. 规则证据入口

| 规则域 | 证据入口 | 4C-12 审计口径 | 仍需补证 |
| --- | --- | --- | --- |
| State-based cleanup lethal destroy | `CORE-260330` p31-p33 rules 318-324；`CORE-260330` p14-p15 rules 142-143；`CORE-260330` p77 rule 460；`SOUL-OFAQ-260114` p19-p20 | Starfall 造成致命伤害后，cleanup `LETHAL_DAMAGE` 产生 `UNIT_DESTROYED` 并作为 first-friendly-destroyed 入队来源 | 替代 / 预防、repeat-until-stable、更多 cleanup 来源和 FAQ 回归 |
| Resonant Soul first-friendly-destroyed draw | `CATALOG` OGN·118/298；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3 | visible surviving friendly Resonant Soul source 在 owner 本回合尚未记录 destroyed owner 时入队，priority pass 后 `TRIGGER_RESOLVED` / `CARD_DRAWN` 1 | 完整 friendly-destroyed trigger engine、更多同类 FU、完整 effect resolution |
| Per-owner first destroy guard | `CATALOG` OGN·118/298；`CORE-260330` p31-p33 rules 318-324；精确同时死亡触发 FAQ 页码 TODO | 每 owner 每 cleanup pass 只按首次 destroyed unit 生成本批 source set；同回合已经记录 destroyed owner 时不入队 | 完整“每回合首次”时序、source/affected object timing、同一 cleanup pass 多对象排序 |
| Cleanup removal-set guard | `CORE-260330` p31-p33 rules 318-324；精确同时死亡触发 FAQ 页码 TODO | source 同时在本轮 cleanup removal set 中时保守不入队 | 完整同时死亡触发次数、APNAP 与 cleanup 多轮交织 |
| Hidden / face-down / standby / opponent source guard | `CORE-260330` p4-p8 rules 107-129；更精确 FAQ 页码 TODO | hidden / face-down / standby / opponent-controlled Resonant Soul source 不入队，不显示 prompt metadata，不抽牌 | face-down 原始触发建模、显露窗口、viewer 级 trigger metadata 全路径、控制权变化组合 |
| Stack destruction compatibility | 既有 `P79ResonantSoulDrawsOnlyForFirstFriendlyUnitDestroyedEachTurn` 测试；`CATALOG` OGN·118/298 | 旧 P79 immediate compatibility 保留，未迁移到真实 `TriggerQueue`；cleanup 事件跳过旧 helper 防重复 | 统一单触发策略、stack destruction real enqueue、后续 P1 迁移 |

## 3. A 复核记录

- B focused：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RealTriggerQueueTests|FullyQualifiedName~P79ResonantSoulDrawsOnlyForFirstFriendlyUnitDestroyedEachTurn"` 通过，27/27。
- B diff check：`git diff --check -- src/Riftbound.Engine/CoreRuleEngine.cs tests/Riftbound.ConformanceTests/RealTriggerQueueTests.cs` 通过。
- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3368/3368。
- A frontend build：passed。
- A Chrome smoke：passed。
- A Stage 3 preflight：passed。

## 4. 仍保留 P0/P1

- P0：完整 trigger engine。
- P0：其他 last-breath / destroyed / friendly-destroyed functional units。
- P0：Viktor / Ghostly 后续 / Kogmaw / Karthus / Undercover Agent 等 friendly-destroyed 或相关触发族。
- P0：完整“每回合首次”时序、完整同时死亡触发次数、同一 cleanup pass 多对象排序、source 同时死亡时的官方裁定。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：完整 effect resolution 与 FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18-step E2E。
- P1：true stack destruction Resonant Soul 从 immediate compatibility 迁移到 `TriggerQueue`。
- P1：trigger batch 正式 DTO、触发来源解释字段、hidden / standby / face-down trigger policy 文档化。

## 5. D 审计结论

4C-12 可作为 `ORDER_TRIGGERS` / trigger engine 的第十二个阶段性关闭点：它证明 Resonant Soul 的 first-friendly-destroyed draw 在 state-based cleanup lethal damage 路径下可以进入真实触发队列，并经排序 / 入栈 / 优先权结算为抽 1 张牌。

项目仍 **NOT READY**：本批不覆盖完整 trigger engine、其他 friendly-destroyed FUs、完整“每回合首次”时序、完整同时死亡触发次数、hidden / face-down 原始触发建模、完整 effect resolution、FAQ regression、1009 / 811 full-official 或最终正式 18-step E2E。阶段 4C 仍处于逐 FU、逐测试的小批推进中。
