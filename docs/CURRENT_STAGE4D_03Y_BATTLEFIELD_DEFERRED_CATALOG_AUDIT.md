# Stage 4D-03Y Battlefield Deferred Catalog Audit

日期：2026-05-14
结论：**IMPLEMENTED REPRESENTATIVE / PROJECT NOT READY**

4D-03Y 完成 `P6BattlefieldEffectCatalog` 的 battlefield catalog hygiene：四个已经由 `BATTLEFIELD_RULE_DOMAIN` 覆盖的旧 representative 不再作为 P6 deferred surfaces 暴露。

## Scope

已退役的旧 deferred representatives：

- `SFD·208/221` Poro Forge：`BATTLEFIELD_DEFERRED_GRANT_LEGEND_EXHAUST_ATTACH_WEAPON`
- `OGN·292/298` Dream Tree：`BATTLEFIELD_DEFERRED_FIRST_FRIENDLY_SPELL_DRAW`
- `UNL-206/219` Blood Altar：`BATTLEFIELD_DEFERRED_DESTROYED_IN_BATTLE_REPLACEMENT_RECALL`
- `UNL-208/219` Blackflame Altar：`BATTLEFIELD_DEFERRED_STATIC_KEYWORD_GRANT_EPHEMERAL_DEFENDER_BONUS`

`P6BattlefieldEffectCatalog.GetDeferredSurfaces()` 现在返回空集合。新增 `GetImplementedBattlefieldRuleSurfaces()` 保存这四个已实现代表项，用于测试旧 surface id、官方文本锚点、surface kind、activated flag、target count、BehaviorSpec `Implemented` + `BATTLEFIELD_RULE_DOMAIN` 状态，以及非 P4 / 非 direct `CardBehaviorRegistry` 边界。

## Runtime Boundary

本切片未修改 `CoreRuleEngine` / `MatchSession` runtime。正确路径仍由 battlefield rule domain、`DECLARE_BATTLE` / combat lifecycle / server-authored prompt 或既有 `LEGEND_ACT` representative 提供。`SFD·208/221` battlefield-granted activated representative 的手写 `ACTIVATE_ABILITY` 继续返回中文 unsupported、无事件、no-mutation，并且不把 raw `ACTIVATE_ABILITY` 暴露给用户错误文案。

## No-Go

- 未把 battlefield-granted action 加入 `P4ActivatedAbilityCatalog`。
- 未修改前端运行时代码。
- 未修改 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`。
- 未关闭 P0-002、P0-003、P0-004、P0-005、P1、READY 或任何 full-official 项目状态。

## Residual Risk

本切片只清理 P6 catalog 代表状态，不扩展新的 battlefield / combat runtime 行为。项目仍 **NOT READY**。
