# Stage 4C-80 Bullet Time Power Damage Audit

审计日期：2026-05-13
结论：**代表性证据已验证；项目整体仍 NOT READY。**

## 范围

- 代表 FU：`FU-b646702ec0`
- 代表卡：弹幕时间 / Bullet Time `OGN·268/298` / cardId `31511`
- 代表 effect：`BULLET_TIME_DAMAGE_ENEMY_BATTLEFIELD_UNITS_BY_POWER_SPENT`
- 本批是 evidence-only overlay，不修改功能代码；覆盖 ordinary hand `PLAY_CARD`、支付基础 1 mana、提交 `SPEND_POWER` 金额、0 目标入栈、stack `damageAmount` 来自支付符能数值、双方让过后对敌方战场单位造成等量伤害。
- 本批同步记录符能不足拒绝，以及 typed power / `RECYCLE_RUNE` 支付资源的代表性守卫：匹配 trait 可支付并只扣对应 trait，缺失 required trait 拒绝，回收符文可精确补足，过量回收与错 trait 路径保持拒绝。
- 本批不声明完整 `JFAQ-251023 p6` adjudication、完整 battle / spell-duel timing、完整 `ASSIGN_COMBAT_DAMAGE` 生命周期、完整 PaymentEngine、完整 FEPR、隐藏信息 / redaction、18-step E2E 或 full-official 覆盖。

## 证据事实

- `CardBehaviorRegistry` 已登记 `OGN·268/298` 为 direct card behavior：费用 1、`BULLET_TIME_DAMAGE_ENEMY_BATTLEFIELD_UNITS_BY_POWER_SPENT`、`DamagesAllEnemyBattlefieldUnits: true`、`DamageAmountFromOptionalPowerCost: true`。
- `p2-preflight-play-bullet-time-power-damage-enemy-battlefield.fixture.json` 覆盖普通主阶段从手牌打出：支付 1 mana 与 `SPEND_POWER:3`、0 目标入栈、双方让过后对敌方战场单位造成 3 点伤害，敌方基地和己方战场单位不受影响，法术进入弃牌堆，符能池扣空。
- `CoreRuleEngineRejectsBulletTimeWhenPowerCostIsInsufficient` 覆盖 `SPEND_POWER` 金额超过可支付符能时的直接拒绝。
- typed power / payment resource tests 覆盖 matching trait spend、missing trait rejection、单张 / 多张回收符文补足、legacy owned rune guard、generic mixed-trait contribution，以及 over-recycle / wrong-trait guard。

## 验证

- focused Bullet Time / typed power / payment resource regression：24/24 passed。
- payment / pay-cost / power / recycle / enemy battlefield damage / stack / priority adjacent regression：250/250 passed。
- backend full：3754/3754 passed。
- frontend build：passed。
- Chrome smoke：passed。

## 非覆盖

不声明 complete `JFAQ-251023 p6`、complete battle / spell-duel lifecycle、complete `ASSIGN_COMBAT_DAMAGE` timing interactions、complete PaymentEngine semantics、complete FEPR target / stack / timing windows、complete prevention / replacement / layer interactions for noncombat damage、hidden-info / redaction matrix、1009/811 full-official 或 formal 18-step E2E 已完成。
