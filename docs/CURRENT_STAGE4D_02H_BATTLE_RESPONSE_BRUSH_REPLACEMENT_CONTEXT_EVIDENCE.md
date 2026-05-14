# Stage 4D-02H Battle Response Brush Replacement Context Evidence

日期：2026-05-14
结论：**ACCEPTED / PROJECT NOT READY**

## Changed Files

- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`

## Evidence Summary

4D-02H proves that the 4D-02G server-side declaration context carrier also preserves Brush replacement optional cost context through battle-response pass.

The focused test constructs:

- a defending-player Brush battlefield;
- an original held-score battlefield replaced by that Brush battlefield;
- one attacker;
- one defender plus a legal Shadow battle-response source;
- `DECLARE_BATTLE` optional costs of `COMBAT_ASSIGNMENT` and `BRUSH_USE_REPLACED_BATTLEFIELD:<original>`.

The test confirms:

- response opens before battle result / replacement / score;
- the Brush optional cost survives response open and response close;
- the resumed battle emits `BATTLEFIELD_REPLACEMENT_APPLIED`;
- the held-score branch uses the original held-score battlefield id for trigger / cost / score;
- the internal carrier remains hidden from player, opponent, spectator, and prompt projections.

## Validation

Focused:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattleDamageAssignmentLifecycleTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~Shadow|FullyQualifiedName~DeclareBattle|FullyQualifiedName~StartBattle|FullyQualifiedName~Priority|FullyQualifiedName~Reaction|FullyQualifiedName~Swift|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

Result:

```text
Passed: 430, Failed: 0, Skipped: 0, Total: 430
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
Passed: 4198, Failed: 0, Skipped: 0, Total: 4198
```

Whitespace:

```sh
git diff --check
```

Result: no output.

## Verdict

4D-02H is accepted as a focused P0-004 test-only narrowing slice. It does not close full official battle lifecycle, P0-005, P1, frontend final gates, card matrix, READY, or the active goal.
