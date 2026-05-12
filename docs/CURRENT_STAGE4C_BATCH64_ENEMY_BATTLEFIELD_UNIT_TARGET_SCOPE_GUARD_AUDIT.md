# Stage 4C-64 Enemy Battlefield Unit Target Scope Guard Audit

审计日期：2026-05-13
结论：**代表性守卫已验证；项目整体仍 NOT READY。**

## 范围

- 代表 FU：`FU-6d67456a80`
- 代表卡：怒海大鲨炮 / Megashark Cannon `OGN·092/298` / cardId `31310`
- 代表 effect：`MEGASHARK_CANNON_PLAY_UNIT_DAMAGE_6_ENEMY_BATTLEFIELD`
- 本批只覆盖 ordinary hand `PLAY_CARD`、支付 6 mana、选择敌方公开战场单位、stack pass-pass 后对目标造成 6 点伤害，以及 `EnemyBattlefieldUnit` 直接目标域排除 non-unit / hidden / standby / dirty / friendly / base / hand / stale targets。

## 修复

- `CardTargetScopes.EnemyBattlefieldUnit` 现在复用 enemy public battlefield-unit guard，只接受敌方公开战场单位。
- 新增 `IsEnemyBattlefieldUnitObject`，要求目标在敌方 `BATTLEFIELD` 区域，且由该区域玩家控制的公开战场单位 guard 判定通过。
- 新增 `EnemyBattlefieldUnitTargetScopeGuardTests`，覆盖 Megashark Cannon valid target damage、invalid target no-mutation，以及无 `TargetRequiredTag` 的 EnemyBattlefieldUnit non-unit rejection regression。

## 验证

- focused：18/18 passed。
- target regression：82/82 passed。
- backend full：3754/3754 passed。
- frontend build：passed。
- Chrome smoke：passed。

## 非覆盖

不声明 composite target scopes、all EnemyBattlefieldUnit card texts / alternate modes、formal multi-battlefield precision、standby / reaction、quick / spell-duel timing、full FEPR stack lifecycle、PaymentEngine、LayerEngine、hidden-info / redaction matrix、FAQ、1009/811 full-official 或 formal 18-step E2E 已完成。
