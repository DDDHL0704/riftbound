# Plan B B5 Equipment Representative Boundary Source Evidence

日期：2026-06-25
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

This file records concrete evidence for removing duplicated equipment representative card-number helpers and routing those checks through a shared representative-boundary source table.

## 2026-06-30 Supplement Evidence: BehaviorSpec-Derived Agile, Tempered, and Friendly-Equipment Boundaries

- `CardEquipmentKeywordRules.EquipmentRepresentativeBoundaries` now exposes a lazy merged boundary set rather than a single hand-written table.
- `BuildBehaviorSpecRepresentativeBoundaries()` loads `data/official/card-catalog.zh-CN.json`, builds functional units and implemented behavior metadata, and calls `BehaviorSpecCatalogBuilder.Build(...)`.
- `AgileDirectPlayAttach` boundaries are derived when the official `BehaviorSpec` is an equipment card with an own `{{灵便}}` line, the implemented behavior plays the source as equipment, `SourceEquipmentTags` includes `灵便`, and `AssembleEquipmentProfileCatalog.HasImplementedRepresentative(cardNo)` is true.
- `TemperedOptionalAttach` boundaries are derived when the official `BehaviorSpec` has an own `{{百炼}}` line and the implemented behavior plays the source as a unit with `SourceUnitTags` including `百炼`.
- `TemperedOptionalAttachEquipment` boundaries are derived when the official `BehaviorSpec` is an equipment card, the implemented behavior plays the source as equipment, `SourceEquipmentTags` includes `武装`, and `AssembleEquipmentProfileCatalog.HasImplementedRepresentative(cardNo)` is true.
- `FriendlyEquipmentStaticPower` boundaries are derived when the implemented behavior plays the source as a unit and `BehaviorSpec.StaticAuras` contains `StaticAuraKinds.FriendlyFieldEquipmentCountToSourceUnitPower`.
- The previous explicit `AgileDirectPlayAttach` rows for `SFD·022/221`, `SFD·056/221`, `SFD·064/221`, and `SFD·186/221` were removed.
- The previous explicit `TemperedOptionalAttach` rows for `SFD·002/221`, `SFD·008/221`, `SFD·119/221`, and `SFD·119a/221` were removed.
- The previous explicit `TemperedOptionalAttachEquipment` row for `SFD·186/221` was removed; `SFD·022/221` 长剑 now also satisfies the same derived equipment choice boundary.
- The previous explicit `FriendlyEquipmentStaticPower` rows for `SFD·085/221` and `SFD·085a/221` were removed.
- `EquipmentState` remains derived from `EquipmentStateRepresentatives` verifier metadata, not from a runtime card-number helper.
- `EquipmentKeywordRepresentativeBoundaryGuardTests.EquipmentKeywordRepresentativeBoundariesUseBehaviorSpecDerivedRowsNotCardNumberSourceRows` now requires `BehaviorSpecCatalogBuilder.Build(...)`, blocks reintroducing removed Agile / Tempered / friendly-equipment static-power explicit rows, and verifies the expected positive / negative runtime boundary queries.
- `TemperedEquipmentOptionalAttachTests` now verifies prompt exposure and successful stack resolution for both `SFD·186/221` 旋转飞斧 and `SFD·022/221` 长剑 as BehaviorSpec-derived weapon equipment choices.
- Validation passed: focused guard red/green 1/1; focused guard / Tempered / Jax / Armed Assaulter / catalog profile 64/64; Akshan / Tempered / guard focused 48/48; EquipmentKeyword / TemperedEquipment / JaxTempered / ArmedAssaulterHasteTempered / AgileEquipment / Assemble / Akshan / MatchRecovery / CardCatalogBaseline / PaymentEngine adjacent 3356/3356; backend full conformance 9052/9052.
- Non-closure: this does not close full Agile reaction timing, full Tempered official breadth, owner/controller breadth, attach lifecycle breadth, copy-text effects, LayerEngine, frontend final validation, full official, or READY.

## 2026-06-28 Supplement: Tempered Attach Equipment Choice Boundary

