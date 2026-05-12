# Stage 4C-83 Mighty Faerie Source Unit Audit

审计日期：2026-05-13
结论：**代表性证据已验证；项目整体仍 NOT READY。**

## 范围

- 代表 FU：`FU-95b4531e4e`
- 代表卡：大力仙灵 / Mighty Faerie `SFD·125/221` / cardId `33215`
- 代表 effect：`MIGHTY_FAERIE_MOVE_PAYMENT_PLAY_UNIT`
- 本批是 evidence-only overlay，不修改功能代码；覆盖 ordinary hand `PLAY_CARD`、支付基础 4 mana、0 目标入栈、stack / pass-pass 后源牌进入控制者基地，成为 4 战力、带 `仙灵` 标签的 `CARD_TYPE:UNIT` 单位对象。
- 本批同步记录当前普通手牌打出路径携带显式目标时拒绝：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- 本批不声明卡面“移动到战场后可以支付紫色符能并移动另一名受控单位到相同战场”的触发路径、完整 control-zone movement、完整 PaymentEngine、完整 FEPR、隐藏信息 / redaction、18-step E2E 或 full-official 覆盖。

## 证据事实

- `CardBehaviorRegistry` 已登记 `SFD·125/221` 为 direct card behavior：费用 4、0 目标、`MIGHTY_FAERIE_MOVE_PAYMENT_PLAY_UNIT`、`PlaysSourceToBaseAsUnit: true`、`SourceUnitPower: 4`、`SourceUnitTags: 仙灵`。
- `p2-preflight-play-mighty-faerie-move-payment-static.fixture.json` 覆盖普通主阶段从手牌打出：支付 4、0 目标入栈、双方让过后进入 P1 基地，成为 4 战力、带 `CARD_TYPE:UNIT|仙灵` 标签、未休眠的单位对象。
- `p4-play-mighty-faerie-target-rejected.fixture.json` 与 `P4MightyFaerieTargetRejectedFixture` 覆盖带目标打出拒绝，并锁定 no-mutation 边界。
- `CoreRuleEnginePlaysKeywordOnlySourceUnit` / `CoreRuleEngineRejectsKeywordOnlySourceUnitWhenTargetsAreProvided` 的参数化回归覆盖该 source-unit / target rejection 家族。

## 验证

- focused keyword-source-unit / target rejection regression：460/460 passed。
- keyword/source-unit / battlefield / movement / target / stack / priority / payment adjacent regression：2117/2117 passed。
- backend full：3754/3754 passed。
- frontend build：passed。
- Chrome smoke：passed。

## 非覆盖

不声明 move-to-battlefield trigger、optional purple power payment、same-battlefield friendly-unit movement、complete control-zone movement matrix、complete PaymentEngine optional payment semantics、complete FEPR target / stack / timing windows、hidden-info / redaction matrix、1009/811 full-official 或 formal 18-step E2E 已完成。
