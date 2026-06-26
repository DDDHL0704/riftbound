# Plan B Movement Source Identity Catalog Source Audit

日期：2026-06-26
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文件记录 Plan B 小切片：把 Bilgewater Bully / 比尔吉沃特恶霸的 boon-roam movement permission 来源身份，从 `CoreRuleEngine` 与 `MatchSession` prompt 路径里的直接 cardNo 分支迁移到 `CardBehaviorRegistry.IsImplementedUnitWithEffectKind`。该切片只收窄 movement / Roam source identity 硬编码；不关闭完整 Roam timing、完整 movement lifecycle、完整 boon-token family、完整 B2 rule-text keyword layer 或 READY。

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

## Residual Risks

- This does not move the Bilgewater Bully conditional keyword into a full generic rule-text conditional keyword engine; it only removes direct source card-number identity checks from current runtime and prompt paths.
- This does not close complete Roam movement timing, movement task lifecycle, or battle/response interactions.
- Project remains **NOT READY**.
