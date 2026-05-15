# Stage 4D-04G Armed Assaulter Haste + Tempered Handoff

日期：2026-05-15
结论：**B DISPATCHED / WRITELOCK OPEN / PROJECT NOT READY**

本文件是 A 主控交给 B-Implementation 的窄实现规格。目标只推进 `SFD·002/221`《武装强袭者》同一 `PLAY_CARD` 命令中 `急速` optional ready 与 `百炼` optional attach 的组合 representative。不得把本批验收扩展为 full `百炼`、full Haste、LayerEngine、card matrix full-official 或 READY。

## 1. 输入事实

- 当前分支为 `main`，当前工作树基线只允许保留未跟踪 `riftbound-dotnet.sln`；该文件不得触碰、暂存或提交。
- `SFD·002/221`《武装强袭者》当前 registry effect kind 为 `ARMED_ASSAULTER_PLAY_UNIT_NO_OPTIONAL_HASTE`，`SourceUnitTags = "急速|百炼"`，HASTE_READY 费用为 1 mana + 1 red power。
- 现有 `p4-play-armed-assaulter-haste-ready.fixture.json` 只覆盖 `optionalCosts=["HASTE_READY"]`，并明确 `百炼装配和武装贴附路径暂缓`。
- 4D-04D / 4D-04E 已为 `TEMPERED_ATTACH:<equipmentObjectId>` 建立己方 `SFD·186/221`《旋转飞斧》零额外费用 attach representative；4D-04G 只要求把该 shape 与 Armed Assaulter 的 HASTE_READY shape 在同一 card / same command 中组合。
- 当前 `TryBuildOptionalCostPlan` 以单一 optional-cost family 为主；本批需要让 Armed Assaulter 的 `HASTE_READY` 与 `TEMPERED_ATTACH:*` 可并存并合并费用。

## 2. B 写锁

允许 B 修改：

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `src/Riftbound.Engine/CardEquipmentKeywordRules.cs`
- `tests/Riftbound.ConformanceTests/ArmedAssaulterHasteTemperedTests.cs` 或相邻 focused tests
- 仅当 focused tests 证明必要时，最小触碰 existing HASTE_READY / Tempered / keyword profile tests

禁止 B 修改：

- frontend runtime / DevUi stores
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- full-card matrix status、`fullOfficial`、Stage 4B count
- broad LayerEngine / continuous effect rewrite
- Ornn static modifiers、Akshan return-control branch、Jax trigger payment branch
- battle lifecycle broad cleanup
- `riftbound-dotnet.sln`

## 3. Required Behavior

1. Server prompt for `SFD·002/221` exposes both `HASTE_READY` and legal `TEMPERED_ATTACH:<equipmentObjectId>` choices when the acting player can pay base 6 mana plus the 1 mana / 1 red HASTE_READY extra cost and has a legal controlled `SFD·186/221` field equipment.
2. The player may submit no optional costs, only `HASTE_READY`, only `TEMPERED_ATTACH:<equipmentObjectId>`, or both `HASTE_READY` and `TEMPERED_ATTACH:<equipmentObjectId>`.
3. Submitting both optional costs pays base 6 mana + 1 mana + 1 red power, records both optional costs on `COST_PAID` and the stack item, and keeps target arrays empty.
4. Resolution with both optional costs plays Armed Assaulter to P1 base as active (`hasteReadyOptionalCostPaid=true`, `isExhausted=false`) and attaches the selected legal Spinning Axe to the new unit with `EQUIPMENT_ATTACHED / TEMPERED_OPTIONAL_ATTACH`.
5. `TEMPERED_ATTACH` without `HASTE_READY` should attach legal Spinning Axe but the unit should enter using the normal non-haste-ready exhaustion behavior.
6. `HASTE_READY` without `TEMPERED_ATTACH` must preserve the existing P4.34 Armed Assaulter fixture behavior.
7. Invalid choices reject no-mutation: duplicate HASTE_READY, duplicate / conflicting `TEMPERED_ATTACH:*`, missing object, enemy object, non-equipment, wrong-card equipment, hand / deck / graveyard object, face-down object, stale object, wrong-controller equipment, insufficient mana, insufficient red power, wrong trait, malformed optional cost, or unrelated optional cost.
8. If the selected equipment becomes stale before stack resolution, Armed Assaulter still resolves; HASTE_READY remains applied if paid, but attach and `EQUIPMENT_ATTACHED` are skipped.
9. Existing `TemperedEquipmentOptionalAttachTests`, `JaxTemperedOptionalAttachTests`, `AkshanGuardTests`, Agile direct attach, assemble / Take Up, Azir reattach, HASTE_READY and PaymentEngine representative tests must remain green.
10. The slice does not update frontend runtime, card matrix JSON, P1-002 status or READY.

## 4. Implementation Hints

- Prefer a narrow Armed Assaulter combination optional-cost planner over a generalized all-optional-cost algebra unless the local code shape makes the generalized helper smaller and safer.
- Reuse `CardPermissionKeywordRules.TryBuildHasteReadyOptionalCost`, existing `HasteOptionalCostNames.HasteReady`, `RuneTrait.Red`, `PaymentCostRules`, and existing `TEMPERED_ATTACH:<equipmentObjectId>` validation / resolution helpers.
- Preserve order-insensitive behavior for the two optional costs if practical, but reject duplicates and conflicting attach object ids.
- Keep command-time validation and stack-resolution revalidation separate. A stale attach target should behave like 4D-04D / 4D-04E stale attach paths, not roll back paid HASTE_READY.
- Keep docs conservative: this batch improves one combination representative only. It does not close P1-002, full `百炼`, full Haste, owner/controller breadth, attach lifecycle breadth, LayerEngine, frontend final validation, card matrix or READY.

## 5. Required Focused Tests

B should add focused tests covering at minimum:

- prompt exposes both `HASTE_READY` and legal `TEMPERED_ATTACH:<spinningAxeObjectId>` for Armed Assaulter and excludes illegal equipment choices.
- legal both-cost command pays 7 mana + 1 red power, stores both optional costs on the stack, resolves active Armed Assaulter to base, attaches Spinning Axe, and emits both active-entry and attach evidence.
- legal `TEMPERED_ATTACH` only attaches but does not mark `hasteReadyOptionalCostPaid`.
- legal `HASTE_READY` only preserves existing fixture behavior.
- invalid duplicate / conflicting optional costs and invalid equipment choices reject no-mutation.
- insufficient red / wrong trait rejects no-mutation for HASTE_READY while leaving hand / resources / stack unchanged.
- selected equipment stale before resolution: unit resolves, HASTE_READY remains if paid, no attach event and no attachment.

## 6. Suggested A-Side Acceptance Commands

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ArmedAssaulter|FullyQualifiedName~P4HasteOptionalReadyBranchPaysManaAndPowerForArmedAssaulter|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~KeywordCoverageReportExposesDeferredKeywordFamilies"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~TemperedEquipmentOptionalAttachTests|FullyQualifiedName~JaxTemperedOptionalAttach|FullyQualifiedName~Akshan|FullyQualifiedName~AssembleEquipment|FullyQualifiedName~TakeUp|FullyQualifiedName~AgileEquipmentDirectPlayAttachTests|FullyQualifiedName~AzirSwiftSwap|FullyQualifiedName~PaymentEngine"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

## 7. Stop Condition

B must stop at a focused implementation plus evidence report. Do not update card matrix JSON, do not open frontend validation, and do not claim `READY`. A 主控会在验收后决定是否更新 checkpoint / audit / completion docs and commit this batch.
