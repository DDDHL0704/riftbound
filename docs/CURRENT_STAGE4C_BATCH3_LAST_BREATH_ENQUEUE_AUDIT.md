# 阶段 4C-3 绝念真实触发入队审计

日期：2026-05-09
结论：**NOT READY**

本文记录 D 对 B 阶段 4C-3 Honest Broker last-breath real enqueue 小批的规则证据与 P0/P1 审计口径。D 本轮只更新文档，不修改服务端、前端、覆盖矩阵、A checkpoint 或 `riftbound-dotnet.sln`。

## 1. 本批关闭子项

4C-3 可以关闭以下 P0 子项：

- `HonestBrokerCardNo` / `HONEST_BROKER_LAST_BREATH_CREATE_GOLD` 从直接结算扩展到真实多触发路径。
- Honest Broker / 《诚实掮客》（`CATALOG` SFD·155/221）遗言金币代表路径已串成：`UNIT_DESTROYED` -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` -> `EQUIPMENT_TOKEN_CREATED`。
- 跨控制者真实 last-breath APNAP 默认顺序可直接提交。
- 非法跨控制者排序 rejected 且 no mutation。
- 单触发 Watchful Sentinel / Honest Broker 仍保留即时结算兼容；本批不宣称统一单触发策略完成。

验证记录：

- A focused：13/13 通过。
- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3339/3339。
- A frontend build：passed。
- A Chrome smoke：passed。
- A stage3 preflight：passed。

## 2. 规则证据入口

| 规则域 | 证据入口 | 4C-3 审计口径 | 仍需补证 |
| --- | --- | --- | --- |
| Honest Broker last-breath enqueue | `CATALOG` SFD·155/221；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3 | 真实 `UNIT_DESTROYED` 中 Honest Broker 遗言金币可进入 `TriggerQueue`，再通过排序 / 入栈 / priority 结算创建金币装备指示物 | 其他 destroyed-family 触发族、非栈摧毁时机、全卡牌绝念仍需补 |
| `ORDER_TRIGGERS` / stack / priority | `CORE-260330` p33-p35 rules 333-340；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3 | 真实 Honest Broker 触发可经 `ORDER_TRIGGERS` prompt、`StackItems`、priority pass 后广播 `TRIGGER_RESOLVED` / `EQUIPMENT_TOKEN_CREATED` | 完整 APNAP 多玩家独立排序、可选触发、effect resolution 与 FAQ regression 仍需补 |
| Trigger payment / decline | `CORE-260330` p52-p55 rules 377, 403-405；`JFAQ-251023` p2-p4 q2.5 | 本批不接支付 / 拒付；只覆盖无费用绝念金币代表路径 | 触发费用确认、拒付、支付失败 no mutation 与 PaymentEngine 统一仍是 P0 |
| State-based cleanup triggers | `CORE-260330` p31-p33 rules 318-324；更精确 FAQ 页码 TODO | 本批从真实 `UNIT_DESTROYED` 代表路径证明可入队，不等于所有清理来源统一入队 | state-based cleanup 触发统一入队、repeat-until-stable 与替代 / 预防交织仍需补 |

## 3. 4C-2 + 4C-3 代表证据合并口径

现在已有两个 last-breath 代表路径具备 real enqueue 证据：

- Watchful Sentinel / 《警觉的哨兵》（`CATALOG` OGN·096/298）：真实 `UNIT_DESTROYED` -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority -> `TRIGGER_RESOLVED` / `CARD_DRAWN`。
- Honest Broker / 《诚实掮客》（`CATALOG` SFD·155/221）：真实 `UNIT_DESTROYED` -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority -> `TRIGGER_RESOLVED` / `EQUIPMENT_TOKEN_CREATED`。

这只证明两个 representative last-breath family members 已接入 real enqueue，不等于完整 last-breath engine 或完整 trigger engine。

## 4. 仍缺 P0/P1

仍缺 P0：

- 完整 trigger engine。
- 其他 destroyed-family。
- state-based cleanup 触发入队。
- trigger payment / decline / payment failure。
- FAQ regression。
- 1009 / 811 full-official 覆盖。
- 最终正式 18 步 E2E。

仍缺 P1：

- `TriggerInstance` / `TriggerBatchPromptView` / `legalOrderingConstraints` 正式 DTO 与长期契约。
- 真实触发来源、可选性、controller、visibility 与卡面摘要的产品级解释字段。
- 单触发即时结算兼容策略的长期口径文档化。

## 5. D 审计结论

4C-3 可作为 `ORDER_TRIGGERS` / trigger engine 的第四个阶段性关闭点：3D 关闭最小 window / UI / evidence，4C-1 关闭 APNAP controller-block 子集，4C-2 关闭 Watchful Sentinel 多触发真实入队代表路径，4C-3 关闭 Honest Broker 遗言金币真实入队代表路径。

这不等于完整触发系统 READY，不等于 1009 / 811 full-official，不等于最终验收版 18 步 E2E。项目仍 **NOT READY**。
