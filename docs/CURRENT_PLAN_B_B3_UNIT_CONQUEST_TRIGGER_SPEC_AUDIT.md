# Plan B / B3 Unit Conquest Trigger Spec Audit

Date: 2026-06-29

Status: focused unit-conquest draw-one, draw-or-call-rune, create-dormant-Gold, overkill create-dormant-Gold, attack-overkill gain-score, pay-return-self-to-hand, grant-self-boon, ready-self-once, grant-friendly-boon, additional-activation, friendly-power, destroy-equipment self-boon, natural battle-conquest TriggerSpec activation, official-deck midgame Treant replay, and official-deck-derived Vayne pay-return replay slices accepted; project remains **NOT READY**.

## Scope

This slice moves implemented unit conquest effects away from engine card-number branching:

- `OGN·039/298` / `OGN·039a/298` 卡莎 official text from `data/official/card-catalog.zh-CN.json`: `当我征服一处战场时，抽一张牌。`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = UNIT_CONQUEST_DRAW_ONE`
  - `Timing = UNIT_CONQUEST`
  - `TargetScope = SOURCE_UNIT`
  - `DrawCount = 1`
- `CoreRuleEngine.TryResolveBattlefieldHeldActivateUnitConquestEffectsTrigger` now routes 卡莎's representative draw effect through `UnitConquestTriggerSpecRules.TryGetUnitConquestDrawTrigger(...)`, and reads the emitted effect id plus draw count from `BehaviorSpec.Triggers`.
- `CoreRuleEngine` now also routes natural `DECLARE_BATTLE` battlefield conquest by surviving conquering units through the same shared `TryResolveUnitConquestTriggerSpecs(...)` helper. `OGN·039/298` 卡莎 is the runtime representative: `BATTLEFIELD_CONQUERED` now activates `UNIT_CONQUEST_DRAW_ONE`, emits `UNIT_CONQUEST_EFFECT_ACTIVATED`, and draws one card without adding a card-number allow-list.
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
- `UNL-018/219` 雪人斗士 official text from `data/official/card-catalog.zh-CN.json`: `当我征服一处战场时，如果你给敌方单位分配了不低于3点的过量伤害，则打出两个休眠的“金币”装备指示物。`
- `RuleTextParser` now parses that conquest text as `TriggerSpec` with:
  - `Kind = UNIT_CONQUEST_OVERKILL_CREATE_DORMANT_GOLD`
  - `Timing = UNIT_CONQUEST`
  - `TargetScope = SOURCE_UNIT`
  - `RequiredOverkillDamage = 3`
  - `CreatedTokenCount = 2`
  - `CreatedTokenName = 金币`
  - `CreatedTokenDestination = OWNER_BASE`
  - `CreatedTokenExhausted = true`
  - `CreatedTokenKeywords = [反应]`
- `CoreRuleEngine` now passes the battle-assigned overkill count from natural `DECLARE_BATTLE` conquest into the shared unit-conquest TriggerSpec resolver. 雪人斗士's representative only resolves on natural `BATTLEFIELD_CONQUERED` when `assignedOverkillDamageToEnemyUnits >= RequiredOverkillDamage`, then creates two exhausted Gold equipment tokens from `BehaviorSpec.Triggers`.
- No `UNL-018/219` card-number branch was added.
- `OGN·034/298` 泰达米尔 official text from `data/official/card-catalog.zh-CN.json`: `当我通过进攻征服一处战场时，如果你给敌方单位造成过不低于5点的过量伤害，则你获得的分数+1。`
- `RuleTextParser` now parses that conquest text as `TriggerSpec` with:
  - `Kind = UNIT_CONQUEST_ATTACK_OVERKILL_GAIN_SCORE`
  - `Timing = UNIT_CONQUEST`
  - `TargetScope = SOURCE_UNIT`
  - `RequiredOverkillDamage = 5`
  - `ScoreAmount = 1`
- `CoreRuleEngine` now routes 泰达米尔's representative through the same natural unit-conquest overkill context. When a natural attacking conquest assigns enough overkill damage, the shared resolver emits `UNIT_CONQUEST_EFFECT_ACTIVATED`, applies `ScoreAmount`, emits `SCORE_GAINED`, and propagates `MATCH_WON` when the extra score reaches the effective winning score.
- No `OGN·034/298` card-number branch was added.
- `OGN·035/298` / `SFD·223/221` / `SFD·223*/221` 薇恩 official text from `data/official/card-catalog.zh-CN.json`: `每当我征服一处战场时，你可以选择支付{{1}}来让我返回所属的手牌。`
- `RuleTextParser` now parses that conquest text as `TriggerSpec` with:
  - `Kind = UNIT_CONQUEST_PAY_1_RETURN_SELF_TO_HAND`
  - `Timing = UNIT_CONQUEST`
  - `TargetScope = SOURCE_UNIT`
  - `ManaCost = 1`
  - `ReturnOriginZone = BATTLEFIELD`
  - `ReturnDestinationZone = HAND`
  - `Optional = true`
- `CoreRuleEngine` now opens and resolves the existing trigger-payment window for 薇恩's representative through `UnitConquestTriggerSpecRules.TryGetUnitConquestPayReturnSelfToHandTrigger(...)`, reading the trigger id and mana cost from `BehaviorSpec.Triggers`. The old play-behavior source effect id `OGN_VAYNE_ASSAULT3_CONQUER_RECALL_PLAY_UNIT` is no longer a Core source selector for this trigger.
- `FullGameEndToEndTests.OfficialDeckMidgamePaysVayneConquestReturnAndScoreVictoryActionLogReplaysToFinalStateHash` now carries the same pay-return TriggerSpec through an official-deck-derived midgame battle state: legal official deck opening, official `OGN·035/298` 薇恩 and `OGN·096/298` Watchful Sentinel objects, `DECLARE_BATTLE`, `TRIGGER_PAYMENT` / `PAY_COST(SPEND_MANA:1)`, source return to owner hand, score victory, and action-log final-state replay.
- No `OGN·035/298` / `SFD·223/221` / `SFD·223*/221` card-number branch was added.
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
- The same `UNL-029/219` / `UNL-029a/219` official text also contains `你征服此处时的征服效果额外触发一次。`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = UNIT_CONQUEST_ADDITIONAL_ACTIVATION`
  - `Timing = BATTLEFIELD_CONQUERED`
  - `TargetScope = CONTROLLED_UNITS_AT_THIS_BATTLEFIELD`
  - `AdditionalTriggerCount = 1`
