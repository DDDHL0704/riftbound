# Plan B Core Legend Identity Catalog Source Audit

日期：2026-06-25
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文件记录 Plan B 小切片：把 `CoreRuleEngine` 中 Rengar / Leona / Sivir / Jhin / Ahri / Lucian / Master Yi level / Draven / Sett / Vi / Vex / Renata / Rek'Sai / Ivern / LeBlanc / Rumble / Jinx / powerful-unit-rune 传奇来源身份识别从独立 `Is*LegendCardNo` helper 改为统一 `LegendCardHasIdentity(cardNo, identityId)` 查询。该切片只迁移来源身份表，不改变触发窗口、目标选择、费用、横置/重置状态、战斗结算、事件 payload 或快照语义。

## 1. Scope

Changed:

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/LegendActionSourceIdentityGuardTests.cs`
- `docs/CURRENT_PLAN_B_CORE_LEGEND_IDENTITY_CATALOG_SOURCE_AUDIT.md`
- `docs/CURRENT_PLAN_B_CORE_LEGEND_IDENTITY_CATALOG_SOURCE_EVIDENCE.md`
- `docs/rules-evidence-index.md`

Not changed:

- official card catalog JSON
- Rengar / Leona / Sivir / Jhin / Ahri / Lucian / Master Yi / Draven / Sett / Vi / Vex / Renata / Rek'Sai / Ivern / LeBlanc / Rumble / Jinx / powerful-unit-rune runtime effect semantics
- control / legacy-owner source checks
- prompt or snapshot contracts
- frontend runtime

## 2. Acceptance Review

| Requirement | Evidence | Verdict |
|---|---|---|
| Rengar legend source identity no longer has a duplicated card-number helper | `IsRengarLegendCardNo` was removed; `ControllerHasRengarLegend` and `TryGetRengarLegend` now check `LegendCardHasIdentity(..., RengarLegendIdentityId)` | Accepted |
| Leona legend source identity no longer has a duplicated card-number helper | `IsLeonaLegendCardNo` was removed; `ControllerHasLeonaLegend` and `TryGetLeonaLegend` now check `LegendCardHasIdentity(..., LeonaLegendIdentityId)` | Accepted |
| Sivir legend source identity no longer has a duplicated card-number helper | `IsSivirLegendCardNo` was removed; `TryGetSivirLegend` now checks `LegendCardHasIdentity(..., SivirLegendIdentityId)` | Accepted |
| Jhin legend source identity no longer has a duplicated card-number helper | `IsJhinLegendCardNo` was removed; `ControllerHasJhinLegend` now checks `LegendCardHasIdentity(..., JhinLegendIdentityId)` | Accepted |
| Ahri legend source identity no longer has a duplicated card-number helper | `IsAhriLegendCardNo` was removed; `ResolveAhriLegendAttackPowerPenalty` now checks `LegendCardHasIdentity(..., AhriLegendIdentityId)` | Accepted |
| Lucian legend source identity no longer has a duplicated card-number helper | `IsLucianLegendCardNo` was removed; `CountLucianLegendEquipmentAssaultBonus` now checks `LegendCardHasIdentity(..., LucianLegendIdentityId)` | Accepted |
| Master Yi level legend source identity no longer has a duplicated card-number helper | `IsMasterYiLevelLegendCardNo` was removed; `ControllerHasMasterYiLevelLegend` now checks `LegendCardHasIdentity(..., MasterYiLevelLegendIdentityId)` | Accepted |
| Draven legend source identity no longer has a duplicated card-number helper | `IsDravenLegendCardNo` was removed; `TryGetDravenLegendCardNo` now checks `LegendCardHasIdentity(..., DravenLegendIdentityId)` | Accepted |
| Sett legend source identity no longer has a duplicated card-number helper | `IsSettLegendCardNo` was removed; `TryGetExhaustedSettLegend` and `TryGetActiveSettLegend` now check `LegendCardHasIdentity(..., SettLegendIdentityId)` | Accepted |
| Vi legend source identity no longer has a duplicated card-number helper | `IsViLegendCardNo` was removed; `TryGetActiveViLegend` now checks `LegendCardHasIdentity(..., ViLegendIdentityId)` | Accepted |
| Vex legend source identity no longer has a duplicated card-number helper | `IsVexLegendCardNo` was removed; `TryGetActiveVexLegend` now checks `LegendCardHasIdentity(..., VexLegendIdentityId)` | Accepted |
| Renata legend source identity no longer has a duplicated card-number helper | `IsRenataLegendCardNo` was removed; `TryGetActiveRenataLegend` now checks `LegendCardHasIdentity(..., RenataLegendIdentityId)` | Accepted |
| Rek'Sai legend source identity no longer has a duplicated card-number helper | `IsReksaiLegendCardNo` was removed; `TryGetActiveReksaiLegend` now checks `LegendCardHasIdentity(..., ReksaiLegendIdentityId)` | Accepted |
| Ivern legend source identity no longer has a duplicated card-number helper | `IsIvernLegendCardNo` was removed; `TryGetActiveIvernLegend` now checks `LegendCardHasIdentity(..., IvernLegendIdentityId)` | Accepted |
| LeBlanc legend source identity no longer has a duplicated card-number helper | `IsLeblancLegendCardNo` was removed; `TryGetActiveLeblancLegend` now checks `LegendCardHasIdentity(..., LeblancLegendIdentityId)` | Accepted |
| Rumble legend source identity no longer has a duplicated card-number helper | `IsRumbleLegendCardNo` was removed; Rumble mechanical static check now calls `LegendCardHasIdentity(..., RumbleLegendIdentityId)` | Accepted |
| Jinx legend source identity no longer has a duplicated card-number helper | `IsJinxLegendCardNo` was removed; `TryGetJinxTurnStartDrawCardNo` now checks `LegendCardHasIdentity(..., JinxLegendIdentityId)` | Accepted |
| Powerful-unit rune legend source identity no longer has a duplicated card-number helper | `IsPowerfulUnitRuneLegendCardNo` was removed; `ResolvePowerfulUnitPlayedRuneLegendTriggers` now checks `LegendCardHasIdentity(..., PowerfulUnitRuneLegendIdentityId)` | Accepted |
| Source identity consumes a shared data definition | `TryGetLegendIdentity` returns `LegendIdentityDefinition` rows with source card numbers, and `LegendCardHasIdentity` is the only consumer for this slice | Accepted |
| Hidden-info / recovery boundary | `MatchRecovery` remains green | Accepted |
| Full Core `Is*CardNo` helper migration | `rg -n "private static bool Is.*CardNo\\(" src/Riftbound.Engine src/Riftbound.CardCatalog src/Riftbound.Contracts` returns no matches | Accepted for Core helper-removal scope, no full-official claim |

## 3. Verification

Focused:

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

Adjacent:

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

Hidden-info / recovery boundary:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"
```

Result: 1989/1989 passed.

Full backend conformance:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj
```

Result: 8584/8584 passed.

## 4. Residual Risks

- `TryGetLegendIdentity` still lives inside `CoreRuleEngine`; the complete long-term catalog extraction remains open.
- This does not broaden official Rengar / Leona / Sivir / Jhin / Ahri / Lucian / Master Yi / Draven / Sett / Vi / Vex / Renata / Rek'Sai / Ivern / LeBlanc / Rumble / Jinx / Volibear / Fiora behavior beyond already implemented representative paths.
- This does not move the full `TryGetLegendIdentity` source table out of `CoreRuleEngine`.
- Project remains **NOT READY**.
