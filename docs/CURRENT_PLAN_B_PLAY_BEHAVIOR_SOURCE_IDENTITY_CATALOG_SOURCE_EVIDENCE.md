# Plan B Play Behavior Source Identity Catalog Source Evidence

日期：2026-06-26
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

This file records concrete evidence for removing the direct Raging Drake card-number check from the current next-spell cost-reduction play-behavior representative.

## Runtime Evidence

- `CoreRuleEngine` now validates the Raging Drake play-behavior source branch through `behavior.EffectKind` using `RAGING_DRAKE_NEXT_SPELL_COST_PLAY_UNIT`.
- The runtime branch still creates the same until-end-of-turn marker id prefix `RAGING_DRAKE_NEXT_SPELL_COST_REDUCTION:<playerId>:<sourceObjectId>`.
- The runtime branch still emits `TRIGGER_RESOLVED` with `effectKind=RAGING_DRAKE_NEXT_SPELL_COST_REDUCTION` and amount 5.
- The previous `RagingDrakeCardNo` source allow-list constant was deleted from `CoreRuleEngine.cs`.

## Test Evidence

Focused test file:

- `tests/Riftbound.ConformanceTests/PlayBehaviorSourceIdentityGuardTests.cs`

Coverage:

- `CardBehaviorRegistryIdentifiesPlaySourceUnitsByEffectKind` accepts the registered `OGN·031/298` source row used by this slice.
- `CardBehaviorRegistryRejectsNonMatchingPlaySourceUnits` rejects wrong-card and wrong-effect source identity matches.
- `RagingDrakeNextSpellCostPlaySourceUsesCatalogEffectKind` blocks reintroducing `RagingDrakeCardNo` or direct `behavior.CardNo` comparisons in `CoreRuleEngine.cs`.
- Existing `P79RagingDrakeCreatesNextSpellCostReductionAfterResolution`, `P79RagingDrakeNextSpellCostReductionPromptShowsReducedSpellCost`, and `P79RagingDrakeNextSpellCostReductionPaysReducedSpellCostAndConsumesMarker` verify runtime and prompt behavior remains intact.

## Verification

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~PlayBehaviorSourceIdentityGuardTests" --nologo
```

Initial result before implementation: failed on `RagingDrakeCardNo` still present.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~PlayBehaviorSourceIdentityGuardTests|FullyQualifiedName~P79RagingDrake|FullyQualifiedName~RagingDrake" --nologo
```

Result after implementation: 8/8 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RagingDrake|FullyQualifiedName~LuxHighCost|FullyQualifiedName~PaymentEngineCoverageAuditTests" --nologo
```

Result: 747/747 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery" --nologo
```

Result: 1989/1989 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8676/8676 passed.

## Non-Closure Statement

This evidence does not close complete play-trigger routing, complete `ORDER_TRIGGERS` / APNAP breadth, complete PaymentEngine breadth, complete LayerEngine breadth, frontend final validation, full official card matrix, or READY.
