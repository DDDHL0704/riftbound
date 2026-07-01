# Plan B / B4 Battlefield Trigger Spec Audit

Date: 2026-07-01

Status: focused B4 battlefield trigger/static spec slices accepted for moved-unit power, held-next-spell Echo, held unit-cost increase, held draw-one, unit battlefield-held draw, held call-rune, held each-player call-rune, held move-unit-to-base, defend move-friendly-unit-to-base, defend grant-Steadfast, held grant-boon, held create-minion, held return-hero, held seven-units win, held pay-power score, held activate-unit-conquest-effects, conquer reveal/recycle, conquer mill, conquer recycle-rune, conquer consume-boon draw, conquer discard-draw, conquer draw-for-other-battlefields, conquer ready-runes-at-end, conquer ready-equipment, conquer pay-create-gold, conquer powerful pay-draw, conquer pay-return-unit create-Sand-Soldier, conquer pay-ready-legend, defend reveal-spell-or-recycle, conquer overkill create-Warhawk, friendly-spell draw, spell-power bonus, high-cost spell insight, unit-play boon, unit-returned call-rune, first-unit-play move-other-to-base, turn-start damage-units, turn-start destroy-draw, first-turn extra-rune, first-turn score, score delay, and winning-score increase; latest defend move-friendly-unit-to-base execution helper follow-up accepted; project remains **NOT READY**.

## Scope

The 2026-07-01 defend move-friendly-unit-to-base execution-helper follow-up removes one B4 per-effect public getter from the battlefield trigger rules surface and moves the trigger-shape predicate into shared rules:

- `BattlefieldTriggerSpecRules.TryGetBattlefieldDefendMoveFriendlyUnitToBaseTrigger(...)` is removed.
- `CoreRuleEngine` defended-battlefield target validation and resolution now route through generic `BattlefieldTriggerSpecRules.TryGetTrigger(cardNo, predicate, out trigger)` with `BattlefieldTriggerSpecRules.IsBattlefieldDefendMoveFriendlyUnitToBaseTrigger`.
- `IsBattlefieldDefendMoveFriendlyUnitToBaseTrigger` checks the parsed `BehaviorSpec.Triggers` shape for `BATTLEFIELD_DEFENSE_MOVE_FRIENDLY_UNIT_TO_BASE`, `BATTLEFIELD_DEFENDED`, `FRIENDLY_UNIT_AT_THIS_BATTLEFIELD`, `MoveCount = 1`, `MoveDestination = OWNER_BASE`, and `Optional = true`. Existing wire/event payloads keep the parsed trigger kind.
- Red/green guard: `BattlefieldDefendMoveFriendlyUnitToBaseTriggerUsesGenericSpecPredicate` rejects the removed getter and the old Core-private predicate across engine sources, and requires the shared generic predicate route.
- Remaining public `TryGetBattlefield...Trigger` getter count in `BattlefieldTriggerSpecRules` is 33. This slice does not close the other B4 execution helpers, optional trigger choice prompts, complete movement/control-zone edge cases, APNAP/simultaneous ordering, or READY.
- Validation: baseline backend full 9084/9084, focused guard / parser / Plunder Alley representatives / Teemo multiplexing / official-deck route 19/19, adjacent `Plunder|BattlefieldDefend|DefendMoveToBase|DeclareBattle|GameHub|FullGameEndToEnd|MatchRecovery|Teemo|CardCatalogBaseline` 2829/2829, backend full conformance 9085/9085.

The 2026-07-01 held move-unit-to-base execution-helper follow-up removes one B4 per-effect public getter from the battlefield trigger rules surface:

- `BattlefieldTriggerSpecRules.TryGetBattlefieldHeldMoveUnitToBaseTrigger(...)` is removed.
- `CoreRuleEngine.TryResolveBattlefieldHeldMoveUnitToBaseTrigger` now routes through generic `BattlefieldTriggerSpecRules.TryGetTrigger(cardNo, predicate, out trigger)` with `BattlefieldTriggerSpecRules.IsBattlefieldHeldMoveUnitToBaseTrigger`.
- `IsBattlefieldHeldMoveUnitToBaseTrigger` checks the parsed `BehaviorSpec.Triggers` shape for `BATTLEFIELD_HELD_MOVE_UNIT_TO_BASE`, `BATTLEFIELD_HELD`, `UNIT_AT_THIS_BATTLEFIELD`, `MoveCount = 1`, and `MoveDestination = OWNER_BASE`. Existing wire/event payloads keep the parsed trigger kind.
- Red/green guard: `BattlefieldHeldMoveUnitToBaseTriggerUsesGenericSpecPredicate` rejects the removed getter across engine sources and requires the generic predicate route.
- Remaining public `TryGetBattlefield...Trigger` getter count in `BattlefieldTriggerSpecRules` is 34. This slice does not close the other B4 execution helpers, optional trigger choice prompts, complete same-battlefield target-choice breadth, APNAP/simultaneous ordering, or READY.
- Validation: baseline backend full 9083/9083, focused guard / parser / held move-unit-to-base representatives / Rehearsal Hall official-deck route 5/5, adjacent `BattlefieldHeldMoveUnitToBase|RehearsalHall|BattlefieldHeld|MoveUnit|FullGameEndToEnd|GameHub|MatchRecovery|CardCatalogBaseline` 2793/2793, backend full conformance 9084/9084.

The 2026-07-01 held each-player call-rune execution-helper follow-up removes one B4 per-effect public getter from the battlefield trigger rules surface:

- `BattlefieldTriggerSpecRules.TryGetBattlefieldHeldEachPlayerCallRuneTrigger(...)` is removed.
- `CoreRuleEngine.TryResolveBattlefieldHeldEachPlayerCallRuneTrigger` now routes through generic `BattlefieldTriggerSpecRules.TryGetTrigger(cardNo, predicate, out trigger)` with `BattlefieldTriggerSpecRules.IsBattlefieldHeldEachPlayerCallRuneTrigger`.
- `IsBattlefieldHeldEachPlayerCallRuneTrigger` checks the parsed `BehaviorSpec.Triggers` shape for `BATTLEFIELD_HELD_EACH_PLAYER_CALL_RUNE`, `BATTLEFIELD_HELD`, `EACH_PLAYER`, and positive `runeCallCount`. Existing wire/event payloads keep the parsed trigger kind.
- Red/green guard: `BattlefieldHeldEachPlayerCallRuneTriggerUsesGenericSpecPredicate` rejects the removed getter across engine sources and requires the generic predicate route.
- Remaining public `TryGetBattlefield...Trigger` getter count in `BattlefieldTriggerSpecRules` is 35. This slice does not close the other B4 execution helpers, optional trigger ordering breadth, APNAP/simultaneous ordering, or READY.
- Validation: baseline backend full 9082/9082, focused guard / parser / held each-player call-rune representatives / Confetti Tree official-deck route 5/5, adjacent `BattlefieldHeldEachPlayerCallRune|BattlefieldHeldRunes|BattlefieldHeld|CallRune|GameHub|FullGameEndToEnd|MatchRecovery|CardCatalogBaseline` 2725/2725, backend full conformance 9083/9083.

The 2026-07-01 held call-rune execution-helper follow-up removes one B4 per-effect public getter from the battlefield trigger rules surface:

- `BattlefieldTriggerSpecRules.TryGetBattlefieldHeldCallRuneTrigger(...)` is removed.
- `CoreRuleEngine.TryResolveBattlefieldHeldCallRuneTrigger` now routes through generic `BattlefieldTriggerSpecRules.TryGetTrigger(cardNo, predicate, out trigger)` with `BattlefieldTriggerSpecRules.IsBattlefieldHeldCallRuneTrigger`.
- `IsBattlefieldHeldCallRuneTrigger` checks the parsed `BehaviorSpec.Triggers` shape for `BATTLEFIELD_HELD_CALL_RUNE`, `BATTLEFIELD_HELD`, and positive `runeCallCount`. Existing wire/event payloads keep the parsed trigger kind.
- Red/green guard: `BattlefieldHeldCallRuneTriggerUsesGenericSpecPredicate` rejects the removed getter across engine sources and requires the generic predicate route.
- Remaining public `TryGetBattlefield...Trigger` getter count in `BattlefieldTriggerSpecRules` is 36. This slice does not close held each-player call-rune, the other B4 execution helpers, optional trigger ordering breadth, APNAP/simultaneous ordering, or READY.
- Validation: baseline backend full 9081/9081, focused guard / parser / held call-rune representatives / Star Peak official-deck route 6/6, adjacent `BattlefieldHeldCallRune|BattlefieldHeldRune|BattlefieldHeld|CallRune|GameHub|FullGameEndToEnd|MatchRecovery|CardCatalogBaseline` 2724/2724, backend full conformance 9082/9082.

The 2026-07-01 held draw-one execution-helper follow-up removes one B4 per-effect public getter from the battlefield trigger rules surface:

- `BattlefieldTriggerSpecRules.TryGetBattlefieldHeldDrawTrigger(...)` is removed.
- `CoreRuleEngine.TryResolveBattlefieldHeldDrawTrigger` now routes through generic `BattlefieldTriggerSpecRules.TryGetTrigger(cardNo, predicate, out trigger)` with `BattlefieldTriggerSpecRules.IsBattlefieldHeldDrawTrigger`.
- `IsBattlefieldHeldDrawTrigger` checks the parsed `BehaviorSpec.Triggers` shape for `BATTLEFIELD_HELD_DRAW_ONE`, `BATTLEFIELD_HELD`, and positive `drawCount`. Existing wire/event payloads keep the parsed trigger kind.
- Red/green guard: `BattlefieldHeldDrawTriggerUsesGenericSpecPredicate` rejects the removed getter across engine sources and requires the generic predicate route.
- Remaining public `TryGetBattlefield...Trigger` getter count in `BattlefieldTriggerSpecRules` is 37. This slice does not close the other B4 execution helpers, unit battlefield-held draw helper cleanup, optional trigger ordering breadth, APNAP/simultaneous ordering, or READY.
- Validation: baseline backend full 9080/9080, focused guard / parser / held-draw representatives / Hidden Valley official-deck route 8/8, adjacent `BattlefieldHeldDraw|UnitBattlefieldHeldDraw|BattlefieldHeld|GameHub|FullGameEndToEnd|MatchRecovery|CardCatalogBaseline` 2713/2713, backend full conformance 9081/9081.

The 2026-07-01 held pay-power score execution-helper follow-up removes one B4 per-effect public getter from the battlefield trigger rules surface:

- `BattlefieldTriggerSpecRules.TryGetBattlefieldHeldPayPowerScoreTrigger(...)` is removed.
- `CoreRuleEngine` held-score payment-resource validation, Brush replacement validation, battle-response resume normalization, and `TryResolveBattlefieldHeldPayPowerScoreTrigger` now route through generic `BattlefieldTriggerSpecRules.TryGetTrigger(cardNo, predicate, out trigger)` with `BattlefieldTriggerSpecRules.IsBattlefieldHeldPayPowerScoreTrigger`.
- `MatchSession` held-score payment-resource prompt metadata and Brush replacement choice construction use the same generic predicate route.
- `IsBattlefieldHeldPayPowerScoreTrigger` checks the parsed `BehaviorSpec.Triggers` shape for `BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE`, `BATTLEFIELD_HELD`, positive `powerCost`, and positive `scoreAmount`. Existing wire/event payloads keep the parsed trigger kind.
- Red/green guard: `BattlefieldHeldPayPowerScoreTriggerUsesGenericSpecPredicate` rejects the removed getter across engine sources and requires the generic predicate route.
- Remaining public `TryGetBattlefield...Trigger` getter count in `BattlefieldTriggerSpecRules` is 38. This slice does not close the other B4 execution helpers, optional trigger ordering breadth, APNAP/simultaneous ordering, or READY.
- Validation: focused guard / held-score representatives / Energy Hub official-deck route 11/11, adjacent `BattlefieldHeld|PaymentResource|ActionPrompt|GameHub|FullGameEndToEnd|MatchRecovery|CardCatalogBaseline` 2836/2836, backend full conformance 9080/9080.

The 2026-07-01 MatchSession battlefield object recognition follow-up removes the B4 trigger allow-list from prompt/snapshot battlefield classification:

- `MatchSession.IsBattlefieldCardObject` no longer enumerates every implemented `BattlefieldTriggerSpecRules.TryGetBattlefield...` helper. It now reuses `BattlefieldTriggerSpecRules.HasImplementedBattlefieldTrigger(cardNo)`, matching the `CoreRuleEngine.HasImplementedBattlefieldRuleSpec` recognition path.
- This keeps prompt choice and snapshot classification on the same BehaviorSpec trigger predicate used by the authoritative engine while preserving existing static-aura and battlefield static-ability recognition checks.
- Existing per-effect `TryGetBattlefield...` helpers still back individual execution paths. This slice closes the `MatchSession` recognition allow-list only; it does not claim complete B4 execution-helper cleanup, optional trigger ordering breadth, APNAP/simultaneous ordering, or READY.
- Red/green guard: `BattlefieldSpecDomainHelpersDoNotUseCardNumberHelperNames` now extracts both `CoreRuleEngine.HasImplementedBattlefieldRuleSpec` and `MatchSession.IsBattlefieldCardObject`, rejecting direct `BattlefieldTriggerSpecRules.TryGetBattlefield...` entries in either recognition method.
- Validation: baseline backend full 9079/9079, focused guard red then green 1/1, adjacent `Battlefield|CardCatalogBaseline|MatchRecovery|FullGameEndToEnd` 3027/3027, backend full conformance 9079/9079.

The 2026-07-01 battlefield trigger rule-card recognition follow-up removes the B4 trigger allow-list from the core battlefield object classifier:

