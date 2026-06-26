# Plan B Play Behavior Source Identity Catalog Source Evidence

日期：2026-06-26
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

This file records concrete evidence for removing direct Raging Drake, Poro Herder, and Balanced Disciple card-number checks from current play-behavior representatives.

## Runtime Evidence

- `CoreRuleEngine` now validates the Raging Drake play-behavior source branch through `behavior.EffectKind` using `RAGING_DRAKE_NEXT_SPELL_COST_PLAY_UNIT`.
- The runtime branch still creates the same until-end-of-turn marker id prefix `RAGING_DRAKE_NEXT_SPELL_COST_REDUCTION:<playerId>:<sourceObjectId>`.
- The runtime branch still emits `TRIGGER_RESOLVED` with `effectKind=RAGING_DRAKE_NEXT_SPELL_COST_REDUCTION` and amount 5.
- `CoreRuleEngine` now validates the Poro Herder boon/draw play-behavior source branch through `behavior.EffectKind` using `PORO_HERDER_NO_PORO_STATIC_PLAY_UNIT`.
- The Poro Herder branch still requires a controlled face-up Poro unit, grants boon to the source, and draws 1.
- `CoreRuleEngine` now validates the Balanced Disciple other-power draw play-behavior source branch through `behavior.EffectKind` using `BALANCED_DISCIPLE_NO_OTHER_POWER_VANILLA_PLAY_UNIT`.
- The Balanced Disciple branch still requires other controlled unit power total at least 5 and draws 1.
- The previous `RagingDrakeCardNo`, `PoroHerderCardNo`, and `BalancedDiscipleCardNo` source allow-list constants were deleted from `CoreRuleEngine.cs`.

## Test Evidence

Focused test file:

- `tests/Riftbound.ConformanceTests/PlayBehaviorSourceIdentityGuardTests.cs`

Coverage:

- `CardBehaviorRegistryIdentifiesPlaySourceUnitsByEffectKind` accepts the registered `OGN·031/298`, `OGN·061/298`, and `UNL-097/219` source rows used by this slice.
- `CardBehaviorRegistryRejectsNonMatchingPlaySourceUnits` rejects wrong-card and wrong-effect source identity matches.
- `RagingDrakeNextSpellCostPlaySourceUsesCatalogEffectKind` blocks reintroducing `RagingDrakeCardNo` or direct `behavior.CardNo` comparisons in `CoreRuleEngine.cs`.
- `PoroHerderBoonDrawPlaySourceUsesCatalogEffectKind` blocks reintroducing `PoroHerderCardNo` or direct `behavior.CardNo` comparisons in `CoreRuleEngine.cs`.
- `BalancedDiscipleOtherPowerDrawPlaySourceUsesCatalogEffectKind` blocks reintroducing `BalancedDiscipleCardNo` or direct `behavior.CardNo` comparisons in `CoreRuleEngine.cs`.
- Existing `P79RagingDrakeCreatesNextSpellCostReductionAfterResolution`, `P79RagingDrakeNextSpellCostReductionPromptShowsReducedSpellCost`, and `P79RagingDrakeNextSpellCostReductionPaysReducedSpellCostAndConsumesMarker` verify runtime and prompt behavior remains intact.
- Existing `P79PoroHerderGrantsBoonAndDrawsWhenControllerHasPoro` and `CoreRuleEnginePlaysBalancedDiscipleOtherPowerDraw` verify Poro Herder and Balanced Disciple runtime behavior remains intact.

## Verification

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~PlayBehaviorSourceIdentityGuardTests" --nologo
```

Initial results before implementation: failed on `RagingDrakeCardNo` in the first slice, then failed on `PoroHerderCardNo` and `BalancedDiscipleCardNo` in the follow-up slice.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~PlayBehaviorSourceIdentityGuardTests|FullyQualifiedName~P79PoroHerder|FullyQualifiedName~BalancedDisciple" --nologo
```

Result after implementation: 14/14 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~PoroHerder|FullyQualifiedName~BalancedDisciple|FullyQualifiedName~CoreRuleEnginePlaysVanillaSourceUnit|FullyQualifiedName~PlayBehaviorSourceIdentityGuardTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2156/2156 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery" --nologo
```

Result: 1989/1989 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8684/8684 passed.

## Non-Closure Statement

This evidence does not close complete play-trigger routing, complete `ORDER_TRIGGERS` / APNAP breadth, complete PaymentEngine breadth, complete LayerEngine breadth, frontend final validation, full official card matrix, or READY.
