# Stage 4C-82 Duel Mutual Power Damage Audit

审计日期：2026-05-13
结论：**代表性证据已验证；项目整体仍 NOT READY。**

## 范围

- 代表 FU：`FU-2779c06158`
- 代表卡：决斗 / Duel `OGN·128/298` / cardId `31352`
- 代表 effect：`DUEL_MUTUAL_POWER_DAMAGE`
- 本批是 evidence-only overlay，不修改功能代码；覆盖 ordinary hand `PLAY_CARD`、支付基础 2 mana、按友方单位 then 敌方单位顺序选择 2 个目标、0 额外费用入栈、stack / pass-pass 后两名目标以自身当前战力互相造成伤害。
- 本批同步记录敌方 2 战力目标受到友方 4 战力单位造成的 4 点伤害后被致命伤害清理摧毁，友方 4 战力单位受到敌方 2 战力单位造成的 2 点伤害后留在战场，以及反向目标顺序拒绝。
- 本批不声明完整 battle / spell-duel lifecycle、完整 `ASSIGN_COMBAT_DAMAGE` timing、完整 LayerEngine / continuous effects、完整 FEPR、replacement / prevention、隐藏信息 / redaction、18-step E2E 或 full-official 覆盖。

## 证据事实

- `CardBehaviorRegistry` 已登记 `OGN·128/298` 为 direct card behavior：费用 2、2 目标、`TargetScope: FriendlyThenEnemyUnits`、`DealsMutualTargetPowerDamage: true`。
- `p2-preflight-play-duel-mutual-power-damage.fixture.json` 覆盖普通主阶段从手牌打出：支付 2、目标顺序为友方 4 战力单位和敌方 2 战力单位，双方让过后产生两个 `DAMAGE_APPLIED`，敌方单位进入弃牌堆，法术进入控制者弃牌堆。
- `CoreRuleEngineRejectsDuelWhenTargetsAreReversed` 和 `p4-play-duel-target-order-rejected.fixture.json` 覆盖先敌方后友方目标顺序时拒绝，且不支付费用、不入栈、不造成伤害、不移动手牌或战场单位。
- Gentleman Duel、Marching Orders、Clash of Giants 等 mutual-power 相邻测试继续保持绿色，作为共享互伤、目标顺序、伤害和清理回归。

## 验证

- focused Duel regression：3/3 passed。
- mutual damage / target / stack / priority / damage / cleanup adjacent regression：1410/1410 passed。
- backend full：3754/3754 passed。
- frontend build：passed。
- Chrome smoke：passed。

## 非覆盖

不声明 complete battle / spell-duel lifecycle、complete `ASSIGN_COMBAT_DAMAGE` timing interactions、complete LayerEngine / continuous effects beyond represented current-power reads、complete FEPR target / stack / timing windows、complete prevention / replacement / cleanup queue interactions for mutual damage、Swift / reaction timing、hidden-info / redaction matrix、1009/811 full-official 或 formal 18-step E2E 已完成。
