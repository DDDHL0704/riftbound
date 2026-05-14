# Stage 4D-02F Battle Assignment No-Result Baseline Evidence

日期：2026-05-14
结论：**BASELINE GREEN / IMPLEMENTATION REQUIRED / PROJECT NOT READY**

本文记录 4D-02F 实现前基线。当前 HEAD 已通过 4D-02E focused / adjacent / backend full tests，但这些测试尚未覆盖 natural `ASSIGN_COMBAT_DAMAGE` 分支的 `BATTLE_NO_RESULT`。

## Baseline Commands

Focused battle lifecycle:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattleDamageAssignmentLifecycleTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~Shadow|FullyQualifiedName~DeclareBattle|FullyQualifiedName~StartBattle|FullyQualifiedName~Priority|FullyQualifiedName~Reaction|FullyQualifiedName~Swift|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

Result: passed 427/427.

Adjacent battlefield / task regression:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Battlefield|FullyQualifiedName~SpellDuel|FullyQualifiedName~DeclareBattle|FullyQualifiedName~MoveUnit|FullyQualifiedName~BoardTaskQueue|FullyQualifiedName~ActivateAbility|FullyQualifiedName~PaymentEngineCoverageAuditTests"
```

Result: passed 608/608.

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: passed 4195/4195.

Diff hygiene:

```sh
git diff --check
```

Result: passed.

## Evidence Inspected

- `docs/CURRENT_STAGE4D_02E_BATTLE_TASK_ADVANCEMENT_AUDIT.md`
- `docs/CURRENT_STAGE4D_02E_BATTLE_TASK_ADVANCEMENT_EVIDENCE.md`
- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`

## Gap Confirmed

- Direct/minimal `DECLARE_BATTLE` no-result representative exists via `P4DeclareBattleCommandEmitsNoResultWhenAllParticipantsAreDestroyed`.
- Natural assignment tests currently cover assignment prompt open, wrong/stale no-mutation, legal close/control, one-on-one immediate stability, response->assignment ordering, and next contested task advancement.
- There is no focused natural assignment test for all participants destroyed / `BATTLE_NO_RESULT` / `BattleResolutionState.Kind = NO_RESULT`.

## Verdict

Baseline is green, but 4D-02F remains unimplemented. P0-004 remains open; P0-005, P1, frontend final gates, card matrix, READY, and active goal remain open.
