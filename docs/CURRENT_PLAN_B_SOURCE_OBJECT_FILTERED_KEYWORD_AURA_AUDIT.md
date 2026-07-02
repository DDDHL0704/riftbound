# Plan B Source Object Filtered Keyword Aura Audit

Date: 2026-06-30

Supplement: 2026-07-01, 2026-07-02

Project status: **NOT READY**.

## Scope

This slice moves `OGN·125/298` 比尔吉沃特恶霸's current boon-gated Roam permission from a runtime effect-kind selector to the shared `BehaviorSpec.StaticAuras` rule-text layer.

The stable catalog effect id `BILGEWATER_BULLY_NO_BOON_ROAM_PLAY_UNIT` remains catalog row identity data. `CoreRuleEngine` and `MatchSession` no longer use it to decide whether the source has Roam.

The 2026-07-01 follow-up removes the remaining source-object filtered keyword kind selector from the engine consumption path. Runtime, prompt, and projection now identify source-object keyword auras by `RULE_TEXT` layer plus `SOURCE_OBJECT` target/participant scope rather than by `StaticAuraKinds.SourceObjectFilteredKeyword`.

The 2026-07-02 follow-up extends the same source-object keyword route to level-gated official text such as `UNL-075/219` 风行狐, `UNL-047/219` 踏苔蜥, and `UNL-113/219` / `UNL-113a/219` 易. These cards now parse `{{等级N>}} 我获得...` keyword grants as `BehaviorSpec.StaticAuras` with `RequiredPlayerExperience`, and runtime/prompt/projection checks enforce that controller experience threshold through the shared source-object keyword predicate.

The later 2026-07-02 follow-up wires source-object level `法盾` grants into the same resource-keyword target-tax calculation used for printed and other-source granted Spellshield. `UNL-047/219` 踏苔蜥 can now charge the enemy spell target tax from its `SOURCE_OBJECT_LEVEL_KEYWORD` aura even when the object has no materialized `法盾` tag.

The B0 evidence follow-up adds an official-deck-derived score-victory replay for the same `UNL-047/219` source-object level `法盾` target-tax route. It does not add runtime behavior; it proves the existing shared Core/MatchSession path survives official deck submission/opening, prompt-driven `PLAY_CARD`, stack resolution, action-log replay, and hidden-info recovery checks.

## Authority

- `data/official/card-catalog.zh-CN.json` row `OGN·125/298` 比尔吉沃特恶霸: if the source has a boon, it gains `游走`.
- `data/official/card-catalog.zh-CN.json` row `UNL-075/219` 风行狐: at level 3, the source gains `{{S}}+1` and `游走`.
- `data/official/card-catalog.zh-CN.json` row `UNL-047/219` 踏苔蜥: at level 3, the source gains `{{S}}+1` and `法盾`.
- `data/official/card-catalog.zh-CN.json` rows `UNL-113/219` and `UNL-113a/219` 易: at level 6, the source gains `法盾` and `游走`.
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
- `StaticAuraParser` now parses `{{等级N>}} 我获得...` keyword grants into `StaticAuraKinds.SourceObjectLevelKeyword` with `RequiredPlayerExperience=N`.
- `CoreRuleEngine`, `MatchSession`, and prompt Roam metadata reuse the source-object keyword applicability path and reject level-gated keyword grants until the source controller has the required experience.
- `CoreRuleEngine.ResolveSpellshieldTargetTaxMana` now includes source-object resource keyword amounts from `StaticAuraSpecRules.IsSourceObjectKeywordStaticAura`, so level-gated `法盾` affects enemy spell target taxes without card-number routing or tag materialization.
- `MatchSession.SpellshieldTaxManaForTarget` uses the same source-object keyword aura route for prompt filtering and target labels.

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
- 2026-07-02 focused level-keyword gate `SourceObjectLevelPowerStaticAuraTests|BehaviorSpecCatalogParsesStaticAuraSpecsForExistingRepresentatives` passed `8/8`.
- 2026-07-02 adjacent / hidden-info gate `SourceObjectLevelPower|SourceObjectKeyword|StaticAura|Roam|MoveUnit|MatchRecovery` passed `2197/2197`.
- 2026-07-02 backend full conformance passed `9133/9133`.
- 2026-07-02 focused source-object level Spellshield target-tax gate `SourceObjectLevelPowerStaticAuraTests` passed `11/11`.
- 2026-07-02 adjacent / hidden-info gate `SourceObjectLevelPower|SourceObjectKeyword|StaticAura|Spellshield|MatchRecovery` passed `2160/2160`.
- 2026-07-02 backend full conformance passed `9137/9137`.
- 2026-07-02 B0 official-deck replay gate `OfficialDeckMidgameAppliesMossStepperSourceObjectLevelSpellshieldTaxAndScoreVictoryActionLogReplaysToFinalStateHash` passed `1/1`.
- 2026-07-02 B0 adjacent / hidden-info gate `OfficialDeckMidgameAppliesMossStepperSourceObjectLevelSpellshieldTax|SourceObjectLevelPower|SourceObjectKeyword|StaticAura|Spellshield|FullGameEndToEnd|MatchRecovery` passed `2258/2258`.
- 2026-07-02 backend full conformance after B0 evidence passed `9138/9138`.

## Holdbacks

This does not close complete source-object keyword official breadth, complete Spellshield target-tax timing breadth, complete Roam timing, complete movement lifecycle, complete boon-token family, complete B0 official-deck breadth, P0 full objective, or READY.
