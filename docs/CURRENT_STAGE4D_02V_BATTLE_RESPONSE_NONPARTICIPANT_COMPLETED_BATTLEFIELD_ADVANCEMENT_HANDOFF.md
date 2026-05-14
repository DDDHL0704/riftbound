# Stage 4D-02V Battle Response Nonparticipant Completed Battlefield Advancement Handoff

日期：2026-05-15
结论：**HANDOFF READY / PROJECT NOT READY**

本文是 4D-02U 之后的下一批服务端实现交接。它只锁定 natural battle response 中真实 Shadow activation 作为非参战 response source，stack resolution 后保留 precise battlefield location，并在 final immediate battle close 后明确当前代表策略：已完成 spell duel 的当前 battlefield 不会在同一推进链里重新开启 spell duel；服务端应推进下一处未完成 contested battlefield。不授权 full combat rewrite、不改前端、不更新卡牌矩阵、不关闭 P0/P1 或 READY。

## 1. Why This Slice

4D-02U 修正并证明：非参战 Shadow response source 在 stack item 没有 movement destination 时，stack resolution 后仍保留 `ObjectLocations[ShadowObjectId].BattlefieldObjectId == BattlefieldObjectId`。

该修正让一个旧的隐性边界变得可见：4D-02P immediate battle close 代表路径中，Shadow 不是 attacker / defender。final response pass 后，如果 Shadow 仍保留在当前 concrete battlefield，服务端需要有明确 guard 证明它不会靠旧的 unknown-location 行为绕过当前 battlefield policy。

当前 runtime 的 `AdvancePendingBattlefieldTasksAfterStateChange` 会跳过 `UntilEndOfTurnEffects` 中已有 `BattlefieldTaskMarkers.SpellDuelCompleted(currentBattlefield)` 的 battlefield，并推进下一处未完成 contested battlefield。本切片的目标是把这个 same-turn completed battlefield skip policy 写成测试，而不是继续让它只是实现细节。

## 2. Target Behavior

最小代表流程：

1. `BF-DAMAGE` 与 `BF-NEXT` 同时 contested。
2. `BF-DAMAGE` 已有 `BattlefieldTaskMarkers.SpellDuelCompleted(BF-DAMAGE)`，当前 active task 是 `task:start-battle:BF-DAMAGE`。
3. P1 declares battle on `BF-DAMAGE` against only `P2-BULWARK`; `P2-SHADOW` is a legal response source on the same battlefield but is not a battle participant.
4. P2 activates Shadow targeting the attacker; stack pass-pass resolves and returns to battle response priority.
5. Before final response pass:
   - Shadow is exhausted;
   - Shadow remains in P2 battlefield zone;
   - Shadow still has `ObjectLocations[ShadowObjectId].BattlefieldObjectId == BattlefieldObjectId`;
   - `BF-NEXT` has not advanced.
6. P2 / P1 final response pass closes the battle immediately:
   - no assignment window opens;
   - `BATTLE_RESPONSE_PRIORITY_CLOSED` precedes `BATTLE_CLOSED`;
   - current `START_BATTLE:BF-DAMAGE` is removed;
   - no new `BATTLEFIELD_CONTESTED` / `SPELL_DUEL_STARTED` is emitted for `BF-DAMAGE`;
   - `UntilEndOfTurnEffects` still contains `BattlefieldTaskMarkers.SpellDuelCompleted(BF-DAMAGE)`;
   - `BF-NEXT` advances to `SPELL_DUEL_TASKS` / `SpellDuelFocus`;
   - if Shadow remains on field after battle close, its `ObjectLocation` remains precise and this fact is asserted, not hidden.

## 3. Suggested Write Scope

Owner：B 服务端规则 / 协议 / 测试实现（当前 Raman）。

允许写入：

- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`

仅 runtime gap 时允许写入：

- `src/Riftbound.Engine/CoreRuleEngine.cs`

禁止写入：

- frontend。
- PaymentEngine broad rewrite、LayerEngine。
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`。
- `riftbound-dotnet.sln`。

## 4. Implementation Notes

- Prefer a focused guard around the existing immediate activation path. This should likely be test-only unless the existing immediate test is hiding a stale current battlefield task or an unexpected current battlefield recontest event.
- Suggested test name:
  - `NaturalBattleResponseActivationImmediateBattleSkipsCompletedCurrentBattlefieldBeforeAdvancingNextTask`
- Reuse the flow from `NaturalBattleResponseActivationImmediateBattleAdvancesNextContestedBattlefieldTask`.
- Add explicit assertions after stack resolution and after final response pass:
  - Shadow is not a battle participant.
  - Shadow precise location remains `BF-DAMAGE` while it remains on field.
  - No `BATTLEFIELD_CONTESTED` / `SPELL_DUEL_STARTED` for `BF-DAMAGE` in the final response pass events.
  - `BF-DAMAGE` has no stale `START_BATTLE` active/pending task.
  - `BF-NEXT` becomes the active `START_SPELL_DUEL` task.
- If final state leaves `BattlefieldStates[BF-DAMAGE].Contested == true`, document that as a current representative policy boundary: same-turn `SpellDuelCompleted(BF-DAMAGE)` suppresses immediate re-entry, and full official recontest semantics remain later P0-004 work.

## 5. Focused Tests

Recommended targeted command:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~NaturalBattleResponseActivationImmediateBattleAdvancesNextContestedBattlefieldTask"
```

Recommended focused command:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~BattleDamageAssignmentLifecycleTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~PendingTaskQueue|FullyQualifiedName~StartBattle|FullyQualifiedName~DeclareBattle|FullyQualifiedName~TriggerPaymentTests|FullyQualifiedName~ShadowActivatedAbilityTests"
```

Focused acceptance should include:

- stack-open and returned response states do not advance `BF-NEXT`;
- final immediate battle close advances `BF-NEXT`;
- current completed `BF-DAMAGE` does not emit a second same-chain spell duel;
- precise nonparticipant response source location remains visible in authoritative state.

## 6. Adjacent Tests

Recommended adjacent command:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Battlefield|FullyQualifiedName~BattlefieldContest|FullyQualifiedName~BattlefieldTasks|FullyQualifiedName~DeclareBattle|FullyQualifiedName~StartBattle|FullyQualifiedName~SpellDuel|FullyQualifiedName~MoveUnit|FullyQualifiedName~ObjectLocation|FullyQualifiedName~GameHub|FullyQualifiedName~TriggerPayment|FullyQualifiedName~PaymentEngine|FullyQualifiedName~PAY_COST|FullyQualifiedName~ShadowActivatedAbilityTests"
```

Final per-slice gate:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

```sh
git diff --check
```

## 7. No-Go

- Do not rewrite battle lifecycle or remove the same-turn `SpellDuelCompleted` marker policy.
- Do not add frontend inference for battlefield recontest.
- Do not broaden Shadow, swift, reaction, PaymentEngine or LayerEngine behavior beyond this guard.
- Do not update card coverage matrix.
- Do not close P0-002, P0-003, P0-004, P0-005, P1 or READY.
- Do not mark active goal complete.
