# 阶段 4C-10 Unsung Hero Cleanup Trigger Enqueue 审计

更新时间：2026-05-10
结论：**NOT READY**

本文记录 D 对阶段 4C-10 Unsung Hero powerful last-breath draw-2 state-based cleanup real trigger enqueue 最小切片的规则证据与 P0/P1 审计口径。D 本轮只更新用户授权的 checkpoint / audit docs，不修改服务端、前端、覆盖矩阵、E evidence 文件或 `riftbound-dotnet.sln`。

## 1. 4C-10 关闭的 P0 子项

4C-10 可以关闭以下 P0 子项：

- Unsung Hero / 《无名英雄》（`CATALOG` SFD·167/221，`FU-1701d1d89a`）`UNSUNG_HERO_LAST_BREATH_DRAW_2_IF_POWERFUL` state-based cleanup real trigger enqueue representative。
- 支撑来源为 Starfall / 《星落》（`CATALOG` OGN·029/298）造成 lethal damage 后的 state-based cleanup。
- 官方化路径为：Starfall lethal damage -> state-based cleanup `LETHAL_DAMAGE` `UNIT_DESTROYED` -> visible base-zone Unsung Hero power >= 5 -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` / `CARD_DRAWN` x2。
- power < 5 cleanup 路径不入队、不抽牌。
- hidden / face-down / standby Unsung Hero cleanup 路径不入队、不显示 prompt metadata、不抽牌。
- 现有 explicit destroy P79 Unsung immediate compatibility 保留。
- 严格边界：本批只用 `CardObjectState.Power >= 5` 代表强力；不覆盖 LayerEngine / effective power / temporary modifier；不覆盖 battlefield objectLocation 全矩阵；不迁移 explicit destroy。
- 本批无协议 / 前端字段变化，不授予 full-official，不扩完整 trigger engine。

## 2. 规则证据入口

| 规则域 | 证据入口 | 4C-10 审计口径 | 仍需补证 |
| --- | --- | --- | --- |
| State-based cleanup lethal destroy | `CORE-260330` p31-p33 rules 318-324；`CORE-260330` p14-p15 rules 142-143；`CORE-260330` p77 rule 460；`SOUL-OFAQ-260114` p19-p20 | Starfall 造成致命伤害后，cleanup `LETHAL_DAMAGE` 产生 `UNIT_DESTROYED` 并作为 Unsung Hero 绝念入队来源 | 替代 / 预防、repeat-until-stable、更多 cleanup 来源和 FAQ 回归 |
| Unsung Hero powerful last-breath draw | `CATALOG` SFD·167/221；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3 | visible base-zone Unsung Hero 在 `CardObjectState.Power >= 5` 时入队，priority pass 后 `TRIGGER_RESOLVED` / `CARD_DRAWN` x2 | effective power / LayerEngine、temporary modifier、battlefield objectLocation 全矩阵 |
| Power threshold guard | `CATALOG` SFD·167/221；`CORE-260330` p57 rule 413.4；相关强力证据见 `rules-evidence-index.md` strong/powerful fixtures | power < 5 cleanup 路径不入队、不抽牌；本批不把派生 `effectivePower` 当作完整官方强力裁决 | 完整 LayerEngine、持续效果层、时间戳、依赖与临时修正重算 |
| Hidden / face-down / standby source guard | `CORE-260330` p4-p8 rules 107-129；更精确 FAQ 页码 TODO | hidden / face-down / standby Unsung Hero cleanup 路径不入队，不显示 prompt metadata，不抽牌 | face-down 原始触发建模、显露窗口、viewer 级 trigger metadata 全路径 |

## 3. A 验证记录

- Focused：21/21 passed。
- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3361/3361。
- Frontend build：passed。
- Chrome smoke：passed。
- Stage 3 preflight：passed。
- `git diff --check`：passed。

## 4. 仍保留 P0/P1

- P0：完整 trigger engine。
- P0：其他 last-breath / destroyed / friendly-destroyed functional units。
- P0：effective power / LayerEngine、temporary modifier 和完整强力判定。
- P0：battlefield objectLocation matrix。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：完整 effect resolution 与 FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18-step E2E。
- P1：trigger batch 正式 DTO、触发来源解释字段、hidden / standby / face-down trigger policy 文档化。

## 5. D 审计结论

4C-10 可作为 `ORDER_TRIGGERS` / trigger engine 的第十个阶段性关闭点：它证明 Unsung Hero 的 powerful last-breath draw-2 在 state-based cleanup lethal damage 路径下可以进入真实触发队列，并经排序 / 入栈 / 优先权结算为两次抽牌。

项目仍 **NOT READY**：本批不覆盖完整 trigger engine、其他 destroyed-family、effective power / LayerEngine、temporary modifier、battlefield objectLocation 全矩阵、hidden / face-down 原始触发建模、FAQ regression、1009 / 811 full-official 或最终正式 18-step E2E。阶段 4C 仍处于逐 FU、逐测试的小批推进中。
