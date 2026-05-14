# Stage 4D-02S Battle Lifecycle Remaining Scope Refresh Audit

日期：2026-05-15
结论：**AUDIT COMPLETE / NEXT HANDOFF REQUIRED / PROJECT NOT READY**

本文刷新 4D-02C 后已经过时的 P0-004 battle lifecycle 剩余范围审计。4D-02B 至 4D-02R 已连续补齐 natural battle response、damage assignment、next contested battlefield advancement、post-payment blocker、actual Shadow activation / stack-return 与 no-result cross-products，但这些仍是 focused representative，不等于 full official spell duel / battle state machine 关闭。

2026-05-15 follow-up：4D-02T 已验收 activation-returned assignment branch 的 cleanup-first blocker ordering；下一切片转为 4D-02U，锁定 actual Shadow activation 作为非参战 response source 时 stack resolution 后的 precise `ObjectLocation.BattlefieldObjectId` preservation。4D-02S 原始建议保留为历史审计背景，不代表当前最新 handoff。

2026-05-15 follow-up：4D-02U 已验收非参战 response source stack-resolution precise location preservation；下一切片转为 4D-02V，锁定该 precise location 修正后 final immediate battle close 的 same-turn completed battlefield skip policy。也就是：当前 battlefield 已完成 spell duel 后，即使非参战 Shadow 仍保留在当前 concrete battlefield，当前推进链仍不重新开启该 battlefield 的 spell duel，而是推进下一处未完成 contested battlefield。

2026-05-15 follow-up：4D-02V 已验收上述 same-turn completed battlefield skip policy，且为 test-only guard。P0-004 仍 open，剩余重点转回 full battle lifecycle breadth：battle response source / stack breadth、cleanup / blocker matrix、battle result ordering matrix、damage assignment breadth、replacement/prevention/LayerEngine 交织和最终 frontend/E2E gates。

2026-05-15 follow-up：4D-02W 已验收 battle response nested standby reaction stack representative。该 test-only guard 证明 Shadow battle-response stack item 上方可加入 P1 standby reaction stack item，并按 LIFO 回到 Shadow stack item、battle response priority 与最终 next contested battlefield advancement。P0-004 仍 open；该切片只收窄 multiple-stack-items breadth 的一个 representative。

2026-05-15 follow-up：下一切片 4D-02X 转向 battle response multiple legal sources。目标是证明同一 battle response window 内两个 ready Shadow response sources 先同时公开，第一个 source 解析回 response 后第二个仍可合法入栈，并继续阻止 `BF-NEXT` 提前推进。

2026-05-15 follow-up：4D-02X 已验收上述 multiple legal sources representative。P0-004 仍 open；该切片只证明两个 Shadow sources 的同窗口顺序消耗，不代表完整 swift / reaction source family 或 stale/no-effect target matrix。

2026-05-15 follow-up：下一切片 4D-02Y 转向 battle response stale target no-effect。目标是把已有 standalone Shadow stale-target no-effect guard 组合进 active battle response stack-return / no early next-battlefield advancement path。

2026-05-15 follow-up：4D-02Y 已验收上述 stale target no-effect representative，并修复 response close 优先使用保存 declaration context 的边界。P0-004 仍 open；该切片只覆盖 Shadow stale-target no-effect，不代表完整 stale/no-effect target matrix。

2026-05-15 follow-up：下一切片 4D-02Z 转向 battle-result ordering matrix，锁定 natural battle response pass -> assignment -> conquer result representative。目标是证明 `BATTLEFIELD_CONQUERED` / `EXPERIENCE_GAINED` 先于 `BATTLE_CLOSED`、`BATTLEFIELD_CONTROL_RESOLVED` 和 `BF-NEXT` 推进。

2026-05-15 follow-up：4D-02Z 已验收上述 response-pass assignment Hunt conquer result ordering representative，并修复 assignment prompt 分支缺少 Hunt conquer result / experience 的 runtime gap。P0-004 仍 open；该切片只覆盖 Hunt conquer representative，不代表完整 held / conquer trigger / replacement / prevention result matrix。

