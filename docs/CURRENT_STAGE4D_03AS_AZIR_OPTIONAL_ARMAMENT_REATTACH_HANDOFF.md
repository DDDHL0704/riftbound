# Stage 4D-03AS Azir Optional Armament Reattach Handoff

日期：2026-05-15
结论：**HANDOFF READY / PAUSE POINT / PROJECT NOT READY**

本 handoff 是 A 主控给后续 Azir full-text 收口切片的写锁和验收规格。它不实现 runtime，不派发 B worker，不修改前端，不修改 card matrix。目标是在 4D-03AM 已验收的 green swift position-swap representative 之后，补齐 `SFD·050/221` / `SFD·050a/221` 阿兹尔官方文本中的 optional armament reattach 分支。

## 1. Target

实现并测试 Azir 官方能力的第二段：

```txt
如果该单位已配有武装，则你可以选择将其中一件武装贴附到我身上。
```

Minimum behavior:

- Azir 支付 1 green、选择受控单位并通过普通 stack item 结算的位置交换行为必须保持 4D-03AM 现有语义。
- 如果被选择单位在结算时配有一个或多个合法武装，服务端必须允许控制者选择 0 或 1 件武装重贴附到 Azir。
- 若选择一件合法武装，结算后该武装的 `AttachedToObjectId` 应从目标单位变为 Azir source object id，并使用现有装备事件语义记录 reattach。
- 若选择 0 件武装、目标无武装、所选武装 stale / no-longer-eligible，位置交换仍应按已有合法性结算；reattach branch no-effect，并在事件 payload 中可审计地区分 `armamentReattachApplied=false`。
- 每回合一次、typed-green payment、target validation、source validation、stale target no-effect 与 no-mutation rejection guard 不得回退。

## 2. Input Facts

- `data/official/card-catalog.zh-CN.json` 固定快照包含 `SFD·050/221` / card id 33126 / 阿兹尔 与 `SFD·050a/221` / card id 33127 / 阿兹尔，二者共享同一官方文本。
- 官方文本为 `支付{{绿色}}：{{迅捷}} — 选择一个受你控制的单位，将我移动到它的位置，再将它移动到我原来的位置。如果该单位已配有武装，则你可以选择将其中一件武装贴附到我身上。每回合仅可使用一次。`
- `docs/CURRENT_STAGE4D_03AM_PAYMENT_ENGINE_AZIR_SWIFT_SWAP_EVIDENCE.md` 已验收 unarmed / position-swap representative，但明确记录 optional armament reattach 仍 deferred。
- `docs/CURRENT_STAGE4D_03AM_CARD_MATRIX_READINESS_AUDIT.md` 确认 `FU-105abedc17` 仍为 `fullOfficial=false`；optional armament reattach 未实现时不得升级 full-official。
- `tests/Riftbound.ConformanceTests/AzirSwiftSwapActivatedAbilityTests.cs` 当前断言 prompt `armamentReattachPolicy == "deferred"`，并断言 `UNIT_LOCATIONS_SWAPPED` event 中 `armamentReattachApplied=false`。
- `src/Riftbound.Engine/CoreRuleEngine.cs` 已有 `AttachedToObjectId`、`EQUIPMENT_ATTACHED`、`EQUIPMENT_REATTACHED`、`EQUIPMENT_DETACHED` 等装备语义；后续实现应复用这些本地形状，不创建平行装备状态模型。

## 3. Suggested B Write Scope

Default write scope for a future implementation worker:

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
- `tests/Riftbound.ConformanceTests/AzirSwiftSwapActivatedAbilityTests.cs`

Optional only if B needs the smallest contract extension for command / prompt shape:

- `src/Riftbound.Contracts/Protocol.cs`
- `src/Riftbound.Contracts/GameCommandJsonMapper.cs`
- focused contract serialization tests

Preferred command shape, if no existing helper is better:

- keep the selected armament as a server-prompted optional action id carried by `ActivateAbilityCommand.OptionalCosts`, for example `AZIR_REATTACH_ARMAMENT:<equipmentObjectId>`
- keep payment resource actions such as `RECYCLE_RUNE:*` separate from the reattach choice during parsing and validation
- store the chosen reattach action on the `StackItemState.OptionalCosts` or the smallest local equivalent so resolution remains server-authoritative

Forbidden in this slice:

- frontend local rule inference
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- broad equipment rewrite or broad attach / detach rule refactor
- unrelated activated abilities, swift timing breadth, battle lifecycle, LayerEngine or HASTE_READY work
- Azir full-official matrix upgrade
- `riftbound-dotnet.sln`

## 4. Acceptance

Minimum acceptance:

1. Prompt metadata must stop claiming `armamentReattachPolicy="deferred"` after implementation; use implemented metadata and expose server-owned reattach choices for each eligible target or an equivalent target-scoped choice shape.
2. Prompt / command must allow no reattach even when the target has attached armament.
3. Command must reject no-mutation when a reattach choice names a missing object, a non-equipment object, an unattached equipment, equipment not attached to the selected target, an opponent-controlled illegal object, multiple selected armaments, or a selected armament without a selected legal target.
4. Resolution must recheck the selected armament after stack pass-pass. If it is still legal and attached to the target, it must update `AttachedToObjectId` to Azir and emit an existing-shape `EQUIPMENT_REATTACHED` event or the smallest local equivalent with previous / new attachment payload.
5. If the selected armament becomes stale before resolution, position swap behavior should remain governed by existing source / target legality, while reattach becomes no-effect and emits no false attach event.
6. `UNIT_LOCATIONS_SWAPPED` or companion events must carry auditable payload: selected armament id when present, `armamentReattachApplied`, and a non-deferred policy marker.
7. Existing 4D-03AM Azir payment, once-per-turn, target validation, stale target no-effect, rune recycle and no-mutation tests remain green.
8. The slice must not update card matrix full-official status, P0/P1 status, frontend final validation or READY.

## 5. Suggested Verification

Implementation-before baseline is recorded in `docs/CURRENT_STAGE4D_03AS_AZIR_OPTIONAL_ARMAMENT_REATTACH_BASELINE_EVIDENCE.md`.

Post-implementation:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AzirSwiftSwap|FullyQualifiedName~Azir|FullyQualifiedName~ActivateAbility|FullyQualifiedName~MoveUnit|FullyQualifiedName~PaymentEngine"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Azir|FullyQualifiedName~ActivateAbility|FullyQualifiedName~MoveUnit|FullyQualifiedName~PaymentEngine|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Priority"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

## 6. Non-Goals

- Do not implement full swift / reaction timing breadth in this slice.
- Do not rewrite the equipment system outside the minimum reattach branch needed by Azir.
- Do not claim full-official Azir until matrix, FAQ and automated evidence gates are separately opened and accepted.
- Do not modify frontend behavior or introduce local frontend rule inference.
- Do not close P0-005, P1, frontend final validation, full-card matrix or READY.

## 7. Handoff Verdict

4D-03AS is ready as a narrow server-authoritative optional armament reattach follow-up slice. Per user instruction, this batch stops after A-side handoff / baseline / readiness docs; no implementation worker is dispatched, and the project remains **NOT READY**.