- `CoreRuleEngine` now computes natural `BATTLEFIELD_CONQUERED` unit-conquest repeat count from same-battlefield controlled sources that expose `UNIT_CONQUEST_ADDITIONAL_ACTIVATION`; it uses precise `ObjectLocationState.BattlefieldObjectId` to keep the modifier scoped to `此处`, and does not apply this repeat to 清算人竞技场's battlefield-held activation route.
- `FullGameEndToEndTests.OfficialDeckMidgameResolvesCrimsonSignetTreantConquestRepeatAndScoreVictoryActionLogReplaysToFinalStateHash` now carries that natural repeat path through an official-deck midgame state: normal opening state generation, server-authoritative `PLAY_CARD` / `MOVE_UNIT` / `DECLARE_BATTLE`, two repeated friendly-boon events, score victory, and action-log final-state replay.
- The old `FriendlyBoonUnitConquestCardNo` / `IsFriendlyBoonUnitConquestCardNo` branch is removed.
- `UNL-027/219` 天声玄龙 official text from `data/official/card-catalog.zh-CN.json`: `当我征服一处战场时，让一名友方单位本回合内{{S}}+8。`
- `RuleTextParser` now parses that conquest text as `TriggerSpec` with:
  - `Kind = UNIT_CONQUEST_FRIENDLY_PLUS_8_THIS_TURN`
  - `Timing = UNIT_CONQUEST`
  - `TargetScope = CONTROLLED_UNIT_ON_FIELD`
  - `PowerDelta = 8`
  - `Duration = UNTIL_END_OF_TURN`
- `CoreRuleEngine.TryResolveBattlefieldHeldActivateUnitConquestEffectsTrigger` now routes 天声玄龙's representative friendly-power effect through `UnitConquestTriggerSpecRules.TryGetUnitConquestFriendlyPowerUntilEndTrigger(...)`, and reads the emitted effect id plus power delta from `BehaviorSpec.Triggers`.
- The old `FriendlyPowerUnitConquestCardNo` / `IsFriendlyPowerUnitConquestCardNo` branch is removed.
- `OGN·056/298` 自适应机器人 official text from `data/official/card-catalog.zh-CN.json`: `当我征服一处战场时，你可以选择摧毁一件装备，以此给予我增益。`
- `RuleTextParser` now parses that conquest text as `TriggerSpec` with:
  - `Kind = UNIT_CONQUEST_DESTROY_EQUIPMENT_GRANT_SELF_BOON`
  - `Timing = UNIT_CONQUEST`
  - `TargetScope = EQUIPMENT_ON_FIELD`
  - `DestroyCount = 1`
  - `BoonCount = 1`
  - `Optional = true`
- `CoreRuleEngine.TryResolveBattlefieldHeldActivateUnitConquestEffectsTrigger` now routes 自适应机器人的 representative destroy-equipment self-boon effect through `UnitConquestTriggerSpecRules.TryGetUnitConquestDestroyEquipmentGrantSelfBoonTrigger(...)`, and reads the emitted effect id from `BehaviorSpec.Triggers`.
- The old `DestroyEquipmentBoonUnitConquestCardNo` / `IsDestroyEquipmentBoonUnitConquestCardNo` branch is removed.
- Current source-helper count for `private|public|internal static bool Is*CardNo(...)` is `0`; remaining `Is*UnitConquest*CardNo(...)` helpers in `CoreRuleEngine`: `0`.

## Non-Goals

- This closes the old card-number helper set for the current 清算人竞技场 unit-conquest representatives, adds natural battle-conquest representative routes including overkill-gated Gold creation, attack-overkill score gain, and pay-return-self-to-hand trigger payment, and covers 绯红印记树怪's `你征服此处时的征服效果额外触发一次。` plus a Vayne official-deck-derived pay-return replay representative.
- Natural battle-conquest activation now invokes the supported TriggerSpec effects for surviving conquering units; complete APNAP ordering, simultaneous multi-source ordering, and optional-target breadth remain open.
- This does not close optional target prompts, complete draw replacement / fatigue breadth, full targeting-stack-timing, the full official opening-to-5-cost Treant window, B0 full-game readiness, or project READY.

## Follow-Up

- Migrate the remaining unit-conquest helpers one effect kind at a time into `TriggerSpec` shapes, keeping simple non-targeted effects ahead of optional / targeted choices.
- After each migration, keep the source guard pattern and the `P79BattlefieldHeldActivateConquestEffects...` runtime representatives green.
