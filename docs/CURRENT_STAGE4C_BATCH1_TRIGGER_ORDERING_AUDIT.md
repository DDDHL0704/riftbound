# 阶段 4C-1 触发排序审计

日期：2026-05-10
结论：**NOT READY**

本文记录 D 对 B 阶段 4C-1 `ORDER_TRIGGERS` / 多触发排序批次的规则证据与 P0/P1 审计口径。D 本轮只更新文档，不修改服务端、前端、覆盖矩阵、A checkpoint 或 `riftbound-dotnet.sln`。

## 1. 本批关闭子项

4C-1 可以关闭以下 P0 子项：

- `ORDER_TRIGGERS` 从 3D 最小排序窗口升级为保守 APNAP controller-block 子集。
- prompt metadata 现在区分 `orderedTriggerIds` 与 `triggerIds`：`orderedTriggerIds` 表示合法 APNAP resolution top-first 默认提交顺序，`triggerIds` 表示 raw queue order。
- `legalOrderingConstraints` 已明确 APNAP policy、top-first semantics、controller block order、legal resolution block order、跨控制者不可重排、同控制者可重排。
- runtime 校验覆盖合法排序 accepted；非法跨控制者重排 rejected 且 no state mutation。
- `BuildCorePrompts` 让 `ORDER_TRIGGERS` window 优先于 `START_BATTLE` / task prompt。
- battle initial stack 代表证据已补：active battle attacker / defender 初始触发 -> `ORDER_TRIGGERS` -> stack priority。
- trigger prompt / snapshot 对不可见 face-down standby source 做 viewer 级脱敏。

验证记录：

- A 后端 full test：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3337/3337。

## 2. 规则证据入口

| 规则域 | 证据入口 | 4C-1 审计口径 | 仍需补证 |
| --- | --- | --- | --- |
| Trigger ordering / APNAP | `CORE-260330` p33-p35 rules 333-340；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3 | 支持 controller-block 子集：跨控制者 block 不可重排，同控制者 block 内可重排，默认 top-first 顺序可提交 | 完整 APNAP 多玩家独立排序、所有同时触发来源和可选触发选择仍需补 |
| Trigger payment / decline | `CORE-260330` p52-p55 rules 377, 403-405；`JFAQ-251023` p2-p4 q2.5 | 本批不接支付 / 拒付；只保证排序 window 和 stack move | 触发费用确认、拒付、支付失败 no mutation 与 PaymentEngine 统一仍是 P0 |
| Battle initial stack | `CORE-260330` p35-p36 rules 341-348；`CORE-260330` p77-p78 rules 454-461；`JFAQ-251023` p2-p4 q2.2-q2.4 | 已有 attacker / defender 初始触发进入 `ORDER_TRIGGERS` 后再进入 stack priority 的代表证据 | 全官方 battle initial stack 特殊顺序、攻防触发特殊排列、battle response window 与完整 FAQ 回归仍需补 |
| Hidden information / face-down standby source | `CORE-260330` p4-p8 rules 107-129；待命/显露相关证据继续复用 `CORE-260330` p39-p42 rules 355-356 | prompt / snapshot 对不可见 face-down standby source viewer 级脱敏，避免用 trigger metadata 泄漏卡面 | face-down standby trigger 的所有实际卡牌路径、目标选择与完整隐藏区交互仍需补；如需更精确 FAQ 页码，标为 evidence TODO |

## 3. 仍缺 P0/P1

仍缺 P0：

- 完整 trigger engine。
- 完整 effect resolution。
- 真实卡牌全触发生成，而不是代表性触发队列。
- trigger payment / decline / payment failure。
- 完整 APNAP 多玩家独立排序。
- battle initial stack 全官方规则。
- FAQ 回归，尤其同时触发、战斗初始触发、触发费用拒付和隐藏信息交织。
- 最终正式 18 步 E2E。
- 1009 张卡 full-official 覆盖。

仍缺 P1：

- `TriggerInstance` / `TriggerBatchPromptView` / `legalOrderingConstraints` 的正式 DTO 文档化与前后端长期契约。
- trigger source、controller、visibility、card text summary 的产品级解释字段。
- 多语言 UI 文案、FAQ 证据链接和调试视图一致性。

## 4. D 审计结论

4C-1 可作为 `ORDER_TRIGGERS` 的第二个阶段性关闭点：3D 关闭最小 window / UI / evidence，4C-1 关闭 APNAP controller-block 子集、battle initial stack 代表路径和 hidden source redaction 子项。

这不等于完整触发系统 READY，不等于 1009 full-official，不等于最终验收版 18 步 E2E。项目仍 **NOT READY**。