- `CoreRuleEngine.HasImplementedBattlefieldRuleSpec` no longer enumerates every implemented `BattlefieldTriggerSpecRules.TryGetBattlefield...` helper. It now asks one shared `BattlefieldTriggerSpecRules.HasImplementedBattlefieldTrigger(cardNo)` predicate for trigger-based battlefield rule-card recognition.
- `BattlefieldTriggerSpecRules.HasImplementedBattlefieldTrigger` enumerates the card's parsed `BehaviorSpec.Triggers` and applies `IsImplementedBattlefieldTrigger(...)` to the accepted B4 battlefield trigger kinds. This keeps newly parsed but unimplemented future battlefield triggers from being treated as executable battlefield rule cards until their shape predicate is admitted.
- Existing per-effect `TryGetBattlefield...` helpers still back individual execution paths. This slice closes the `CoreRuleEngine` recognition allow-list only; it does not claim complete B4 execution-helper cleanup, optional trigger ordering breadth, APNAP/simultaneous ordering, or READY.
- Red/green guard: `BattlefieldSpecDomainHelpersDoNotUseCardNumberHelperNames` now extracts the `HasImplementedBattlefieldRuleSpec` method body and rejects direct `BattlefieldTriggerSpecRules.TryGetBattlefield...` entries there.
- Validation: baseline backend full 9079/9079, focused guard red then green 1/1, adjacent `Battlefield|CardCatalogBaseline|MatchRecovery|FullGameEndToEnd` 3027/3027, backend full conformance 9079/9079.

The 2026-06-25 held activate-unit-conquest-effects follow-up moves another implemented battlefield held trigger away from engine card-number branching:

- `OGN·286/298` / 清算人竞技场 official text: `当你据守此处时，激活此处所有单位的征服效果。`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_HELD_ACTIVATE_UNIT_CONQUEST_EFFECTS`
  - `Timing = BATTLEFIELD_HELD`
  - `TargetScope = UNIT_AT_THIS_BATTLEFIELD`
- `CoreRuleEngine.TryResolveBattlefieldHeldActivateUnitConquestEffectsTrigger` now finds eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldHeldActivateUnitConquestEffectsTrigger(...)`, validates the parsed timing / target scope shape, and emits the parsed trigger kind in battlefield resolution and unit-conquest activation events.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldHeldActivateConquestEffectsCardNo` constant; the dev seed keeps only the catalog card number literal for scenario construction.
- The old `BattlefieldHeldActivateConquestEffectsCardNo` / `IsBattlefieldHeldActivateConquestEffectsCardNo` branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `48` total / `45` in `CoreRuleEngine`; Core battlefield helper count is `1`.
- This slice only routes the battlefield source trigger through `BehaviorSpec.Triggers`. The individual unit conquest-effect implementations that are activated by this battlefield remain the existing representative paths and are still part of the open B3/B4 cleanup matrix.

The 2026-06-25 defend grant-Steadfast follow-up moves another implemented battlefield defended trigger away from engine card-number branching:

- `OGN·279/298` / 强化阵地 official text: `当你防守此处时，选择一名单位，使其在本次战斗期间获得{{坚守2}}。（如果它是防守方，则{{S}}+2。）`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_DEFENSE_GRANT_STEADFAST_TWO`
  - `Timing = BATTLEFIELD_DEFENDED`
  - `TargetScope = DEFENDER_UNIT_AT_THIS_BATTLEFIELD`
  - `GrantedKeyword = 坚守`
  - `KeywordBonus = 2`
- `CoreRuleEngine.TryResolveBattlefieldDefenderSteadfastChoice` now finds eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldDefendGrantSteadfastTrigger(...)`, validates the parsed timing / target scope / keyword / bonus shape, and carries the parsed keyword bonus into combat-power calculation and event payloads.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldDefenderSteadfastTwoCardNo` constant; the dev seed keeps only the catalog card number literal for scenario construction.
- The old `BattlefieldDefenderSteadfastTwoCardNo` / `IsBattlefieldDefenderSteadfastTwoCardNo` branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `51` total / `48` in `CoreRuleEngine`; Core battlefield helper count is `4`.
- This slice preserves the existing representative target boundary: the server currently chooses among declared defender objects for this battle. Broader official choice semantics remain part of the open battlefield trigger / target-scope matrix.

The 2026-06-25 defend move-friendly-unit-to-base follow-up moves another implemented battlefield defended trigger away from engine card-number branching:

- `OGN·285/298` / 劫掠船巷 official text: `当你防守此处时，你可以选择将此处的一名友方单位移动到基地。`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_DEFENSE_MOVE_FRIENDLY_UNIT_TO_BASE`
  - `Timing = BATTLEFIELD_DEFENDED`
  - `TargetScope = FRIENDLY_UNIT_AT_THIS_BATTLEFIELD`
  - `MoveCount = 1`
  - `MoveDestination = OWNER_BASE`
  - `Optional = true`
- `CoreRuleEngine.TryResolveBattlefieldDefenderMoveToBaseChoice` and `TryResolveBattlefieldDefenderMoveToBaseTrigger` now find eligible battlefield sources through the generic `BattlefieldTriggerSpecRules.TryGetTrigger(...)` predicate route and validate the parsed timing / target scope / movement shape before accepting battlefield targets or resolving the move.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldDefendMoveFriendlyUnitToBaseCardNo` constant; the dev seed keeps only the catalog card number literal for scenario construction.
- The old `BattlefieldDefendMoveFriendlyUnitToBaseCardNo` / `IsBattlefieldDefendMoveFriendlyUnitToBaseCardNo` branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `52` total / `49` in `CoreRuleEngine`; Core battlefield helper count is `5`.
- The 2026-06-28 target-scope follow-up corrects the representative target set from declared defenders to the parsed `FRIENDLY_UNIT_AT_THIS_BATTLEFIELD` scope: `DECLARE_BATTLE` prompt metadata now derives battlefield-effect target slots from generic `TriggerSpec` timing/target scope, and Core accepts an exhausted non-defending same-battlefield friendly unit as the Plunder move target while a separate Teemo defend trigger consumes the enemy target.

The 2026-06-27 B0 follow-up adds official-deck action-log replay coverage for the same parsed Plunder Alley route without changing runtime code:

- legal Jhin vs Vex official decks submit and open through normal server prompts until P2 selects `OGN·285/298` Plunder Alley;
- the focused midgame replay stages official `OGN·096/298` Watchful Sentinel and `UNL-057/219` Wildclaw Beastmaster at that battlefield, then submits the same-battlefield friendly-unit target through `battlefieldTargetObjectIds`;
- the engine emits parsed `BATTLEFIELD_DEFENSE_MOVE_FRIENDLY_UNIT_TO_BASE` resolution plus `UNIT_MOVED_TO_BASE`, moves the selected P2 friendly unit to P2 base, and replays the resulting command log through score victory to the same final state hash.

The 2026-06-25 held pay-power score follow-up moves another implemented battlefield held trigger away from engine card-number branching:

- `SFD·214/221` / 能量枢纽 official text: `当你据守此处时，你可以选择支付{{A}}{{A}}{{A}}{{A}}，以此额外获得1分。`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE`
  - `Timing = BATTLEFIELD_HELD`
  - `PowerCost = 4`
  - `ScoreAmount = 1`
  - `Optional = true`
- `CoreRuleEngine.TryResolveBattlefieldHeldPayPowerScoreTrigger`, held-score payment-resource validation, battle-response resume filtering, and Brush battlefield replacement validation now find eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldHeldPayPowerScoreTrigger(...)` and read the power cost / score amount from `BehaviorSpec.Triggers`.
- `MatchSession` prompt metadata and battlefield-object recognition now use the same trigger-spec query instead of the old `BattlefieldHeldPayPowerScoreCardNo` constant; `BehaviorSpec.triggers` catalog typing now exposes `powerCost` to DevUi.
- The old `BattlefieldHeldPayPowerScoreCardNo` / `IsBattlefieldHeldPayPowerScoreCardNo` / fixed `BattlefieldHeldScorePowerCost` branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `54` total / `50` in `CoreRuleEngine`; Core battlefield helper count is `6`.

The 2026-06-25 winning-score increase follow-up moves another implemented battlefield static rule away from engine card-number branching:

- `OGN·276/298` / `OGN·276a/298` official text: `使赢得游戏所需的分数+1。`
- `RuleTextParser` now parses that text as `StaticAbilitySpec` with:
  - `Kind = BATTLEFIELD_WINNING_SCORE_INCREASE`
  - `Amount = 1`
- `CoreRuleEngine.EffectiveWinningScore`, `MatchSession` snapshot/prompt effective-winning-score projections, and `MatchRecovery` spectator validation now find eligible battlefield sources through `BattlefieldStaticAbilitySpecRules.TryGetBattlefieldWinningScoreIncreaseAbility(...)` and read the score-threshold modifier from `BehaviorSpec.StaticAbilities`.
- `MatchSession` battlefield-object recognition now uses the same static-ability query instead of the old `BattlefieldIncreaseWinningScoreCardNo` / `BattlefieldIncreaseWinningScoreAltCardNo` constants.
- The old `BattlefieldIncreaseWinningScoreCardNo` / `BattlefieldIncreaseWinningScoreAltCardNo` / `IsBattlefieldIncreaseWinningScoreCardNo` branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `55` total / `51` in `CoreRuleEngine`; Core battlefield helper count is `7`.

The 2026-06-27 B0 follow-up adds official-deck score-victory action-log replay coverage for the same parsed winning-score route without changing runtime code:

- legal Vex official decks submit and open through normal server prompts until P1 selects `OGN·276/298`;
- the focused replay state is derived from that official opening, includes official `OGN·290/298` Glory Arena as the first-turn score source, starts P2 on seven score, then submits replayable `END_TURN`;
- P2 first turn start projects `winningScore=9`, resolves `BATTLEFIELD_FIRST_TURN_GAIN_SCORE` to take P2 to eight without a win, then replays the resulting command log through score victory at `winningScore=9` to the same final state hash.

The 2026-06-25 score-delay follow-up moves another implemented battlefield static rule away from engine card-number branching:

- `SFD·209/221` / 遗忘丰碑 official text: `每名玩家在各自的第三回合开始前，无法从此处获得分数。`
- `RuleTextParser` now parses that text as `StaticAbilitySpec` with:
  - `Kind = BATTLEFIELD_SCORE_DELAY_UNTIL_TURN`
  - `Amount = 3`
- `CoreRuleEngine.TryBuildBattlefieldScorePreventedEvent` now finds eligible battlefield sources through `BattlefieldStaticAbilitySpecRules.TryGetBattlefieldScoreDelayUntilTurnAbility(...)` and reads the release turn ordinal from `BehaviorSpec.StaticAbilities`.
- `MatchSession` battlefield-object recognition now uses the same static-ability query instead of the old `BattlefieldScoreDelayCardNo` constant.
- The old `BattlefieldScoreDelayCardNo` / `IsBattlefieldScoreDelayCardNo` / fixed `BattlefieldScoreDelayReleasedTurnOrdinal` branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `56` total / `52` in `CoreRuleEngine`; Core battlefield helper count is `8`.
- This slice preserves the existing flat battlefield-zone representative behavior for score prevention. Full official `此处` physical battlefield scoping remains open.

The 2026-06-27 B0 follow-up adds official-deck score-victory action-log replay coverage for the same parsed Forgotten Monument route without changing runtime code:

- legal Vex official decks submit and open through normal server prompts until P1 selects `SFD·209/221` Forgotten Monument;
- the focused replay state is derived from that official opening, includes official `OGN·290/298` Glory Arena as the first-turn score source, then submits replayable `END_TURN`;
- P2 first turn start resolves the parsed `BATTLEFIELD_SCORE_DELAY_UNTIL_THIRD_TURN` static ability, emits `BATTLEFIELD_SCORE_PREVENTED` for `BATTLEFIELD_FIRST_TURN_GAIN_SCORE`, keeps P2 at zero score for that step, and replays the resulting command log through score victory to the same final state hash.

The 2026-06-25 first-turn score follow-up moves another implemented battlefield rule away from engine card-number branching:

- `OGN·290/298` / 荣耀竞技场 official text: `每名玩家在各自的第一个回合开始阶段，获得1分。`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_FIRST_TURN_GAIN_SCORE`
  - `Timing = TURN_START`
  - `TargetScope = EACH_PLAYER`
  - `FirstTurnOnly = true`
  - `ScoreAmount = 1`
- `CoreRuleEngine.ApplyBattlefieldFirstTurnScore` now finds eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldFirstTurnScoreTrigger(...)` and sums parsed score amounts from `BehaviorSpec.Triggers`.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldFirstTurnScoreCardNo` constant.
- The old `BattlefieldFirstTurnScoreCardNo` / `IsBattlefieldFirstTurnScoreCardNo` card-number branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `57` total / `53` in `CoreRuleEngine`; Core battlefield helper count is `9`.

The 2026-06-27 B0 follow-up adds official-deck score-victory action-log replay coverage for the same parsed Glory Arena route without changing runtime code:

- legal Vex official decks submit and open through normal server prompts until P1 selects `OGN·290/298` Glory Arena;
- the focused replay state is derived from that official opening, reset to a replayable P1 main phase before P2's first turn, then submits replayable `END_TURN`;
- P2 first turn start resolves the parsed `BATTLEFIELD_FIRST_TURN_GAIN_SCORE` source, emits `BATTLEFIELD_TRIGGER_RESOLVED` and `SCORE_GAINED` with amount `1`, and replays the resulting command log through score victory to the same final state hash.

The 2026-06-25 first-turn extra-rune follow-up moves another implemented battlefield rule away from engine card-number branching:

- `OGN·284/298` / 力量方尖碑 official text: `每名玩家在各自的第一个回合开始阶段，额外召出一枚符文。`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_FIRST_TURN_EXTRA_RUNE`
  - `Timing = TURN_START`
  - `TargetScope = EACH_PLAYER`
  - `RuneCallCount = 1`
  - `FirstTurnOnly = true`
- `CoreRuleEngine.BattlefieldFirstTurnExtraRuneCount` now finds eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldFirstTurnExtraRuneTrigger(...)` and sums the parsed rune count from `BehaviorSpec.Triggers`.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldFirstTurnExtraRuneCardNo` constant.
- The old `BattlefieldFirstTurnExtraRuneCardNo` / `IsBattlefieldFirstTurnExtraRuneCardNo` card-number branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `58` total / `54` in `CoreRuleEngine`; Core battlefield helper count is `10`.

The 2026-06-27 B0 follow-up adds official-deck score-victory action-log replay coverage for the same parsed Power Obelisk route without changing runtime code:

