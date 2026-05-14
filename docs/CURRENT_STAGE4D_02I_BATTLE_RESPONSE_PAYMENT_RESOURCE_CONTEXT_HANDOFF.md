# Stage 4D-02I Battle Response Payment Resource Context Handoff

日期：2026-05-14
结论：**HANDOFF READY / PROJECT NOT READY**

本文是 A 侧在 4D-02H 后建立的下一轮 P0-004 交接规格。4D-02G 已证明 battle-response pass 后可保留 battlefield target context，4D-02H 已证明同一 carrier 可保留 Brush replacement optional cost。下一条最小相邻缺口是：held-score payment-resource optional cost 也必须经过 natural battle-response pass 后继续进入支付 / 审计路径，防止 `RECYCLE_RUNE:*` / `TEMP_PAYMENT_RESOURCE:*` 被 battle-response 重建声明时丢失。

## Owner

- 建议 owner：B / Raman，服务端规则、协议、测试实现。
- A 负责验收 diff、跑 focused / adjacent / backend full、更新审计与 checkpoint。

## Observed Gap

- 4D-02G carrier 会保存 `OptionalCosts`，理论上应覆盖 held-score payment-resource actions。
- 4D-02H dedicated guard 只覆盖 `BRUSH_USE_REPLACED_BATTLEFIELD:*`。
- 当前还没有 dedicated natural battle-response test 证明 `RECYCLE_RUNE:*` 或 `TEMP_PAYMENT_RESOURCE:*` optional cost 在 response pass 后仍会被 `BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE` 消费，并写入 `RUNE_RECYCLED` / `TEMPORARY_PAYMENT_RESOURCE_SPENT` / `COST_PAID` 等支付审计。
- 这是 P0-004 battle lifecycle 与 P0-005 payment context 的交界风险，但本切片只证明 context preservation，不做 PaymentEngine breadth refactor。

## Target Slice

实现并验证：natural active `START_BATTLE` 中，攻击方声明带 `COMBAT_ASSIGNMENT + RECYCLE_RUNE:<runeObjectId>` 或 `COMBAT_ASSIGNMENT + TEMP_PAYMENT_RESOURCE:<resourceId>` 的 `DECLARE_BATTLE`，若存在合法 battle response，服务端必须：

- 先开启 `BATTLE_RESPONSE_PRIORITY_OPENED`；
- 在 response window events 中保留 payment-resource optional cost；
- 双方 pass 后继续用原始 optional cost 结算 battle；
- held-score branch 使用原始 payment-resource action 完成 4 power payment；
- 输出对应支付审计，并让 `COST_PAID.paymentResourceActionIds` / `recycledRuneObjectIds` / `temporaryPaymentResourceIds` 与提交内容一致；
- 不把 `BATTLE_RESPONSE_DECLARATION_CONTEXT:*` carrier 暴露到 player / spectator snapshot 或 prompt。

## Expected Tests

至少新增 focused test：

- `BattleDamageAssignmentLifecycleTests.NaturalBattleResponsePreservesHeldScorePaymentResourceContextAfterPass`

建议 fixture shape：

- active contested held-score battlefield object：`SFD·214/221`，由防守方 / 据守支付方控制；
- P1 有一个 attacking unit；
- P2 有一个 defending unit 可据守该 battlefield，并在同一 battlefield 有合法 Shadow response source；
- P2 当前 rune pool 不足以直接支付 4 power，例如 3 power；
- P2 base 有一个可回收 basic rune，或 P2 拥有一个 eligible temporary payment resource；
- declare uses `OptionalCosts: ["COMBAT_ASSIGNMENT", "RECYCLE_RUNE:<P2 rune id>"]`，如选 temporary resource 则使用 `PaymentCostRules.TemporaryPaymentResourceActionId(resourceId)`；
- response opens, both players pass, resumed battle resolves held-score payment path.

建议断言：

- initial declare accepted；
- `BATTLE_DECLARED.optionalCosts`、`BATTLE_RESPONSE_PRIORITY_OPENED.optionalCosts`、`BATTLE_RESPONSE_PRIORITY_CLOSED.optionalCosts` 和 resumed `BATTLE_DECLARED.optionalCosts` include the payment-resource action；
- no `BATTLEFIELD_HELD` / `COST_PAID` / `SCORE_GAINED` / `BATTLE_CLOSED` before response pass；
- after pass, held-score branch emits `BATTLEFIELD_TRIGGER_RESOLVED` with `trigger=BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE`；
- recycle path emits `RUNE_RECYCLED` / `POWER_GAINED` as applicable, and `COST_PAID.recycledRuneObjectIds` contains the submitted rune id；
- temporary path, if selected, emits temporary resource spent audit and `COST_PAID.temporaryPaymentResourceIds` contains the submitted resource id；
- `SCORE_GAINED` awards 1 score to the held-score controller；
- internal carrier filtered from P1 / P2 / spectator snapshots and prompt；
- no stale battle declaration / assignment prompt remains for the selected branch.

## Write Scope

允许修改：

- `src/Riftbound.Engine/CoreRuleEngine.cs` only if the focused test exposes a real runtime gap
- `src/Riftbound.Engine/MatchSession.cs` only if public projection filtering or prompt metadata needs adjustment
- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`
- 可新增 4D-02I audit / evidence docs after implementation

不要修改：

- front-end files
- PaymentEngine broad refactor
- LayerEngine
- card coverage matrix
- unrelated fixtures
- `riftbound-dotnet.sln`

## Validation Gate

实现后必须通过：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattleDamageAssignmentLifecycleTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~Shadow|FullyQualifiedName~DeclareBattle|FullyQualifiedName~StartBattle|FullyQualifiedName~Priority|FullyQualifiedName~Reaction|FullyQualifiedName~Swift|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Battlefield|FullyQualifiedName~SpellDuel|FullyQualifiedName~DeclareBattle|FullyQualifiedName~MoveUnit|FullyQualifiedName~BoardTaskQueue|FullyQualifiedName~ActivateAbility|FullyQualifiedName~PaymentEngineCoverageAuditTests"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

```sh
git diff --check
```

## No-Go

- Do not rewrite combat.
- Do not start LayerEngine.
- Do not broaden PaymentEngine beyond the existing held-score payment-resource branch.
- Do not change frontend.
- Do not update card coverage matrix.
- Do not close P0-004, P0-005, P1, READY, or active goal.

## Verdict

This is a focused payment-resource context preservation guard for the natural battle-response branch. It should narrow P0-004 without claiming full official battle lifecycle or full PaymentEngine closure.
