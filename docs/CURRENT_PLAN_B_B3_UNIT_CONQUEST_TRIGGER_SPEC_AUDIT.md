# Plan B / B3 Unit Conquest Trigger Spec Audit

Date: 2026-06-25

Status: focused unit-conquest draw-one, draw-or-call-rune, create-dormant-Gold, grant-self-boon, ready-self-once, and grant-friendly-boon TriggerSpec slices accepted; project remains **NOT READY**.

## Scope

This slice moves implemented unit conquest effects away from engine card-number branching:

- `OGN·039/298` / `OGN·039a/298` 卡莎 official text from `data/official/card-catalog.zh-CN.json`: `当我征服一处战场时，抽一张牌。`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = UNIT_CONQUEST_DRAW_ONE`
  - `Timing = UNIT_CONQUEST`
  - `TargetScope = SOURCE_UNIT`
  - `DrawCount = 1`
- `CoreRuleEngine.TryResolveBattlefieldHeldActivateUnitConquestEffectsTrigger` now routes 卡莎's representative draw effect through `UnitConquestTriggerSpecRules.TryGetUnitConquestDrawTrigger(...)`, and reads the emitted effect id plus draw count from `BehaviorSpec.Triggers`.
- The old `KaisaUnitConquestDrawCardNo` / `IsKaisaUnitConquestDrawCardNo` branch is removed.
- `OGN·155/298` 奇亚娜 official text from `data/official/card-catalog.zh-CN.json`: `当我征服一处战场时，抽一张牌或召出一枚休眠的符文。`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = UNIT_CONQUEST_DRAW_ONE_OR_CALL_RUNE`
  - `Timing = UNIT_CONQUEST`
  - `TargetScope = SOURCE_UNIT`
  - `DrawCount = 1`
  - `RuneCallCount = 1`
- `CoreRuleEngine.TryResolveBattlefieldHeldActivateUnitConquestEffectsTrigger` now routes 奇亚娜's representative draw-or-call-rune effect through `UnitConquestTriggerSpecRules.TryGetUnitConquestDrawOrCallRuneTrigger(...)`, and reads the emitted effect id, draw count, and rune-call count from `BehaviorSpec.Triggers`.
- The old `QiyanaUnitConquestDrawOrRuneCardNo` / `IsQiyanaUnitConquestDrawOrRuneCardNo` branch is removed.
- `UNL-222/219` / `SFD·069/221` 坏坏魄罗 official text from `data/official/card-catalog.zh-CN.json`: `当我征服一处战场时，打出一个休眠的“金币”装备指示物。`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = UNIT_CONQUEST_CREATE_DORMANT_GOLD`
  - `Timing = UNIT_CONQUEST`
  - `TargetScope = SOURCE_UNIT`
  - `CreatedTokenCount = 1`
  - `CreatedTokenName = 金币`
  - `CreatedTokenDestination = OWNER_BASE`
  - `CreatedTokenExhausted = true`
  - `CreatedTokenKeywords = [反应]`
- `CoreRuleEngine.TryResolveBattlefieldHeldActivateUnitConquestEffectsTrigger` now routes 坏坏魄罗's representative dormant-Gold effect through `UnitConquestTriggerSpecRules.TryGetUnitConquestCreateDormantGoldTrigger(...)`, and reads the emitted effect id, token name, token count, exhausted state, and token tags from `BehaviorSpec.Triggers`.
- The old `BadPoroUnitConquestGoldCardNo` / `IsBadPoroUnitConquestGoldCardNo` branch is removed.
- `SFD·232/221` / `SFD·232*/221` / `OGN·164/298` / `OGN·164a/298` 瑟提 official text from `data/official/card-catalog.zh-CN.json`: `当我被打出时、或当我征服一处战场时，给予我增益。`
- `RuleTextParser` now parses that conquest text as `TriggerSpec` with:
  - `Kind = UNIT_CONQUEST_GRANT_SELF_BOON`
  - `Timing = UNIT_CONQUEST`
  - `TargetScope = SOURCE_UNIT`
  - `BoonCount = 1`
- `CoreRuleEngine.TryResolveBattlefieldHeldActivateUnitConquestEffectsTrigger` now routes 瑟提's representative self-boon effect through `UnitConquestTriggerSpecRules.TryGetUnitConquestGrantSelfBoonTrigger(...)`, and reads the emitted effect id from `BehaviorSpec.Triggers`.
- The old `SettUnitConquestSelfBoonCardNo` / `IsSettUnitConquestSelfBoonCardNo` branch is removed.
- `SFD·113/221` / `SFD·113a/221` 卢锡安 official text from `data/official/card-catalog.zh-CN.json`: `每回合首次，当我征服一处战场时，让我变为活跃状态。`
- `RuleTextParser` now parses that conquest text as `TriggerSpec` with:
  - `Kind = UNIT_CONQUEST_READY_SELF_ONCE_PER_TURN`
  - `Timing = UNIT_CONQUEST`
  - `TargetScope = SOURCE_UNIT`
  - `OncePerTurn = true`
- `CoreRuleEngine.TryResolveBattlefieldHeldActivateUnitConquestEffectsTrigger` now routes 卢锡安's representative ready-self effect through `UnitConquestTriggerSpecRules.TryGetUnitConquestReadySelfOnceTrigger(...)`, and reads the emitted effect id plus once-per-turn flag from `BehaviorSpec.Triggers`.
- The old `LucianUnitConquestReadyCardNo` / `IsLucianUnitConquestReadyCardNo` branch is removed.
- `UNL-029/219` / `UNL-029a/219` 绯红印记树怪 official text from `data/official/card-catalog.zh-CN.json`: `当我征服一处战场时，给予一名友方单位{{增益}}。`
- `RuleTextParser` now parses that conquest text as `TriggerSpec` with:
  - `Kind = UNIT_CONQUEST_GRANT_FRIENDLY_BOON`
  - `Timing = UNIT_CONQUEST`
  - `TargetScope = CONTROLLED_UNIT_ON_FIELD`
  - `BoonCount = 1`
- `CoreRuleEngine.TryResolveBattlefieldHeldActivateUnitConquestEffectsTrigger` now routes 绯红印记树怪's representative friendly-boon effect through `UnitConquestTriggerSpecRules.TryGetUnitConquestGrantFriendlyBoonTrigger(...)`, and reads the emitted effect id from `BehaviorSpec.Triggers`.
- The old `FriendlyBoonUnitConquestCardNo` / `IsFriendlyBoonUnitConquestCardNo` branch is removed.
- Current source-helper count for `private static bool Is*CardNo(...)` is `41` total / `38` in `CoreRuleEngine`; the remaining unit-conquest helper count is `2`.

## Non-Goals

- This does not close the full unit-conquest family. Friendly-power / destroy-equipment conquest representatives still have card-number helper branches.
- This does not implement 绯红印记树怪's separate `你征服此处时的征服效果额外触发一次。` doubling effect.
- This does not add natural battle-conquest trigger queuing for every unit. The validated runtime route is the existing 清算人竞技场 representative that activates unit conquest effects from a battlefield held trigger.
- This does not close optional target prompts, complete draw replacement / fatigue breadth, full targeting-stack-timing, B0 full-game readiness, or project READY.

## Follow-Up

- Migrate the remaining unit-conquest helpers one effect kind at a time into `TriggerSpec` shapes, keeping simple non-targeted effects ahead of optional / targeted choices.
- After each migration, keep the source guard pattern and the `P79BattlefieldHeldActivateConquestEffects...` runtime representatives green.
