# Plan B Core Legend Identity Catalog Source Evidence

日期：2026-06-27
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

This file records concrete evidence for removing duplicated Core legend identity card-number helpers and direct source comparisons for Rengar / Leona / Sivir / Jhin / Ahri / Lucian / Master Yi level / Draven / Garen intro / Lux intro / Sett / Vi / Vex / Renata / Rek'Sai / Ivern / LeBlanc / Rumble / Jinx / powerful-unit-rune / Annie and routing those checks through a shared identity data definition.

## 2026-06-28 Source-Table Extraction Evidence

- `LegendIdentityCatalog` now owns the exact source-card rows for Ahri / Lucian / Master Yi level / Draven / Garen intro / Lux intro / Annie / Jinx / Rumble / powerful-unit-rune / Sett / Vi / Vex / Renata / Rek'Sai / Ivern / LeBlanc / Rengar / Leona / Sivir / Jhin identity ids.
- `CoreRuleEngine.TryGetLegendIdentity` now builds `LegendIdentityDefinition` from `LegendIdentityCatalog.SourceCardNosForIdentity(...)` instead of keeping source arrays in Core.
- Rengar / Leona / Draven / Garen / Jinx fallback event payload defaults now read `LegendIdentityCatalog.PrimarySourceCardNoForIdentity(...)`.
- `LegendActionSourceIdentityGuardTests.CoreLegendIdentitySourceRowsUseSharedCatalog` covers exact source rows, positive/negative identity matching, primary fallback, and source guards that block reintroducing the old Core identity source arrays.
- Validation passed: focused guard 1/1; LegendActionSourceIdentity / Rengar / Leona / Sivir / Jhin / Ahri / Lucian / MasterYi / Draven / Garen / Lux / Sett / ViLegend / Vex / Renata / Reksai / Ivern / Leblanc / Rumble / Jinx / Volibear / Fiora / Annie / PowerfulUnit / RuneLegend representatives 446/446; same set plus FullGameEndToEnd / GameHubJoin / CardCatalogBaseline / MatchRecovery adjacent 2954/2954; backend full 8873/8873.

## 1. Runtime Evidence

