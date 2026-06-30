# Plan B Source Attack Damage Behavior Fields Audit

Date: 2026-06-30

Project status: **NOT READY**.

## Scope

This slice removes the Sharpshooter Pirate-specific runtime effect-kind selector and hard-coded 1 damage value from the shared battle attack-trigger path.

The stable catalog effect id `SHARPSHOOTER_PIRATE_ATTACK_TRIGGER_PLAY_UNIT` remains in `CardBehaviorRegistry`, fixtures, and matrix evidence as row identity data. `CoreRuleEngine` no longer references that id to decide whether an attacking field unit damages the first defending unit.

## Authority

- `data/official/card-catalog.zh-CN.json` row `OGN·130/298` 神射海盗: when the source attacks, it deals 1 damage to an enemy unit at that battlefield.
- Existing evidence index entry `p2-preflight-play-sharpshooter-pirate-attack-trigger-static` records the official card row, `CORE-260330` unit/play/battle authorities, representative `DECLARE_BATTLE`, `TRIGGER_RESOLVED`, and `DAMAGE_APPLIED`.
- Existing `P79SharpshooterPirate*` tests cover the authoritative attack trigger and the defensive skip path.

## Implementation

- `CardBehaviorDefinition` now carries attack-damage trigger metadata:
  - `SourceAttackDamageToFirstDefenderAmount`
  - `SourceAttackDamageToFirstDefenderEffectKind`
- `OGN·130/298` fills those fields with `1` and `SHARPSHOOTER_PIRATE_ATTACK_DAMAGE_1`.
- `CoreRuleEngine.ResolveSourceAttackDamageToFirstDefenderTriggers` now scans public, controlled attacking source units, resolves each source through `CardBehaviorRegistry.TryGetByCardNo`, and applies matching behavior fields.
- Existing `TRIGGER_RESOLVED.effectKind` and `DAMAGE_APPLIED.effectKind` payload values remain `SHARPSHOOTER_PIRATE_ATTACK_DAMAGE_1`.

## Validation

- Baseline before this slice: backend full conformance passed `9033/9033`.
- Red focused source guard failed before implementation because `SourceAttackDamageToFirstDefenderAmount` and `SourceAttackDamageToFirstDefenderEffectKind` did not exist.
- Green focused gate: `CoreRuleEngineTriggerSourceSelectionUsesBehaviorFieldsWhereAvailable|SharpshooterPirateAttackDamageCarriesOfficialBehaviorFields|P79SharpshooterPirate` passed `4/4`.
- Adjacent / hidden-info gate: `SharpshooterPirate|DeclareBattle|BattleDamageAssignment|TriggerSourceIdentityGuardTests|CardCatalogBaselineTests|PaymentEngineCoverageAuditTests|MatchRecovery` passed `3194/3194`.
- Backend full conformance passed `9034/9034`.

## Holdbacks

This does not close complete combat-trigger timing, complete attack-trigger target selection, complete TriggerSpec migration, complete `ORDER_TRIGGERS` / APNAP ordering, P0 full objective, P1, or READY.
