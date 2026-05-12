# Stage 4C-78 Moon Rise Power Minus 2 Audit

审计日期：2026-05-13
结论：**代表性证据已验证；项目整体仍 NOT READY。**

## 范围

- 代表 FU：`FU-4329e00e20`
- 代表卡：月之降临 / Moon Rise `UNL-198/219` / cardId `34751`
- 代表 effect：`MOON_RISE_ENEMY_BATTLEFIELD_MINUS_2_NO_MOVE`
- 本批是 evidence-only overlay，不修改功能代码；覆盖 ordinary hand `PLAY_CARD`、支付 3 mana、0 目标入栈、stack / pass-pass 后让敌方战场单位本回合内战力 -2，并记录当前单战场区域模型下跳过可选敌方单位移动。
- 本批不声明完整多战场选择、可选敌方单位移动、完整 control-zone movement matrix、完整 cleanup replacement / duration matrix、完整 battle / spell-duel lifecycle、完整 FEPR、hidden-info / redaction matrix 或 full-official 覆盖。

## 证据事实

- `CardBehaviorRegistry` 已登记 `UNL-198/219` 为 direct card behavior：费用 3、0 目标、`PowerModifierAmount: -2`、`ModifiesAllEnemyBattlefieldUnits: true`。
- `p2-preflight-play-moonrise-enemy-battlefield-power-minus-2.fixture.json` 覆盖普通主阶段从手牌打出：支付 3、0 目标入栈、双方让过后两名敌方战场单位获得 -2 until-end-of-turn power modifier。
- fixture 锁定友方战场单位、友方基地单位和敌方基地单位不受影响，并记录 1 战力敌方战场单位变为 -1 的负战力边界。
- Stage 3B / 3C / 3D 已将该 FU 标记为 battlefield lifecycle、negative-power / combat-output clamp、spell-duel / battle lifecycle 后续压力候选；本批只把既有代表证据入账为 4C overlay。

## 验证

- focused Moon Rise / negative-power / cleanup regression：5/5 passed。
- power modifier / negative-power / cleanup / combat damage / stack adjacent regression：196/196 passed。
- backend full：3754/3754 passed。
- frontend build：passed。
- Chrome smoke：passed。

## 非覆盖

不声明 choosing one battlefield in a full multi-battlefield area model、optional enemy unit movement before the -2 modifier、complete control-zone movement matrix、complete cleanup replacement / duration-effect matrix、complete spell-duel / battle damage assignment lifecycle、complete FEPR target / stack / timing windows、hidden-info / redaction matrix、1009/811 full-official 或 formal 18-step E2E 已完成。
