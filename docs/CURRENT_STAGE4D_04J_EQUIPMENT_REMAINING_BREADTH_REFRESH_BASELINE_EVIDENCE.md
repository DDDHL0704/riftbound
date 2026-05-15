# Stage 4D-04J Equipment Remaining Breadth Refresh Baseline Evidence

日期：2026-05-15
结论：**BASELINE RECORDED / PROJECT NOT READY**

本文件记录 4D-04J A-side handoff 的 implementation-before baseline。此 baseline 只证明当前 equipment keyword / P5 state representative anchors 是绿色的，不代表下一批已经实现，不关闭 P1-001、P1-002、LayerEngine、full equipment official coverage、frontend final validation、card matrix 或 READY。

## 1. Scope

本 baseline 覆盖：

- Equipment keyword deferred / implemented representative report guard.
- P5 equipment state owner/controller representative.
- P5 attached equipment follows host representative.
- P5 host destroyed detaches / recalls equipment representative.

本 baseline 不覆盖：

- Full `百炼` breadth.
- Agile reaction timing or Jax-granted Agile.
- Full owner/controller matrix across all zones and control changes.
- Full attach lifecycle, replacement, text-disabled while attached, or all equipment movement.
- Other equipment static modifiers or full LayerEngine.
- Frontend runtime, card matrix JSON, `fullOfficial`, or READY.

## 2. Commands

Focused state / keyword guard:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P5EquipmentStateAssembleLongSwordPreservesOwnerControllerAndAttachment|FullyQualifiedName~P5EquipmentStateAssembleLongSwordRejectsControllerMismatchWithoutSideEffects|FullyQualifiedName~P5EquipmentStateAssembleLongSwordAllowsControlledOpponentOwnedTarget|FullyQualifiedName~P5MoveUnitCommandMovesExplicitAttachedEquipmentWithHostToBattlefield|FullyQualifiedName~P5MoveUnitCommandMovesExplicitAttachedEquipmentWithHostToBase|FullyQualifiedName~CoreRuleEngineDetachesEquipmentWhenHostUnitIsDestroyed|FullyQualifiedName~P5EquipmentStateAssembleLongSwordOwnerControllerFixture|FullyQualifiedName~P5MoveUnitCommandAttachedEquipmentFollowsHostFixture|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~P6EquipmentKeywordFamiliesReportSpecAndExecutionBoundaryCoverage|FullyQualifiedName~KeywordCoverageReportExposesDeferredKeywordFamilies"
```

Result:

```text
Passed! - Failed: 0, Passed: 11, Skipped: 0, Total: 11
```

## 3. Baseline Interpretation

- The existing P5 equipment state representatives are alive and can be referenced by the next handoff:
  - Long Sword attach preserves explicit owner/controller identity and `attachedToObjectId`.
  - Controller mismatch rejects without side effects.
  - A unit owned by the opponent but controlled by the active player can be a legal attach target in the representative path.
  - Explicitly owned/controlled attached equipment follows its host between base and battlefield.
  - Destroyed host detaches / recalls the equipment to owner base.
- The equipment keyword coverage report remains conservative and still exposes deferred equipment breadth.
- This evidence is intentionally not a full backend pass and not a final acceptance gate.

## 4. Closure

4D-04J is docs-only. It does not modify runtime, tests, frontend, card matrix JSON, or `riftbound-dotnet.sln`. Project remains **NOT READY**.
