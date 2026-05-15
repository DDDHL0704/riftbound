# Stage 4D-04A Keyword Deferred Surface Handoff

日期：2026-05-15
结论：**HANDOFF READY / PAUSE POINT / PROJECT NOT READY**

本 handoff 是 A 主控对 P1-002 keyword deferred surface 的下一批收口规格。本批只做 A-side audit / baseline / dispatch boundary，不实现 runtime，不修改 tests，不修改 frontend，不修改 card matrix，也不派发 B worker。目标是把下一枚 P1 规则模型切片从泛化的 "keywords deferred" 收窄到可验收的 equipment keyword status / execution-boundary 对齐任务。

## 1. Target

下一枚建议 B 切片：**4D-04B Equipment keyword execution-boundary status split**。

目标不是把装备关键词全量升级为 full official，而是先修正当前 profile 口径与现有执行边界之间的偏差：

- `CardEquipmentKeywordRules.BuildProfile` 当前对所有 assemble / agile / tempered / weapon surfaces 都返回 `recognized-deferred`。
- `MatchSession` 已有多批 `ASSEMBLE_EQUIPMENT` 服务端权威 profile、typed / any / experience / additional-cost representative 和 no-mutation guard。
- `CardEquipmentKeywordRules.BuildAttachmentProfile` 已把 Take Up attach / detach representative 单独标为 `implemented-representative`。
- 因此下一步应让 equipment keyword report 能区分 "已有代表性执行边界" 与 "仍 deferred 的 official breadth"，而不是继续把整族都压成单一 deferred 状态。

## 2. Input Facts

- `docs/CURRENT_SERVER_RULE_AUDIT.md` 的 P1-002 段落仍记录：equipment / interaction / combat / resource keyword profiles 存在 `recognized-deferred` 内部事实。
- `src/Riftbound.Engine/CardEquipmentKeywordRules.cs` 当前 reason 仍写明 assemble costs、agile reaction attachment、tempered optional attachment、static equipment modifiers 和 owner/controller execution remain deferred。
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs` 当前 equipment family baseline：
  - `装配`：32 entries / 31 functional units，profile deferred 32 / 31。
  - `灵便`：4 entries / 4 functional units，profile deferred 4 / 4。
  - `百炼`：16 entries / 11 functional units，profile deferred 16 / 11。
  - Take Up attachment profile 仍为 `implemented-representative`。
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` 已有 equipment representative fixtures covering no-attach play, assemble attach, assemble rejection, move-unit attached-equipment rejection, Take Up attach / detach and dirty-control rejection.
- 4D closure plan 已明确：4C evidence overlay 后，剩余工作应回到 P0/P1 规则模型、状态机和最终验收，不应继续用 evidence-only overlay 推进。

## 3. Suggested B Write Scope

默认写锁：

- `src/Riftbound.Engine/CardEquipmentKeywordRules.cs`
- `src/Riftbound.Engine/KeywordCoverageReporter.cs`（仅当 row/status 表达必须扩展时）
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs`

可选且仅最小必要：

- `src/Riftbound.Engine/MatchSession.cs` 中读取现有 implemented assemble profile 的只读 helper / query，不改装配 runtime semantics。
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` 中已有 equipment representative fixture anchor 的断言补强。

禁止触碰：

- frontend runtime / DevUi local rules
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- broad equipment runtime rewrite
- Azir / Maduli / Ezreal historical focused slices
- battle lifecycle, PaymentEngine broad refactor, LayerEngine broad implementation
- `riftbound-dotnet.sln`

## 4. Acceptance

最低验收：

1. Equipment keyword profile must distinguish known implemented representative assemble / attachment boundaries from still-deferred official breadth.
2. The slice must not mark `灵便` reaction attachment, `百炼` optional attachment, static equipment modifiers, copy-text effects, owner/controller changes, attach lifecycle breadth, or full equipment official coverage as complete unless the implementation really covers them and adds focused tests.
3. Coverage tests must fail if the profile collapses all equipment keywords into implemented status or hides remaining deferred rows.
4. Existing Take Up attachment representative must remain `implemented-representative`.
5. Existing equipment fixture suite must remain green.
6. Completion docs must continue to say `P1-002 remains open`, `fullOfficial=false`, and project `NOT READY`.

## 5. Suggested Verification

Implementation-before baseline is recorded in `docs/CURRENT_STAGE4D_04A_KEYWORD_DEFERRED_SURFACE_BASELINE_EVIDENCE.md`.

Post-implementation focused baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~KeywordCoverageReportExposesDeferredKeywordFamilies|FullyQualifiedName~P6EquipmentKeywordFamiliesReportSpecAndExecutionBoundaryCoverage|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~P4EquipmentAttachmentProfileMapsTakeUpToRepresentativeAttachDetach"
```

Adjacent fixture regression:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P4EquipmentKeywordProfilesKeepExistingNoAttachFixturesGreen|FullyQualifiedName~P4EquipmentAttachmentRepresentativeKeepsTakeUpAttachDetachFixturesGreen|FullyQualifiedName~AssembleEquipment"
```

Broader keyword guard:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~KeywordCoverageReportExposesDeferredKeywordFamilies|FullyQualifiedName~P6InteractionKeywordFamiliesReportSpecAndExecutionBoundaryCoverage|FullyQualifiedName~P6EquipmentKeywordFamiliesReportSpecAndExecutionBoundaryCoverage|FullyQualifiedName~P6ResourceAndExperienceFamiliesReportSpecAndExecutionBoundaryCoverage|FullyQualifiedName~P4CombatKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~P4ResourceKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~P4InteractionKeywordProfilesMapOfficialTextToRegistryTags"
```

Final local hygiene:

```sh
git diff --check
```

## 6. Non-Goals

- Do not close all P1 keywords in one slice.
- Do not upgrade any card matrix row to `fullOfficial=true`.
- Do not erase deferred statuses to make coverage look better.
- Do not implement broad LayerEngine or continuous effect replacement.
- Do not run frontend final validation as a proxy for P1 closure.
- Do not call `update_goal complete`.

## 7. Handoff Verdict

4D-04A is accepted as a P1-002 planning / baseline slice. It reorients the next work away from matrix evidence overlays and toward keyword execution-boundary status alignment, with equipment as the first safe slice. No B worker is dispatched in this batch; no runtime, test, frontend or matrix write lock remains open; project remains **NOT READY**.