- legal Vex official decks submit and open through normal server prompts until P1 selects `OGN·284/298` Power Obelisk;
- the focused replay state is derived from that official opening, reset to a replayable P1 main phase before P2's first turn, then submits replayable `END_TURN`;
- P2 first turn start resolves the parsed `BATTLEFIELD_FIRST_TURN_EXTRA_RUNE` source, emits `RUNES_CALLED` with `count=4`, decreases P2's rune deck by four, and replays the resulting command log through score victory to the same final state hash.

The 2026-06-25 turn-start destroy-draw follow-up moves another implemented battlefield trigger away from engine card-number branching and corrects the official `此处` target boundary:

- `UNL-209/219` / 暮色玫瑰实验室 official text: `在你的开始阶段开始时，你可以选择摧毁一名此处由你控制的单位，以此抽一张牌。（此行动在得分前进行。）`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_TURN_START_DESTROY_UNIT_DRAW`
  - `Timing = TURN_START`
  - `TargetScope = CONTROLLED_UNIT_AT_THIS_BATTLEFIELD`
  - `DestroyCount = 1`
  - `DrawCount = 1`
  - `Optional = true`
- `CoreRuleEngine.ApplyBattlefieldTurnStartDestroyUnitDraw` now finds eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldTurnStartDestroyUnitDrawTrigger(...)` and reads the destroy/draw counts from `BehaviorSpec.Triggers`.
- The runtime now resolves the auto-representative destroy target from the source battlefield object's `ObjectLocations[source].BattlefieldObjectId`, so a controlled unit at another battlefield object is not destroyed by this source.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldTurnStartDestroyUnitDrawCardNo` constant, and the dev seed includes explicit battlefield `ObjectLocations` plus an off-scope controlled unit.
- The old `BattlefieldTurnStartDestroyUnitDrawCardNo` / `IsBattlefieldTurnStartDestroyUnitDrawCardNo` card-number branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `59` total / `55` in `CoreRuleEngine`; Core battlefield helper count is `11`.

The 2026-06-25 turn-start damage-units follow-up moves another implemented battlefield trigger away from engine card-number branching and corrects the official `此处` scope:

- `UNL-212/219` / 冰霜要塞 official text: `在每名玩家各自的开始阶段开始时，对此处的所有单位造成1点伤害。`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_TURN_START_DAMAGE_ALL_UNITS`
  - `Timing = TURN_START`
  - `TargetScope = UNIT_AT_THIS_BATTLEFIELD`
  - `DamageAmount = 1`
- `CoreRuleEngine.ApplyBattlefieldTurnStartDamageAllUnits` now finds eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldTurnStartDamageAllUnitsTrigger(...)` and reads the damage amount from `BehaviorSpec.Triggers`.
- The runtime now resolves the affected unit set from the source battlefield's `ObjectLocations[source].BattlefieldObjectId` and no longer damages units at unrelated battlefield objects, preserving the official `此处` boundary.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldTurnStartDamageAllUnitsCardNo` constant, and the dev seed now includes explicit battlefield `ObjectLocations` for the source and targets.
- The old `BattlefieldTurnStartDamageAllUnitsCardNo` / `IsBattlefieldTurnStartDamageAllUnitsCardNo` card-number branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `60` total / `56` in `CoreRuleEngine`; Core battlefield helper count is `12`.

The 2026-06-27 B0 follow-up adds official-deck score-victory action-log replay coverage for the same parsed Frost Hold route without changing runtime code:

- legal Vex official decks submit and open through normal server prompts until P1 selects `UNL-212/219` Frost Hold;
- the focused midgame replay stages both players' official `UNL-057/219` Wildclaw Beastmasters at that battlefield and submits replayable `END_TURN`;
- turn start resolves the parsed `BATTLEFIELD_TURN_START_DAMAGE_ALL_UNITS` source before scoring, damages only units scoped to that Frost Hold battlefield, and replays the resulting command log through score victory to the same final state hash.

The 2026-06-27 B0 follow-up adds official-deck score-victory action-log replay coverage for the same parsed Duskpetal Lab route without changing runtime code:

- legal Vex official decks submit and open through normal server prompts until P2 selects `UNL-209/219` Duskpetal Lab;
- the focused midgame replay stages one P2 official `UNL-057/219` Wildclaw Beastmaster at Duskpetal Lab and another P2 Wildclaw at a different battlefield, then submits replayable `END_TURN`;
- turn start resolves the parsed `BATTLEFIELD_TURN_START_DESTROY_UNIT_DRAW` source before scoring, destroys only the same-battlefield controlled unit, draws one card, leaves the offsite controlled unit on the battlefield, and replays the resulting command log through score victory to the same final state hash.

This slice moves one implemented battlefield trigger away from engine card-number branching:

- `OGN·277/298` / 后巷酒吧 official text: `每当一名单位从此处向别处移动时，让其本回合内{{S}}+1。`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_UNIT_MOVED_AWAY_POWER_MODIFIER`
  - `Timing = BATTLEFIELD_UNIT_MOVED_AWAY`
  - `TargetScope = MOVED_UNIT`
  - `PowerDelta = 1`
  - `Duration = UNTIL_END_OF_TURN`
- `CoreRuleEngine.ApplyBattlefieldMovedUnitPowerPlusOne` now finds eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldMovedUnitPowerModifierTrigger(...)` and reads the power delta from `BehaviorSpec.Triggers`.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldMovedUnitPowerPlusOneCardNo` constant.

The 2026-06-25 follow-up also moves one implemented held-battlefield trigger away from engine card-number branching:

- `UNL-216/219` / 皮城学院 official text: `当你据守此处时，在本回合内，你的下一个法术获得等同于其基础费用的{{回响}}。（你可以选择支付此额外费用，以重复此法术效果。）`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_HELD_NEXT_SPELL_GAINS_ECHO`
  - `Timing = BATTLEFIELD_HELD`
  - `Duration = UNTIL_END_OF_TURN`
- `CoreRuleEngine.TryResolveBattlefieldHeldNextSpellEchoTrigger` now recognizes eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldHeldNextSpellEchoTrigger(...)`.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldHeldNextSpellEchoCardNo` constant.
- The old `BattlefieldHeldNextSpellEchoCardNo` / `IsBattlefieldHeldNextSpellEchoCardNo` card-number branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `92` total / `88` in `CoreRuleEngine`.

The 2026-06-25 held unit-cost follow-up moves another implemented held-battlefield trigger away from engine card-number branching:

- `UNL-219/219` / 海力亚秘库 official text: `当你据守此处时，你的非指示物单位在本回合内的打出费用增加{{1}}。`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_HELD_NON_TOKEN_UNIT_COST_INCREASE`
  - `Timing = BATTLEFIELD_HELD`
  - `Duration = UNTIL_END_OF_TURN`
  - `ManaDelta = 1`
- `CoreRuleEngine.TryResolveBattlefieldHeldUnitCostIncreaseTrigger` now recognizes eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldHeldUnitCostIncreaseTrigger(...)`.
- `CoreRuleEngine` and `MatchSession` read the mana increase from the until-end marker; the `+1` case keeps the existing marker shape `BATTLEFIELD_HELD_NON_TOKEN_UNIT_COST_INCREASE:{playerId}` for compatibility, while larger future deltas can be encoded as a marker suffix.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldHeldUnitCostIncreaseCardNo` constant.
- The old `BattlefieldHeldUnitCostIncreaseCardNo` / `IsBattlefieldHeldUnitCostIncreaseCardNo` card-number branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `91` total / `87` in `CoreRuleEngine`; Core battlefield helper count is `43`.

The 2026-06-27 B0 follow-up adds official-deck action-log replay coverage for the same parsed Vaults of Helia route without adding a runtime card-number branch:

- legal Poppy official decks submit and open through normal server prompts until P1 selects `UNL-219/219` Vaults of Helia;
- the focused midgame replay carries `BATTLEFIELD_HELD_NON_TOKEN_UNIT_COST_INCREASE:P1` into a later main-phase `PLAY_CARD` prompt for official `OGN·211/298` Loyal Craftsman;
- the engine exposes `manaCost=3`, `minimumManaCost=4`, and `battlefieldHeldUnitCostIncreaseMana=1` in source requirements, emits `COST_PAID` with the same surcharge, and replays the resulting command log through score victory to the same final state hash.

The 2026-06-25 friendly-spell draw follow-up moves another implemented battlefield trigger away from engine card-number branching:

- `OGN·292/298` / 幻梦之树 official text: `每回合首次：当你对此处的友方单位使用法术时，抽一张牌。`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_FRIENDLY_SPELL_DRAW_ONE`
  - `Timing = BATTLEFIELD_FRIENDLY_SPELL_TARGETED`
  - `TargetScope = FRIENDLY_UNIT_AT_THIS_BATTLEFIELD`
  - `DrawCount = 1`
- `CoreRuleEngine.TryGetBattlefieldFriendlySpellDrawSource` now recognizes eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldFriendlySpellDrawTrigger(...)` and reads the draw count from `BehaviorSpec.Triggers`.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldFriendlySpellDrawCardNo` constant.
- The old `BattlefieldFriendlySpellDrawCardNo` / `IsBattlefieldFriendlySpellDrawCardNo` card-number branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `90` total / `86` in `CoreRuleEngine`; Core battlefield helper count is `42`.

The 2026-06-26 locality follow-up tightens the same BehaviorSpec-backed trigger without adding a card-number branch:

- `CoreRuleEngine.TryGetBattlefieldFriendlySpellDrawSource` now enforces `TargetScope = FRIENDLY_UNIT_AT_THIS_BATTLEFIELD` per candidate source when precise `ObjectLocations` identify the target unit's `BattlefieldObjectId`.
- The shared `FRIENDLY_UNIT_AT_THIS_BATTLEFIELD` helper is reused by `BATTLEFIELD_SPELL_POWER_PLUS_1`, so Dream Tree and Waste Hall both ignore friendly units at a different battlefield while preserving legacy fixtures that do not carry precise battlefield location data.
- `P79BattlefieldFriendlySpellTargetSkipsDifferentBattlefieldUnit` and `P79BattlefieldSpellPowerBonusSkipsDifferentBattlefieldUnit` lock this boundary.

The 2026-06-25 spell-power bonus follow-up moves another implemented battlefield trigger away from engine card-number branching:

- `UNL-205/219` / 废弃大厅 official text: `当一名玩家打出法术时，该玩家可以选择让自己在此处控制的一名单位在本回合内{{S}}+1。`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_SPELL_POWER_PLUS_1`
  - `Timing = BATTLEFIELD_SPELL_PLAYED`
  - `TargetScope = FRIENDLY_UNIT_AT_THIS_BATTLEFIELD`
  - `PowerDelta = 1`
  - `Duration = UNTIL_END_OF_TURN`
- `CoreRuleEngine.TryResolveBattlefieldSpellPowerBonusTrigger` now recognizes eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldSpellPowerBonusTrigger(...)` and reads the power delta from `BehaviorSpec.Triggers`.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldSpellPowerBonusCardNo` constant.
- The old `BattlefieldSpellPowerBonusCardNo` / `IsBattlefieldSpellPowerBonusCardNo` card-number branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `89` total / `85` in `CoreRuleEngine`; Core battlefield helper count is `41`.

The 2026-06-25 high-cost spell insight follow-up moves another implemented battlefield trigger away from engine card-number branching:

- `UNL-211/219` / 失落书库 official text: `若此战场受你控制，当你打出一张法术牌时，如果消耗了不低于{{4}}法力，则进行{{洞察}}。（查看你主牌堆顶部的一张牌。你可以选择将其回收。）`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_HIGH_COST_SPELL_INSIGHT_RECYCLE`
  - `Timing = BATTLEFIELD_SPELL_PLAYED`
  - `MinimumPaidMana = 4`
  - `RecycleCount = 1`
- `CoreRuleEngine.TryResolveBattlefieldHighCostSpellInsightTrigger` now recognizes eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldHighCostSpellInsightRecycleTrigger(...)` and reads both the paid-mana threshold and recycle count from `BehaviorSpec.Triggers`.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldHighCostSpellInsightCardNo` constant.
- The old `BattlefieldHighCostSpellInsightCardNo` / `IsBattlefieldHighCostSpellInsightCardNo` card-number branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `87` total / `83` in `CoreRuleEngine`; Core battlefield helper count is `39`.

The 2026-06-25 unit-play boon follow-up moves another implemented battlefield trigger away from engine card-number branching:

- `UNL-218/219` / 偶像谷 official text: `当一名玩家在此处打出一名单位时，该玩家可以选择支付{{1}}，以此给予该单位{{增益}}。（未拥有增益的单位获得一个{{S}}+1增益。）`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_PLAY_UNIT_PAY_1_GRANT_BOON`
  - `Timing = BATTLEFIELD_UNIT_PLAYED`
  - `TargetScope = PLAYED_UNIT_AT_THIS_BATTLEFIELD`
  - `ManaCost = 1`
  - `BoonCount = 1`
- `CoreRuleEngine.TryResolveBattlefieldPlayUnitPayOneBoonTrigger` now recognizes eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldPlayUnitPayBoonTrigger(...)` and reads both the mana cost and boon count from `BehaviorSpec.Triggers`.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldPlayUnitPayOneBoonCardNo` constant.
- The old `BattlefieldPlayUnitPayOneBoonCardNo` / `IsBattlefieldPlayUnitPayOneBoonCardNo` card-number branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `85` total / `81` in `CoreRuleEngine`; Core battlefield helper count is `37`.

The 2026-06-25 unit-returned call-rune follow-up moves another implemented battlefield trigger away from engine card-number branching:

- `UNL-214/219` / 鬼影湾 official text: `当此处的一名单位返回到一名玩家的手牌时，该玩家可以选择支付{{1}}，以此召出一枚休眠的符文。`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_UNIT_RETURNED_PAY_1_CALL_RUNE`
  - `Timing = BATTLEFIELD_UNIT_RETURNED`
  - `TargetScope = RETURNED_UNIT_AT_THIS_BATTLEFIELD`
  - `ManaCost = 1`
  - `RuneCallCount = 1`
