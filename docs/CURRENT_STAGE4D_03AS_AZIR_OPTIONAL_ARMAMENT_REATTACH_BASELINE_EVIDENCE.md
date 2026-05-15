# Stage 4D-03AS Azir Optional Armament Reattach Baseline Evidence

日期：2026-05-15
结论：**BASELINE RECORDED / IMPLEMENTATION NOT STARTED / PROJECT NOT READY**

本文件记录 4D-03AS implementation-before baseline。它只证明当前 4D-03AM Azir swift swap representative 仍为绿色；optional armament reattach branch 仍 deferred，后续不得把本 baseline 误解为 full-official Azir evidence。

## 1. Official Text

`data/official/card-catalog.zh-CN.json` contains:

- `SFD·050/221`, card id 33126, 阿兹尔, green, hero unit, energy 6, power 6.
- `SFD·050a/221`, card id 33127, 阿兹尔, green, hero unit, energy 6, power 6.

Official ability text:

```txt
支付{{绿色}}：{{迅捷}} — 选择一个受你控制的单位，将我移动到它的位置，再将它移动到我原来的位置。如果该单位已配有武装，则你可以选择将其中一件武装贴附到我身上。每回合仅可使用一次。
```

## 2. Current Runtime State

Current implementation facts before 4D-03AS:

- `P4ActivatedAbilityCatalog` exposes `AZIR_SWIFT_PAY_GREEN_SWAP_WITH_CONTROLLED_UNIT` for both Azir collector numbers.
- `MatchSession` exposes green typed payment metadata, controlled-unit target choices, swift marker, once-per-turn marker and precise swap policy.
- `MatchSession` still exposes `armamentReattachPolicy=deferred` for the Azir requirement.
- `CoreRuleEngine` validates source, target and payment resources, commits green payment through `PaymentCostRules`, creates an ordinary stack item and resolves by swapping precise field locations.
- `CoreRuleEngine` still emits `UNIT_LOCATIONS_SWAPPED` with `armamentReattachApplied=false` and `armamentReattachPolicy=deferred`.
- No implementation worker has been dispatched for 4D-03AS in this batch.

## 3. Current Test Anchors

Existing focused tests in `tests/Riftbound.ConformanceTests/AzirSwiftSwapActivatedAbilityTests.cs` cover:

- both Azir collector aliases
- prompt source requirement / target choices / green payment metadata
- successful command and stack resolution for both collector numbers
- green rune recycle for typed green shortfall
- once-per-turn rejection and end-turn cleanup
- invalid timing, invalid targets, invalid source / target shape and no-mutation rejection
- wrong trait / generic temporary / duplicate / invalid / unnecessary payment resource actions
- unsupported optional cost and insufficient green payment

Current tests deliberately assert the deferred armament state:

- prompt metadata: `armamentReattachPolicy == "deferred"`
- resolution event: `armamentReattachApplied == false`

## 4. Commands Run

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AzirSwiftSwap|FullyQualifiedName~Azir|FullyQualifiedName~ActivateAbility|FullyQualifiedName~MoveUnit|FullyQualifiedName~PaymentEngine"
```

Result: 194/194 passed.

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Azir|FullyQualifiedName~ActivateAbility|FullyQualifiedName~MoveUnit|FullyQualifiedName~PaymentEngine|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Priority"
```

Result: 387/387 passed.

```sh
git diff --check
```

Result: passed.

## 5. Non-Completion Notes

- Optional armament reattach remains deferred.
- This baseline does not update runtime, tests, frontend or card matrix JSON.
- This baseline does not replace post-implementation focused / adjacent / backend full verification.
- This baseline does not replace final frontend build / Chrome smoke / formal 18-step fresh-run.
- This baseline does not close P0-005, P1, full-card matrix or READY.
