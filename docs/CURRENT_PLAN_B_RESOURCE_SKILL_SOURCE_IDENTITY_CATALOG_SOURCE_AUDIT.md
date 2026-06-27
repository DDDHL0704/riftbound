# Plan B Resource Skill Source Identity Catalog Source Audit

日期：2026-06-27
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文件记录 Plan B 小切片：把 Blue Sentinel / 苍蓝雕纹魔像 held-battlefield delayed resource skill 的 runtime、prompt 与 recovery source-card revalidation、Lux / 拉克丝 spell-only resource skill 的 runtime/prompt source-card validation，以及 Jhin / 烬 movement resource skill 的 runtime/recovery source-card validation，从 direct card-number equality 迁移到 activated/resource ability source-card group helper；随后把官方 Jhin alt-A `UNL-022a/219` 与 Blue Sentinel alt-A `UNL-087a/219` 纳入对应 resource ability source-card group。该切片不新增资源技能、不改 spell-only resource amount、不改 movement resource trigger id、不改 payment-only temporary ledger、不改 delayed trigger id、prompt metadata、snapshot 或 recovery payload shape；Blue Sentinel delayed resource audit event 现在保留实际来源 cardNo。它让已有 `P4ActivatedAbilityCatalog.SourceCardNosForAbility` 成为 source identity 与 alt/reprint cardinality 的单一入口。

## Scope

Changed:

- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/BlueSentinelResourceSkillTests.cs`
- `tests/Riftbound.ConformanceTests/JhinMovementResourceSkillTests.cs`
- `tests/Riftbound.ConformanceTests/LuxResourceSkillTests.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
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
| Jhin movement resource source group includes official alt-A | `P4ActivatedAbilityCatalog.SourceCardNosForAbility(JhinMoveResourceAbilityId)` now returns `UNL-022/219` and `UNL-022a/219` | Accepted |
| Blue Sentinel delayed resource source group includes official alt-A | `P4ActivatedAbilityCatalog.SourceCardNosForAbility(BlueSentinelResourceAbilityId)` now returns `UNL-087/219` and `UNL-087a/219` | Accepted |
| Core runtime no longer directly selects Blue Sentinel by card number | `BlueSentinelDelayedSourceStillHoldsBattlefield` and `BuildBlueSentinelHeldDelayedResourceTriggers` now call `IsSourceCardNoForAbilityId(BlueSentinelResourceAbilityId, sourceState.CardNo)` | Accepted |
| Prompt/payment metadata no longer directly selects Blue Sentinel by card number | both `MatchSession` Blue Sentinel delayed-source checks now call the same helper | Accepted |
| Recovery validation no longer directly selects Blue Sentinel by card number | `MatchRecovery` source-card and source-still-holds-battlefield checks call the same helper; expected card-no diagnostics are generated from `SourceCardNosForAbility`, now `UNL-087/219 or UNL-087a/219` | Accepted |
| Blue Sentinel alt-A runtime uses the same source group | `BlueSentinelAltHeldBattlefieldQueuesAndConsumesDelayedResourceWithActualSourceCard` failed before source-group expansion with no delayed trigger, then passed after `UNL-087a/219` joined the group | Accepted |
| Blue Sentinel delayed resource audit keeps actual source cardNo | `TryMaterializeBlueSentinelDelayedResources` now emits `ABILITY_ACTIVATED.cardNo` from the source object, so alt-A reports `UNL-087a/219` | Accepted |
| Core runtime no longer directly selects Jhin movement resource by card number | `BuildJhinMovementResourceTrigger` now calls `IsSourceCardNoForAbilityId(JhinMoveResourceAbilityId, sourceState.CardNo)` | Accepted |
| Recovery validation no longer directly selects Jhin movement resource by card number | `ValidateTriggerQueueJhinMovementResourceContext` now calls `IsSourceCardNoForAbilityId(JhinMoveResourceAbilityId, sourceCardNo)`; expected card-no diagnostics are generated from `SourceCardNosForAbility` | Accepted |
| Core runtime no longer directly selects Lux spell-only resource by card number | `CanUseLuxSpellOnlyResourceSource` now calls `IsSourceCardNoForAbilityId(LuxResourceAbilityId, sourceState.CardNo)` | Accepted |
| Prompt metadata no longer directly selects Lux spell-only resource by card number | `CanPromptLuxSpellOnlyResourceSource` now calls `IsSourceCardNoForAbilityId(LuxResourceAbilityId, cardObject.CardNo)` | Accepted |
| Regression guard prevents direct source-card comparison from returning | `BlueSentinelSourceIdentityUsesAbilitySourceCardGroup` fails on direct `sourceState.CardNo` / `sourceCardNo` comparisons to `P4ActivatedAbilityCatalog.BlueSentinelCardNo` and requires the helper in Core, MatchSession, and MatchRecovery | Accepted |
| Jhin regression guard prevents direct source-card comparison from returning | `JhinMovementSourceIdentityUsesAbilitySourceCardGroup` fails on direct `sourceState.CardNo` / `sourceCardNo` comparisons to `P4ActivatedAbilityCatalog.JhinCardNo` and requires the helper in Core and MatchRecovery | Accepted |
| Jhin alt-A runtime uses the same source group | `JhinAltMovementResourceSkillGainsManaAndPaymentOnlyPower` failed before source-group expansion with no movement trigger, then passed after `UNL-022a/219` joined the group | Accepted |
| Recovery diagnostics follow the same source group | Jhin recovery source-card drift tests now assert expected source-card labels from `SourceCardNosForAbility`, preserving validation while reflecting `UNL-022/219 or UNL-022a/219` | Accepted |
| Lux regression guard prevents direct source-card comparison from returning | `LuxSpellOnlySourceIdentityUsesAbilitySourceCardGroup` fails on direct `sourceState.CardNo` / `cardObject.CardNo` comparisons to `P4ActivatedAbilityCatalog.LuxCardNo` and requires the helper in Core and MatchSession | Accepted |
| Existing Blue Sentinel resource-skill behavior is unchanged | BlueSentinel / MatchRecovery / PaymentEngine / PaymentEngineUnification adjacent representatives passed 2797/2797 | Accepted |
| Existing Jhin resource-skill behavior is unchanged | Jhin / MatchRecovery / PaymentEngine adjacent representatives passed 2705/2705 | Accepted |
| Existing Lux resource-skill behavior is unchanged | Lux / MatchRecovery / PaymentEngine adjacent representatives passed 2698/2698 | Accepted |
| Backend full conformance remains green | 8780/8780 passed | Accepted |

