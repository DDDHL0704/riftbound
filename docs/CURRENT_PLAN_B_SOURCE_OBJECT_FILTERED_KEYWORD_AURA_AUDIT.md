# Plan B Source Object Filtered Keyword Aura Audit

Date: 2026-06-30

Supplement: 2026-07-01

Project status: **NOT READY**.

## Scope

This slice moves `OGN·125/298` 比尔吉沃特恶霸's current boon-gated Roam permission from a runtime effect-kind selector to the shared `BehaviorSpec.StaticAuras` rule-text layer.

The stable catalog effect id `BILGEWATER_BULLY_NO_BOON_ROAM_PLAY_UNIT` remains catalog row identity data. `CoreRuleEngine` and `MatchSession` no longer use it to decide whether the source has Roam.

The 2026-07-01 follow-up removes the remaining source-object filtered keyword kind selector from the engine consumption path. Runtime, prompt, and projection now identify source-object keyword auras by `RULE_TEXT` layer plus `SOURCE_OBJECT` target/participant scope rather than by `StaticAuraKinds.SourceObjectFilteredKeyword`.

## Authority

- `data/official/card-catalog.zh-CN.json` row `OGN·125/298` 比尔吉沃特恶霸: if the source has a boon, it gains `游走`.
- `docs/rules-evidence-index.md` already records `p2-preflight-play-bilgewater-bully-no-boon-roam-static` and `p4-play-bilgewater-bully-target-rejected` as audited representative paths.
- Existing representative tests cover both the boon-present and boon-absent Roam permission paths.

## Implementation

- `BehaviorSpec.StaticAuras` now has `StaticAuraKinds.SourceObjectFilteredKeyword`.
- `StaticAuraParser` parses `如果我拥有...，则我获得{{...}}` into:
  - `Layer=RULE_TEXT`
  - `TargetScope=SOURCE_OBJECT`
  - `ParticipantScope=SOURCE_OBJECT`
  - `TargetFilter=TAG:<condition>`
  - `GrantedKeyword=<keyword>`
- `StaticAuraSpecRules.IsSourceObjectKeywordStaticAura` identifies shared source-object keyword auras from BehaviorSpec shape:
  - `Layer=RULE_TEXT`
  - `TargetScope=SOURCE_OBJECT`
  - `ParticipantScope=SOURCE_OBJECT`
  - non-empty `GrantedKeyword`
- `CoreRuleEngine` enumerates `StaticAuraSpecRules.GetStaticAuras(cardNo)` and applies the optional `TargetFilter` for Roam permission and combat keyword recomputation.
- `MatchSession` reads the same scope predicate for prompt Roam permission and continuous-effect projection.
- `StaticAuraSpecRules.TryGetSourceObjectFilteredKeywordAura` has been removed so new runtime code cannot reintroduce the kind-specific selector.

## Validation

- Baseline before this slice: backend full conformance passed `9026/9026`.
- Red focused guard failed before implementation:
  - `MovementSourceIdentityGuardTests.BilgewaterBullyBoonRoamSourceIdentityUsesSourceObjectFilteredKeywordAura` still found `BilgewaterBullyBoonRoamSourceEffectKind`.
  - `CardCatalogBaselineTests.BehaviorSpecCatalogParsesStaticAuraSpecsForExistingRepresentatives` found no `StaticAuras` entry for `OGN·125/298`.
- Engine build passed: `dotnet build src/Riftbound.Engine/Riftbound.Engine.csproj -c Debug`.
- Green focused representative gate `BilgewaterBullyBoonRoamSourceIdentityUsesSourceObjectFilteredKeywordAura|BehaviorSpecCatalogParsesStaticAuraSpecsForExistingRepresentatives|P79BilgewaterBully` passed `4/4`.
- Adjacent / hidden-info gate `MovementSourceIdentityGuardTests|BilgewaterBully|PreciseRoam|MoveUnit|CardCatalogBaselineTests|MatchRecovery` passed `2393/2393`.
- Backend full conformance passed `9026/9026`.
- 2026-07-01 focused routing gate `SourceObjectKeywordStaticAuraExecutionRoutesThroughBehaviorSpecScope|BilgewaterBullyBoonRoamSourceIdentityUsesSourceObjectFilteredKeywordAura|P79BilgewaterBully` passed `4/4`.
- 2026-07-01 adjacent / hidden-info gate `BattlefieldStaticAuraSpecRoutingGuardTests|MovementSourceIdentityGuardTests|CardCatalogBaselineTests|P79BilgewaterBully|PreciseRoam|MoveUnit|MatchRecovery` passed `2411/2411`.
- 2026-07-01 backend full conformance passed `9066/9066`.

## Holdbacks

This does not close complete source-object filtered keyword official breadth, complete Roam timing, complete movement lifecycle, complete boon-token family, P0 full objective, or READY.
