# Plan B Source Stun Ready Power Behavior Fields Audit

Date: 2026-06-30

Project status: **NOT READY**.

## Scope

This slice removes the Eclipse Vanguard-specific runtime effect-kind selector and hard-coded +1 power value from the shared stun-trigger path.

The stable catalog effect id `ECLIPSE_VANGUARD_STUN_TRIGGER_PLAY_UNIT` remains in `CardBehaviorRegistry`, fixtures, and matrix evidence as row identity data. `CoreRuleEngine` no longer references that id to decide whether a field unit readies and gains power when its controller stuns an enemy unit.

## Authority

- `data/official/card-catalog.zh-CN.json` row `OGN·059/298` 星蚀先锋: when the controller stuns an enemy unit, ready the source and it gets +1 power this turn.
- Existing evidence index entry `p2-preflight-play-eclipse-vanguard-stun-trigger-static` records the official card row, `CORE-260330` unit/play/timing authorities, representative stun trigger, `TRIGGER_RESOLVED`, `UNIT_READIED`, and `POWER_MODIFIED_UNTIL_END_OF_TURN`.
- Existing `P79EclipseVanguard*` tests cover the authoritative ready/+1 path, friendly-unit rejection, and standby-source rejection.

## Implementation

- `CardBehaviorDefinition` now carries stun-trigger ready/power metadata:
  - `SourceReadiesWhenControllerStunsEnemyUnit`
  - `SourcePowerOnControllerStunsEnemyUnitAmount`
  - `SourceStunEnemyUnitTriggerEffectKind`
- `OGN·059/298` fills those fields with `true`, `1`, and `ECLIPSE_VANGUARD_STUN_TRIGGER_READY_POWER_1`.
- `CoreRuleEngine.ResolveEclipseVanguardStunTriggers` now scans public, controlled field source units, resolves each source through `CardBehaviorRegistry.TryGetByCardNo`, and applies matching behavior fields.
- Existing `TRIGGER_RESOLVED.trigger`, `TRIGGER_RESOLVED.effectKind`, and `POWER_MODIFIED_UNTIL_END_OF_TURN.reason` payload values remain `ECLIPSE_VANGUARD_STUN_TRIGGER_READY_POWER_1`.

## Validation

- A pre-change full baseline was not captured in this resumed slice; the initial full run was canceled after red tests had already changed the tree.
- Red focused source guard failed before implementation because `SourceReadiesWhenControllerStunsEnemyUnit`, `SourcePowerOnControllerStunsEnemyUnitAmount`, and `SourceStunEnemyUnitTriggerEffectKind` did not exist.
- Green focused gate: `CoreRuleEngineTriggerSourceSelectionUsesBehaviorFieldsWhereAvailable|EclipseVanguardStunReadyPowerCarriesOfficialBehaviorFields|P79EclipseVanguard` passed `5/5`.
- Adjacent / hidden-info gate: `EclipseVanguard|TriggerSourceIdentityGuardTests|CardCatalogBaselineTests|PaymentEngineCoverageAuditTests|MatchRecovery` passed `3029/3029`.
- Backend full conformance passed `9035/9035`.

## Holdbacks

This does not close complete stun-trigger timing, complete TriggerSpec migration, complete `ORDER_TRIGGERS` / APNAP ordering, complete stun family breadth, P0 full objective, P1, or READY.
