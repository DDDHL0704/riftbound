# Plan B Source Stun Ready Power Behavior Fields Evidence

Date: 2026-06-30

Project status: **NOT READY**.

## Evidence Basis

Official card data:

- `data/official/card-catalog.zh-CN.json` row `OGN·059/298` 星蚀先锋 states that when the controller stuns an enemy unit, the source is readied and gets +1 power this turn.

Existing engine evidence:

- `P79EclipseVanguardReadiesAndGainsPowerWhenControllerStunsEnemyUnit` proves the representative enemy-stun ready/+1 path.
- `P79EclipseVanguardSkipsTriggerWhenControllerStunsFriendlyUnit` proves friendly-unit stun does not emit this trigger.
- `P79EclipseVanguardSkipsTriggerWhenSourceIsStandby` proves standby sources do not emit the trigger.
- `docs/rules-evidence-index.md` already records the `p2-preflight-play-eclipse-vanguard-stun-trigger-static` and target-rejection evidence rows.

## Engine Evidence

Before this slice, `CoreRuleEngine.ResolveEclipseVanguardStunTriggers` selected sources through `EclipseVanguardStunTriggerSourceEffectKind = ECLIPSE_VANGUARD_STUN_TRIGGER_PLAY_UNIT` and used Core constants for the emitted ready/+1 effect kind and power value.

After this slice:

- `CoreRuleEngine` no longer contains `EclipseVanguardStunTriggerSourceEffectKind`.
- `CoreRuleEngine` no longer contains `ECLIPSE_VANGUARD_STUN_TRIGGER_PLAY_UNIT` as a runtime selector.
- `CoreRuleEngine` no longer contains `ECLIPSE_VANGUARD_STUN_TRIGGER_READY_POWER_1`; that value lives on the official `OGN·059/298` behavior row.
- `CardBehaviorRegistry` stores the official source stun-trigger ready flag, power amount, and emitted effect kind on the `OGN·059/298` behavior row.
- `CoreRuleEngine.ResolveEclipseVanguardStunTriggers` scans public controlled field source units and applies any matching `SourceReadiesWhenControllerStunsEnemyUnit` behavior fields.

## Test Evidence

- `TriggerSourceIdentityGuardTests.CoreRuleEngineTriggerSourceSelectionUsesBehaviorFieldsWhereAvailable` blocks reintroducing the Eclipse Vanguard runtime effect-kind selector and emitted effect-kind / power constants in Core.
- `CardCatalogBaselineTests.EclipseVanguardStunReadyPowerCarriesOfficialBehaviorFields` locks the official row to `SourceReadiesWhenControllerStunsEnemyUnit=true`, `SourcePowerOnControllerStunsEnemyUnitAmount=1`, and effect kind `ECLIPSE_VANGUARD_STUN_TRIGGER_READY_POWER_1`.
- Existing Eclipse Vanguard focused regressions passed unchanged, proving trigger behavior is preserved.
- Red focused source guard failed before implementation because the behavior fields did not exist.
- Focused behavior-field gate passed `5/5`.
- Adjacent / hidden-info gate passed `3029/3029`.
- Backend full conformance passed `9035/9035`.

## Non-Claims

This evidence does not claim complete stun-trigger timing, complete TriggerSpec migration, complete `ORDER_TRIGGERS` / APNAP ordering, complete stun family breadth, P0 completion, P1, or READY.
