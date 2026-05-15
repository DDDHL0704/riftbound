# Stage 4D-04I Ornn Dynamic Equipment Static Recompute Baseline Evidence

日期：2026-05-15
结论：**IMPLEMENTATION-BEFORE BASELINE RECORDED / PROJECT NOT READY**

本文件记录 4D-04I 派发前的 A-side baseline。它不是实现验收；只证明 4D-04H entry-time representative 和相邻路径当前绿色，并锁定下一步要补的 dynamic recompute 缺口。

## 1. Baseline Facts

- 当前分支：`main`
- 最新提交：`c2dab522 feat: add ornn friendly equipment static power representative`
- 工作树基线：只保留未跟踪 `riftbound-dotnet.sln`，不得触碰。
- 4D-04H 已证明 Ornn 从手牌入场时按当前友方公开 field equipment count 增加战力。
- 当前缺口：4D-04H 的 helper 只在 `PlaySourceUnitToBase` / `PlaySourceUnitToBattlefield` entry resolution 中计算一次；Ornn 已在场后，装备数量变化的 dynamic static recompute 尚未由 tests 或 LayerEngine 证明。
- `MatchStateExposesContinuousEffectPowerLayerViews` 已证明 snapshot 有 `basePower` / `effectivePower` / `continuousEffects` 代表视图，但该视图尚未证明 Ornn equipment-count static can recompute after later field changes.

## 2. Commands

Focused / keyword / LayerEngine-view guard:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~OrnnFriendlyEquipmentStaticPowerTests|FullyQualifiedName~MatchStateExposesContinuousEffectPowerLayerViews|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~KeywordCoverageReportExposesDeferredKeywordFamilies"
```

Result: **6/6 passed**.

Adjacent equipment / payment regression:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Ornn|FullyQualifiedName~TemperedEquipmentOptionalAttachTests|FullyQualifiedName~JaxTemperedOptionalAttach|FullyQualifiedName~Akshan|FullyQualifiedName~ArmedAssaulterHasteTemperedTests|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~P4EquipmentKeywordProfilesKeepExistingNoAttachFixturesGreen|FullyQualifiedName~P4EquipmentAttachmentProfileMapsTakeUpToRepresentativeAttachDetach|FullyQualifiedName~MatchStateExposesContinuousEffectPowerLayerViews"
```

Result: **114/114 passed**.

Whitespace / patch hygiene before docs:

```sh
git diff --check
```

Result: **passed**.

## 3. Interpretation

- Existing 4D-04H Ornn entry-time representative is green before 4D-04I.
- Existing equipment keyword, Tempered, Jax, Akshan, Armed Assaulter and continuous-effect snapshot representatives remain green before 4D-04I.
- This evidence does not prove dynamic static recompute. It establishes a safe baseline for a future B slice that must add focused tests and runtime support.

## 4. Not Covered

- Ornn recompute after later equipment enter / leave / controller / visibility changes
- complete LayerEngine
- full `百炼` breadth
- other equipment static modifiers
- owner/controller breadth
- attach lifecycle breadth
- frontend final validation
- card matrix full-official coverage
- READY / active goal completion
