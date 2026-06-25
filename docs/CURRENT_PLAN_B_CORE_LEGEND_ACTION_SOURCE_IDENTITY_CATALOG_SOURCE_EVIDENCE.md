# Plan B Core Legend Action Source Identity Catalog Source Evidence

日期：2026-06-25
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

This file records the concrete evidence for removing duplicated Core legend-action source card-number helpers and routing those checks through the existing `TryGetLegendAbility` source table.

## 1. Runtime Evidence

- `CoreRuleEngine.ControllerHasAzirLegend` now calls `LegendCardHasAbility(legendState.CardNo, AzirLegendAbilityId)`.
- `CoreRuleEngine.ControllerHasEzrealLegend` now calls `LegendCardHasAbility(cardObject.CardNo, EzrealLegendAbilityId)`.
- Core `HasTeemoStandbyHidePermission` now calls `LegendCardHasAbility(legendState.CardNo, TeemoLegendAbilityId)`.
- `CoreRuleEngine.TryGetExhaustedIreliaLegend` now calls `LegendCardHasAbility(candidate.CardNo, IreliaLegendAbilityId)`.
- `LegendCardHasAbility` resolves the ability through `TryGetLegendAbility` and checks the ability's `SourceCardNos`.
- The previous `IsAzirLegendCardNo`, `IsEzrealLegendCardNo`, `IsTeemoLegendCardNo`, and `IsIreliaLegendCardNo` helpers were deleted.
- Existing events, prompts, payment semantics, and snapshot shape are unchanged.

## 2. Test Evidence

Focused test file:

- `tests/Riftbound.ConformanceTests/LegendActionSourceIdentityGuardTests.cs`

Coverage:

- `CoreLegendActionSourceIdentityDoesNotUseDuplicatedCardNumberHelpers` blocks reintroducing `IsAzirLegendCardNo`, `IsEzrealLegendCardNo`, `IsTeemoLegendCardNo`, and `IsIreliaLegendCardNo`.
- The same guard requires `LegendCardHasAbility`, the four ability ids, and `TryGetLegendAbility`.
- Adjacent tests cover Azir, LegendAct / LegendAction prompt paths, armament, Sand Soldier, GameHub and full-game representatives.

## 3. Verification

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~CoreLegendActionSourceIdentityDoesNotUseDuplicatedCardNumberHelpers"
```

Result: failed before implementation on `IsEzrealLegendCardNo` in the first slice and `IsAzirLegendCardNo` in the follow-up slice, then 1/1 passed after each implementation.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Azir|FullyQualifiedName~LegendAction|FullyQualifiedName~LegendAct|FullyQualifiedName~Armament|FullyQualifiedName~SandSoldier|FullyQualifiedName~GameHub|FullyQualifiedName~FullGameEndToEnd"
```

Result: 384/384 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"
```

Result: 1989/1989 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj
```

Result: 8580/8580 passed.

## 4. Helper Count

After this slice, `rg -n "private static bool Is.*CardNo\\(" src/Riftbound.Engine src/Riftbound.CardCatalog src/Riftbound.Contracts` reports 18 total helpers, all in `CoreRuleEngine.cs`.

## 5. Non-Closure Statement

This evidence does not close complete Azir, Ezreal, Teemo, or Irelia official behavior, full legend-action official breadth, full legend-action data modeling, remaining Core legend helper migration, card matrix full-official, frontend final validation or READY.
