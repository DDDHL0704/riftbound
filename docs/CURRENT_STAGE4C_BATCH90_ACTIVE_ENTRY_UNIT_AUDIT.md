# Stage 4C-90 Active Entry Unit Evidence Audit

审计日期：2026-05-13
结论：**代表性证据已验证；项目整体仍 NOT READY。**

## 范围

- 代表 FU：`FU-c1dc472304`、`FU-1207daea8f`
- 代表卡：先锋扈从 / Vanguard Squire `OGS·016/024` / cardId `31595`
- 代表卡：好斗的龙犬 / Aggressive Dragonhound `SFD·006/221` / cardId `33078`
- 代表 effects：`VANGUARD_SQUIRE_PLAY_ACTIVE_UNIT`、`AGGRESSIVE_DRAGONHOUND_PLAY_ACTIVE_UNIT`
- 本批是 evidence-only overlay，不修改功能代码；覆盖 active-entry source-unit ordinary hand `PLAY_CARD`、支付基础费用、0 目标入栈、stack / pass-pass 后源牌进入控制者基地，成为官方战力、官方标签、未休眠的 `CARD_TYPE:UNIT` 单位对象。
- 本批同步记录 active-entry source-unit 带目标打出拒绝，防止 0 目标单位路径被 target input 驱动变异。
- 本批不声明其他 active-entry family、游走移动、战场移动、横置技能、进攻触发、隐藏信息 / redaction、1009/811 full-official 或 final READY 覆盖。

## 证据事实

- `CardBehaviorRegistry` 已登记 `OGS·016/024` 为 direct card behavior：费用 6、0 目标、`VANGUARD_SQUIRE_PLAY_ACTIVE_UNIT`、`PlaysSourceToBaseAsUnit: true`、`SourceUnitPower: 5`、`SourceUnitTags: 精锐`。
- `CardBehaviorRegistry` 已登记 `SFD·006/221` 为 direct card behavior：费用 3、0 目标、`AGGRESSIVE_DRAGONHOUND_PLAY_ACTIVE_UNIT`、`PlaysSourceToBaseAsUnit: true`、`SourceUnitPower: 3`、`SourceUnitTags: 犬形|龙`。
- `p2-preflight-play-vanguard-squire-active-unit.fixture.json` 覆盖普通主阶段从手牌打出：支付 6、0 目标入栈、双方让过后进入 P1 基地，成为 5 战力、`CARD_TYPE:UNIT|精锐` 且 `IsExhausted=false` 的单位对象。
- `p2-preflight-play-aggressive-dragonhound-active-unit.fixture.json` 覆盖普通主阶段从手牌打出：支付 3、0 目标入栈、双方让过后进入 P1 基地，成为 3 战力、`CARD_TYPE:UNIT|犬形|龙` 且 `IsExhausted=false` 的单位对象。
- `CoreRuleEngineRejectsActiveEntrySourceUnitWhenTargetsAreProvided` 覆盖两张卡带目标打出拒绝；拒绝后费用、手牌、基地、stack 与 tick 不发生变更。

## 验证

- focused active-entry source-unit / target rejection regression：24/24 passed。
- adjacent source-unit / play-card / target / stack / priority / payment regression：1879/1879 passed。
- backend full：3771/3771 passed。

## 非覆盖

不声明其他 active-entry source-unit family、roam / battlefield movement、tap skills、attack triggers、完整 source-zone play matrix、complete PaymentEngine、complete FEPR target / stack / timing windows、hidden-info / redaction matrix、1009/811 full-official 或 final READY 已完成。
