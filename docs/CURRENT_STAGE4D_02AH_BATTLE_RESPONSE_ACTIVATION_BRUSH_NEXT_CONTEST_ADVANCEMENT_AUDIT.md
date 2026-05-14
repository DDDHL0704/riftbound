# Stage 4D-02AH Battle Response Activation Brush Next Contest Advancement Audit

日期：2026-05-15
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

## Scope

4D-02AH 接受一个 P0-004 battle lifecycle focused guard，用于覆盖 BF-A / BF-B 双争夺场景中的 activation-returned Brush replacement-aware held-score branch next-contested advancement：

- BF-A 是 Brush battlefield，并替代原始 `SFD·214/221` held-score battlefield；
- BF-A 已完成 spell duel，当前 active `START_BATTLE` 从 BF-A 打开 natural battle response priority；
- BF-B 同时 contested，等待后续 `START_SPELL_DUEL` advancement；
- P1 declaration optional costs 包含 `COMBAT_ASSIGNMENT` 与 `BRUSH_USE_REPLACED_BATTLEFIELD:<OriginalHeldScoreBattlefieldObjectId>`；
- P2 在 battle response window 中真实 activation Shadow，并通过 stack pass-pass 解析；
- response、stack、returned response 阶段均不得提前推进 BF-B；
- returned response pass-pass 后，BF-A replacement-aware held-score branch 以原始 held-score battlefield 为 effective battlefield，P2 得分，current battle close；
- BF-B `BATTLEFIELD_CONTESTED` / `SPELL_DUEL_STARTED` 只能在 Brush replacement、held-score score 和 BF-A battle close 后出现。

## Changed Files

- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`

## Runtime Change

None. This is a test-only guard.

The accepted guard composes existing runtime behavior from:

- 4D-02K activation-returned Brush replacement context preservation;
- 4D-02AF / 4D-02AG activation-returned held-score next-contested advancement pattern;
- existing BF-NEXT no-early-advancement assertions.

This does not broaden PaymentEngine, LayerEngine, frontend, card matrix, prevention, or full combat breadth.

## Behavior Accepted

New guard:

```text
NaturalBattleResponseActivationBrushReplacementAdvancesNextContestedBattlefieldTask
```

The test proves:

- Brush replacement context survives actual Shadow activation, stack resolution, returned response priority, and final response pass;
- internal `BATTLE_RESPONSE_DECLARATION_CONTEXT:*` remains filtered from public projections while carried and is cleared after final resume;
- BF-B does not advance during response opening, Shadow activation, stack priority, stack resolution, or returned response priority;
- `BATTLEFIELD_REPLACEMENT_APPLIED` points from the Brush battlefield to `OriginalHeldScoreBattlefieldObjectId`;
- held-score trigger, `COST_PAID`, and `SCORE_GAINED` use `OriginalHeldScoreBattlefieldObjectId`;
- Brush replacement, held-score payment / score, and BF-A `BATTLE_CLOSED` happen before BF-B `BATTLEFIELD_CONTESTED` / `SPELL_DUEL_STARTED`;
- final prompt is `SpellDuelFocus` for `NextBattlefieldObjectId`;
- no stale BF-A assignment or battle declaration prompt remains.

## Validation

Evidence is recorded in:

- `docs/CURRENT_STAGE4D_02AH_BATTLE_RESPONSE_ACTIVATION_BRUSH_NEXT_CONTEST_ADVANCEMENT_EVIDENCE.md`

Results:

- targeted new guard: 1/1
- focused: 291/291
- adjacent: 821/821
- backend full: 4233/4233
- `git diff --check`: no output

## Residual Risk

This slice narrows P0-004 but does not close full official battle lifecycle:

- replacement-aware held-score next-contested advancement is now represented for Brush, but not matrix-complete across all replacement / prevention branches;
- cleanup / blocker ordering, held / conquer / control / no-result matrix breadth, deeper battle-response stack chains, arbitrary multi-combat assignment, prevention, damage modification, and LayerEngine interactions remain open;
- final frontend, Chrome smoke, formal E2E, hidden-info long-chain, card coverage matrix, and completion audit remain open.

Project remains **NOT READY**.