- `EquipmentRepresentativeBoundaryKinds.TemperedOptionalAttachEquipment` now identifies equipment cards that may be selected by the existing Tempered optional attach representative path.
- `CardEquipmentKeywordRules.CanBeTemperedOptionalAttachEquipment(cardNo)` routes that choice check through `HasRepresentativeBoundary`.
- At the time of this slice, `SFD·186/221` remained the only implemented representative equipment source row for this narrow path; the 2026-06-30 BehaviorSpec follow-up supersedes that row source.
- `ActionPromptBuilder.IsPromptTemperedOptionalAttachChoice` now calls `CanBeTemperedOptionalAttachEquipment` instead of comparing against a `SpinningAxeCardNo` runtime constant.
- `CoreRuleEngine.IsLegalTemperedOptionalAttachChoice` uses the same helper during command validation and stack-resolution revalidation.
- `EquipmentKeywordRepresentativeBoundaryGuardTests.TemperedOptionalAttachEquipmentChoiceDoesNotUseRuntimeSpinningAxeCardNumberConstant` blocks reintroducing `SpinningAxeCardNo` in `CoreRuleEngine` or `MatchSession`.
- Validation passed: focused guard / Tempered / Jax / Armed Assaulter representative 62/62; EquipmentKeyword / TemperedEquipment / JaxTempered / ArmedAssaulterHasteTempered / AgileEquipment / AssembleEquipment / Akshan / MatchRecovery / CardCatalogBaseline adjacent 2506/2506; backend full 8867/8867.
- Non-closure at the time of that slice: it did not expand legal equipment choices beyond the existing Spinning Axe representative row. The 2026-06-30 BehaviorSpec follow-up supersedes that narrow row source by deriving implemented weapon equipment choices such as Long Sword; full Tempered official breadth, owner/controller breadth, attach lifecycle breadth, copy-text effects, LayerEngine, frontend final validation, full official, and READY remain open.

## 2026-06-28 Follow-up Evidence: Sentinel Adept Source Boundary Constant Cleanup

- `CoreRuleEngine` and `MatchSession` no longer define `SentinelAdeptCardNo`.
- Sentinel Adept remains covered by `CardEquipmentKeywordRules.EquipmentRepresentativeBoundaries` row `SFD·008/221` / `EquipmentRepresentativeBoundaryKinds.TemperedOptionalAttach`.
- Core and ActionPromptBuilder continue to consume that source row through `HasTemperedOptionalAttachRepresentativeBoundary(...)`.
- `EquipmentKeywordRepresentativeBoundaryGuardTests.TemperedOptionalAttachSourceBoundaryDoesNotUseRuntimeSentinelAdeptCardNumberConstant` blocks reintroducing `SentinelAdeptCardNo` in `CoreRuleEngine` or `MatchSession`.
- Validation passed for this follow-up: focused equipment boundary guard 3/3; EquipmentKeyword / TemperedEquipment / JaxTempered / ArmedAssaulterHasteTempered / AgileEquipment / AssembleEquipment / Akshan / MatchRecovery / CardCatalogBaseline adjacent 2509/2509.
- Non-closure: this removes dead runtime source data only; it does not expand legal Tempered cards, full attach lifecycle breadth, copy-text effects, LayerEngine, frontend final validation, full official, or READY.

## 1. Runtime Evidence

- `CardEquipmentKeywordRules.EquipmentRepresentativeBoundaries` now exposes `CardEquipmentRepresentativeBoundary` values keyed by `EquipmentRepresentativeBoundaryKinds`, with Agile / Tempered / friendly-equipment static-power values derived from `BehaviorSpec` and equipment-state values derived from verifier metadata.
- `CardEquipmentKeywordRules.HasRepresentativeBoundary(cardNo, boundaryKind)` is the shared source-row query.
- `CardEquipmentKeywordRules.HasAgileDirectPlayAttachRepresentativeBoundary` now routes Agile direct-play attach representative checks through `HasRepresentativeBoundary`.
- `CardEquipmentKeywordRules.HasTemperedOptionalAttachRepresentativeBoundary` now routes Tempered optional attach representative checks through `HasRepresentativeBoundary`.
- `CardEquipmentKeywordRules.HasFriendlyEquipmentStaticPowerRepresentativeBoundary` now routes Ornn friendly-equipment static power representative checks through `HasRepresentativeBoundary`.
- `CardEquipmentKeywordRules.TryGetEquipmentStateRepresentative` now gates Long Sword equipment-state fixture lookup through the `EquipmentState` boundary row before returning the verifier metadata.
- `CardEquipmentKeywordRules.BuildProfile` now consumes the new representative-boundary queries for Agile, Tempered, friendly-equipment static power, and equipment-state profile flags.
- `CoreRuleEngine.IsAgileDirectPlayAttachRepresentative` now calls `HasAgileDirectPlayAttachRepresentativeBoundary`.
- `CoreRuleEngine.IsTemperedOptionalAttachRepresentative` now calls `HasTemperedOptionalAttachRepresentativeBoundary`.
- `ActionPromptBuilder.IsAgileDirectPlayAttachRepresentative` now calls `HasAgileDirectPlayAttachRepresentativeBoundary`.
- `ActionPromptBuilder.IsTemperedOptionalAttachRepresentative` now calls `HasTemperedOptionalAttachRepresentativeBoundary`.
- The previous `IsAgileDirectPlayAttachRepresentativeCardNo`, `IsTemperedOptionalAttachRepresentativeCardNo`, `IsFriendlyEquipmentStaticPowerRepresentativeCardNo`, and `IsEquipmentStateRepresentativeCardNo` helpers were deleted.
- Existing source-control checks, target extraction, payment, events, prompts, and snapshot shape are unchanged.

