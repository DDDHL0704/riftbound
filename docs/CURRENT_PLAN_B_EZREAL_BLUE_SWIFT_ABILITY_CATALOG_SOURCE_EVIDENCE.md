# Plan B Ezreal Blue Swift Ability Catalog Source Evidence

日期：2026-06-25
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

This file records the concrete evidence for the Plan B follow-up that removes duplicate Ezreal blue swift source-card identity from `CoreRuleEngine`.

## 1. Runtime Evidence

- `CoreRuleEngine.ResolveEzrealBlueSwiftMoveAbilityStackItem` now looks up the stack item's ability definition through `P4ActivatedAbilityCatalog.TryGetByEffectKind`.
- `TryMoveEzrealBlueSwiftSourceToBase` receives that definition and validates the source with `P4ActivatedAbilityCatalog.IsSourceCardNoForAbility`.
- The previous `CoreRuleEngine` helper `IsEzrealBlueSwiftCardNo` was deleted.
- `CoreRuleEngine.cs` no longer directly references `P4ActivatedAbilityCatalog.EzrealBlueSwiftAltCardNo` or `P4ActivatedAbilityCatalog.EzrealBlueSwiftPromoCardNo`.
- Existing stack-source safeguards remain: source must be public, unit-tagged, controlled by the activating player, match the stack `CardNo`, and still occupy the precise battlefield location before it can move to base.

## 2. Test Evidence

Focused test file:

- `tests/Riftbound.ConformanceTests/EzrealBlueSwiftMoveToBaseActivatedAbilityTests.cs`

New regression:

- `CoreRuleEngineUsesActivatedAbilityCatalogForEzrealBlueSwiftSourceIdentity`

The new regression scans `CoreRuleEngine.cs` to enforce that:

- `IsEzrealBlueSwiftCardNo` is absent.
- direct Core references to `EzrealBlueSwiftAltCardNo` and `EzrealBlueSwiftPromoCardNo` are absent.
- Core still uses `P4ActivatedAbilityCatalog.IsSourceCardNoForAbility`.

Existing behavior coverage retained:

- catalog aliases for all three collector Nos.
- prompt source requirement / no-target metadata / blue typed cost / legal recycle choice.
- successful command and pass-pass stack resolution for all three collector Nos.
- stale source, controller change, face-down / no-longer-public source and invalid source no-effect / no-mutation guards.

## 3. Verification

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~CatalogExposesEzrealBlueSwiftMoveForAllCollectorNumbers|FullyQualifiedName~CoreRuleEngineUsesActivatedAbilityCatalogForEzrealBlueSwiftSourceIdentity|FullyQualifiedName~EzrealCommandPaysBlueCreatesStackAndResolutionMovesSourceToBase|FullyQualifiedName~EzrealStackResolutionNoEffectsWhenSourceLeavesBattlefieldBeforeResolution"
```

Result: 6/6 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~EzrealBlueSwift|FullyQualifiedName~P4ActivatedAbility|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~PaymentEngineCoverageAuditTests"
```

Result: 1011/1011 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"
```

Result: 1989/1989 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj
```

Result: 8533/8533 passed.

## 4. Helper Count

After this slice, `rg -n "private static bool Is.*CardNo\\(" src/Riftbound.Engine src/Riftbound.CardCatalog src/Riftbound.Contracts` reports 31 total helpers, with 28 in `CoreRuleEngine.cs`.

## 5. Non-Closure Statement

This evidence does not close full official Ezreal, full swift timing, attack / defense damage trigger, cannot-combat-damage static, FAQ adjudication, card matrix full-official, frontend final validation or READY.
