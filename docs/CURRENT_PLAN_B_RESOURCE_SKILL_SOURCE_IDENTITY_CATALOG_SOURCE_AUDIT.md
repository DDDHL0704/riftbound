# Plan B Resource Skill Source Identity Catalog Source Audit

日期：2026-06-27
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文件记录 Plan B 小切片：把 Blue Sentinel / 苍蓝雕纹魔像 held-battlefield delayed resource skill 的 runtime、prompt 与 recovery source-card revalidation 从 direct `BlueSentinelCardNo` equality 迁移到 activated/resource ability source-card group helper。该切片不新增资源技能、不改 payment-only temporary ledger、不改 delayed trigger id、prompt metadata、audit event、snapshot 或 recovery payload shape；它只让已有 `P4ActivatedAbilityCatalog.SourceCardNosForAbility` 成为 source identity 的单一入口。

## Scope

Changed:

- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/BlueSentinelResourceSkillTests.cs`
- `docs/CURRENT_PLAN_B_RESOURCE_SKILL_SOURCE_IDENTITY_CATALOG_SOURCE_AUDIT.md`
- `docs/CURRENT_PLAN_B_RESOURCE_SKILL_SOURCE_IDENTITY_CATALOG_SOURCE_EVIDENCE.md`
- `docs/rules-evidence-index.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`

Not changed:

- official card catalog JSON
- Blue Sentinel ability catalog row
- delayed trigger id / effect kind / payment resource restriction
- generated power amount or payment-only lifecycle
- frontend runtime
- `fullOfficial` / READY status

## Acceptance Review

| Requirement | Evidence | Verdict |
|---|---|---|
| Shared ability-id source group helper exists | `P4ActivatedAbilityCatalog.IsSourceCardNoForAbilityId(abilityId, cardNo)` now wraps `TryGetByAbilityId` + `IsSourceCardNoForAbility` | Accepted |
| Core runtime no longer directly selects Blue Sentinel by card number | `BlueSentinelDelayedSourceStillHoldsBattlefield` and `BuildBlueSentinelHeldDelayedResourceTriggers` now call `IsSourceCardNoForAbilityId(BlueSentinelResourceAbilityId, sourceState.CardNo)` | Accepted |
| Prompt/payment metadata no longer directly selects Blue Sentinel by card number | both `MatchSession` Blue Sentinel delayed-source checks now call the same helper | Accepted |
| Recovery validation no longer directly selects Blue Sentinel by card number | `MatchRecovery` source-card and source-still-holds-battlefield checks call the same helper; expected card-no diagnostics are generated from `SourceCardNosForAbility` | Accepted |
| Regression guard prevents direct source-card comparison from returning | `BlueSentinelSourceIdentityUsesAbilitySourceCardGroup` fails on direct `sourceState.CardNo` / `sourceCardNo` comparisons to `P4ActivatedAbilityCatalog.BlueSentinelCardNo` and requires the helper in Core, MatchSession, and MatchRecovery | Accepted |
| Existing resource-skill behavior is unchanged | BlueSentinel / MatchRecovery / PaymentEngine adjacent representatives passed 2701/2701 | Accepted |
| Backend full conformance remains green | 8772/8772 passed | Accepted |

## Verification

Focused:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BlueSentinelSourceIdentityUsesAbilitySourceCardGroup" --nologo
```

Result: failed before implementation on direct `sourceState.CardNo` / `P4ActivatedAbilityCatalog.BlueSentinelCardNo`, then 1/1 passed after implementation.

Adjacent:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BlueSentinelResourceSkillTests|FullyQualifiedName~MatchRecovery|FullyQualifiedName~PaymentEngineCoverageAuditTests" --nologo
```

Result: 2701/2701 passed.

Full backend conformance:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8772/8772 passed.

## Residual Risks

- Jhin movement resource and Lux spell-only resource still have direct source-card equality in runtime / recovery paths and remain open follow-up candidates.
- This does not close complete resource-skill official breadth, complete PaymentEngine / PAY_COST matrix, complete recovery payload breadth, frontend final validation, card matrix full-official coverage, or READY.
- Project remains **NOT READY**.
