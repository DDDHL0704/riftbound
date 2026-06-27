# Plan B Activated Ability Source Identity Catalog Source Evidence

日期：2026-06-27
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

This file records concrete evidence for routing activated ability source identity checks through the existing catalog source-card group helper instead of direct source-card number equality. The original slice covered command-side `ability.SourceCardNo` revalidation; the 2026-06-27 follow-ups cover Gatekeeper Maduli prompt target filtering / command target legality and Vi alt-A source-card group cardinality.

## 1. Runtime Evidence

- `P4ActivatedAbilityCatalog.SourceCardNosForAbility` already defines the canonical source-card group for activated abilities, including existing alt/promo source rows such as Renata Glasc, Azir, and Ezreal Blue Swift.
- `P4ActivatedAbilityCatalog.IsSourceCardNoForAbility` is the shared predicate for checking whether a concrete source card number belongs to that source-card group.
- `CoreRuleEngine` no longer contains `string.Equals(sourceState.CardNo, ability.SourceCardNo, StringComparison.Ordinal)`.
- The remaining command-side source revalidation branches for Vi, Malzahar, Dragon Soul Sage, Xerath, Crimson Rose, Fluft Poro, and Shadow now call `P4ActivatedAbilityCatalog.IsSourceCardNoForAbility(ability, sourceState.CardNo)`.
- `CoreRuleEngine.IsLegalGatekeeperMaduliMoveTarget` and `MatchSession.IsPromptGatekeeperMaduliMoveTarget` fetch `GatekeeperMaduliMoveAbilityId` and use `P4ActivatedAbilityCatalog.IsSourceCardNoForAbility(ability, sourceState.CardNo)` instead of direct `sourceState.CardNo` / `GatekeeperMaduliCardNo` equality.
- `P4ActivatedAbilityCatalog.SourceCardNosForAbility(ViDoublePowerAbilityId)` now returns `UNL-030/219` and official alt-A `UNL-030a/219`.
- `CoreRuleEngine.ResolveViDoublePowerAbility` writes the actual source object card number into the stack item (`sourceState.CardNo ?? ability.SourceCardNo`) so alt-A source identity is preserved through stack resolution.
- This slice does not change source visibility, controller checks, target checks, payment calculation, exhaustion, event payloads, or snapshot projection.

## 2. Test Evidence

Focused test file:

- `tests/Riftbound.ConformanceTests/ActivatedAbilitySourceIdentityGuardTests.cs`
- `tests/Riftbound.ConformanceTests/ViDoublePowerAbilityTests.cs`

Coverage:

- `CoreActivatedAbilitySourceChecksUseCatalogSourceCardGroups` blocks reintroducing direct `sourceState.CardNo` / `ability.SourceCardNo` source equality in `CoreRuleEngine`.
- The same guard requires `P4ActivatedAbilityCatalog.IsSourceCardNoForAbility(ability, sourceState.CardNo)` to remain present.
- `GatekeeperMaduliTargetLegalityUsesCatalogSourceCardGroup` blocks reintroducing direct `sourceState.CardNo` / `P4ActivatedAbilityCatalog.GatekeeperMaduliCardNo` source equality in Core and MatchSession and requires the shared source-card group helper in both files.
- `ViDoublePowerSourceGroupIncludesAltArt` locks `PAY_2_RED_DOUBLE_POWER` source-card group to include both `UNL-030/219` and `UNL-030a/219`.
- `ViAltDoublePowerAbilityAddsStackItemAndResolves` proves official alt-A Vi activates the same double-power ability, emits a stack item with `cardNo = UNL-030a/219`, and resolves to 6 power with a +3 until-end-of-turn modifier.
- Adjacent activated ability coverage includes Vi, Xerath, Malzahar, Dragon Soul Sage, Crimson Rose, Fluft Poro, Shadow, plus Renata / Azir / Ezreal source-group representatives and `PaymentEngineCoverageAuditTests`.
- Follow-up adjacent coverage includes Gatekeeper Maduli prompt / command / stale target representatives, Crimson Rose cannot-ready interaction representatives, `ActivatedAbilitySourceIdentityGuardTests`, and `PaymentEngineCoverageAuditTests`.
- Vi follow-up adjacent coverage includes `P4ActivateAbilityCommand*`, `PaymentEngineCoverageAuditTests`, `PaymentEngineUnificationTests`, and `MatchRecovery`.
- `MatchRecovery` remains green in the adjacent sets.

## 3. Verification

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~CoreActivatedAbilitySourceChecksUseCatalogSourceCardGroups" --nologo
```

Result: failed before implementation on direct `sourceState.CardNo` / `ability.SourceCardNo` comparison, then 1/1 passed after implementation.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GatekeeperMaduliTargetLegalityUsesCatalogSourceCardGroup" --nologo
```

Result: failed before implementation on direct `sourceState.CardNo` / `P4ActivatedAbilityCatalog.GatekeeperMaduliCardNo` comparison, then 1/1 passed after implementation.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~ViDoublePowerSourceGroupIncludesAltArt|FullyQualifiedName~ViAltDoublePowerAbilityAddsStackItemAndResolves" --nologo
```

Result: failed before implementation because `SourceCardNosForAbility` returned only `UNL-030/219` and alt-A activation was rejected, then 2/2 passed after implementation.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~ActivatedAbilitySourceIdentityGuardTests|FullyQualifiedName~ViActivated|FullyQualifiedName~Xerath|FullyQualifiedName~Malzahar|FullyQualifiedName~DragonSoulSage|FullyQualifiedName~CrimsonRose|FullyQualifiedName~FluftPoro|FullyQualifiedName~ShadowActivated|FullyQualifiedName~RenataActivated|FullyQualifiedName~AzirSwiftSwap|FullyQualifiedName~EzrealBlueSwift|FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2992/2992 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~ActivatedAbilitySourceIdentityGuardTests|FullyQualifiedName~GatekeeperMaduliActivatedAbilityTests|FullyQualifiedName~CrimsonRoseActivatedAbilityTests|FullyQualifiedName~PaymentEngineCoverageAuditTests" --nologo
```

Result: 759/759 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~ViDoublePowerAbilityTests|FullyQualifiedName~P4ActivateAbilityCommand|FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2825/2825 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8778/8778 passed.

## 4. Non-Closure Statement

This evidence does not close complete activated ability source group extraction from BehaviorSpec, remaining activated ability alternate-art / reprint source-card group cardinality, complete activated ability family breadth, complete PaymentEngine / PAY_COST matrix, complete target/timing/stack breadth, frontend final validation, or READY.
