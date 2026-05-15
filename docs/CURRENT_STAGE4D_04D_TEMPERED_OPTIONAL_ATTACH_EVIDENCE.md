# Stage 4D-04D Tempered Optional Attach Evidence

日期：2026-05-15
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

本文件记录 4D-04D Tempered optional attach representative 的 A 侧验证命令与结果。所有命令在 `/Users/dinghaolin/MyProjects/riftbound-dotnet` 下执行，并先加载 `source scripts/dev-env.sh`。

## 1. Focused / Keyword Guard

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~TemperedEquipmentOptionalAttachTests|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~KeywordCoverageReportExposesDeferredKeywordFamilies"
```

Result: **14/14 passed**.

Coverage: `SFD·008/221` prompt exposes only legal `TEMPERED_ATTACH:<equipmentObjectId>` choices; legal `SFD·186/221` optional attach resolves to `EQUIPMENT_ATTACHED`; no-optional play remains no-attach; invalid enemy / missing / non-equipment / hand / face-down / stale / wrong-card / wrong-controller choices reject no-mutation; stale equipment at resolution skips attachment event.

## 2. Adjacent Equipment Regression

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment|FullyQualifiedName~TakeUp|FullyQualifiedName~AgileEquipmentDirectPlayAttachTests|FullyQualifiedName~AzirSwiftSwap|FullyQualifiedName~JaxWeaponAttach|FullyQualifiedName~P4EquipmentAttachmentProfileMapsTakeUpToRepresentativeAttachDetach"
```

Result: **139/139 passed**.

Coverage: assemble, Take Up, Agile direct attach, Azir reattach, Jax weapon attach and equipment attachment profile representative behavior remain green after Tempered optional attach.

## 3. Backend Full

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: **4380/4380 passed**.

## 4. Hygiene

```sh
git diff --check
```

Result: **passed**.

`riftbound-dotnet.sln` remains untracked and untouched.

## 5. Non-Proxy Warning

The green backend full suite and Tempered representative tests do not close P1-002, LayerEngine, full equipment official breadth, frontend final validation, card matrix full-official coverage or READY.
