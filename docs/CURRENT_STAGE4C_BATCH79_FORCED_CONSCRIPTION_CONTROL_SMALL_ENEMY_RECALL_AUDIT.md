# Stage 4C-79 Forced Conscription Control Small Enemy Recall Audit

审计日期：2026-05-13
结论：**代表性证据已验证；项目整体仍 NOT READY。**

## 范围

- 代表 FU：`FU-0681eefc4e`
- 代表卡：强制征召 / Forced Conscription `UNL-140/219` / cardId `34683`
- 代表 effect：`FORCED_CONSCRIPTION_CONTROL_SMALL_ENEMY_RECALL`
- 本批是 evidence-only overlay，不修改功能代码；覆盖 ordinary hand `PLAY_CARD`、不支付 5 经验额外费用、支付基础 5 mana、选择敌方战场上一名 3 战力及以下且带 `CARD_TYPE:UNIT` 的单位、stack / pass-pass 后获得其控制权、使其休眠并放入控制者基地。
- 本批同步记录 4 战力目标拒绝，以及恢复 / 脏 stack target 指向敌方战场中已由当前玩家控制的单位时不产生控制权事件也不搬区。
- 本批不声明支付 5 经验选择任意敌方单位分支、完整 owner/controller 分离模型、完整 control-zone movement matrix、完整 cleanup replacement / duration matrix、完整 PaymentEngine optional-cost semantics、完整 FEPR 或 full-official 覆盖。

## 证据事实

- `CardBehaviorRegistry` 已登记 `UNL-140/219` 为 direct card behavior：费用 5、1 目标、`TargetScope: EnemyBattlefieldUnit`、`MaxTargetPower: 3`、`TargetRequiredTag: CARD_TYPE:UNIT`、`GainsControlOfTargetToBase: true`、`ExhaustsControlledTarget: true`。
- `p2-preflight-play-forced-conscription-control-small-enemy-recall.fixture.json` 覆盖普通主阶段从手牌打出：支付 5、选择 3 战力敌方战场单位、双方让过后 `UNIT_CONTROL_GAINED`、目标休眠并进入 P1 基地。
- `CoreRuleEngineRejectsForcedConscriptionWhenTargetPowerAboveThree` 覆盖 4 战力目标拒绝且无 tick / event / cost / hand / battlefield / stack mutation。
- `CoreRuleEngineForcedConscriptionResolutionSkipsAlreadyControlledEnemyZoneTarget` 覆盖脏恢复目标不产生 `UNIT_CONTROL_GAINED`，目标仍留在 P2 battlefield，controller 不被重写。

## 验证

- focused Forced Conscription / Taken For A Ride / Hostile Takeover control regression：18/18 passed。
- control / battlefield / target / stack / payment adjacent regression：1718/1718 passed。
- backend full：3754/3754 passed。
- frontend build：passed。
- Chrome smoke：passed。

## 非覆盖

不声明 optional 5 experience any-enemy-unit branch、complete owner/controller separation beyond represented preflight zone ownership、complete control-zone movement matrix、complete cleanup replacement / duration-effect matrix、complete PaymentEngine optional-cost semantics、complete FEPR target / stack / timing windows、hidden-info / redaction matrix、1009/811 full-official 或 formal 18-step E2E 已完成。
