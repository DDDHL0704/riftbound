# Stage 4D-04F Akshan Orange Extra Equipment Steal Handoff

日期：2026-05-15
结论：**B DISPATCHED / WRITELOCK OPEN / PROJECT NOT READY**

本文件是 A 主控交给 B-Implementation 的窄实现规格。目标只推进 `SFD·109/221`《阿克尚》“支付橙橙额外费用，夺取一件敌方装备；若为武装则贴附到阿克尚；直到阿克尚离场为止”的 server-authoritative representative。不得把本批验收扩展为 full `百炼`、LayerEngine、card matrix full-official 或 READY。

## 1. 输入事实

- 当前分支为 `main`，当前工作树基线只允许保留未跟踪 `riftbound-dotnet.sln`；该文件不得触碰、暂存或提交。
- `SFD·109/221`《阿克尚》当前 registry effect kind 为 `AKSHAN_NO_OPTIONAL_ASSEMBLE_NO_EXTRA_PLAY_UNIT`，已有 `tests/Riftbound.ConformanceTests/AkshanGuardTests.cs` 覆盖 no-extra / no-target 普通打出路径与基础 invalid-input no-mutation。
- 4C-44 已明确 Akshan ordinary no-extra route 完成，但 `百炼装配`、橙橙额外费用、敌方装备移动/控制、武装贴附、控制直到离场清理仍是 deferred。
- 4D-04D / 4D-04E 已为 `TEMPERED_ATTACH:<equipmentObjectId>` 建立了“optional cost encodes equipment object id, command-time validate, stack resolution revalidate, stale no-effect”的邻近模式；Akshan 可以复用这种 shape，但不能只做己方装备贴附。
- 现有 `CardObjectState` 已有 `OwnerId`、`ControllerId`、`AttachedToObjectId`；已有临时控制 / field move helper 可作为参考，但 Akshan 的持续时间是“直到阿克尚离场”，不是 end of turn。

## 2. B 写锁

允许 B 修改：

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `src/Riftbound.Engine/CardEquipmentKeywordRules.cs`
- `tests/Riftbound.ConformanceTests/AkshanGuardTests.cs` 或新增同目录 focused Akshan 测试文件
- 仅当 focused tests 证明必要时，最小触碰与 `PLAY_CARD` optional cost / typed power prompt 直接相关的相邻测试

禁止 B 修改：

- frontend runtime / DevUi stores
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- full-card matrix status、`fullOfficial`、Stage 4B count
- broad LayerEngine / continuous effect rewrite
- Ornn、Armed Assaulter、Jax 以外的 unrelated equipment branches
- battle lifecycle broad cleanup
- `riftbound-dotnet.sln`

## 3. Required Behavior

1. Server prompt for `SFD·109/221` exposes an Akshan optional cost choice such as `AKSHAN_STEAL_EQUIPMENT:<equipmentObjectId>` only for legal enemy equipment objects on the field, and only when the acting player can pay the base 4 mana plus 2 orange power after legal payment-resource actions.
2. The optional choice must identify the selected equipment object; target arrays remain the normal Akshan no-target play path unless B finds an existing server-owned target shape that is clearly safer.
3. Submitting the optional choice requires paying base mana plus `powerByTrait.orange = 2`. `COST_PAID` / stack audit must preserve the optional cost id and the orange typed power cost.
4. Command-time invalid choices reject no-mutation: missing object, friendly equipment, non-equipment, unit/spell, hand/deck/graveyard object, face-down object, already P1-controlled object, wrong controller/owner dirtiness, unsupported malformed optional cost, insufficient orange power, wrong-trait power, duplicate or conflicting optional costs.
5. On stack resolution, Akshan enters P1 base exactly as the no-extra route already does.
6. If the selected equipment is still a legal enemy equipment at resolution, it moves to P1 base, `ControllerId` becomes P1, `OwnerId` is preserved, and any previous attachment is cleared before the Akshan effect applies.
7. If the selected equipment has `CardEquipmentKeywordNames.Weapon` / `武装`, it becomes attached to the newly played Akshan by setting `AttachedToObjectId = AkshanObjectId`; non-weapon equipment is controlled/moved but not attached.
8. Resolution emits auditable events for the control/move and, when applicable, the attachment. Reuse existing event kinds where sensible; otherwise use specific payload fields: `sourceObjectId`, `equipmentObjectId`, `previousControllerId`, `controllerId`, `ownerId`, `equipmentCardNo`, `attachedToObjectId`, `reason = AKSHAN_ORANGE_EXTRA_EQUIPMENT_STEAL`, `optionalCosts`.
9. If the selected equipment becomes stale or no longer enemy equipment before resolution, Akshan still enters base, but the equipment move/control/attach part is no-effect and emits no false success event. The extra cost is not refunded.
10. The control duration must be represented and enforced as “until Akshan leaves”. End turn must not return the equipment while Akshan remains in play. When Akshan leaves the field, the equipment returns to owner control at owner base and detaches; emit an auditable return event.
11. If the equipment itself leaves the field before Akshan, any Akshan control marker should become harmless and must not corrupt later state.
12. Existing no-extra Akshan, 4D-04D Tempered, 4D-04E Jax trigger-payment, Agile direct attach, assemble / Take Up, Azir reattach and payment engine tests must remain green.

