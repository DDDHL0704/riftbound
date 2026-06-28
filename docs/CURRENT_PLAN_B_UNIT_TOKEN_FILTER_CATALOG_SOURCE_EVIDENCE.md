# Plan B Unit Token Filter Catalog Source Evidence

日期：2026-06-25
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

This file records the concrete evidence for moving static-aura `UNIT_TOKEN` target filtering from a local `StaticAuraSpecRules` card-number helper to the token factory catalog domain.

## 2026-06-28 Unit-Token Creation Identity Evidence

- `P6TokenFactoryCatalog` exposes named constants for Warhawk, Faerie, Sand Soldier, and Zaun minion token identities.
- `P4ActivatedAbilityCatalog.WarhawkTokenCardNo` is now an alias of `P6TokenFactoryCatalog.WarhawkTokenCardNo`.
- `CoreRuleEngine` no longer defines local `WarhawkTokenCardNo`, `FaerieTokenCardNo`, `SandSoldierTokenCardNo`, or `ZaunMinionTokenCardNo` constants.
- Lillia Faerie, Azir Sand Soldier, Warhawk, and Viktor destroyed-non-minion Zaun minion token creation paths resolve token definitions through `P6TokenFactoryCatalog`.
- `CardCatalogBaselineTests.P6UnitTokenCreationIdentityRoutesThroughTokenFactoryCatalog` covers the named constants, P4 alias parity, catalog definition membership, and source guard against reintroducing Core-local token cardNo constants.
- Validation passed: focused guard 1/1; P6TokenFactory / Warhawk / Faerie / SandSoldier / ViktorDestroyedNonMinion / FluftPoro / Azir / Lillia / Ivern / HuntingGrounds / ImperialShrine representatives 175/175; CardCatalogBaseline / TokenFactory / Token / Warhawk / Faerie / SandSoldier / Minion / ViktorDestroyedNonMinion / FullGameEndToEnd / MatchRecovery adjacent 2504/2504; backend full 8869/8869.

## 1. Runtime Evidence

- `P6TokenFactoryCatalog.IsUnitTokenFactory(cardNo)` was added as the shared token factory category helper.
- `StaticAuraSpecRules.TargetMatchesFilter` now resolves `StaticAuraTargetFilters.UnitToken` through `P6TokenFactoryCatalog.IsUnitTokenFactory(target.CardNo)`.
- The previous `StaticAuraSpecRules.IsUnitTokenCardNo` helper was deleted.
- Existing `StaticAuraSpec` parsing and continuous-effect projection are unchanged.

## 2. Test Evidence

Focused test file:

- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs`

Coverage:

- `P6TokenFactoryClassifiesUnitTokenFactoriesByCategory` covers all current official unit token factory rows.
- `P6TokenFactoryRejectsNonUnitTokenFactoriesByCategory` rejects null/empty card numbers, battlefield tokens, equipment tokens, and a normal non-token card.
- `StaticAuraUnitTokenFilterDoesNotUseLocalCardNumberHelper` blocks reintroducing `StaticAuraSpecRules.IsUnitTokenCardNo` and verifies the static-aura filter consumes `P6TokenFactoryCatalog.IsUnitTokenFactory`.
- Existing static-aura catalog tests continue to parse Soul Shepherd's `UNIT_TOKEN` target filter.

## 3. Verification

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~P6TokenFactoryClassifiesUnitTokenFactoriesByCategory|FullyQualifiedName~P6TokenFactoryRejectsNonUnitTokenFactoriesByCategory|FullyQualifiedName~StaticAuraUnitTokenFilterDoesNotUseLocalCardNumberHelper|FullyQualifiedName~StaticAuraCatalogParsesCurrentPowerAuras"
```

Result: 17/17 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~StaticAura|FullyQualifiedName~TokenFactory|FullyQualifiedName~UnitToken|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~P6TokenFactory|FullyQualifiedName~SoulShepherd|FullyQualifiedName~CardCatalogBaselineTests"
```

Result: 632/632 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"
```

Result: 1989/1989 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj
```

Result: 8571/8571 passed.

## 4. Helper Count

After this slice, `rg -n "private static bool Is.*CardNo\\(" src/Riftbound.Engine src/Riftbound.CardCatalog src/Riftbound.Contracts` reports 27 total helpers, with 25 in `CoreRuleEngine.cs`.

## 5. Non-Closure Statement

This evidence does not close complete token taxonomy, complete static-aura target-filter breadth, card matrix full-official, frontend final validation or READY.
