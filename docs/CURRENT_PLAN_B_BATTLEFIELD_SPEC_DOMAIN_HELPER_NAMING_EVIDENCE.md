# Plan B Battlefield Spec Domain Helper Naming Evidence

日期：2026-06-25
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

This file records the concrete evidence for removing two misleading `Is*CardNo` helper names from `CoreRuleEngine` where the implementation already consumes BehaviorSpec / spec-rule domains rather than local card-number allow-lists.

## 1. Runtime Evidence

- `CoreRuleEngine.IsBattlefieldCardObject` still recognizes battlefield objects by either:
  - `P6TokenFactoryCatalog.BattlefieldCardTag`
  - `HasImplementedBattlefieldRuleSpec(cardNo)`
- `HasImplementedBattlefieldRuleSpec` is backed by spec-rule lookups across:
  - `StaticAuraSpecRules`
  - `BattlefieldTriggerSpecRules`
  - `BattlefieldStaticAbilitySpecRules`
- Turn-start held scoring still excludes dedicated score-rule battlefields through `HasDedicatedBattlefieldScoreRuleSpec`.
- `HasDedicatedBattlefieldScoreRuleSpec` is backed by `BattlefieldTriggerSpecRules.TryGetBattlefieldFirstTurnScoreTrigger` and `BattlefieldStaticAbilitySpecRules.TryGetBattlefieldScoreDelayUntilTurnAbility`.
- Existing runtime behavior, events, prompts, and snapshot shape are unchanged.

## 2. Test Evidence

Focused test file:

- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs`

Coverage:

- `BattlefieldSpecDomainHelpersDoNotUseCardNumberHelperNames` blocks reintroducing `IsImplementedBattlefieldCardNo` and `IsDedicatedBattlefieldScoreRuleCardNo`.
- The same guard requires the replacement helper names and the underlying spec-rule domain calls.
- Existing battlefield trigger / static ability guard tests remain green.

## 3. Verification

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BattlefieldSpecDomainHelpersDoNotUseCardNumberHelperNames|FullyQualifiedName~P6FunctionalUnitCoverageAuditsSameTextVariantsAndReprints|FullyQualifiedName~BattlefieldFirstTurnScoreTriggerDoesNotUseCardNumberAllowList|FullyQualifiedName~BattlefieldScoreDelayStaticAbilityDoesNotUseCardNumberAllowList"
```

Result: 4/4 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Battlefield|FullyQualifiedName~BattlefieldTriggerSpec|FullyQualifiedName~BattlefieldStatic|FullyQualifiedName~StaticAura|FullyQualifiedName~CardCatalogBaselineTests|FullyQualifiedName~FullGameEndToEnd"
```

Result: 1107/1107 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"
```

Result: 1989/1989 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj
```

Result: 8579/8579 passed.

## 4. Helper Count

After this slice, `rg -n "private static bool Is.*CardNo\\(" src/Riftbound.Engine src/Riftbound.CardCatalog src/Riftbound.Contracts` reports 24 total helpers, with 22 in `CoreRuleEngine.cs`.

## 5. Non-Closure Statement

This evidence does not close complete battlefield official breadth, complete trigger/static ability/static aura migration, card matrix full-official, frontend final validation or READY.
