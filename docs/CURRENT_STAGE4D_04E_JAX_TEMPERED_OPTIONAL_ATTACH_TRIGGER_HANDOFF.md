# 4D-04E Jax Tempered Optional Attach Trigger Handoff

日期：2026-05-15
结论：**HANDOFF ACCEPTED BY 4D-04E AUDIT / WRITELOCK CLOSED / PROJECT NOT READY**

本文件是 A 主控给 B-Implementation 的窄实现交接。目标是把 4D-04D 已落地的 `TEMPERED_ATTACH:<equipmentObjectId>` 代表路径接到 Jax weapon attach trigger payment 代表路径上；不得把本批扩大成完整 `百炼`、完整 LayerEngine、完整武装生命周期或 full-official card matrix 更新。

状态更新：B-Implementation / Bacon `019e2ba3-4b9a-7710-a702-1e8e28ecd8ea` 已完成本 handoff，A 侧验收见 `docs/CURRENT_STAGE4D_04E_JAX_TEMPERED_OPTIONAL_ATTACH_TRIGGER_AUDIT.md` 与 `docs/CURRENT_STAGE4D_04E_JAX_TEMPERED_OPTIONAL_ATTACH_TRIGGER_EVIDENCE.md`。下方原始 scope 保留为本批边界记录；写锁已关闭。

## 1. Current Facts

- 4D-04D 已让 `SFD·008/221`《哨兵好手》从手牌 `PLAY_CARD` 时可以选择己方场上 `SFD·186/221`《旋转飞斧》作为零额外费用 `TEMPERED_ATTACH:<equipmentObjectId>`，结算时重验合法后 attach 并发 `EQUIPMENT_ATTACHED` / `TEMPERED_OPTIONAL_ATTACH`。
- `CoreRuleEngine.TryOpenJaxWeaponAttachPaymentWindow(...)` 已存在，并且 `ASSEMBLE_EQUIPMENT` 路径已在装配 `武装` 到 `SFD·119/221` / `SFD·119a/221` Jax 后打开 `TRIGGER_PAYMENT`，trigger 为 `JAX_WEAPON_ATTACH_PAY_1_DRAW_1`。
- 现在的缺口是：stack resolution 中 `TryAttachTemperedOptionalEquipmentToSource(...)` 只返回 attachment event；`ResolveStackItemEffect(...)` 的结果目前没有把该 attach 触发出的 `PendingPaymentState` 带回 `ResolvePassPriority(...)`。
- 当前项目仍 **NOT READY**。本批只减少 Jax trigger integration residual，不关闭 P1-002、LayerEngine、full-card matrix、frontend final validation 或 READY。

## 2. Scope

Allowed write scope:

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `src/Riftbound.Engine/CardEquipmentKeywordRules.cs`
- focused tests, preferably a new `tests/Riftbound.ConformanceTests/JaxTemperedOptionalAttachTests.cs`, or minimal additions to the existing tempered / trigger payment test files
- minimal `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs` profile/report assertions if needed

Must not touch:

- frontend runtime, stores, components or E2E scripts
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- broad `百炼` for every card
- Ornn / Akshan / Armed Assaulter special branches
- broad LayerEngine, battle lifecycle or broad PaymentEngine refactor
- Azir / Maduli / Ezreal historical slices
- `riftbound-dotnet.sln`

## 3. Required Behavior

1. `SFD·119/221` and `SFD·119a/221` Jax from hand may expose `TEMPERED_ATTACH:<equipmentObjectId>` when the acting player controls a legal public on-field `SFD·186/221`《旋转飞斧》.
2. The optional cost must preserve normal `PLAY_CARD` no-target payment and stack behavior; the selected optional cost must be recorded on the stack item.
3. Command validation must reject invalid handwritten choices no-mutation: missing object, enemy object, non-equipment, wrong card, hand / deck / graveyard object, face-down object, stale object and wrong-controller object.
4. On stack resolution, Jax enters base first; if the selected Spinning Axe remains legal, it attaches to Jax and emits `EQUIPMENT_ATTACHED` with reason `TEMPERED_OPTIONAL_ATTACH`.
5. Because the attached card is a `武装`, the same resolution must open the existing Jax trigger payment window:
   - `paymentWindow=TRIGGER_PAYMENT`
   - `trigger=JAX_WEAPON_ATTACH_PAY_1_DRAW_1`
   - `sourceObjectId=<Jax object id>`
   - `equipmentObjectId=<Spinning Axe object id>`
   - `equipmentCardNo=SFD·186/221`
   - `mana=1`
   - choices are pay 1 mana and decline
6. Paying 1 draws 1 and closes the payment window; decline closes with no draw; insufficient payment rejects and keeps the window without drawing.
7. If the selected equipment becomes stale before stack resolution, Jax still enters base, but attach and payment window are skipped without false attachment or payment events.
8. Existing `ASSEMBLE_EQUIPMENT` Jax trigger payment path must remain green.

## 4. Implementation Notes

- Prefer reusing `TryOpenJaxWeaponAttachPaymentWindow(...)` rather than duplicating trigger payment payload construction.
- If necessary, extend `StackResolutionResult` with an optional `PendingPaymentState?` and have `ResolvePassPriority(...)` prefer that stack-resolution payment when there is no pending hand choice, no winner, no remaining stack, and no queued trigger.
- Avoid opening two trigger payment windows. If future generic event-driven attach triggers are introduced, this representative should still only open one pending payment.
- Keep `CardEquipmentKeywordRules` language conservative: Jax may be marked as a representative Jax-trigger integration slice, but full `百炼` breadth, dynamic costs, owner/controller changes, already-attached breadth beyond this path, Ornn / Akshan branches, static modifiers, attach lifecycle breadth and full official status remain deferred.

## 5. Acceptance Gate

B implementation is acceptable only if A can verify all of the following:

1. Server prompt metadata exposes `TEMPERED_ATTACH:<equipmentObjectId>` for Jax `SFD·119/221` and `SFD·119a/221` only when legal controlled Spinning Axe exists.
2. Invalid optional-cost choices reject no-mutation across missing, enemy, non-equipment, wrong-card, off-field, face-down, stale and wrong-controller cases.
3. Legal Jax `PLAY_CARD` keeps no-target payment / stack behavior and records the optional cost.
4. Stack resolution attaches Spinning Axe to Jax and opens one `TRIGGER_PAYMENT` pending payment for `JAX_WEAPON_ATTACH_PAY_1_DRAW_1`.
5. Pay / decline / insufficient branches match existing Jax weapon attach payment semantics.
6. Stale equipment before resolution causes no attach event and no payment window while still letting Jax enter base.
7. Existing Tempered optional attach, Jax assemble trigger payment, Agile direct attach, AssembleEquipment and Take Up representatives remain green.
8. The slice does not update frontend runtime, card matrix JSON, P1-002 status or READY.

Suggested verification commands:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~JaxTemperedOptionalAttach|FullyQualifiedName~TemperedEquipmentOptionalAttachTests|FullyQualifiedName~JaxWeaponAttach|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~KeywordCoverageReportExposesDeferredKeywordFamilies"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment|FullyQualifiedName~TakeUp|FullyQualifiedName~AgileEquipmentDirectPlayAttachTests|FullyQualifiedName~AzirSwiftSwap|FullyQualifiedName~TriggerPaymentTests|FullyQualifiedName~PaymentEngine"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```
