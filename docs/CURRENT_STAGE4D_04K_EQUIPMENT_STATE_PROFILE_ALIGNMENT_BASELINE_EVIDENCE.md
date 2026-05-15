# Stage 4D-04K Equipment State Profile Alignment Baseline Evidence

日期：2026-05-15
结论：**BASELINE RECORDED / PROJECT NOT READY**

本文件记录 4D-04K handoff 的 implementation-before baseline。此 baseline 只证明当前 P5 equipment state anchors 与 adjacent equipment representatives 在最新工作树中为绿色，不代表 4D-04K 已实现，不关闭 P1-001、P1-002、LayerEngine、full equipment official coverage、frontend final validation、card matrix 或 READY。

## 1. Scope

Focused baseline 覆盖：

- Equipment keyword deferred / implemented representative report guard.
- P5 Long Sword owner/controller / attachment invariant.
- P5 controller mismatch no-mutation rejection.
- P5 controlled opponent-owned target attach representative.
- P5 attached equipment follows host base <-> battlefield.
- P5 host destroyed detach / recall representative.

Adjacent baseline 覆盖：

- Existing no-attach equipment fixtures.
- Take Up attach / detach representative.
- Assemble equipment representative and rejection guards.
- Agile direct-play attach representatives.
- Tempered optional attach representatives.
- Jax, Akshan, Armed Assaulter and Ornn equipment-adjacent representatives.

This baseline does not cover full equipment official behavior, full LayerEngine, frontend final validation, Chrome smoke, formal 18-step E2E or card matrix completion.

## 2. Commands And Results

Focused state / profile guard:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P5EquipmentStateAssembleLongSwordPreservesOwnerControllerAndAttachment|FullyQualifiedName~P5EquipmentStateAssembleLongSwordRejectsControllerMismatchWithoutSideEffects|FullyQualifiedName~P5EquipmentStateAssembleLongSwordAllowsControlledOpponentOwnedTarget|FullyQualifiedName~P5MoveUnitCommandMovesExplicitAttachedEquipmentWithHostToBattlefield|FullyQualifiedName~P5MoveUnitCommandMovesExplicitAttachedEquipmentWithHostToBase|FullyQualifiedName~CoreRuleEngineDetachesEquipmentWhenHostUnitIsDestroyed|FullyQualifiedName~P5EquipmentStateAssembleLongSwordOwnerControllerFixture|FullyQualifiedName~P5MoveUnitCommandAttachedEquipmentFollowsHostFixture|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~P6EquipmentKeywordFamiliesReportSpecAndExecutionBoundaryCoverage|FullyQualifiedName~KeywordCoverageReportExposesDeferredKeywordFamilies"
```

Result:

```text
Passed! - Failed: 0, Passed: 11, Skipped: 0, Total: 11
```

Adjacent equipment regression:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P4EquipmentKeywordProfilesKeepExistingNoAttachFixturesGreen|FullyQualifiedName~P4EquipmentAttachmentRepresentativeKeepsTakeUpAttachDetachFixturesGreen|FullyQualifiedName~AssembleEquipment|FullyQualifiedName~AgileEquipmentDirectPlayAttachTests|FullyQualifiedName~TemperedEquipmentOptionalAttachTests|FullyQualifiedName~JaxTemperedOptionalAttach|FullyQualifiedName~Akshan|FullyQualifiedName~ArmedAssaulterHasteTemperedTests|FullyQualifiedName~OrnnFriendlyEquipmentStaticPowerTests"
```

Result:

```text
Passed! - Failed: 0, Passed: 195, Skipped: 0, Total: 195
```

## 3. Baseline Interpretation

- The existing P5 equipment state representative set is green and safe to reference in the next profile/verifier slice.
- The adjacent equipment runtime and profile representatives remain green before any 4D-04K implementation diff.
- This evidence is deliberately narrower than backend full test and cannot support READY.

## 4. Closure

4D-04K is handoff-ready only. No runtime, test, frontend or matrix change has been made by this baseline batch. Project remains **NOT READY**.
