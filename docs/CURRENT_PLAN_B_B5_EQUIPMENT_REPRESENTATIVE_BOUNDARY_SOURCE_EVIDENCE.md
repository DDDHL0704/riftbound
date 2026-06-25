# Plan B B5 Equipment Representative Boundary Source Evidence

日期：2026-06-25
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

This file records concrete evidence for removing duplicated equipment representative card-number helpers and routing those checks through a shared representative-boundary source table.

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
