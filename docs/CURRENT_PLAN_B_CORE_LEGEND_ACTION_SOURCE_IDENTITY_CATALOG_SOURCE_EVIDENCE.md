# Plan B Core Legend Action Source Identity Catalog Source Evidence

日期：2026-06-25
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

This file records the concrete evidence for removing duplicated Core legend-action source card-number helpers and routing those checks through the existing `TryGetLegendAbility` source table.

## 2026-06-28 Azir / Lillia Source-Group Evidence

- `LegendActionAbilityCatalog` exposes shared source-card groups for Azir's Sand Soldier legend action and Lillia's Faerie legend action.
- `CoreRuleEngine.TryGetLegendAbility` and `MatchSession.ImplementedLegendActionAbilities` both read those two source groups through `LegendActionAbilityCatalog.SourceCardNosForAbility(...)`.
- `CoreRuleEngine.ControllerHasAzirLegend` uses `LegendActionAbilityCatalog.AzirLegendAbilityId`.
- `LegendActionSourceIdentityGuardTests.AzirAndLilliaLegendActionSourceGroupsUseSharedCatalog` covers the expected source-card rows, positive/negative matching, and source guards that block reintroducing duplicated Azir / Lillia source-card arrays in Core or MatchSession.
- Validation passed: focused guard 1/1; LegendActionSourceIdentity / Azir / Lillia / LegendAct / LegendAction / SandSoldier / Faerie representatives 168/168; LegendAction / LegendAct / Azir / Lillia / SandSoldier / Faerie / FullGameEndToEnd / GameHubJoin / CardCatalogBaseline / MatchRecovery adjacent 2766/2766; backend full 8870/8870.

## 2026-06-28 Reaction / Dynamic Source-Group Evidence

- `LegendActionAbilityCatalog` exposes shared source-card groups for Darius, Diana, Kai'Sa, Ornn, Ezreal, Irelia, and Teemo legend actions.
- `CoreRuleEngine.TryGetLegendAbility` and `MatchSession.ImplementedLegendActionAbilities` both read those seven source groups through `LegendActionAbilityCatalog.SourceCardNosForAbility(...)`.
- `LegendActionSourceIdentityGuardTests.ReactionAndDynamicLegendActionSourceGroupsUseSharedCatalog` covers exact source-card rows, positive/negative matching, and source guards that block reintroducing duplicated Darius / Diana / Kai'Sa / Ornn / Ezreal / Irelia / Teemo source-card arrays in Core or MatchSession.
- Validation passed: focused guard 1/1; LegendActionSourceIdentity / Darius / Diana / Kaisa / Ornn / Ezreal / Irelia / Teemo / LegendAct / LegendAction representatives 264/264; LegendAction / LegendAct / Darius / Diana / Kaisa / Ornn / Ezreal / Irelia / Teemo / FullGameEndToEnd / GameHubJoin / CardCatalogBaseline / MatchRecovery adjacent 2826/2826; backend full 8871/8871.

## 2026-06-28 Main Legend Source-Group Evidence

- `LegendActionAbilityCatalog` exposes shared source-card groups for Yasuo, Lee Sin, Poppy, Viktor, Miss Fortune, Kha'Zix, Pyke, and Jax legend actions.
- Kha'Zix boon/move ability ids and Jax attach/reattach ability ids intentionally share their respective source-card groups through separate catalog rows.
- `CoreRuleEngine.TryGetLegendAbility` and `MatchSession.ImplementedLegendActionAbilities` both read those source groups through `LegendActionAbilityCatalog.SourceCardNosForAbility(...)`.
- `LegendActionSourceIdentityGuardTests.MainLegendActionSourceGroupsUseSharedCatalog` covers exact source-card rows, positive/negative matching, and source guards that block reintroducing duplicated main legend-action source-card arrays in Core or MatchSession.
- Validation passed: focused guard 1/1; LegendActionSourceIdentity / Yasuo / LeeSin / Poppy / Viktor / MissFortune / Khazix / Pyke / Jax / LegendAct / LegendAction representatives 178/178; LegendAction / LegendAct / Yasuo / LeeSin / Poppy / Viktor / MissFortune / Khazix / Pyke / Jax / FullGameEndToEnd / GameHubJoin / CardCatalogBaseline / MatchRecovery adjacent 2765/2765; backend full 8872/8872.

## 1. Runtime Evidence

- `CoreRuleEngine.ControllerHasAzirLegend` now calls `LegendCardHasAbility(legendState.CardNo, AzirLegendAbilityId)`.
- `CoreRuleEngine.ControllerHasEzrealLegend` now calls `LegendCardHasAbility(cardObject.CardNo, EzrealLegendAbilityId)`.
- Core `HasTeemoStandbyHidePermission` now calls `LegendCardHasAbility(legendState.CardNo, TeemoLegendAbilityId)`.
- The Irelia reaction ability source row still resolves through `LegendCardHasAbility(cardNo, IreliaLegendAbilityId)` and `TryGetLegendAbility`; the separate Irelia conquest ready-self trigger was later moved to the legend-conquest `TriggerSpec` path.
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
