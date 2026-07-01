# Plan B Movement Source Identity Catalog Source Evidence

日期：2026-06-26
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

This file records concrete evidence for removing direct Bilgewater Bully card-number checks from the current boon-roam movement source representatives in runtime and prompt generation. 2026-06-30 follow-up evidence records that the same representative no longer consumes the runtime effect-kind selector and now reads `BehaviorSpec.StaticAuras` / `SOURCE_OBJECT_FILTERED_KEYWORD`. 2026-07-01 follow-up evidence records that Core and MatchSession consume that BehaviorSpec through `StaticAuraSpecRules.IsSourceObjectKeywordStaticAura` scope routing rather than a kind-specific lookup.

## Runtime Evidence

- `CoreRuleEngine.HasBilgewaterBullyBoonRoamPermission(...)` now validates source identity through `CardBehaviorRegistry.IsImplementedUnitWithEffectKind` using `BILGEWATER_BULLY_NO_BOON_ROAM_PLAY_UNIT`.
- `MatchSession.HasBilgewaterBullyBoonPromptRoamPermission(...)` now validates prompt source identity through the same catalog source effect kind.
- Both paths still require the source object to have `CardObjectTags.Boon`.
- The previous `BilgewaterBullyCardNo` Core and MatchSession source allow-list constants were deleted.
- 2026-06-30 follow-up: `CoreRuleEngine` and `MatchSession` no longer contain `BilgewaterBullyBoonRoamSourceEffectKind` or `BILGEWATER_BULLY_NO_BOON_ROAM_PLAY_UNIT` in runtime permission paths.
- 2026-06-30 follow-up: `StaticAuraParser` parses `OGN·125/298` as `SOURCE_OBJECT_FILTERED_KEYWORD` with `TargetFilter=TAG:增益` and `GrantedKeyword=游走`.
- 2026-07-01 follow-up: both files enumerate `StaticAuraSpecRules.GetStaticAuras(cardNo)` and filter with `StaticAuraSpecRules.IsSourceObjectKeywordStaticAura`; `StaticAuraSpecRules.TryGetSourceObjectFilteredKeywordAura` has been removed from the engine helper surface.

## Test Evidence

Focused test file:

- `tests/Riftbound.ConformanceTests/MovementSourceIdentityGuardTests.cs`

Coverage:

- `CardBehaviorRegistryIdentifiesMovementSourceUnitsByEffectKind` accepts the registered `OGN·125/298` source row used by this slice.
- `CardBehaviorRegistryRejectsNonMatchingMovementSourceUnits` rejects wrong-card and wrong-effect source identity matches.
- `BilgewaterBullyBoonRoamSourceIdentityUsesSourceObjectFilteredKeywordAura` blocks reintroducing `BilgewaterBullyCardNo`, `BilgewaterBullyBoonRoamSourceEffectKind`, `BILGEWATER_BULLY_NO_BOON_ROAM_PLAY_UNIT`, or direct `sourceState.CardNo` comparisons in both `CoreRuleEngine.cs` and `MatchSession.cs`, and requires `StaticAuraSpecRules.IsSourceObjectKeywordStaticAura`.
- `CardCatalogBaselineTests.BehaviorSpecCatalogParsesStaticAuraSpecsForExistingRepresentatives` locks `OGN·125/298` to a `SOURCE_OBJECT_FILTERED_KEYWORD` rule-text aura.
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

2026-06-30 follow-up:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo --filter "FullyQualifiedName~BilgewaterBullyBoonRoamSourceIdentityUsesSourceObjectFilteredKeywordAura|FullyQualifiedName~BehaviorSpecCatalogParsesStaticAuraSpecsForExistingRepresentatives|FullyQualifiedName~P79BilgewaterBully"
```

Result: 4/4 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo --filter "FullyQualifiedName~MovementSourceIdentityGuardTests|FullyQualifiedName~BilgewaterBully|FullyQualifiedName~PreciseRoam|FullyQualifiedName~MoveUnit|FullyQualifiedName~CardCatalogBaselineTests|FullyQualifiedName~MatchRecovery"
```

Result: 2393/2393 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo
```

Result: 9026/9026 passed.

## Non-Closure Statement

This evidence does not close complete Roam timing, complete movement lifecycle, complete boon-token family, complete B2 rule-text keyword layer, frontend final validation, or READY.
