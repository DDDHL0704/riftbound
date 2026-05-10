# 阶段 4C-26 Jax Weapon Attach Payment Draw 审计

更新时间：2026-05-10
结论：**NOT READY**

本文记录 Jax / 贾克斯 `SFD·119/221`、`SFD·119a/221` / `FU-73f3be35df` 的规则证据与 P0/P1 审计口径。本批只关闭一个极窄代表性服务端切片：visible face-up Jax 通过现有 equipment attach route 被贴附 weapon / armament 后，打开既有 `TRIGGER_PAYMENT` / `PAY_COST`；支付 1 后抽 1，拒付关闭窗口且不抽牌、不变更。它不代表 full-official，不得标记 READY / READY-CANDIDATE，不启动最终 18-step E2E。

配套证据文档：`docs/CURRENT_STAGE4C_BATCH26_JAX_WEAPON_ATTACH_PAYMENT_DRAW_EVIDENCE.md`。

## 本批范围

- 4C-26 收 Jax / 贾克斯 `SFD·119/221` 与 `SFD·119a/221`、`FU-73f3be35df` 的 weapon attach payment draw representative slice。
- 官方文本入口：2026-04-27 catalog 中 `SFD·119/221` 与 `SFD·119a/221` 均写明“当你为我贴附武装时，可以选择支付 1，以此抽一张牌”。
- 代表路径：visible face-up Jax source -> weapon / armament via existing equipment attach route becomes attached to Jax -> open existing `TRIGGER_PAYMENT` / `PAY_COST` prompt -> `PAY_COST(SPEND_MANA:1)` -> controller draws 1。
- decline 路径：`PAY_COST(DECLINE)` 关闭支付窗口，不抽牌，不产生其它权威状态 mutation。
- guard：non-Jax / non-armament no prompt；hidden / face-down / standby / opponent-controlled Jax source no trigger / no leak；insufficient payment rejected without draw。
- 本批不新增 protocol / frontend shape，不实现 full Forge / 百炼 / assemble lifecycle、full equipment attachment rules、full optional trigger family / order triggers、full PaymentEngine、draw / replacement / hidden-zone matrix、FAQ regression、1009/811 full-official 或最终 18-step E2E。

## 规则证据入口

| 规则域 | 证据入口 | 当前 4C-26 状态 | 仍缺 |
|---|---|---|---|
| Jax weapon attach payment draw | `CATALOG` `SFD·119/221`、`SFD·119a/221`；FU `FU-73f3be35df` | 已记录：visible face-up Jax 被贴附 weapon / armament 后打开 `TRIGGER_PAYMENT` / `PAY_COST`，支付 1 抽 1，拒付不变更 | full Jax / Forge / 百炼 official，完整 attach timing |
| Equipment / armament attach | `CORE-260330` p89 rules 718-719；`SOUL-JFAQ-260114` p22-p23 | 已记录：只复用现有 equipment attach route 与 `EQUIPMENT_ATTACHED` 代表事件作为触发来源 | 完整装备控制权、卸除、重贴附、区域归属、attached top-card matrix |
| Trigger payment | `CORE-260330` p52-p55 rules 377, 403-405；`JFAQ-251023` p2-p4 q2.5 | 已记录：复用现有 `TRIGGER_PAYMENT` / `PAY_COST`，覆盖 spend / decline / insufficient 代表路径 | Quote / Authorize / Commit、替代 / 额外费用、stale / wrong-player / multi-window full matrix |
| Draw | `CORE-260330` p57 rule 413 | 已记录：支付成功后控制者抽 1 | burn-out / replacement / hidden-zone / replay redaction 全矩阵 |
| Hidden / invalid source guard | `CORE-260330` p4-p8 rules 107-129 | 已记录：hidden / face-down / standby / opponent-controlled Jax source no trigger / no leak；non-Jax / non-armament no prompt | face-down 原始触发建模、viewer-specific metadata 全路径、显露窗口 |

## 验证记录

