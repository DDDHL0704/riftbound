# Stage 4D-02F Battle Assignment No-Result Evidence

日期：2026-05-14
结论：**VALIDATED / PROJECT NOT READY**

## Changed Paths

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`
- `docs/CURRENT_STAGE4D_02F_BATTLE_ASSIGNMENT_NO_RESULT_AUDIT.md`
- `docs/CURRENT_STAGE4D_02F_BATTLE_ASSIGNMENT_NO_RESULT_EVIDENCE.md`

## Validation

Focused battle lifecycle:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattleDamageAssignmentLifecycleTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~Shadow|FullyQualifiedName~DeclareBattle|FullyQualifiedName~StartBattle|FullyQualifiedName~Priority|FullyQualifiedName~Reaction|FullyQualifiedName~Swift|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

Result: passed 428/428.

Adjacent battlefield / task regression:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Battlefield|FullyQualifiedName~SpellDuel|FullyQualifiedName~DeclareBattle|FullyQualifiedName~MoveUnit|FullyQualifiedName~BoardTaskQueue|FullyQualifiedName~ActivateAbility|FullyQualifiedName~PaymentEngineCoverageAuditTests"
```

Result: passed 608/608.

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: passed 4196/4196.

`git diff --check`: passed.

## Evidence Points

- Natural assignment no-result emits `BATTLE_NO_RESULT` with `reason = ALL_PARTICIPANTS_DESTROYED`.
- `BATTLE_CLOSED` is emitted even when every participant was removed during cleanup.
- No `BATTLEFIELD_HELD` or `BATTLEFIELD_CONQUERED` event is emitted for the no-result battle.
- Matching `START_BATTLE` is cleared and prompt is not stale `ASSIGN_COMBAT_DAMAGE` / `DECLARE_BATTLE`.
- `BattleResolutionState.Kind` is `NO_RESULT`, `WinnerPlayerId` is null, surviving attacker/defender lists are empty, and destroyed object ids contain all participants.

## Remaining Risk

Project remains NOT READY. This slice narrows P0-004 for the no-result assignment branch only and does not claim full official battle lifecycle closure.
