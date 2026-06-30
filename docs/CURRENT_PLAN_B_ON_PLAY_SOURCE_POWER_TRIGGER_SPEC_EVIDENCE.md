# Plan B On-Play Source-Power Trigger Spec Evidence

Date: 2026-06-29
Last updated: 2026-06-30

Project status: **NOT READY**.

## 2026-06-30 Recovery Follow-Up

`MatchRecovery` now validates Teemo on-play self-power trigger source cards through `CardBehaviorRegistry.IsOnPlaySourcePowerTriggerSource(cardNo, effectKind)` instead of hardcoded Teemo source-card constants. The predicate checks that the card behavior row has the requested effect kind, plays the source to base as a unit, applies a nonzero power modifier to that source unit, and exposes that modifier through behavior metadata.

The recovery diagnostic for wrong source cards now says the source card must reference `BehaviorSpec on-play source-unit power modifier trigger`. This aligns recovered snapshot, authoritative-state and spectator replay validation with the runtime route while keeping existing public effect ids and trigger queue payload shape stable.

## Evidence Basis

Official card data:

- `data/official/card-catalog.zh-CN.json` rows `OGN·197/298`, `OGN·197a/298`, `OGN·197b/298`, and `FND-196/298` are Teemo source-unit representatives with the same on-play self-power text.
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` maps those official rows to executable behavior metadata with `PlaysSourceToBaseAsUnit=true`, `SourceUnitPower=1`, `SourceUnitTags=待命|约德尔人`, `AppliesPowerModifierToSourceUnit=true`, and `PowerModifierAmount=3`.
- Existing Teemo fixture and recovery coverage already proves the runtime effect ids and trigger queue shape are stable.

## Engine Evidence

Before this slice, `CoreRuleEngine.IsQueuedOnPlaySourcePowerTrigger` recognized the trigger queue route by checking whether `behavior.EffectKind` contained both `TEEMO` and `PLAY_UNIT_SELF_POWER_PLUS_3`.

After this slice, the shared predicate no longer reads the effect name. It requires the source card to be played as a unit to base, to apply a nonzero power modifier to that source unit, and to expose that modifier through executable behavior metadata.

This keeps the Teemo runtime shape stable while allowing another official card with the same behavior pattern to enter the route by data fields instead of engine source edits.

## Test Evidence

- `OnPlaySourcePowerTriggerSourceGuardTests.QueuedOnPlaySourcePowerTriggerUsesBehaviorFieldsInsteadOfEffectKindName` failed red against the previous implementation because the predicate did not contain `behavior.PlaysSourceToBaseAsUnit`.
- The same focused guard passed after the runtime change.
- The adjacent `OnPlaySourcePowerTriggerSourceGuardTests|Teemo|TriggerQueue|MatchRecovery` filter passed `2097/2097`, covering Teemo fixture and recovery expectations that depend on stable trigger ids and effect kinds.
- Backend full conformance passed `9024/9024`.
- 2026-06-30 recovery follow-up red guard failed on the missing recovery BehaviorSpec predicate and remaining Teemo card-number constants.
- 2026-06-30 focused `OnPlaySourcePowerTriggerSourceGuardTests|TeemoOnPlaySelfPowerSourceCardContextDrift` passed `6/6`.
- 2026-06-30 adjacent `OnPlaySourcePowerTriggerSourceGuardTests|Teemo|TriggerQueue|MatchRecovery` passed `2098/2098`.
- 2026-06-30 backend full conformance passed `9050/9050`.

## Non-Claims

This evidence does not claim full on-play trigger family coverage, full BehaviorSpec parser/executor parity, complete cleanup/replacement duration coverage, complete standby/reveal lifecycle, PaymentEngine completeness, full official matrix closure, or READY.
