# Plan B Battlefield Static Source Identity Catalog Source Audit

日期：2026-06-26
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

2026-06-30 follow-up: this 2026-06-26 source-identity slice has been superseded for the live runtime selector by `docs/CURRENT_PLAN_B_STATIC_SPELL_COST_REDUCTION_BEHAVIOR_FIELDS_AUDIT.md`. Core and MatchSession now derive the Eager Apprentice representative through `StaticSpellCostReductionMana=1` and `StaticSpellCostReductionMinimumManaCost=1` behavior fields rather than the `EAGER_APPRENTICE_SPELL_COST_STATIC_PLAY_UNIT` runtime effect id. The effect id remains row identity data in the catalog, fixtures, and matrix evidence.

本文件记录 Plan B 小切片：把 Eager Apprentice / 踊跃的学徒的 battlefield spell-cost static representative 来源身份，从 `CoreRuleEngine` 与 `MatchSession` prompt 路径里的直接 cardNo 分支迁移到 `CardBehaviorRegistry.IsImplementedUnitWithEffectKind`。该切片只收窄当前战场静态法术减费来源身份硬编码；不关闭完整静态费用族、完整 PaymentEngine breadth、完整 LayerEngine breadth、frontend final validation 或 READY。

## Scope

Changed:

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/BattlefieldStaticSourceIdentityGuardTests.cs`
- `docs/CURRENT_PLAN_B_BATTLEFIELD_STATIC_SOURCE_IDENTITY_CATALOG_SOURCE_AUDIT.md`
- `docs/CURRENT_PLAN_B_BATTLEFIELD_STATIC_SOURCE_IDENTITY_CATALOG_SOURCE_EVIDENCE.md`
- `docs/rules-evidence-index.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`

Not changed:

- official card catalog JSON
- card matrix JSON
- protocol shape
- payment semantics
- frontend runtime
- `fullOfficial` / READY status

## Acceptance Review

| Requirement | Evidence | Verdict |
|---|---|---|
| Eager Apprentice runtime source identity no longer directly selects by card number | `CoreRuleEngine.ResolveBattlefieldSpellCostReductionMana` calls `CardBehaviorRegistry.IsImplementedUnitWithEffectKind` with `EAGER_APPRENTICE_SPELL_COST_STATIC_PLAY_UNIT` instead of comparing `cardObject.CardNo` to `EagerApprenticeCardNo` | Accepted |
| Eager Apprentice prompt source identity no longer directly selects by card number | `MatchSession.PromptBattlefieldSpellCostReductionMana` uses the same catalog source effect kind and no longer contains `EagerApprenticeCardNo` | Accepted |
| Existing visibility/control constraints are preserved | both runtime and prompt paths still require the source object to be controlled by the spell player and not face-down | Accepted |
| Registry identity is exact enough to reject wrong rows | `BattlefieldStaticSourceIdentityGuardTests` accepts `OGN·084/298` only for `EAGER_APPRENTICE_SPELL_COST_STATIC_PLAY_UNIT` and rejects wrong-card / wrong-effect examples | Accepted |
| Existing representative behavior is preserved | Eager Apprentice focused regression and adjacent `P79BattlefieldStatic` regression remain green | Accepted |
| Full static-cost family breadth | complete battlefield static cost-reduction family, complete PaymentEngine breadth, and complete LayerEngine breadth remain residual | Residual, no READY claim |

## Verification

Starting backend baseline before implementation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8668/8668 passed.

Focused source identity guard:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BattlefieldStaticSourceIdentityGuardTests" --nologo
```

Initial result before implementation: failed on `EagerApprenticeCardNo` still present.
Result after implementation: 4/4 passed.

Focused Eager Apprentice behavior:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~EagerApprentice|FullyQualifiedName~BattlefieldStaticSourceIdentityGuardTests" --nologo
```

Result: 11/11 passed.

Adjacent battlefield static regression:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~P79BattlefieldStatic" --nologo
```

Result: 31/31 passed.

Hidden-info / recovery boundary:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery" --nologo
```

Result: 1989/1989 passed.

Full backend conformance:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8672/8672 passed.

## Residual Risks

- This does not generalize every battlefield static cost reducer; it removes the current Eager Apprentice source card-number dependency and routes source identity through the implemented catalog effect kind.
- This does not close complete PaymentEngine breadth, complete LayerEngine breadth, complete static-cost official-card matrix, frontend final validation, or READY.
- Project remains **NOT READY**.
