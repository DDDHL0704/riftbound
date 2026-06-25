# Plan B Core Legend Identity Catalog Source Evidence

日期：2026-06-25
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

This file records concrete evidence for removing duplicated Core legend identity card-number helpers for Rengar / Leona / Sivir / Jhin and routing those checks through a shared identity data definition.

## 1. Runtime Evidence

- `CoreRuleEngine.TryGetLegendIdentity` now defines source card-number rows for `RengarLegendIdentityId`, `LeonaLegendIdentityId`, `SivirLegendIdentityId`, and `JhinLegendIdentityId`.
- `CoreRuleEngine.LegendCardHasIdentity` resolves the identity through `TryGetLegendIdentity` and checks `LegendIdentityDefinition.SourceCardNos`.
- `CoreRuleEngine.ControllerHasRengarLegend` and `CoreRuleEngine.TryGetRengarLegend` now call `LegendCardHasIdentity(..., RengarLegendIdentityId)`.
- `CoreRuleEngine.ControllerHasLeonaLegend` and `CoreRuleEngine.TryGetLeonaLegend` now call `LegendCardHasIdentity(..., LeonaLegendIdentityId)`.
- `CoreRuleEngine.TryGetSivirLegend` now calls `LegendCardHasIdentity(..., SivirLegendIdentityId)`.
- `CoreRuleEngine.ControllerHasJhinLegend` now calls `LegendCardHasIdentity(..., JhinLegendIdentityId)`.
- The previous `IsRengarLegendCardNo`, `IsLeonaLegendCardNo`, `IsSivirLegendCardNo`, and `IsJhinLegendCardNo` helpers were deleted.
- Existing source-control checks, target extraction, trigger resolution, events, prompts, and snapshot shape are unchanged.

## 2. Test Evidence

Focused test file:

- `tests/Riftbound.ConformanceTests/LegendActionSourceIdentityGuardTests.cs`

Coverage:

- `CoreLegendIdentitySourceDoesNotUseDuplicatedCardNumberHelpers` blocks reintroducing `IsRengarLegendCardNo`, `IsLeonaLegendCardNo`, `IsSivirLegendCardNo`, and `IsJhinLegendCardNo`.
- The same guard requires `LegendCardHasIdentity`, the four identity ids, and `TryGetLegendIdentity`.
- Adjacent tests cover Rengar, Leona, Sivir, Jhin, LegendAction, and full-game representatives.

## 3. Verification

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~CoreLegendIdentitySourceDoesNotUseDuplicatedCardNumberHelpers"
```

Result: failed before implementation on `IsRengarLegendCardNo`, then 1/1 passed after implementation.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Rengar|FullyQualifiedName~Leona|FullyQualifiedName~Sivir|FullyQualifiedName~Jhin|FullyQualifiedName~LegendAction|FullyQualifiedName~FullGameEndToEnd"
```

Result: 143/143 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"
```

Result: 1989/1989 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj
```

Result: 8581/8581 passed.

## 4. Helper Count

After this slice, `rg -n "private static bool Is.*CardNo\\(" src/Riftbound.Engine src/Riftbound.CardCatalog src/Riftbound.Contracts` reports 14 total helpers, all in `CoreRuleEngine.cs`.

## 5. Non-Closure Statement

This evidence does not close complete Rengar, Leona, Sivir, or Jhin official behavior, full legend identity data modeling, remaining Core legend helper migration, card matrix full-official, frontend final validation or READY.
