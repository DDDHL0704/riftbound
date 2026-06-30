# Plan B Source Power On Standby Hidden Behavior Fields Audit

Date: 2026-06-30

Project status: **NOT READY**.

## Scope

This slice removes the Ember Monk-specific runtime effect-kind selector and hard-coded +2 modifier from the shared standby-hidden trigger path.

The stable catalog effect id `EMBER_MONK_STANDBY_TRIGGER_PLAY_UNIT` remains in `CardBehaviorRegistry`, fixtures, and matrix evidence as row identity data. `CoreRuleEngine` no longer references that id to decide whether a field unit gains power when its controller hides a standby card.

## Authority

- `data/official/card-catalog.zh-CN.json` row `OGN·167/298` 余火修士: when the controller plays a face-down standby card, the source gets +2 power this turn.
- Existing evidence index entry `p2-preflight-play-ember-monk-standby-trigger-static` records the official card row, `CORE-260330` unit/play/standby authorities, representative `HIDE_CARD`, `TRIGGER_RESOLVED`, and `POWER_MODIFIED_UNTIL_END_OF_TURN`.
- Existing `P79EmberMonkGainsPowerWhenFriendlyStandbyCardIsHidden` covers the authoritative +2 event and rejects face-down, standby, and opposing sources.

## Implementation

- `CardBehaviorDefinition` now carries standby-hidden source-power trigger metadata:
  - `SourcePowerOnControllerStandbyHiddenAmount`
  - `SourcePowerOnControllerStandbyHiddenEffectKind`
- `OGN·167/298` fills those fields with `2` and `EMBER_MONK_FACE_DOWN_STANDBY_POWER_2`.
- `CoreRuleEngine.ResolveSourcePowerOnControllerStandbyHiddenTriggers` now scans public, controlled field source units, resolves each source through `CardBehaviorRegistry.TryGetByCardNo`, and applies matching behavior fields.
- Existing `TRIGGER_RESOLVED.effectKind` and `POWER_MODIFIED_UNTIL_END_OF_TURN.reason` payload values remain `EMBER_MONK_FACE_DOWN_STANDBY_POWER_2`.

## Validation

- Baseline before this slice: backend full conformance passed `9032/9032`.
- Red focused source guard failed before implementation because `SourcePowerOnControllerStandbyHiddenAmount` and `SourcePowerOnControllerStandbyHiddenEffectKind` did not exist.
- Green focused gate: `CoreRuleEngineTriggerSourceSelectionUsesBehaviorFieldsWhereAvailable|EmberMonkStandbyHiddenPowerCarriesOfficialBehaviorFields|P79EmberMonkGainsPowerWhenFriendlyStandbyCardIsHidden` passed `3/3`.
- Adjacent / hidden-info gate: `EmberMonk|TriggerSourceIdentityGuardTests|CardCatalogBaselineTests|PaymentEngineCoverageAuditTests|MatchRecovery` passed `3026/3026`.
- Backend full conformance passed `9033/9033`.

## Holdbacks

This does not close complete standby-hidden trigger timing, complete TriggerSpec migration, complete `ORDER_TRIGGERS` / APNAP ordering, P0 full objective, P1, or READY.
