# 阶段 4C-2 真实触发入队审计

日期：2026-05-09
结论：**NOT READY**

本文记录 D 对 B 阶段 4C-2 real card-trigger enqueue 最小切片的规则证据与 P0/P1 审计口径。D 本轮只更新文档，不修改服务端、前端、覆盖矩阵、A checkpoint 或 `riftbound-dotnet.sln`。

## 1. 本批关闭子项

4C-2 可以关闭以下 P0 子项：

- 真实 `UNIT_DESTROYED` 路径中，多张 `Watchful Sentinel` / 《警觉的哨兵》（`CATALOG` OGN·096/298）遗言抽牌触发已接入 `TriggerQueue`。
- 多触发代表路径已串成：`TriggerQueue` -> `ORDER_TRIGGERS` prompt -> `StackItems` -> pass priority -> `TRIGGER_RESOLVED` / `CARD_DRAWN`。
- 单个 Watchful Sentinel 仍保留即时结算兼容；本批不宣称统一单触发策略。
- 跨控制者 APNAP 默认 `orderedTriggerIds` 可直接提交并 accepted。
- 非法跨控制者排序 rejected 且 no state mutation。
- 本批未改协议 / 前端。

验证记录：

- A focused：11/11 通过。
- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3338/3338。
- A frontend build：passed。
- A Chrome smoke：passed。
- A stage3 preflight：passed。

## 2. 规则证据入口

| 规则域 | 证据入口 | 4C-2 审计口径 | 仍需补证 |
| --- | --- | --- | --- |
| 真实卡牌触发入队 | `CATALOG` OGN·096/298；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3 | Watchful Sentinel 多张遗言抽牌触发从真实 `UNIT_DESTROYED` 进入 `TriggerQueue`，再进入排序 / 结算链路径 | 其他 last-breath / friendly-destroyed / attack / conquer 触发族仍需逐族补证 |
| `ORDER_TRIGGERS` / stack / priority | `CORE-260330` p33-p35 rules 333-340；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3 | 真实触发可经 `ORDER_TRIGGERS` prompt 排序、移动到 `StackItems`，再经 pass priority 结算 | 完整 APNAP 多玩家独立排序、可选触发、effect resolution 与 FAQ 回归仍需补 |
| Trigger payment / decline | `CORE-260330` p52-p55 rules 377, 403-405；`JFAQ-251023` p2-p4 q2.5 | 本批不接支付 / 拒付；只覆盖无费用遗言抽牌代表路径 | 触发费用确认、拒付、支付失败 no mutation 与 PaymentEngine 统一仍是 P0 |
| State-based cleanup triggers | `CORE-260330` p31-p33 rules 318-324；更精确 FAQ 页码 TODO | 本批从真实 `UNIT_DESTROYED` 事件代表路径证明可入队，不等于所有清理来源统一入队 | state-based cleanup 触发统一入队、repeat-until-stable 与替代 / 预防交织仍需补 |

## 3. 仍缺 P0/P1

仍缺 P0：

- 完整 trigger engine。
- 其他 last-breath / friendly-destroyed / attack / conquer 触发族。
- state-based cleanup 触发统一入队。
- trigger payment / decline / payment failure。
- 完整 effect resolution 与完整 FAQ regression。
- 1009 / 811 full-official 覆盖。
- 最终正式 18 步 E2E。

仍缺 P1：

- `TriggerInstance` / `TriggerBatchPromptView` / `legalOrderingConstraints` 正式 DTO 与长期契约。
- 真实触发来源、可选性、controller、visibility 与卡面摘要的产品级解释字段。
- 单触发即时结算兼容策略的长期口径文档化。

## 4. D 审计结论

4C-2 可作为 `ORDER_TRIGGERS` / trigger engine 的第三个阶段性关闭点：3D 关闭最小 window / UI / evidence，4C-1 关闭 APNAP controller-block 子集，4C-2 关闭 Watchful Sentinel 多触发真实卡牌事件入队代表路径。

这不等于完整触发系统 READY，不等于 1009 / 811 full-official，不等于最终验收版 18 步 E2E。项目仍 **NOT READY**。
