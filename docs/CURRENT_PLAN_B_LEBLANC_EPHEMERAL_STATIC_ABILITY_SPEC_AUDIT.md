# Plan B LeBlanc Ephemeral Static AbilitySpec Audit

Date: 2026-06-30

Project status: **NOT READY**.

## Scope

This slice migrates the existing LeBlanc same-battlefield Ephemeral cleanup suppression from a Core-owned effect-kind selector to `BehaviorSpec.StaticAbilities`.

The runtime behavior is unchanged: at the controlled player's turn start, visible non-standby friendly units with `瞬息` at LeBlanc's battlefield are not destroyed by the normal Ephemeral cleanup path. This slice only changes how the engine recognizes the source text.

## Authority

- `data/official/card-catalog.zh-CN.json` row `UNL-090/219` LeBlanc / 乐芙兰: `你在我所处战场的{{瞬息}}效果不会触发。`
- `data/official/card-catalog.zh-CN.json` row `UNL-090a/219` LeBlanc / 乐芙兰: `你在我所处战场的{{瞬息}}效果不会触发。`
- Existing Plan B LeBlanc evidence records the representative turn-start cleanup suppression behavior and hidden-info boundaries.

## Implementation

- `StaticAbilityKinds.SameBattlefieldEphemeralTurnStartSuppression` defines the generic lifecycle static-ability family.
- `RuleTextParser` parses LeBlanc's official text into a `StaticAbilitySpec` with:
  - `Kind=SAME_BATTLEFIELD_EPHEMERAL_TURN_START_SUPPRESSION`
  - `TargetFilter=TAG:瞬息`
- `CardStaticAbilitySpecRules.TryGetStaticAbility(..., IsSameBattlefieldEphemeralTurnStartSuppressionAbility, ...)` validates the catalog-backed static ability shape at runtime.
- `CoreRuleEngine.IsEphemeralTurnStartSuppressedByLeblancStatic(...)` keeps the existing public-object, same-battlefield, face-up, non-standby, unit, and controller guards, but its source predicate now reads `BehaviorSpec.StaticAbilities`.
- `CoreRuleEngine` no longer defines or references:
  - `LeblancEphemeralStaticSourceEffectKind`
  - `LEBLANC_PLAY_KEYWORD_UNIT`
  - `LEBLANC_ALT_A_BACK_ROW_STATIC_PLAY_UNIT`

## Validation

- Red guard failed first because `StaticAbilityKinds.SameBattlefieldEphemeralTurnStartSuppression` did not exist.
- Focused gate passed `4/4`:
  - `BehaviorSpecCatalogParsesLeblancEphemeralSuppressionStaticAbility`
  - `LeblancEphemeralStaticSuppressionDoesNotUseDuplicatedCardNumberAllowList`
  - `CoreRuleEngineSuppressesEphemeralTurnStartAtLeblancBattlefield`
- Adjacent LeBlanc / Ephemeral / StaticAbility / CardCatalogBaseline / MatchRecovery gate passed `2387/2387`.
- Backend full conformance passed `9038/9038`.

## Holdbacks

This does not close complete Ephemeral replacement / cleanup breadth, complete LeBlanc official behavior, simultaneous lifecycle ordering, full official card-matrix readiness, frontend final validation, P0/P1, or READY.