- `LegendIdentityCatalog` now defines source card-number rows for `RengarLegendIdentityId`, `LeonaLegendIdentityId`, `SivirLegendIdentityId`, `JhinLegendIdentityId`, `AhriLegendIdentityId`, `LucianLegendIdentityId`, `MasterYiLevelLegendIdentityId`, `DravenLegendIdentityId`, `GarenIntroLegendIdentityId`, `LuxIntroLegendIdentityId`, `SettLegendIdentityId`, `ViLegendIdentityId`, `VexLegendIdentityId`, `RenataLegendIdentityId`, `ReksaiLegendIdentityId`, `IvernLegendIdentityId`, `LeblancLegendIdentityId`, `RumbleLegendIdentityId`, `JinxLegendIdentityId`, `PowerfulUnitRuneLegendIdentityId`, and `AnnieLegendIdentityId`.
- `CoreRuleEngine.TryGetLegendIdentity` now reads those rows from `LegendIdentityCatalog.SourceCardNosForIdentity(...)`.
- `CoreRuleEngine.LegendCardHasIdentity` resolves the identity through `TryGetLegendIdentity` and checks `LegendIdentityDefinition.SourceCardNos`.
- `CoreRuleEngine.ControllerHasRengarLegend` and `CoreRuleEngine.TryGetRengarLegend` now call `LegendCardHasIdentity(..., RengarLegendIdentityId)`.
- `CoreRuleEngine.ControllerHasLeonaLegend` and `CoreRuleEngine.TryGetLeonaLegend` now call `LegendCardHasIdentity(..., LeonaLegendIdentityId)`.
- `CoreRuleEngine.TryGetSivirLegend` now calls `LegendCardHasIdentity(..., SivirLegendIdentityId)`.
- `CoreRuleEngine.ControllerHasJhinLegend` now calls `LegendCardHasIdentity(..., JhinLegendIdentityId)`.
- `CoreRuleEngine.ResolveAhriLegendAttackPowerPenalty` now calls `LegendCardHasIdentity(..., AhriLegendIdentityId)`.
- `CoreRuleEngine.CountLucianLegendEquipmentAssaultBonus` now calls `LegendCardHasIdentity(..., LucianLegendIdentityId)`.
- `CoreRuleEngine.ControllerHasMasterYiLevelLegend` now calls `LegendCardHasIdentity(..., MasterYiLevelLegendIdentityId)`.
- `CoreRuleEngine.TryGetDravenLegendCardNo` now calls `LegendCardHasIdentity(..., DravenLegendIdentityId)`.
- `CoreRuleEngine.TryGetGarenIntroLegendCardNo` now calls `LegendCardHasIdentity(..., GarenIntroLegendIdentityId)` instead of comparing `legendState.CardNo` directly with `GarenIntroLegendCardNo`.
- `CoreRuleEngine.TryGetLuxHighCostSpellDrawCardNo` now calls `LegendCardHasIdentity(..., LuxIntroLegendIdentityId)` instead of comparing `legendState.CardNo` directly with `LuxIntroLegendCardNo`.
- `CoreRuleEngine.TryGetExhaustedSettLegend` and `CoreRuleEngine.TryGetActiveSettLegend` now call `LegendCardHasIdentity(..., SettLegendIdentityId)`.
- `CoreRuleEngine.TryGetActiveViLegend` now calls `LegendCardHasIdentity(..., ViLegendIdentityId)`.
- `CoreRuleEngine.TryGetActiveVexLegend` now calls `LegendCardHasIdentity(..., VexLegendIdentityId)`.
- `CoreRuleEngine.TryGetActiveRenataLegend` now calls `LegendCardHasIdentity(..., RenataLegendIdentityId)`.
- `CoreRuleEngine.TryGetActiveReksaiLegend` now calls `LegendCardHasIdentity(..., ReksaiLegendIdentityId)`.
- `CoreRuleEngine.TryGetActiveIvernLegend` now calls `LegendCardHasIdentity(..., IvernLegendIdentityId)`.
- `CoreRuleEngine.TryGetActiveLeblancLegend` now calls `LegendCardHasIdentity(..., LeblancLegendIdentityId)`.
- Rumble mechanical static check now calls `LegendCardHasIdentity(..., RumbleLegendIdentityId)`.
- `CoreRuleEngine.TryGetJinxTurnStartDrawCardNo` now calls `LegendCardHasIdentity(..., JinxLegendIdentityId)`.
- `CoreRuleEngine.ResolvePowerfulUnitPlayedRuneLegendTriggers` now calls `LegendCardHasIdentity(..., PowerfulUnitRuneLegendIdentityId)`.
- `CoreRuleEngine.ReadyRunesForAnnieAtTurnEnd` now calls `LegendCardHasIdentity(..., AnnieLegendIdentityId)` instead of comparing `legendState.CardNo` directly with `AnnieIntroLegendCardNo`.
- The previous `IsRengarLegendCardNo`, `IsLeonaLegendCardNo`, `IsSivirLegendCardNo`, `IsJhinLegendCardNo`, `IsAhriLegendCardNo`, `IsLucianLegendCardNo`, `IsMasterYiLevelLegendCardNo`, `IsDravenLegendCardNo`, `IsSettLegendCardNo`, `IsViLegendCardNo`, `IsVexLegendCardNo`, `IsRenataLegendCardNo`, `IsReksaiLegendCardNo`, `IsIvernLegendCardNo`, `IsLeblancLegendCardNo`, `IsRumbleLegendCardNo`, `IsJinxLegendCardNo`, and `IsPowerfulUnitRuneLegendCardNo` helpers were deleted.
- Existing source-control checks, target extraction, trigger resolution, events, prompts, and snapshot shape are unchanged.

## 2. Test Evidence

Focused test file:

- `tests/Riftbound.ConformanceTests/LegendActionSourceIdentityGuardTests.cs`

Coverage:

- `CoreLegendIdentitySourceDoesNotUseDuplicatedCardNumberHelpers` blocks reintroducing `IsRengarLegendCardNo`, `IsLeonaLegendCardNo`, `IsSivirLegendCardNo`, and `IsJhinLegendCardNo`.
- The same guard requires `LegendCardHasIdentity`, the four identity ids, and `TryGetLegendIdentity`.
- `CoreLegendStaticIdentitySourceDoesNotUseDuplicatedCardNumberHelpers` blocks reintroducing `IsAhriLegendCardNo`, `IsLucianLegendCardNo`, `IsMasterYiLevelLegendCardNo`, and `IsDravenLegendCardNo`.
- The same guard requires the four additional identity ids, `LegendCardHasIdentity`, and `TryGetLegendIdentity`.
- `CoreActiveLegendIdentitySourceDoesNotUseDuplicatedCardNumberHelpers` blocks reintroducing `IsSettLegendCardNo`, `IsViLegendCardNo`, `IsVexLegendCardNo`, `IsRenataLegendCardNo`, `IsReksaiLegendCardNo`, `IsIvernLegendCardNo`, and `IsLeblancLegendCardNo`.
- The same guard requires the seven active legend identity ids, `LegendCardHasIdentity`, and `TryGetLegendIdentity`.
- `CoreRemainingLegendIdentitySourceDoesNotUseDuplicatedCardNumberHelpers` blocks reintroducing `IsRumbleLegendCardNo`, `IsJinxLegendCardNo`, and `IsPowerfulUnitRuneLegendCardNo`.
- The same guard requires the three final identity ids, `LegendCardHasIdentity`, and `TryGetLegendIdentity`.
- `CoreAnnieTurnEndRuneReadySourceUsesLegendIdentity` blocks reintroducing the direct `string.Equals(legendState.CardNo, AnnieIntroLegendCardNo, ...)` source branch and requires `AnnieLegendIdentityId`, `LegendCardHasIdentity`, and `TryGetLegendIdentity`.
- `CoreIntroLegendSourcesUseLegendIdentity` blocks reintroducing the direct `string.Equals(legendState.CardNo, GarenIntroLegendCardNo, ...)` and `string.Equals(legendState.CardNo, LuxIntroLegendCardNo, ...)` source branches and requires `GarenIntroLegendIdentityId`, `LuxIntroLegendIdentityId`, `LegendCardHasIdentity`, and `TryGetLegendIdentity`.
- Adjacent tests cover Rengar, Leona, Sivir, Jhin, LegendAction, and full-game representatives.
- Follow-up adjacent tests cover Ahri, Lucian, Master Yi, Draven, LegendStatic, BattleDamageAssignment, and full-game representatives.
- Active-legend adjacent tests cover Sett, Vi, Vex, Renata, Rek'Sai, Ivern, LeBlanc, battlefield-conquer, and full-game representatives.
- Final adjacent tests cover Rumble, Jinx, Volibear, Fiora, powerful-unit rune, turn-start, and full-game representatives.

