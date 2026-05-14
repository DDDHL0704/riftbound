# Stage 4D-02L Immediate Battle Task Advancement Handoff

日期：2026-05-14
结论：**HANDOFF READY / PROJECT NOT READY**

本文是 4D-02K 后继续收窄 P0-004 battle lifecycle 的下一批服务端实现交接。它只锁定 immediate `DECLARE_BATTLE` 直接结算分支在 battle close / battlefield control / held / conquer 后与 `ASSIGN_COMBAT_DAMAGE` 分支保持相同 pending battlefield task advancement 语义，不授权 full combat rewrite、不改前端、不更新卡牌矩阵、不关闭 P0/P1 或 READY。

## 1. Why This Slice

4D-02E 已证明 `ASSIGN_COMBAT_DAMAGE` 分支在关闭当前 battle 后会推进下一个 contested battlefield。4D-01C 又证明 assignment 分支里的 battlefield control standby cleanup 会先于 next contested task advancement。

只读检查发现 immediate `DECLARE_BATTLE` 直接结算分支在 `CoreRuleEngine.cs` 中完成 `ApplyBattleCleanup`、`CloseResolvedBattle`、`ResolveBattlefieldControlAfterBattle`、`AppendBattlefieldResolutionEvents` 和 `AppendBattleResolutionEvents` 后直接返回 `nextState`；不像 `ASSIGN_COMBAT_DAMAGE` 分支那样调用 `AdvancePendingBattlefieldTasksAfterStateChange(...)`。

因此需要一个 dedicated guard 证明：不进入 damage assignment prompt 的普通 battle 也会在当前 `START_BATTLE` 结算关闭后继续推进后续 contested battlefield，而不会停在 stale `BATTLE_TASKS` / `WAIT`。

## 2. Target Behavior

最小代表流程：

1. `BF-A` 已完成 spell duel，当前 pending queue active task 是 `task:start-battle:BF-A`。
2. P1 在 `BF-A` 提交 immediate `DECLARE_BATTLE`，该 battle 不需要 `ASSIGN_COMBAT_DAMAGE` prompt。
3. `BF-A` battle 结算、battle cleanup、battle close、battlefield control / held / conquer resolution 均完成。
4. 当前 `BF-A` 的 matching `START_BATTLE` task 被移除，不残留 stale battle prompt。
5. 若 `BF-B` 已 contested 且尚未完成 spell duel，服务端立即推进到 `SPELL_DUEL_TASKS`：
   - 输出 `BATTLEFIELD_CONTESTED`；
   - 输出 `SPELL_DUEL_STARTED`；
   - active task 为 `task:start-spell-duel:BF-B`；
   - prompt 为 `SpellDuelFocus` 且 related battlefield 是 `BF-B`。
6. 如果 immediate battle 打开 pending payment、进入 finished status、仍有 cleanup task、stack、active spell duel 或 active battle 阻塞，则不得提前推进下一 contested battlefield。

## 3. Suggested Write Scope

Owner：B 服务端规则 / 协议 / 测试实现（当前 Raman）。

允许写入：

- `tests/Riftbound.ConformanceTests/BattlefieldContestBattleTaskGuardTests.cs`
- 或 `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`

仅 runtime gap 时允许写入：

- `src/Riftbound.Engine/CoreRuleEngine.cs`

禁止写入：

- `src/Riftbound.Engine/MatchSession.cs`，除非发现 prompt/snapshot contract gap。
- PaymentEngine / LayerEngine。
- frontend。
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`。

## 4. Implementation Notes

- Prefer a failing guard first. If current runtime already advances tasks for immediate battle through another route, keep the slice test-only.
- Minimal runtime shape should mirror the assignment branch: after constructing the post-battle `nextState`, call the shared post-state-change battlefield task advancement only when no pending payment / terminal status / active stack / cleanup / active battle / active spell duel blocks it.
- Preserve current payment-window semantics: conquest / held triggers that open `PendingPayment` must keep the payment window authoritative and should not auto-advance another battlefield until the payment path closes and existing post-state-change logic permits advancement.
- Preserve existing `BATTLE_DECLARED`, `BATTLE_CLOSED`, `BATTLEFIELD_CONTROL_RESOLVED`, `BATTLEFIELD_HELD`, `BATTLEFIELD_CONQUERED`, battle resolution and battlefield resolution audit payloads.

## 5. Focused Tests

Recommended focused command:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~BattleDamageAssignmentLifecycleTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~PendingTaskQueue|FullyQualifiedName~StartBattle|FullyQualifiedName~DeclareBattle"
```

Focused acceptance should include:

- Immediate `DECLARE_BATTLE` clearing current `START_BATTLE:BF-A`.
- Next contested `BF-B` entering `SPELL_DUEL_TASKS` without a stale `BF-A` battle prompt.
- Event order keeps battle declaration / close / control-or-resolution before `BF-B` contest / spell-duel start.
- Blocking case for `PendingPayment` or an explicit justification if an existing test already covers it.

## 6. Adjacent Tests

Recommended adjacent command:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Battlefield|FullyQualifiedName~BattlefieldContest|FullyQualifiedName~BattlefieldTasks|FullyQualifiedName~DeclareBattle|FullyQualifiedName~StartBattle|FullyQualifiedName~SpellDuel|FullyQualifiedName~MoveUnit|FullyQualifiedName~ObjectLocation|FullyQualifiedName~GameHub"
```

Final per-slice gate:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

## 7. No-Go

- Do not rewrite combat damage assignment.
- Do not broaden battle response, payment, replacement or LayerEngine behavior.
- Do not modify frontend task UI.
- Do not update card coverage matrix.
- Do not close P0-002, P0-003, P0-004, P0-005, P1 or READY.
- Do not mark active goal complete.
