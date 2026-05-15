# Stage 4D-04H Ornn Friendly Equipment Static Power Evidence

日期：2026-05-15
结论：**A-SIDE VALIDATION PASSED / PROJECT NOT READY**

本文件记录 4D-04H implementation 后的 A-side 验收命令和结果。

## 1. Commands

Focused / keyword guard:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~OrnnFriendlyEquipmentStaticPowerTests|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~KeywordCoverageReportExposesDeferredKeywordFamilies"
```

Result: **5/5 passed**.

Adjacent equipment / payment regression:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Ornn|FullyQualifiedName~TemperedEquipmentOptionalAttachTests|FullyQualifiedName~JaxTemperedOptionalAttach|FullyQualifiedName~Akshan|FullyQualifiedName~ArmedAssaulterHasteTemperedTests|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~P4EquipmentKeywordProfilesKeepExistingNoAttachFixturesGreen|FullyQualifiedName~P4EquipmentAttachmentProfileMapsTakeUpToRepresentativeAttachDetach|FullyQualifiedName~MatchStateExposesContinuousEffectPowerLayerViews"
```

Result: **114/114 passed**.

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: **4443/4443 passed**.

Whitespace / patch hygiene:

```sh
git diff --check
```

Result: **passed**.

## 2. Interpretation

- The new focused coverage proves Ornn counts only friendly public field equipment at hand-play entry resolution and records `friendlyEquipmentPowerBonus` only when non-zero.
- The adjacent regression set proves existing Ornn profile guards, Tempered, Jax, Akshan, Armed Assaulter, attachment profile and continuous-effect snapshot representatives stayed green.
- Backend full verifies the repository test suite is green at this batch boundary.
- This is representative evidence only. It does not close P1-001, P1-002, full `百炼`, full static LayerEngine recompute, frontend final validation, card matrix full-official coverage or READY.
