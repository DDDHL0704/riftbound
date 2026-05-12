# Stage 4C-75 Gentleman Duel Damage Audit

审计日期：2026-05-13
结论：**代表性证据已验证；项目整体仍 NOT READY。**

## 范围

- 代表 FU：`FU-265c03a141`
- 代表卡：绅士决斗 / Gentleman Duel `OGS·008/024` / cardId `31587`
- 代表 effect：`GENTLEMAN_DUEL_POWER_PLUS_3_THEN_MUTUAL_POWER_DAMAGE`
- 本批是 evidence-only overlay，不修改功能代码；覆盖 ordinary hand `PLAY_CARD`、支付 6 mana、友方单位再敌方单位目标、stack / pass-pass 后友方目标本回合战力 +3，随后两名目标按当前战力互相造成伤害，并摧毁受到致命伤害的敌方目标。
- 本批不声明 Swift / spell-duel timing、完整 LayerEngine、duration cleanup、replacement / prevention、完整 FEPR target matrix 或 full-official 覆盖。

## 证据事实

- `CardBehaviorRegistry` 已登记 `OGS·008/024` 为 direct card behavior：费用 6、2 目标、`TargetScope: FriendlyThenEnemyUnits`、`PowerModifierAmount: 3`、`DealsMutualTargetPowerDamage: true`。
- `p2-preflight-play-gentleman-duel-power-then-mutual-damage.fixture.json` 覆盖友方 2 战力单位先获得 +3，成为 5 战力，再对敌方 3 战力单位造成 5 点伤害；敌方单位对友方单位造成 3 点伤害。
- fixture 期望事件包含 `POWER_MODIFIED_UNTIL_END_OF_TURN`、两条 `DAMAGE_APPLIED` 与 `UNIT_DESTROYED`；最终敌方目标进入 owner graveyard，友方目标保留 3 damage、5 power 与 `untilEndOfTurnPowerModifier: 3`。
- 共享 `FriendlyThenEnemyUnits` 目标顺序 guard 由 Duel sibling tests 覆盖；Gentleman Duel 专属 invalid-target matrix 仍 deferred。

## 验证

- focused Gentleman Duel / mutual damage regression：6/6 passed。
- mutual damage / damage / cleanup adjacent regression：203/203 passed。
- backend full：3754/3754 passed。
- frontend build：passed。
- Chrome smoke：passed。

## 非覆盖

不声明 Gentleman Duel 的 Swift / spell-duel focus timing、complete FEPR target / stack / timing windows、完整 LayerEngine / continuous-effect ordering、end-of-turn duration cleanup、damage prevention / replacement、全部 lethal-cleanup permutations、Gentleman Duel 专属 invalid-target matrix、battle damage assignment lifecycle、hidden-info / redaction matrix、1009/811 full-official 或 formal 18-step E2E 已完成。
