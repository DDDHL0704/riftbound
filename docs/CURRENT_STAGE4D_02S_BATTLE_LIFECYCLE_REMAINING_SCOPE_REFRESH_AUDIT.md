# Stage 4D-02S Battle Lifecycle Remaining Scope Refresh Audit

日期：2026-05-15
结论：**AUDIT COMPLETE / NEXT HANDOFF REQUIRED / PROJECT NOT READY**

本文刷新 4D-02C 后已经过时的 P0-004 battle lifecycle 剩余范围审计。4D-02B 至 4D-02R 已连续补齐 natural battle response、damage assignment、next contested battlefield advancement、post-payment blocker、actual Shadow activation / stack-return 与 no-result cross-products，但这些仍是 focused representative，不等于 full official spell duel / battle state machine 关闭。

## Evidence Inspected

- `docs/A_MASTER_AGENT_GOAL.md`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_STAGE4D_02C_BATTLE_LIFECYCLE_REMAINING_SCOPE_AUDIT.md`
- Stage 4D-02B through 4D-02R audit / evidence documents
- `tests/Riftbound.ConformanceTests/BattlefieldContestBattleTaskGuardTests.cs`
- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`
- `tests/Riftbound.ConformanceTests/SpellDuelBattleStateMachineTests.cs`
- `tests/Riftbound.ConformanceTests/ShadowActivatedAbilityTests.cs`
- `tests/Riftbound.ConformanceTests/TriggerPaymentTests.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`

## Current Verified Surface

- `START_SPELL_DUEL` / `START_BATTLE` tasks are created, promoted, exposed in prompt / reconnect metadata, and bound to the active battlefield.
- Active `START_BATTLE` prompt / command guards reject wrong battlefield, stale, wrong-player, hidden standby, equipment, spell, rune, base-zone and off-battlefield participants without mutation.
- Natural `DECLARE_BATTLE + COMBAT_ASSIGNMENT` can open `ASSIGN_COMBAT_DAMAGE` for supported assignment-ordering branches.
- Natural battle response priority can open before assignment, and pass-pass returns to assignment instead of closing or advancing the next battlefield early.
- Actual Shadow battle response activation now has guards for stack-open, stack pass-pass resolution, returned response priority, assignment continuation, immediate battle close, trigger-payment blocker, and no-result assignment continuation.
- `ASSIGN_COMBAT_DAMAGE` runtime has representative guards for stale prompt, illegal command no-mutation, simultaneous damage, cleanup, battle close, battlefield control, no-result, persisted `BattleResolutionState`, and current battle task cleanup.
- Next contested battlefield advancement is guarded for:
  - ordinary assignment battle close;
  - battle response pass -> assignment;
  - actual battle response activation -> assignment;
  - actual battle response activation -> assignment no-result;
  - actual battle response activation -> immediate battle close;
  - post-battle trigger payment blockers;
  - actual activation -> immediate battle close -> trigger payment blocker;
  - battle-control-driven illegal standby cleanup in the non-activation assignment path.
- Declaration-context carriers for Icevale trigger payment, Brush replacement, `RECYCLE_RUNE:*`, `TEMP_PAYMENT_RESOURCE:*`, and activation-returned Brush replacement are covered as internal, non-public context that does not leak into player / spectator snapshots or prompts.

## Superseded 4D-02C Gaps

The 4D-02C audit said natural active `START_BATTLE` mostly resolved through immediate combat. That is no longer the complete picture: the suite now proves natural assignment prompt/runtime binding and multiple battle-response continuations into assignment.

The 4D-02C audit said multi-attacker / multi-defender assignment lacked a natural lifecycle guard. That is now narrowed by natural assignment, battle-response assignment, activation assignment, and activation assignment no-result guards. It still remains representative, not full official breadth.

The 4D-02C audit said next contested battlefield advancement after battle close was not proven. That has been narrowed by 4D-02E, 4D-02L through 4D-02R, and trigger-payment blocker slices.

## Remaining P0-004 Gaps

- Full official battle lifecycle remains open. The current model is still a representative state machine, not a complete official combat system.
- Battle response breadth is still narrow: Shadow swift stun is the representative source, and the suite does not yet prove a full matrix of swift / reaction chains, multiple legal response sources, multiple stack items, no-effect / stale targets, and response windows across every battle-result branch.
- Cleanup / blocker ordering is not matrix-complete. The non-activation assignment path has a battle-control standby cleanup guard, but the activation-returned assignment path does not yet prove battle-control-driven cleanup blocks next contested battlefield advancement.
- Battle result ordering is representative, not exhaustive: held / conquer / control / no-result / trigger payment / cleanup combinations are not fully enumerated across immediate, assignment, response-pass and activation-returned branches.
- Damage assignment breadth is still limited by current supported representative policy: up to two attackers / defenders and known assignment-ordering keyword cases. Arbitrary official multi-combat assignment and all same-priority permutations remain outside this stage.
- Replacement / prevention / damage modification / continuous-effect LayerEngine interactions remain deferred; P0-004 should not be closed while those interactions can alter combat outcome or cleanup ordering.
- Frontend and E2E gates remain downstream. These server guards do not replace final Chrome smoke, formal 18-step rerun, hidden-info long-chain checks, full-card matrix closure, or final completion audit.

## Next Recommended Slice

Proceed with `4D-02T Battle Response Activation Assignment Standby Cleanup Advancement`.

Goal: prove the actual Shadow activation -> stack resolution -> return-to-response -> assignment branch also preserves the cleanup-first blocker ordering already proven by 4D-01C for the non-activation branch.

Recommended representative:

- `BF-A` and `BF-B` are both contested.
- `BF-A` has a hidden / illegal standby object that becomes invalid after battle control resolves.
- P1 declares a supported assignment battle on `BF-A`.
- P2 activates Shadow in battle response; stack resolves; response pass-pass opens assignment.
- Legal assignment closes `BF-A`, resolves battle control, enqueues/removes illegal standby, and only then advances `BF-B` to `SPELL_DUEL_TASKS`.

Expected guard:

- no `BF-B` advancement during response, stack-open, returned response, or assignment prompt;
- event order should prove `BATTLE_CLOSED` / `BATTLEFIELD_CONTROL_RESOLVED` precede `BATTLEFIELD_STANDBY_REMOVED`, and standby cleanup precedes `BF-B` `BATTLEFIELD_CONTESTED` / `SPELL_DUEL_STARTED`;
- no frontend, PaymentEngine, LayerEngine, card coverage matrix, or broad combat rewrite.

## No-Go

- Do not close P0-004, P0-005, P1, READY, or the active goal.
- Do not use this audit as a proxy for full official combat.
- Do not update card coverage matrix.
- Do not broaden PaymentEngine / LayerEngine.
- Do not modify frontend.
- Do not touch `riftbound-dotnet.sln`.

## Verdict

4D-02B through 4D-02R materially narrowed P0-004, especially activation-returned assignment / immediate / payment / no-result task advancement. The next useful narrow slice is cleanup-blocker ordering for the activation-returned assignment branch. Project remains **NOT READY**.
