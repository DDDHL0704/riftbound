# Stage 4D-02G Battle Response Declaration Context Evidence

日期：2026-05-14
结论：**ACCEPTED / PROJECT NOT READY**

## Changed Files

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`

## Evidence Summary

4D-02G proves that an active natural `START_BATTLE` declaration carrying server-side battle declaration context no longer skips legal battle-response priority.

The focused test uses an Icevale Archer attack target context plus a legal Shadow battle-response window:

- `DECLARE_BATTLE` carries `BattlefieldTargetObjectIds = [AttackerObjectId]`;
- battle response opens first;
- both players pass priority;
- resumed battle still has the original target context;
- Icevale Archer trigger payment opens from the preserved context.

The test also verifies the internal context carrier remains server-only:

- P1 snapshot does not contain `BATTLE_RESPONSE_DECLARATION_CONTEXT`;
- P2 snapshot does not contain `BATTLE_RESPONSE_DECLARATION_CONTEXT`;
- spectator snapshot does not contain `BATTLE_RESPONSE_DECLARATION_CONTEXT`;
- P2 prompt does not contain `BATTLE_RESPONSE_DECLARATION_CONTEXT`.

## Validation

Focused:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattleDamageAssignmentLifecycleTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~Shadow|FullyQualifiedName~DeclareBattle|FullyQualifiedName~StartBattle|FullyQualifiedName~Priority|FullyQualifiedName~Reaction|FullyQualifiedName~Swift|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

Result:

```text
Passed: 429, Failed: 0, Skipped: 0, Total: 429
```

Adjacent:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Battlefield|FullyQualifiedName~SpellDuel|FullyQualifiedName~DeclareBattle|FullyQualifiedName~MoveUnit|FullyQualifiedName~BoardTaskQueue|FullyQualifiedName~ActivateAbility|FullyQualifiedName~PaymentEngineCoverageAuditTests"
```

Result:

```text
Passed: 608, Failed: 0, Skipped: 0, Total: 608
```

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result:

```text
Passed: 4197, Failed: 0, Skipped: 0, Total: 4197
```

Whitespace:

```sh
git diff --check
```

Result: no output.

## Verdict

4D-02G is accepted as a focused P0-004 narrowing slice. It does not close full official battle lifecycle, P0-005, P1, frontend final gates, card matrix, READY, or the active goal.
