# Stage 4C-77 Syndra Spell Duel Echo Static Audit

审计日期：2026-05-13
结论：**代表性证据已验证；项目整体仍 NOT READY。**

## 范围

- 代表 FU：`FU-bf350b5796`
- 代表卡：辛德拉 / Syndra `UNL-146/219` / cardId `34691`
- 代表 effect：`SYNDRA_SPELL_DUEL_ECHO_STATIC`
- 本批是 evidence-only overlay，不修改功能代码；覆盖 ordinary hand `PLAY_CARD`、支付 6 mana、0 目标入栈、stack / pass-pass 后源牌进入控制者基地成为 6 战力、无额外标签的 `CARD_TYPE:UNIT` 单位对象，并记录共享 source-unit 带目标拒绝证据。
- 本批不声明实际法术对决检测、授予法术回响 2 紫色、支付授予的回响并重复法术效果、完整 Spell Duel lifecycle、完整 PaymentEngine、LayerEngine 或 full-official 覆盖。

## 证据事实

- `CardBehaviorRegistry` 已登记 `UNL-146/219` 为 direct card behavior：费用 6、0 目标、`PlaysSourceToBaseAsUnit: true`、`SourceUnitPower: 6`。
- `p2-preflight-play-syndra-spell-duel-echo-static.fixture.json` 覆盖普通主阶段从手牌打出：支付 6、0 目标入栈、双方让过后《辛德拉》进入 P1 基地成为 6 战力 `CARD_TYPE:UNIT` 单位对象。
- fixture 的官方卡面说明明确记录：法术对决检测、回响额外费用授予和重复法术效果路径暂缓。
- `CoreRuleEngineRejectsVanillaSourceUnitWhenTargetsAreProvided` 共享参数覆盖 `UNL-146/219` 带目标打出拒绝；本批不新增测试，只入账既有证据。

## 验证

- focused Syndra / source-unit / SpellDuel / Echo regression：361/361 passed。
- SpellDuel / Echo / Payment / Stack / Priority adjacent regression：553/553 passed。
- backend full：3754/3754 passed。
- frontend build：passed。
- Chrome smoke：passed。

## 非覆盖

不声明 Syndra 的 actual spell-duel detection trigger、Echo 2 purple grant、granted Echo payment and repeat effects、complete spell-duel focus / pending task matrix、complete PaymentEngine / optional-cost semantics、LayerEngine / continuous-effect ordering、complete FEPR target / stack / timing windows、hidden-info / redaction matrix、1009/811 full-official 或 formal 18-step E2E 已完成。
