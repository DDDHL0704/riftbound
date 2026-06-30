# Plan B Play Behavior Source Identity Catalog Source Evidence

日期：2026-06-27
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

This file records concrete evidence for removing direct Raging Drake, Poro Herder, Balanced Disciple, Crescent Guard, Ascended Believer, Sly Salamander, Rampaging Soul, Armed Assaulter, and Akshan card-number checks from current play-behavior representatives. The 2026-06-27 follow-up covers the Akshan orange-extra equipment-steal stack resolution source revalidation path. The 2026-06-29 follow-up further moves Crescent Guard's ready optional-cost selector and Akshan's orange-extra enemy-equipment steal selector from catalog effect ids to behavior fields. The 2026-06-30 follow-up moves Armed Assaulter's Haste + Tempered optional-cost selector from a runtime catalog effect id to Haste ready behavior fields plus the Tempered representative boundary, moves Ascended Believer / Sly Salamander / Rampaging Soul conditional source-unit power/tags selectors to conditional behavior fields, and moves Balanced Disciple's conditional source draw selector to source draw behavior fields.

## Runtime Evidence

- `CoreRuleEngine` now validates the Raging Drake play-behavior source branch through `behavior.EffectKind` using `RAGING_DRAKE_NEXT_SPELL_COST_PLAY_UNIT`.
- The runtime branch still creates the same until-end-of-turn marker id prefix `RAGING_DRAKE_NEXT_SPELL_COST_REDUCTION:<playerId>:<sourceObjectId>`.
- The runtime branch still emits `TRIGGER_RESOLVED` with `effectKind=RAGING_DRAKE_NEXT_SPELL_COST_REDUCTION` and amount 5.
- `CoreRuleEngine` now validates the Poro Herder boon/draw play-behavior source branch through `behavior.EffectKind` using `PORO_HERDER_NO_PORO_STATIC_PLAY_UNIT`.
- The Poro Herder branch still requires a controlled face-up Poro unit, grants boon to the source, and draws 1.
- `CoreRuleEngine` now validates the Balanced Disciple other-power draw play-behavior source branch through `SourceDrawConditionKind=OTHER_CONTROLLED_UNIT_POWER_AT_LEAST`, `SourceDrawRequiredOtherControlledUnitPower=5`, and `SourceDrawCount=1`.
- The Balanced Disciple branch still requires other controlled unit power total at least 5 and draws 1.
- `CoreRuleEngine` now validates the Crescent Guard ready optional-cost source branch through `SourceReadyAdditionalPowerCost`, `SourceReadyAdditionalPowerTrait`, and `SourceReadyConditionKind`.
- The Crescent Guard payment branch still requires controller played-spell turn memory, still parses `SPEND_POWER:purple:1`, and still rejects the optional cost without the service-authoritative spell memory.
- `MatchSession` now validates the Crescent Guard ActionPrompt optional-cost source branch through the same source-ready behavior fields.
- The Crescent Guard prompt branch still requires `PromptPlayerPlayedSpellThisTurn` through `CardSourceReadyConditionKinds.ControllerPlayedSpellThisTurn` and still exposes purple payment metadata only when current or recyclable purple resources can pay the optional cost.
- `CoreRuleEngine` now validates the Ascended Believer conditional power branch through `ConditionalSourceUnitConditionKind=CONTROLLER_PLAYED_FOUR_PLUS_COST_SPELL_THIS_TURN` and `ConditionalSourceUnitPowerBonus=4`.
- The Ascended Believer branch still requires `PlayerPlayedFourPlusCostSpellThisTurn` before granting +4 power.
- `CoreRuleEngine` now validates the Sly Salamander conditional power / keyword branch through `ConditionalSourceUnitConditionKind=CONTROLLER_GAINED_EXPERIENCE_THIS_TURN`, `ConditionalSourceUnitPowerBonus=1`, and `ConditionalSourceUnitTags=游走`.
- The Sly Salamander branch still requires `PlayerGainedExperienceThisTurn` before granting +1 power and roam.
- `CoreRuleEngine` now validates the Rampaging Soul conditional keyword branch through `ConditionalSourceUnitConditionKind=CONTROLLER_DISCARDED_HAND_CARD_THIS_TURN` and `ConditionalSourceUnitTags=强攻|游走`.
- The Rampaging Soul branch still requires `PlayerDiscardedHandCardThisTurn` before granting assault and roam.
- `CoreRuleEngine` now validates the Armed Assaulter haste / tempered optional-cost representative source branch through `PlaysSourceToBaseAsUnit`, `HasHasteReadyEntryCost(behavior)`, and `CardEquipmentKeywordRules.HasTemperedOptionalAttachRepresentativeBoundary(behavior.CardNo)`.
- The Armed Assaulter branch no longer contains `ArmedAssaulterHasteTemperedSourceEffectKind` or the `ARMED_ASSAULTER_PLAY_UNIT_NO_OPTIONAL_HASTE` runtime selector; it still defers Haste math and Tempered choice legality to the existing optional-cost and equipment-choice checks.
- `CoreRuleEngine` and `MatchSession` now validate the Akshan orange-extra optional-cost representative source branch through `SourceStealEnemyEquipmentAdditionalPowerCost`, `SourceStealEnemyEquipmentAdditionalPowerTrait`, and `SourceStealEnemyEquipmentOptionalCostPrefix`.
- `CoreRuleEngine.TryResolveSourceStealEnemyEquipment` now validates the post-entry source object against the current stack `behavior.CardNo` row and source steal behavior fields instead of a direct `AkshanCardNo` constant or runtime Akshan effect id.
- The Akshan branch still requires legal enemy equipment, available orange power, and the post-entry source object identity check before moving / controlling the selected equipment; the legacy event reason is supplied by `SourceStealEnemyEquipmentReason`.
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
- `BalancedDiscipleOtherPowerDrawPlaySourceUsesBehaviorFields` blocks reintroducing `BalancedDiscipleCardNo`, `BalancedDiscipleOtherPowerDrawSourceEffectKind`, `BALANCED_DISCIPLE_NO_OTHER_POWER_VANILLA_PLAY_UNIT`, or direct `behavior.CardNo` comparisons in `CoreRuleEngine.cs`, and requires the source draw behavior fields.
- `CrescentGuardReadyOptionalCostSourceUsesBehaviorFields` blocks reintroducing `CrescentGuardCardNo`, `CrescentGuardReadyOptionalCostSourceEffectKind`, `CRESCENT_GUARD_NO_SPELL_VANILLA_PLAY_UNIT`, or direct `behavior.CardNo` comparisons in `CoreRuleEngine.cs` and `MatchSession.cs`, and requires the source-ready behavior fields.
- `ConditionalSourceUnitPowerAndTagsUseCatalogEffectKind` blocks reintroducing `AscendedBelieverCardNo`, `SlySalamanderCardNo`, `RampagingSoulCardNo`, or direct `behavior.CardNo` comparisons in the conditional source-unit power / keyword branches.
- `OptionalCostRepresentativeSourcesUseBehaviorFieldsWhereAvailable` blocks reintroducing Armed Assaulter / Akshan direct `behavior.CardNo` comparisons, blocks reintroducing direct `akshanState.CardNo, AkshanCardNo`, blocks the Armed Assaulter and Akshan runtime effect id selectors, and requires the Haste + Tempered behavior/boundary path plus source steal behavior fields.
- Existing `P79RagingDrakeCreatesNextSpellCostReductionAfterResolution`, `P79RagingDrakeNextSpellCostReductionPromptShowsReducedSpellCost`, and `P79RagingDrakeNextSpellCostReductionPaysReducedSpellCostAndConsumesMarker` verify runtime and prompt behavior remains intact.
- Existing `P79PoroHerderGrantsBoonAndDrawsWhenControllerHasPoro` and `CoreRuleEnginePlaysBalancedDiscipleOtherPowerDraw` verify Poro Herder and Balanced Disciple runtime behavior remains intact.
- Existing `CoreRuleEnginePlaysCrescentGuardReadyAfterSpellPayment`, `CoreRuleEngineRejectsCrescentGuardReadyPaymentWithoutSpellMemory`, and `ActionPromptExposesCrescentGuardReadyPaymentAfterSpell` verify Crescent Guard payment, rejection, and prompt behavior remains intact.
- Existing Ascended Believer, Sly Salamander, and Rampaging Soul paired fixtures verify the conditional power / keyword behavior remains intact.
- Existing Armed Assaulter haste / tempered and Akshan guard focused tests verify the optional-cost representative behavior remains intact.

