# Plan B Sigil Typed Resource Activated Ability Spec Evidence

Date: 2026-07-02

Project status: **NOT READY**.

## Evidence Basis

Official card data is the authority for this slice:

- SFD Sigils: `SFD·222/221`, `SFD·226/221`, `SFD·229/221`, `SFD·231/221`, `SFD·234/221`, `SFD·238/221`.
- OGN Sigils: `OGN·040/298`, `OGN·081/298`, `OGN·120/298`, `OGN·163/298`, `OGN·204/298`, `OGN·245/298`.
- Core rules already indexed for payment resources: `CORE-260330` p10-p13 rules 131 and 135.2.e; p20 rules 162-167.

These cards share a single rules text family: an exhausted source pays the activated cost, the ability has reaction timing, and the generated typed rune power is restricted to rune-cost payment.

## Engine Evidence

Before this slice:

- `P4ActivatedAbilityCatalog` contained a hand-written `P4SigilTypedResourceProfile[]` row for each implemented SFD / OGN Sigil.
- The runtime ability ids, effect kinds, and payment restrictions were already implemented, but the profile source was engine-maintained data rather than BehaviorSpec output.
- Adding another same-shape Sigil required editing the engine catalog.

After this slice:

- `RuleTextParsers.ActivatedAbilityParser` parses the official Sigil text into `ActivatedAbilitySpec.Kind = TYPED_RESOURCE_SKILL`.
- The parsed spec records `ExhaustsSourceAsCost=true`, `ReactionSpeed=true`, `IsResourceSkill=true`, `PaymentOnlyResource=true`, `GeneratedPowerTrait`, and `GeneratedPower=1`.
- `P4ActivatedAbilityCatalog` derives Sigil typed-resource profiles from the built BehaviorSpec catalog and reconstructs the same public profile fields from the structured spec.
- Existing public constants and runtime wire strings remain compatible, including SFD / OGN ability-id prefixes and `PAY_RUNE_COSTS_ONLY_TYPED_*_TEMPORARY_LEDGER_4D_03*` restrictions.
- The source-card group selector still preserves distinct SFD / OGN runtime definitions; this slice changes the profile definition source, not source identity grouping.

## Test Evidence

- `CardCatalogBaselineTests.BehaviorSpecCatalogParsesSigilTypedResourceActivatedAbilities` verifies all 12 official Sigil rows parse to the structured typed-resource activated ability fields.
- `ActivatedAbilitySourceIdentityGuardTests.P4SigilTypedResourceProfilesAreDerivedFromBehaviorSpecs` blocks reintroducing the hand-written `P4SigilTypedResourceProfile[]` table and requires `P4ActivatedAbilityCatalog` to build profiles from `BehaviorSpecCatalogBuilder`.
- Red focused guard failed before implementation on missing structured activated-ability fields.
- Focused gate passed `2/2`.
- Adjacent / hidden-info gate passed `3382/3382`.
- Backend full conformance passed `9145/9145`.
- Dev UI build passed with `PATH="/opt/homebrew/bin:/Users/dinghaolin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin:/usr/bin:/bin:/usr/sbin:/sbin" npm --prefix src/Riftbound.DevUi run build`.

## Non-Claims

This evidence does not claim complete P4 activated ability BehaviorSpec migration, complete resource-skill official breadth, complete payment / targeting / timing coverage, P0 closure, P1 closure, card-matrix readiness, or READY.
