# Stage 4D-03X Legend Action Deferred Catalog Audit

日期：2026-05-14
结论：**IMPLEMENTED REPRESENTATIVE / PROJECT NOT READY**

4D-03X 完成 `P6LegendAbilityCatalog` 的 legend action catalog hygiene：Yasuo、Lee Sin、Diana、Poppy、Viktor 五个已经由 `LEGEND_ACT` / `LEGEND_ACTION_DOMAIN` 实现的旧 representative 不再作为 P6 deferred surfaces 暴露。

## Scope

已退役的旧 deferred representatives：

- `FND-259/298` Yasuo：`LEGEND_PAY_2_EXHAUST_MOVE_FRIENDLY_UNIT`
- `OGN·257/298` Lee Sin：`LEGEND_PAY_1_EXHAUST_GRANT_BOON`
- `UNL-234/219` Diana：`LEGEND_SPELL_DUEL_EXHAUST_GAIN_1_MANA`
- `UNL-237/219` Poppy：`LEGEND_SPEND_3_EXPERIENCE_EXHAUST_DRAW`
- `FND-265/298` Viktor：`LEGEND_PAY_1_EXHAUST_CREATE_MINION`

`P6LegendAbilityCatalog.GetDeferredSurfaces()` 现在返回空集合。新增 `GetImplementedLegendActionSurfaces()` 保存这五个已实现代表项，用于测试官方文本锚点、retired deferred surface id、BehaviorSpec `Implemented` + `LEGEND_ACTION_DOMAIN` 状态，以及非 P4 / 非 direct `CardBehaviorRegistry` 边界。

## Runtime Boundary

本切片未修改 `CoreRuleEngine` / `MatchSession` runtime。正确可玩路径仍是现有 `LEGEND_ACT`。手写 `ACTIVATE_ABILITY` 提交这些 legend action ability id 继续返回中文 unsupported、无事件、no-mutation，并且不把 raw `ACTIVATE_ABILITY` 暴露给用户错误文案。

## No-Go

- 未把 legend actions 加入 `P4ActivatedAbilityCatalog`。
- 未修改前端运行时代码。
- 未修改 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`。
- 未关闭 P0-005、P1、READY 或任何 full-official 项目状态。

## Residual Risk

本切片只清理 P6 catalog 代表状态，不扩展新的 legend action 行为。其他 legend / battlefield / token 规则域仍按既有 Stage 4D 状态推进，项目仍 **NOT READY**。
