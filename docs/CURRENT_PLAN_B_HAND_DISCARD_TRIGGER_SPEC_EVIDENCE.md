# Plan B / Hand Discard Trigger Spec Evidence

Date: 2026-06-25

Project status: **NOT READY**.

## Rule Sources

- `data/official/card-catalog.zh-CN.json`: `OGN·202/298`, `OGN·202a/298`, and `ARC-005/006` 金克丝 official text is `每当你弃置任意数量的手牌时，让我变为活跃状态，且本回合内{{S}}+1。`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`: existing P1-002/P1-004 Jinx discard-trigger representative behavior evidence.
- `docs/rules-authority-and-audit.md` and `docs/rules-evidence-index.md`: official catalog and local evidence-index entries remain the authority inputs for this representative slice.

## BehaviorSpec Evidence

- `BehaviorSpecCatalogParsesHandDiscardReadyPowerTrigger(OGN·202/298)` verifies the base print parses to `TriggerSpec.Kind = JINX_DISCARDED_HAND_CARDS_READY_POWER_1`, `Timing = HAND_CARDS_DISCARDED`, `TargetScope = SOURCE_UNIT`, `ReadiesSource = true`, `PowerDelta = 1`, and `Duration = UNTIL_END_OF_TURN`.
- `BehaviorSpecCatalogParsesHandDiscardReadyPowerTrigger(OGN·202a/298)` verifies the same shape for the alternate print.
- `BehaviorSpecCatalogParsesHandDiscardReadyPowerTrigger(ARC-005/006)` verifies the same shape for the Arcane promo print.
- `HandDiscardReadyPowerTriggerDoesNotUseCardNumberAllowList` verifies `CoreRuleEngine` no longer contains the Jinx discard-trigger card-number constants, old effect-kind constant, old static behavior object, source helper, the three card numbers, or the old literal trigger value as a Core branch.
- `P79JinxDiscardTriggerReadiesAndGainsPowerOnceForDiscardBatch` keeps the existing visible-source behavior and hidden / face-down / standby / opponent-controlled guard matrix green.
- `CoreRuleEnginePlaysJinxDiscardTwoHand` keeps the existing play-card discard batch route green.

## Runtime Evidence

- `HandDiscardTriggerSpecRules` builds its trigger map from `BehaviorSpecCatalogBuilder`, matching the existing unit-moved, unit-conquest, and unit-destroyed trigger-spec rule pattern.
- `CoreRuleEngine.ResolveHandCardsDiscardedReadyPowerTriggers(...)` now reads the effect id, ready flag, power delta, and duration from `TriggerSpec`.
- The emitted trigger payload remains `JINX_DISCARDED_HAND_CARDS_READY_POWER_1` for compatibility, but Core no longer selects the behavior by a Jinx card-number constant.
- `src/Riftbound.DevUi/src/types/catalog.ts` now includes `triggers[].readiesSource` so catalog consumers can read the new spec field.

## Validation

- Focused behavior-spec / source guard / Jinx representatives: `9/9` passing.
- Adjacent discard / Jinx / Rampaging Soul / Leblanc / BattlefieldConquerDiscard / RewindTimeline / UndercoverAgent / full-game representatives: `92/92` passing.
- `MatchRecovery`: `1989/1989` passing.
- Backend full conformance: `8531/8531` passing.
- DevUi build: passing.

## Residual Risk

- This slice proves one hand-discard ready/power representative has moved to BehaviorSpec-driven routing. It does not prove the complete discard-trigger family, complete simultaneous-trigger timing, optional trigger prompt breadth, discard/draw replacement breadth, full hidden-info policy for face-down original triggers, frontend smoke, or READY.
