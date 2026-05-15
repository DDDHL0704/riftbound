# Stage 4D-03AS Azir Optional Armament Reattach Evidence

日期：2026-05-15
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

## 1. Official Text

`data/official/card-catalog.zh-CN.json` contains:

- `SFD·050/221`, card id 33126, 阿兹尔, green, hero unit, energy 6, power 6.
- `SFD·050a/221`, card id 33127, 阿兹尔, green, hero unit, energy 6, power 6.

Official ability text:

```txt
支付{{绿色}}：{{迅捷}} — 选择一个受你控制的单位，将我移动到它的位置，再将它移动到我原来的位置。如果该单位已配有武装，则你可以选择将其中一件武装贴附到我身上。每回合仅可使用一次。
```

## 2. Runtime Evidence

Implementation facts:

- `P4ActivatedAbilityCatalog` defines `AZIR_REATTACH_ARMAMENT:` optional-cost token helpers and the implemented armament reattach policy.
- `MatchSession` exposes Azir prompt metadata with `armamentReattachPolicy=implemented`, `armamentReattachChoicePrefix`, and target-scoped `armamentReattachChoicesByTargetObjectId`.
- `CoreRuleEngine` accepts no reattach or one legal reattach token, separates it from `RECYCLE_RUNE:*` payment resource actions, and rejects unsupported / invalid reattach choices without mutation.
- Activation stores the selected reattach token on the ordinary stack item while preserving typed-green payment, once-per-turn marker and target validation.
- Stack resolution rechecks source / target legality, swaps precise locations, then rechecks the selected armament. A still-legal target-attached armament is moved to Azir via `AttachedToObjectId` and `EQUIPMENT_REATTACHED`; a stale selected armament is skipped without a false attach event.
- `UNIT_LOCATIONS_SWAPPED` carries `selectedArmamentObjectId`, `armamentReattachApplied`, and `armamentReattachPolicy` audit payload.

## 3. Test Evidence

New or updated focused tests cover:

- prompt metadata and target-scoped reattach choices
- no-choice path when the target has attached armaments
- successful selected armament reattach after stack pass-pass
- missing object, non-equipment, unattached equipment, equipment attached to another unit, opponent-controlled equipment and multiple reattach choices rejecting no-mutation
- selected armament becoming stale before resolution, preserving the swap and emitting no false `EQUIPMENT_REATTACHED`
- existing green payment, rune recycle, once-per-turn and invalid target guards

## 4. Commands Run

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AzirSwiftSwap|FullyQualifiedName~Azir|FullyQualifiedName~ActivateAbility|FullyQualifiedName~MoveUnit|FullyQualifiedName~PaymentEngine"
```

Result: 204/204 passed.

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Azir|FullyQualifiedName~ActivateAbility|FullyQualifiedName~MoveUnit|FullyQualifiedName~PaymentEngine|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Priority"
```

Result: 397/397 passed.

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: 4355/4355 passed.

```sh
git diff --check
```

Result: passed.

## 5. Non-Completion Notes

- This evidence does not update `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.
- This evidence does not claim full-official Azir or full swift timing / FAQ breadth.
- This evidence does not replace final frontend build / Chrome smoke / formal 18-step fresh-run.
- This evidence does not close P0-005, P1, full-card matrix or READY.