- `CoreRuleEngine.TryResolveBattlefieldUnitReturnedCallRuneTrigger` now recognizes eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldUnitReturnedPayCallRuneTrigger(...)` and reads both the mana cost and rune-call count from `BehaviorSpec.Triggers`.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldUnitReturnedCallRuneCardNo` constant.
- The old `BattlefieldUnitReturnedCallRuneCardNo` / `IsBattlefieldUnitReturnedCallRuneCardNo` / `BattlefieldUnitReturnedCallRuneManaCost` card-number and fixed-cost branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `84` total / `80` in `CoreRuleEngine`; Core battlefield helper count is `36`.

The 2026-06-25 first-unit-play move-other-to-base follow-up moves another implemented battlefield trigger away from engine card-number branching:

- `UNL-215/219` / 流星疗泉 official text: `每回合首次，当玩家在此处打出一名非指示物单位时，该玩家可以选择将自己在此处控制的另一名单位移动到其基地。`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_FIRST_UNIT_PLAYED_MOVE_OTHER_TO_BASE`
  - `Timing = BATTLEFIELD_UNIT_PLAYED`
  - `TargetScope = OTHER_CONTROLLED_UNIT_AT_THIS_BATTLEFIELD`
  - `MoveCount = 1`
  - `MoveDestination = OWNER_BASE`
  - `OncePerTurn = true`
  - `ExcludesTokens = true`
- `CoreRuleEngine.TryResolveBattlefieldFirstUnitPlayedMoveOtherToBaseTrigger` now recognizes eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldFirstUnitPlayedMoveOtherToBaseTrigger(...)` and reads the once-per-turn, non-token, target-scope and movement parameters from `BehaviorSpec.Triggers`.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldFirstUnitPlayedMoveOtherToBaseCardNo` constant.
- The old `BattlefieldFirstUnitPlayedMoveOtherToBaseCardNo` / `IsBattlefieldFirstUnitPlayedMoveOtherToBaseCardNo` card-number branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `83` total / `79` in `CoreRuleEngine`; Core battlefield helper count is `35`.

The 2026-06-25 held draw-one follow-up moves another implemented battlefield trigger away from engine card-number branching:

- `OGN·280/298` official text: `当你据守此处时，抽一张牌。`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_HELD_DRAW_ONE`
  - `Timing = BATTLEFIELD_HELD`
  - `DrawCount = 1`
- `CoreRuleEngine.TryResolveBattlefieldHeldDrawTrigger` now recognizes eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldHeldDrawTrigger(...)` and reads the draw count from `BehaviorSpec.Triggers`.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldHoldDrawCardNo` constant.
- The old `BattlefieldHoldDrawCardNo` / `IsBattlefieldHoldDrawCardNo` card-number branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `82` total / `78` in `CoreRuleEngine`; Core battlefield helper count is `34`.

The 2026-06-27 unit battlefield-held draw follow-up moves the implemented Dunehorn Beast held trigger away from engine card-number branching:

- `SFD·027/221` / 穿沙角兽 official text: `如果你的手牌不超过两张，则我以活跃状态进场。` / `当我据守一处战场时，抽两张牌。`
- `RuleTextParser` now parses the held-draw sentence as `TriggerSpec` with:
  - `Kind = UNIT_BATTLEFIELD_HELD_DRAW`
  - `Timing = BATTLEFIELD_HELD`
  - `TargetScope = SOURCE_UNIT`
  - `DrawCount = 2`
- `CoreRuleEngine.TryResolveUnitBattlefieldHeldDrawTriggers` now recognizes surviving held units through `UnitBattlefieldHeldTriggerSpecRules.TryGetUnitBattlefieldHeldDrawTrigger(...)` and reads the draw count from `BehaviorSpec.Triggers`.
- The old `DunehornBeastCardNo` / `DunehornBeastBattlefieldHeldDrawEffectKind` card-number branch is removed. The emitted runtime event uses the parsed trigger kind for both `trigger` and `effectKind`.
- This slice only closes the unit battlefield-held draw path. The separate low-hand active entry condition is now tracked and covered by `docs/CURRENT_PLAN_B_OTHER_FRIENDLY_ACTIVE_ENTRY_STATIC_ABILITY_SPEC_AUDIT.md`.

The 2026-06-27 B0 follow-up adds official-deck action-log replay coverage for the same parsed Dunehorn Beast route without changing runtime code:

- legal Jhin official decks submit and open through normal server prompts;
- the focused midgame replay stages P2 official `SFD·027/221` Dunehorn Beast as a surviving defender holding P2's selected slow battlefield;
- the engine emits parsed `UNIT_BATTLEFIELD_HELD_DRAW`, draws two controlled main-deck cards for P2, and replays the resulting command log through score victory to the same final state hash.

The 2026-06-25 held call-rune follow-up moves another implemented held-battlefield trigger away from engine card-number branching:

- `OGN·288/298` / 星尖峰 official text: `当你据守此处时，你可以选择召出一枚休眠的符文。`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_HELD_CALL_RUNE`
  - `Timing = BATTLEFIELD_HELD`
  - `RuneCallCount = 1`
- `CoreRuleEngine.TryResolveBattlefieldHeldCallRuneTrigger` now recognizes eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldHeldCallRuneTrigger(...)` and reads the rune-call count from `BehaviorSpec.Triggers`.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldHoldCallRuneCardNo` constant.
- The old `BattlefieldHoldCallRuneCardNo` / `IsBattlefieldHoldCallRuneCardNo` card-number branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `81` total / `77` in `CoreRuleEngine`; Core battlefield helper count is `33`.

The 2026-06-25 held each-player call-rune follow-up moves another implemented held-battlefield trigger away from engine card-number branching:

- `SFD·219/221` / 彩纸灵树 official text: `当你据守此处时，每名玩家召出一枚休眠的符文。`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_HELD_EACH_PLAYER_CALL_RUNE`
  - `Timing = BATTLEFIELD_HELD`
  - `TargetScope = EACH_PLAYER`
  - `RuneCallCount = 1`
- `CoreRuleEngine.TryResolveBattlefieldHeldEachPlayerCallRuneTrigger` now recognizes eligible battlefield sources through the generic `BattlefieldTriggerSpecRules.TryGetTrigger(...)` predicate route and reads the target scope and rune-call count from `BehaviorSpec.Triggers`.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldHoldEachPlayerCallRuneCardNo` constant.
- The old `BattlefieldHoldEachPlayerCallRuneCardNo` / `IsBattlefieldHoldEachPlayerCallRuneCardNo` card-number branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `80` total / `76` in `CoreRuleEngine`; Core battlefield helper count is `32`.

The 2026-06-25 held move-unit-to-base follow-up moves another implemented held-battlefield trigger away from engine card-number branching:

- `UNL-207/219` / 业余排练厅 official text: `当你据守此处时，你可以选择将战场上的一名单位移动到其基地。`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_HELD_MOVE_UNIT_TO_BASE`
  - `Timing = BATTLEFIELD_HELD`
  - `TargetScope = UNIT_AT_THIS_BATTLEFIELD`
  - `MoveCount = 1`
  - `MoveDestination = OWNER_BASE`
- `CoreRuleEngine.TryResolveBattlefieldHeldMoveUnitToBaseTrigger` now recognizes eligible battlefield sources through the generic `BattlefieldTriggerSpecRules.TryGetTrigger(...)` predicate route and reads the target scope and movement parameters from `BehaviorSpec.Triggers`.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldHeldMoveUnitToBaseCardNo` constant.
- The old `BattlefieldHeldMoveUnitToBaseCardNo` / `IsBattlefieldHeldMoveUnitToBaseCardNo` card-number branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `79` total / `75` in `CoreRuleEngine`; Core battlefield helper count is `31`.

The 2026-06-27 B0 score-victory follow-up extends the same parsed Rehearsal Hall route without adding any card-number branch:

- legal official decks submit and open through normal prompts until P2 selects `UNL-207/219` Rehearsal Hall;
- the focused midgame replay stages official `OGN·096/298` Watchful Sentinel and `UNL-057/219` Wildclaw Beastmaster at that battlefield, resolves the held trigger, moves the surviving defender to its owner's base, and then continues through score victory;
- `CoreRuleEngine.ApplyBattlefieldHeldScoresAtTurnStart` now uses the shared effective field controller resolver, so official battlefield objects with null `ControllerId` still score for their owner / effective controller during turn-start held scoring.

The 2026-06-25 held grant-boon follow-up moves another implemented held-battlefield trigger away from engine card-number branching:

- `OGN·283/298` / 纳沃利角斗场 official text: `当你据守此处时，给予此处的一名单位增益。（如果该单位未拥有增益，则获得一个{{S}}+1增益。）`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_HELD_GRANT_BOON`
  - `Timing = BATTLEFIELD_HELD`
  - `TargetScope = UNIT_AT_THIS_BATTLEFIELD`
  - `BoonCount = 1`
- `CoreRuleEngine.TryResolveBattlefieldHeldGrantBoonTrigger` now recognizes eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldHeldGrantBoonTrigger(...)` and reads the target scope and boon count from `BehaviorSpec.Triggers`.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldHoldGrantBoonCardNo` constant.
- The old `BattlefieldHoldGrantBoonCardNo` / `IsBattlefieldHoldGrantBoonCardNo` card-number branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `78` total / `74` in `CoreRuleEngine`; Core battlefield helper count is `30`.

The 2026-06-25 held create-minion follow-up moves another implemented held-battlefield trigger away from engine card-number branching:

- `OGN·275/298` / 团结圣坛 official text: `当你据守此处时，打出一名1{{S}}的“随从”到你的基地。`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_HELD_CREATE_MINION`
  - `Timing = BATTLEFIELD_HELD`
  - `CreatedTokenCount = 1`
  - `CreatedTokenName = 随从`
  - `CreatedTokenPower = 1`
  - `CreatedTokenDestination = OWNER_BASE`
- `CoreRuleEngine.TryResolveBattlefieldHeldCreateMinionTrigger` now recognizes eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldHeldCreateMinionTrigger(...)` and reads token count, token family, token power and destination from `BehaviorSpec.Triggers`.
- The token identity is resolved through `P6TokenFactoryCatalog` by token family, power and unit tag; the current official representative continues to create `OGN·271/298`, but the runtime no longer names that card number as the trigger's source condition.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldHoldCreateMinionCardNo` constant.
- The old `BattlefieldHoldCreateMinionCardNo` / `IsBattlefieldHoldCreateMinionCardNo` card-number branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `76` total / `73` in `CoreRuleEngine`; Core battlefield helper count is `29`.

The 2026-06-25 held return-hero follow-up moves another implemented held-battlefield trigger away from engine card-number branching:

- `OGN·281/298` / 圣化之墓 official text: `当你据守此处时，如果你的英雄区域已无英雄单位牌，则可以选择让该英雄从废牌堆中返回英雄区域。`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_HELD_RETURN_HERO_FROM_GRAVEYARD`
  - `Timing = BATTLEFIELD_HELD`
  - `TargetScope = OWNED_HERO_UNIT_IN_GRAVEYARD`
  - `ReturnCount = 1`
  - `RequiredEmptyZone = CHAMPION`
  - `ReturnOriginZone = GRAVEYARD`
  - `ReturnDestinationZone = CHAMPION`
  - `ReturnCardFilter = TAG:CARD_CATEGORY:英雄单位`
- `CoreRuleEngine.TryResolveBattlefieldHeldReturnHeroTrigger` now recognizes eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldHeldReturnHeroTrigger(...)` and reads the empty-zone gate, return origin/destination, count and hero-unit filter from `BehaviorSpec.Triggers`.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldHeldReturnHeroCardNo` constant.
- The old `BattlefieldHeldReturnHeroCardNo` / `IsBattlefieldHeldReturnHeroCardNo` card-number branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `75` total / `72` in `CoreRuleEngine`; Core battlefield helper count is `28`.

The 2026-06-25 held seven-units win follow-up moves another implemented held-battlefield trigger away from engine card-number branching:

- `OGN·293/298` / `OGN·293a/298` / 宏伟广场 official text: `当你据守此处，且在此拥有至少七名单位时，你赢得游戏胜利。`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_HELD_SEVEN_UNITS_WIN`
  - `Timing = BATTLEFIELD_HELD`
  - `TargetScope = CONTROLLED_UNITS_AT_THIS_BATTLEFIELD`
  - `RequiredUnitCount = 7`
  - `WinsGame = true`
- `CoreRuleEngine.TryResolveBattlefieldHeldSevenUnitsWinTrigger` now recognizes eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldHeldSevenUnitsWinTrigger(...)`, reads the required count / win flag from `BehaviorSpec.Triggers`, and counts controlled units whose `ObjectLocations.BattlefieldObjectId` is the held battlefield object.
- The representative regression now covers the official `在此` boundary: seven controlled battlefield-zone units split across multiple battlefields no longer satisfy the condition.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldHeldSevenUnitsWinCardNo` / `BattlefieldHeldSevenUnitsWinAltCardNo` constants.
- The old `BattlefieldHeldSevenUnitsWinCardNo` / `BattlefieldHeldSevenUnitsWinAltCardNo` / `IsBattlefieldHeldSevenUnitsWinCardNo` card-number branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `75` total / `71` in `CoreRuleEngine`; Core battlefield helper count is `27`.

The 2026-06-25 conquer reveal/recycle follow-up moves another implemented conquered-battlefield trigger away from engine card-number branching:

- `OGN·291/298` / 烛光圣殿 official text: `当你征服此处时，查看主牌堆顶部的两张牌。你可以选择从这两张牌中回收任意数量的卡牌，并将其余的卡牌按任意顺序放回原处。`
- `RuleTextParser` now parses that two-sentence official text as one `TriggerSpec` with:
  - `Kind = BATTLEFIELD_CONQUERED_REVEAL_TOP_TWO_RECYCLE`
  - `Timing = BATTLEFIELD_CONQUERED`
  - `RevealCount = 2`
  - `RevealSourceZone = MAIN_DECK`
  - `RecycleCount = 2`
  - `RecycleDestinationZone = MAIN_DECK`
