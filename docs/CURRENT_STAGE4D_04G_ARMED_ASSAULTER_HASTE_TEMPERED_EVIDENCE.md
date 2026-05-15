# Stage 4D-04G Armed Assaulter Haste + Tempered Evidence

日期：2026-05-15
结论：**A-SIDE VALIDATION PASSED / PROJECT NOT READY**

本文件记录 4D-04G implementation 后的 A-side 验收命令和结果。

## 1. Commands

Focused / keyword guard:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ArmedAssaulterHasteTemperedTests|FullyQualifiedName~P4HasteOptionalReadyBranchPaysManaAndPowerForArmedAssaulter|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~KeywordCoverageReportExposesDeferredKeywordFamilies"
```

Result: **26/26 passed**.

Adjacent equipment / payment regression:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~TemperedEquipmentOptionalAttachTests|FullyQualifiedName~JaxTemperedOptionalAttach|FullyQualifiedName~Akshan|FullyQualifiedName~AssembleEquipment|FullyQualifiedName~TakeUp|FullyQualifiedName~AgileEquipmentDirectPlayAttachTests|FullyQualifiedName~AzirSwiftSwap|FullyQualifiedName~PaymentEngine"
```

Result: **235/235 passed**.

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: **4440/4440 passed**.

Whitespace / patch hygiene:

```sh
git diff --check
```

Result: **passed**.

## 2. Interpretation

- The new focused coverage proves Armed Assaulter same-command `HASTE_READY` + legal `TEMPERED_ATTACH:<equipmentObjectId>` payment and resolution.
- The adjacent regression set proves existing Tempered, Jax, Akshan, Agile, Assemble, Take Up, Azir and PaymentEngine representatives stayed green.
- Backend full verifies the repository test suite is green at this batch boundary.
- This is representative evidence only. It does not close P1-002, full `百炼`, full Haste, LayerEngine, frontend final validation, card matrix full-official coverage or READY.
