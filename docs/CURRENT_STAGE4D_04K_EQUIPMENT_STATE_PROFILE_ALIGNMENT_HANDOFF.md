# Stage 4D-04K Equipment State Profile Alignment Handoff

日期：2026-05-15
结论：**HANDOFF READY / PAUSE POINT / PROJECT NOT READY**

本 handoff 是 A 主控对 4D-04J 的下一步拆分。本批仍不要求 B 实现新的装备 runtime；目标是把已经存在的 P5 equipment state representatives 明确接入 equipment profile / verifier 口径，避免文档和 API 继续把所有 owner/controller / attach lifecycle 能力都描述成未覆盖，同时不掩盖 full official residual。

## 1. Target

下一枚建议 B 切片：**4D-04K Equipment state representative profile alignment / verifier**。

目标：

- 在 `CardEquipmentKeywordRules` / coverage tests 中承认已有 P5 equipment state representative boundaries：
  - Long Sword 装配后 owner/controller/attachedToObjectId 不变量。
  - controller mismatch no-mutation rejection。
  - controlled opponent-owned target 可由当前 controller 装配。
  - 显式 owner/controller 匹配的贴附装备随宿主 base <-> battlefield 移动。
  - 宿主被摧毁时装备 detach / recall 到 owner base。
- 保持 `灵便` reaction timing、Jax-granted Agile、full `百炼` breadth、其他 equipment static modifiers、copy-text effects、cross-controller / cross-zone breadth、complete attach lifecycle、LayerEngine 与 full equipment official coverage deferred。
- 不改变 card matrix JSON，不升级 `fullOfficial`，不改变 frontend 行为。

## 2. Input Facts

- 当前分支为 `main`，工作树只允许保留未跟踪 `riftbound-dotnet.sln`；该文件不得触碰、暂存或提交。
- 4D-04J 已记录 remaining breadth refresh，入口为 `docs/CURRENT_STAGE4D_04J_EQUIPMENT_REMAINING_BREADTH_REFRESH_HANDOFF.md`。
- `CardEquipmentKeywordProfile` 当前只显式记录 assemble / Agile direct-play attach / Tempered optional attach / Ornn friendly-equipment static power representative boundary。
- `CardEquipmentKeywordRules.EquipmentKeywordReason` 当前仍把 `owner/controller changes` 与 `attach lifecycle breadth` 作为未拆分 residual。
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` 当前已有 P5 equipment state anchors，但这些 anchors 没有被 equipment keyword profile 口径显式引用。

## 3. Suggested B Write Scope

默认写锁：

- `src/Riftbound.Engine/CardEquipmentKeywordRules.cs`
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs`

可选且仅最小必要：

- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`，仅用于新增或调整一个 manifest / verifier，绑定现有 P5 equipment state anchors。
- `src/Riftbound.Engine/KeywordCoverageReporter.cs`，仅当 reporter 需要暴露新增 representative-boundary metadata 时触碰。

禁止触碰：

- `src/Riftbound.Engine/CoreRuleEngine.cs` runtime semantics，除非 A 另开新 runtime 写锁。
- frontend runtime / DevUi local rules.
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.
- broad equipment rewrite, broad LayerEngine rewrite, PaymentEngine, battle lifecycle, hidden-info redaction.
- full-card matrix status、`fullOfficial`、Stage 4B count.
- `riftbound-dotnet.sln`.

## 4. Required Behavior

1. Profile or verifier output must explicitly distinguish P5 equipment state representative coverage from full equipment official completion.
2. Existing P5 equipment state tests must be discoverable from the verifier / profile test so future refactors cannot silently drop them.
3. Long Sword / weapon / Agile equipment rows must remain `recognized-deferred` while explaining that some state representatives exist.
4. Existing assemble-only representative rows must not be downgraded.
5. Coverage report must keep deferred equipment rows visible; counts may only change if the tests document exactly why and still prove deferred breadth is present.
6. No runtime, frontend, matrix or full-official status changes are allowed in this slice.

## 5. Acceptance

Minimum A-side acceptance after B diff:

1. Focused profile/verifier tests prove the new P5 representative metadata and the residual closure language.
2. Existing focused state/profile baseline remains green.
3. Adjacent equipment regression remains green.
4. `git diff --check` passes.
5. Completion docs continue to say `P1-001 remains open`, `P1-002 remains open`, project `NOT READY`.

## 6. Suggested Verification

Implementation-before baseline is recorded in `docs/CURRENT_STAGE4D_04K_EQUIPMENT_STATE_PROFILE_ALIGNMENT_BASELINE_EVIDENCE.md`.

Focused state / profile guard:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P5EquipmentStateAssembleLongSwordPreservesOwnerControllerAndAttachment|FullyQualifiedName~P5EquipmentStateAssembleLongSwordRejectsControllerMismatchWithoutSideEffects|FullyQualifiedName~P5EquipmentStateAssembleLongSwordAllowsControlledOpponentOwnedTarget|FullyQualifiedName~P5MoveUnitCommandMovesExplicitAttachedEquipmentWithHostToBattlefield|FullyQualifiedName~P5MoveUnitCommandMovesExplicitAttachedEquipmentWithHostToBase|FullyQualifiedName~CoreRuleEngineDetachesEquipmentWhenHostUnitIsDestroyed|FullyQualifiedName~P5EquipmentStateAssembleLongSwordOwnerControllerFixture|FullyQualifiedName~P5MoveUnitCommandAttachedEquipmentFollowsHostFixture|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~P6EquipmentKeywordFamiliesReportSpecAndExecutionBoundaryCoverage|FullyQualifiedName~KeywordCoverageReportExposesDeferredKeywordFamilies"
```

Adjacent equipment regression:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P4EquipmentKeywordProfilesKeepExistingNoAttachFixturesGreen|FullyQualifiedName~P4EquipmentAttachmentRepresentativeKeepsTakeUpAttachDetachFixturesGreen|FullyQualifiedName~AssembleEquipment|FullyQualifiedName~AgileEquipmentDirectPlayAttachTests|FullyQualifiedName~TemperedEquipmentOptionalAttachTests|FullyQualifiedName~JaxTemperedOptionalAttach|FullyQualifiedName~Akshan|FullyQualifiedName~ArmedAssaulterHasteTemperedTests|FullyQualifiedName~OrnnFriendlyEquipmentStaticPowerTests"
```

Final hygiene:

```sh
git diff --check
```

## 7. Non-Goals

- Do not implement new runtime equipment behavior.
- Do not claim full owner/controller or full attach lifecycle completion.
- Do not change frontend behavior.
- Do not update card matrix JSON or `fullOfficial`.
- Do not run frontend final validation as a proxy for P1 closure.
- Do not call `update_goal complete`.

## 8. Handoff Verdict

4D-04K is ready as a B implementation handoff, but no B worker is dispatched and no write lock is open until A explicitly opens it. This batch makes the next implementation slice small, auditable, and aligned with the real current evidence. Project remains **NOT READY**.