## 4. Implementation Hints

- Prefer a narrow Akshan-specific optional cost prefix over generalized full `百炼` or LayerEngine changes.
- Reuse `RuneTrait.Orange`, `PaymentCostRules`, `SPEND_POWER:<trait>:<amount>` and existing `RECYCLE_RUNE:<runeObjectId>` payment-resource paths instead of introducing a new payment subsystem.
- Legal enemy equipment should be determined from authoritative field location plus `CardObjectState` tags/controller, not from frontend inference.
- For “until Akshan leaves”, a narrow marker keyed by source object id and equipment object id is acceptable if it is cleaned by authoritative state transitions. Do not model it as `RETURN_CONTROL_TO_OWNER_AT_TURN_END`.
- Keep the behavior command-time and resolution-time revalidated. Stale resolution should behave like the existing Tempered/Jax stale attach paths: source still resolves, selected side effect does not.
- Keep docs conservative: this batch improves one Akshan representative only. It does not close P1-002, full `百炼`, owner/controller breadth, attach lifecycle breadth, LayerEngine, frontend final validation, card matrix or READY.

## 5. Required Focused Tests

B should add focused tests covering at minimum:

- prompt exposes exactly the legal enemy equipment choices and excludes friendly / non-equipment / face-down / stale / wrong-zone / already-controlled equipment.
- legal orange-orange optional cost pays typed orange power, records optional cost on the stack, resolves Akshan to base, moves enemy weapon equipment to P1 base, preserves owner, changes controller, and attaches weapon to Akshan.
- legal enemy non-weapon equipment is controlled/moved but not attached.
- insufficient orange, wrong trait, malformed optional cost, duplicate/conflicting optional costs and invalid selected equipment reject no-mutation.
- selected equipment stale before resolution: Akshan enters base; no equipment event, no controller change, no attachment.
- Akshan remains in play across end turn: equipment is not returned merely because the turn ended.
- Akshan leaves play: controlled equipment returns to owner base, controller returns to owner, attachment clears, and return event is emitted.
- Existing no-extra `AkshanPlayCardWithNoTargetsUsesStackAndResolvesToBase` remains green.

## 6. Suggested A-Side Acceptance Commands

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Akshan|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~KeywordCoverageReportExposesDeferredKeywordFamilies"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~TemperedEquipmentOptionalAttachTests|FullyQualifiedName~JaxTemperedOptionalAttach|FullyQualifiedName~AssembleEquipment|FullyQualifiedName~TakeUp|FullyQualifiedName~AgileEquipmentDirectPlayAttachTests|FullyQualifiedName~AzirSwiftSwap|FullyQualifiedName~PaymentEngine"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

## 7. Stop Condition

B must stop at a focused implementation plus evidence report. Do not update card matrix JSON, do not open frontend validation, and do not claim `READY`. A 主控会在验收后决定是否更新 checkpoint / audit / completion docs and commit this batch.
