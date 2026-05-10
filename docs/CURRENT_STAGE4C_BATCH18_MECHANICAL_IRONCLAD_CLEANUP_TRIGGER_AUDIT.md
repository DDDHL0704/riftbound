# 阶段 4C-18 Mechanical + Ironclad Cleanup Trigger 审计

更新时间：2026-05-10
结论：**NOT READY**

本文记录阶段 4C-18 的文档 / 规则证据 / P0-P1 审计口径。4C-16 已完成 Mechanical Trickster true stack last-breath 迁移，4C-17 已完成 Ironclad Vanguard true stack last-breath 迁移；4C-18 已把这两张卡的 state-based cleanup lethal damage 路线纳入真实 TriggerQueue / Stack / Priority baseline。整体仍 **NOT READY**。

## 范围

- Mechanical Trickster / `OGN·239/298` / `MECHANICAL_TRICKSTER_LAST_BREATH_CREATE_MINIONS`。
- Ironclad Vanguard / `SFD·021/221` / 冻结矩阵 FU `FU-6d0971786b` / `IRONCLAD_VANGUARD_LAST_BREATH_CREATE_ROBOTS`。
- 只覆盖 state-based cleanup lethal damage 后的 last-breath enqueue baseline；true stack 路线分别由 4C-16 / 4C-17 覆盖。
- 不进入 Kogmaw / Karthus / Undercover Agent。
- 不宣称 full trigger engine、1009 / 811 full-official、正式 18-step E2E 或 READY / READY-CANDIDATE。

## 已验证官方化路径

- state-based cleanup `LETHAL_DAMAGE` 产生 `UNIT_DESTROYED`。
- visible、face-up、non-standby 的 Mechanical Trickster 或 Ironclad Vanguard source 进入 `TriggerQueue`。
- 单触发可 auto-stack；多触发走 `ORDER_TRIGGERS` -> `StackItems`。
- priority 双方 pass 后 `TRIGGER_RESOLVED`。
- Mechanical Trickster 结算为 `UNIT_TOKEN_CREATED` x3。
- Ironclad Vanguard 结算为 `UNIT_TOKEN_CREATED` x2。

代表 lethal-damage 来源为 Starfall cleanup route。新增测试名见验证结果。

## Negative guard

- hidden / face-down / standby source 不入队。
- 不显示 prompt metadata，不创建 token。
- source 同时在本轮 cleanup removal set 中的复杂 multiplicity 仍作为后续 full official matrix 风险保留。
- viewer 级 hidden original visibility 仍是全局 P0，不因本批 cleanup baseline 自动关闭。

## 验证结果

- B focused filter：通过 47/47。
- B backend full：通过 3388/3388。
- A backend full：通过 3388/3388。
- A frontend build：通过。
- A Chrome smoke：通过。
- `git diff --check`、矩阵 JSON/断言通过。
- 新增测试：`StateBasedCleanupMechanicalTrickstersTriggerOrderAndCreateMinionsThroughStack`、`StateBasedCleanupIroncladVanguardsTriggerOrderAndCreateRobotsThroughStack`、`StateBasedCleanupHiddenMechanicalTrickstersDoNotEnqueueTriggers`、`StateBasedCleanupHiddenIroncladVanguardsDoNotEnqueueTriggers`。
- 无协议 / 前端字段变化，前端无需新增复杂交互；仍只消费服务端 snapshot/prompt。

## 仍保留 P0/P1

- P0：Kogmaw / Karthus / Undercover Agent 等 destroyed-family / friendly-destroyed holdbacks。
- P0：完整 trigger engine、完整 effect resolution、trigger batch / 可选触发选择、完整 APNAP 组合。
- P1：same source same cleanup pass / same stack pass 多对象触发次数的 full official multiplicity matrix。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：FAQ regression、1009 entries / 811 functional units full-official 覆盖。
- P0：正式 18-step E2E 与 completion audit。

4C-18 的当前判断：**已关闭 Mechanical Trickster + Ironclad Vanguard cleanup-route representative trigger enqueue baseline，但不能关闭 READY 阻断**。
