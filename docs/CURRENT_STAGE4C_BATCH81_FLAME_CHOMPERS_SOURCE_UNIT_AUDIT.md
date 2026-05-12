# Stage 4C-81 Flame Chompers Source Unit Audit

审计日期：2026-05-13
结论：**代表性证据已验证；项目整体仍 NOT READY。**

## 范围

- 代表 FU：`FU-af2c43c430`
- 代表卡：嚼火者手雷 / Flame Chompers `OGN·006/298` / cardId `31210`
- 代表 effect：`OGN_FLAME_CHOMPERS_DISCARD_ALT_PLAY_UNIT`
- 本批是 evidence-only overlay，不修改功能代码；覆盖 ordinary hand `PLAY_CARD`、支付基础 3 mana、0 目标入栈、stack / pass-pass 后源牌进入控制者基地，成为 3 战力 `CARD_TYPE:UNIT` 单位对象。
- 本批同步记录通用 source-unit 带目标打出拒绝，以及官方开局 smoke 中 `OGN·006/298` 作为手牌 / 卡牌候选可见的代表证据。
- 本批不声明卡面“当被弃置时可支付红色符能改为打出到场上”的替代路径、完整 discard replacement / cleanup queue、完整 PaymentEngine、完整 FEPR、隐藏信息 / redaction、18-step E2E 或 full-official 覆盖。

## 证据事实

- `CardBehaviorRegistry` 已登记 `OGN·006/298` 为 direct card behavior：费用 3、0 目标、`OGN_FLAME_CHOMPERS_DISCARD_ALT_PLAY_UNIT`、`PlaysSourceToBaseAsUnit: true`、`SourceUnitPower: 3`。
- `p2-preflight-play-ogn-flame-chompers-discard-static.fixture.json` 覆盖普通主阶段从手牌打出：支付 3、0 目标入栈、双方让过后进入 P1 基地，成为 3 战力、仅带 `CARD_TYPE:UNIT` 标签、未休眠的单位对象。
- `CoreRuleEngineRejectsVanillaSourceUnitWhenTargetsAreProvided` 的 Flame Chompers inline case 覆盖带目标打出拒绝，不让目标驱动本应为 0 目标的普通 source-unit 路径发生变异。
- `OfficialDeckSubmitReadyAndMulliganFlowWorksThroughHub` 作为官方开局 smoke，覆盖该卡在 opening hand / desktop shell card candidate 范围内可见，但不执行弃置替代打出。

## 验证

- focused source-unit / target rejection / official opening regression：306/306 passed。
- source-unit / play-card / target / stack / priority / payment / discard / cleanup adjacent regression：1954/1954 passed。
- backend full：3754/3754 passed。
- frontend build：passed。
- Chrome smoke：passed。

## 非覆盖

不声明 discard replacement path、red power payment during discard replacement、complete replacement / cleanup queue semantics、alternate source-zone play interactions、complete PaymentEngine、complete FEPR target / stack / timing windows、hidden-info / redaction matrix、1009/811 full-official 或 formal 18-step E2E 已完成。
