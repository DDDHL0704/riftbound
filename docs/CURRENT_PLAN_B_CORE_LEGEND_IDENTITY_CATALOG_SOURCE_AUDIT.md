# Plan B Core Legend Identity Catalog Source Audit

日期：2026-06-25
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文件记录 Plan B 小切片：把 `CoreRuleEngine` 中 Rengar / Leona / Sivir / Jhin / Ahri / Lucian / Master Yi level / Draven 传奇来源身份识别从独立 `Is*LegendCardNo` helper 改为统一 `LegendCardHasIdentity(cardNo, identityId)` 查询。该切片只迁移来源身份表，不改变触发窗口、目标选择、费用、横置/重置状态、战斗结算、事件 payload 或快照语义。

## 1. Scope

Changed:

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/LegendActionSourceIdentityGuardTests.cs`
- `docs/CURRENT_PLAN_B_CORE_LEGEND_IDENTITY_CATALOG_SOURCE_AUDIT.md`
- `docs/CURRENT_PLAN_B_CORE_LEGEND_IDENTITY_CATALOG_SOURCE_EVIDENCE.md`
- `docs/rules-evidence-index.md`

Not changed:

- official card catalog JSON
- Rengar / Leona / Sivir / Jhin / Ahri / Lucian / Master Yi / Draven runtime effect semantics
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
| Source identity consumes a shared data definition | `TryGetLegendIdentity` returns `LegendIdentityDefinition` rows with source card numbers, and `LegendCardHasIdentity` is the only consumer for this slice | Accepted |
| Hidden-info / recovery boundary | `MatchRecovery` remains green | Accepted |
| Full remaining legend helper migration | Sett / Vi / Vex / Renata / Rek'Sai / Ivern / LeBlanc / Rumble / Jinx / powerful-unit-rune helpers remain residual | Residual, no full-official claim |

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

Adjacent:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Rengar|FullyQualifiedName~Leona|FullyQualifiedName~Sivir|FullyQualifiedName~Jhin|FullyQualifiedName~LegendAction|FullyQualifiedName~FullGameEndToEnd"
```

Result: 143/143 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Ahri|FullyQualifiedName~Lucian|FullyQualifiedName~MasterYi|FullyQualifiedName~Draven|FullyQualifiedName~LegendStatic|FullyQualifiedName~BattleDamageAssignment|FullyQualifiedName~FullGameEndToEnd"
```

Result: 154/154 passed.

Hidden-info / recovery boundary:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"
```

Result: 1989/1989 passed.

Full backend conformance:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj
```

Result: 8582/8582 passed.

## 4. Residual Risks

- `TryGetLegendIdentity` still lives inside `CoreRuleEngine`; the complete long-term catalog extraction remains open.
- This does not broaden official Rengar / Leona / Sivir / Jhin / Ahri / Lucian / Master Yi / Draven behavior beyond already implemented representative paths.
- This does not remove the remaining 10 Core `private static bool Is*CardNo(...)` helpers.
- Project remains **NOT READY**.
