# 阶段 4C-8 Scouting Warhawk Cleanup Trigger Enqueue 审计

更新时间：2026-05-10
结论：**NOT READY**

本文记录 D 对阶段 4C-8 Scouting Warhawk state-based cleanup lethal damage real trigger enqueue 最小切片的规则证据与 P0/P1 审计口径。D 本轮只更新用户授权的 checkpoint / audit docs，不修改服务端、前端、覆盖矩阵、E evidence 文件或 `riftbound-dotnet.sln`。

## 1. 4C-8 关闭的 P0 子项

4C-8 可以关闭以下 P0 子项：

- Scouting Warhawk / 《侦察飞鹰》（`CATALOG` OGN·216/298，`FU-0500c77a70`）state-based cleanup lethal damage real trigger enqueue representative。
- 支撑测试使用 Starfall / 《星落》（`CATALOG` OGN·029/298）作为 lethal damage + state-based cleanup source；本批不是 explicit destroy source 的新增覆盖。
- 官方化路径为：Starfall lethal damage -> state-based cleanup `LETHAL_DAMAGE` `UNIT_DESTROYED` -> visible Scouting Warhawk `SCOUTING_WARHAWK_LAST_BREATH_CALL_RUNE_1` -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` / `RUNES_CALLED`。
- hidden / face-down / standby Warhawk cleanup 路径不入队、不显示 prompt metadata、不触发 `RUNES_CALLED`。
- 4C-7 explicit destroy 路径与 single-trigger compatibility 保留。
- 本批无协议 / 前端字段变化，不授予 full-official，不扩完整 trigger engine。

## 2. 规则证据入口

| 规则域 | 证据入口 | 4C-8 审计口径 | 仍需补证 |
| --- | --- | --- | --- |
| State-based cleanup lethal destroy | `CORE-260330` p31-p33 rules 318-324；`CORE-260330` p14-p15 rules 142-143；`CORE-260330` p77 rule 460；`SOUL-OFAQ-260114` p19-p20 | Starfall 造成致命伤害后，cleanup `LETHAL_DAMAGE` 产生 `UNIT_DESTROYED` 并作为 Warhawk 绝念入队来源 | 替代 / 预防、repeat-until-stable、更多 cleanup 来源和 FAQ 回归 |
| Scouting Warhawk last-breath enqueue | `CATALOG` OGN·216/298；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3 | visible Warhawk 在 cleanup 致命伤害摧毁后进入 `TriggerQueue`、`ORDER_TRIGGERS`、`StackItems`，并在 priority pass 后 `RUNES_CALLED` | Sad / Loyal Poro、其他 last-breath / destroyed / friendly-destroyed FUs 与完整 trigger engine |
| Starfall lethal cleanup representative | `CATALOG` OGN·029/298；`CORE-260330` p14-p15 rules 142-143；`CORE-260330` p39-p42 rules 355-356 | Starfall 只作为本批 lethal damage + cleanup 入口代表，不外推为全部伤害 / 清理 / 触发组合完成 | FAQ regression、更多法术 / 技能 / 战斗伤害来源、替代 / 预防交织 |
| Hidden / face-down / standby source guard | `CORE-260330` p4-p8 rules 107-129；更精确 FAQ 页码 TODO | hidden / face-down / standby Warhawk cleanup 路径不入队，不显示 prompt metadata，不触发 `RUNES_CALLED` | face-down 原始触发建模、显露窗口、viewer 级 trigger metadata 全路径 |

## 3. A 验证记录

- Focused：11/11 passed。
- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3352/3352。
- Frontend build：passed。
- Chrome smoke：passed。
- Stage 3 preflight：passed。
- `git diff --check`：passed。

## 4. 仍保留 P0/P1

- P0：完整 trigger engine。
- P0：Sad / Loyal Poro。
- P0：其他 last-breath / destroyed / friendly-destroyed functional units。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：完整 effect resolution 与 FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18-step E2E。
- P1：trigger batch 正式 DTO、触发来源解释字段、hidden / standby / face-down trigger policy 文档化。

## 5. D 审计结论

4C-8 可作为 `ORDER_TRIGGERS` / trigger engine 的第八个阶段性关闭点：它证明 Scouting Warhawk 在 state-based cleanup lethal damage 路径下也能进入真实触发队列，并经排序 / 入栈 / 优先权结算为 `RUNES_CALLED`。

项目仍 **NOT READY**：本批不覆盖完整 trigger engine、Sad / Loyal Poro、其他 destroyed-family、hidden / face-down 原始触发建模、FAQ regression、1009 / 811 full-official 或最终正式 18-step E2E。阶段 4C 仍处于逐 FU、逐测试的小批推进中。
