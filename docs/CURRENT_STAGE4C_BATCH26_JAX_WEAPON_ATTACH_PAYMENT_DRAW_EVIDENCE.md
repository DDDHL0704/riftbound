# 阶段 4C-26 Jax Weapon Attach Payment Draw 证据

更新时间：2026-05-10
结论：**NOT READY**

本文只记录 Jax / 贾克斯 `SFD·119/221`、`SFD·119a/221`、`FU-73f3be35df` 的代表性 weapon attach -> trigger payment -> draw 1 证据。4C-26 不宣称 full-official，不进入 1009 张卡全量实现，不启动最终正式 18-step E2E。

## 证据锚点

| 领域 | 证据 | 4C-26 使用方式 |
|---|---|---|
| 卡牌文本 | 2026-04-27 固定 catalog：`SFD·119/221` 与 `SFD·119a/221` Jax / 贾克斯 | 只取“当你为我贴附武装时，可以选择支付 1，以此抽一张牌”作为代表路径。 |
| Equipment / armament attach | `CORE-260330` p89 rules 718-719；`SOUL-JFAQ-260114` p22-p23 | 本批复用现有 equipment attach route；weapon / armament attach 事件只作为 Jax 触发来源。 |
| Trigger payment | `CORE-260330` p52-p55 rules 377, 403-405；`JFAQ-251023` p2-p4 q2.5 | `TRIGGER_PAYMENT` / `PAY_COST` 代表 payment window，覆盖 pay / decline / insufficient payment rejection。 |
| Draw | `CORE-260330` p57 rule 413 | 支付成功后抽 1。 |
| Hidden information | `CORE-260330` p4-p8 rules 107-129 | hidden / face-down / standby / opponent-controlled source 不触发、不泄漏。 |

## 实现证据

- Representative route：visible face-up Jax source 通过 existing equipment attach route 被贴附 weapon / armament 后，服务端打开 `TRIGGER_PAYMENT` / `PAY_COST` prompt。
- Pay route：`PAY_COST(SPEND_MANA:1)` 后控制者抽 1，并关闭支付窗口。
- Decline route：`PAY_COST(DECLINE)` 关闭支付窗口，不抽牌，不产生其它 mutation。
- Guard route：non-Jax / non-armament no prompt；hidden / face-down / standby / opponent-controlled Jax source 不产生 trigger payment prompt，不泄漏 hidden source metadata；insufficient payment rejected without draw。
- Protocol boundary：本批无新增 protocol / frontend shape，只复用现有 `TRIGGER_PAYMENT` / `PAY_COST`。
- Boundary：本批不覆盖 full Forge / 百炼 / assemble lifecycle、full equipment attachment rules、full optional trigger family / order triggers、full PaymentEngine、draw / replacement / hidden-zone matrix、FAQ regression、1009/811 full-official 或正式 18-step E2E。

## 验证

- Focused backend：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~JaxWeaponAttach|FullyQualifiedName~TriggerPayment"` 通过 37/37。
- Small regression：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~JaxWeaponAttach|FullyQualifiedName~Icevale|FullyQualifiedName~Vayne|FullyQualifiedName~Lux|FullyQualifiedName~SunkenTemple|FullyQualifiedName~BattlefieldConquerGold|FullyQualifiedName~TriggerPayment"` 通过 46/46。
- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 3439/3439。
- Frontend build：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 通过；仅保留既有 SignalR / Rollup `PURE` 注释提示。
- Chrome smoke：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` 通过；覆盖 `/`、`/lobby`、`/decks`、`/cards`、`/rooms/stage3-smoke`、`/matches/stage3-smoke`、`/matches/stage3-smoke/result`。
- Tests added in `TriggerPaymentTests`：`JaxWeaponAttachOpensTriggerPaymentPrompt`、`JaxWeaponAttachPaymentAcceptedDrawsOneAndClosesWindow`、`JaxWeaponAttachPaymentDeclineClosesWithoutDraw`、`JaxWeaponAttachNonJaxOrNonEquipmentDoesNotOpenPayment`、`JaxWeaponAttachHiddenStandbyOrOpponentControlledDoesNotOpenPayment`、`JaxWeaponAttachInsufficientPaymentRejectsWithoutDraw`。

## 仍未关闭

- 完整 Forge / 百炼 / assemble lifecycle、打出时可选装配、减费、已贴附武装选择和装配合法性全矩阵。
- 完整 equipment / weapon / armament attachment rules、控制权、卸除、重贴附、区域归属和 attached top-card matrix。
- 完整 trigger engine、complete APNAP、trigger batch、optional trigger handling、order triggers 与完整 effect resolution。
- 完整 PaymentEngine、triggered-cost 通用模型、Quote / Authorize / Commit、替代 / 额外费用、insufficient / stale / wrong-player / multi-window full matrix。
- draw / replacement / burn-out / hidden-zone visibility / replay redaction 全矩阵。
- hidden / face-down 原始触发建模、viewer-specific metadata 全路径、显露窗口。
- UI/DTO 解释字段、event label / replay redaction、equipment attach UX。
- FAQ regression、1009 entries / 811 functional units full-official、正式 18-step E2E。
