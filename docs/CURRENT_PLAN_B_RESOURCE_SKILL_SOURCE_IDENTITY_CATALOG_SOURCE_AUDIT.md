# Plan B Resource Skill Source Identity Catalog Source Audit

日期：2026-06-27
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文件记录 Plan B 小切片：把 Blue Sentinel / 苍蓝雕纹魔像 held-battlefield delayed resource skill 的 runtime、prompt 与 recovery source-card revalidation，以及 Lux / 拉克丝 spell-only resource skill 的 runtime/prompt source-card validation，从 direct card-number equality 迁移到 activated/resource ability source-card group helper。该切片不新增资源技能、不改 spell-only resource amount、不改 payment-only temporary ledger、不改 delayed trigger id、prompt metadata、audit event、snapshot 或 recovery payload shape；它只让已有 `P4ActivatedAbilityCatalog.SourceCardNosForAbility` 成为 source identity 的单一入口。

## Scope

Changed:

- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/BlueSentinelResourceSkillTests.cs`
- `tests/Riftbound.ConformanceTests/LuxResourceSkillTests.cs`
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
| Core runtime no longer directly selects Lux spell-only resource by card number | `CanUseLuxSpellOnlyResourceSource` now calls `IsSourceCardNoForAbilityId(LuxResourceAbilityId, sourceState.CardNo)` | Accepted |
| Prompt metadata no longer directly selects Lux spell-only resource by card number | `CanPromptLuxSpellOnlyResourceSource` now calls `IsSourceCardNoForAbilityId(LuxResourceAbilityId, cardObject.CardNo)` | Accepted |
| Regression guard prevents direct source-card comparison from returning | `BlueSentinelSourceIdentityUsesAbilitySourceCardGroup` fails on direct `sourceState.CardNo` / `sourceCardNo` comparisons to `P4ActivatedAbilityCatalog.BlueSentinelCardNo` and requires the helper in Core, MatchSession, and MatchRecovery | Accepted |
| Lux regression guard prevents direct source-card comparison from returning | `LuxSpellOnlySourceIdentityUsesAbilitySourceCardGroup` fails on direct `sourceState.CardNo` / `cardObject.CardNo` comparisons to `P4ActivatedAbilityCatalog.LuxCardNo` and requires the helper in Core and MatchSession | Accepted |
| Existing Blue Sentinel resource-skill behavior is unchanged | BlueSentinel / MatchRecovery / PaymentEngine adjacent representatives passed 2701/2701 | Accepted |
| Existing Lux resource-skill behavior is unchanged | Lux / MatchRecovery / PaymentEngine adjacent representatives passed 2698/2698 | Accepted |
| Backend full conformance remains green | 8773/8773 passed | Accepted |

## Verification

Focused:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BlueSentinelSourceIdentityUsesAbilitySourceCardGroup" --nologo
```

Result: failed before implementation on direct `sourceState.CardNo` / `P4ActivatedAbilityCatalog.BlueSentinelCardNo`, then 1/1 passed after implementation.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~LuxSpellOnlySourceIdentityUsesAbilitySourceCardGroup" --nologo
```

Result: failed before implementation on direct `sourceState.CardNo` / `P4ActivatedAbilityCatalog.LuxCardNo`, then 1/1 passed after implementation.

Adjacent:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BlueSentinelResourceSkillTests|FullyQualifiedName~MatchRecovery|FullyQualifiedName~PaymentEngineCoverageAuditTests" --nologo
```

Result: 2701/2701 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~LuxResourceSkillTests|FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2698/2698 passed.

Full backend conformance:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8773/8773 passed.

## Residual Risks

- Jhin movement resource still has direct source-card equality in runtime / recovery paths and remains an open follow-up candidate.
- This does not close complete resource-skill official breadth, complete PaymentEngine / PAY_COST matrix, complete recovery payload breadth, frontend final validation, card matrix full-official coverage, or READY.
- Project remains **NOT READY**.
