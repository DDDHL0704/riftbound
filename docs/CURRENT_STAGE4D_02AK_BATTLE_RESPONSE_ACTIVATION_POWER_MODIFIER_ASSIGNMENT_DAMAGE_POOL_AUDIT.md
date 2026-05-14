# 4D-02AK Battle Response Activation Power Modifier Assignment Damage Pool Audit

日期：2026-05-15
结论：**ACCEPTED / focused slice only / project NOT READY**

## Scope

本切片收窄 P0-004 battle response activation-returned assignment damage semantics：actual Shadow activation / stack resolution / returned response 后，非眩晕且带 `UntilEndOfTurnPowerModifier` 的 battle participant 在 assignment prompt、runtime validation 与 committed damage pool 中必须使用当前 effective `Power` 一次。

验收目标：

- Modified Bulwark defender seeded as `Power = 1` / `UntilEndOfTurnPowerModifier = -1` remains non-stunned after Shadow stuns the attacker.
- Returned assignment prompt exposes `damagePool[BulwarkDefenderObjectId] == 1`、`lethalDamageThreshold[BulwarkDefenderObjectId] == 1`、Bulwark participant `power == 1`。
- Prompt required assignments / choices still require and offer Bulwark's 1 damage.
- Stale double-counted assignment shape that omits Bulwark damage is rejected as `InvalidPayload` without mutation and without BF-B advancement.
- Legal effective-power assignment commits Bulwark 1 + Shadow 1 damage, keeps stunned attacker damage at 0, then closes BF-A before advancing BF-B.

## Implementation Accepted

- `src/Riftbound.Engine/CoreRuleEngine.cs`
  - `BuildCombatDamagePool` now uses `cardObject.Power` for non-stunned participants instead of double-counting `UntilEndOfTurnPowerModifier`.
  - `BuildCombatLethalDamageThreshold` now uses `cardObject.Power - cardObject.Damage` for non-stunned participants.
- `src/Riftbound.Engine/MatchSession.cs`
  - `ResolutionResult.BattleEffectivePowerFor` now returns `cardObject.Power` for non-stunned participants, while preserving 02AJ stunned-as-zero semantics.
- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`
  - Added `NaturalBattleResponseActivationPowerModifierUsesEffectiveAssignmentDamagePool`.
  - Guard proves prompt metadata, runtime rejection, committed `COMBAT_DAMAGE_ASSIGNED.damagePool`, and `DAMAGE_APPLIED.sourceDamagePool` all agree on effective power 1.

## Validation

- Targeted new guard：1/1 pass。
- Focused suite：294/294 pass。
- Adjacent suite：824/824 pass。
- Backend full：4236/4236 pass。
- `git diff --check`：pass。

## Residual Risk

该切片只关闭 activation-returned assignment 中非眩晕 power-modified participant 的 focused representative。它不关闭完整 P0-004 battle lifecycle、P0-005 PaymentEngine、LayerEngine、damage prevention / replacement / modification breadth、keyword/static power 全组合、前端最终验收或 1009/811 full-official card matrix。项目仍 **NOT READY**，不得触发 `update_goal complete`。