- `CoreRuleEngine.ResolveBattlefieldConquerRevealRecycleTrigger` now recognizes eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldConquerRevealRecycleTrigger(...)` and reads the reveal/recycle counts and zones from `BehaviorSpec.Triggers`.
- The runtime keeps the existing deterministic representative path: it reveals the top two controlled main-deck cards, recycles the parsed count, and returns any non-recycled revealed cards to the top in original order. The official optional choice and ordering prompt remains outside this narrow routing slice.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldConquerRevealRecycleCardNo` constant.
- The old `BattlefieldConquerRevealRecycleCardNo` / `IsBattlefieldConquerRevealRecycleCardNo` card-number branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `74` total / `70` in `CoreRuleEngine`; Core battlefield helper count is `26`.

The 2026-06-27 B0 replay follow-up adds no runtime rule changes. It proves the parsed `OGN·291/298` Candlelit Sanctum route can be reached from a verified legal official-deck opening, start from a focused midgame `START_BATTLE` state, reveal the top two controlled main-deck cards, recycle the parsed count to the bottom of that deck, and replay through score victory to the same final state hash.

The 2026-06-25 conquer mill follow-up moves another implemented conquered-battlefield trigger away from engine card-number branching:

- `SFD·212/221` / 雷区 official text: `当你征服此处时，将你主牌堆顶部的两张牌放入废牌堆。`
- `RuleTextParser` now parses that official text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_CONQUERED_MILL_TOP_TWO`
  - `Timing = BATTLEFIELD_CONQUERED`
  - `MillCount = 2`
  - `MillSourceZone = MAIN_DECK`
  - `MillDestinationZone = GRAVEYARD`
- `CoreRuleEngine.TryResolveBattlefieldConquerMillTwoTrigger` now recognizes eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldConquerMillTrigger(...)` and reads the mill count and zones from `BehaviorSpec.Triggers`.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldConquerMillTwoCardNo` constant.
- The old `BattlefieldConquerMillTwoCardNo` / `IsBattlefieldConquerMillTwoCardNo` card-number branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `73` total / `69` in `CoreRuleEngine`; Core battlefield helper count is `25`.

The 2026-06-27 B0 replay follow-up adds no runtime rule changes. It proves the parsed `SFD·212/221` Minefield route can be reached from a verified legal official-deck opening, start from a focused midgame `START_BATTLE` state, move the top two controlled main-deck cards to graveyard, and replay through score victory to the same final state hash.

The 2026-06-25 conquer recycle-rune follow-up moves another implemented conquered-battlefield trigger away from engine card-number branching:

- `OGN·287/298` / 雷霆之纹 official text: `当你征服此处时，回收一枚你的符文。`
- `RuleTextParser` now parses that official text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_CONQUERED_RECYCLE_RUNE`
  - `Timing = BATTLEFIELD_CONQUERED`
  - `TargetScope = OWNED_RUNE_IN_BASE`
  - `RecycleCount = 1`
  - `RecycleSourceZone = BASE`
  - `RecycleDestinationZone = MAIN_DECK`
- `CoreRuleEngine.ResolveBattlefieldConquerRecycleRuneTrigger` now recognizes eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldConquerRecycleRuneTrigger(...)` and reads the rune count and source/destination zones from `BehaviorSpec.Triggers`.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldConquerRecycleRuneCardNo` constant.
- The old `BattlefieldConquerRecycleRuneCardNo` / `IsBattlefieldConquerRecycleRuneCardNo` card-number branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `72` total / `68` in `CoreRuleEngine`; Core battlefield helper count is `24`.

The 2026-06-28 B0 replay follow-up adds no runtime rule changes. It proves the parsed `OGN·287/298` Thunder Sigil / 雷霆之纹 route can be reached from a verified legal official-deck opening, start from a focused midgame `START_BATTLE` state, recycle one controlled base rune to the bottom of the main deck, and replay through score victory to the same final state hash.

The 2026-06-25 conquer consume-boon draw follow-up moves another implemented conquered-battlefield trigger away from engine card-number branching:

- `OGN·282/298` / 希拉娜修道院 official text: `当你征服此处时，你可以选择消耗一个增益，以此抽一张牌。`
- `RuleTextParser` now parses that official text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_CONQUERED_CONSUME_BOON_DRAW`
  - `Timing = BATTLEFIELD_CONQUERED`
  - `TargetScope = CONTROLLED_BOON_UNIT_ON_FIELD`
  - `ConsumedBoonCount = 1`
  - `DrawCount = 1`
- `CoreRuleEngine.TryResolveBattlefieldConquerConsumeBoonDrawTrigger` now recognizes eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldConquerConsumeBoonDrawTrigger(...)` and reads the draw count and consumed-boon count from `BehaviorSpec.Triggers`.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldConquerConsumeBoonDrawCardNo` constant.
- The old `BattlefieldConquerConsumeBoonDrawCardNo` / `IsBattlefieldConquerConsumeBoonDrawCardNo` card-number branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `71` total / `67` in `CoreRuleEngine`; Core battlefield helper count is `23`.

The 2026-06-25 conquer discard-draw follow-up moves another implemented conquered-battlefield trigger away from engine card-number branching:

- `OGN·298/298` / 祖安地沟 official text: `当你征服此处时，弃置一张手牌，然后抽一张牌。`
- `RuleTextParser` now parses that official text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_CONQUERED_DISCARD_DRAW`
  - `Timing = BATTLEFIELD_CONQUERED`
  - `TargetScope = CONTROLLED_HAND_CARD`
  - `DiscardCount = 1`
  - `DiscardSourceZone = HAND`
  - `DiscardDestinationZone = GRAVEYARD`
  - `DrawCount = 1`
- `CoreRuleEngine.TryResolveBattlefieldConquerDiscardDrawTrigger` now recognizes eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldConquerDiscardDrawTrigger(...)` and reads the discard/draw counts and discard zones from `BehaviorSpec.Triggers`.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldConquerDiscardDrawCardNo` constant.
- The old `BattlefieldConquerDiscardDrawCardNo` / `IsBattlefieldConquerDiscardDrawCardNo` card-number branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `70` total / `66` in `CoreRuleEngine`; Core battlefield helper count is `22`.

The 2026-06-28 B0 replay follow-up adds no runtime rule changes. It proves the parsed `OGN·298/298` Zaun Sump / 祖安地沟 route can be reached from a verified legal official-deck opening, start from a focused midgame `START_BATTLE` state, discard one controlled hand card to graveyard, draw one controlled main-deck card, and replay through score victory to the same final state hash.

The 2026-06-25 conquer draw-for-other-battlefields follow-up moves another implemented conquered-battlefield trigger away from engine card-number branching:

- `SFD·217/221` / 权能之座 official text: `当你征服此处时，你和盟友每控制一处其他战场，你便抽一张牌。`
- `RuleTextParser` now parses that official text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_CONQUERED_DRAW_FOR_OTHER_BATTLEFIELDS`
  - `Timing = BATTLEFIELD_CONQUERED`
  - `TargetScope = OTHER_CONTROLLED_BATTLEFIELDS`
  - `DrawCountPerParticipant = 1`
- `CoreRuleEngine.TryResolveBattlefieldConquerDrawForOtherBattlefieldsTrigger` now recognizes eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldConquerDrawForOtherBattlefieldsTrigger(...)` and reads the per-other-battlefield draw count from `BehaviorSpec.Triggers`.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldConquerDrawForOtherBattlefieldsCardNo` constant.
- The old `BattlefieldConquerDrawForOtherBattlefieldsCardNo` / `IsBattlefieldConquerDrawForOtherBattlefieldsCardNo` card-number branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `69` total / `65` in `CoreRuleEngine`; Core battlefield helper count is `21`.

The 2026-06-28 B0 replay follow-up adds no runtime rule changes. It proves the parsed `SFD·217/221` Seat of Power / 权能之座 route can be reached from a verified legal official-deck opening, start from a focused midgame `START_BATTLE` state with two other controlled battlefield source objects, draw two controlled main-deck cards, and replay through score victory to the same final state hash.

The 2026-06-25 conquer ready-runes-at-end follow-up moves another implemented conquered-battlefield trigger away from engine card-number branching:

- `OGN·289/298` / 巨神峰之巅 official text: `当你征服此处时，选择两枚符文，并在本回合结束时，让它们变为活跃状态。`
- `RuleTextParser` now parses that official text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_CONQUERED_READY_RUNES_AT_END`
  - `Timing = BATTLEFIELD_CONQUERED`
  - `TargetScope = OWNED_RUNE_IN_BASE`
  - `RuneReadyCount = 2`
  - `ReadyTiming = END_OF_TURN`
- `CoreRuleEngine.TryResolveBattlefieldConquerReadyRunesAtEndTrigger` now recognizes eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldConquerReadyRunesAtEndTrigger(...)` and reads the rune count and delayed ready timing from `BehaviorSpec.Triggers`.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldConquerReadyTwoRunesAtEndCardNo` constant.
- The old `BattlefieldConquerReadyTwoRunesAtEndCardNo` / `IsBattlefieldConquerReadyTwoRunesAtEndCardNo` card-number branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `68` total / `64` in `CoreRuleEngine`; Core battlefield helper count is `20`.

The 2026-06-28 B0 replay follow-up adds no runtime rule changes. It proves the parsed `OGN·289/298` Mount Targon / 巨神峰之巅 route can be reached from a verified legal official-deck opening, start from a focused midgame `START_BATTLE` state with two exhausted controlled base runes, schedule two delayed ready-rune markers on conquest, consume those markers on a subsequent server-authored `END_TURN`, ready the same runes, and replay through score victory to the same final state hash.

The 2026-06-25 conquer ready-equipment follow-up moves another implemented conquered-battlefield trigger away from engine card-number branching:

- `SFD·221/221` / 月帷祭坛 official text: `当你征服此处时，你可以选择让一件友方装备变为活跃状态。如果它是一件武装，则你可以选择将其卸除。`
- `RuleTextParser` now parses that official text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_CONQUERED_READY_EQUIPMENT`
  - `Timing = BATTLEFIELD_CONQUERED`
  - `TargetScope = FRIENDLY_EQUIPMENT`
  - `EquipmentReadyCount = 1`
  - `DetachesArmament = true`
- `CoreRuleEngine.TryResolveBattlefieldConquerReadyEquipmentTrigger` now recognizes eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldConquerReadyEquipmentTrigger(...)` and reads the equipment count / armament detach policy from `BehaviorSpec.Triggers`.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldConquerReadyEquipmentCardNo` constant.
- The old `BattlefieldConquerReadyEquipmentCardNo` / `IsBattlefieldConquerReadyEquipmentCardNo` card-number branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `67` total / `63` in `CoreRuleEngine`; Core battlefield helper count is `19`.

The 2026-06-25 conquer pay-create-gold follow-up moves another implemented conquered-battlefield trigger away from engine card-number branching:

- `SFD·220/221` / 珍宝堆 official text: `当你征服此处时，你可以选择支付{{1}}，以此打出一个休眠的“金币”装备指示物。`
- `RuleTextParser` now parses that official text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_CONQUERED_PAY_1_CREATE_GOLD`
  - `Timing = BATTLEFIELD_CONQUERED`
  - `ManaCost = 1`
  - `CreatedTokenCount = 1`
  - `CreatedTokenName = 金币`
  - `CreatedTokenDestination = OWNER_BASE`
  - `CreatedTokenExhausted = true`
- `CoreRuleEngine.TryOpenBattlefieldConquerPayOneCreateGoldPaymentWindow` now recognizes eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldConquerPayCreateGoldTrigger(...)`, reads the mana cost from `BehaviorSpec.Triggers`, and builds the payment choice from that parsed cost.
- `CoreRuleEngine.ResolveBattlefieldConquerGoldTriggerPayment` revalidates the same parsed trigger after payment and creates the parsed exhausted equipment token through the existing server-authoritative token path.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldConquerPayOneCreateGoldCardNo` constant. The development seed keeps the official card number only as fixture data.
- The old `BattlefieldConquerPayOneCreateGoldCardNo` / `IsBattlefieldConquerPayOneCreateGoldCardNo` card-number branch and `BattlefieldGoldManaCost` fixed-cost constant are removed. Current source-helper count for `private static bool Is*CardNo(...)` is `66` total / `62` in `CoreRuleEngine`; Core battlefield helper count is `18`.

The 2026-06-26 B0 replay follow-up adds no runtime rule changes. It proves the parsed `SFD·220/221` Treasure Pile trigger-payment route can be reached from a verified legal official-deck opening, start from a focused midgame `START_BATTLE` state, open `TRIGGER_PAYMENT`, accept replayable `PAY_COST(SPEND_MANA:1)`, create an exhausted Gold token, and replay through score victory to the same final state hash.

The 2026-06-25 conquer powerful pay-draw follow-up moves another implemented conquered-battlefield trigger away from engine card-number branching:

