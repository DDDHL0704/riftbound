# 阶段 4C-24 Vayne Conquer Recall 审计

更新时间：2026-05-10
结论：**NOT READY**

本文记录 Vayne / 薇恩 `OGN·035/298` / `FU-c027639a3c` 的规则证据与 P0/P1 审计口径。本批只关闭一个极窄代表性服务端切片：visible face-up Vayne 征服战场后打开现有 `TRIGGER_PAYMENT` / `PAY_COST`，支付 1 后返回 owner hand，拒付不变更。它不代表 full-official，不得标记 READY / READY-CANDIDATE，不启动最终 18-step E2E。

配套证据文档：`docs/CURRENT_STAGE4C_BATCH24_VAYNE_CONQUER_RECALL_EVIDENCE.md`。

## 本批范围

- A 已决定 4C-24 收 Vayne / 薇恩 `OGN·035/298` / `FU-c027639a3c` 的征服支付回手代表切片。
- 官方文本入口：2026-04-27 catalog 中 `OGN·035/298` 写明 Vayne 有 `强攻3`、对手已控制任意战场时活跃进场，以及“每当我征服一处战场时，你可以选择支付 1 来让我返回所属的手牌”。
- 规则路径：visible face-up Vayne conquers a battlefield -> open existing `TRIGGER_PAYMENT` / `PAY_COST` prompt -> `PAY_COST(SPEND_MANA:1)` -> Vayne returns to owner hand；`PAY_COST(DECLINE)` closes the payment window with no return / no mutation.
- hidden / face-down / standby / opponent-controlled Vayne source must not trigger and must not leak prompt metadata.
- 本批不实现 full Assault3、active-entry、完整 conquer/control-zone matrix、完整 PaymentEngine 或最终 18-step E2E。

## 规则证据入口

| 规则域 | 证据入口 | 当前 4C-24 状态 | 仍缺 |
|---|---|---|---|
| Vayne conquer recall | `CATALOG` `OGN·035/298`；FU `FU-c027639a3c` | 已记录：visible face-up Vayne 征服后打开 `TRIGGER_PAYMENT` / `PAY_COST`，支付 1 后返回 owner hand，拒付不变更 | full Assault3、active-entry、所有 Vayne variants / conquer family |
| Battlefield conquer timing | `CORE-260330` p77-p78 rules 454-464；`JFAQ-251023` p6-p7 q5.1-q5.4 | 已记录：只使用现有代表性 conquer event 作为触发来源 | 完整 control freeze/release、held/conquer scoring order、battle cleanup 全矩阵 |
| Trigger payment | `CORE-260330` p52-p55 rules 377, 403-405；`JFAQ-251023` p2-p4 q2.5 | 已记录：复用现有 `TRIGGER_PAYMENT` / `PAY_COST`，覆盖 spend / decline 代表路径 | Quote / Authorize / Commit、替代 / 额外费用、insufficient / stale / multi-window full matrix |
| Zone movement / owner hand | `CORE-260330` p4-p8 rules 107-129；p57-p59 rules 413-416 | 已记录：支付成功后源 Vayne 移动到 owner hand | controller / owner 复杂变化、public/private hand visibility、return timing matrix |
| Hidden / invalid source guard | `CORE-260330` p4-p8 rules 107-129 | 已记录：hidden / face-down / standby / opponent-controlled source no trigger / no leak | face-down 原始触发建模、viewer-specific metadata 全路径、显露窗口 |

## 验证记录

- Focused backend：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Vayne|FullyQualifiedName~BattlefieldConquer|FullyQualifiedName~TriggerPayment|FullyQualifiedName~PAY_COST"` 通过 52/52。
- 本批未记录 backend full / frontend build / Chrome smoke / Stage 3 preflight；不得用 focused backend 替代最终正式 18-step E2E。
- D 本轮只更新 docs 审计 / 证据 / checkpoint 文档；不修改服务端、前端、coverage matrix JSON、baseline/risk 文档或 `riftbound-dotnet.sln`。

## 关闭项

- `OGN·035/298` / `FU-c027639a3c` 的 visible face-up conquer -> `TRIGGER_PAYMENT` / `PAY_COST` representative baseline 已记录。
- `PAY_COST(SPEND_MANA:1)` 后 Vayne 返回 owner hand 的代表路径已记录。
- `PAY_COST(DECLINE)` 不返回、不变更的代表路径已记录。
- hidden / face-down / standby / opponent-controlled source no trigger / no leak guard 已记录。

## 停止条件

- 不把 4C-24 标记为 full-official、READY 或 READY-CANDIDATE。
- 不因 Vayne 代表路径外推完整 Assault3、active-entry、complete conquer/control-zone matrix、full PaymentEngine 或其它 Vayne variants。
- 如后续需要 battle/control freeze、complete conquer scoring、owner/controller 复杂变化、public/private hand visibility 或 full PaymentEngine，应另开专项，不在本批补做。

## 仍存在 P0/P1

- P0：完整 battlefield / control / conquer lifecycle、control freeze/release、held/conquer scoring order 与 battle cleanup 全矩阵。
- P0：完整 PaymentEngine、triggered-cost 通用模型、Quote / Authorize / Commit、替代 / 额外费用、insufficient / stale / multi-window full matrix。
- P0：完整 trigger engine、complete APNAP / trigger batch、optional trigger handling 与完整 effect resolution。
- P0：hidden / face-down 原始触发建模、viewer-specific metadata 全路径、显露窗口。
- P0：FAQ regression、1009 entries / 811 functional units full-official、正式 18-step E2E 与 completion audit。
- P1：Vayne Assault3 combat modifier、active-entry condition、owner/controller return-hand matrix、hand visibility / replay redaction 仍需后续全矩阵证据。
