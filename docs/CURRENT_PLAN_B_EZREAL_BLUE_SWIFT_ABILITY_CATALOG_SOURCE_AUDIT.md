# Plan B Ezreal Blue Swift Ability Catalog Source Audit

日期：2026-06-25
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文件记录 Plan B 小切片：把 Ezreal blue swift move-to-base stack resolution 的来源卡号识别从 `CoreRuleEngine` 本地硬编码 helper 改为 `P4ActivatedAbilityCatalog` ability source group。该切片只收窄既有 `SFD·082/221` / `SFD·082a/221` / `SFD·082b/221·P` 伊泽瑞尔代表技能的 Core 卡号硬编码，不关闭 full official Ezreal、attack / defense damage trigger、cannot-combat-damage static、完整 swift / reaction timing、矩阵 full-official 或 READY。

## 1. Scope

Changed:

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/EzrealBlueSwiftMoveToBaseActivatedAbilityTests.cs`
- `docs/CURRENT_PLAN_B_EZREAL_BLUE_SWIFT_ABILITY_CATALOG_SOURCE_AUDIT.md`
- `docs/CURRENT_PLAN_B_EZREAL_BLUE_SWIFT_ABILITY_CATALOG_SOURCE_EVIDENCE.md`
- `docs/rules-evidence-index.md`
- `docs/p2-rules-preflight.md`

Not changed:

- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- frontend runtime
- card matrix JSON
- attack / defense damage trigger runtime
- cannot-combat-damage static runtime
- full swift / reaction timing breadth

## 2. Acceptance Review

| Requirement | Evidence | Verdict |
|---|---|---|
| Core stack resolution no longer hardcodes Ezreal alt / promo card Nos | `CoreRuleEngine.cs` no longer contains `IsEzrealBlueSwiftCardNo`, `EzrealBlueSwiftAltCardNo`, or `EzrealBlueSwiftPromoCardNo` | Accepted |
| Resolution source identity uses shared ability catalog | `ResolveEzrealBlueSwiftMoveAbilityStackItem` resolves the ability definition via `P4ActivatedAbilityCatalog.TryGetByEffectKind`, and `TryMoveEzrealBlueSwiftSourceToBase` uses `P4ActivatedAbilityCatalog.IsSourceCardNoForAbility` | Accepted |
| Existing stale-source protection preserved | Stack `CardNo` equality and existing public / controller / zone / battlefield-location checks remain in place before moving the source to base | Accepted |
| Conformance guards the migration | `CoreRuleEngineUsesActivatedAbilityCatalogForEzrealBlueSwiftSourceIdentity` prevents reintroducing the Core helper or direct alt / promo constants | Accepted |
| Full official Ezreal | Attack / defense damage trigger, cannot-combat-damage static, full swift / reaction timing and FAQ breadth remain residual | Residual, no full-official claim |

## 3. Verification

Focused:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~CatalogExposesEzrealBlueSwiftMoveForAllCollectorNumbers|FullyQualifiedName~CoreRuleEngineUsesActivatedAbilityCatalogForEzrealBlueSwiftSourceIdentity|FullyQualifiedName~EzrealCommandPaysBlueCreatesStackAndResolutionMovesSourceToBase|FullyQualifiedName~EzrealStackResolutionNoEffectsWhenSourceLeavesBattlefieldBeforeResolution"
```

Result: 6/6 passed.

Adjacent:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~EzrealBlueSwift|FullyQualifiedName~P4ActivatedAbility|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~PaymentEngineCoverageAuditTests"
```

Result: 1011/1011 passed.

Hidden-info / recovery boundary:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"
```

Result: 1989/1989 passed.

Full backend conformance:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj
```

Result: 8533/8533 passed.

## 4. Residual Risks

- This does not implement Ezreal's attack / defense damage trigger.
- This does not implement Ezreal's cannot-combat-damage static.
- This keeps the existing representative swift timing; full swift / reaction timing breadth remains open.
- The ability source group still lives in `P4ActivatedAbilityCatalog`; this slice only removes the duplicate Core helper and makes resolution consume the existing catalog identity.
- Project remains **NOT READY**.
