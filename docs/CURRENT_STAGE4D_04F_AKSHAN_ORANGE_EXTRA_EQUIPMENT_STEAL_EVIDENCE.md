# Stage 4D-04F Akshan Orange Extra Equipment Steal Evidence

日期：2026-05-15
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

本文件记录 4D-04F Akshan orange extra equipment steal representative 的 A 侧验证命令与结果。所有命令在 `/Users/dinghaolin/MyProjects/riftbound-dotnet` 下执行，并先加载 `source scripts/dev-env.sh`。

## 1. Focused / Keyword Guard

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Akshan|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~KeywordCoverageReportExposesDeferredKeywordFamilies"
```

Result: **28/28 passed**.

Coverage: `SFD·109/221` prompt exposes legal `AKSHAN_STEAL_EQUIPMENT:<equipmentObjectId>` choices only for legal enemy equipment while orange payment is available; legal weapon steal pays 2 orange, moves/controls the equipment, preserves owner and attaches to Akshan; orange rune recycle can supply the second orange power through existing payment-resource action; non-weapon equipment is controlled/moved without attach; invalid / malformed / duplicate choices reject no-mutation; stale selected equipment before resolution skips equipment side effect; end turn alone does not return stolen equipment; Akshan leaving returns equipment to owner base and clears attachment. Existing Akshan no-extra path and keyword guard tests remain green.

## 2. Adjacent Equipment / Payment Regression

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~TemperedEquipmentOptionalAttachTests|FullyQualifiedName~JaxTemperedOptionalAttach|FullyQualifiedName~AssembleEquipment|FullyQualifiedName~TakeUp|FullyQualifiedName~AgileEquipmentDirectPlayAttachTests|FullyQualifiedName~AzirSwiftSwap|FullyQualifiedName~PaymentEngine"
```

Result: **209/209 passed**.

Coverage: Tempered optional attach, Jax trigger-payment, assemble, Take Up, Agile direct attach, Azir reattach and PaymentEngine representative behavior remain green after Akshan equipment-control integration.

## 3. Backend Full

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: **4417/4417 passed**.

## 4. Hygiene

```sh
git diff --check
```

Result: **passed**.

`riftbound-dotnet.sln` remains untracked and untouched.

## 5. Non-Proxy Warning

The green backend full suite and Akshan representative tests do not close P1-002, LayerEngine, full equipment official breadth, frontend final validation, card matrix full-official coverage or READY.
