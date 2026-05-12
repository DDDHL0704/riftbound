# Stage 4C-89 Vanilla Unit Evidence Audit

审计日期：2026-05-13
结论：**代表性证据已验证；项目整体仍 NOT READY。**

## 范围

- 代表 FU：`FU-d635fc47f4`、`FU-72ce6fb8a4`
- 代表卡：山脉亚龙 / Mountain Drake `OGN·142/298` / cardId `31366`
- 代表卡：船坞潜伏者 / Dockside Lurker `OGN·175/298` / cardId `31405`
- 代表 effects：`MOUNTAIN_DRAKE_PLAY_UNIT`、`DOCKSIDE_LURKER_PLAY_UNIT`
- 本批是 evidence-only overlay，不修改功能代码；覆盖无卡面效果单位 ordinary hand `PLAY_CARD`、支付基础费用、0 目标入栈、stack / pass-pass 后源牌进入控制者基地，成为官方战力的 `CARD_TYPE:UNIT` 单位对象。
- 本批同步记录通用 vanilla source-unit 带目标打出拒绝，防止 0 目标单位路径被 target input 驱动变异。
- 本批不声明任何非 vanilla 单位、active-entry 静态、关键词、战场移动、隐藏信息 / redaction、1009/811 full-official 或 final READY 覆盖。

## 证据事实

- `CardBehaviorRegistry` 已登记 `OGN·142/298` 为 direct card behavior：费用 9、0 目标、`MOUNTAIN_DRAKE_PLAY_UNIT`、`PlaysSourceToBaseAsUnit: true`、`SourceUnitPower: 10`。
- `CardBehaviorRegistry` 已登记 `OGN·175/298` 为 direct card behavior：费用 3、0 目标、`DOCKSIDE_LURKER_PLAY_UNIT`、`PlaysSourceToBaseAsUnit: true`、`SourceUnitPower: 3`。
- `p2-preflight-play-mountain-drake-vanilla-unit.fixture.json` 覆盖普通主阶段从手牌打出：支付 9、0 目标入栈、双方让过后进入 P1 基地，成为 10 战力、仅带 `CARD_TYPE:UNIT` 标签的单位对象。
- `p2-preflight-play-dockside-lurker-vanilla-unit.fixture.json` 覆盖普通主阶段从手牌打出：支付 3、0 目标入栈、双方让过后进入 P1 基地，成为 3 战力、仅带 `CARD_TYPE:UNIT` 标签的单位对象。
- `CoreRuleEngineRejectsVanillaSourceUnitWhenTargetsAreProvided` 覆盖两张卡带目标打出拒绝；拒绝后费用、手牌、基地、stack 与 tick 不发生变更。

## 验证

- focused vanilla source-unit / target rejection regression：305/305 passed。
- adjacent source-unit / play-card / target / stack / priority / payment regression：1879/1879 passed。
- backend full：3771/3771 passed。

## 非覆盖

不声明其他单位来源路径、静态文本单位、active-entry、关键词、完整 source-zone play matrix、complete PaymentEngine、complete FEPR target / stack / timing windows、hidden-info / redaction matrix、1009/811 full-official 或 final READY 已完成。
