# Plan B / B3 Unit Conquest Trigger Spec Audit

Date: 2026-06-25

Status: focused unit-conquest draw-one TriggerSpec slice accepted; project remains **NOT READY**.

## Scope

This slice moves one implemented unit conquest effect away from engine card-number branching:

- `OGN·039/298` / `OGN·039a/298` 卡莎 official text from `data/official/card-catalog.zh-CN.json`: `当我征服一处战场时，抽一张牌。`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = UNIT_CONQUEST_DRAW_ONE`
  - `Timing = UNIT_CONQUEST`
  - `TargetScope = SOURCE_UNIT`
  - `DrawCount = 1`
- `CoreRuleEngine.TryResolveBattlefieldHeldActivateUnitConquestEffectsTrigger` now routes 卡莎's representative draw effect through `UnitConquestTriggerSpecRules.TryGetUnitConquestDrawTrigger(...)`, and reads the emitted effect id plus draw count from `BehaviorSpec.Triggers`.
- The old `KaisaUnitConquestDrawCardNo` / `IsKaisaUnitConquestDrawCardNo` branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `46` total / `43` in `CoreRuleEngine`; the remaining unit-conquest helper count is `7`.

## Non-Goals

- This does not close the full unit-conquest family. Bad Poro / Qiyana / Sett / Lucian / friendly-boon / friendly-power / destroy-equipment conquest representatives still have card-number helper branches.
- This does not add natural battle-conquest trigger queuing for every unit. The validated runtime route is the existing 清算人竞技场 representative that activates unit conquest effects from a battlefield held trigger.
- This does not close optional target prompts, complete draw replacement / fatigue breadth, full targeting-stack-timing, B0 full-game readiness, or project READY.

## Follow-Up

- Migrate the remaining unit-conquest helpers one effect kind at a time into `TriggerSpec` shapes, starting with simple non-targeted effects before optional / targeted choices.
- After each migration, keep the source guard pattern and the `P79BattlefieldHeldActivateConquestEffects...` runtime representatives green.
