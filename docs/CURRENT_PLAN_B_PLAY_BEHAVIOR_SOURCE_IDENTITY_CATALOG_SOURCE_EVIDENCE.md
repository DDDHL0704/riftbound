# Plan B Play Behavior Source Identity Catalog Source Evidence

日期：2026-06-27
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

This file records concrete evidence for removing direct Raging Drake, Poro Herder, Balanced Disciple, Crescent Guard, Ascended Believer, Sly Salamander, Rampaging Soul, Armed Assaulter, and Akshan card-number checks from current play-behavior representatives. The 2026-06-27 follow-up covers the Akshan orange-extra equipment-steal stack resolution source revalidation path.

## Runtime Evidence

- `CoreRuleEngine` now validates the Raging Drake play-behavior source branch through `behavior.EffectKind` using `RAGING_DRAKE_NEXT_SPELL_COST_PLAY_UNIT`.
- The runtime branch still creates the same until-end-of-turn marker id prefix `RAGING_DRAKE_NEXT_SPELL_COST_REDUCTION:<playerId>:<sourceObjectId>`.
- The runtime branch still emits `TRIGGER_RESOLVED` with `effectKind=RAGING_DRAKE_NEXT_SPELL_COST_REDUCTION` and amount 5.
- `CoreRuleEngine` now validates the Poro Herder boon/draw play-behavior source branch through `behavior.EffectKind` using `PORO_HERDER_NO_PORO_STATIC_PLAY_UNIT`.
- The Poro Herder branch still requires a controlled face-up Poro unit, grants boon to the source, and draws 1.
- `CoreRuleEngine` now validates the Balanced Disciple other-power draw play-behavior source branch through `behavior.EffectKind` using `BALANCED_DISCIPLE_NO_OTHER_POWER_VANILLA_PLAY_UNIT`.
- The Balanced Disciple branch still requires other controlled unit power total at least 5 and draws 1.
- `CoreRuleEngine` now validates the Crescent Guard ready optional-cost source branch through `behavior.EffectKind` using `CRESCENT_GUARD_NO_SPELL_VANILLA_PLAY_UNIT`.
- The Crescent Guard payment branch still requires `PlayerPlayedSpellThisTurn`, still parses `SPEND_POWER:purple:1`, and still rejects the optional cost without the service-authoritative spell memory.
- `MatchSession` now validates the Crescent Guard ActionPrompt optional-cost source branch through `behavior.EffectKind` using `CRESCENT_GUARD_NO_SPELL_VANILLA_PLAY_UNIT`.
- The Crescent Guard prompt branch still requires `PromptPlayerPlayedSpellThisTurn` and still exposes purple payment metadata only when current or recyclable purple resources can pay the optional cost.
- `CoreRuleEngine` now validates the Ascended Believer conditional power branch through `behavior.EffectKind` using `ASCENDED_BELIEVER_NO_SPELL_VANILLA_PLAY_UNIT`.
- The Ascended Believer branch still requires `PlayerPlayedFourPlusCostSpellThisTurn` before granting +4 power.
- `CoreRuleEngine` now validates the Sly Salamander conditional power / keyword branch through `behavior.EffectKind` using `SLY_SALAMANDER_NO_EXPERIENCE_VANILLA_PLAY_UNIT`.
- The Sly Salamander branch still requires `PlayerGainedExperienceThisTurn` before granting +1 power and roam.
- `CoreRuleEngine` now validates the Rampaging Soul conditional keyword branch through `behavior.EffectKind` using `RAMPAGING_SOUL_NO_DISCARD_SPIRIT_PLAY_UNIT`.
- The Rampaging Soul branch still requires `PlayerDiscardedHandCardThisTurn` before granting assault and roam.
- `CoreRuleEngine` now validates the Armed Assaulter haste / tempered optional-cost representative source branch through `behavior.EffectKind` using `ARMED_ASSAULTER_PLAY_UNIT_NO_OPTIONAL_HASTE`.
- The Armed Assaulter branch still requires a source unit and still defers the haste / tempered choice legality to the existing optional-cost and equipment-choice checks.
- `CoreRuleEngine` and `MatchSession` now validate the Akshan orange-extra optional-cost representative source branch through `behavior.EffectKind` using `AKSHAN_NO_OPTIONAL_ASSEMBLE_NO_EXTRA_PLAY_UNIT`.
- `CoreRuleEngine.TryResolveAkshanOrangeExtraEquipmentSteal` now validates the post-entry source object against the current stack `behavior.CardNo` row instead of a direct `AkshanCardNo` constant.
- The Akshan branch still requires legal enemy equipment, available orange power, and the post-entry source object identity check before moving / controlling the selected equipment.
- The previous `RagingDrakeCardNo`, `PoroHerderCardNo`, `BalancedDiscipleCardNo`, `CrescentGuardCardNo`, `AscendedBelieverCardNo`, `SlySalamanderCardNo`, `RampagingSoulCardNo`, `ArmedAssaulterCardNo`, and direct Akshan `behavior.CardNo` source comparisons were deleted from the relevant engine source paths.
- The previous Core `AkshanCardNo` constant and direct `akshanState.CardNo, AkshanCardNo` resolution check were deleted.

## Test Evidence

Focused test file:

- `tests/Riftbound.ConformanceTests/PlayBehaviorSourceIdentityGuardTests.cs`

Coverage:

- `CardBehaviorRegistryIdentifiesPlaySourceUnitsByEffectKind` accepts the registered `OGN·031/298`, `OGN·061/298`, `UNL-097/219`, `UNL-122/219`, `UNL-004/219`, `UNL-108/219`, `OGN·019/298`, `SFD·002/221`, and `SFD·109/221` source rows used by this slice.
- `CardBehaviorRegistryRejectsNonMatchingPlaySourceUnits` rejects wrong-card and wrong-effect source identity matches.
- `RagingDrakeNextSpellCostPlaySourceUsesCatalogEffectKind` blocks reintroducing `RagingDrakeCardNo` or direct `behavior.CardNo` comparisons in `CoreRuleEngine.cs`.
- `PoroHerderBoonDrawPlaySourceUsesCatalogEffectKind` blocks reintroducing `PoroHerderCardNo` or direct `behavior.CardNo` comparisons in `CoreRuleEngine.cs`.
- `BalancedDiscipleOtherPowerDrawPlaySourceUsesCatalogEffectKind` blocks reintroducing `BalancedDiscipleCardNo` or direct `behavior.CardNo` comparisons in `CoreRuleEngine.cs`.
- `CrescentGuardReadyOptionalCostSourceUsesCatalogEffectKind` blocks reintroducing `CrescentGuardCardNo` or direct `behavior.CardNo` comparisons in `CoreRuleEngine.cs` and `MatchSession.cs`.
- `ConditionalSourceUnitPowerAndTagsUseCatalogEffectKind` blocks reintroducing `AscendedBelieverCardNo`, `SlySalamanderCardNo`, `RampagingSoulCardNo`, or direct `behavior.CardNo` comparisons in the conditional source-unit power / keyword branches.
- `OptionalCostRepresentativeSourcesUseCatalogEffectKind` blocks reintroducing Armed Assaulter / Akshan direct `behavior.CardNo` comparisons in the optional-cost representative branches and blocks reintroducing direct `akshanState.CardNo, AkshanCardNo` in the Akshan resolution source revalidation branch.
- Existing `P79RagingDrakeCreatesNextSpellCostReductionAfterResolution`, `P79RagingDrakeNextSpellCostReductionPromptShowsReducedSpellCost`, and `P79RagingDrakeNextSpellCostReductionPaysReducedSpellCostAndConsumesMarker` verify runtime and prompt behavior remains intact.
- Existing `P79PoroHerderGrantsBoonAndDrawsWhenControllerHasPoro` and `CoreRuleEnginePlaysBalancedDiscipleOtherPowerDraw` verify Poro Herder and Balanced Disciple runtime behavior remains intact.
- Existing `CoreRuleEnginePlaysCrescentGuardReadyAfterSpellPayment`, `CoreRuleEngineRejectsCrescentGuardReadyPaymentWithoutSpellMemory`, and `ActionPromptExposesCrescentGuardReadyPaymentAfterSpell` verify Crescent Guard payment, rejection, and prompt behavior remains intact.
- Existing Ascended Believer, Sly Salamander, and Rampaging Soul paired fixtures verify the conditional power / keyword behavior remains intact.
- Existing Armed Assaulter haste / tempered and Akshan guard focused tests verify the optional-cost representative behavior remains intact.

## Verification

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~PlayBehaviorSourceIdentityGuardTests" --nologo
```

Initial results before implementation: failed on `RagingDrakeCardNo` in the first slice, then failed on `PoroHerderCardNo` and `BalancedDiscipleCardNo` in the follow-up slice, then failed on `CrescentGuardCardNo` in the Crescent Guard slice, then failed on `AscendedBelieverCardNo` / `SlySalamanderCardNo` / `RampagingSoulCardNo` in the conditional-entry slice, then failed on `ArmedAssaulterCardNo` / `AkshanCardNo` behavior-source comparisons in the optional-cost representative slice, then failed on `akshanState.CardNo, AkshanCardNo` in the Akshan resolution source revalidation follow-up.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~OptionalCostRepresentativeSourcesUseCatalogEffectKind" --nologo
```

Result: failed before implementation on direct `akshanState.CardNo, AkshanCardNo`, then 1/1 passed after implementation.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~PlayBehaviorSourceIdentityGuardTests|FullyQualifiedName~Akshan|FullyQualifiedName~ArmedAssaulter" --nologo
```

Result after implementation: 87/87 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Akshan|FullyQualifiedName~ArmedAssaulter|FullyQualifiedName~PlayBehaviorSourceIdentityGuardTests|FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~ConformanceFixtureShapeTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2916/2916 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~AkshanGuardTests|FullyQualifiedName~PlayBehaviorSourceIdentityGuardTests|FullyQualifiedName~ArmedAssaulterHasteTemperedTests|FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~ConformanceFixtureShapeTests" --nologo
```

Result: 926/926 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery" --nologo
```

Result: 1989/1989 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8771/8771 passed.

## Non-Closure Statement

This evidence does not close complete play-trigger routing, complete `ORDER_TRIGGERS` / APNAP breadth, complete PaymentEngine breadth, complete LayerEngine breadth, frontend final validation, full official card matrix, or READY.