- `SFD·218/221` / 沉没神庙 official text: `当你征服此处时，如果此战场上留存至少一名{{强力}}单位，则你可以选择支付{{1}}来抽一张牌。（战力达到5或以上时，即为强力单位。）`
- `RuleTextParser` now parses that official text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_CONQUERED_POWERFUL_PAY_1_DRAW`
  - `Timing = BATTLEFIELD_CONQUERED`
  - `TargetScope = SURVIVING_POWERFUL_UNIT_AT_THIS_BATTLEFIELD`
  - `RequiredPowerThreshold = 5`
  - `ManaCost = 1`
  - `DrawCount = 1`
- `CoreRuleEngine.TryOpenBattlefieldConquerPowerfulPayDrawPaymentWindow` now recognizes eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldConquerPowerfulPayDrawTrigger(...)`, reads the mana cost / draw count / power threshold from `BehaviorSpec.Triggers`, and builds the payment choice from that parsed cost.
- `CoreRuleEngine.ResolveBattlefieldConquerPowerfulDrawTriggerPayment` revalidates the same parsed trigger after payment and rechecks the selected surviving powerful unit before drawing.
- The payment-window selector now evaluates all surviving conquest attackers instead of only the first attacker object, matching the official `此战场上留存至少一名{{强力}}单位` condition.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldConquerPowerfulPayOneDrawCardNo` constant. The development seed keeps the official card number only as fixture data.
- The old `BattlefieldConquerPowerfulPayOneDrawCardNo` / `IsBattlefieldConquerPowerfulPayOneDrawCardNo` card-number branch and `BattlefieldPowerfulDrawManaCost` fixed-cost constant are removed. Current source-helper count for `private static bool Is*CardNo(...)` is `65` total / `61` in `CoreRuleEngine`; Core battlefield helper count is `17`.

The 2026-06-26 B0 replay follow-up adds no runtime rule changes. It proves the parsed `SFD·218/221` Sunken Temple trigger-payment route can be reached from a verified legal official-deck opening, start from a focused midgame `START_BATTLE` state, open `TRIGGER_PAYMENT` only after conquest with a surviving powerful attacker, accept replayable `PAY_COST(SPEND_MANA:1)`, draw one controlled main-deck card, and replay through score victory to the same final state hash.

The 2026-06-25 conquer pay-return-unit create-Sand-Soldier follow-up moves another implemented conquered-battlefield trigger away from engine card-number branching:

- `SFD·207/221` / 帝王神坛 official text: `当你征服此处时，你可以选择支付{{1}}并让你在此处控制的一名单位返回其所属的手牌，以此在此处打出一名2{{S}}的“黄沙士兵”。`
- `RuleTextParser` now parses that official text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_CONQUERED_PAY_1_RETURN_UNIT_CREATE_SAND_SOLDIER`
  - `Timing = BATTLEFIELD_CONQUERED`
  - `TargetScope = CONTROLLED_UNIT_AT_THIS_BATTLEFIELD`
  - `ManaCost = 1`
  - `ReturnCount = 1`
  - `ReturnOriginZone = BATTLEFIELD`
  - `ReturnDestinationZone = HAND`
  - `CreatedTokenCount = 1`
  - `CreatedTokenName = 黄沙士兵`
  - `CreatedTokenPower = 2`
  - `CreatedTokenDestination = BATTLEFIELD`
  - `CreatedTokenExhausted = false`
- `CoreRuleEngine.TryOpenBattlefieldConquerPayOneReturnUnitCreateSandSoldierPaymentWindow` now recognizes eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldConquerPayReturnUnitCreateSandSoldierTrigger(...)`, reads the mana cost / return zones / token name and token power from `BehaviorSpec.Triggers`, and opens `TRIGGER_PAYMENT`; `ResolveBattlefieldConquerSandSoldierTriggerPayment` commits the payment before returning the selected unit and creating the concrete unit token through `P6TokenFactoryCatalog` by token family and power.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldConquerPayOneReturnUnitCreateSandSoldierCardNo` constant. The development seed keeps the official card number only as fixture data.
- The old `BattlefieldConquerPayOneReturnUnitCreateSandSoldierCardNo` / `IsBattlefieldConquerPayOneReturnUnitCreateSandSoldierCardNo` card-number branch and `BattlefieldSandSoldierManaCost` fixed-cost constant are removed. Current source-helper count for `private static bool Is*CardNo(...)` is `64` total / `60` in `CoreRuleEngine`; Core battlefield helper count is `16`.

The 2026-06-27 B0 replay follow-up proves the parsed `SFD·207/221` Imperial Shrine route can be reached from a verified legal official-deck opening, start from a focused midgame `START_BATTLE` state, open `TRIGGER_PAYMENT`, accept replayable `PAY_COST(SPEND_MANA:1)` to pay the parsed cost, return the controlled conquest attacker to hand and create a ready 2-power Sand Soldier token at that battlefield, accept replayable `PAY_COST(DECLINE)` to close the same trigger window without cost, return, or token creation, and replay both branches through score victory to the same final state hash. The follow-up also keeps `MatchSession` snapshot projection redacting object ids that moved into a non-viewer hand, main deck, rune deck, or hidden battlefield standby from battle / battlefield task / resolution metadata ids and object-id collections.

The 2026-06-25 conquer pay-ready-legend follow-up moves another implemented conquered-battlefield trigger away from engine card-number branching:

- `SFD·210/221` / 传奇殿堂 official text: `当你征服此处时，你可以选择支付{{1}}，以此让你的传奇变为活跃状态。`
- `RuleTextParser` now parses that official text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_CONQUERED_PAY_1_READY_LEGEND`
  - `Timing = BATTLEFIELD_CONQUERED`
  - `TargetScope = CONTROLLED_LEGEND`
  - `ManaCost = 1`
  - `LegendReadyCount = 1`
- `CoreRuleEngine.TryOpenBattlefieldConquerPayReadyLegendPaymentWindow` now recognizes eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldConquerPayReadyLegendTrigger(...)`, reads the mana cost and legend-ready count from `BehaviorSpec.Triggers`, and opens `TRIGGER_PAYMENT`; `ResolveBattlefieldConquerReadyLegendTriggerPayment` commits the payment before readying the selected exhausted legend.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldConquerPayOneReadyLegendCardNo` constant. The development seed keeps the official card number only as fixture data.
- The old `BattlefieldConquerPayOneReadyLegendCardNo` / `IsBattlefieldConquerPayOneReadyLegendCardNo` card-number branch and `BattlefieldReadyLegendManaCost` fixed-cost constant are removed. Current source-helper count for `private static bool Is*CardNo(...)` is `63` total / `59` in `CoreRuleEngine`; Core battlefield helper count is `15`.

The 2026-06-27 B0 replay follow-up moves the parsed `SFD·210/221` Hall of Legends route onto `TRIGGER_PAYMENT`. It proves the route can be reached from a verified legal official-deck opening, start from a focused midgame `START_BATTLE` state with an exhausted controlled legend, accept replayable `PAY_COST(SPEND_MANA:1)` to pay the parsed cost and ready that legend, accept replayable `PAY_COST(DECLINE)` to close the same trigger window without cost and leave that legend exhausted, and replay both branches through score victory to the same final state hash.

The 2026-06-25 defend reveal-spell-or-recycle follow-up moves another implemented defended-battlefield trigger away from engine card-number branching:

- `SFD·215/221` / 拉文布鲁姆学院 official text: `当你防守此处时，展示你主牌堆顶部的一张牌。如果是一张法术牌，则将其放入你的手牌，否则将其回收。`
- `RuleTextParser` now parses that two-sentence official text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_DEFENSE_REVEAL_TOP_DRAW_SPELL_OR_RECYCLE`
  - `Timing = BATTLEFIELD_DEFENDED`
  - `RevealCount = 1`
  - `RevealSourceZone = MAIN_DECK`
  - `RevealMatchCardFilter = TAG:CARD_TYPE:SPELL`
  - `RevealMatchDestinationZone = HAND`
  - `RevealMissDestinationZone = MAIN_DECK`
- `CoreRuleEngine.ResolveBattlefieldDefendRevealSpellTrigger` now recognizes eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldDefendRevealTopDrawSpellOrRecycleTrigger(...)` and reads the reveal count, source zone, match filter and branch destinations from `BehaviorSpec.Triggers`.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldDefendRevealSpellCardNo` constant. The development seed keeps the official card number only as fixture data.
- The old `BattlefieldDefendRevealSpellCardNo` / `IsBattlefieldDefendRevealSpellCardNo` card-number branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `62` total / `58` in `CoreRuleEngine`; Core battlefield helper count is `14`.

The 2026-06-26 B0 replay follow-up adds no runtime rule changes. It proves the parsed `SFD·215/221` Ravenbloom route can be reached from verified legal official-deck openings, start from a focused midgame `START_BATTLE` state with official `SFD·087/221` on top of the defending player's controlled main deck, reveal that top card, recognize it as a spell, move it to hand, and replay through score victory to the same final state hash.

The 2026-06-26 non-spell B0 replay follow-up also adds no runtime rule changes. It proves the same parsed `SFD·215/221` route can start from a focused midgame state with a controlled official non-spell unit on top of the defending player's main deck, reveal that top card, recognize it is not a spell, recycle it to the bottom of that main deck, and replay through score victory to the same final state hash.

The 2026-06-25 conquer overkill create-Warhawk follow-up moves another implemented conquered-battlefield trigger away from engine card-number branching:

