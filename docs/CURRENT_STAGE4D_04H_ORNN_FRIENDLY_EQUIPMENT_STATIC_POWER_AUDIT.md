# Stage 4D-04H Ornn Friendly Equipment Static Power Audit

日期：2026-05-15
结论：**IMPLEMENTED / A-VALIDATED / WRITELOCK CLOSED / PROJECT NOT READY**

本文件记录 4D-04H 的 A-side 窄实现与验收。该批只推进 `SFD·085/221` / `SFD·085a/221`《奥恩》从手牌 `PLAY_CARD` 结算入场时，按己方公开场上装备数量获得入场战力加成的 representative；它不是完整 continuous LayerEngine / static recompute。

## 1. Scope

- A 侧直接完成窄 runtime / focused-test / profile guard 实现，不派发 B。
- Runtime 变更限制在 `src/Riftbound.Engine/CardBehaviorRegistry.cs`、`src/Riftbound.Engine/CardEquipmentKeywordRules.cs` 与 `src/Riftbound.Engine/CoreRuleEngine.cs`。
- Focused tests 新增 `tests/Riftbound.ConformanceTests/OrnnFriendlyEquipmentStaticPowerTests.cs`。
- Catalog/profile guard 更新在 `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs`，明确 Ornn friendly-equipment static power representative boundary 已有窄覆盖，同时仍为 `recognized-deferred`。
- Frontend runtime、card matrix JSON、`MatchSession.cs`、`riftbound-dotnet.sln` 未触碰。

## 2. Accepted Behavior

1. `SFD·085/221` 与 `SFD·085a/221` 从手牌 `PLAY_CARD` 入场时，会统计 controller 友方公开场上装备数量。
2. 计数只包含己方公开 field equipment，包括基地/战场中的正面装备；不包含手牌、正面朝下对象、敌方装备、脏 controller 装备或非装备单位。
3. Ornn 基础战力 4 加上该计数后进入基地或战场；代表测试锁定两件合法友方公开装备时入场战力为 6。
4. `UNIT_PLAYED` event payload 在有加成时包含 `friendlyEquipmentPowerBonus`，便于审计和前端只读展示；没有加成时不写该字段。
5. Ornn 没有友方公开场上装备时仍以基础战力 4 入场。
6. `CardEquipmentKeywordRules` 把这条 Ornn representative 标为 implemented boundary，但保留 full `百炼` breadth、dynamic static recompute、LayerEngine、attach lifecycle 与 owner/controller breadth 的 deferred 口径。

## 3. Residuals

- 本批只在 Ornn 入场结算点计算一次当前友方公开场上装备数量；装备后续进出场、控制权变化、贴附状态变化、失去来源、同层时间戳与依赖重算仍未由完整 LayerEngine 统一处理。
- Full `百炼` official breadth、其他装备静态修正、owner/controller breadth、attach lifecycle breadth、LayerEngine、full-card matrix、frontend final validation 均仍 deferred。
- 本批不更新 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`，不改变任何 `fullOfficial=false` 口径。
- Active goal 仍 **NOT READY**，不得调用 `update_goal complete`。
