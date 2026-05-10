# 阶段 4C-25 Icevale Archer Attack Payment 审计

更新时间：2026-05-10
结论：**NOT READY**

本文记录 Icevale Archer / 冰谷弓箭手 `UNL-065/219` / `FU-c170628e3a` 的规则证据与 P0/P1 审计口径。本批只关闭一个极窄代表性服务端切片：active start-battle task 下 visible face-up Icevale 作为攻击者，使用现有 `DeclareBattleCommand.BattlefieldTargetObjectIds` 预选同一 battlefield 的正面单位目标；战斗声明后打开 `TRIGGER_PAYMENT` / `PAY_COST`，支付 1 后目标本回合 power -1，拒付不变更。它不代表 full-official，不得标记 READY / READY-CANDIDATE，不启动最终 18-step E2E。

配套证据文档：`docs/CURRENT_STAGE4C_BATCH25_ICEVALE_ARCHER_ATTACK_PAYMENT_EVIDENCE.md`。

## 本批范围

- A 已决定 4C-25 收 Icevale Archer / 冰谷弓箭手 `UNL-065/219` / `FU-c170628e3a` 的 attack trigger payment representative slice。
- 官方文本入口：2026-04-27 catalog 中 `UNL-065/219` 写明“当我进攻时，你可以选择支付 1，以此让此处的一名单位在本回合内 -1”。
- 代表路径：active start-battle task -> visible face-up Icevale is declared as attacker -> `DeclareBattleCommand.BattlefieldTargetObjectIds` preselects a visible face-up unit on the same battlefield -> open existing `TRIGGER_PAYMENT` / `PAY_COST` prompt -> `PAY_COST(SPEND_MANA:1)` -> target gets `POWER_MODIFIED_UNTIL_END_OF_TURN:-1`。
- decline 路径：`PAY_COST(DECLINE)` 关闭支付窗口，目标不获得 -1，权威状态不发生目标战力 mutation。
- guard：invalid target、hidden / face-down / standby / opponent-controlled Icevale source 均 no trigger / no leak / no mutation。
- 本批不实现 full attack-trigger family、完整 target selection prompt、支付后恢复战斗时点、Spellshield target tax、LayerEngine、full PaymentEngine、1009/811 full-official 或最终 18-step E2E。

## 规则证据入口

| 规则域 | 证据入口 | 当前 4C-25 状态 | 仍缺 |
|---|---|---|---|
| Icevale Archer attack payment | `CATALOG` `UNL-065/219`；FU `FU-c170628e3a` | 已记录：active start-battle task 下 visible face-up Icevale 作为攻击者后打开 `TRIGGER_PAYMENT` / `PAY_COST`，支付 1 后同处目标本回合 -1，拒付不变更 | full attack-trigger family、其它 attack / defense triggers、完整 combat timing |
| Battle / attack timing | `CORE-260330` p33-p35 rules 333-340；p77-p78 rules 454-464；`JFAQ-251023` p2-p4 q2.2-q2.4 | 已记录：只使用现有 start-battle / declare battle representative task 作为 attack trigger 来源 | 完整 battle task lifecycle、支付后恢复战斗时点、battle response window、damage assignment 交织 |
| Trigger payment | `CORE-260330` p52-p55 rules 377, 403-405；`JFAQ-251023` p2-p4 q2.5 | 已记录：复用现有 `TRIGGER_PAYMENT` / `PAY_COST`，覆盖 spend / decline 代表路径 | Quote / Authorize / Commit、替代 / 额外费用、insufficient / stale / multi-window full matrix |
| Target selection / same battlefield | `CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p39-p42 rules 355-356 | 已记录：用 `DeclareBattleCommand.BattlefieldTargetObjectIds` 预选同一 battlefield 的正面单位目标，并覆盖 invalid target guard | 完整 target selection prompt、目标重选、目标失效、Spellshield target tax、同处 / 控制权 / 可见性全矩阵 |
| Temporary power modifier | `CORE-260330` p14-p15 rules 142-143；p31-p33 rules 318-324 | 已记录：支付成功后目标获得本回合 power -1 代表修正 | LayerEngine、timestamp / dependency、end-of-turn cleanup duration 全矩阵 |
| Hidden / invalid source guard | `CORE-260330` p4-p8 rules 107-129 | 已记录：hidden / face-down / standby / opponent-controlled source no trigger / no leak | face-down 原始触发建模、viewer-specific metadata 全路径、显露窗口 |

## 验证记录

- Focused backend：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Icevale|FullyQualifiedName~AttackPayment|FullyQualifiedName~TriggerPayment|FullyQualifiedName~DeclareBattle|FullyQualifiedName~Vayne|FullyQualifiedName~Lux"` 通过 102/102。
- JSON / diff hygiene：`jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 通过；`git diff --check` 通过。
- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 3429/3429。
- Frontend build：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 通过。
- Chrome smoke：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` 通过。
- 本批未记录 Stage 3 preflight；不得用 focused backend 或 smoke 替代最终正式 18-step E2E。
- D 本轮只更新 docs 审计 / 证据 / checkpoint 文档；A 后续对齐 coverage matrix JSON、baseline / risk / freeze 文档口径；不修改前端或 `riftbound-dotnet.sln`。

## 关闭项

- `UNL-065/219` / `FU-c170628e3a` 的 active start-battle visible face-up attacker -> `TRIGGER_PAYMENT` / `PAY_COST` representative baseline 已记录。
- `PAY_COST(SPEND_MANA:1)` 后同一 battlefield 目标本回合 power -1 的代表路径已记录。
- `PAY_COST(DECLINE)` 不修改目标战力的代表路径已记录。
- invalid target、hidden / face-down / standby / opponent-controlled source guard 已记录。

## 停止条件

- 不把 4C-25 标记为 full-official、READY 或 READY-CANDIDATE。
- 不因 Icevale 代表路径外推 full attack-trigger family、完整 target selection prompt、支付后恢复战斗时点、Spellshield target tax、LayerEngine 或 full PaymentEngine。
- 如后续需要完整 battle task lifecycle、独立目标选择 prompt、目标重选 / 目标失效、Spellshield tax 或 LayerEngine timestamp / dependency，应另开专项，不在本批补做。

## 仍存在 P0/P1

- P0：完整 spell duel / battle lifecycle、start-battle task、battle response window、damage assignment 与支付窗口恢复时点仍未完成。
- P0：完整 PaymentEngine、triggered-cost 通用模型、Quote / Authorize / Commit、替代 / 额外费用、insufficient / stale / multi-window full matrix。
- P0：完整 trigger engine、complete APNAP / trigger batch、optional trigger handling、attack-trigger family 与完整 effect resolution。
- P0：完整 target selection prompt、same-battlefield target matrix、target invalidation、Spellshield target tax。
- P0：完整 LayerEngine、temporary modifier timestamp / dependency / cleanup duration matrix。
- P0：hidden / face-down 原始触发建模、viewer-specific metadata 全路径、显露窗口。
- P0：FAQ regression、1009 entries / 811 functional units full-official、正式 18-step E2E 与 completion audit。
- P1：Icevale attack payment 的 UI/DTO 解释字段、战斗恢复 UX、event label / replay redaction 仍需后续全矩阵证据。
