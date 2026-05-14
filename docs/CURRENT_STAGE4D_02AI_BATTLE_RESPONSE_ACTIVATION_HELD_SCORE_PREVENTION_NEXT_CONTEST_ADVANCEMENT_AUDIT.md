# Stage 4D-02AI Battle Response Activation Held-Score Prevention Next Contest Advancement Audit

日期：2026-05-15
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

## Scope

4D-02AI 接受一个 P0-004 battle lifecycle focused guard，用于覆盖 BF-A / BF-B 双争夺场景中的 activation-returned held-score score-prevention branch next-contested advancement：

- BF-A 是 `SFD·214/221` held-score battlefield；
- P2 同时控制 `SFD·209/221` score-delay battlefield，且当前 turn ordinal 尚未释放得分；
- BF-A 已完成 spell duel，当前 active `START_BATTLE` 从 BF-A 打开 natural battle response priority；
- BF-B 同时 contested，等待后续 `START_SPELL_DUEL` advancement；
- P1 declaration optional costs 只包含 `COMBAT_ASSIGNMENT`；
- P2 在 battle response window 中真实 activation Shadow，并通过 stack pass-pass 解析；
- response、stack、returned response 阶段均不得提前推进 BF-B；
- returned response pass-pass 后，BF-A held-score branch 被 `BATTLEFIELD_SCORE_PREVENTED` 阻止；
- prevention 分支不得支付 held-score cost、不得 `SCORE_GAINED`；
- BF-B `BATTLEFIELD_CONTESTED` / `SPELL_DUEL_STARTED` 只能在 score prevention 与 BF-A battle close 后出现。

## Changed Files

- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`

## Runtime Change

None. This is a test-only guard.

The accepted guard composes existing runtime behavior from:

- direct battlefield score-delay prevention (`SFD·209/221`);
- 4D-02AF / 4D-02AG activation-returned held-score next-contested advancement pattern;
- existing battle-response declaration context redaction and no-early-BF-B assertions.

This does not broaden PaymentEngine, LayerEngine, frontend, card matrix, damage prevention, or full combat breadth.

## Behavior Accepted

New guard:

```text
NaturalBattleResponseActivationHeldScorePreventionAdvancesNextContestedBattlefieldTask
```

The test proves:

- BF-A / BF-B are both contested while BF-A is the active `START_BATTLE`;
- actual Shadow activation, stack pass-pass resolution, and returned response priority all preserve the battle-response declaration context without exposing it publicly;
- BF-B does not advance during response opening, Shadow activation, stack priority, stack resolution, or returned response priority;
- final held-score branch emits `BATTLEFIELD_HELD` before `BATTLEFIELD_SCORE_PREVENTED`;
- `BATTLEFIELD_SCORE_PREVENTED.trigger == "BATTLEFIELD_SCORE_DELAY_UNTIL_THIRD_TURN"`;
- `BATTLEFIELD_SCORE_PREVENTED.preventedReason == "BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE"`;
- score-delay source ids include the P2 `SFD·209/221` battlefield, and score-source ids include BF-A;
- no held-score `COST_PAID` and no held-score `SCORE_GAINED` are emitted;
- P2 score remains 0 and P2 keeps the 4 power left after Shadow activation;
- score prevention happens before BF-A `BATTLE_CLOSED`;
- BF-A `BATTLE_CLOSED` happens before BF-B `BATTLEFIELD_CONTESTED` / `SPELL_DUEL_STARTED`;
- final prompt is `SpellDuelFocus` for `NextBattlefieldObjectId`;
- no stale BF-A assignment or battle declaration prompt remains.

## Validation

Evidence is recorded in:

- `docs/CURRENT_STAGE4D_02AI_BATTLE_RESPONSE_ACTIVATION_HELD_SCORE_PREVENTION_NEXT_CONTEST_ADVANCEMENT_EVIDENCE.md`

Results:

- targeted new guard: 1/1
- focused: 292/292
- adjacent: 822/822
- backend full: 4234/4234
- `git diff --check`: no output

## Residual Risk

This slice narrows P0-004 but does not close full official battle lifecycle:

- held-score score-prevention next-contested advancement is now represented for `SFD·209/221`, but not matrix-complete across all prevention, replacement, damage modification, and LayerEngine branches;
- cleanup / blocker ordering, held / conquer / control / no-result matrix breadth, deeper battle-response stack chains, arbitrary multi-combat assignment, damage prevention, damage modification, and LayerEngine interactions remain open;
- final frontend, Chrome smoke, formal E2E, hidden-info long-chain, card coverage matrix, and completion audit remain open.

Project remains **NOT READY**.
