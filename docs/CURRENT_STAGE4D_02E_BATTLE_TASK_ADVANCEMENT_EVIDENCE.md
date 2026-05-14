# Stage 4D-02E Battle Task Advancement Evidence

日期：2026-05-14
结论：**VALIDATED / PROJECT NOT READY**

## Changed Paths

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`
- `docs/CURRENT_STAGE4D_02E_BATTLE_TASK_ADVANCEMENT_AUDIT.md`
- `docs/CURRENT_STAGE4D_02E_BATTLE_TASK_ADVANCEMENT_EVIDENCE.md`

## Validation

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

`git diff --check`: passed.

## Evidence Points

- Legal `ASSIGN_COMBAT_DAMAGE` closes `BF-DAMAGE` battle and clears matching `START_BATTLE`.
- `BF-DAMAGE` battle close emits battle cleanup/control events.
- Remaining contested `BF-NEXT` immediately emits `BATTLEFIELD_CONTESTED` and `SPELL_DUEL_STARTED`.
- Resulting state enters `SPELL_DUEL_OPEN`, with `FocusPlayerId`, `ActivePlayerId`, `PendingTaskQueue.Phase`, and `PendingTaskQueue.ActiveTaskId` pointing at `BF-NEXT`.
- Prompt is `SPELL_DUEL_FOCUS`, not stale `WAIT`, `ASSIGN_COMBAT_DAMAGE`, or `DECLARE_BATTLE`.

## Remaining Risk

The project remains NOT READY. This is a focused advancement guard and does not close P0-004, P0-005, P1, LayerEngine, frontend smoke, or full official battle lifecycle coverage.
