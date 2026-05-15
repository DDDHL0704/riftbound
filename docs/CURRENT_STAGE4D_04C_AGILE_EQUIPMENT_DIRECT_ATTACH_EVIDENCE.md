# Stage 4D-04C Agile Equipment Direct Attach Evidence

日期：2026-05-15
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

本文件记录 4D-04C Agile equipment direct-play attach representative 的 A 侧验证命令与结果。所有命令在 `/Users/dinghaolin/MyProjects/riftbound-dotnet` 下执行，并先加载 `source scripts/dev-env.sh`。

## 1. Focused / Migrated Baseline

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AgileEquipmentDirectPlayAttachTests|FullyQualifiedName~LongSword|FullyQualifiedName~Steraks|FullyQualifiedName~ClothArmor|FullyQualifiedName~SpinningAxe|FullyQualifiedName~P4EquipmentKeywordProfilesKeepExistingNoAttachFixturesGreen"
```

Result: **57/57 passed**.

Coverage: printed Agile direct attach success path, prompt shape, missing / invalid target no-mutation guard, migrated Agile fixture baseline and existing no-attach profile guard.

## 2. Rejected / Shape Guards

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P4RejectedFixtures|FullyQualifiedName~ConformanceFixtureShapeTests"
```

Result: **113/113 passed**.

Coverage: updated p4 Agile rejected fixtures now reject missing required target instead of legal target presence; fixture shape remains valid.

## 3. Adjacent Equipment / Historical Focused

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment|FullyQualifiedName~TakeUp|FullyQualifiedName~AzirSwiftSwap|FullyQualifiedName~Maduli|FullyQualifiedName~Ezreal|FullyQualifiedName~SeaMonsterHook|FullyQualifiedName~SfurSong|FullyQualifiedName~P6EquipmentSeedBroadcastsPlayAndAssembleInDevelopment"
```

Result: **207/207 passed**.

Coverage: assemble, Take Up, Azir reattach, Maduli, Ezreal and representative equipment seed behavior remain green after Agile direct attach.

## 4. Keyword Guard

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~KeywordCoverageReportExposesDeferredKeywordFamilies|FullyQualifiedName~P6EquipmentKeywordFamiliesReportSpecAndExecutionBoundaryCoverage|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~P4EquipmentAttachmentProfileMapsTakeUpToRepresentativeAttachDetach|FullyQualifiedName~AgileEquipmentDirectPlayAttachTests"
```

Result: **17/17 passed**.

Coverage: report/profile status exposes Agile direct-play representative without converting residual official breadth to implemented/full-ready.

## 5. Historical P79 / Arena Service Crew Recheck

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldStaticEquipmentCostReductionSeedPaysReducedEquipmentCost|FullyQualifiedName~P79BattlefieldStaticReducesFirstEquipmentCost|FullyQualifiedName~CoreRuleEngineReadiesArenaServiceCrewWhenEquipmentPlayed|FullyQualifiedName~P79ArenaServiceCrewReadiesWhenControllerPlaysEquipment|FullyQualifiedName~P79ArenaServiceCrewSkipsOpponentEquipment|FullyQualifiedName~P79BattlefieldStaticEquipmentCostReductionSkipsOpponentControlledSource"
```

Result: **6/6 passed**.

Coverage: historical tests that previously assumed no target now include legal Agile attach targets and still validate cost reduction / ready trigger semantics.

## 6. Backend Full

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: **4368/4368 passed**.

## 7. Hygiene

```sh
git diff --check
```

Result: **passed**.

`riftbound-dotnet.sln` remains untracked and untouched.

## 8. Non-Proxy Warning

The green backend full suite and Agile representative tests do not close P1-002, LayerEngine, full equipment official breadth, frontend final validation, card matrix full-official coverage or READY.
