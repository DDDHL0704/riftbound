# Stage 4D-04J Equipment Remaining Breadth Refresh Handoff

日期：2026-05-15
结论：**HANDOFF READY / PAUSE POINT / PROJECT NOT READY**

本文件是 A 主控在 4D-04I-B 之后对 equipment 剩余面的刷新与下一批路由。它只做 planning / baseline / dispatch boundary，不实现 runtime，不修改 tests，不修改 frontend，不修改 card matrix，也不派发新的 B worker。目标是把 P1-001 / P1-002 中的 equipment residual 从泛化列表，收窄到下一枚可验收的 profile / verifier 或窄实现切片。

## 1. Target

下一枚建议 B 切片：**4D-04K Equipment state representative profile alignment / verifier**。

目标不是把装备系统升级为 full official，而是先把已有代表路径与仍 deferred 的 official breadth 分开：

- 现有 `CardEquipmentKeywordRules` 已登记 assemble、printed Agile direct-play attach、Tempered optional attach、Ornn friendly-equipment static power 等 representative boundaries。
- 现有 P5 tests 已覆盖一部分 equipment state representative：Long Sword 装配后 owner/controller/attachedToObjectId 不变量、controller mismatch 拒绝、controlled opponent-owned target 可被当前 controller 装配、显式 owner/controller 匹配时贴附装备随宿主 base <-> battlefield 移动、宿主被摧毁时装备解除贴附并回到 owner base。
- 但 `CardEquipmentKeywordRules` 的 reason 仍把 `owner/controller changes` 与 `attach lifecycle breadth` 作为大块 residual 描述，没有把这些已存在的代表路径从 full breadth residual 中拆出来。
- 下一步应优先做 profile / verifier alignment：承认已有 P5 equipment state representatives，同时继续保留 reaction timing、Jax-granted Agile、full Tempered breadth、其他 static modifiers、copy-text effects、cross-controller / cross-zone breadth、complete attach lifecycle 和 full official coverage deferred。

## 2. Input Facts

- 当前分支为 `main`，当前工作树只允许保留未跟踪 `riftbound-dotnet.sln`；该文件不得触碰、暂存或提交。
- 4D-04I-B Ornn dynamic equipment static recompute 已验收并关锁，入口为 `docs/CURRENT_STAGE4D_04I_ORNN_DYNAMIC_EQUIPMENT_STATIC_RECOMPUTE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_04I_ORNN_DYNAMIC_EQUIPMENT_STATIC_RECOMPUTE_EVIDENCE.md`。
- `src/Riftbound.Engine/CardEquipmentKeywordRules.cs` 当前 reason 明确保留 `reaction-timing breadth`、`Jax-granted agile`、`remaining ephemeral/static equipment breadth`、`full tempered official breadth`、`copy-text effects`、`owner/controller changes`、`attach lifecycle breadth` 与 `full equipment official coverage` residual。
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` 已有 P5 representative anchors：
  - `P5EquipmentStateAssembleLongSwordPreservesOwnerControllerAndAttachment`
  - `P5EquipmentStateAssembleLongSwordRejectsControllerMismatchWithoutSideEffects`
  - `P5EquipmentStateAssembleLongSwordAllowsControlledOpponentOwnedTarget`
  - `P5MoveUnitCommandMovesExplicitAttachedEquipmentWithHostToBattlefield`
  - `P5MoveUnitCommandMovesExplicitAttachedEquipmentWithHostToBase`
  - `CoreRuleEngineDetachesEquipmentWhenHostUnitIsDestroyed`
  - `P5EquipmentStateAssembleLongSwordOwnerControllerFixture`
  - `P5MoveUnitCommandAttachedEquipmentFollowsHostFixture`
- `src/Riftbound.Engine/CoreRuleEngine.cs` 已有 `AttachedEquipmentObjectIds`、`CanMoveExplicitAttachedEquipmentWithHost`、`MoveAttachedEquipmentWithHost` 与 `DetachEquipmentFromRemovedHost` 等 helper / lifecycle paths，但这些并不等同完整 attachment lifecycle 或 LayerEngine。

## 3. Suggested B Write Scope

默认写锁：

- `src/Riftbound.Engine/CardEquipmentKeywordRules.cs`
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs`

可选且仅最小必要：

- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`，仅用于补一个 manifest / verifier，确认上述 P5 equipment state representative tests 仍被 coverage 口径引用。
- `src/Riftbound.Engine/KeywordCoverageReporter.cs`，仅当现有 row/status 无法表达 implemented representative + deferred breadth 并存时触碰。

禁止触碰：

- runtime semantics in `CoreRuleEngine.cs`，除非 B 通过 focused failing test 证明当前 P5 state path 有真实 bug，且 A 重新开窄 runtime 写锁。
- frontend runtime / DevUi local rules
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- broad equipment rewrite, broad LayerEngine rewrite, PaymentEngine, battle lifecycle, hidden-info redaction
- full-card matrix status、`fullOfficial`、Stage 4B count
- `riftbound-dotnet.sln`

## 4. Acceptance

1. Equipment profile / report language must distinguish existing P5 equipment state representatives from still-deferred official breadth.
2. Existing P4/P5 representative tests must stay green, including owner/controller identity, controller mismatch rejection, controlled opponent-owned target attach, explicit attached-equipment host movement, and host-destroy detach / recall.
3. The slice must not mark full owner/controller breadth, full attach lifecycle, Agile reaction timing, Jax-granted Agile, full Tempered breadth, other equipment static modifiers, copy-text effects, LayerEngine or full equipment official coverage as complete.
4. Coverage tests must fail if the profile collapses all equipment residuals into implemented status or hides remaining deferred rows.
5. Completion docs must continue to say `P1-001 remains open`, `P1-002 remains open`, `fullOfficial=false`, and project `NOT READY`.

## 5. Suggested Verification

Implementation-before baseline is recorded in `docs/CURRENT_STAGE4D_04J_EQUIPMENT_REMAINING_BREADTH_REFRESH_BASELINE_EVIDENCE.md`.

Focused state / keyword guard:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P5EquipmentStateAssembleLongSwordPreservesOwnerControllerAndAttachment|FullyQualifiedName~P5EquipmentStateAssembleLongSwordRejectsControllerMismatchWithoutSideEffects|FullyQualifiedName~P5EquipmentStateAssembleLongSwordAllowsControlledOpponentOwnedTarget|FullyQualifiedName~P5MoveUnitCommandMovesExplicitAttachedEquipmentWithHostToBattlefield|FullyQualifiedName~P5MoveUnitCommandMovesExplicitAttachedEquipmentWithHostToBase|FullyQualifiedName~CoreRuleEngineDetachesEquipmentWhenHostUnitIsDestroyed|FullyQualifiedName~P5EquipmentStateAssembleLongSwordOwnerControllerFixture|FullyQualifiedName~P5MoveUnitCommandAttachedEquipmentFollowsHostFixture|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~P6EquipmentKeywordFamiliesReportSpecAndExecutionBoundaryCoverage|FullyQualifiedName~KeywordCoverageReportExposesDeferredKeywordFamilies"
```

Adjacent equipment regression, if B touches tests or reporter:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P4EquipmentKeywordProfilesKeepExistingNoAttachFixturesGreen|FullyQualifiedName~P4EquipmentAttachmentRepresentativeKeepsTakeUpAttachDetachFixturesGreen|FullyQualifiedName~AssembleEquipment|FullyQualifiedName~AgileEquipmentDirectPlayAttachTests|FullyQualifiedName~TemperedEquipmentOptionalAttachTests|FullyQualifiedName~JaxTemperedOptionalAttach|FullyQualifiedName~Akshan|FullyQualifiedName~ArmedAssaulterHasteTemperedTests|FullyQualifiedName~OrnnFriendlyEquipmentStaticPowerTests"
```

Final hygiene:

```sh
git diff --check
```

## 6. Non-Goals

- Do not implement full equipment state system in this batch.
- Do not introduce a broad LayerEngine.
- Do not update card matrix JSON or any `fullOfficial` flag.
- Do not run frontend final validation as a proxy for P1 closure.
- Do not erase deferred statuses to make coverage look better.
- Do not call `update_goal complete`.

## 7. Handoff Verdict

4D-04J is accepted as an A-side refresh / handoff batch. It identifies the next safe slice as a profile / verifier alignment around already-existing P5 equipment state representatives, while keeping full equipment official breadth, P1-001, P1-002, frontend final validation, card matrix and READY open. No runtime, test, frontend or matrix write lock is open after this batch.
