# Plan B Other-Friendly Static Keyword Aura Spec Evidence

更新时间：2026-06-27

## Evidence Summary

This slice turns `其他友方单位获得{{...}}` static keyword grants into a shared BehaviorSpec-driven engine path.

Implemented evidence:

- `RuleTextParsers.StaticAuraParser` parses global other-friendly keyword grants into `StaticAuraSpec.Kind=OTHER_FRIENDLY_UNITS_KEYWORD`.
- `StaticAuraSpecRules.GetStaticAuras(cardNo, StaticAuraKinds.OtherFriendlyUnitsKeyword)` exposes parsed specs to shared engine layers.
- `MatchSession.BuildOtherFriendlyUnitsKeywordAuraEffects` projects RULE_TEXT continuous effects to public other friendly units while excluding the source.
- Play-card prompts and Core play-card planning both use a shared static-granted keyword check for `预知`, so public Gemstone Seer grants the top-card optional recycle target to a later-played other friendly unit.
- Existing combat/resource keyword resolvers now include `OTHER_FRIENDLY_UNITS_KEYWORD`, keeping the new spec kind generic instead of lifecycle-Predict-only.

## Covered Cards

| Card | Official text | BehaviorSpec |
|---|---|---|
| `OGN·100/298` 宝石真知者 | `{{预知}}。其他友方单位获得{{预知}}。` | `Kind=OTHER_FRIENDLY_UNITS_KEYWORD`, `GrantedKeyword=预知` |

## Tests

- `CardCatalogBaselineTests.BehaviorSpecCatalogParsesStaticAuraSpecsForExistingRepresentatives`
  - Verifies Gemstone Seer parses to `OTHER_FRIENDLY_UNITS_KEYWORD`, `RULE_TEXT`, `WHILE_SOURCE_AND_TARGET_ON_PUBLIC_FIELD`, `OTHER_FRIENDLY_UNITS`, `OTHER_FRIENDLY_PUBLIC_UNITS`, `PowerDeltaPerParticipant=0`, `TargetFilter=null`, and `GrantedKeyword=预知`.
- `ConformanceFixtureRunnerTests.GemstoneSeerStaticGrantedPredictPromptExposesOnlyFriendlyTopMainDeckCardForOtherFriendlyUnit`
  - Verifies public Gemstone Seer grants `预知` to a later-played other friendly unit in hand.
  - Verifies the prompt exposes only the controller's top main-deck card, excluding the second friendly card and the opponent's hidden deck card.
- `ConformanceFixtureRunnerTests.CoreRuleEnginePlaysOtherFriendlyStaticGrantedPredictSourceUnitRecycleTopCard`
  - Verifies Core accepts the static-granted `预知` target, resolves the stack after both players pass, plays the unit to base, recycles the selected top card to the bottom of the main deck, and projects the RULE_TEXT aura to the now-public other friendly unit.

## Validation Results

- Focused other-friendly static keyword Predict slice: 3/3 passed.
- Adjacent Gemstone / OtherFriendly / StaticKeyword / StaticAura / Predict / MatchRecovery: 2119/2119 passed.
- Backend full conformance: 8805/8805 passed.

## Residuals

Not closed by this slice:

- Complete keyword removal / loss layering.
- Static-granted `预知` simultaneous-entry and self-grant edge cases.
- Other-friendly keyword breadth beyond the `预知` representative.
- Gemstone Seer official-deck full-game route.
