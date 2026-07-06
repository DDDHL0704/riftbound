# Plan B Teemo Unit Identity Catalog Source Evidence

日期：2026-06-25；2026-07-06 增量
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

This file records the concrete evidence for removing duplicated Teemo unit card-number allow-lists and rule-path display-name checks from the legend-action target domain.

## 1. Runtime Evidence

- `UnitIdentityCatalog.TeemoUnitIdentityId` is the shared identity for currently implemented Teemo units.
- `UnitIdentityCatalog.SourceCardNosForIdentity` derives that identity from current implemented unit behavior rows instead of duplicating card-number allow-lists in rule paths.
- `CoreRuleEngine.IsValidOwnedTeemoUnitTarget` now validates target identity with `UnitIdentityCatalog.IsSourceCardNoForIdentity(UnitIdentityCatalog.TeemoUnitIdentityId, targetState.CardNo)`.
- `MatchSession.LegendActionIsValidOwnedTeemoUnitTarget` now uses the same shared unit identity for prompt target generation.
- The previous `CoreRuleEngine.IsTeemoUnitCardNo` helper was deleted.
- The previous `MatchSession.LegendActionIsTeemoUnitCardNo` helper was deleted.
- `CoreRuleEngine.cs` and `MatchSession.cs` no longer contain the `"提莫"` display-name branch or `CardBehaviorRegistry.IsImplementedUnitNamed` call.
- Existing target guards remain: target must be visible, unit-tagged, owned by the acting player, and located in an allowed owned zone before the legend action can recall it.

## 2. Test Evidence

Focused test file:

- `tests/Riftbound.ConformanceTests/TeemoLegendActionDomainGuardTests.cs`

Coverage:

- `UnitIdentityCatalogIdentifiesImplementedTeemoUnitsByCatalogIdentity` covers all eight existing implemented Teemo unit card Nos.
- `UnitIdentityCatalogDoesNotTreatLegendsOrOtherUnitsAsTeemoUnits` rejects Teemo legends and another unit.
- `UnitIdentityCatalogUsesImplementedBehaviorRowsForTeemoUnitIdentity` verifies the shared identity derives the current implemented Teemo unit set.
- `TeemoLegendActionDomainDoesNotUseDuplicatedCardNumberAllowLists` blocks reintroducing the Core / MatchSession Teemo unit cardNo helpers, blocks rule-path display-name checks, and verifies both files consume `UnitIdentityCatalog`.
- Existing `P79LegendActTeemoRecallsOwnedChampionZoneTeemoUnit` verifies command behavior remains intact.

## 3. Verification

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~TeemoLegendActionDomainGuardTests|FullyQualifiedName~P79LegendActTeemoRecallsOwnedChampionZoneTeemoUnit"
```

Result: 14/14 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~Teemo|FullyQualifiedName~LegendAct|FullyQualifiedName~LegendAction|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~PaymentEngineCoverageAuditTests"
```

Result: 1151/1151 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecovery"
```

Result: 1989/1989 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore
```

Result: 9183/9183 passed.

## 4. Helper Count

After this slice, `rg -n "static bool Is[A-Za-z0-9_]+CardNo\\(" src/Riftbound.Engine src/Riftbound.CardCatalog src/Riftbound.Contracts` reports 0 helpers.

## 5. Non-Closure Statement

This evidence does not close Teemo full official behavior, full legend-action official breadth, standby replacement breadth, hidden-info / random-zone breadth, card matrix full-official, frontend final validation or READY.
