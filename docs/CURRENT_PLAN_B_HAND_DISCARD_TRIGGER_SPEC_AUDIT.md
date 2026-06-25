# Plan B / Hand Discard Trigger Spec Audit

Date: 2026-06-25

Status: focused hand-discard trigger-spec slice accepted; project remains **NOT READY**.

## Scope

This slice moves the implemented Jinx hand-discard ready/power representative from a Core card-number branch to `BehaviorSpec.Triggers`:

- Official catalog source: `data/official/card-catalog.zh-CN.json` has `OGN·202/298`, `OGN·202a/298`, and `ARC-005/006` 金克丝 text `每当你弃置任意数量的手牌时，让我变为活跃状态，且本回合内{{S}}+1。`
- `TriggerKinds.HandCardsDiscardedReadySourcePower` preserves the existing emitted trigger value `JINX_DISCARDED_HAND_CARDS_READY_POWER_1` for event/replay compatibility.
- `TriggerTimings.HandCardsDiscarded` models the hand-discard trigger timing.
- `TriggerSpec.ReadiesSource` models the source-ready effect that pairs with the existing `PowerDelta` and `Duration` fields.
- `RuleTextParsers.TriggerParser` now parses the official text into `TriggerSpec.Kind = JINX_DISCARDED_HAND_CARDS_READY_POWER_1`, `Timing = HAND_CARDS_DISCARDED`, `TargetScope = SOURCE_UNIT`, `ReadiesSource = true`, `PowerDelta = 1`, and `Duration = UNTIL_END_OF_TURN`.
- `CoreRuleEngine.ResolveHandCardsDiscardedReadyPowerTriggers(...)` now checks `HandDiscardTriggerSpecRules.TryGetHandCardsDiscardedReadySourcePowerTrigger(...)` and reads the emitted effect id plus ready/power shape from `TriggerSpec`.
- The old Core `OgnJinxDiscardTriggerCardNo`, `OgnJinxDiscardTriggerAltCardNo`, `ArcJinxDiscardTriggerCardNo`, `JinxDiscardedHandCardsEffectKind`, `JinxDiscardedHandCardsBehavior`, and `IsJinxDiscardTriggerCardNo(...)` branch is removed.

## Runtime Effect

- Successful hand-discard batches still emit one `TRIGGER_RESOLVED` per eligible visible source with `effectKind = JINX_DISCARDED_HAND_CARDS_READY_POWER_1`, then emit `UNIT_READIED` and `POWER_MODIFIED_UNTIL_END_OF_TURN`.
- The ready flag, power delta, duration, and emitted effect id now come from the parsed `TriggerSpec`.
- Existing guards remain: no trigger for empty discard batches, hidden / face-down / standby source, opponent-controlled source, non-unit source, or sources not on the field.
- DevUi runtime behavior is unchanged; catalog typing now includes `triggers[].readiesSource`.

## Non-Goals

- This does not implement the complete discard-trigger family, discard replacement timing, deck-out draw replacement, or simultaneous trigger ordering.
- This does not convert this immediate representative trigger to full `ORDER_TRIGGERS` / stack timing.
- This does not complete hidden face-down original trigger policy, full Jinx official breadth, or all discard/draw card effects.
- This does not close B0 full-game readiness or project READY.

## Validation

- Focused behavior-spec / source guard / Jinx representatives: `9/9` passing.
- Adjacent discard / Jinx / Rampaging Soul / Leblanc / BattlefieldConquerDiscard / RewindTimeline / UndercoverAgent / full-game representatives: `92/92` passing.
- `MatchRecovery`: `1989/1989` passing.
- Backend full conformance: `8531/8531` passing.
- DevUi build: passing.
- Source-helper count for `private static bool Is*CardNo(...)`: `33` total / `30` in `CoreRuleEngine`.
