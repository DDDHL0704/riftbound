# Stage 4D-04G Armed Assaulter Haste + Tempered Audit

日期：2026-05-15
结论：**IMPLEMENTED / A-VALIDATED / WRITELOCK CLOSED / PROJECT NOT READY**

本文件记录 4D-04G 的 A-side 验收。该批只推进 `SFD·002/221`《武装强袭者》同一 `PLAY_CARD` 中 `HASTE_READY` 与 `TEMPERED_ATTACH:<equipmentObjectId>` 的组合 representative，不关闭 full `百炼`、full Haste、LayerEngine、card matrix full-official、frontend final validation 或 READY。

## 1. Scope

- B-Implementation / Bacon `019e2ba3-4b9a-7710-a702-1e8e28ecd8ea` 完成窄实现。
- Runtime 变更限制在 `src/Riftbound.Engine/CoreRuleEngine.cs` 与 `src/Riftbound.Engine/CardEquipmentKeywordRules.cs`。
- Focused tests 新增 `tests/Riftbound.ConformanceTests/ArmedAssaulterHasteTemperedTests.cs`。
- A 侧补一条 catalog/profile guard 到 `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs`，明确 `SFD·002/221` 当前属于 Tempered optional attach representative boundary，同时仍为 `recognized-deferred`。
- `src/Riftbound.Engine/MatchSession.cs`、frontend runtime、card matrix JSON、`riftbound-dotnet.sln` 未触碰。

## 2. Accepted Behavior

1. `SFD·002/221` 的服务端 prompt 在资源与合法己方 `SFD·186/221`《旋转飞斧》存在时同时暴露 `HASTE_READY` 与 `TEMPERED_ATTACH:<equipmentObjectId>`。
2. 同一 `PLAY_CARD` 可提交 no optional、only `HASTE_READY`、only `TEMPERED_ATTACH:*`，或 `HASTE_READY` + one legal `TEMPERED_ATTACH:*`。
3. 两种 optional cost 同时提交时，命令支付 base 6 mana + haste 1 mana + 1 red power，`COST_PAID` 与 stack item 都保留两个 optional costs，target arrays 保持空。
4. 结算时 Armed Assaulter 进入 P1 base 且因 `HASTE_READY` active；合法 selected Spinning Axe 贴附到 Armed Assaulter，并发出 `EQUIPMENT_ATTACHED` / `TEMPERED_OPTIONAL_ATTACH` evidence。
5. `TEMPERED_ATTACH` only 会贴附装备但不标记 `hasteReadyOptionalCostPaid`；`HASTE_READY` only 保留原 P4.34 行为。
6. duplicate / conflicting optional costs、invalid equipment、malformed optional、insufficient mana、insufficient red、wrong trait 等均 no-mutation reject。
7. selected equipment 若在 stack resolution 前 stale，只跳过 attach side effect；已支付的 HASTE_READY 仍保留，单位仍 active 入基地。
8. 相邻 Tempered、Jax trigger-payment、Akshan、Agile direct attach、Assemble、Take Up、Azir reattach、PaymentEngine representative tests 仍绿色。

## 3. Residuals

- P1-002 keyword execution-boundary full closure 仍未完成。
- Full `百炼` official breadth、full Haste official breadth、Ornn static modifiers、owner/controller breadth、attach lifecycle breadth、LayerEngine 与 full-card matrix 均仍 deferred。
- 本批不更新 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`，不改变 `fullOfficial=false` 口径。
- Active goal 仍 **NOT READY**，不得调用 `update_goal complete`。
