# 阶段 4C-23 Lux High-Cost Spell Power 审计

更新时间：2026-05-10
结论：**NOT READY**

本文记录 Lux / 拉克丝 `OGS·006/024` / `FU-f18a49e06d` 的规则证据与 P0/P1 审计口径。本批只关闭一个代表性服务端切片：visible face-up Lux 由其控制者打出费用不低于 5 的法术后，服务端记录触发并让 Lux 本回合战力 +3。它不代表 full-official，不得标记 READY / READY-CANDIDATE，不启动最终 18-step E2E。

配套证据文档：`docs/CURRENT_STAGE4C_BATCH23_LUX_HIGH_COST_SPELL_POWER_EVIDENCE.md`。

## 本批范围

- A 已决定 4C-23 收 Lux / 拉克丝 `OGS·006/024` / `FU-f18a49e06d`，而不是 Aphelios / `FU-67c6b0186e`。理由是 Aphelios 的武装贴附三模式、本回合未选记忆、符文/增益选择会牵出新 prompt 与模式记忆设计；Lux 更适合稳定代表切片。
- 官方文本入口：2026-04-27 catalog 中 `OGS·006/024` 写明“每当你打出费用不低于 5 的法术时，让我本回合内战力 +3”。
- 规则路径：controller 成功打出 cost >= 5 spell -> visible face-up Lux source on field/base -> `TRIGGER_QUEUED` / `TRIGGER_RESOLVED` compatibility events -> `POWER_MODIFIED_UNTIL_END_OF_TURN` +3。
- 本批保留现有 single-trigger immediate compatibility 事件路线，不宣称完整 `ORDER_TRIGGERS` / stack route；多触发和可选触发仍为后续 P0。
- low-cost spell、opponent spell、face-down Lux、standby Lux、source not on field 均不得触发。

## 规则证据入口

| 规则域 | 证据入口 | 当前 4C-23 状态 | 仍缺 |
|---|---|---|---|
| Lux high-cost spell trigger | `CATALOG` `OGS·006/024`；FU `FU-f18a49e06d` | 已记录：控制者打出 cost >= 5 spell 后通过 compatibility trigger events 让 Lux 本回合 +3 | 所有高费法术触发族、optional trigger、同时多触发 |
| Spell play / cost threshold | `CORE-260330` p9；p39-p42 rules 355-356 | 使用 printed behavior mana cost >= 5 的代表判断 | paid-cost override、增减费、额外费用、替代费用全矩阵 |
| Temporary power duration | `CORE-260330` p14-p15 rules 142-143；p31-p33 rules 318-324 | 通过 `UntilEndOfTurnPowerModifier` 与 `POWER_MODIFIED_UNTIL_END_OF_TURN` 记录 | LayerEngine、timestamp/dependency、回合结束全持续效果矩阵 |
| Trigger visibility guard | `CORE-260330` p4-p8 rules 107-129 | face-down / standby / invalid source no trigger / no leak | face-down 原始触发建模、显露窗口、viewer-specific metadata 全路径 |
| Trigger engine | `CORE-260330` p33-p35 rules 333-340；p52-p55 rules 383.3.d-383.3.e | 本批只记录 `TRIGGER_QUEUED` / `TRIGGER_RESOLVED` compatibility events representative event | 完整 `TriggerInstance`、batch、APNAP、optional trigger handling |

## 验证记录

- Focused backend：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Lux|FullyQualifiedName~HighCostSpell|FullyQualifiedName~Ravenbloom|FullyQualifiedName~RealTriggerQueue"` 通过 67/67。
- 本批修正测试构造：`LuxOpponentHighCostSpellDoesNotTrigger` 使用 P2 手牌中的 `P2-SPELL-EVOLUTION-DAY`，确保 opponent-spell negative 覆盖真实通过。
- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 3413/3413。
- Frontend build：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 通过。
- Chrome smoke：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` 通过。
- Stage 3 preflight 本批未运行；不得用本批 focused / backend full / smoke 替代正式 18-step E2E。

## 关闭项

- `OGS·006/024` / `FU-f18a49e06d` 的 visible face-up high-cost spell trigger representative baseline 已记录。
- `POWER_MODIFIED_UNTIL_END_OF_TURN` +3 与 `UntilEndOfTurnPowerModifier` +3 的代表路径已记录。
- low-cost spell、opponent spell、face-down / standby / invalid source no trigger guard 已记录。

## 停止条件

- 不把 4C-23 标记为 full-official、READY 或 READY-CANDIDATE。
- 不因 Lux 代表路径外推 Aphelios、Icevale Archer、Vayne 或其他 triggered-cost / spell-played trigger FUs。
- 如后续需要 paid-cost override、完整 PaymentEngine、LayerEngine 或 optional trigger handling，应另开专项，不在本批补做。

## 仍存在 P0/P1

- P0：完整 trigger engine、complete APNAP / trigger batch、optional trigger handling 与完整 effect resolution。
- P0：完整 PaymentEngine、paid-cost vs printed-cost 判定、增减费 / 额外费用 / 替代费用 full matrix。
- P0：完整 LayerEngine、temporary modifier timestamp / dependency / cleanup duration matrix。
- P0：hidden / face-down 原始触发建模、viewer-specific metadata 全路径、显露窗口。
- P0：FAQ regression、1009 entries / 811 functional units full-official、正式 18-step E2E 与 completion audit。
- P1：Lux high-cost spell family、其它 spell-played temporary power FUs 与 multi-trigger ordering 仍需后续全矩阵证据。
