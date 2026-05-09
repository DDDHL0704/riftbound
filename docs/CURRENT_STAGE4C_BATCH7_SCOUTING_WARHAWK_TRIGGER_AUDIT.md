# 阶段 4C-7 Scouting Warhawk Trigger Enqueue 审计

更新时间：2026-05-10
结论：**NOT READY**

本文记录 D 对阶段 4C-7 Scouting Warhawk explicit destroy real trigger enqueue 最小切片的规则证据与 P0/P1 审计口径。D 本轮只更新用户授权的 checkpoint / audit docs，不修改服务端、前端、覆盖矩阵或 `riftbound-dotnet.sln`。

## 1. 4C-7 关闭的 P0 子项

4C-7 可以关闭以下 P0 子项：

- Scouting Warhawk / 《侦察飞鹰》（`CATALOG` OGN·216/298，`FU-0500c77a70`）explicit destroy real trigger enqueue representative。
- 支撑测试使用 Spirit Fire / 《妖异狐火》（`CATALOG` OGN·256/298）作为 explicit destroy source；本批不是 state cleanup。
- 官方化路径为：explicit destroy `UNIT_DESTROYED` -> visible Scouting Warhawk `SCOUTING_WARHAWK_LAST_BREATH_CALL_RUNE_1` -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` / `RUNES_CALLED`。
- hidden / face-down / standby Warhawk 不入队、不显示 prompt metadata、不触发 `RUNES_CALLED`。
- single-trigger compatibility 保留；既有 `P79ScoutingWarhawk` 测试继续通过。
- 本批无协议 / 前端字段变化，不授予 full-official，不扩完整 trigger engine。

## 2. 规则证据入口

| 规则域 | 证据入口 | 4C-7 审计口径 | 仍需补证 |
| --- | --- | --- | --- |
| Scouting Warhawk last-breath enqueue | `CATALOG` OGN·216/298；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3 | explicit destroy 中 visible Warhawk 绝念召 1 符文进入 `TriggerQueue`、`ORDER_TRIGGERS`、`StackItems`，并在 priority pass 后 `RUNES_CALLED` | state cleanup Warhawk、其他 last-breath / destroyed / friendly-destroyed FUs 与完整 trigger engine |
| Spirit Fire explicit destroy source | `CATALOG` OGN·256/298；`CORE-260330` p39-p42 rules 355-356；`CORE-260330` p62-p63 rule 428 | Spirit Fire 作为 explicit destroy source 支撑本批，不等同 state cleanup 或所有摧毁来源 | 更多 explicit destroy source、替代 / 预防、目标税与 FAQ 回归 |
| Hidden / face-down / standby source guard | `CORE-260330` p4-p8 rules 107-129；更精确 FAQ 页码 TODO | hidden / face-down / standby Warhawk 不入队，不显示 prompt metadata，不触发 `RUNES_CALLED` | face-down 原始触发建模、显露窗口、viewer 级 trigger metadata 全路径 |
| Trigger ordering / stack / priority | `CORE-260330` p33-p35 rules 333-340；`CORE-260330` p52-p55 rules 383.3.d-383.3.e | Warhawk 触发经 `ORDER_TRIGGERS`、入栈和 priority pass 后结算 | 完整 APNAP、可选触发、触发费用、完整 effect resolution |

## 3. A 验证记录

- Focused：9/9 passed。
- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3350/3350。
- Frontend build：passed。
- Chrome smoke：passed。
- Stage 3 preflight：passed。
- `git diff --check`：passed。

## 4. 仍保留 P0/P1

- P0：完整 trigger engine。
- P0：state cleanup Warhawk。
- P0：其他 last-breath / destroyed / friendly-destroyed functional units。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：完整 effect resolution 与 FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18-step E2E。
- P1：trigger batch 正式 DTO、触发来源解释字段、hidden / standby / face-down trigger policy 文档化。

## 5. D 审计结论

4C-7 可作为 `ORDER_TRIGGERS` / trigger engine 的第七个阶段性关闭点：它证明 Scouting Warhawk 的 explicit destroy last-breath 召符文代表路径可以进入真实触发队列并经排序 / 入栈 / 优先权结算。

项目仍 **NOT READY**：本批不覆盖完整 trigger engine、state cleanup Warhawk、其他 destroyed-family、hidden / face-down 原始触发建模、FAQ regression、1009 / 811 full-official 或最终正式 18-step E2E。阶段 4C 仍处于逐 FU、逐测试的小批推进中。
