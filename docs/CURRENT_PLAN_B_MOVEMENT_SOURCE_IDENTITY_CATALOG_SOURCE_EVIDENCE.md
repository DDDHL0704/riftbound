# Plan B Movement Source Identity Catalog Source Evidence

日期：2026-06-26
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

This file records concrete evidence for removing direct Bilgewater Bully card-number checks from the current boon-roam movement source representatives in runtime and prompt generation.

## Runtime Evidence

- `CoreRuleEngine.HasBilgewaterBullyBoonRoamPermission(...)` now validates source identity through `CardBehaviorRegistry.IsImplementedUnitWithEffectKind` using `BILGEWATER_BULLY_NO_BOON_ROAM_PLAY_UNIT`.
- `MatchSession.HasBilgewaterBullyBoonPromptRoamPermission(...)` now validates prompt source identity through the same catalog source effect kind.
- Both paths still require the source object to have `CardObjectTags.Boon`.
- The previous `BilgewaterBullyCardNo` Core and MatchSession source allow-list constants were deleted.

## Test Evidence

Focused test file:

- `tests/Riftbound.ConformanceTests/MovementSourceIdentityGuardTests.cs`

Coverage:

- `CardBehaviorRegistryIdentifiesMovementSourceUnitsByEffectKind` accepts the registered `OGN·125/298` source row used by this slice.
- `CardBehaviorRegistryRejectsNonMatchingMovementSourceUnits` rejects wrong-card and wrong-effect source identity matches.
- `BilgewaterBullyBoonRoamSourceIdentityUsesCatalogEffectKind` blocks reintroducing `BilgewaterBullyCardNo` or direct `sourceState.CardNo` comparisons in both `CoreRuleEngine.cs` and `MatchSession.cs`.
- Existing `P79BilgewaterBullyWithBoonCanUseRoam` / `P79BilgewaterBullyWithoutBoonDoesNotUseRoam` representatives verify runtime and prompt behavior remains intact.

## Verification

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MovementSourceIdentityGuardTests" --nologo
```

Result: 4/4 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BilgewaterBully|FullyQualifiedName~PreciseRoam|FullyQualifiedName~MoveUnit" --nologo
```

Result: 93/93 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery" --nologo
```

Result: 1989/1989 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8668/8668 passed.

## Non-Closure Statement

This evidence does not close complete Roam timing, complete movement lifecycle, complete boon-token family, complete B2 rule-text keyword layer, frontend final validation, or READY.
