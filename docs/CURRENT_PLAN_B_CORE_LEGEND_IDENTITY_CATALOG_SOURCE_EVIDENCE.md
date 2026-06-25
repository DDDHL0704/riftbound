# Plan B Core Legend Identity Catalog Source Evidence

日期：2026-06-25
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

This file records concrete evidence for removing duplicated Core legend identity card-number helpers for Rengar / Leona / Sivir / Jhin / Ahri / Lucian / Master Yi level / Draven / Sett / Vi / Vex / Renata / Rek'Sai / Ivern / LeBlanc and routing those checks through a shared identity data definition.

## 1. Runtime Evidence

- `CoreRuleEngine.TryGetLegendIdentity` now defines source card-number rows for `RengarLegendIdentityId`, `LeonaLegendIdentityId`, `SivirLegendIdentityId`, `JhinLegendIdentityId`, `AhriLegendIdentityId`, `LucianLegendIdentityId`, `MasterYiLevelLegendIdentityId`, `DravenLegendIdentityId`, `SettLegendIdentityId`, `ViLegendIdentityId`, `VexLegendIdentityId`, `RenataLegendIdentityId`, `ReksaiLegendIdentityId`, `IvernLegendIdentityId`, and `LeblancLegendIdentityId`.
- `CoreRuleEngine.LegendCardHasIdentity` resolves the identity through `TryGetLegendIdentity` and checks `LegendIdentityDefinition.SourceCardNos`.
- `CoreRuleEngine.ControllerHasRengarLegend` and `CoreRuleEngine.TryGetRengarLegend` now call `LegendCardHasIdentity(..., RengarLegendIdentityId)`.
- `CoreRuleEngine.ControllerHasLeonaLegend` and `CoreRuleEngine.TryGetLeonaLegend` now call `LegendCardHasIdentity(..., LeonaLegendIdentityId)`.
- `CoreRuleEngine.TryGetSivirLegend` now calls `LegendCardHasIdentity(..., SivirLegendIdentityId)`.
- `CoreRuleEngine.ControllerHasJhinLegend` now calls `LegendCardHasIdentity(..., JhinLegendIdentityId)`.
- `CoreRuleEngine.ResolveAhriLegendAttackPowerPenalty` now calls `LegendCardHasIdentity(..., AhriLegendIdentityId)`.
- `CoreRuleEngine.CountLucianLegendEquipmentAssaultBonus` now calls `LegendCardHasIdentity(..., LucianLegendIdentityId)`.
- `CoreRuleEngine.ControllerHasMasterYiLevelLegend` now calls `LegendCardHasIdentity(..., MasterYiLevelLegendIdentityId)`.
- `CoreRuleEngine.TryGetDravenLegendCardNo` now calls `LegendCardHasIdentity(..., DravenLegendIdentityId)`.
- `CoreRuleEngine.TryGetExhaustedSettLegend` and `CoreRuleEngine.TryGetActiveSettLegend` now call `LegendCardHasIdentity(..., SettLegendIdentityId)`.
- `CoreRuleEngine.TryGetActiveViLegend` now calls `LegendCardHasIdentity(..., ViLegendIdentityId)`.
- `CoreRuleEngine.TryGetActiveVexLegend` now calls `LegendCardHasIdentity(..., VexLegendIdentityId)`.
- `CoreRuleEngine.TryGetActiveRenataLegend` now calls `LegendCardHasIdentity(..., RenataLegendIdentityId)`.
- `CoreRuleEngine.TryGetActiveReksaiLegend` now calls `LegendCardHasIdentity(..., ReksaiLegendIdentityId)`.
- `CoreRuleEngine.TryGetActiveIvernLegend` now calls `LegendCardHasIdentity(..., IvernLegendIdentityId)`.
- `CoreRuleEngine.TryGetActiveLeblancLegend` now calls `LegendCardHasIdentity(..., LeblancLegendIdentityId)`.
- The previous `IsRengarLegendCardNo`, `IsLeonaLegendCardNo`, `IsSivirLegendCardNo`, `IsJhinLegendCardNo`, `IsAhriLegendCardNo`, `IsLucianLegendCardNo`, `IsMasterYiLevelLegendCardNo`, `IsDravenLegendCardNo`, `IsSettLegendCardNo`, `IsViLegendCardNo`, `IsVexLegendCardNo`, `IsRenataLegendCardNo`, `IsReksaiLegendCardNo`, `IsIvernLegendCardNo`, and `IsLeblancLegendCardNo` helpers were deleted.
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
- Adjacent tests cover Rengar, Leona, Sivir, Jhin, LegendAction, and full-game representatives.
- Follow-up adjacent tests cover Ahri, Lucian, Master Yi, Draven, LegendStatic, BattleDamageAssignment, and full-game representatives.
- Active-legend adjacent tests cover Sett, Vi, Vex, Renata, Rek'Sai, Ivern, LeBlanc, battlefield-conquer, and full-game representatives.

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
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"
```

Result: 1989/1989 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj
```

Result: 8583/8583 passed.

## 4. Helper Count

After this slice, `rg -n "private static bool Is.*CardNo\\(" src/Riftbound.Engine src/Riftbound.CardCatalog src/Riftbound.Contracts` reports 3 total helpers, all in `CoreRuleEngine.cs`.

## 5. Non-Closure Statement

This evidence does not close complete Rengar, Leona, Sivir, Jhin, Ahri, Lucian, Master Yi, Draven, Sett, Vi, Vex, Renata, Rek'Sai, Ivern, or LeBlanc official behavior, full legend identity data modeling, remaining Core legend helper migration, card matrix full-official, frontend final validation or READY.
