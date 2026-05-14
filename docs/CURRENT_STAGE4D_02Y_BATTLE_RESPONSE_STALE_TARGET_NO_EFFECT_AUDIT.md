# Stage 4D-02Y Battle Response Stale Target No-Effect Audit

日期：2026-05-15
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

## Scope

4D-02Y 接受一个 P0-004 battle lifecycle focused guard，用于覆盖 active battle response stack 中 Shadow target stale / no-effect branch：

- Shadow 在 battle response priority 中创建 stack item。
- 解析前目标停止 attacking。
- stack pass-pass 后 Shadow 解析为 `ABILITY_NO_EFFECT`，reason 为 `TARGET_NO_LONGER_LEGAL`。
- attacker 不获得 `STUNNED`。
- battle 仍 active，priority 回到 P2 battle response。
- `BF-NEXT` 不在 stale no-effect stack resolution 或 returned response priority 期间提前推进。
- 最终 response close 后仍按 `BATTLE_RESPONSE_PRIORITY_CLOSED -> BATTLE_CLOSED -> BATTLEFIELD_CONTROL_RESOLVED -> BF-NEXT contested/spell duel` 推进。

## Changed Files

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`

## Runtime Change

This slice exposed a real runtime edge in `ResolveBattleResponsePriorityPassed`.

`BattleState` is dynamically derived from card `IsAttacking` / `IsDefending` flags. When Shadow's target stops attacking before stack resolution, the active battle can still have preserved declaration context, but the dynamic battle attacker list can be empty at response close time. The old response-close path rejected before it could use the preserved declaration context.

The accepted fix makes response close prefer the saved battle response declaration context, then derives:

- battle attacker object ids;
- battle defender object ids;
- battlefield id;
- attacking player id fallback from the original attacker object ids.

This is scoped to response close reconstruction. It does not broaden PaymentEngine, LayerEngine, frontend, or card matrix behavior.

## Behavior Accepted

New guard:

```text
NaturalBattleResponseActivationNoEffectForStaleTargetReturnsToResponseBeforeAdvancement
```

The test proves the existing standalone Shadow stale-target behavior now composes with active battle response priority and pending battlefield task advancement.

## Validation

Evidence is recorded in:

- `docs/CURRENT_STAGE4D_02Y_BATTLE_RESPONSE_STALE_TARGET_NO_EFFECT_EVIDENCE.md`

Results:

- targeted new guard: 1/1
- focused: 280/280
- adjacent: 810/810
- backend full: 4222/4222
- `git diff --check`: no output

## Residual Risk

This slice narrows stale/no-effect battle-response behavior, but it does not close full official P0-004:

- only Shadow stale-target no-effect is covered;
- natural card-driven target movement/removal before resolution is not matrix-complete;
- broader cross-card swift / reaction chains remain representative;
- battle-result ordering across all held / conquer / control / no-result / payment combinations remains incomplete;
- replacement / prevention / damage modification / LayerEngine interactions remain deferred;
- final frontend, Chrome smoke, formal E2E, hidden-info long-chain, card coverage matrix, and completion audit remain open.

Project remains **NOT READY**.
