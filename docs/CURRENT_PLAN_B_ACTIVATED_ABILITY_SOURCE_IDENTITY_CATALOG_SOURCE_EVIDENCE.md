# Plan B Activated Ability Source Identity Catalog Source Evidence

日期：2026-06-27
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

This file records concrete evidence for routing command-side activated ability source identity checks through the existing catalog source-card group helper instead of direct `ability.SourceCardNo` equality.

## 1. Runtime Evidence

- `P4ActivatedAbilityCatalog.SourceCardNosForAbility` already defines the canonical source-card group for activated abilities, including existing alt/promo source rows such as Renata Glasc, Azir, and Ezreal Blue Swift.
- `P4ActivatedAbilityCatalog.IsSourceCardNoForAbility` is the shared predicate for checking whether a concrete source card number belongs to that source-card group.
- `CoreRuleEngine` no longer contains `string.Equals(sourceState.CardNo, ability.SourceCardNo, StringComparison.Ordinal)`.
- The remaining command-side source revalidation branches for Vi, Malzahar, Dragon Soul Sage, Xerath, Crimson Rose, Fluft Poro, and Shadow now call `P4ActivatedAbilityCatalog.IsSourceCardNoForAbility(ability, sourceState.CardNo)`.
- This slice does not change source visibility, controller checks, target checks, payment calculation, stack item creation, exhaustion, event payloads, or snapshot projection.

## 2. Test Evidence

Focused test file:

- `tests/Riftbound.ConformanceTests/ActivatedAbilitySourceIdentityGuardTests.cs`

Coverage:

- `CoreActivatedAbilitySourceChecksUseCatalogSourceCardGroups` blocks reintroducing direct `sourceState.CardNo` / `ability.SourceCardNo` source equality in `CoreRuleEngine`.
- The same guard requires `P4ActivatedAbilityCatalog.IsSourceCardNoForAbility(ability, sourceState.CardNo)` to remain present.
- Adjacent activated ability coverage includes Vi, Xerath, Malzahar, Dragon Soul Sage, Crimson Rose, Fluft Poro, Shadow, plus Renata / Azir / Ezreal source-group representatives and `PaymentEngineCoverageAuditTests`.
- `MatchRecovery` remains green in the adjacent set.

## 3. Verification

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~CoreActivatedAbilitySourceChecksUseCatalogSourceCardGroups" --nologo
```

Result: failed before implementation on direct `sourceState.CardNo` / `ability.SourceCardNo` comparison, then 1/1 passed after implementation.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~ActivatedAbilitySourceIdentityGuardTests|FullyQualifiedName~ViActivated|FullyQualifiedName~Xerath|FullyQualifiedName~Malzahar|FullyQualifiedName~DragonSoulSage|FullyQualifiedName~CrimsonRose|FullyQualifiedName~FluftPoro|FullyQualifiedName~ShadowActivated|FullyQualifiedName~RenataActivated|FullyQualifiedName~AzirSwiftSwap|FullyQualifiedName~EzrealBlueSwift|FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2992/2992 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8768/8768 passed.

## 4. Non-Closure Statement

This evidence does not close complete activated ability source group extraction from BehaviorSpec, complete activated ability family breadth, complete PaymentEngine / PAY_COST matrix, complete target/timing/stack breadth, frontend final validation, or READY.
