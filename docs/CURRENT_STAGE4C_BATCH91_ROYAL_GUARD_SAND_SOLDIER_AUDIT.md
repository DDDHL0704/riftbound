# Stage 4C-91 Royal Guard Sand Soldier Evidence Audit

审计日期：2026-05-13
结论：**代表性证据已验证；项目整体仍 NOT READY。**

## 范围

- 代表 FU：`FU-29d76f0175`
- 代表卡：皇家守卫 / Royal Guard `SFD·157/221` / cardId `33251`
- 代表 effect：`ROYAL_GUARD_PLAY_UNIT_CREATE_SAND_SOLDIER`
- 本批是 evidence-only overlay，不修改功能代码；覆盖 ordinary hand `PLAY_CARD`、支付基础 4 费用、0 目标入栈、stack / pass-pass 后源牌进入控制者基地，成为 2 战力 `CARD_TYPE:UNIT` 单位对象，并创建一名 2 战力、带 `CARD_TYPE:UNIT|黄沙士兵` 标签的黄沙士兵单位指示物。
- 本批同步记录 Royal Guard 带目标打出拒绝，防止 0 目标单位 + token 创建路径被 target input 驱动变异。
- 本批不声明精确“此处”目的地选择、完整 token factory、control-zone / battlefield 目的地矩阵、replacement / cleanup、完整 PaymentEngine、完整 FEPR target / stack / timing windows、hidden-info / redaction、1009/811 full-official 或 final READY 覆盖。

## 证据事实

- `CardBehaviorRegistry` 已登记 `SFD·157/221` 为 direct card behavior：费用 4、0 目标、`ROYAL_GUARD_PLAY_UNIT_CREATE_SAND_SOLDIER`、`PlaysSourceToBaseAsUnit: true`、`SourceUnitPower: 2`、`CreatedBaseUnitTokenCount: 1`、`CreatedBaseUnitTokenPower: 2`、`CreatedBaseUnitTokenName: "黄沙士兵"`、`CreatedBaseUnitTokenTags: CARD_TYPE:UNIT|黄沙士兵`。
- `p2-preflight-play-royal-guard-create-sand-soldier.fixture.json` 覆盖普通主阶段从手牌打出：支付 4、0 目标入栈、双方让过后源牌进入 P1 基地，成为 2 战力 `CARD_TYPE:UNIT` 单位对象。
- 同一 fixture 锁定 `UNIT_TOKEN_CREATED`：源对象 `P1-UNIT-ROYAL-GUARD` 创建 `P1-UNIT-ROYAL-GUARD-TOKEN-001`，名称 `黄沙士兵`，战力 2，目的地为当前代表模型的控制者基地，标签为 `CARD_TYPE:UNIT|黄沙士兵`。
- `CoreRuleEngineRejectsRoyalGuardWhenTargetsAreProvided` 覆盖带目标打出拒绝；拒绝后费用、手牌、基地、stack 与 tick 不发生变更。

## 验证

- focused Royal Guard / Sand Soldier regression：10/10 passed。
- adjacent source-unit / token / target / stack / priority / payment regression：1880/1880 passed。
- backend full：3771/3771 passed。

## 非覆盖

不声明精确“此处”目的地选择、完整 token factory、control-zone / battlefield 目的地矩阵、replacement / cleanup、完整 PaymentEngine、完整 FEPR target / stack / timing windows、hidden-info / redaction matrix、1009/811 full-official 或 final READY 已完成。
