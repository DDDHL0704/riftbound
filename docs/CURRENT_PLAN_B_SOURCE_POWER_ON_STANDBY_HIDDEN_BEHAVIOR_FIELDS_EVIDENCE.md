# Plan B Source Power On Standby Hidden Behavior Fields Evidence

Date: 2026-06-30

Project status: **NOT READY**.

## Evidence Basis

Official card data:

- `data/official/card-catalog.zh-CN.json` row `OGN·167/298` 余火修士 states that when the controller plays a face-down standby card, the source gets +2 power this turn.

Existing engine evidence:

- `P79EmberMonkGainsPowerWhenFriendlyStandbyCardIsHidden` proves the representative standby-hidden power trigger path.
- The same test proves face-down, standby, and opposing Ember Monk objects do not receive the modifier.
- `docs/rules-evidence-index.md` already records the `p2-preflight-play-ember-monk-standby-trigger-static` and target-rejection evidence rows.

## Engine Evidence

Before this slice, `CoreRuleEngine.ResolveEmberMonkStandbyHiddenPowerTrigger` selected sources through `EmberMonkStandbyTriggerSourceEffectKind = EMBER_MONK_STANDBY_TRIGGER_PLAY_UNIT` and used Core constants for the emitted effect kind and +2 power modifier.

After this slice:

- `CoreRuleEngine` no longer contains `EmberMonkStandbyTriggerSourceEffectKind`.
- `CoreRuleEngine` no longer contains `EMBER_MONK_STANDBY_TRIGGER_PLAY_UNIT` as a runtime selector.
- `CoreRuleEngine` no longer contains `EMBER_MONK_FACE_DOWN_STANDBY_POWER_2`; that value lives on the official `OGN·167/298` behavior row.
- `CardBehaviorRegistry` stores the official source-power amount and emitted effect kind on the `OGN·167/298` behavior row.
- `CoreRuleEngine.ResolveSourcePowerOnControllerStandbyHiddenTriggers` scans public controlled field source units and applies any matching `SourcePowerOnControllerStandbyHiddenAmount` behavior fields.

## Test Evidence

- `TriggerSourceIdentityGuardTests.CoreRuleEngineTriggerSourceSelectionUsesBehaviorFieldsWhereAvailable` blocks reintroducing the Ember Monk runtime effect-kind selector and emitted effect-kind constant in Core.
- `CardCatalogBaselineTests.EmberMonkStandbyHiddenPowerCarriesOfficialBehaviorFields` locks the official row to `SourcePowerOnControllerStandbyHiddenAmount=2` and effect kind `EMBER_MONK_FACE_DOWN_STANDBY_POWER_2`.
- Existing Ember Monk focused regression passed unchanged, proving trigger behavior is preserved.
- Baseline before this slice: backend full conformance passed `9032/9032`.
- Focused behavior-field gate passed `3/3`.
- Adjacent / hidden-info gate passed `3026/3026`.
- Backend full conformance passed `9033/9033`.

## Non-Claims

This evidence does not claim complete standby-hidden trigger timing, complete TriggerSpec migration, complete `ORDER_TRIGGERS` / APNAP ordering, P0 completion, P1, or READY.
