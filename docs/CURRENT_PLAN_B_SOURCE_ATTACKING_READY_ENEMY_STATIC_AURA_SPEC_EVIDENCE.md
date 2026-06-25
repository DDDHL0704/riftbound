# Plan B / Source Attacking Ready Enemy Static Aura Spec Evidence

Date: 2026-06-25

Project status: **NOT READY**.

## Rule Sources

- `data/official/card-catalog.zh-CN.json`: `OGN·131/298` 沙丘亚龙 official text is `当我进攻时，如果此处有处于活跃状态的敌方单位，则让我{{S}}+2。`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`: existing P1-002/P1-004 Dune Drake ready-enemy combat-power representative evidence.
- `docs/rules-authority-and-audit.md` and `docs/rules-evidence-index.md`: official catalog and local evidence-index entries remain the authority inputs for this representative slice.

## BehaviorSpec Evidence

- `BehaviorSpecCatalogParsesStaticAuraSpecsForExistingRepresentatives` verifies Dune Drake parses to `StaticAuraSpec.Kind = SOURCE_ATTACKING_READY_ENEMY_UNIT_POWER`, `Layer = STATIC_AURA`, `TargetScope = SOURCE_OBJECT`, `ParticipantScope = READY_ENEMY_BATTLEFIELD_PUBLIC_UNITS`, `PowerDeltaPerParticipant = 2`, and `RequiredReadyEnemyUnitCount = 1`.
- `StaticAuraProjectionDoesNotUseMatchSessionCardNumberAllowList` now also verifies `CoreRuleEngine` no longer contains `DuneDrakeCardNo` or `OGN·131/298`.
- `P79DuneDrakeGainsPowerWhenAttackingReadyEnemyUnit` and `P79DuneDrakeSkipsPowerWhenDefending` keep the existing combat-power runtime representative green.

## Runtime Evidence

- `StaticAuraSpecRules.TryGetSourceAttackingReadyEnemyUnitPowerAura(...)` exposes source-attacking-ready-enemy combat static auras from `BehaviorSpecCatalogBuilder`.
- `CoreRuleEngine.ResolveSourceAttackingReadyEnemyUnitPowerBonus(...)` reads the ready-enemy threshold and power delta from `StaticAuraSpec` instead of selecting by Dune Drake card number.
- `src/Riftbound.DevUi/src/types/catalog.ts` now includes `staticAuras[].requiredReadyEnemyUnitCount` so catalog consumers can read the new spec field.

## Validation

- Focused static-aura parse / source guard / Dune Drake representatives: `4/4` passing.
- Adjacent static-aura / Dune Drake / Waterbender / Scarlet Pigeon / Wise Elder / Ornn / combat damage / declare battle / full-game representatives: `411/411` passing.
- `MatchRecovery`: `1989/1989` passing.
- Backend full conformance: `8531/8531` passing.
- DevUi build: passing.

## Residual Risk

- This slice proves one source-attacking-ready-enemy combat static aura has moved to BehaviorSpec-driven routing. It does not prove complete combat conditional power, complete battle / spell-duel lifecycle, assignment prompt breadth, frontend smoke, or READY.
