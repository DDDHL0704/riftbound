# Plan B Battlefield Static Source Identity Catalog Source Evidence

日期：2026-06-26
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

This file records concrete evidence for removing direct Eager Apprentice card-number checks from the current battlefield spell-cost static source representatives in runtime and prompt generation.

## Runtime Evidence

- `CoreRuleEngine.ResolveBattlefieldSpellCostReductionMana(...)` now validates source identity through `CardBehaviorRegistry.IsImplementedUnitWithEffectKind` using `EAGER_APPRENTICE_SPELL_COST_STATIC_PLAY_UNIT`.
- `MatchSession.PromptBattlefieldSpellCostReductionMana(...)` now validates prompt source identity through the same catalog source effect kind.
- Both paths still require the source object to be controlled by the spell player and not face-down.
- The previous `EagerApprenticeCardNo` Core and MatchSession source allow-list constants were deleted.

## Test Evidence

Focused test file:

- `tests/Riftbound.ConformanceTests/BattlefieldStaticSourceIdentityGuardTests.cs`

Coverage:

- `CardBehaviorRegistryIdentifiesBattlefieldStaticSourceUnitsByEffectKind` accepts the registered `OGN·084/298` source row used by this slice.
- `CardBehaviorRegistryRejectsNonMatchingBattlefieldStaticSourceUnits` rejects wrong-card and wrong-effect source identity matches.
- `EagerApprenticeSpellCostSourceIdentityUsesCatalogEffectKind` blocks reintroducing `EagerApprenticeCardNo` or direct `cardObject.CardNo` comparisons in both `CoreRuleEngine.cs` and `MatchSession.cs`.
- Existing `P79BattlefieldStaticEagerApprenticeReducesSpellCost`, `P79BattlefieldStaticEagerApprenticeSkipsOpponentControlledSource`, and `P79BattlefieldStaticEagerApprenticePromptShowsSpellCostReduction` verify runtime and prompt behavior remains intact.

## Verification

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BattlefieldStaticSourceIdentityGuardTests" --nologo
```

Initial result before implementation: failed on `EagerApprenticeCardNo` still present.
Result after implementation: 4/4 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~EagerApprentice|FullyQualifiedName~BattlefieldStaticSourceIdentityGuardTests" --nologo
```

Result: 11/11 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~P79BattlefieldStatic" --nologo
```

Result: 31/31 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery" --nologo
```

Result: 1989/1989 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8672/8672 passed.

## Non-Closure Statement

This evidence does not close complete battlefield static cost-reduction family breadth, complete PaymentEngine breadth, complete LayerEngine breadth, frontend final validation, full official card matrix, or READY.
