# Stage 4D-02X Battle Response Multiple Legal Sources Audit

日期：2026-05-15
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

## Scope

4D-02X 接受一个 P0-004 battle lifecycle focused guard，用于覆盖同一 battle response window 中多个独立合法 response sources 的 representative path：

- 初始 P2 battle response prompt 同时公开两个 ready Shadow sources。
- Shadow A activation / stack resolution 后，Shadow A exhausted 且不再公开，Shadow B 仍保持 ready 并继续作为合法 source。
- Shadow B activation / stack resolution 后，两个 Shadow 都 exhausted 且不再作为 enabled source 暴露。
- 两次 stack open / pass-pass resolution / returned-response 期间 `BF-NEXT` 不得提前推进。
- 最终只有 `BATTLE_RESPONSE_PRIORITY_CLOSED -> BATTLE_CLOSED -> BATTLEFIELD_CONTROL_RESOLVED` 后才推进 `BF-NEXT` contest / spell duel。

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
NaturalBattleResponseAllowsMultipleLegalSourcesSequentiallyBeforeAdvancement
```

The test proves prompt source filtering and stack-return semantics compose when more than one response source is available to the same player inside one battle response window. It specifically guards against:

- only exposing one of multiple legal response sources;
- leaving an exhausted source enabled after it resolves;
- losing the second source after the first stack item resolves;
- advancing the next contested battlefield while response stack items or returned response priority are still open.

## Validation

Evidence is recorded in:

- `docs/CURRENT_STAGE4D_02X_BATTLE_RESPONSE_MULTIPLE_LEGAL_SOURCES_EVIDENCE.md`

Results:

- targeted new guard: 1/1
- focused: 279/279
- adjacent: 809/809
- backend full: 4221/4221
- `git diff --check`: no output

## Residual Risk

This slice narrows multiple-source battle-response breadth, but it does not close full official P0-004:

- both sources are Shadow representatives, not a broad swift / reaction source family;
- deeper reaction chains and opponent responses after each source remain representative;
- stale target / no-effect response handling remains open;
- battle-result ordering across all held / conquer / control / no-result / payment combinations remains incomplete;
- replacement / prevention / damage modification / LayerEngine interactions remain deferred;
- final frontend, Chrome smoke, formal E2E, hidden-info long-chain, card coverage matrix, and completion audit remain open.

Project remains **NOT READY**.
