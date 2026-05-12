# Stage 4C-92 Stern Sergeant Experience Evidence Audit

审计日期：2026-05-13
结论：**代表性证据已验证；项目整体仍 NOT READY。**

## 范围

- 代表 FU：`FU-5f03740098`
- 代表卡：严厉军士 / Stern Sergeant `UNL-157/219` / cardId `34705`
- 代表 effect：`STERN_SERGEANT_PLAY_UNIT_GAIN_EXPERIENCE_STATIC`
- 本批是 evidence-only overlay，不修改功能代码；覆盖 ordinary hand `PLAY_CARD`、支付基础 6 费用、0 目标入栈、stack / pass-pass 后源牌进入控制者基地，成为 6 战力、带 `CARD_TYPE:UNIT|精锐` 标签的单位对象，并在结算后按控制者场上友方单位数量获得经验。
- 本批同步记录带目标打出拒绝，防止 0 目标经验单位路径被 target input 驱动变异。
- 本批不声明战斗/移动触发经验、经验消耗技能、完整 experience economy、完整 PaymentEngine、完整 FEPR target / stack / timing windows、LayerEngine、hidden-info / redaction、1009/811 full-official 或 final READY 覆盖。

## 证据事实

- `CardBehaviorRegistry` 已登记 `UNL-157/219` 为 direct card behavior：费用 6、0 目标、`STERN_SERGEANT_PLAY_UNIT_GAIN_EXPERIENCE_STATIC`、`PlaysSourceToBaseAsUnit: true`、`SourceUnitPower: 6`、`SourceUnitTags: 精锐`、`GainExperienceOnPlayPerFriendlyFieldUnit: 1`。
- `p2-preflight-play-stern-sergeant-experience-static.fixture.json` 覆盖普通主阶段从手牌打出：支付 6、0 目标入栈、双方让过后源牌进入 P1 基地，成为 6 战力 `CARD_TYPE:UNIT|精锐` 单位对象；自身结算后位于场上，因此获得 1 经验。
- `p4-play-stern-sergeant-dynamic-experience.fixture.json` 覆盖已有友方单位计数：结算后严厉军士自身 + 两名既有友方场上单位共 3 名友方单位，因此获得 3 经验；友方装备和敌方单位不计入。
- `CoreRuleEngineRejectsKeywordOnlySourceUnitWhenTargetsAreProvided` 覆盖带目标打出拒绝；拒绝后费用、手牌、基地、stack 与 tick 不发生变更。

## 验证

- focused Stern Sergeant / keyword source-unit regression：460/460 passed。
- adjacent experience / source-unit / target / stack / priority / payment regression：1913/1913 passed。
- backend full：3771/3771 passed。

## 非覆盖

不声明战斗/移动触发经验、经验消耗技能、完整 experience economy、完整 PaymentEngine、完整 FEPR target / stack / timing windows、LayerEngine、hidden-info / redaction matrix、1009/811 full-official 或 final READY 已完成。
