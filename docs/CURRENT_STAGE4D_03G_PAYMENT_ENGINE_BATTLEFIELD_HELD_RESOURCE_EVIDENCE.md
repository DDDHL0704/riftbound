# Stage 4D-03G Payment Engine Battlefield Held Resource Evidence

日期：2026-05-13
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文记录 Stage 4D-03G battlefield held score payment resource focused slice 证据。该证据接受 4D-03G focused slice，不关闭 P0-005 full official，不升级卡牌矩阵，不改变项目 **NOT READY** 结论。

## 1. Code Changes

- `src/Riftbound.Engine/CoreRuleEngine.cs`
  - `TryBuildMinimalDeclareBattle` 允许 `COMBAT_ASSIGNMENT` 搭配必要的 `RECYCLE_RUNE:*` payment resource action，并在命令早期校验 held-score 战场来源、支付玩家、资源动作合法性和必要性。
  - `TryResolveBattlefieldHeldPayPowerScoreTrigger` 接收 combat optional costs，提取 `RECYCLE_RUNE:*` payment resource action，并在支付 4 power 前应用合法回收。
  - battlefield held score 的 `PaymentPlan` 现在携带 `paymentResourceActionIds`，`RUNE_RECYCLED` / `POWER_GAINED` / `COST_PAID` 共享 `paymentId` 与 `paymentWindow = BATTLEFIELD_HELD`。
  - 不必要或非法 resource action rejected 且不改变 authoritative state。
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
  - 新增 `P79BattlefieldHeldPaysPowerToGainScoreWithRecycleRunePaymentResource`。
  - 新增 `P79BattlefieldHeldRejectsInvalidRecycleRunePaymentResourceWithoutMutation`，覆盖 unnecessary / wrong-player / missing-card-no / duplicate resource actions。

## 2. Covered Acceptance Points

- P2 只有 3 generic power、基地有可回收红色基础符文时，可在 `DECLARE_BATTLE` 的 `optionalCosts` 中提交 `COMBAT_ASSIGNMENT` 与 `RECYCLE_RUNE:<objectId>`，据守能量枢纽后支付 4 power 得 1 分。
- 回收符文从基地移入符文牌堆底部，符文对象恢复未横置，生成 `RUNE_RECYCLED` 与 `POWER_GAINED`。
- `COST_PAID` payload 包含 `paymentWindow = BATTLEFIELD_HELD`、`reason = BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE`、`paymentResourceActions`、`recycledRuneObjectIds`、generic / total power cost 与 remaining pool metadata。
- 当前 power 已足够时夹带 `RECYCLE_RUNE:*` 会 rejected 且 state hash 不变。
- 错玩家符文、缺少服务端 `cardNo` 的符文和重复 resource action rejected 且 no-mutation。
- 既有普通 power、typed power、once-per-turn 和 third-turn delay / prevent regression 保持绿色。

## 3. Verification

Focused regression:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldHeldPaysPowerToGainScore|FullyQualifiedName~P79BattlefieldHeldPaysTypedPowerToGainScore|FullyQualifiedName~P79BattlefieldScoreDelayPreventsHeldScorePaymentBeforeThirdTurn|FullyQualifiedName~PaymentEngineUnificationTests"
```

Result: passed, 22/22.

Adjacent regression:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldHeld|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

Result: passed, 224/224.

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: passed, 3809/3809.

Whitespace:

```sh
git diff --check
```

Result: no output.

## 4. Remaining Scope

This evidence is intentionally a focused slice. The following remain open:

- concrete mana-only `TRIGGER_PAYMENT` paths are unchanged and no power-cost trigger resource-action representative was added;
- full `[A]` / payment-step `[C]` resource skill model;
- `LEGEND_ACT` payment resource action;
- complete Haste / Echo / Spellshield payment windows;
- replacement / prevention / optional / extra / alternative cost breadth beyond accepted representatives;
- prompt `sourceRequirements` / command payment quote parity for every payment path;
- LayerEngine, keyword full-pass and full-card official matrix.
