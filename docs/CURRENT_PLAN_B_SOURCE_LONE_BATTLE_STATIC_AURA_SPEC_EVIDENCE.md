# Plan B / Source Lone Battle Static Aura Spec Evidence

Date: 2026-06-25

Project status: **NOT READY**.

## Rule Sources

- `data/official/card-catalog.zh-CN.json`: `OGN·055/298` 驭水者 official text is `如果我独自进攻或防守一处战场，则我获得 {{S}}+2。`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`: existing P1-002/P1-004 Waterbender lone-battle combat-power representative evidence.
- `docs/rules-authority-and-audit.md` and `docs/rules-evidence-index.md`: official catalog and local evidence-index entries remain the authority inputs for this representative slice.

## BehaviorSpec Evidence

- `BehaviorSpecCatalogParsesStaticAuraSpecsForExistingRepresentatives` verifies Waterbender parses to `StaticAuraSpec.Kind = SOURCE_LONE_BATTLE_POWER`, `Layer = STATIC_AURA`, `TargetScope = SOURCE_OBJECT`, `ParticipantScope = BATTLEFIELD_PUBLIC_UNITS`, `PowerDeltaPerParticipant = 2`, `RequiredAttackingUnitCount = 1`, and `RequiredDefendingUnitCount = 1`.
- `StaticAuraProjectionDoesNotUseMatchSessionCardNumberAllowList` now also verifies `CoreRuleEngine` no longer contains `WaterbenderCardNo` or `OGN·055/298`.
- `P79WaterbenderGainsPowerWhenAttackingAlone`, `P79WaterbenderSkipsPowerWhenAttackingWithAnotherUnit`, and `P79WaterbenderGainsPowerWhenDefendingAlone` keep the existing combat-power runtime representatives green.

## Runtime Evidence

- `StaticAuraSpecRules.IsSourceLoneBattlePowerStaticAura(...)` identifies lone-battle combat static auras from `BehaviorSpecCatalogBuilder` by `SOURCE_OBJECT` target scope and `BATTLEFIELD_PUBLIC_UNITS` participant scope.
- `CoreRuleEngine.ResolveSourceBattleStatePowerStaticAuraBonus(...)` reads the attacker-count / defender-count thresholds and power delta from `StaticAuraSpec` instead of selecting by Waterbender card number.
- `src/Riftbound.DevUi/src/types/catalog.ts` now includes `staticAuras[].requiredDefendingUnitCount` so catalog consumers can read the new spec field.

## Validation

- Focused static-aura parse / source guard / Waterbender representatives: `5/5` passing.
- Adjacent static-aura / Waterbender / Scarlet Pigeon / Dune Drake / Wise Elder / Ornn / combat damage / declare battle / full-game representatives: `411/411` passing.
- `MatchRecovery`: `1989/1989` passing.
- Backend full conformance: `8531/8531` passing.
- DevUi build: passing.

## Residual Risk

- This slice proves one source-lone-battle combat static aura has moved to BehaviorSpec-driven routing. It does not prove Dune Drake, complete combat conditional power, complete battle / spell-duel lifecycle, assignment prompt breadth, frontend smoke, or READY.
