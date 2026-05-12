# Stage 4C-84 Heimerdinger Source Unit Audit

审计日期：2026-05-13
结论：**代表性证据已验证；项目整体仍 NOT READY。**

## 范围

- 代表 FU：`FU-02075a26e3`
- 代表卡：黑默丁格 / Heimerdinger `ARC-003/006` / cardId `31571`
- 共享卡：黑默丁格 / Heimerdinger `OGN·111/298` / cardId `31329`
- 代表 effects：`ARC_HEIMERDINGER_YORDLE_STATIC_PLAY_UNIT`、`OGN_HEIMERDINGER_YORDLE_TAP_STATIC_PLAY_UNIT`
- 本批是 evidence-only overlay，不修改功能代码；覆盖两张黑默丁格 ordinary hand `PLAY_CARD`、支付基础 3 mana、0 目标入栈、stack / pass-pass 后源牌进入控制者基地，成为 3 战力、带 `约德尔人` 标签的 `CARD_TYPE:UNIT` 单位对象。
- 本批同步记录 active-entry / keyword-source-unit 带目标打出拒绝，以及 `ARC-003/006` 在官方开局 smoke 中作为手牌 / 卡牌候选可见的代表证据。
- 本批不声明卡面“拥有场上其他友方传奇、单位、装备的所有横置技能”的静态复制路径、FAQ 交互、完整 ability-copy model、完整 PaymentEngine、完整 FEPR、隐藏信息 / redaction、18-step E2E 或 full-official 覆盖。

## 证据事实

- `CardBehaviorRegistry` 已登记 `ARC-003/006` 为 direct card behavior：费用 3、0 目标、`ARC_HEIMERDINGER_YORDLE_STATIC_PLAY_UNIT`、`PlaysSourceToBaseAsUnit: true`、`SourceUnitPower: 3`、`SourceUnitTags: 约德尔人`。
- `CardBehaviorRegistry` 已登记 `OGN·111/298` 为 direct card behavior：费用 3、0 目标、`OGN_HEIMERDINGER_YORDLE_TAP_STATIC_PLAY_UNIT`、`PlaysSourceToBaseAsUnit: true`、`SourceUnitPower: 3`、`SourceUnitTags: 约德尔人`。
- `p2-preflight-play-arc-heimerdinger-yordle-static.fixture.json` 覆盖 ARC 普通主阶段从手牌打出：支付 3、0 目标入栈、双方让过后进入 P1 基地，成为 3 战力、带 `CARD_TYPE:UNIT|约德尔人` 标签、未休眠的单位对象。
- `p2-preflight-play-ogn-heimerdinger-yordle-static.fixture.json` 覆盖 OGN 普通主阶段从手牌打出：支付 3、0 目标入栈、双方让过后进入 P1 基地，成为 3 战力、带 `CARD_TYPE:UNIT|约德尔人` 标签、未休眠的单位对象。
- `CoreRuleEngineRejectsActiveEntrySourceUnitWhenTargetsAreProvided` / `CoreRuleEngineRejectsKeywordOnlySourceUnitWhenTargetsAreProvided` 覆盖两张共享条目的带目标打出拒绝。

## 验证

- focused active/keyword source-unit / target rejection / official opening regression：484/484 passed。
- source-unit / target / stack / priority / payment / activated-ability adjacent regression：1847/1847 passed。
- backend full：3754/3754 passed。
- frontend build：passed。
- Chrome smoke：passed。

## 非覆盖

不声明 copied tap skills from other friendly legends / units / equipment、complete static ability-copy model、ability ownership / controller matrix、activated / tap ability PaymentEngine integration、`SOUL-JFAQ-260114 p11/p22` regression beyond citation tracking、shared oracle full-official behavior、complete FEPR target / stack / timing windows、hidden-info / redaction matrix、1009/811 full-official 或 formal 18-step E2E 已完成。