## 3. Verification

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~CoreLegendIdentitySourceDoesNotUseDuplicatedCardNumberHelpers"
```

Result: failed before implementation on `IsRengarLegendCardNo`, then 1/1 passed after implementation.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~CoreLegendStaticIdentitySourceDoesNotUseDuplicatedCardNumberHelpers"
```

Result: failed before implementation on `IsAhriLegendCardNo`, then 1/1 passed after implementation.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~CoreActiveLegendIdentitySourceDoesNotUseDuplicatedCardNumberHelpers"
```

Result: failed before implementation on `IsSettLegendCardNo`, then 1/1 passed after implementation.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~CoreRemainingLegendIdentitySourceDoesNotUseDuplicatedCardNumberHelpers"
```

Result: failed before implementation on `IsRumbleLegendCardNo`, then 1/1 passed after implementation.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~CoreAnnieTurnEndRuneReadySourceUsesLegendIdentity|FullyQualifiedName~P79LegendTriggerAnnieReadiesTwoRunesAtTurnEnd" --nologo
```

Result: failed before implementation on direct `AnnieIntroLegendCardNo` source comparison, then 2/2 passed after implementation.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~CoreIntroLegendSourcesUseLegendIdentity" --nologo
```

Result: failed before implementation on direct `GarenIntroLegendCardNo` source comparison, then 1/1 passed after implementation.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Annie|FullyQualifiedName~LegendActionSourceIdentityGuardTests|FullyQualifiedName~TurnEnd|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2086/2086 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Garen|FullyQualifiedName~Lux|FullyQualifiedName~LegendActionSourceIdentityGuardTests|FullyQualifiedName~HighCostSpell|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2100/2100 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Rengar|FullyQualifiedName~Leona|FullyQualifiedName~Sivir|FullyQualifiedName~Jhin|FullyQualifiedName~LegendAction|FullyQualifiedName~FullGameEndToEnd"
```

Result: 143/143 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Ahri|FullyQualifiedName~Lucian|FullyQualifiedName~MasterYi|FullyQualifiedName~Draven|FullyQualifiedName~LegendStatic|FullyQualifiedName~BattleDamageAssignment|FullyQualifiedName~FullGameEndToEnd"
```

Result: 154/154 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Sett|FullyQualifiedName~ViLegend|FullyQualifiedName~Vex|FullyQualifiedName~Renata|FullyQualifiedName~Reksai|FullyQualifiedName~Ivern|FullyQualifiedName~Leblanc|FullyQualifiedName~BattlefieldConquer|FullyQualifiedName~FullGameEndToEnd"
```

Result: 270/270 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Rumble|FullyQualifiedName~Jinx|FullyQualifiedName~Volibear|FullyQualifiedName~Fiora|FullyQualifiedName~PowerfulUnit|FullyQualifiedName~RuneLegend|FullyQualifiedName~TurnStart|FullyQualifiedName~FullGameEndToEnd"
```

Result: 97/97 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"
```

Result: 1989/1989 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj
```

Result: 8584/8584 passed before the Annie follow-up; 8766/8766 passed after the Annie follow-up; 8767/8767 passed after the Garen/Lux intro follow-up; 8873/8873 passed after the source-table extraction follow-up.

## 4. Helper Count

After this slice, `rg -n "private static bool Is.*CardNo\\(" src/Riftbound.Engine src/Riftbound.CardCatalog src/Riftbound.Contracts` reports no helpers.

## 5. Non-Closure Statement

This evidence does not close complete Rengar, Leona, Sivir, Jhin, Ahri, Lucian, Master Yi, Draven, Garen intro, Lux intro, Sett, Vi, Vex, Renata, Rek'Sai, Ivern, LeBlanc, Rumble, Jinx, Volibear, Fiora, or Annie official behavior, full legend identity effect modeling, card matrix full-official, frontend final validation or READY.
