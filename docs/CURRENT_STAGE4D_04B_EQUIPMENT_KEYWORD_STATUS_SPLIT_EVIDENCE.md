# Stage 4D-04B Equipment Keyword Status Split Evidence

日期：2026-05-15
结论：**VERIFIED / PROJECT NOT READY**

本文件记录 4D-04B equipment keyword status split 的 A 侧验证命令与结果。

## 1. Focused Equipment Keyword/Profile Guard

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~KeywordCoverageReportExposesDeferredKeywordFamilies|FullyQualifiedName~P6EquipmentKeywordFamiliesReportSpecAndExecutionBoundaryCoverage|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~P4EquipmentAttachmentProfileMapsTakeUpToRepresentativeAttachDetach"
```

结果：

```txt
已通过! - 失败: 0，通过: 4，已跳过: 0，总计: 4
```

## 2. Adjacent Equipment Fixture / Assemble Guard

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P4EquipmentKeywordProfilesKeepExistingNoAttachFixturesGreen|FullyQualifiedName~P4EquipmentAttachmentRepresentativeKeepsTakeUpAttachDetachFixturesGreen|FullyQualifiedName~AssembleEquipment"
```

结果：

```txt
已通过! - 失败: 0，通过: 98，已跳过: 0，总计: 98
```

## 3. Broader Keyword Family Guard

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~KeywordCoverageReportExposesDeferredKeywordFamilies|FullyQualifiedName~P6InteractionKeywordFamiliesReportSpecAndExecutionBoundaryCoverage|FullyQualifiedName~P6EquipmentKeywordFamiliesReportSpecAndExecutionBoundaryCoverage|FullyQualifiedName~P6ResourceAndExperienceFamiliesReportSpecAndExecutionBoundaryCoverage|FullyQualifiedName~P4CombatKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~P4ResourceKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~P4InteractionKeywordProfilesMapOfficialTextToRegistryTags"
```

结果：

```txt
已通过! - 失败: 0，通过: 8，已跳过: 0，总计: 8
```

## 4. Backend Full

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

结果：

```txt
已通过! - 失败: 0，通过: 4355，已跳过: 0，总计: 4355
```

## 5. Hygiene

命令：

```sh
git diff --check
```

结果：passed。

## 6. Evidence Verdict

4D-04B is verified for the narrow equipment keyword status split. The tests prove that implemented assemble representatives are now visible in equipment keyword status, while deferred equipment breadth remains visible. This evidence does not prove P1-002 closure or READY.
