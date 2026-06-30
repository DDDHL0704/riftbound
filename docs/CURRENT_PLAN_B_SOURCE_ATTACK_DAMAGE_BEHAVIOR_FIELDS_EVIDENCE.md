# Plan B Source Attack Damage Behavior Fields Evidence

Date: 2026-06-30

Project status: **NOT READY**.

## Evidence Basis

Official card data:

- `data/official/card-catalog.zh-CN.json` row `OGN·130/298` 神射海盗 states that when the source attacks, it deals 1 damage to an enemy unit at that battlefield.

Existing engine evidence:

- `P79SharpshooterPirateDamagesEnemyUnitWhenAttackingBattlefield` proves the representative attack-damage trigger path.
- `P79SharpshooterPirateSkipsAttackDamageWhenDefending` proves the defensive source does not emit the attack trigger.
- `docs/rules-evidence-index.md` already records the `p2-preflight-play-sharpshooter-pirate-attack-trigger-static` and target-rejection evidence rows.

## Engine Evidence

Before this slice, `CoreRuleEngine.ResolveSharpshooterPirateAttackDamageTrigger` selected sources through `SharpshooterPirateAttackTriggerSourceEffectKind = SHARPSHOOTER_PIRATE_ATTACK_TRIGGER_PLAY_UNIT` and used Core constants for the emitted effect kind and 1 damage value.

After this slice:

- `CoreRuleEngine` no longer contains `SharpshooterPirateAttackTriggerSourceEffectKind`.
- `CoreRuleEngine` no longer contains `SHARPSHOOTER_PIRATE_ATTACK_TRIGGER_PLAY_UNIT` as a runtime selector.
- `CoreRuleEngine` no longer contains `SHARPSHOOTER_PIRATE_ATTACK_DAMAGE_1`; that value lives on the official `OGN·130/298` behavior row.
- `CardBehaviorRegistry` stores the official source attack-damage amount and emitted effect kind on the `OGN·130/298` behavior row.
- `CoreRuleEngine.ResolveSourceAttackDamageToFirstDefenderTriggers` scans public controlled attacking source units and applies any matching `SourceAttackDamageToFirstDefenderAmount` behavior fields.

## Test Evidence

- `TriggerSourceIdentityGuardTests.CoreRuleEngineTriggerSourceSelectionUsesBehaviorFieldsWhereAvailable` blocks reintroducing the Sharpshooter Pirate runtime effect-kind selector and emitted effect-kind constant in Core.
- `CardCatalogBaselineTests.SharpshooterPirateAttackDamageCarriesOfficialBehaviorFields` locks the official row to `SourceAttackDamageToFirstDefenderAmount=1` and effect kind `SHARPSHOOTER_PIRATE_ATTACK_DAMAGE_1`.
- Existing Sharpshooter Pirate focused regressions passed unchanged, proving trigger behavior is preserved.
- Baseline before this slice: backend full conformance passed `9033/9033`.
- Focused behavior-field gate passed `4/4`.
- Adjacent / hidden-info gate passed `3194/3194`.
- Backend full conformance passed `9034/9034`.

## Non-Claims

This evidence does not claim complete combat-trigger timing, complete attack-trigger target selection, complete TriggerSpec migration, complete `ORDER_TRIGGERS` / APNAP ordering, P0 completion, P1, or READY.
