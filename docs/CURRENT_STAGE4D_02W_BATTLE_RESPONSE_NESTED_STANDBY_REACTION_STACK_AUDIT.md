# Stage 4D-02W Battle Response Nested Standby Reaction Stack Audit

日期：2026-05-15
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

## Scope

4D-02W 接受一个 P0-004 battle lifecycle focused guard，用于覆盖 active battle response window 中的 nested stack breadth：

- P2 在 battle response priority 中用 Shadow swift stun 创建第一个 stack item。
- P2 pass 后，P1 在同一 stack priority window 中用 face-down standby reaction 创建第二个 stack item。
- standby reaction 作为 top stack item 先解析并回到 P1 base。
- Shadow stack item 仍保留，随后解析并回到 battle response priority。
- 整个 nested stack 过程中 `BF-NEXT` 不得提前推进。
- 只有 `BATTLE_RESPONSE_PRIORITY_CLOSED -> BATTLE_CLOSED -> BATTLEFIELD_CONTROL_RESOLVED` 后，才推进下一处 contested battlefield / spell duel task。

## Changed Files

- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`

## Runtime Changes

None. This was a test-only guard.

No changes were made to:

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- frontend files
- PaymentEngine / LayerEngine
- card coverage matrix
- `riftbound-dotnet.sln`

## Behavior Accepted

New guard:

```text
NaturalBattleResponseActivationAllowsNestedStandbyReactionStackBeforeReturningToResponse
```

The test combines two previously separate representative surfaces:

- Shadow battle-response activation / stack resolution / return-to-response.
- generic standby reaction stack nesting.

The accepted guard proves they compose under active battle response priority. It verifies prompt exposure for `REVEAL_CARD`, two-item stack ordering, standby LIFO resolution, Shadow resolution after the nested reaction, and delayed next contested battlefield advancement.

## Validation

Evidence is recorded in:

- `docs/CURRENT_STAGE4D_02W_BATTLE_RESPONSE_NESTED_STANDBY_REACTION_STACK_EVIDENCE.md`

Results:

- targeted new guard: 1/1
- focused: 278/278
- adjacent: 808/808
- backend full: 4220/4220
- `git diff --check`: no output

## Residual Risk

This slice narrows battle-response stack breadth, but it does not close full official P0-004:

- only one nested standby reaction representative is covered;
- multiple independent legal response sources are not matrix-complete;
- broader swift / reaction / counter chains remain open;
- stale target / no-effect branches across all battle-result paths remain representative;
- replacement / prevention / LayerEngine interactions remain deferred;
- final frontend, Chrome smoke, formal E2E, hidden-info long-chain, card coverage matrix, and completion audit remain open.

Project remains **NOT READY**.
