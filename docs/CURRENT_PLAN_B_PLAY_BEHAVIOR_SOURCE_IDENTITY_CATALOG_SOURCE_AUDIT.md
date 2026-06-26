# Plan B Play Behavior Source Identity Catalog Source Audit

日期：2026-06-26
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文件记录 Plan B 小切片：把 Raging Drake / 狂暴龙怪的 next-spell cost reduction play-behavior 来源身份，从 `CoreRuleEngine` 的直接 cardNo 分支迁移到 catalog effect kind 分支。该切片只收窄当前 play resolution representative source identity 硬编码；不关闭完整 play-trigger family、完整 PaymentEngine breadth、完整 LayerEngine breadth、frontend final validation 或 READY。

## Scope

Changed:

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/PlayBehaviorSourceIdentityGuardTests.cs`
- `docs/CURRENT_PLAN_B_PLAY_BEHAVIOR_SOURCE_IDENTITY_CATALOG_SOURCE_AUDIT.md`
- `docs/CURRENT_PLAN_B_PLAY_BEHAVIOR_SOURCE_IDENTITY_CATALOG_SOURCE_EVIDENCE.md`
- `docs/rules-evidence-index.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`

Not changed:

- official card catalog JSON
- card matrix JSON
- protocol shape
- next-spell cost-reduction semantics
- frontend runtime
- `fullOfficial` / READY status

## Acceptance Review

| Requirement | Evidence | Verdict |
|---|---|---|
| Raging Drake play resolution source identity no longer directly selects by card number | `CoreRuleEngine` now checks `behavior.EffectKind == RAGING_DRAKE_NEXT_SPELL_COST_PLAY_UNIT` instead of `behavior.CardNo == RagingDrakeCardNo` | Accepted |
| Existing next-spell marker semantics are preserved | the branch still creates `RAGING_DRAKE_NEXT_SPELL_COST_REDUCTION:<playerId>:<sourceObjectId>` and emits `TRIGGER_RESOLVED.effectKind=RAGING_DRAKE_NEXT_SPELL_COST_REDUCTION` | Accepted |
| Registry identity is exact enough to reject wrong rows | `PlayBehaviorSourceIdentityGuardTests` accepts `OGN·031/298` only for `RAGING_DRAKE_NEXT_SPELL_COST_PLAY_UNIT` and rejects wrong-card / wrong-effect examples | Accepted |
| Existing representative behavior is preserved | Raging Drake focused regression and adjacent RagingDrake / LuxHighCost / PaymentEngineCoverageAuditTests regression remain green | Accepted |
| Full play-trigger family breadth | complete play-trigger routing, complete PaymentEngine breadth, and complete LayerEngine breadth remain residual | Residual, no READY claim |

## Verification

Focused source identity guard:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~PlayBehaviorSourceIdentityGuardTests" --nologo
```

Initial result before implementation: failed on `RagingDrakeCardNo` still present.

Focused Raging Drake behavior:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~PlayBehaviorSourceIdentityGuardTests|FullyQualifiedName~P79RagingDrake|FullyQualifiedName~RagingDrake" --nologo
```

Result after implementation: 8/8 passed.

Adjacent cost / payment regression:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RagingDrake|FullyQualifiedName~LuxHighCost|FullyQualifiedName~PaymentEngineCoverageAuditTests" --nologo
```

Result: 747/747 passed.

Hidden-info / recovery boundary:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery" --nologo
```

Result: 1989/1989 passed.

Full backend conformance:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8676/8676 passed.

## Residual Risks

- This does not generalize every play-trigger representative; it removes the current Raging Drake source card-number dependency and routes the branch through the implemented catalog effect kind.
- This does not close complete play-trigger ordering, complete `ORDER_TRIGGERS` / APNAP breadth, complete PaymentEngine breadth, complete LayerEngine breadth, frontend final validation, or READY.
- Project remains **NOT READY**.
