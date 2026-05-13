# Stage 4C-93 Royal Attendant Legend Mode Evidence Audit

审计日期：2026-05-13
结论：**代表性证据已验证；项目整体仍 NOT READY。**

## 范围

- 代表 FU：`FU-92e31978af`
- 代表卡：皇家随从 / Royal Attendant `SFD·039/221` / cardId `33115`
- 代表 effects：`ROYAL_ATTENDANT_LEGEND_READY_PLAY_UNIT`、`ROYAL_ATTENDANT_READY_LEGEND_PLAY_UNIT`、`ROYAL_ATTENDANT_EXHAUST_LEGEND_PLAY_UNIT`
- 本批是 evidence-only overlay，不修改功能代码；覆盖 ordinary hand `PLAY_CARD`、支付基础 3 费用、选择 `LEGEND` 目标与服务端 mode、stack / pass-pass 后源牌进入控制者基地成为 4 战力 `CARD_TYPE:UNIT` 单位对象，并按 mode 让目标传奇变为活跃或休眠。
- 本批同步记录 prompt mode / target candidates、Hub seed 路径与无效目标拒绝，防止前端或 command 侧自行裁决传奇状态。
- 本批不声明完整 legend interaction domain、非手牌/替代来源打出、完整 PaymentEngine、完整 FEPR target / stack / timing windows、LayerEngine、hidden-info / redaction、1009/811 full-official 或 final READY 覆盖。

## 证据事实

- `CardBehaviorRegistry` 已登记 `SFD·039/221` 为 direct card behavior：费用 3、源单位入场 4 战力，并提供 `READY_LEGEND` / `EXHAUST_LEGEND` 两个 `LEGEND` 目标 mode。
- `p2-preflight-play-royal-attendant-legend-ready-static.fixture.json` 覆盖普通主阶段从手牌打出：选择 P1 传奇、`READY_LEGEND` mode、支付 3、入栈、双方让过后源牌进入 P1 基地，并写入 `UNIT_READIED` 让目标传奇恢复活跃。
- `P79RoyalAttendantReadiesOrExhaustsTargetLegend` 覆盖 `READY_LEGEND` 与 `EXHAUST_LEGEND` 两个 mode 的服务端权威状态变更。
- `P79RoyalAttendantPromptExposesLegendModesAndTargets` 覆盖 `ActionPrompt` 公开两个 mode、`LEGEND` 目标域和双方传奇目标候选。
- `P79RoyalAttendantSeedOffersLegendModesAndReadiesTarget` 覆盖 development Hub seed 路径，和历史 Chrome smoke 使用的前端 server-authoritative READY_LEGEND 流程一致。
- `p4-play-royal-attendant-target-rejected.fixture.json` 覆盖无效显式目标拒绝；拒绝后费用、手牌、基地、stack 与 tick 不发生变更。

## 验证

- focused Royal Attendant / legend mode regression：5/5 passed。
- adjacent legend / source-unit / target / stack / priority / payment regression：1894/1894 passed。
- backend full：3771/3771 passed。

## 非覆盖

不声明完整 legend interaction domain、非手牌或替代来源打出、完整 PaymentEngine、完整 FEPR target / stack / timing windows、LayerEngine、hidden-info / redaction matrix、1009/811 full-official 或 final READY 已完成。
