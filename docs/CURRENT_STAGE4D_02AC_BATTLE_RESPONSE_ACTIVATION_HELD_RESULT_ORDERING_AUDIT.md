# Stage 4D-02AC Battle Response Activation Held Result Ordering Audit

日期：2026-05-15
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

## Scope

4D-02AC 接受一个 P0-004 battle lifecycle focused guard，用于覆盖 actual Shadow activation / stack resolution / returned response 后进入 assignment 的 Hunt held result ordering：

- natural battle 先打开 battle response priority。
- P2 在 battle response window 中真实 activation Shadow，并通过 stack pass-pass 解析。
- Shadow 解析后 battle 仍 active，priority 回到 P2 response，且 `BF-NEXT` 不提前推进。
- P2 / P1 pass returned response 后进入 `ASSIGN_COMBAT_DAMAGE`。
- 合法 assignment 让带 Hunt 的 P2 defender 摧毁 attacker 并守住当前 battlefield。
- `BATTLEFIELD_HELD` 与 `EXPERIENCE_GAINED` 在 cleanup / battle close / battlefield control / next contested battlefield advancement 前出现。

## Changed Files

- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`

## Runtime Change

None.

This slice is test-only. Existing 4D-02AA assignment prompt result handling already emits Hunt held result / experience before cleanup, battle close, battlefield control resolution, and next battlefield advancement. 4D-02AC proves that behavior also composes after a real Shadow activation, stack resolution, returned battle response priority, and assignment prompt lifecycle.

This does not broaden PaymentEngine, LayerEngine, frontend, card matrix, held-score payment, replacement, prevention, or full combat breadth.

## Behavior Accepted

New guard:

```text
NaturalBattleResponseActivationAssignmentHeldResultOrdersBeforeNextAdvancement
```

The test proves activation-returned assignment semantics now compose with Hunt held result ordering:

- stack-open and stack-resolved returned-response windows do not advance `BF-NEXT`;
- response close opens assignment before current battle cleanup;
- `BATTLEFIELD_HELD` precedes `EXPERIENCE_GAINED`;
- both result events precede `DAMAGE_REMOVED`, `BATTLE_CLOSED`, `BATTLEFIELD_CONTROL_RESOLVED`, next `BATTLEFIELD_CONTESTED`, and next `SPELL_DUEL_STARTED`;
- no `BATTLEFIELD_CONQUERED` is emitted for this held branch;
- the current `START_BATTLE` task is removed and the next active task is `task:start-spell-duel:BF-NEXT`.

## Validation

Evidence is recorded in:

- `docs/CURRENT_STAGE4D_02AC_BATTLE_RESPONSE_ACTIVATION_HELD_RESULT_ORDERING_EVIDENCE.md`

Results:

- targeted new guard: 1/1
- focused: 284/284
- adjacent: 814/814
- backend full: 4226/4226
- `git diff --check`: no output

## Residual Risk

This slice narrows battle-result ordering, but it does not close full official P0-004:

- only activation-returned assignment Hunt held ordering is covered;
- full held / conquer trigger, replacement, prevention, damage-modification, cleanup-blocker, and payment-interaction matrices remain open;
- arbitrary official multi-combat assignment and all same-priority permutations remain outside this focused guard;
- LayerEngine interactions remain deferred;
- final frontend, Chrome smoke, formal E2E, hidden-info long-chain, card coverage matrix, and completion audit remain open.

Project remains **NOT READY**.
