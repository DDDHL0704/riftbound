# Stage 4D-02J Battle Response Temporary Payment Resource Context Handoff

日期：2026-05-14
结论：**HANDOFF READY / PROJECT NOT READY**

本文是 A 侧在 4D-02I 后建立的下一轮 P0-004 交接规格。4D-02I 已证明 held-score `RECYCLE_RUNE:*` optional cost 可经过 natural battle-response pass 后进入 `BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE` payment / audit path。该切片留下的最小相邻缺口是：同一 context carrier 也需要 dedicated guard 锁住 `TEMP_PAYMENT_RESOURCE:*`，避免后续 refactor 只保留 recycle rune action 而丢失 temporary resource action。

## Owner

- 建议 owner：B / Raman，服务端规则、协议、测试实现。
- A 负责验收 diff、跑 focused / adjacent / backend full、更新审计与 checkpoint。

## Observed Gap

- 4D-02I focused guard 覆盖 `RECYCLE_RUNE:*`。
- 既有 PaymentEngine tests 覆盖 temporary resource 的 held-score consumption semantics，但尚未证明该 action 能跨越 natural battle-response pass boundary。
- 该缺口属于 P0-004 battle lifecycle context preservation 与 P0-005 temporary payment resource audit 的交界面；本切片只补 dedicated battle-response guard，不扩 PaymentEngine。

## Target Slice

实现并验证：natural active `START_BATTLE` 中，攻击方声明带 `COMBAT_ASSIGNMENT + TEMP_PAYMENT_RESOURCE:<resourceId>` 的 `DECLARE_BATTLE`，若存在合法 battle response，服务端必须：

- 先开启 `BATTLE_RESPONSE_PRIORITY_OPENED`；
- 在 `BATTLE_DECLARED` / `BATTLE_RESPONSE_PRIORITY_OPENED` / `BATTLE_RESPONSE_PRIORITY_CLOSED` / resumed `BATTLE_DECLARED` 中保留 temporary resource optional cost；
- pass 前不结算 held-score、temporary resource spend、score 或 battle close；
- 双方 pass 后使用原始 temporary resource action 完成 held-score payment；
- 输出 temporary payment resource spend audit、`COST_PAID.temporaryPaymentResourceIds` / `temporaryPaymentResourcePower` / `temporaryPaymentResourcePowerByTrait` 与 `SCORE_GAINED`；
- 不把 `BATTLE_RESPONSE_DECLARATION_CONTEXT:*` carrier 暴露到 player / spectator snapshot 或 prompt。

## Expected Tests

至少新增 focused test：

- `BattleDamageAssignmentLifecycleTests.NaturalBattleResponsePreservesHeldScoreTemporaryPaymentResourceContextAfterPass`

建议 fixture shape：

- 复用 4D-02I 的 held-score battlefield / Shadow response setup；
- P2 当前 rune pool 不足以直接支付 4 power，例如 3 power；
- P2 拥有一个 owner 为 P2、remaining generic power 为 1、`AllowedPaymentKinds` 包含 `PaymentCostRules.RuneCostPaymentKind` 的 `TemporaryPaymentResourceState`；
- declare uses `OptionalCosts: ["COMBAT_ASSIGNMENT", PaymentCostRules.TemporaryPaymentResourceActionId(resourceId)]`；
- response opens, both players pass, resumed battle resolves held-score temporary payment path.

建议断言：

- optional costs preserved across opened / closed / resumed events；
- pass 前 no `TEMPORARY_PAYMENT_RESOURCE_SPENT` / `COST_PAID` / `SCORE_GAINED` / `BATTLE_CLOSED`；
- pass 后 `BATTLEFIELD_TRIGGER_RESOLVED.trigger=BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE`；
- pass 后 temporary resource is consumed / removed or depleted according to current model；
- `COST_PAID.paymentResourceActions` contains the temporary action；
- `COST_PAID.temporaryPaymentResourceIds` contains the resource id；
- `temporaryPaymentResourcePower` and/or `temporaryPaymentResourcePowerByTrait` reflects the consumed amount；
- `SCORE_GAINED` awards 1 score to the held-score controller；
- internal carrier filtered from P1 / P2 / spectator snapshots and prompt；
- no stale battle declaration / assignment prompt remains.

## Write Scope

允许修改：

- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs` only if the focused test exposes a real runtime gap
- `src/Riftbound.Engine/MatchSession.cs` only if public projection filtering or prompt metadata needs adjustment
- 可新增 4D-02J audit / evidence docs after implementation

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
- Do not broaden PaymentEngine beyond the existing held-score temporary resource branch.
- Do not change frontend.
- Do not update card coverage matrix.
- Do not close P0-004, P0-005, P1, READY, or active goal.

## Verdict

This is a focused temporary payment-resource context preservation guard for the natural battle-response branch. It should narrow P0-004 without claiming full official battle lifecycle or full PaymentEngine closure.