- `UNL-217/219` / 捕猎场 official text: `当你征服此处时，如果你给敌方单位分配了不低于3点的过量伤害，则打出一名1{{S}}“战鹰”，它拥有{{法盾}}。`
- `RuleTextParser` now parses that official text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_CONQUERED_OVERKILL_CREATE_WARHAWK`
  - `Timing = BATTLEFIELD_CONQUERED`
  - `RequiredOverkillDamage = 3`
  - `CreatedTokenCount = 1`
  - `CreatedTokenName = 战鹰`
  - `CreatedTokenPower = 1`
  - `CreatedTokenDestination = BATTLEFIELD`
  - `CreatedTokenKeywords = [法盾]`
- `CoreRuleEngine.TryResolveBattlefieldConquerOverkillCreateWarhawkTrigger` now recognizes eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldConquerOverkillCreateWarhawkTrigger(...)` and reads the overkill threshold, token family, token power, destination and required keyword from `BehaviorSpec.Triggers`.
- The concrete token still resolves through `P6TokenFactoryCatalog` by parsed token family and power, then validates that the token definition carries the parsed `法盾` keyword tag.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldConquerOverkillCreateWarhawkCardNo` constant. The development seed keeps the official card number only as fixture data.
- The old `BattlefieldConquerOverkillCreateWarhawkCardNo` / `IsBattlefieldConquerOverkillCreateWarhawkCardNo` card-number branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `61` total / `57` in `CoreRuleEngine`; Core battlefield helper count is `13`.

The 2026-06-26 B0 replay follow-up adds no runtime rule changes. It proves the parsed `UNL-217/219` Hunting Grounds route can be reached from a verified legal official-deck opening, start from a focused midgame `START_BATTLE` state, assign at least 3 overkill damage to an enemy unit, create the parsed 1-power `UNL·T02` Warhawk token with `法盾` at that battlefield, and replay through score victory to the same final state hash.

## Non-Closure

This is a narrow B4 cleanup slice. It does not close all battlefield trigger families, same-turn movement policy, complete battlefield lifecycle, remaining conquest triggers, optional trigger choice prompts, B0 full real-deck end-to-end game completion, frontend/browser smoke, full official coverage or READY.

## Validation

- focused behavior-spec/source guard/runtime/GameHub representative: passed `5/5`;
- adjacent BattlefieldMoved / BattlefieldMovePower / MoveUnit / BoardTaskQueue / FullGame / GameHub: passed `326/326`;
- MatchRecovery: passed `1989/1989`;
- backend full conformance: passed `8369/8369`;
- DevUi build: passed after adding `/opt/homebrew/bin` to PATH for local `npm`;
- `git diff --check`: passed.

2026-06-27 moved-unit power B0 score-victory follow-up validation:

- focused B0 Back Alley Bar replay: passed `1/1`;
- FullGameEndToEnd: passed `76/76`;
- adjacent BackAlleyBar / BattlefieldMovedUnitPower / BattlefieldMovePower / MoveUnit / FullGameEndToEnd / MatchRecovery representatives: passed `2152/2152`;
- backend full conformance: passed `8832/8832`;
- DevUi build: not run; this follow-up did not change DevUi source or catalog TypeScript shape.

2026-06-27 held-next-spell Echo B0 score-victory follow-up validation:

- focused B0 Piltover Academy replay: passed `1/1`;
- FullGameEndToEnd: passed `76/76`;
- adjacent PiltoverAcademy / HeldNextSpellEcho / BattlefieldHeld / BattlefieldTriggerSpec / Stack / FullGameEndToEnd / MatchRecovery representatives: passed `2655/2655`;
- backend full conformance: passed `8832/8832`;
- DevUi build: not run; this follow-up did not change DevUi source or catalog TypeScript shape.

2026-06-25 held-next-spell Echo parser/runtime validation:

- focused behavior-spec/source guard/runtime/GameHub representative: passed `5/5`;
- adjacent BattlefieldHeld / BattlefieldTriggerSpec / BattlefieldMovedUnitPower / BattlefieldMovePower / GameHub battlefield representatives: passed `102/102`;
- MatchRecovery: passed `1989/1989`;
- backend full conformance: passed `8373/8373`;
- DevUi build/browser smoke: not repeated; this slice did not touch DevUi.

2026-06-25 held unit-cost increase follow-up validation:

- focused behavior-spec/source guard/runtime/GameHub representative: passed `5/5`;
- adjacent BattlefieldHeld / BattlefieldTriggerSpec / PaymentEngine / PlayCard / GameHub battlefield representatives / BoardTaskQueue / FullGame: passed `1156/1156`;
- MatchRecovery: passed `1989/1989`;
- DevUi build: passed after adding `/opt/homebrew/bin` to PATH for local `npm`;
- backend full conformance: passed `8375/8375`.

2026-06-27 held unit-cost B0 official-deck replay follow-up validation:

- focused behavior-spec parser plus Vaults of Helia action-log replay: passed `2/2`;
- FullGameEndToEnd: passed `73/73`;
- adjacent BattlefieldHeldUnitCostIncrease / VaultsOfHelia / PaymentEngine / PlayCard / BattlefieldHeld / BattlefieldTriggerSpec / CardCatalogBaselineTests / FullGameEndToEnd / MatchRecovery: passed `3445/3445`;
- backend full conformance: passed `8829/8829`;

2026-06-25 friendly-spell draw follow-up validation:

- focused behavior-spec/source guard/runtime representative: passed `5/5`;
- adjacent BattlefieldFriendlySpellDraw / BattlefieldFriendlySpellTarget / BattlefieldTriggerSpec / held-trigger representatives: passed `12/12`;
- MatchRecovery: passed `1989/1989`;
- DevUi build: passed after adding `/opt/homebrew/bin` to PATH for local `npm`;
- backend full conformance: passed `8377/8377`.

2026-06-25 spell-power bonus follow-up validation:

- focused behavior-spec/source guard/runtime/GameHub representative: passed `5/5`;
- adjacent BattlefieldSpellPowerBonus / BattlefieldTriggerSpec / recently migrated battlefield trigger representatives: passed `21/21`;
- MatchRecovery: passed `1989/1989`;
- backend full conformance: passed `8379/8379`;
- DevUi build/browser smoke: not repeated; this slice did not touch DevUi files or frontend behavior.

2026-06-25 high-cost spell insight follow-up validation:

- focused behavior-spec/source guard/runtime/GameHub representative: passed `6/6`;
- adjacent BattlefieldHighCostSpellInsight / BattlefieldTriggerSpec / BattlefieldSpellPowerBonus / BattlefieldFriendlySpellDraw / P6 battlefield surface representatives: passed `15/15`;
- MatchRecovery: passed `1989/1989`;
- backend full conformance: passed `8382/8382`;
- DevUi build/browser smoke: not repeated; this slice did not touch DevUi files or frontend behavior.

2026-06-25 unit-play boon follow-up validation:

- focused behavior-spec/source guard/runtime representative: passed `6/6`;
- adjacent BattlefieldPlayUnitBoon / BattlefieldTriggerSpec / PlayCard / Boon / GameHub / P6 battlefield surface representatives: passed `331/331`;
- MatchRecovery: passed `1989/1989`;
- backend full conformance: passed `8385/8385`;
- DevUi build/browser smoke: not repeated; this slice did not touch DevUi files or frontend behavior.

2026-06-25 unit-returned call-rune follow-up validation:

- focused behavior-spec/source guard/runtime/GameHub representative: passed `6/6`;
- adjacent BattlefieldReturnCallRune / BattlefieldReturnedUnit / BattlefieldTriggerSpec / recent battlefield trigger representatives / call-rune representatives: passed `23/23`;
- MatchRecovery: passed `1989/1989`;
- backend full conformance: passed `8387/8387`;
- DevUi build/browser smoke: not repeated; this slice did not touch DevUi files or frontend behavior.

2026-06-25 first-unit-play move-other-to-base follow-up validation:

- focused behavior-spec/source guard/runtime/GameHub representative: passed `5/5`;
- adjacent BattlefieldFirstUnit / BattlefieldPlayUnitBoon / BattlefieldTriggerSpec / MoveUnit / PlayCard / GameHub representatives: passed `342/342`;
- MatchRecovery: passed `1989/1989`;
- backend full conformance: passed `8389/8389`;
- DevUi build: passed after synchronizing `BehaviorSpec.triggers` catalog typing.

2026-06-25 held draw-one follow-up validation:

- focused behavior-spec/source guard/runtime/GameHub representative: passed `5/5`;
- adjacent BattlefieldHeld / BattlefieldTriggerSpec / Dunehorn / held-trigger GameHub representatives: passed `63/63`;
- MatchRecovery: passed `1989/1989`;
- backend full conformance: passed `8391/8391`;
- DevUi build/browser smoke: not repeated; this slice did not touch DevUi files or frontend behavior.

2026-06-27 unit battlefield-held draw B0 official-deck replay follow-up validation:

- focused Dunehorn Beast action-log replay: passed `1/1`;
- FullGameEndToEnd: passed `74/74`;
- adjacent Dunehorn / UnitBattlefieldHeldDraw / BattlefieldHeldDraw / BattlefieldHeld / BattlefieldTriggerSpec / FullGameEndToEnd / MatchRecovery: passed `2152/2152`;
- backend full conformance: passed `8830/8830`;

2026-06-25 held call-rune follow-up validation:

- focused behavior-spec/source guard/runtime/GameHub representative: passed `4/4`;
- adjacent BattlefieldHeld / BattlefieldTriggerSpec / BattlefieldReturnCallRune representatives: passed `64/64`;
- MatchRecovery: passed `1989/1989`;
- backend full conformance: passed `8393/8393`;
- DevUi build/browser smoke: not repeated; this slice did not touch DevUi files or frontend behavior.

2026-06-25 held each-player call-rune follow-up validation:

- focused behavior-spec/source guard/runtime/GameHub representative: passed `4/4`;
- adjacent BattlefieldHeld / BattlefieldTriggerSpec / CallRune / Runes representatives: passed `103/103`;
- MatchRecovery: passed `1989/1989`;
- backend full conformance: passed `8395/8395`;
- DevUi build/browser smoke: not repeated; this slice did not touch DevUi files or frontend behavior.

2026-06-25 held move-unit-to-base follow-up validation:

- focused behavior-spec/source guard/runtime/GameHub representative: passed `4/4`;
- adjacent BattlefieldHeld / BattlefieldTriggerSpec / MoveUnit / FullGame / GameHub representatives: passed `360/360`;
- MatchRecovery: passed `1989/1989`;
- backend full conformance: passed `8397/8397`;
- DevUi build/browser smoke: not repeated; this slice did not touch DevUi files or frontend behavior.

2026-06-25 held grant-boon follow-up validation:

- focused behavior-spec/source guard/runtime/GameHub representative: passed `4/4`;
- adjacent BattlefieldHeld / BattlefieldTriggerSpec / Boon / FullGame / GameHub representatives: passed `360/360`;
- MatchRecovery: passed `1989/1989`;
- backend full conformance: passed `8399/8399`;
- DevUi build/browser smoke: not repeated; this slice did not touch DevUi files or frontend behavior.

2026-06-25 held create-minion follow-up validation:

- focused behavior-spec/source guard/runtime/GameHub representative: passed `4/4`;
- adjacent BattlefieldHeld / BattlefieldTriggerSpec / Token / FullGame / GameHub representatives: passed `349/349`;
- MatchRecovery: passed `1989/1989`;
- backend full conformance: passed `8401/8401`;
- DevUi build/browser smoke: not repeated; this slice did not touch DevUi files or frontend behavior.

2026-06-25 held return-hero follow-up validation:

- focused behavior-spec/source guard/runtime/GameHub representative: passed `5/5`;
- adjacent BattlefieldHeld / BattlefieldTriggerSpec / ChampionZone / FullGame / GameHub representatives: passed `290/290`;
- MatchRecovery: passed `1989/1989`;
- backend full conformance: passed `8403/8403`;
- DevUi build: passed after synchronizing `BehaviorSpec.triggers` catalog typing.

2026-06-25 held seven-units win follow-up validation:

- focused behavior-spec/source guard/runtime/GameHub representative: passed `7/7`;
- adjacent BattlefieldHeld / BattlefieldTriggerSpec / FullGame / GameHub representatives: passed `293/293`;
- MatchRecovery: passed `1989/1989`;
- backend full conformance: passed `8407/8407`;
- DevUi build: passed after synchronizing `BehaviorSpec.triggers` catalog typing.

2026-06-25 conquer reveal/recycle follow-up validation:

- focused behavior-spec/source guard/runtime/GameHub representative: passed `4/4`;
- adjacent BattlefieldConquer / BattlefieldTriggerSpec / FullGame / GameHub representatives: passed `272/272`;
- MatchRecovery: passed `1989/1989`;
- backend full conformance: passed `8409/8409`;
- DevUi build: passed after synchronizing `BehaviorSpec.triggers` catalog typing.

2026-06-27 conquer reveal/recycle B0 action-log replay follow-up validation:

- focused B0 Candlelit Sanctum replay: passed `1/1`;
- FullGameEndToEnd: passed `75/75`;
- adjacent Candlelit / BattlefieldConquerRevealRecycle / BattlefieldConquer / BattlefieldTriggerSpec / FullGameEndToEnd / GameHubJoin / MatchRecovery representatives: passed `2355/2355`;
- backend full conformance: passed `8831/8831`;
- DevUi build: not run; this follow-up did not change DevUi source or catalog TypeScript shape.

2026-06-25 conquer mill follow-up validation:

- focused behavior-spec/source guard/runtime/GameHub representative: passed `4/4`;
- adjacent BattlefieldConquer / BattlefieldTriggerSpec / FullGame / GameHub representatives: passed `274/274`;
- MatchRecovery: passed `1989/1989`;
- backend full conformance: passed `8411/8411`;
- DevUi build: passed after synchronizing `BehaviorSpec.triggers` catalog typing.

2026-06-27 conquer mill B0 action-log replay follow-up validation:

- focused B0 Minefield replay: passed `1/1`;
- FullGameEndToEnd: passed `76/76`;
- adjacent Minefield / BattlefieldConquerMill / BattlefieldConquer / BattlefieldTriggerSpec / FullGameEndToEnd / GameHubJoin / MatchRecovery representatives: passed `2356/2356`;
- backend full conformance: passed `8832/8832`;
- DevUi build: not run; this follow-up did not change DevUi source or catalog TypeScript shape.

2026-06-27 held move-unit-to-base B0 score-victory owner-fallback follow-up validation:

- focused B0 Rehearsal Hall replay: passed `1/1`;
- FullGameEndToEnd: passed `76/76`;
- adjacent RehearsalHall / BattlefieldHeldMoveUnitToBase / BattlefieldHeldScore / BattlefieldHeld / MoveUnit / FullGameEndToEnd / MatchRecovery representatives: passed `2233/2233`;
- backend full conformance: passed `8832/8832`;
- DevUi build: not run; this follow-up did not change DevUi source or catalog TypeScript shape.

2026-06-25 conquer recycle-rune follow-up validation:

- focused behavior-spec/source guard/runtime/GameHub representative: passed `4/4`;
- adjacent BattlefieldConquer / BattlefieldTriggerSpec / FullGame / GameHub representatives: passed `276/276`;
- MatchRecovery: passed `1989/1989`;
- backend full conformance: passed `8413/8413`;
- DevUi build: passed after synchronizing `BehaviorSpec.triggers` catalog typing.

2026-06-28 conquer recycle-rune B0 action-log replay follow-up validation:

- focused B0 Thunder Sigil replay: passed `1/1`;
- FullGameEndToEnd: passed `83/83`;
- adjacent BattlefieldConquerRecycleRune / FullGameEndToEnd / MatchRecovery representatives: passed `2076/2076`;
- backend full conformance: passed `8847/8847`;
- DevUi build: not run; this follow-up did not change DevUi source or catalog TypeScript shape.

2026-06-25 conquer consume-boon draw follow-up validation:

- focused behavior-spec/source guard/runtime/GameHub representative: passed `5/5`;
- adjacent BattlefieldConquer / BattlefieldTriggerSpec / Boon / FullGame / GameHub representatives: passed `357/357`;
- MatchRecovery: passed `1989/1989`;
- backend full conformance: passed `8415/8415`;
- DevUi build: passed after synchronizing `BehaviorSpec.triggers` catalog typing.

2026-06-25 conquer discard-draw follow-up validation:

- focused behavior-spec/source guard/runtime/GameHub representative: passed `5/5`;
- adjacent BattlefieldConquer / BattlefieldTriggerSpec / Discard / Jinx / FullGame / GameHub representatives: passed `330/330`;
- MatchRecovery: passed `1989/1989`;
- backend full conformance: passed `8417/8417`;
- DevUi build: passed after synchronizing `BehaviorSpec.triggers` catalog typing.

2026-06-28 conquer discard-draw B0 action-log replay follow-up validation:

- focused B0 Zaun Sump replay: passed `1/1`;
- FullGameEndToEnd: passed `84/84`;
- adjacent BattlefieldConquerDiscardDraw / FullGameEndToEnd / MatchRecovery representatives: passed `2077/2077`;
- backend full conformance: passed `8848/8848`;
- DevUi build: not run; this follow-up did not change DevUi source or catalog TypeScript shape.

2026-06-25 conquer draw-for-other-battlefields follow-up validation:

- focused behavior-spec/source guard/runtime/GameHub representative: passed `5/5`;
- adjacent BattlefieldConquer / BattlefieldTriggerSpec / FullGame / GameHub representatives: passed `282/282`;
- MatchRecovery: passed `1989/1989`;
- backend full conformance: passed `8419/8419`;
- DevUi build: passed after synchronizing `BehaviorSpec.triggers` catalog typing.

2026-06-28 conquer draw-for-other-battlefields B0 action-log replay follow-up validation:

- focused B0 Seat of Power replay: passed `1/1`;
- FullGameEndToEnd: passed `85/85`;
- adjacent BattlefieldConquerDrawForOtherBattlefields / FullGameEndToEnd / MatchRecovery representatives: passed `2076/2076`;
- backend full conformance: passed `8849/8849`;
- DevUi build: not run; this follow-up did not change DevUi source or catalog TypeScript shape.

2026-06-28 conquer ready-runes-at-end B0 action-log replay follow-up validation:

- focused B0 Mount Targon replay: passed `1/1`;
- FullGameEndToEnd: passed `86/86`;
- adjacent BattlefieldConquerReadyRunesAtEnd / FullGameEndToEnd / MatchRecovery representatives: passed `2079/2079`;
- backend full conformance: passed `8850/8850`;
- DevUi build: not run; this follow-up did not change DevUi source or catalog TypeScript shape.

2026-06-25 conquer ready-runes-at-end follow-up validation:

- focused behavior-spec/source guard/runtime/GameHub representative: passed `5/5`;
- adjacent BattlefieldConquer / BattlefieldTriggerSpec / Rune / FullGame / GameHub representatives: passed `458/458`;
- MatchRecovery: passed `1989/1989`;
- DevUi build: passed after synchronizing `BehaviorSpec.triggers` catalog typing;
- backend full conformance: passed `8421/8421`.

2026-06-25 conquer ready-equipment follow-up validation:

- focused behavior-spec/source guard/runtime/GameHub representative: passed `4/4`;
- adjacent BattlefieldConquer / BattlefieldTriggerSpec / Equipment / FullGame / GameHub representatives: passed `750/750`;
- MatchRecovery: passed `1989/1989`;
- DevUi build: passed after synchronizing `BehaviorSpec.triggers` catalog typing;
- backend full conformance: passed `8423/8423`.

2026-06-25 conquer pay-create-gold follow-up validation:

- focused behavior-spec/source guard/runtime/GameHub representative: passed `4/4`;
- adjacent BattlefieldConquer / TriggerPayment representatives: passed `130/130`;
- MatchRecovery: passed `1989/1989`;
- DevUi build: passed after synchronizing `BehaviorSpec.triggers` catalog typing;
- backend full conformance: passed `8425/8425`.

2026-06-26 Treasure Pile B0 action-log replay follow-up validation:

- focused B0 Treasure Pile replay: passed `1/1`;
- FullGameEndToEnd: passed `42/42`;
- adjacent TreasurePile / BattlefieldConquerGold / TriggerPayment / FullGameEndToEnd / MatchRecovery representatives: passed `2124/2124`;
- backend full conformance: passed `8736/8736`;
- DevUi build: not run; this follow-up did not change DevUi source or catalog TypeScript shape.

2026-06-25 conquer powerful pay-draw follow-up validation:

- focused behavior-spec/source guard/runtime representatives: passed `4/4`;
- GameHub seed representative: passed `1/1`;
- adjacent BattlefieldConquer / TriggerPayment / BattlefieldTriggerSpec representatives: passed `91/91`;
- MatchRecovery: passed `1989/1989`;
- DevUi build: passed after synchronizing `BehaviorSpec.triggers` catalog typing;
- backend full conformance: passed `8428/8428`.

2026-06-26 Sunken Temple B0 action-log replay follow-up validation:

- focused B0 Sunken Temple replay: passed `1/1`;
- FullGameEndToEnd: passed `43/43`;
- adjacent SunkenTemple / PowerfulDraw / BattlefieldConquerPowerful / TriggerPayment / FullGameEndToEnd / MatchRecovery representatives: passed `2129/2129`;
- backend full conformance: passed `8737/8737`;
- DevUi build: not run; this follow-up did not change DevUi source or catalog TypeScript shape.

2026-06-25 conquer pay-return-unit create-Sand-Soldier follow-up validation:

- focused behavior-spec/source guard/runtime/GameHub representative: passed `5/5`;
- adjacent BattlefieldConquer / TriggerPayment / BattlefieldTriggerSpec / SandSoldier representatives: passed `141/141`;
- MatchRecovery: passed `1989/1989`;
- backend full conformance: passed `8430/8430`.

2026-06-27 Imperial Shrine / Hall of Legends B0 trigger-payment replay follow-up validation:

- focused B0 Imperial Shrine / Hall of Legends pay + decline replay: passed `4/4`;
- FullGameEndToEnd: passed `72/72`;
- adjacent ImperialShrine / SandSoldier / PayReturnUnit / ReturnUnitCreate / HallOfLegends / ReadyLegend / LegendReadied / TriggerPayment / BattlefieldConquer / FullGameEndToEnd / MatchRecovery representatives: passed `2222/2222`;
- backend full conformance: passed `8828/8828`;
- DevUi build: not run; this follow-up did not change DevUi source or catalog TypeScript shape.

2026-06-25 conquer pay-ready-legend follow-up validation:

- focused behavior-spec/source guard/runtime/GameHub representative: passed `5/5`;
- adjacent BattlefieldConquer / BattlefieldTriggerSpec / ReadyLegend / LegendReadied representatives: passed `81/81`;
- MatchRecovery: passed `1989/1989`;
- DevUi build: passed after synchronizing `BehaviorSpec.triggers` catalog typing;
- backend full conformance: passed `8432/8432`.

2026-06-25 defend reveal-spell-or-recycle follow-up validation:

- focused behavior-spec/source guard/runtime/GameHub representative: passed `7/7`;
- adjacent BattlefieldDefend / BattlefieldDefender / BattlefieldTriggerSpec / RevealCard / DeclareBattle representatives: passed `188/188`;
- MatchRecovery: passed `1989/1989`;
- DevUi build: passed after synchronizing `BehaviorSpec.triggers` catalog typing;
- backend full conformance: passed `8434/8434`.

2026-06-26 Ravenbloom B0 action-log replay follow-up validation:

- focused B0 Ravenbloom replay: passed `1/1`;
- FullGameEndToEnd: passed `46/46`;
- adjacent Ravenbloom / DefendReveal / RevealSpell / BattlefieldDefend / BattlefieldTriggerSpec / FullGameEndToEnd / MatchRecovery representatives: passed `2056/2056`;
- backend full conformance: passed `8740/8740`;
- DevUi build: not run; this follow-up did not change DevUi source or catalog TypeScript shape.

2026-06-26 Ravenbloom non-spell B0 action-log replay follow-up validation:

- focused B0 Ravenbloom non-spell replay: passed `1/1`;
- FullGameEndToEnd: passed `47/47`;
- adjacent Ravenbloom / DefendReveal / RevealSpell / Recycle / BattlefieldDefend / BattlefieldTriggerSpec / FullGameEndToEnd / MatchRecovery representatives: passed `2181/2181`;
- backend full conformance: passed `8741/8741`;
- DevUi build: not run; this follow-up did not change DevUi source or catalog TypeScript shape.

2026-06-26 Hunting Grounds overkill create-Warhawk B0 action-log replay follow-up validation:

- focused B0 Hunting Grounds replay: passed `1/1`;
- FullGameEndToEnd: passed `48/48`;
- adjacent Hunting / Overkill / Warhawk / BattlefieldConquer / BattlefieldTriggerSpec / FullGameEndToEnd / MatchRecovery representatives: passed `2142/2142`;
- backend full conformance: passed `8742/8742`;
- DevUi build: not run; this follow-up did not change DevUi source or catalog TypeScript shape.

2026-06-26 Dream Tree friendly-spell draw locality / B0 action-log replay follow-up validation:

- focused Dream Tree / Waste Hall same-battlefield locality representatives: passed `8/8`;
- focused B0 Dream Tree replay: passed `1/1`;
- FullGameEndToEnd: passed `49/49`;
- adjacent BattlefieldFriendlySpellDraw / BattlefieldSpellPowerBonus / BattlefieldTriggerSpec / FullGameEndToEnd / MatchRecovery representatives: passed `2051/2051`;
- backend full conformance: passed `8745/8745`;
- DevUi build: not run; this follow-up did not change DevUi source or catalog TypeScript shape.

2026-06-26 Waste Hall spell-power bonus B0 action-log replay follow-up validation:

- focused B0 Waste Hall replay: passed `1/1`;
- FullGameEndToEnd: passed `50/50`;
- adjacent BattlefieldSpellPowerBonus / BattlefieldTriggerSpec / FullGameEndToEnd / MatchRecovery representatives: passed `2045/2045`;
- backend full conformance: passed `8746/8746`;
- DevUi build: not run; this follow-up did not change DevUi source or catalog TypeScript shape.

2026-06-27 Lost Library high-cost spell insight B0 action-log replay follow-up validation:

- focused B0 Lost Library replay: passed `1/1`;
- FullGameEndToEnd: passed `51/51`;
- adjacent BattlefieldHighCostSpellInsight / BattlefieldTriggerSpec / FullGameEndToEnd / MatchRecovery representatives: passed `2046/2046`;
- backend full conformance: passed `8747/8747`;
- DevUi build: not run; this follow-up did not change DevUi source or catalog TypeScript shape.

2026-06-27 Plunder Alley defend move-friendly-unit-to-base B0 action-log replay follow-up validation:

- focused B0 Plunder Alley replay: passed `1/1`;
- FullGameEndToEnd: passed `68/68`;
- adjacent Plunder / BattlefieldDefend / BattlefieldTriggerSpec / DeclareBattle / GameHub / FullGameEndToEnd / MatchRecovery representatives: passed `2396/2396`;
- backend full conformance: passed `8765/8765`;
- DevUi build: not run; this follow-up did not change DevUi source or catalog TypeScript shape.

2026-06-25 conquer overkill create-Warhawk follow-up validation:

- focused behavior-spec/source guard/runtime/GameHub representative: passed `3/3`;
- adjacent BattlefieldConquer / BattlefieldTriggerSpec / Overkill / Warhawk / DeclareBattle representatives: passed `221/221`;
- MatchRecovery: passed `1989/1989`;
- DevUi build: passed after synchronizing `BehaviorSpec.triggers` catalog typing;
- backend full conformance: passed `8436/8436`.

2026-06-25 turn-start damage-units follow-up validation:

- focused behavior-spec/source guard/runtime/GameHub representative: passed `5/5`;
- CardCatalog baseline: passed `147/147`;
- adjacent BattlefieldTurnStartDamage / BattlefieldTriggerSpec / GameHub representatives: passed `226/226`;
- FullGame representatives: passed `7/7`;
- MatchRecovery: passed `1989/1989`;
- backend full conformance: passed `8438/8438`.

2026-06-27 Frost Hold turn-start damage B0 score-victory follow-up validation:

- focused `OfficialDeckMidgameResolvesFrostHoldTurnStartDamageAndScoreVictoryActionLogReplaysToFinalStateHash`: passed `1/1`;
- FullGameEndToEnd: passed `77/77`;
- adjacent FrostHold / BattlefieldTurnStartDamage / BattlefieldTriggerSpec / FullGameEndToEnd / MatchRecovery: passed `2071/2071`;
- backend full conformance: passed `8833/8833`.

2026-06-27 Duskpetal Lab turn-start destroy-draw B0 score-victory follow-up validation:

- focused `OfficialDeckMidgameResolvesDuskpetalLabTurnStartDestroyDrawAndScoreVictoryActionLogReplaysToFinalStateHash`: passed `1/1`;
- FullGameEndToEnd: passed `78/78`;
- adjacent Duskpetal / BattlefieldTurnStartDestroy / BattlefieldTurnStart / BattlefieldTriggerSpec / FullGameEndToEnd / MatchRecovery: passed `2077/2077`;
- backend full conformance: passed `8834/8834`.

2026-06-27 Power Obelisk first-turn extra-rune B0 score-victory follow-up validation:

- focused `OfficialDecksResolvePowerObeliskFirstTurnExtraRuneAndScoreVictoryActionLogReplaysToFinalStateHash`: passed `1/1`;
- FullGameEndToEnd: passed `79/79`;
- adjacent PowerObelisk / FirstTurnRune / BattlefieldFirstTurn / BattlefieldTriggerSpec / FullGameEndToEnd / MatchRecovery: passed `2076/2076`;
- backend full conformance: passed `8835/8835`.

2026-06-27 Glory Arena first-turn score B0 score-victory follow-up validation:

- focused `OfficialDecksResolveGloryArenaFirstTurnScoreAndScoreVictoryActionLogReplaysToFinalStateHash`: passed `1/1`;
- FullGameEndToEnd: passed `80/80`;
- adjacent GloryArena / FirstTurnScore / ScoreDelay / BattlefieldFirstTurn / BattlefieldTriggerSpec / FullGameEndToEnd / MatchRecovery: passed `2083/2083`;
- backend full conformance: passed `8836/8836`.

2026-06-27 Forgotten Monument score-delay B0 score-victory follow-up validation:

- focused `OfficialDecksResolveForgottenMonumentScoreDelayAndScoreVictoryActionLogReplaysToFinalStateHash`: passed `1/1`;
- FullGameEndToEnd: passed `81/81`;
- adjacent ForgottenMonument / ScoreDelay / ScorePrevented / FirstTurnScore / BattlefieldHeldScore / BattleResponse / BattlefieldTriggerSpec / FullGameEndToEnd / MatchRecovery: passed `2140/2140`;
- backend full conformance: passed `8837/8837`.

2026-06-27 winning-score increase B0 score-victory follow-up validation:

- focused `OfficialDecksResolveWinningScoreIncreaseAndScoreVictoryActionLogReplaysToFinalStateHash`: passed `1/1`;
- FullGameEndToEnd: passed `82/82`;
- adjacent WinningScore / BattlefieldStaticWinningScore / MindAndBalance / FullGameEndToEnd / MatchRecovery: passed `2082/2082`;
- backend full conformance: passed `8838/8838`.

2026-06-25 turn-start destroy-draw follow-up validation:

- focused behavior-spec/source guard/runtime/GameHub representative: passed `5/5`;
- CardCatalog baseline: passed `149/149`;
- adjacent BattlefieldTurnStart / BattlefieldTriggerSpec / GameHub representatives: passed `230/230`;
- MatchRecovery: passed `1989/1989`;
- backend full conformance: passed `8440/8440`;
- DevUi build: passed after synchronizing `BehaviorSpec.triggers` catalog typing.

2026-06-25 first-turn extra-rune follow-up validation:

- focused behavior-spec/source guard/runtime/GameHub representative: passed `5/5`;
- CardCatalog baseline: passed `151/151`;
- adjacent FirstTurnRune / BattlefieldFirstTurn / BattlefieldTriggerSpec / GameHub representatives: passed `226/226`;
- MatchRecovery: passed `1989/1989`;
- backend full conformance: passed `8442/8442`;
- DevUi build: passed after synchronizing `BehaviorSpec.triggers` catalog typing.

2026-06-25 held activate-unit-conquest-effects follow-up validation:

- focused behavior-spec/source guard/runtime representatives: passed `8/8`;
- CardCatalog baseline: passed `170/170`;
- adjacent BattlefieldHeldActivateConquest / DeclareBattle / BattleDamageAssignment / FullGameEndToEnd / GameHub representatives: passed `230/230`;
- MatchRecovery: passed `1989/1989`;
- backend full conformance: passed `8461/8461`;
- DevUi build: not run; this follow-up did not change DevUi source or catalog TypeScript shape.

2026-06-28 Moonveil Altar ready-equipment B0 score-victory follow-up validation:

- focused `OfficialDeckMidgameResolvesMoonveilAltarConquerReadyEquipmentAndScoreVictoryActionLogReplaysToFinalStateHash`: passed `1/1`;
- FullGameEndToEnd: passed `87/87`;
- adjacent ReadyEquipment / BattlefieldConquer / BattlefieldTriggerSpec / FullGameEndToEnd / MatchRecovery: passed `2157/2157`;
- backend full conformance: passed `8851/8851`;
- DevUi build: not run; this follow-up did not change DevUi source or catalog TypeScript shape.

2026-06-28 Shirana Monastery consume-boon draw B0 score-victory follow-up validation:

- focused `OfficialDeckMidgameResolvesShiranaMonasteryConquerConsumeBoonDrawAndScoreVictoryActionLogReplaysToFinalStateHash`: passed `1/1`;
- FullGameEndToEnd: passed `88/88`;
- adjacent ConsumeBoon / Boon / BattlefieldConquer / BattlefieldTriggerSpec / FullGameEndToEnd / MatchRecovery: passed `2255/2255`;
- backend full conformance: passed `8852/8852`;
- DevUi build: not run; this follow-up did not change DevUi source or catalog TypeScript shape.
