# Stage 4D-02K Battle Response Activation Brush Context Handoff

日期：2026-05-14
结论：**HANDOFF READY / PROJECT NOT READY**

本文是 A 侧在 4D-02J 后建立的下一轮 P0-004 交接规格。4D-02G 到 4D-02J 已证明多类 declaration context 可穿过 battle-response pass-pass；4D-02B 已证明真实 Shadow activation 可从 natural battle-response window 进入 stack、结算、回到 response window 再关闭 battle。尚缺的交叉 guard 是：declaration context 必须穿过实际 `ACTIVATE_ABILITY -> stack -> resolve -> return-to-response -> final pass-pass`，而不是只在无人响应时保留。

## Owner

- 建议 owner：B / Raman，服务端规则、协议、测试实现。
- A 负责验收 diff、跑 focused / adjacent / backend full、更新审计与 checkpoint。

## Recommended Representative

使用 Brush replacement context + real Shadow activation：

- P1 attacks a P2-controlled Brush battlefield.
- Brush replaces original `SFD·214/221` held-score battlefield.
- P2 controls one declared defender at the Brush battlefield.
- P2 also controls a ready Shadow source at the same battlefield, but Shadow is **not** included in `DefenderObjectIds`.
- P2 starts with enough resources for both Shadow and held-score, for example `mana=1, power=5`.
- P1 declares `OptionalCosts: ["COMBAT_ASSIGNMENT", "BRUSH_USE_REPLACED_BATTLEFIELD:<originalHeldScoreBattlefieldObjectId>"]`.
- P2 activates Shadow from battle-response priority, pays 1 mana + 1 power, and stuns the attacking unit.
- Stack pass-pass resolves Shadow, returns to battle-response priority.
- Final response pass-pass must restore the original Brush optional cost and enter the replacement-aware held-score path.

Why this representative:

- Brush optional cost is already stable in 4D-02H pass-pass.
- It produces a concrete final branch: `BATTLEFIELD_REPLACEMENT_APPLIED` and held-score payment / score events.
- It avoids `RECYCLE_RUNE:*` / `TEMP_PAYMENT_RESOURCE:*` resource fixture coupling with Shadow's own payment.
- Keeping Shadow outside `DefenderObjectIds` avoids resumed declaration failing because Shadow exhausted itself.

## Expected Tests

至少新增 focused test：

- `BattleDamageAssignmentLifecycleTests.NaturalBattleResponseActivationPreservesBrushReplacementContextAfterStackResolution`

建议断言：

- initial declare accepted；
- `BATTLE_DECLARED.optionalCosts` and `BATTLE_RESPONSE_PRIORITY_OPENED.optionalCosts` include the Brush choice；
- response prompt exposes Shadow activation to P2；
- Shadow activation emits `ABILITY_ACTIVATED`, `UNIT_EXHAUSTED`, `COST_PAID`, and `STACK_ITEM_ADDED`；
- internal carrier remains present in authoritative state but does not leak to P1 / P2 / spectator snapshot or prompt after activation；
- stack pass-pass emits `ABILITY_RESOLVED` / status effect and returns to battle-response priority；
- carrier still does not leak after stack resolution；
- final response pass-pass emits `BATTLE_RESPONSE_PRIORITY_CLOSED.optionalCosts` and resumed `BATTLE_DECLARED.optionalCosts` with the Brush choice；
- final branch emits `BATTLEFIELD_REPLACEMENT_APPLIED`, `BATTLEFIELD_TRIGGER_RESOLVED trigger=BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE`, `COST_PAID`, `SCORE_GAINED`, and `BATTLE_CLOSED`；
- final state clears internal carrier and has no stale battle declaration / assignment prompt.

## Write Scope

允许修改：

- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs` only if the focused test exposes a real runtime gap
- `src/Riftbound.Engine/MatchSession.cs` only if public projection filtering or prompt metadata needs adjustment
- 可新增 4D-02K audit / evidence docs after implementation

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
- Do not broaden PaymentEngine.
- Do not change frontend.
- Do not update card coverage matrix.
- Do not close P0-004, P0-005, P1, READY, or active goal.

## Verdict

This is a focused guard for preserving battle declaration context across an actual battle-response stack resolution. It should narrow P0-004 without claiming full official battle lifecycle closure.
