# Stage 4D-02AF Battle Response Activation Held-Score Next Contest Advancement Audit

日期：2026-05-15
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

## Scope

4D-02AF 接受一个 P0-004 battle lifecycle focused guard，用于覆盖 BF-A / BF-B 双争夺场景中的 activation-returned held-score branch next-contested advancement：

- BF-A 已完成 spell duel，当前 active `START_BATTLE` 从 BF-A 打开 natural battle response priority；
- BF-B 同时 contested，等待后续 `START_SPELL_DUEL` advancement；
- P1 declaration optional costs 包含 `COMBAT_ASSIGNMENT` 与 `TEMP_PAYMENT_RESOURCE:<HeldScoreTemporaryResourceId>`；
- P2 在 battle response window 中真实 activation Shadow，并通过 stack pass-pass 解析；
- response、stack、returned response 阶段均不得提前推进 BF-B；
- returned response pass-pass 后，BF-A held-score branch 消费 temporary payment resource、P2 得分、current battle close；
- BF-B `BATTLEFIELD_CONTESTED` / `SPELL_DUEL_STARTED` 只能在 BF-A held-score score / battle close 后出现。

## Changed Files

- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`

## Runtime Change

None. This is a test-only guard.

The accepted guard composes existing runtime behavior from:

- 4D-02AE activation-returned held-score `TEMP_PAYMENT_RESOURCE:*` context preservation;
- existing activation-returned next contested battlefield advancement;
- existing BF-NEXT no-early-advancement assertions.

This does not broaden PaymentEngine, LayerEngine, frontend, card matrix, replacement, prevention, or full combat breadth.

## Behavior Accepted

New guard:

```text
NaturalBattleResponseActivationHeldScoreTemporaryPaymentAdvancesNextContestedBattlefieldTask
```

The test proves:

- held-score temporary payment context survives actual Shadow activation, stack resolution, returned response priority, and final response pass;
- `TEMPORARY_PAYMENT_RESOURCE_SPENT` / `TEMPORARY_PAYMENT_RESOURCE_CLEARED` / `COST_PAID` / `SCORE_GAINED` happen on BF-A before current battle close;
- BF-B does not advance during response opening, Shadow activation, stack priority, stack resolution, or returned response priority;
- BF-B advances to `SPELL_DUEL_TASKS` only after BF-A score and battle close;
- final prompt is `SpellDuelFocus` for `NextBattlefieldObjectId`;
- internal `BATTLE_RESPONSE_DECLARATION_CONTEXT:*` is cleared and remains filtered from public projections.

## Validation

Evidence is recorded in:

- `docs/CURRENT_STAGE4D_02AF_BATTLE_RESPONSE_ACTIVATION_HELD_SCORE_NEXT_CONTEST_ADVANCEMENT_EVIDENCE.md`

Results:

- targeted new guard: 1/1
- focused: 289/289
- adjacent: 819/819
- backend full: 4231/4231
- `git diff --check`: no output

## Residual Risk

This slice narrows P0-004 but does not close full official battle lifecycle:

- only one activation-returned held-score temporary payment branch is paired with BF-B advancement;
- recycle-resource held-score advancement is not separately mirrored here;
- cleanup / blocker ordering, held / conquer / control / no-result matrix breadth, deeper battle-response stack chains, arbitrary multi-combat assignment, replacement, prevention, damage modification, and LayerEngine interactions remain open;
- final frontend, Chrome smoke, formal E2E, hidden-info long-chain, card coverage matrix, and completion audit remain open.

Project remains **NOT READY**.
