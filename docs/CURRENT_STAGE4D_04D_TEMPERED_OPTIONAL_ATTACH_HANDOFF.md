# Stage 4D-04D Tempered Optional Attach Handoff

日期：2026-05-15
结论：**B DISPATCHED / WRITELOCK OPEN / PROJECT NOT READY**

本 handoff 是 A 主控对 P1-002 equipment keyword residual 的下一枚窄实现规格。4D-04B 已经把 equipment keyword report 拆成 representative boundary 与 still-deferred official breadth；4D-04C 已覆盖印刷 `灵便` 装备从手牌打出时的 direct attach representative。4D-04D 只打开 `百炼` 可选装配的一条零额外费用代表路径，不处理完整百炼、Jax、owner/controller 或装备生命周期系统。

## 1. Target

下一枚 B 切片：**Tempered optional attach representative**。

目标：当 P1 从手牌打出 `SFD·008/221`《哨兵好手》时，服务端可公开一个 optional cost choice：选择己方已在场、受控、公开的 `SFD·186/221`《旋转飞斧》作为 `百炼` 装配对象。该代表路径基于《旋转飞斧》`装配 A`，由 `百炼` 减免 `A`，因此本批不新增额外符能支付。合法命令进入既有 `PLAY_CARD` 支付 / stack 流程；结算后《哨兵好手》进入基地，选中的《旋转飞斧》`AttachedToObjectId` 指向该新单位，并产生可审计 attachment event。

固定代表面：

- tempered source：`SFD·008/221` 哨兵好手
- armament source：`SFD·186/221` 旋转飞斧
- optional token：`TEMPERED_ATTACH:<equipmentObjectId>`
- attachment reason：`TEMPERED_OPTIONAL_ATTACH`

## 2. Input Facts

- 4D-04C accepted：printed Agile equipment direct-play attach 已有 prompt target / command validation / stack resolution / `EQUIPMENT_ATTACHED` 代表路径。
- `SFD·008/221` 当前已有 no-optional ordinary `PLAY_CARD` preflight：0 target 入栈，结算后成为 `CARD_TYPE:UNIT`、`哨兵`、`百炼` 单位。
- `SFD·186/221` 当前已有 `ASSEMBLE_ANY_POWER` / `装配 A` representative profile；本批只复用其 profile 作为 `百炼` 减费至 0 的 armament。
- `CardEquipmentKeywordRules` 当前已能识别 `HasTempered`，但 `tempered optional attachment` 仍全部记录为 deferred residual。
- 现有 attachment event / `AttachedToObjectId` / assemble / Take Up / Agile direct attach / Azir reattach / Jax attach 测试已覆盖 attachment 基础形状。

## 3. B Write Scope

B-Implementation / Bacon `019e2ba3-4b9a-7710-a702-1e8e28ecd8ea` 当前独占 4D-04D runtime / focused-test 写锁。

允许范围：

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `src/Riftbound.Engine/CardEquipmentKeywordRules.cs`
- focused Tempered equipment tests, preferably `tests/Riftbound.ConformanceTests/TemperedEquipmentOptionalAttachTests.cs`
- minimal `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs` update if keyword status needs to expose the Tempered representative boundary

禁止触碰：

- frontend / DevUi runtime
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- broad equipment rewrite
- full `百炼` implementation for every printed card
- Jax / Ornn / Akshan / Armed Assaulter special branches
- LayerEngine or continuous effect replacement
- battle lifecycle and PaymentEngine broad refactors
- historical Azir / Maduli / Ezreal focused slices
- `riftbound-dotnet.sln`

## 4. Acceptance

4D-04D may be accepted only if A verifies all of the following:

1. `ActionPrompt` / source requirements expose legal optional cost choices for `SFD·008/221` only when a legal controlled `SFD·186/221` armament is available.
2. Legal optional choice means the selected equipment is face-up, controlled by the acting player, already in a public field zone, `CARD_TYPE:EQUIPMENT`, and cardNo `SFD·186/221`; missing object, enemy object, non-equipment object, hand/deck/graveyard object, face-down object, stale object or wrong-controller object rejects no-mutation.
3. Legal command preserves existing no-target `PLAY_CARD` payment and stack behavior, with `optionalCosts=["TEMPERED_ATTACH:<equipmentObjectId>"]` recorded on the stack item.
4. Stack resolution revalidates the selected armament. If still legal, the new `SFD·008/221` unit enters base and the selected `SFD·186/221` equipment is attached to it with `AttachedToObjectId=sourceObjectId`.
5. Resolution emits auditable attachment evidence, such as `EQUIPMENT_ATTACHED`, with source unit id, equipment id, card numbers, reason `TEMPERED_OPTIONAL_ATTACH`, and optional cost payload.
6. The no-optional `SFD·008/221` path remains green and does not attach equipment.
7. Existing assemble, Take Up, Agile direct attach, Azir reattach, Jax weapon attach and equipment cleanup representative tests remain green.
8. Coverage/profile docs may say `百炼` has one optional attach representative only if Jax trigger breadth, full printed tempered card breadth, dynamic colored costs, already-attached breadth beyond this representative, owner/controller changes, static modifiers, attach lifecycle, LayerEngine and full official equipment breadth remain explicitly deferred.
9. Project remains **NOT READY** and P1-002 remains open.

## 5. Suggested Verification

Focused:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~TemperedEquipmentOptionalAttachTests|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~KeywordCoverageReportExposesDeferredKeywordFamilies"
```

Adjacent:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment|FullyQualifiedName~TakeUp|FullyQualifiedName~AgileEquipmentDirectPlayAttachTests|FullyQualifiedName~AzirSwiftSwap|FullyQualifiedName~JaxWeaponAttach|FullyQualifiedName~P4EquipmentAttachmentProfileMapsTakeUpToRepresentativeAttachDetach"
```

Full backend and hygiene:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

## 6. Non-Goals

- Do not implement full `百炼` for all printed cards.
- Do not implement Jax weapon-attach payment/draw trigger integration through `百炼`.
- Do not implement Ornn static equipment modifiers, Akshan enemy-equipment movement, Armed Assaulter haste branch, or colored/dynamic `百炼` payment breadth.
- Do not implement owner/controller changes, equipment follow movement, or full attach lifecycle breadth.
- Do not update card matrix `fullOfficial`.
- Do not run frontend final validation as a proxy for P1 closure.
- Do not call `update_goal complete`.

## 7. Handoff Verdict

4D-04D is dispatched as a narrow P1-002 runtime slice. It should reduce the `百炼` optional attachment residual by adding one server-authoritative representative path, while preserving the larger equipment / LayerEngine / full-card matrix blockers.