2026-05-15 follow-up：下一切片 4D-02AA 转向对称的 battle-response held result ordering representative。目标是证明 natural battle response pass -> assignment 后，防守方 Hunt held result 的 `BATTLEFIELD_HELD` / `EXPERIENCE_GAINED` 先于 cleanup、`BATTLE_CLOSED`、`BATTLEFIELD_CONTROL_RESOLVED` 和 `BF-NEXT` 推进。

2026-05-15 follow-up：4D-02AA 已验收上述 response-pass assignment Hunt held result ordering representative，并修复 assignment prompt 分支缺少 defender-held Hunt result / experience 的 runtime gap。P0-004 仍 open；该切片只覆盖 Hunt held representative，不代表完整 held-trigger / held-score payment / replacement / prevention result matrix。

2026-05-15 follow-up：下一切片 4D-02AB 转向 activation-returned assignment result ordering cross-product。目标是证明 actual Shadow activation / stack resolution / returned response 后进入 assignment，攻击方 Hunt conquer result 的 `BATTLEFIELD_CONQUERED` / `EXPERIENCE_GAINED` 仍先于 cleanup、`BATTLE_CLOSED`、`BATTLEFIELD_CONTROL_RESOLVED` 和 `BF-NEXT` 推进。

2026-05-15 follow-up：4D-02AB 已验收上述 activation-returned assignment Hunt conquer result ordering representative，且为 test-only guard。P0-004 仍 open；该切片只证明 actual Shadow activation / stack resolution / returned response -> assignment -> Hunt conquer representative，不代表 activation-returned held branch、cleanup-blocker matrix、replacement / prevention / LayerEngine result matrix 或 full official combat breadth。

2026-05-15 follow-up：下一切片 4D-02AC 转向 activation-returned assignment held result ordering cross-product。目标是证明 actual Shadow activation / stack resolution / returned response 后进入 assignment，防守方 Hunt held result 的 `BATTLEFIELD_HELD` / `EXPERIENCE_GAINED` 仍先于 cleanup、`BATTLE_CLOSED`、`BATTLEFIELD_CONTROL_RESOLVED` 和 `BF-NEXT` 推进。

2026-05-15 follow-up：4D-02AC 已验收上述 activation-returned assignment Hunt held result ordering representative，且为 test-only guard。P0-004 仍 open；该切片只证明 actual Shadow activation / stack resolution / returned response -> assignment -> Hunt held representative，不代表 cleanup-blocker matrix、held-score payment、replacement / prevention / LayerEngine result matrix 或 full official combat breadth。

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
- Battle response stack breadth now has one nested standby reaction representative: a P1 standby reaction can be added above a Shadow response stack item, resolve first by LIFO, and return to Shadow / battle response before next battlefield advancement.
- Multiple legal response source breadth now has one representative: two ready Shadow sources can be exposed together and consumed sequentially in the same battle response window before next battlefield advancement.
- Stale/no-effect response target breadth now has one representative: Shadow no-effects when its target stops attacking inside battle response stack resolution, then returns to response priority before next battlefield advancement.
- Battle-result ordering now has one response-pass assignment Hunt conquer representative: `BATTLEFIELD_CONQUERED` / `EXPERIENCE_GAINED` happen before damage cleanup, battle close, battlefield control resolution, and next contested battlefield advancement.
- Battle-result ordering now also has one response-pass assignment Hunt held representative: `BATTLEFIELD_HELD` / `EXPERIENCE_GAINED` happen before damage cleanup, battle close, battlefield control resolution, and next contested battlefield advancement.
- Battle-result ordering now has one activation-returned assignment Hunt conquer representative after actual Shadow activation / stack resolution / returned response priority: `BATTLEFIELD_CONQUERED` / `EXPERIENCE_GAINED` still happen before damage cleanup, battle close, battlefield control resolution, and next contested battlefield advancement.
- Battle-result ordering now also has one activation-returned assignment Hunt held representative after actual Shadow activation / stack resolution / returned response priority: `BATTLEFIELD_HELD` / `EXPERIENCE_GAINED` still happen before damage cleanup, battle close, battlefield control resolution, and next contested battlefield advancement.
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
- Battle response breadth is still narrow: Shadow swift stun plus one nested standby reaction stack, two-Shadow source sequencing, and one Shadow stale-target no-effect branch are representative, and the suite does not yet prove a full matrix of swift / reaction chains, cross-card independent legal response sources, deeper multiple stack items, no-effect / stale targets, and response windows across every battle-result branch.
- Cleanup / blocker ordering is not matrix-complete. The non-activation assignment path has a battle-control standby cleanup guard, but the activation-returned assignment path does not yet prove battle-control-driven cleanup blocks next contested battlefield advancement.
- Battle result ordering is representative, not exhaustive: held / conquer / control / no-result / trigger payment / cleanup combinations are not fully enumerated across immediate, assignment, response-pass and activation-returned branches.
- Damage assignment breadth is still limited by current supported representative policy: up to two attackers / defenders and known assignment-ordering keyword cases. Arbitrary official multi-combat assignment and all same-priority permutations remain outside this stage.
- Replacement / prevention / damage modification / continuous-effect LayerEngine interactions remain deferred; P0-004 should not be closed while those interactions can alter combat outcome or cleanup ordering.
- Frontend and E2E gates remain downstream. These server guards do not replace final Chrome smoke, formal 18-step rerun, hidden-info long-chain checks, full-card matrix closure, or final completion audit.

