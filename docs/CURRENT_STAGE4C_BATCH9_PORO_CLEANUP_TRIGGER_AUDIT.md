# 阶段 4C-9 Poro Cleanup Trigger Enqueue 审计

更新时间：2026-05-10
结论：**NOT READY**

本文记录 D 对阶段 4C-9 Sad / Loyal Poro 条件抽牌 state-based cleanup real trigger enqueue 最小切片的规则证据与 P0/P1 审计口径。D 本轮只更新用户授权的 checkpoint / audit docs，不修改服务端、前端、覆盖矩阵、E evidence 文件或 `riftbound-dotnet.sln`。

## 1. 4C-9 关闭的 P0 子项

4C-9 可以关闭以下 P0 子项：

- Sad Poro / 《哀哀魄罗》（`CATALOG` SFD·036/221，`FU-f8bfd5c6f9`）`SAD_PORO_LAST_BREATH_DRAW_1` state-based cleanup real trigger enqueue representative。
- Sad Poro / 《哀哀魄罗》（`CATALOG` UNL-221/219，`FU-938b749c23`）`SAD_PORO_LAST_BREATH_DRAW_1` state-based cleanup real trigger enqueue representative。
- Loyal Poro / 《忠忠魄罗》（`CATALOG` UNL-156/219，`FU-0415e3b46d`）`LOYAL_PORO_LAST_BREATH_DRAW_1` state-based cleanup real trigger enqueue representative。
- 支撑来源为 Starfall / 《星落》（`CATALOG` OGN·029/298）造成 lethal damage 后的 state-based cleanup。
- 官方化路径为：Starfall lethal damage -> state-based cleanup `LETHAL_DAMAGE` `UNIT_DESTROYED` -> visible base-zone Poro condition -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` / `CARD_DRAWN`。
- Sad 条件：base-zone、visible、非 face-down、非 standby，且同位置无其他友方正面非待命单位时触发。
- Loyal 条件：base-zone、visible、非 face-down、非 standby，且同位置有至少一个其他友方正面非待命单位，并且该友方不在本轮 cleanup removal set 中时触发。
- hidden / face-down / standby Poro cleanup 路径不入队、不显示 prompt metadata、不抽牌。
- 现有 explicit destroy P79 Sad / Loyal immediate compatibility 保留。
- 本批无协议 / 前端字段变化，不授予 full-official，不扩完整 trigger engine。

## 2. 规则证据入口

| 规则域 | 证据入口 | 4C-9 审计口径 | 仍需补证 |
| --- | --- | --- | --- |
| State-based cleanup lethal destroy | `CORE-260330` p31-p33 rules 318-324；`CORE-260330` p14-p15 rules 142-143；`CORE-260330` p77 rule 460；`SOUL-OFAQ-260114` p19-p20 | Starfall 造成致命伤害后，cleanup `LETHAL_DAMAGE` 产生 `UNIT_DESTROYED` 并作为 Poro 条件绝念入队来源 | 替代 / 预防、repeat-until-stable、更多 cleanup 来源和 FAQ 回归 |
| Sad Poro condition draw | `CATALOG` SFD·036/221；`CATALOG` UNL-221/219；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3 | visible base-zone Sad Poro 在同位置无其他友方正面非待命单位时入队，结算后 `CARD_DRAWN` | battlefield objectLocation condition matrix、同位置同时 cleanup timing、更多区域与控制权组合 |
| Loyal Poro condition draw | `CATALOG` UNL-156/219；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3 | visible base-zone Loyal Poro 在同位置有其他友方正面非待命单位且该友方不在 cleanup removal set 中时入队，结算后 `CARD_DRAWN` | 同位置其他友方也同时被 cleanup 摧毁的落单判定未官方化；runtime 当前保守不入队 |
| Hidden / face-down / standby source guard | `CORE-260330` p4-p8 rules 107-129；更精确 FAQ 页码 TODO | hidden / face-down / standby Poro cleanup 路径不入队，不显示 prompt metadata，不抽牌 | face-down 原始触发建模、显露窗口、viewer 级 trigger metadata 全路径 |

## 3. A 验证记录

- Focused：21/21 passed。
- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3358/3358。
- Frontend build：passed。
- Chrome smoke：passed。
- Stage 3 preflight：passed。
- `git diff --check`：passed。

## 4. 仍保留 P0/P1

- P0：完整 trigger engine。
- P0：其他 last-breath / destroyed / friendly-destroyed functional units。
- P0：battlefield objectLocation Poro condition matrix。
- P0：simultaneous cleanup condition timing，尤其同位置其他友方也同时被 cleanup 摧毁时的 Sad / Loyal 判定。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：完整 effect resolution 与 FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18-step E2E。
- P1：trigger batch 正式 DTO、触发来源解释字段、hidden / standby / face-down trigger policy 文档化。

## 5. D 审计结论

4C-9 可作为 `ORDER_TRIGGERS` / trigger engine 的第九个阶段性关闭点：它证明 Sad / Loyal Poro 的条件抽牌在 state-based cleanup lethal damage 路径下可以进入真实触发队列，并经排序 / 入栈 / 优先权结算为 `CARD_DRAWN`。

项目仍 **NOT READY**：本批不覆盖完整 trigger engine、其他 destroyed-family、battlefield objectLocation Poro condition matrix、simultaneous cleanup condition timing、hidden / face-down 原始触发建模、FAQ regression、1009 / 811 full-official 或最终正式 18-step E2E。阶段 4C 仍处于逐 FU、逐测试的小批推进中。