## 2. Test Evidence

Focused test files:

- `tests/Riftbound.ConformanceTests/EquipmentKeywordRepresentativeBoundaryGuardTests.cs`
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs`

Coverage:

- `EquipmentKeywordRepresentativeBoundariesUseBehaviorSpecDerivedRowsNotCardNumberSourceRows` blocks reintroducing the four old `Is*CardNo` helper names in `CardEquipmentKeywordRules.cs`.
- The same guard requires `EquipmentRepresentativeBoundaryKinds.AgileDirectPlayAttach`, `HasRepresentativeBoundary`, and `TryGetEquipmentStateRepresentative`.
- The same guard verifies positive and negative source-row checks for Agile direct-play attach, Tempered optional attach, Tempered attach equipment, friendly-equipment static power, and Long Sword equipment-state representative metadata.
- `TemperedEquipmentOptionalAttachTests.LegalTemperedOptionalAttachAcceptsBehaviorSpecWeaponEquipment` verifies that Long Sword can be selected by the Tempered optional attach prompt and attaches on stack resolution without using a card-specific runtime branch.
- `AkshanGuardTests.AkshanCanPayTemperedAttachAndOrangeStealTogether` verifies that a source with both `百炼` and source-steal optional-cost behavior can pay one `TEMPERED_ATTACH:*` and one `AKSHAN_STEAL_EQUIPMENT:*` in the same play command.
- `P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags` continues to verify the profile flags for Long Sword, Sentinel Adept, Armed Assaulter, Jax, and Ornn.
- `P5EquipmentStateAssembleLongSwordOwnerControllerFixtureProfileBindsExistingVerifierAnchors` now verifies Long Sword state metadata through `TryGetEquipmentStateRepresentative`.

## 3. Rule Evidence

- `data/official/card-catalog.zh-CN.json` is the card-text source for this slice.
- `SFD·022/221`, `SFD·056/221`, `SFD·064/221`, and `SFD·186/221` expose `{{灵便}}` equipment text and remain the implemented Agile direct-play attach representatives.
- `SFD·002/221`, `SFD·008/221`, `SFD·119/221`, `SFD·119a/221`, `SFD·085/221`, and `SFD·085a/221` expose `{{百炼}}` unit text; the implemented optional attach representative boundaries are now derived from that text plus implemented source-unit behavior tags.
- `SFD·022/221`, `SFD·056/221`, `SFD·064/221`, and `SFD·186/221` expose implemented weapon-equipment behavior with representative assemble profiles; they satisfy the derived `TemperedOptionalAttachEquipment` choice boundary.
- `SFD·085/221` and `SFD·085a/221` expose the friendly-equipment static power text; the implemented profile boundary remains unchanged.
- `SFD·022/221` Long Sword remains the equipment-state owner/controller/attachment lifecycle fixture representative.

## 4. Verification

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~EquipmentKeywordRepresentativeBoundaryGuardTests"
```

Result: failed before implementation because the old explicit Tempered source rows were still present.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~EquipmentKeywordRepresentativeBoundaryGuardTests|FullyQualifiedName~TemperedEquipmentOptionalAttachTests|FullyQualifiedName~JaxTemperedOptionalAttachTests|FullyQualifiedName~ArmedAssaulterHasteTemperedTests|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags"
```

Result: 64/64 passed after implementation.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~AkshanGuardTests|FullyQualifiedName~TemperedEquipmentOptionalAttachTests|FullyQualifiedName~EquipmentKeywordRepresentativeBoundaryGuardTests"
```

Result: 48/48 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~EquipmentKeyword|FullyQualifiedName~TemperedEquipment|FullyQualifiedName~JaxTempered|FullyQualifiedName~ArmedAssaulterHasteTempered|FullyQualifiedName~AgileEquipment|FullyQualifiedName~Assemble|FullyQualifiedName~Akshan|FullyQualifiedName~MatchRecovery|FullyQualifiedName~CardCatalogBaseline|FullyQualifiedName~PaymentEngine"
```

Result: 3356/3356 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj
```

Result: 9052/9052 passed.

## 5. Helper Count

After this slice, this source-only helper declaration check reports no matches:

```sh
rg -n "\\b(?:private|public|internal|protected)?\\s*static\\s+bool\\s+Is[A-Za-z0-9]+CardNo\\s*\\(" src/Riftbound.Engine src/Riftbound.CardCatalog src/Riftbound.Contracts
```

## 6. Non-Closure Statement

This evidence does not close full Agile, Tempered, weapon, equipment static modifier, copy-text, attach lifecycle, owner/controller, full card matrix, frontend final validation, or READY status.
