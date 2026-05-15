# Stage 4D-04E Jax Tempered Optional Attach Trigger Evidence

日期：2026-05-15
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

本文件记录 4D-04E Jax Tempered optional attach trigger integration 的 A 侧验证命令与结果。所有命令在 `/Users/dinghaolin/MyProjects/riftbound-dotnet` 下执行，并先加载 `source scripts/dev-env.sh`。

## 1. Focused / Keyword Guard

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~JaxTemperedOptionalAttach|FullyQualifiedName~TemperedEquipmentOptionalAttachTests|FullyQualifiedName~JaxWeaponAttach|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~KeywordCoverageReportExposesDeferredKeywordFamilies"
```

Result: **41/41 passed**.

Coverage: `SFD·119/221` / `SFD·119a/221` prompt exposes legal `TEMPERED_ATTACH:<equipmentObjectId>` choices; legal `SFD·186/221` optional attach resolves to `EQUIPMENT_ATTACHED` and opens one Jax `TRIGGER_PAYMENT`; pay 1 draws 1; decline closes without draw; insufficient payment rejects and keeps the window; no-optional play remains no-payment; invalid enemy / missing / non-equipment / hand / face-down / stale / wrong-card / wrong-controller choices reject no-mutation; stale equipment at resolution skips attachment and payment. Existing 4D-04D Sentinel Tempered tests and profile guards remain green.

## 2. Adjacent Equipment / Payment Regression

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment|FullyQualifiedName~TakeUp|FullyQualifiedName~AgileEquipmentDirectPlayAttachTests|FullyQualifiedName~AzirSwiftSwap|FullyQualifiedName~TriggerPaymentTests|FullyQualifiedName~PaymentEngine"
```

Result: **243/243 passed**.

Coverage: assemble, Take Up, Agile direct attach, Azir reattach, existing Jax assemble-trigger payment, generic TriggerPayment and PaymentEngine representative behavior remain green after Jax Tempered optional attach trigger integration.

## 3. Backend Full

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: **4397/4397 passed**.

## 4. Hygiene

```sh
git diff --check
```

Result: **passed**.

`riftbound-dotnet.sln` remains untracked and untouched.

## 5. Non-Proxy Warning

The green backend full suite and Jax Tempered representative tests do not close P1-002, LayerEngine, full equipment official breadth, frontend final validation, card matrix full-official coverage or READY.
