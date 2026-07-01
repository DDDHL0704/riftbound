# Plan B Movement Source Identity Catalog Source Audit

日期：2026-06-26
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文件记录 Plan B 小切片：把 Bilgewater Bully / 比尔吉沃特恶霸的 boon-roam movement permission 来源身份，从 `CoreRuleEngine` 与 `MatchSession` prompt 路径里的直接 cardNo 分支迁移到 `CardBehaviorRegistry.IsImplementedUnitWithEffectKind`。2026-06-30 follow-up 继续把该代表的 runtime effect-kind selector 迁移到 `BehaviorSpec.StaticAuras` / `SOURCE_OBJECT_FILTERED_KEYWORD`。2026-07-01 follow-up 再把引擎消费侧从 kind-specific lookup 迁移到 `StaticAuraSpecRules.IsSourceObjectKeywordStaticAura` scope predicate。该切片只收窄 movement / Roam source identity 与当前 source-object filtered keyword representative 硬编码；不关闭完整 Roam timing、完整 movement lifecycle、完整 boon-token family、完整 B2 rule-text keyword layer 或 READY。

## Scope

Changed:

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/MovementSourceIdentityGuardTests.cs`
- `docs/CURRENT_PLAN_B_MOVEMENT_SOURCE_IDENTITY_CATALOG_SOURCE_AUDIT.md`
- `docs/CURRENT_PLAN_B_MOVEMENT_SOURCE_IDENTITY_CATALOG_SOURCE_EVIDENCE.md`
- `docs/rules-evidence-index.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`

Not changed:

- official card catalog JSON
- card matrix JSON
- protocol shape
- movement timing semantics
- frontend runtime
- `fullOfficial` / READY status

## Acceptance Review

| Requirement | Evidence | Verdict |
|---|---|---|
| Bilgewater Bully boon-roam runtime source identity no longer directly selects by card number | `CoreRuleEngine.HasBilgewaterBullyBoonRoamPermission` calls `CardBehaviorRegistry.IsImplementedUnitWithEffectKind` with `BILGEWATER_BULLY_NO_BOON_ROAM_PLAY_UNIT` instead of comparing `sourceState.CardNo` to `BilgewaterBullyCardNo` | Accepted |
| Bilgewater Bully boon-roam prompt source identity no longer directly selects by card number | `MatchSession.HasBilgewaterBullyBoonPromptRoamPermission` uses the same catalog source effect kind and no longer contains `BilgewaterBullyCardNo` | Accepted |
| Existing boon condition is preserved | both runtime and prompt paths still require `CardObjectTags.Boon` after matching the catalog source identity | Accepted |
| Registry identity is exact enough to reject wrong rows | `MovementSourceIdentityGuardTests` accepts `OGN·125/298` only for `BILGEWATER_BULLY_NO_BOON_ROAM_PLAY_UNIT` and rejects wrong-card / wrong-effect examples | Accepted |
| Bilgewater Bully boon-roam no longer selects by runtime effect kind | `CoreRuleEngine` and `MatchSession` now enumerate `BehaviorSpec.StaticAuras` and filter with `StaticAuraSpecRules.IsSourceObjectKeywordStaticAura` / `SOURCE_OBJECT_FILTERED_KEYWORD` instead of consuming `BILGEWATER_BULLY_NO_BOON_ROAM_PLAY_UNIT` in runtime permission paths | Accepted |
| Existing representative behavior is preserved | Bilgewater / precise-roam / movement adjacent regression remains green | Accepted |
| Full movement and keyword breadth | complete Roam timing, complete movement lifecycle, full boon-token family, and full rule-text keyword layer remain residual | Residual, no READY claim |

## Verification

Starting backend baseline:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8664/8664 passed.

Focused source identity guard:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MovementSourceIdentityGuardTests" --nologo
```

Result after implementation: 4/4 passed.

Adjacent Bilgewater / precise movement regression:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BilgewaterBully|FullyQualifiedName~PreciseRoam|FullyQualifiedName~MoveUnit" --nologo
```

Result: 93/93 passed.

Hidden-info / recovery boundary:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery" --nologo
```

Result: 1989/1989 passed.

Full backend conformance:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8668/8668 passed.

2026-06-30 source-object filtered keyword follow-up:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo --filter "FullyQualifiedName~BilgewaterBullyBoonRoamSourceIdentityUsesSourceObjectFilteredKeywordAura|FullyQualifiedName~BehaviorSpecCatalogParsesStaticAuraSpecsForExistingRepresentatives|FullyQualifiedName~P79BilgewaterBully"
```

Result after implementation: 4/4 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo --filter "FullyQualifiedName~MovementSourceIdentityGuardTests|FullyQualifiedName~BilgewaterBully|FullyQualifiedName~PreciseRoam|FullyQualifiedName~MoveUnit|FullyQualifiedName~CardCatalogBaselineTests|FullyQualifiedName~MatchRecovery"
```

Result: 2393/2393 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo
```

Result: 9026/9026 passed.

## Residual Risks

- This does not close complete source-object filtered keyword official breadth; it only connects the current Bilgewater Bully representative to the shared rule-text keyword aura path.
- This does not close complete Roam movement timing, movement task lifecycle, or battle/response interactions.
- Project remains **NOT READY**.
