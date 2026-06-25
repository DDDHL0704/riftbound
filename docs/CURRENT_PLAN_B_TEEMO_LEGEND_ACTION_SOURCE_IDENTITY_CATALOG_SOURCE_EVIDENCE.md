# Plan B Teemo Legend Action Source Identity Catalog Source Evidence

日期：2026-06-25
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

This file records the concrete evidence for removing the remaining `MatchSession` `private static bool Is*CardNo(...)` helpers from the Teemo legend-action / standby-hide source path.

## 1. Runtime Evidence

- `MatchSession.HasTeemoStandbyHidePermission` still requires a controlled object in the player's legend zone.
- The Teemo legend source check now calls `HasImplementedLegendActionAbility(legendState.CardNo, TeemoLegendAbilityId)`.
- `HasImplementedLegendActionAbility` is backed by `ImplementedLegendActionAbilities()` and matches by exact `AbilityId` plus the ability's source card list.
- The previous `IsTeemoLegendCardNo` helper was deleted.
- The unused `IsImplementedLegendActionCardNo` helper was deleted.
- Existing Teemo legend-action target identity remains backed by `CardBehaviorRegistry.IsImplementedUnitNamed(targetState.CardNo, "提莫")`.

## 2. Test Evidence

Focused test file:

- `tests/Riftbound.ConformanceTests/TeemoLegendActionDomainGuardTests.cs`

Coverage:

- `TeemoLegendActionDomainDoesNotUseDuplicatedCardNumberAllowLists` blocks reintroducing `IsTeemoLegendCardNo`, `IsImplementedLegendActionCardNo`, and the older Teemo unit helper names.
- The same guard requires `HasImplementedLegendActionAbility`, `TeemoLegendAbilityId`, and `CardBehaviorRegistry.IsImplementedUnitNamed`.
- Existing Teemo unit registry positive / negative rows remain green.

## 3. Verification

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~TeemoLegendActionDomainDoesNotUseDuplicatedCardNumberAllowLists"
```

Result: failed before implementation on `IsTeemoLegendCardNo`, then 1/1 passed after implementation.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~TeemoLegendAction|FullyQualifiedName~LegendAction|FullyQualifiedName~LegendAct|FullyQualifiedName~P6LegendAbilityCatalog|FullyQualifiedName~GameHub|FullyQualifiedName~FullGameEndToEnd"
```

Result: 327/327 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"
```

Result: 1989/1989 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj
```

Result: 8579/8579 passed.

## 4. Helper Count

After this slice, `rg -n "private static bool Is.*CardNo\\(" src/Riftbound.Engine src/Riftbound.CardCatalog src/Riftbound.Contracts` reports 22 total helpers, all in `CoreRuleEngine.cs`.

## 5. Non-Closure Statement

This evidence does not close complete Teemo official behavior, full legend-action official breadth, full legend-action data modeling, remaining Core legend helper migration, card matrix full-official, frontend final validation or READY.
