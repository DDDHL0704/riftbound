# Plan B / Source Attacking Static Aura Spec Evidence

Date: 2026-06-25

Project status: **NOT READY**.

## Rule Sources

- `data/official/card-catalog.zh-CN.json`: `UNL-154/219` 猩红飞鸽 official text is `如果我和另一名单位一起进攻一处战场，则我获得{{S}}+2。`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`: existing P1-002/P1-004 Scarlet Pigeon multi-attacker combat-power representative evidence.
- `docs/rules-authority-and-audit.md` and `docs/rules-evidence-index.md`: official catalog and local evidence-index entries remain the authority inputs for this representative slice.

## BehaviorSpec Evidence

- `BehaviorSpecCatalogParsesStaticAuraSpecsForExistingRepresentatives` verifies Scarlet Pigeon parses to `StaticAuraSpec.Kind = SOURCE_ATTACKING_WITH_ANOTHER_UNIT_POWER`, `Layer = STATIC_AURA`, `TargetScope = SOURCE_OBJECT`, `ParticipantScope = ATTACKING_BATTLEFIELD_PUBLIC_UNITS`, `PowerDeltaPerParticipant = 2`, and `RequiredAttackingUnitCount = 2`.
- `StaticAuraProjectionDoesNotUseMatchSessionCardNumberAllowList` now also verifies `CoreRuleEngine` no longer contains `ScarletPigeonCardNo`, `IsScarletPigeonCardNo`, or `UNL-154/219`.
- `P79ScarletPigeonGainsPowerWhenAttackingWithAnotherUnit` keeps the existing combat-power runtime representative green.

## Runtime Evidence

- `StaticAuraSpecRules.TryGetSourceAttackingWithAnotherUnitPowerAura(...)` exposes source-attacking combat static auras from `BehaviorSpecCatalogBuilder`.
- `CoreRuleEngine.ResolveSourceAttackingWithAnotherUnitPowerBonus(...)` reads the attacker-count threshold and power delta from `StaticAuraSpec` instead of selecting by Scarlet Pigeon card number.
- `src/Riftbound.DevUi/src/types/catalog.ts` now includes `staticAuras[].requiredAttackingUnitCount` so catalog consumers can read the new spec field.

## Validation

- Focused static-aura parse / source guard / Scarlet Pigeon representative: `3/3` passing.
- Adjacent static-aura / Scarlet Pigeon / Wise Elder / Ornn / Dune Drake / Waterbender / combat damage / declare battle / full-game representatives: `411/411` passing.
- `MatchRecovery`: `1989/1989` passing.
- Backend full conformance: `8531/8531` passing.
- DevUi build: passing.

## Residual Risk

- This slice proves one source-attacking combat static aura has moved to BehaviorSpec-driven routing. It does not prove Dune Drake, Waterbender, complete combat conditional power, complete battle / spell-duel lifecycle, assignment prompt breadth, frontend smoke, or READY.
