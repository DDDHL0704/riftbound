# Plan B Source Object Filtered Keyword Aura Evidence

Date: 2026-06-30

Supplement: 2026-07-01

Project status: **NOT READY**.

## Evidence Basis

Official card data:

- `data/official/card-catalog.zh-CN.json` row `OGN·125/298` 比尔吉沃特恶霸 states that if the source has a boon, it gains `游走`.

Existing evidence:

- `P79BilgewaterBullyWithBoonCanUseRoam` covers the boon-present movement permission path.
- `P79BilgewaterBullyWithoutBoonDoesNotUseRoam` covers the boon-absent rejection path.
- The audited fixture rows remain recorded in `docs/rules-evidence-index.md`.

## Engine Evidence

Before this slice, `CoreRuleEngine` and `MatchSession` selected Bilgewater Bully's Roam permission through `BILGEWATER_BULLY_NO_BOON_ROAM_PLAY_UNIT`.

After this slice:

- `CoreRuleEngine` no longer contains `BilgewaterBullyBoonRoamSourceEffectKind`.
- `MatchSession` no longer contains `BilgewaterBullyBoonRoamSourceEffectKind`.
- `CoreRuleEngine` and `MatchSession` no longer contain `BILGEWATER_BULLY_NO_BOON_ROAM_PLAY_UNIT` as a runtime selector.
- `StaticAuraParser` parses the official conditional keyword text into `SOURCE_OBJECT_FILTERED_KEYWORD`.
- Runtime and prompt permission checks enumerate `StaticAuraSpecRules.GetStaticAuras(cardNo)` and filter by `StaticAuraSpecRules.IsSourceObjectKeywordStaticAura`.
- Continuous-effect projection now emits a recomputed `RULE_TEXT:SOURCE_OBJECT_FILTERED_KEYWORD:*` state when the source is public, in-field, a unit, and currently matches the tag filter.
- `StaticAuraSpecRules.TryGetSourceObjectFilteredKeywordAura` has been removed from the engine helper surface; the remaining `SOURCE_OBJECT_FILTERED_KEYWORD` value is catalog data parsed from official text, not a runtime selector.

## Test Evidence

- `MovementSourceIdentityGuardTests.BilgewaterBullyBoonRoamSourceIdentityUsesSourceObjectFilteredKeywordAura` failed red before implementation because `CoreRuleEngine` still contained `BilgewaterBullyBoonRoamSourceEffectKind`.
- `CardCatalogBaselineTests.BehaviorSpecCatalogParsesStaticAuraSpecsForExistingRepresentatives` failed red before implementation because `OGN·125/298` had no source-object filtered keyword aura.
- The same guard now blocks reintroducing the Bilgewater runtime effect-kind selector and requires `StaticAuraSpecRules.IsSourceObjectKeywordStaticAura` in both Core and MatchSession.
- `BattlefieldStaticAuraSpecRoutingGuardTests.SourceObjectKeywordStaticAuraExecutionRoutesThroughBehaviorSpecScope` blocks reintroducing `StaticAuraSpecRules.TryGetSourceObjectFilteredKeywordAura` in both Core and MatchSession and requires the shared source-object keyword scope predicate.
- Existing Bilgewater representative tests still pass for boon-present and boon-absent Roam behavior.
- Adjacent / hidden-info gate `MovementSourceIdentityGuardTests|BilgewaterBully|PreciseRoam|MoveUnit|CardCatalogBaselineTests|MatchRecovery` passed `2393/2393`.
- Backend full conformance passed `9026/9026`.
- 2026-07-01 focused routing gate `SourceObjectKeywordStaticAuraExecutionRoutesThroughBehaviorSpecScope|BilgewaterBullyBoonRoamSourceIdentityUsesSourceObjectFilteredKeywordAura|P79BilgewaterBully` passed `4/4`.
- 2026-07-01 adjacent / hidden-info gate `BattlefieldStaticAuraSpecRoutingGuardTests|MovementSourceIdentityGuardTests|CardCatalogBaselineTests|P79BilgewaterBully|PreciseRoam|MoveUnit|MatchRecovery` passed `2411/2411`.
- 2026-07-01 backend full conformance passed `9066/9066`.

## Non-Claims

This evidence does not claim complete source-object filtered keyword official breadth, complete Roam timing, complete movement lifecycle, complete boon-token family, P0 completion, or READY.
