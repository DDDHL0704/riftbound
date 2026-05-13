# Stage 4D-03H Payment Engine Trigger Resource Evidence

日期：2026-05-13
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文记录 Stage 4D-03H trigger payment resource focused slice 证据。该证据接受 4D-03H focused slice，不关闭 P0-005 full official，不升级卡牌矩阵，不改变项目 **NOT READY** 结论。

## 1. Code Changes

- `src/Riftbound.Engine/CoreRuleEngine.cs`
  - 为 `SFD·180/221` / `SFD·180a/221` 菲奥娜增加 `SFD_FIORA_POWERFUL_READY_PAY_YELLOW_READY` 代表触发。
  - 在栈结算事件中识别 `BOON_GRANTED` / `POWER_MODIFIED_UNTIL_END_OF_TURN` 的真实 power transition，只有从非强力变为强力时才打开 `TRIGGER_PAYMENT`。
  - `TRIGGER_PAYMENT` / `PAY_COST` 接受一个 spend choice 加零个或多个合法 `RECYCLE_RUNE:*` payment resource action。
  - Fiora 支付窗口在结算时重新校验 source Fiora、target unit、控制者、公开正面场上状态与 target 仍为强力。
  - 支付成功后通过 shared `PaymentPlan` / `TryCommitPayment` 扣黄色符能，必要时先回收黄色符文，再 ready target unit 并关闭 payment window。
- `tests/Riftbound.ConformanceTests/TriggerPaymentTests.cs`
  - 新增 Fiora boon power transition 打开黄色 trigger payment 测试。
  - 新增黄色支付使目标活跃、decline 不支付不 ready、回收黄色符文补费、invalid / stale no-mutation 覆盖。

## 2. Covered Acceptance Points

- P1 控制 SFD Fiora，己方 exhausted 4-power unit 因 `OGN·053/298` boon 成为 5-power 时，服务端打开 `TRIGGER_PAYMENT`。
- Payment window 费用为 `powerByTrait.yellow = 1`，legal choices 包含 `SPEND_POWER:yellow:1` 与 `DECLINE`。
- 当前黄色符能足够时，提交 `SPEND_POWER:yellow:1` 会扣空黄色符能、ready target unit、记录 `COST_PAID` / `TRIGGER_RESOLVED` / `UNIT_READIED` / `PAYMENT_WINDOW_CLOSED`。
- 当前黄色符能不足但基地有可回收黄色符文时，prompt / pending payment 暴露 `RECYCLE_RUNE:<objectId>`，命令可先回收再支付黄色符能。
- Decline 会关闭窗口但不支付、不 ready、不回收。
- 重复 resource action、不必要回收、source Fiora 离场或 target 不再强力均 rejected 且 no-mutation。
- 既有 Treasure Pile、Sunken Temple、Vayne、Icevale Archer、Jax mana-only trigger payment regression 保持绿色。

## 3. Verification

Focused regression:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Fiora|FullyQualifiedName~TriggerPaymentTests|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~PAY_COST"
```

Result: passed, 69/69.

Adjacent regression:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~TriggerPayment|FullyQualifiedName~PAY_COST|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

Result: passed, 242/242.

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: passed, 3818/3818.

Whitespace:

```sh
git diff --check
```

Result: no output.

## 4. Remaining Scope

This evidence is intentionally a focused slice. The following remain open:

- full trigger engine batching, ordering, multiplicity and optional-trigger UX;
- full `[A]` / payment-step `[C]` resource skill model;
- `LEGEND_ACT` payment resource action;
- complete Haste / Echo / Spellshield payment windows;
- replacement / prevention / optional / extra / alternative cost breadth beyond accepted representatives;
- prompt `sourceRequirements` / command payment quote parity for every payment path;
- LayerEngine, keyword full-pass and full-card official matrix.
