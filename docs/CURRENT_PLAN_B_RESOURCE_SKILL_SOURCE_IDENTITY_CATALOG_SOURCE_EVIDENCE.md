# Plan B Resource Skill Source Identity Catalog Source Evidence

日期：2026-06-27
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

This file records concrete evidence for routing Blue Sentinel delayed resource-skill source identity through the activated/resource ability source-card group helper instead of direct `BlueSentinelCardNo` equality.

## Runtime Evidence

- `P4ActivatedAbilityCatalog.IsSourceCardNoForAbilityId` now allows callers to validate a concrete source card number through the ability row's `SourceCardNosForAbility` group without hand-rolling `TryGetByAbilityId` at every call site.
- `CoreRuleEngine.BlueSentinelDelayedSourceStillHoldsBattlefield` uses `P4ActivatedAbilityCatalog.IsSourceCardNoForAbilityId(P4ActivatedAbilityCatalog.BlueSentinelResourceAbilityId, sourceState.CardNo)`.
- `CoreRuleEngine.BuildBlueSentinelHeldDelayedResourceTriggers` uses the same helper before creating `BLUE_SENTINEL_HELD_DELAYED_RESOURCE` trigger queue entries.
- `MatchSession` uses the same helper in both Blue Sentinel delayed-resource prompt/payment metadata source-still-holds-battlefield checks.
- `MatchRecovery` uses the same helper for recovered snapshot, authoritative state, and spectator replay trigger-queue source-card validation.
- `MatchRecovery.ExpectedSourceCardNoLabelForAbilityId` builds source-card diagnostic labels from `P4ActivatedAbilityCatalog.SourceCardNosForAbility`, so the current single-card message remains `UNL-087/219` while future source groups do not need a recovery-code change.

## Test Evidence

Focused test file:

- `tests/Riftbound.ConformanceTests/BlueSentinelResourceSkillTests.cs`

Coverage:

- `CatalogExposesBlueSentinelDelayedResourceSkill` keeps the catalog row evidence for `BLUE_SENTINEL_HELD_DELAYED_NEXT_MAIN_GAIN_GENERIC_POWER`.
- `BlueSentinelSourceIdentityUsesAbilitySourceCardGroup` blocks direct `sourceState.CardNo` / `sourceCardNo` comparisons to `P4ActivatedAbilityCatalog.BlueSentinelCardNo` in Core, MatchSession, and MatchRecovery, and requires `P4ActivatedAbilityCatalog.IsSourceCardNoForAbilityId` in all three files.
- Existing Blue Sentinel focused tests still cover held-battlefield delayed trigger creation, next-main payment prompt metadata, generated power materialization, no-stack resource resolution, stale trigger rejection, hidden/standby/wrong-controller/wrong-battlefield guards, and temporary payment resource cleanup.
- Adjacent `MatchRecovery` coverage keeps recovered/spectator trigger queue context validation green.

## Verification

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BlueSentinelSourceIdentityUsesAbilitySourceCardGroup" --nologo
```

Result: failed before implementation on direct `sourceState.CardNo` / `P4ActivatedAbilityCatalog.BlueSentinelCardNo`, then 1/1 passed after implementation.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BlueSentinelResourceSkillTests|FullyQualifiedName~MatchRecovery|FullyQualifiedName~PaymentEngineCoverageAuditTests" --nologo
```

Result: 2701/2701 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8772/8772 passed.

## Non-Closure Statement

This evidence does not close Jhin movement resource, Lux spell-only resource, complete resource-skill official breadth, complete PaymentEngine / PAY_COST matrix, complete recovery payload breadth, frontend final validation, full official card matrix, or READY.