- Focused backend：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~JaxWeaponAttach|FullyQualifiedName~TriggerPayment"` 通过 37/37。
- Small regression：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~JaxWeaponAttach|FullyQualifiedName~Icevale|FullyQualifiedName~Vayne|FullyQualifiedName~Lux|FullyQualifiedName~SunkenTemple|FullyQualifiedName~BattlefieldConquerGold|FullyQualifiedName~TriggerPayment"` 通过 46/46。
- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 3439/3439。
- Frontend build：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 通过；仅保留既有 SignalR / Rollup `PURE` 注释提示。
- Chrome smoke：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` 通过；覆盖 `/`、`/lobby`、`/decks`、`/cards`、`/rooms/stage3-smoke`、`/matches/stage3-smoke`、`/matches/stage3-smoke/result`。
- Tests added in `TriggerPaymentTests`：`JaxWeaponAttachOpensTriggerPaymentPrompt`、`JaxWeaponAttachPaymentAcceptedDrawsOneAndClosesWindow`、`JaxWeaponAttachPaymentDeclineClosesWithoutDraw`、`JaxWeaponAttachNonJaxOrNonEquipmentDoesNotOpenPayment`、`JaxWeaponAttachHiddenStandbyOrOpponentControlledDoesNotOpenPayment`、`JaxWeaponAttachInsufficientPaymentRejectsWithoutDraw`。
- 本批未记录 Stage 3 preflight；不得用 focused / small regression / Chrome smoke 替代最终正式 18-step E2E。
- D 本轮只更新 docs 审计 / 证据 / 索引 / TODO 文档；不修改 A checkpoint、coverage matrix JSON、baseline / risk / freeze 文档、服务端、前端或 `riftbound-dotnet.sln`。

## 关闭项

- `SFD·119/221`、`SFD·119a/221` / `FU-73f3be35df` 的 visible face-up Jax weapon attach -> `TRIGGER_PAYMENT` / `PAY_COST` representative baseline 已记录。
- `PAY_COST(SPEND_MANA:1)` 后抽 1 的代表路径已记录。
- `PAY_COST(DECLINE)` 关闭窗口且不抽牌、不变更的代表路径已记录。
- non-Jax / non-armament、hidden / face-down / standby / opponent-controlled source、insufficient payment guard 已记录。

## 停止条件

- 不把 4C-26 标记为 full-official、READY 或 READY-CANDIDATE。
- 不因 Jax 代表路径外推 full Forge / 百炼 / assemble lifecycle、full equipment attachment rules、full optional trigger family / order triggers 或 full PaymentEngine。
- 不声明新的 protocol / frontend shape；本批只复用既有 `TRIGGER_PAYMENT` / `PAY_COST`。
- 如后续需要完整百炼装配、贴附目标 prompt、装备控制权/卸除/重贴附、burn-out / replacement draw matrix 或 FAQ regression，应另开专项，不在本批补做。

## 仍存在 P0/P1

- P0：完整 Forge / 百炼 / assemble lifecycle、打出时可选装配、减费、已贴附武装选择和装配合法性全矩阵仍未完成。
- P0：完整 equipment / weapon / armament attachment rules、控制权、卸除、重贴附、区域归属和 attached top-card matrix 仍未完成。
- P0：完整 trigger engine、complete APNAP / trigger batch、optional trigger handling、order triggers 与完整 effect resolution。
- P0：完整 PaymentEngine、triggered-cost 通用模型、Quote / Authorize / Commit、替代 / 额外费用、insufficient / stale / wrong-player / multi-window full matrix。
- P0：draw / replacement / burn-out / hidden-zone visibility / replay redaction 全矩阵。
- P0：hidden / face-down 原始触发建模、viewer-specific metadata 全路径、显露窗口。
- P0：FAQ regression、1009 entries / 811 functional units full-official、正式 18-step E2E 与 completion audit。
- P1：Jax weapon attach payment 的 UI/DTO 解释字段、event label / replay redaction 和 equipment attach UX 仍需后续全矩阵证据。