## Verification

Focused:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BlueSentinelSourceIdentityUsesAbilitySourceCardGroup" --nologo
```

Result: failed before implementation on direct `sourceState.CardNo` / `P4ActivatedAbilityCatalog.BlueSentinelCardNo`, then 1/1 passed after implementation.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BlueSentinelSourceGroupIncludesAltArt|FullyQualifiedName~BlueSentinelAltHeldBattlefieldQueuesAndConsumesDelayedResourceWithActualSourceCard" --nologo
```

Result: failed before implementation because `SourceCardNosForAbility` returned only `UNL-087/219` and alt-A held-battlefield produced no delayed trigger, then 2/2 passed after implementation.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~JhinMovementSourceIdentityUsesAbilitySourceCardGroup" --nologo
```

Result: failed before implementation on direct `sourceState.CardNo` / `P4ActivatedAbilityCatalog.JhinCardNo`, then 1/1 passed after implementation.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~JhinMovementResourceSourceGroupIncludesAltArt|FullyQualifiedName~JhinAltMovementResourceSkillGainsManaAndPaymentOnlyPower" --nologo
```

Result: failed before implementation because `SourceCardNosForAbility` returned only `UNL-022/219` and alt-A movement produced no trigger, then 2/2 passed after implementation.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~LuxSpellOnlySourceIdentityUsesAbilitySourceCardGroup" --nologo
```

Result: failed before implementation on direct `sourceState.CardNo` / `P4ActivatedAbilityCatalog.LuxCardNo`, then 1/1 passed after implementation.

Adjacent:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BlueSentinelResourceSkillTests" --nologo
```

Result: 17/17 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BlueSentinelResourceSkillTests|FullyQualifiedName~MatchRecovery|FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~PaymentEngineUnificationTests" --nologo
```

Result: 2797/2797 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~JhinMovementResourceSkillTests|FullyQualifiedName~MatchRecovery|FullyQualifiedName~PaymentEngineCoverageAuditTests" --nologo
```

Result: 2705/2705 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~LuxResourceSkillTests|FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2698/2698 passed.

Full backend conformance:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8780/8780 passed.

## Residual Risks

- This does not close complete resource-skill official breadth, remaining ability source-card group cardinality for other alternate arts/reprints, complete PaymentEngine / PAY_COST matrix, complete recovery payload breadth, frontend final validation, card matrix full-official coverage, or READY.
- Project remains **NOT READY**.
