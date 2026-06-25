# Plan B Core Legend Action Source Identity Catalog Source Evidence

日期：2026-06-25
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

This file records the concrete evidence for removing three duplicated Core legend-action source card-number helpers and routing those checks through the existing `TryGetLegendAbility` source table.

## 1. Runtime Evidence

- `CoreRuleEngine.ControllerHasEzrealLegend` now calls `LegendCardHasAbility(cardObject.CardNo, EzrealLegendAbilityId)`.
- Core `HasTeemoStandbyHidePermission` now calls `LegendCardHasAbility(legendState.CardNo, TeemoLegendAbilityId)`.
- `CoreRuleEngine.TryGetExhaustedIreliaLegend` now calls `LegendCardHasAbility(candidate.CardNo, IreliaLegendAbilityId)`.
- `LegendCardHasAbility` resolves the ability through `TryGetLegendAbility` and checks the ability's `SourceCardNos`.
- The previous `IsEzrealLegendCardNo`, `IsTeemoLegendCardNo`, and `IsIreliaLegendCardNo` helpers were deleted.
- Existing events, prompts, payment semantics, and snapshot shape are unchanged.

## 2. Test Evidence

Focused test file:

- `tests/Riftbound.ConformanceTests/LegendActionSourceIdentityGuardTests.cs`

Coverage:

- `CoreLegendActionSourceIdentityDoesNotUseDuplicatedCardNumberHelpers` blocks reintroducing `IsEzrealLegendCardNo`, `IsTeemoLegendCardNo`, and `IsIreliaLegendCardNo`.
- The same guard requires `LegendCardHasAbility`, the three ability ids, and `TryGetLegendAbility`.
- Adjacent tests cover LegendAct / LegendAction prompt paths, Ezreal, Teemo, Irelia, standby hide/reveal, GameHub and full-game representatives.

## 3. Verification

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~CoreLegendActionSourceIdentityDoesNotUseDuplicatedCardNumberHelpers"
```

Result: failed before implementation on `IsEzrealLegendCardNo`, then 1/1 passed after implementation.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~LegendAction|FullyQualifiedName~LegendAct|FullyQualifiedName~Ezreal|FullyQualifiedName~Teemo|FullyQualifiedName~Irelia|FullyQualifiedName~HideCard|FullyQualifiedName~RevealCard|FullyQualifiedName~GameHub|FullyQualifiedName~FullGameEndToEnd"
```

Result: 544/544 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"
```

Result: 1989/1989 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj
```

Result: 8580/8580 passed.

## 4. Helper Count

After this slice, `rg -n "private static bool Is.*CardNo\\(" src/Riftbound.Engine src/Riftbound.CardCatalog src/Riftbound.Contracts` reports 19 total helpers, all in `CoreRuleEngine.cs`.

## 5. Non-Closure Statement

This evidence does not close complete Ezreal, Teemo, or Irelia official behavior, full legend-action official breadth, full legend-action data modeling, remaining Core legend helper migration, card matrix full-official, frontend final validation or READY.