## Next Recommended Slice

4D-02T through 4D-02W 已完成. Next should continue through the broader remaining P0-004 matrix rather than extending the same Shadow / standby nested-stack branch indefinitely.

Suggested next goal after 4D-02Y: choose a new narrow representative from the remaining matrix, such as a battle-result ordering branch not yet covered by the current assignment / immediate / payment / no-result paths.

4D-02Z selected representative: natural battle response pass -> assignment -> Hunt conquer result ordering. This intentionally targets result ordering rather than additional Shadow activation/source breadth. 4D-02Z is now accepted.

4D-02AA selected representative: natural battle response pass -> assignment -> Hunt held result ordering. This intentionally mirrors 4D-02Z without expanding held-score payment, replacement, or full battlefield trigger breadth.

4D-02AB selected representative: natural battle response actual Shadow activation -> returned response -> assignment -> Hunt conquer result ordering. This composes existing activation-returned assignment advancement with 4D-02Z result ordering. 4D-02AB is now accepted.

4D-02AC selected representative: natural battle response actual Shadow activation -> returned response -> assignment -> Hunt held result ordering. This composes existing activation-returned assignment advancement with 4D-02AA result ordering. 4D-02AC is now accepted.

Recommended representative:

- `BF-A` and `BF-B` are both contested.
- Avoid repeating 4D-02U / 4D-02V source-location and same-turn completed battlefield policy.
- Prefer a distinct axis that materially narrows P0-004 full-official risk.
- Keep scope server-only unless the selected slice explicitly needs UI verification.

Expected guard:

- targeted focused guard first;
- adjacent battlefield / spell-duel / response / object-location tests;
- full backend test;
- no frontend, PaymentEngine, LayerEngine, card coverage matrix, or broad combat rewrite.

## No-Go

- Do not close P0-004, P0-005, P1, READY, or the active goal.
- Do not use this audit as a proxy for full official combat.
- Do not update card coverage matrix.
- Do not broaden PaymentEngine / LayerEngine.
- Do not modify frontend.
- Do not touch `riftbound-dotnet.sln`.

## Verdict

4D-02B through 4D-02AC materially narrowed P0-004, especially activation-returned assignment / immediate / payment / no-result task advancement, cleanup-blocker ordering, precise object-location preservation for nonparticipant response sources, same-turn completed battlefield skip policy, one nested battle-response stack representative, one multiple-legal-source response representative, one stale-target no-effect response representative, response-pass assignment Hunt conquer / held result-ordering representatives, and activation-returned assignment Hunt conquer / held result-ordering representatives. Project remains **NOT READY**.