## Verification

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~PlayBehaviorSourceIdentityGuardTests" --nologo
```

Initial results before implementation: failed on `RagingDrakeCardNo` in the first slice, then failed on `PoroHerderCardNo` and `BalancedDiscipleCardNo` in the follow-up slice, then failed on `CrescentGuardCardNo` in the Crescent Guard slice, then failed on `AscendedBelieverCardNo` / `SlySalamanderCardNo` / `RampagingSoulCardNo` in the conditional-entry slice, then failed on `ArmedAssaulterCardNo` / `AkshanCardNo` behavior-source comparisons in the optional-cost representative slice, then failed on `akshanState.CardNo, AkshanCardNo` in the Akshan resolution source revalidation follow-up, then failed on `CrescentGuardReadyOptionalCostSourceEffectKind` in the source-ready optional-cost field follow-up, then failed on `AkshanOrangeExtraEquipmentStealSourceEffectKind` in the source steal enemy equipment optional-cost field follow-up, then failed on `ArmedAssaulterHasteTemperedSourceEffectKind` in the Haste + Tempered source guard follow-up, then failed on `BalancedDiscipleOtherPowerDrawSourceEffectKind` in the source draw behavior-field follow-up.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~OptionalCostRepresentativeSourcesUseBehaviorFieldsWhereAvailable" --nologo
```

Result: failed before implementation on direct `akshanState.CardNo, AkshanCardNo`, then later failed on `ArmedAssaulterHasteTemperedSourceEffectKind`, then 1/1 passed after implementation.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~PlayBehaviorSourceIdentityGuardTests|FullyQualifiedName~Akshan|FullyQualifiedName~ArmedAssaulter" --nologo
```

Result after implementation: 87/87 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo --filter "FullyQualifiedName~BalancedDiscipleOtherPowerDrawPlaySourceUsesBehaviorFields|FullyQualifiedName~BalancedDiscipleSourceDrawCarriesOfficialOtherPowerCondition|FullyQualifiedName~CoreRuleEnginePlaysBalancedDiscipleOtherPowerDraw"
```

Result after implementation: 3/3 passed; adjacent / hidden-info gate `PlayBehaviorSourceIdentityGuardTests|BalancedDisciple|CardCatalogBaselineTests|PaymentEngineCoverageAuditTests|MatchRecovery` passed 3024/3024; backend full conformance passed 9026/9026.

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
