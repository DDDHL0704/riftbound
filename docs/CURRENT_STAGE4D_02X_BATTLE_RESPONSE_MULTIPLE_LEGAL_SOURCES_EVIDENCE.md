# Stage 4D-02X Battle Response Multiple Legal Sources Evidence

日期：2026-05-15
结论：**TARGETED / FOCUSED / ADJACENT / FULL GREEN / PROJECT NOT READY**

## Validation

Targeted new guard:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~NaturalBattleResponseAllowsMultipleLegalSourcesSequentiallyBeforeAdvancement"
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Focused:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~BattleDamageAssignmentLifecycleTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~PendingTaskQueue|FullyQualifiedName~StartBattle|FullyQualifiedName~DeclareBattle|FullyQualifiedName~TriggerPaymentTests|FullyQualifiedName~ShadowActivatedAbilityTests|FullyQualifiedName~RevealCard"
```

Result:

```text
Passed: 279, Failed: 0, Skipped: 0, Total: 279
```

Adjacent:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Battlefield|FullyQualifiedName~BattlefieldContest|FullyQualifiedName~BattlefieldTasks|FullyQualifiedName~DeclareBattle|FullyQualifiedName~StartBattle|FullyQualifiedName~SpellDuel|FullyQualifiedName~BattleResponse|FullyQualifiedName~StackPriority|FullyQualifiedName~RevealCard|FullyQualifiedName~StandbyReaction|FullyQualifiedName~MoveUnit|FullyQualifiedName~ObjectLocation|FullyQualifiedName~GameHub|FullyQualifiedName~TriggerPayment|FullyQualifiedName~PaymentEngine|FullyQualifiedName~PAY_COST|FullyQualifiedName~ShadowActivatedAbilityTests"
```

Result:

```text
Passed: 809, Failed: 0, Skipped: 0, Total: 809
```

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result:

```text
Passed: 4221, Failed: 0, Skipped: 0, Total: 4221
```

Whitespace:

```sh
git diff --check
```

Result: no output.

## Notes

No runtime changes were required. The new guard confirms two ready Shadow sources can be exposed together, consumed sequentially in one battle response window, and prevented from advancing `BF-NEXT` until the response window closes and the current battle resolves.
