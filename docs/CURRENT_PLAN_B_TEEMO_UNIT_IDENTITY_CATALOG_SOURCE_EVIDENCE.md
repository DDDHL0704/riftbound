# Plan B Teemo Unit Identity Catalog Source Evidence

日期：2026-06-25
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

This file records the concrete evidence for removing duplicated Teemo unit card-number allow-lists from the legend-action target domain.

## 1. Runtime Evidence

- `CardBehaviorRegistry.IsImplementedUnitNamed(cardNo, displayName)` was added as a generic implemented-unit identity helper.
- `CoreRuleEngine.IsValidOwnedTeemoUnitTarget` now validates target identity with `CardBehaviorRegistry.IsImplementedUnitNamed(targetState.CardNo, "提莫")`.
- `MatchSession.LegendActionIsValidOwnedTeemoUnitTarget` now uses the same registry identity for prompt target generation.
- The previous `CoreRuleEngine.IsTeemoUnitCardNo` helper was deleted.
- The previous `MatchSession.LegendActionIsTeemoUnitCardNo` helper was deleted.
- Existing target guards remain: target must be visible, unit-tagged, owned by the acting player, and located in an allowed owned zone before the legend action can recall it.

## 2. Test Evidence

Focused test file:

- `tests/Riftbound.ConformanceTests/TeemoLegendActionDomainGuardTests.cs`

Coverage:

- `CardBehaviorRegistryIdentifiesImplementedTeemoUnitsByCatalogIdentity` covers all eight existing implemented Teemo unit card Nos.
- `CardBehaviorRegistryDoesNotTreatLegendsOrOtherUnitsAsTeemoUnits` rejects Teemo legends and another unit.
- `TeemoLegendActionDomainDoesNotUseDuplicatedCardNumberAllowLists` blocks reintroducing the Core / MatchSession Teemo unit cardNo helpers and verifies both files consume `CardBehaviorRegistry.IsImplementedUnitNamed`.
- Existing `P79LegendActTeemoRecallsOwnedChampionZoneTeemoUnit` verifies command behavior remains intact.

## 3. Verification

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~TeemoLegendActionDomainGuardTests|FullyQualifiedName~P79LegendActTeemoRecallsOwnedChampionZoneTeemoUnit"
```

Result: 13/13 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Teemo|FullyQualifiedName~LegendAct|FullyQualifiedName~LegendAction|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~PaymentEngineCoverageAuditTests"
```

Result: 1105/1105 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"
```

Result: 1989/1989 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj
```

Result: 8545/8545 passed.

## 4. Helper Count

After this slice, `rg -n "private static bool Is.*CardNo\\(" src/Riftbound.Engine src/Riftbound.CardCatalog src/Riftbound.Contracts` reports 30 total helpers, with 27 in `CoreRuleEngine.cs`.

## 5. Non-Closure Statement

This evidence does not close Teemo full official behavior, full legend-action official breadth, standby replacement breadth, hidden-info / random-zone breadth, card matrix full-official, frontend final validation or READY.
