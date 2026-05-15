# Stage 4D-04A Keyword Deferred Surface Baseline Evidence

日期：2026-05-15
结论：**BASELINE RECORDED / NO RUNTIME CHANGE / PROJECT NOT READY**

本文件记录 4D-04A A-side handoff 前的 keyword deferred baseline。该 baseline 用于后续 B 切片比对，不代表 P1-002 closure。

## 1. Repository State

Baseline 前仓库事实：

```txt
## main
?? riftbound-dotnet.sln
```

`riftbound-dotnet.sln` 是预期本地未跟踪文件，本批未触碰。

## 2. Focused Catalog / Profile Baseline

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~KeywordCoverageReportExposesDeferredKeywordFamilies|FullyQualifiedName~P6InteractionKeywordFamiliesReportSpecAndExecutionBoundaryCoverage|FullyQualifiedName~P6EquipmentKeywordFamiliesReportSpecAndExecutionBoundaryCoverage|FullyQualifiedName~P6ResourceAndExperienceFamiliesReportSpecAndExecutionBoundaryCoverage|FullyQualifiedName~P4CombatKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~P4ResourceKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~P4InteractionKeywordProfilesMapOfficialTextToRegistryTags"
```

结果：

```txt
已通过! - 失败: 0，通过: 8，已跳过: 0，总计: 8
```

该组覆盖：

- `KeywordCoverageReportExposesDeferredKeywordFamilies`
- interaction / equipment / resource family count baselines
- combat / resource / equipment / interaction profile-to-registry mapping baselines

## 3. Representative Fixture Baseline

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P4CombatKeywordProfilesKeepExistingKeywordUnitFixturesGreen|FullyQualifiedName~P4ResourceKeywordProfilesKeepExistingKeywordUnitFixturesGreen|FullyQualifiedName~P4EquipmentKeywordProfilesKeepExistingNoAttachFixturesGreen|FullyQualifiedName~P4EquipmentAttachmentRepresentativeKeepsTakeUpAttachDetachFixturesGreen|FullyQualifiedName~P4InteractionKeywordProfilesKeepExistingRepresentativeFixturesGreen"
```

结果：

```txt
已通过! - 失败: 0，通过: 144，已跳过: 0，总计: 144
```

该组覆盖现有 keyword representative fixtures：

- combat keyword representative units and battle / move guards
- resource keyword representative units, Hunt conquest / held experience, level / encourage / spellshield fixtures
- equipment no-attach / assemble / Take Up attach-detach representatives
- interaction Standby / Echo / Ambush representatives and rejection guards

## 4. Current Deferred Counts

当前 baseline 中仍保留的 deferred surface：

- Interaction:
  - `待命`：53 entries / 43 functional units，profile deferred 53 / 43。
  - `回响`：24 entries / 24 functional units，其中 profile implemented 10 / 10，profile deferred 14 / 14。
  - `伏击`：18 entries / 18 functional units，profile deferred 18 / 18。
- Equipment:
  - `装配`：32 entries / 31 functional units，profile deferred 32 / 31。
  - `灵便`：4 entries / 4 functional units，profile deferred 4 / 4。
  - `百炼`：16 entries / 11 functional units，profile deferred 16 / 11。
  - Take Up attachment representative：`implemented-representative`。
- Resource:
  - `狩猎`：14 entries / 14 functional units，registry execution 14 / 14，profile deferred 14 / 14。
  - `等级`：18 entries / 17 functional units，registry execution 5 / 5，profile deferred 18 / 17。
  - `鼓舞`：15 entries / 10 functional units，registry execution 5 / 5，profile deferred 15 / 10。
  - `GainExperience` template family：51 entries / 47 functional units implemented at spec family level, but resource keyword profile still tracks broader deferred branches.
- Combat:
  - Assault / Steadfast / Bulwark / Back Row / Roam profile still uses `recognized-deferred` for combat damage, assignment order and roam movement execution breadth, while representative fixtures remain green.

## 5. Baseline Verdict

4D-04A proves that current keyword reporter and representative fixture surfaces are green before the next P1-002 implementation slice. It also confirms the core issue remains real: several keyword families have representative execution paths, but family profiles still contain broad `recognized-deferred` statuses. This batch records the boundary and pauses; it does not close P1-002 or READY.
