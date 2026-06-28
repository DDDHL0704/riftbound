# Plan B B5 Equipment Representative Boundary Source Evidence

日期：2026-06-25
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

This file records concrete evidence for removing duplicated equipment representative card-number helpers and routing those checks through a shared representative-boundary source table.

## 2026-06-28 Supplement: Tempered Attach Equipment Choice Boundary

- `EquipmentRepresentativeBoundaryKinds.TemperedOptionalAttachEquipment` now identifies equipment cards that may be selected by the existing Tempered optional attach representative path.
- `CardEquipmentKeywordRules.CanBeTemperedOptionalAttachEquipment(cardNo)` routes that choice check through `HasRepresentativeBoundary`.
- `SFD·186/221` remains the only implemented representative equipment source row for this narrow path.
- `ActionPromptBuilder.IsPromptTemperedOptionalAttachChoice` now calls `CanBeTemperedOptionalAttachEquipment` instead of comparing against a `SpinningAxeCardNo` runtime constant.
- `CoreRuleEngine.IsLegalTemperedOptionalAttachChoice` uses the same helper during command validation and stack-resolution revalidation.
- `EquipmentKeywordRepresentativeBoundaryGuardTests.TemperedOptionalAttachEquipmentChoiceDoesNotUseRuntimeSpinningAxeCardNumberConstant` blocks reintroducing `SpinningAxeCardNo` in `CoreRuleEngine` or `MatchSession`.
- Validation passed: focused guard / Tempered / Jax / Armed Assaulter representative 62/62; EquipmentKeyword / TemperedEquipment / JaxTempered / ArmedAssaulterHasteTempered / AgileEquipment / AssembleEquipment / Akshan / MatchRecovery / CardCatalogBaseline adjacent 2506/2506; backend full 8867/8867.
- Non-closure: this does not expand legal equipment choices beyond the existing Spinning Axe representative row, and it does not close full Tempered official breadth, owner/controller breadth, attach lifecycle breadth, copy-text effects, LayerEngine, frontend final validation, full official, or READY.

## 2026-06-28 Follow-up Evidence: Sentinel Adept Source Boundary Constant Cleanup

- `CoreRuleEngine` and `MatchSession` no longer define `SentinelAdeptCardNo`.
- Sentinel Adept remains covered by `CardEquipmentKeywordRules.EquipmentRepresentativeBoundaries` row `SFD·008/221` / `EquipmentRepresentativeBoundaryKinds.TemperedOptionalAttach`.
- Core and ActionPromptBuilder continue to consume that source row through `HasTemperedOptionalAttachRepresentativeBoundary(...)`.
- `EquipmentKeywordRepresentativeBoundaryGuardTests.TemperedOptionalAttachSourceBoundaryDoesNotUseRuntimeSentinelAdeptCardNumberConstant` blocks reintroducing `SentinelAdeptCardNo` in `CoreRuleEngine` or `MatchSession`.
- Validation passed for this follow-up: focused equipment boundary guard 3/3; EquipmentKeyword / TemperedEquipment / JaxTempered / ArmedAssaulterHasteTempered / AgileEquipment / AssembleEquipment / Akshan / MatchRecovery / CardCatalogBaseline adjacent 2509/2509.
- Non-closure: this removes dead runtime source data only; it does not expand legal Tempered cards, full attach lifecycle breadth, copy-text effects, LayerEngine, frontend final validation, full official, or READY.

## 1. Runtime Evidence

- `CardEquipmentKeywordRules.EquipmentRepresentativeBoundaries` now defines `CardEquipmentRepresentativeBoundary` rows keyed by `EquipmentRepresentativeBoundaryKinds`.
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

- `EquipmentKeywordRepresentativeBoundariesUseSourceRowsNotCardNumberHelpers` blocks reintroducing the four old `Is*CardNo` helper names in `CardEquipmentKeywordRules.cs`.
- The same guard requires `EquipmentRepresentativeBoundaryKinds.AgileDirectPlayAttach`, `HasRepresentativeBoundary`, and `TryGetEquipmentStateRepresentative`.
- The same guard verifies positive and negative source-row checks for Agile direct-play attach, Tempered optional attach, friendly-equipment static power, and Long Sword equipment-state representative metadata.
- `P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags` continues to verify the profile flags for Long Sword, Sentinel Adept, Armed Assaulter, Jax, and Ornn.
- `P5EquipmentStateAssembleLongSwordOwnerControllerFixtureProfileBindsExistingVerifierAnchors` now verifies Long Sword state metadata through `TryGetEquipmentStateRepresentative`.

## 3. Rule Evidence

- `data/official/card-catalog.zh-CN.json` is the card-text source for this slice.
- `SFD·022/221`, `SFD·056/221`, `SFD·064/221`, and `SFD·186/221` expose `{{灵便}}` equipment text and remain the implemented Agile direct-play attach representatives.
- `SFD·002/221`, `SFD·008/221`, `SFD·119/221`, `SFD·119a/221`, `SFD·085/221`, and `SFD·085a/221` expose `{{百炼}}` unit text; the implemented optional attach representative rows remain unchanged.
- `SFD·085/221` and `SFD·085a/221` expose the friendly-equipment static power text; the implemented profile boundary remains unchanged.
- `SFD·022/221` Long Sword remains the equipment-state owner/controller/attachment lifecycle fixture representative.

## 4. Verification

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~EquipmentKeywordRepresentativeBoundaryGuardTests"
```

Result: failed before implementation because the new source-row APIs did not exist.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~EquipmentKeywordRepresentativeBoundaryGuardTests|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~P5EquipmentStateAssembleLongSwordOwnerControllerFixtureProfileBindsExistingVerifierAnchors"
```

Result: 3/3 passed after implementation.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~EquipmentKeyword|FullyQualifiedName~AgileEquipment|FullyQualifiedName~TemperedEquipment|FullyQualifiedName~OrnnFriendlyEquipment|FullyQualifiedName~EquipmentState|FullyQualifiedName~Assemble|FullyQualifiedName~FullGameEndToEnd"
```

Result: 223/223 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"
```

Result: 1989/1989 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj
```

Result: 8585/8585 passed.

## 5. Helper Count

After this slice, this source-only helper declaration check reports no matches:

```sh
rg -n "\\b(?:private|public|internal|protected)?\\s*static\\s+bool\\s+Is[A-Za-z0-9]+CardNo\\s*\\(" src/Riftbound.Engine src/Riftbound.CardCatalog src/Riftbound.Contracts
```

## 6. Non-Closure Statement

This evidence does not close full Agile, Tempered, weapon, equipment static modifier, copy-text, attach lifecycle, owner/controller, full card matrix, frontend final validation, or READY status.
